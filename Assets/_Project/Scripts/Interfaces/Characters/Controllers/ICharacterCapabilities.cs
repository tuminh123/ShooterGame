using UnityEngine;

#region Action Permission

/// <summary>Cho phép controller khóa việc thực thi skill mà không tắt vỏ nhân vật.</summary>
public interface ICharacterActionPermission
{
    bool CanAttack { get; }
}

#endregion

#region Contact Completion

/// <summary>Hoàn tất phản ứng tiếp xúc sau khi skill của frame đã xử lý damage.</summary>
public interface ICharacterContactCompletion
{
    void CompletePatternContact(CharacterView view, CharacterRuntimeData identity, ulong generation);
}

#endregion

#if UNITY_EDITOR
#region Separation Gizmos

/// <summary>Vẽ phạm vi tách đàn của controller khi chọn nhân vật trong Editor.</summary>
public interface ICharacterSeparationGizmos
{
    void DrawSeparationGizmo(Vector3 position);
}

#endregion
#endif
