using UnityEngine;

// Nhân vật quản lý HP/giáp và áp dụng layout theo cấu hình riêng của từng loại.
[RequireComponent(typeof(CharacterView))]
public sealed class CharacterUI : MonoBehaviour
{
    #region References & Spawn State

    [SerializeField] private RectTransform vitalsRoot; // Canvas World Space con; script này nằm trên vỏ để vẫn chạy khi Canvas bị ẩn.
    [SerializeField] private BarView healthBar; // View chung, chỉ nhận dữ liệu HP.
    private CharacterView character; // Vỏ cùng GameObject, cung cấp runtime và trạng thái visual.
    private CharacterRuntimeData runtime; // Giữ đúng lượt spawn để gỡ event trước khi pool tái sử dụng vỏ.
    private bool showsVitals; // Tính lại khi bật: Player hoặc boss mới hiện bar.

    #endregion

    #region Binding & Lifecycle

    public RectTransform VitalsRoot => vitalsRoot;

    public void ApplyLayout(CharacterVisualSettings settings)
    {
        if (vitalsRoot != null)
        {
            vitalsRoot.localPosition = settings.HealthBarOffset;
            vitalsRoot.localScale = Vector3.one * settings.HealthBarScale;
            vitalsRoot.sizeDelta = settings.HealthBarSize;
        }
        if (healthBar != null) healthBar.ApplyLayout(settings.HealthBarSize, settings.ShieldThickness);
    }

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
        // Bật rõ thanh nhỏ, không chỉ Canvas: thanh con có thể đang tắt trong prefab/Inspector.
        // Lớp giáp là con của healthBar nên không hiện nếu chỉ bật vitalsRoot.
        if (healthBar != null) healthBar.SetVisible(visible);
        if (!visible) return;
        if (healthBar == null)
        {
            Debug.LogError("CharacterUI: chưa gán Health Bar của thanh máu nhỏ trong prefab nhân vật.", this);
            return;
        }
        healthBar.SetValue(runtime.CurrentHealth, runtime.FinalStats.maxHealth);
        healthBar.SetShield(runtime.CurrentShield, runtime.MaxShield);
    }

    #endregion
}
