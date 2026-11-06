using UnityEngine;

// * Tham chiếu đến một lượt spawn, không chỉ đến GameObject mà pool có thể tái sử dụng.
// * Runtime và generation cùng tạo danh tính; giữ đúng mục tiêu khi cả GameObject lẫn runtime được pool.
public readonly struct CharacterTarget
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
}
