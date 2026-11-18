using System;
using System.Runtime.CompilerServices;

/// <summary>
/// Danh tính của <b>một lượt spawn</b> nhân vật, không phải danh tính của GameObject.
/// </summary>
/// <remarks>
/// Cả vỏ <see cref="CharacterView"/> lẫn <see cref="CharacterRuntimeData"/> đều được pool tái sử dụng.
/// Vì vậy giữ reference tới vỏ là chưa đủ: sau khi nhân vật chết và vỏ được spawn lại, reference cũ
/// sẽ trỏ vào một nhân vật khác. Handle ghi lại bộ ba (view, runtime, generation) tại thời điểm tạo.
/// Khi pool tái sử dụng runtime, generation tăng lên, và handle cũ tự động không còn hợp lệ.
///
/// Đây là nơi duy nhất so sánh generation. Code mới không được tự viết lại phép so sánh này.
/// </remarks>
public readonly struct CharacterHandle : IEquatable<CharacterHandle>
{
    #region Fields

    private readonly CharacterView view;
    private readonly CharacterRuntimeData runtime;
    private readonly ulong generation;

    #endregion

    #region Constructors

    /// <summary>Chụp lượt spawn hiện tại của vỏ. Vỏ null hoặc chưa có runtime sẽ tạo ra handle không hợp lệ.</summary>
    public CharacterHandle(CharacterView view)
        : this(view, view != null ? view.RuntimeData : null) { }

    /// <summary>
    /// Dùng khi bên gọi đã giữ sẵn runtime, ví dụ trong lúc factory đang khởi tạo
    /// và <see cref="CharacterView.RuntimeData"/> chưa được gán.
    /// </summary>
    internal CharacterHandle(CharacterView view, CharacterRuntimeData runtime)
    {
        this.view = view;
        this.runtime = runtime;
        generation = runtime != null ? runtime.Generation : 0;
    }

    #endregion

    #region Validity

    /// <summary>
    /// Vỏ gốc, không qua kiểm tra. Chỉ dùng sau khi <see cref="IsCurrent"/> trả về true,
    /// hoặc để trả vỏ về đúng nơi đã thuê nó.
    /// </summary>
    public CharacterView View => view;

    /// <summary>
    /// Lượt spawn này vẫn đang chiếm vỏ: vỏ còn tồn tại, vẫn giữ đúng runtime và runtime chưa bị pool tái sử dụng.
    /// Không quan tâm nhân vật còn sống hay đã chết.
    /// </summary>
    /// <remarks>Dùng cho việc dọn dẹp, vì nhân vật đã chết vẫn cần được despawn đúng lượt.</remarks>
    public bool IsCurrent =>
        view != null && runtime != null &&
        ReferenceEquals(view.RuntimeData, runtime) &&
        runtime.Generation == generation;

    /// <summary>Lượt spawn vẫn đang chiếm vỏ và nhân vật còn HP.</summary>
    /// <remarks>Dùng cho gameplay: chọn mục tiêu, đếm quái còn sống, áp hiệu ứng.</remarks>
    public bool IsAlive => IsCurrent && runtime.IsAlive;

    /// <summary>
    /// Lấy nhân vật nếu nó còn sống <b>và</b> đang hoạt động trong scene.
    /// Vỏ đang nằm trong pool (đã tắt) sẽ trả về false.
    /// </summary>
    public bool TryGetCharacter(out CharacterView character)
    {
        character = view;
        return IsAlive && view.isActiveAndEnabled;
    }

    #endregion

    #region Equality

    // Handle được dùng làm key cho lịch hit và aura. Phải so cả generation
    // để vỏ hoặc runtime được tái sử dụng không nhận nhầm lịch của lượt cũ.

    /// <summary>Hai handle bằng nhau khi cùng vỏ, cùng runtime và cùng generation.</summary>
    public bool Equals(CharacterHandle other) =>
        ReferenceEquals(view, other.view) &&
        ReferenceEquals(runtime, other.runtime) &&
        generation == other.generation;

    /// <inheritdoc/>
    public override bool Equals(object obj) => obj is CharacterHandle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // Dùng hash theo reference: UnityEngine.Object override GetHashCode theo instance ID,
        // nên ở đây dùng RuntimeHelpers để giống hệt quy tắc ReferenceEquals trong Equals.
        unchecked
        {
            int hash = ReferenceEquals(view, null) ? 0 : RuntimeHelpers.GetHashCode(view);
            hash = hash * 397 ^ (ReferenceEquals(runtime, null) ? 0 : RuntimeHelpers.GetHashCode(runtime));
            return hash * 397 ^ generation.GetHashCode();
        }
    }

    #endregion
}
