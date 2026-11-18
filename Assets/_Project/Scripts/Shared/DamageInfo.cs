using System;

/// <summary>
/// Một đòn sát thương <b>đã được bên gây sát thương tính xong</b>, gửi tới bên nhận.
/// </summary>
/// <remarks>
/// Bên gây sát thương chịu trách nhiệm tính lượng sát thương, bao gồm cả chí mạng: mỗi đòn chỉ tung
/// xác suất chí mạng một lần, và phần tỉ lệ chí mạng vượt quá 100% được cộng thêm vào sát thương chí mạng
/// (xem <see cref="CharacterRuntimeData.CreateDamage(float)"/>).
/// Bên nhận chỉ trừ giáp/HP theo con số này, không tính lại. Như vậy công thức sát thương
/// chỉ nằm ở một phía, và bên nhận không cần biết nguồn gây sát thương là skill, đạn hay hiệu ứng.
/// </remarks>
public readonly struct DamageInfo
{
    #region Properties

    /// <summary>Lượng sát thương cuối cùng, không âm.</summary>
    public float Amount { get; }

    /// <summary>
    /// Phe của bên gây sát thương. Bên nhận dùng nó để bỏ qua đòn cùng phe; đạn và hiệu ứng
    /// dùng nó để chọn phe đối địch làm mục tiêu.
    /// </summary>
    public CharacterTeam SourceTeam { get; }

    /// <summary>
    /// Đòn có chí mạng hay không. Phần thưởng chí mạng đã được cộng sẵn vào <see cref="Amount"/>.
    /// Hiện chưa có nơi nào đọc giá trị này; nó được giữ lại cho hiển thị số sát thương sau này.
    /// </summary>
    public bool IsCritical { get; }

    #endregion

    #region Constructor

    /// <summary>Tạo một đòn sát thương.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Khi <paramref name="amount"/> âm, NaN hoặc vô cực. Chặn ngay tại nguồn, vì một giá trị NaN
    /// lọt vào HP sẽ làm hỏng mọi phép so sánh sau đó (nhân vật không bao giờ chết).
    /// </exception>
    public DamageInfo(float amount, CharacterTeam sourceTeam, bool isCritical = false)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount));

        Amount = amount;
        SourceTeam = sourceTeam;
        IsCritical = isCritical;
    }

    #endregion
}

/// <summary>Kết quả sau khi bên nhận đã trừ một <see cref="DamageInfo"/> vào giáp và HP.</summary>
/// <remarks>
/// Đòn chỉ làm mất giáp vẫn tính là <see cref="Accepted"/>. Nhờ vậy đạn vẫn bị tiêu hao, skill vẫn tính
/// là trúng và hiệu ứng trúng đòn vẫn phát, kể cả khi HP chưa giảm.
/// </remarks>
public readonly struct DamageResult
{
    #region Properties

    /// <summary>Lượng HP thực sự bị mất.</summary>
    public float HealthLost { get; }

    /// <summary>Lượng giáp thực sự bị mất.</summary>
    public float ShieldLost { get; }

    /// <summary>Tổng lượng mất của cả giáp và HP.</summary>
    public float TotalLost => HealthLost + ShieldLost;

    /// <summary>Đòn có gây ảnh hưởng hay không (mất giáp hoặc mất HP).</summary>
    public bool Accepted => TotalLost > 0f;

    /// <summary>Đòn này làm nhân vật chết.</summary>
    public bool Killed { get; }

    #endregion

    #region Constructor

    /// <summary>Tạo kết quả trúng đòn. Chỉ bên nhận sát thương tạo struct này.</summary>
    public DamageResult(float healthLost, float shieldLost, bool killed)
    {
        HealthLost = healthLost;
        ShieldLost = shieldLost;
        Killed = killed;
    }

    #endregion
}
