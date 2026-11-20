using System;
using UnityEngine;

/// <summary>
/// Nhân vật hiển thị trên UI, do tool <c>Character UI Exporter</c> xuất từ prefab ngoại hình gameplay.
/// Gồm hai nhánh: <b>Rig</b> (cây xương rỗng, cùng tên/đường dẫn như prefab gốc nên chạy được clip gốc)
/// và <b>Layers</b> (các Image phẳng, xếp theo sortingOrder của prefab gốc).
/// </summary>
/// <remarks>
/// UI vẽ theo thứ tự hierarchy còn gameplay vẽ theo sortingOrder; chép nguyên cây sang UI sẽ vẽ sai
/// (gậy sau lưng, bóng đè người). Vì vậy hình tách khỏi xương, và mỗi frame chép tư thế xương sang hình.
/// </remarks>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class CharacterUIVisual : MonoBehaviour
{
    #region References

    [Tooltip("Animator trên node Rider của Rig; dùng controller riêng cho UI (Idle + Victory).")]
    [SerializeField] private Animator animator;
    [Tooltip("Gốc chứa các Image; tư thế xương được quy đổi về hệ tọa độ của node này.")]
    [SerializeField] private RectTransform layerRoot;
    [Tooltip("Xương nguồn của từng hình, cùng thứ tự với Parts.")]
    [SerializeField] private Transform[] bones = Array.Empty<Transform>();
    [Tooltip("Hình (Image) đi theo xương cùng chỉ số.")]
    [SerializeField] private RectTransform[] parts = Array.Empty<RectTransform>();

    #endregion

    #region Animator States

    // Tên state và trigger của UI_CharacterController (Assets/_Project/Animations/UI).
    private static readonly int IdleState = Animator.StringToHash("Idle");
    private static readonly int CelebrateTrigger = Animator.StringToHash("Celebrate");

    #endregion

    #region Lifecycle

    private void Awake()
    {
        if (animator == null || layerRoot == null || bones.Length != parts.Length)
            throw new InvalidOperationException($"CharacterUIVisual '{name}' is missing references; export it again with Character UI Exporter.");
        // Home và màn Character chạy khi game pause (timeScale = 0); dùng unscaled để nhân vật vẫn thở.
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    // Animator cập nhật xương trước LateUpdate, nên chép tư thế ở đây là tư thế của frame hiện tại.
    private void LateUpdate() => SyncParts();

    #endregion

    #region Public API

    /// <summary>Quay về Idle từ đầu, dùng khi đổi nhân vật đang xem.</summary>
    public void PlayIdle() => animator.Play(IdleState, 0, 0f);

    /// <summary>Phát Victory một lần rồi tự về Idle (chọn nhân vật, nâng cấp thành công).</summary>
    public void Celebrate() => animator.SetTrigger(CelebrateTrigger);

    /// <summary>
    /// Chép tư thế mỗi xương sang hình tương ứng. Public để tool xuất gọi một lần,
    /// giúp prefab đã đúng tư thế ngay cả khi chưa chạy game.
    /// </summary>
    public void SyncParts()
    {
        Matrix4x4 toLayer = layerRoot.worldToLocalMatrix;
        for (int i = 0; i < parts.Length; i++)
        {
            Transform bone = bones[i];
            RectTransform part = parts[i];
            if (bone == null || part == null) continue;
            // Quy đổi qua ma trận để giữ đúng cả vị trí, góc xoay và tỉ lệ (kể cả tỉ lệ âm khi lật).
            Matrix4x4 local = toLayer * bone.localToWorldMatrix;
            part.localPosition = local.GetColumn(3);
            part.localRotation = local.rotation;
            part.localScale = local.lossyScale;
        }
    }

    #endregion

    #region Editor Export

#if UNITY_EDITOR
    /// <summary>Chỉ tool xuất gọi: gán tham chiếu sau khi dựng Rig và Layers.</summary>
    public void SetupForExport(Animator uiAnimator, RectTransform layers, Transform[] sourceBones, RectTransform[] images)
    {
        animator = uiAnimator;
        layerRoot = layers;
        bones = sourceBones;
        parts = images;
    }
#endif

    #endregion
}
