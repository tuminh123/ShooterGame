using UnityEngine;

// * Tham chiếu đến một lượt spawn, không chỉ đến GameObject mà pool có thể tái sử dụng.
// * Runtime mới mỗi lượt spawn đóng vai trò danh tính; không cần thêm ID vào template SO.
public readonly struct CharacterTarget
{
    private readonly CharacterView view;
    private readonly CharacterRuntimeData runtime;

    public CharacterTarget(CharacterView view)
    {
        this.view = view;
        runtime = view != null ? view.RuntimeData : null;
    }

    internal CharacterTarget(CharacterView view, CharacterRuntimeData runtime)
    {
        this.view = view;
        this.runtime = runtime;
    }

    public bool TryGetCharacter(out CharacterView character)
    {
        character = view;
        return view != null && view.isActiveAndEnabled && runtime != null &&
            ReferenceEquals(view.RuntimeData, runtime) && runtime.IsAlive;
    }
}
