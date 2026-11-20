using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Rung ngang khi bấm vào nút đang bị khóa (MAX, không đủ tiền, chưa mở), báo rõ là "không bấm được".
/// Button bỏ qua click khi bị khóa, nhưng component khác trên cùng object vẫn nhận click nên rung được.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class ButtonDeniedShake : MonoBehaviour, IPointerClickHandler
{
    #region Settings

    [SerializeField] private float strength = 14f;
    [SerializeField, Min(0.05f)] private float duration = 0.3f;
    [SerializeField, Min(1f)] private float frequency = 18f;

    #endregion

    #region State

    private Button button;
    private Tween shake;
    private Vector3 restPosition;
    private bool restCached;

    #endregion

    #region Unity Events

    private void Awake() => button = GetComponent<Button>();

    /// <summary>Chỉ rung khi nút đang bị khóa; nút bấm được thì để Button xử lý như thường.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (button.IsInteractable()) return;
        if (!restCached)
        {
            restPosition = transform.localPosition;
            restCached = true;
        }
        shake.Stop();
        transform.localPosition = restPosition;
        shake = Tween.ShakeLocalPosition(transform, new Vector3(strength, 0f, 0f), duration, frequency, useUnscaledTime: true);
    }

    // Tắt giữa lúc rung vẫn trả nút về chỗ cũ.
    private void OnDisable()
    {
        shake.Stop();
        if (restCached) transform.localPosition = restPosition;
    }

    #endregion
}
