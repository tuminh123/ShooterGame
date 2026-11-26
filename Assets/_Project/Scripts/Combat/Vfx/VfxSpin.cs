using UnityEngine;

/// <summary>
/// Xoay đều một phần của VFX (đầu đạn, khiên, điểm phát vệt) quanh một trục local. Chỉ để trang trí.
/// </summary>
/// <remarks>
/// Thay cho script xoay demo của AllIn1 khi chép prefab về dự án, để prefab không phụ thuộc code bên thứ ba.
/// Dùng <c>Time.deltaTime</c> (có scale) nên dừng theo pause của gameplay. Không cấp phát, không Destroy,
/// nên dùng được với pool; OnDisable trả góc gốc để lần lấy ra pool sau bắt đầu giống nhau.
/// </remarks>
public sealed class VfxSpin : MonoBehaviour
{
    #region Settings

    [Tooltip("Trục xoay trong không gian local của object này.")]
    [SerializeField] private Vector3 axis = Vector3.up;
    [Tooltip("Độ mỗi giây; âm để xoay ngược chiều.")]
    [SerializeField] private float degreesPerSecond = 180f;

    #endregion

    #region State

    private Quaternion baseRotation; // Góc chỉnh trong prefab, chụp một lần.

    #endregion

    #region Unity Callbacks

    private void Awake() => baseRotation = transform.localRotation;

    private void Update() => transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);

    private void OnDisable() => transform.localRotation = baseRotation;

    #endregion
}
