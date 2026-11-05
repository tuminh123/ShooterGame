using UnityEngine;

// UI gắn dưới nhân vật: đọc runtime để hiện HP/giáp, không đổi vị trí hoặc scale đã chỉnh trên prefab.
[RequireComponent(typeof(CharacterView))]
public sealed class CharacterUI : MonoBehaviour
{
    #region References & Spawn State

    [SerializeField] private RectTransform vitalsRoot; // Canvas World Space con; script này nằm trên vỏ để vẫn chạy khi Canvas bị ẩn.
    [SerializeField] private BarView healthBar; // View chung, chỉ nhận dữ liệu HP.
    [SerializeField] private BarView shieldBar; // Cùng loại view; ẩn khi giáp bằng 0.
    private CharacterView character; // Vỏ cùng GameObject, cung cấp runtime và trạng thái visual.
    private CharacterRuntimeData runtime; // Giữ đúng lượt spawn để gỡ event trước khi pool tái sử dụng vỏ.
    private bool showsVitals; // Tính lại khi bật: Player hoặc boss mới hiện bar.

    #endregion

    #region Binding & Lifecycle

    private void OnEnable()
    {
        if (character == null) character = GetComponent<CharacterView>();
        Unbind();
        runtime = character.RuntimeData;
        showsVitals = runtime != null && (runtime.Team == CharacterTeam.Player || character.IsBoss);
        if (showsVitals)
        {
            runtime.Changed += RefreshVitals;
            character.VisualVisibilityChanged += OnVisualVisibilityChanged;
            character.Deactivated += Unbind;
        }
        RefreshVitals();
    }

    // ClearSpawn có thể diễn ra trước OnDisable; gỡ mọi tham chiếu của lượt cũ ngay khi được báo.
    private void Unbind()
    {
        if (runtime != null) runtime.Changed -= RefreshVitals;
        if (character != null)
        {
            character.VisualVisibilityChanged -= OnVisualVisibilityChanged;
            character.Deactivated -= Unbind;
        }
        runtime = null;
        showsVitals = false;
        if (vitalsRoot != null) vitalsRoot.gameObject.SetActive(false);
    }

    private void OnDisable() => Unbind();
    private void OnDestroy() => Unbind();

    #endregion

    #region Vitals Presentation

    private void OnVisualVisibilityChanged(bool visible) => RefreshVitals(); // Boss bị cull thì bar cũng ẩn theo.

    private void RefreshVitals()
    {
        if (vitalsRoot == null) return;
        bool visible = showsVitals && runtime != null && runtime.IsAlive && character != null &&
            character.isActiveAndEnabled && ReferenceEquals(character.RuntimeData, runtime) && character.IsVisualVisible;
        vitalsRoot.gameObject.SetActive(visible);
        if (!visible) return;
        healthBar.SetValue(runtime.CurrentHealth, runtime.FinalStats.maxHealth);
        shieldBar.SetValue(runtime.CurrentShield, runtime.MaxShield);
        shieldBar.SetVisible(runtime.CurrentShield > 0f);
    }

    #endregion
}
