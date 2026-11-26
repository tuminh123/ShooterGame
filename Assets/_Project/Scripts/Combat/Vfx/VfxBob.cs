using UnityEngine;

/// <summary>
/// Nhún lên xuống một phần của VFX (quả cầu trong aura) theo hình sin quanh vị trí local gốc. Chỉ để trang trí.
/// </summary>
/// <remarks>
/// Thay cho <c>AllIn1VfxBounceAnimation</c>. Bản gốc chụp vị trí <b>thế giới</b> lúc Start nên khi VFX đi theo
/// nhân vật thì quả cầu bị bỏ lại; bản này dùng vị trí local nên đi cùng object cha. Dùng <c>Time.deltaTime</c>
/// (có scale) để dừng theo pause; OnDisable trả vị trí gốc cho lần lấy ra pool sau.
/// </remarks>
public sealed class VfxBob : MonoBehaviour
{
    #region Settings

    [Tooltip("Độ lệch lớn nhất so với vị trí gốc, theo không gian local của object cha.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.5f, 0f);
    [Tooltip("Tốc độ góc của sóng sin (radian mỗi giây).")]
    [SerializeField, Min(0f)] private float speed = 3f;

    #endregion

    #region State

    private Vector3 basePosition; // Vị trí chỉnh trong prefab, chụp một lần.
    private float phase; // Thời gian riêng: bắt đầu từ 0 mỗi lần bật, không phụ thuộc Time.time.

    #endregion

    #region Unity Callbacks

    private void Awake() => basePosition = transform.localPosition;

    private void OnEnable() => phase = 0f;

    private void Update()
    {
        phase += speed * Time.deltaTime;
        // (sin + 1) / 2 chạy trong [0, 1], giống bản gốc: chỉ nhún về phía offset, không vượt qua vị trí gốc.
        transform.localPosition = basePosition + offset * ((Mathf.Sin(phase) + 1f) * 0.5f);
    }

    private void OnDisable() => transform.localPosition = basePosition;

    #endregion
}
