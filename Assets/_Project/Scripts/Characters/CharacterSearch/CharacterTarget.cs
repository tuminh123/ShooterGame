using UnityEngine;

// * Tham chiếu đến một lượt spawn, không chỉ đến GameObject mà pool có thể tái sử dụng.
// * Runtime và generation cùng tạo danh tính; giữ đúng mục tiêu khi cả GameObject lẫn runtime được pool.
public readonly struct CharacterTarget : System.IEquatable<CharacterTarget>
{
    private readonly CharacterView view;
    private readonly CharacterRuntimeData runtime;
    private readonly ulong generation;

    public CharacterTarget(CharacterView view)
    {
        this.view = view;
        runtime = view != null ? view.RuntimeData : null;
        generation = runtime != null ? runtime.Generation : 0;
    }

    internal CharacterTarget(CharacterView view, CharacterRuntimeData runtime)
    {
        this.view = view;
        this.runtime = runtime;
        generation = runtime != null ? runtime.Generation : 0;
    }

    public bool TryGetCharacter(out CharacterView character)
    {
        character = view;
        return view != null && view.isActiveAndEnabled && runtime != null &&
            ReferenceEquals(view.RuntimeData, runtime) && runtime.Generation == generation && runtime.IsAlive;
    }

    // Dùng làm key cho lịch hit/aura: so cả generation để vỏ/runtime tái sử dụng không nhận lịch cũ.
    public bool Equals(CharacterTarget other) => ReferenceEquals(view, other.view) &&
        ReferenceEquals(runtime, other.runtime) && generation == other.generation;
    public override bool Equals(object obj) => obj is CharacterTarget other && Equals(other);
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = ReferenceEquals(view, null) ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(view);
            hash = hash * 397 ^ (ReferenceEquals(runtime, null) ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(runtime));
            return hash * 397 ^ generation.GetHashCode();
        }
    }
}
