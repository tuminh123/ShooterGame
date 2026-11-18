using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lượt chọn skill khi lên cấp trong trận: đếm số lượt đang chờ, roll lựa chọn, áp dụng lựa chọn qua Combat,
/// và chặn tạm skill bị lỗi. Không biết gì về panel, pause hay slow motion — đó là việc của UI.
/// </summary>
/// <remarks>
/// Do <see cref="RunManager"/> tạo; trước đây toàn bộ logic này nằm trong SkillChoiceController (UI).
/// Lượt chờ gắn với Player hiện tại: đổi Player, Player chết/bị thu hồi hoặc reset lượt đều hủy hết lượt chờ.
/// </remarks>
public sealed class SkillChoice : IDisposable
{
    #region Fields

    private readonly IReadOnlyList<SkillDataSO> catalog; // Skill được phép thưởng (snapshot từ SkillDatabaseSO).
    private readonly ISkillGrant grant;
    private readonly IOwnedSkillReader owned;
    private readonly IPlayerReader players;
    private readonly SkillChoicePool pool = new SkillChoicePool(); // Luật roll: cấp kế tiếp, bỏ skill max và skill bị chặn.
    private readonly SkillChoiceProgress progress = new SkillChoiceProgress(); // Số lượt chưa dùng.
    private readonly HashSet<SkillDataSO> failedSkills = new HashSet<SkillDataSO>(); // Skill lỗi bị chặn tới khi hủy/retry.
    private CharacterView owner;
    private int version; // Tăng mỗi lần hủy: kết quả Choose của phiên cũ không được tiêu lượt mới.
    private bool choosing; // Chặn Choose lồng nhau từ callback gameplay.
    private bool disposed;

    #endregion

    #region Properties / Events

    /// <summary>Số lượt chọn đang chờ.</summary>
    public int Pending => progress.Pending;

    /// <summary>Player nhận skill của các lượt đang chờ; null khi không có Player.</summary>
    public CharacterView Owner => owner;

    /// <summary>Còn lượt chờ và Player còn sống.</summary>
    public bool CanChoose => progress.Pending > 0 && owner != null && owner.RuntimeData != null && owner.RuntimeData.IsAlive;

    /// <summary>(số lượt trước, số lượt sau) khi lên cấp, chọn xong hoặc pool cạn.</summary>
    public event Action<int, int> PendingChanged;

    /// <summary>Mọi lượt chờ bị hủy (reset lượt, đổi Player, Player chết, rời Playing).</summary>
    public event Action Canceled;

    #endregion

    #region Constructor / Lifecycle

    public SkillChoice(IReadOnlyList<SkillDataSO> catalog, ISkillGrant grant, IOwnedSkillReader owned, IPlayerReader players)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.grant = grant ?? throw new ArgumentNullException(nameof(grant));
        this.owned = owned ?? throw new ArgumentNullException(nameof(owned));
        this.players = players ?? throw new ArgumentNullException(nameof(players));
        players.PlayerChanged += OnPlayerChanged;
        AttachOwner(players.CurrentPlayer);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        players.PlayerChanged -= OnPlayerChanged;
        DetachOwner();
        PendingChanged = null;
        Canceled = null;
    }

    #endregion

    #region Queue

    /// <summary>RunManager gọi khi lên cấp: cộng đúng chênh lệch level (lên 2 cấp = 2 lượt).</summary>
    public void AddLevels(int previousLevel, int currentLevel)
    {
        if (disposed) return;
        int before = progress.Pending;
        progress.AddLevels(previousLevel, currentLevel);
        NotifyPending(before);
    }

    /// <summary>Hủy mọi lượt chờ và bỏ chặn skill lỗi; phát <see cref="Canceled"/>.</summary>
    public void Cancel()
    {
        if (disposed) return;
        version++;
        progress.Clear();
        failedSkills.Clear();
        Canceled?.Invoke();
    }

    /// <summary>Bỏ chặn skill lỗi (sau khi sửa dữ liệu); không cấp thêm hay xóa lượt đang chờ.</summary>
    public void RetryFailed() => failedSkills.Clear();

    #endregion

    #region Roll / Choose

    /// <summary>
    /// Roll tối đa 3 lựa chọn từ runtime mới nhất vào <paramref name="result"/>. <paramref name="reroll"/> = true thì
    /// ưu tiên skill khác các lựa chọn đang có trong <paramref name="result"/>.
    /// Không còn ứng viên và không có skill nào bị chặn (mọi skill đã max) thì xóa lượt chờ.
    /// </summary>
    public void Roll(List<SkillChoiceOffer> result, bool reroll)
    {
        if (result == null) throw new ArgumentNullException(nameof(result));
        if (disposed || !CanChoose) { result.Clear(); return; }
        pool.Build(catalog, owned, owner, result, excluded: failedSkills, previousOffers: reroll ? result : null);
        // Pool cạn vì skill lỗi thì giữ lượt để retry sau khi sửa cấu hình.
        if (result.Count == 0 && failedSkills.Count == 0)
        {
            int before = progress.Pending;
            progress.Clear();
            NotifyPending(before);
        }
    }

    /// <summary>
    /// Áp dụng một lựa chọn. Chỉ tiêu một lượt khi gameplay nhận/nâng skill thành công.
    /// Exception thì giữ lượt và chặn skill đó khỏi các lần roll sau (log lỗi, không ném tiếp).
    /// </summary>
    public bool Choose(SkillChoiceOffer offer)
    {
        if (disposed || choosing || !CanChoose) return false;
        choosing = true;
        int revision = version; // Chụp phiên trước khi gameplay có thể phát callback hủy.
        try
        {
            if (!offer.TryApply(grant, owned, owner) || revision != version) return false;
            int before = progress.Pending;
            progress.Consume();
            NotifyPending(before);
            return true;
        }
        catch (Exception exception)
        {
            if (revision == version) failedSkills.Add(offer.Definition);
            Debug.LogException(exception);
            return false;
        }
        finally { choosing = false; }
    }

    #endregion

    #region Private Helpers

    // Lượt chờ thuộc về Player cũ: hủy rồi gắn Player mới.
    private void OnPlayerChanged(CharacterView player)
    {
        DetachOwner();
        Cancel();
        AttachOwner(player);
    }

    private void AttachOwner(CharacterView player)
    {
        owner = player;
        if (owner == null) return;
        owner.Died += OnOwnerEnded;
        owner.Released += OnOwnerEnded;
    }

    private void DetachOwner()
    {
        if (owner != null)
        {
            owner.Died -= OnOwnerEnded;
            owner.Released -= OnOwnerEnded;
        }
        owner = null;
    }

    // Player chết hoặc bị trả pool: không áp dụng lượt chờ cho lượt spawn khác.
    private void OnOwnerEnded(CharacterView character) => Cancel();

    private void NotifyPending(int before)
    {
        if (before != progress.Pending) PendingChanged?.Invoke(before, progress.Pending);
    }

    #endregion
}
