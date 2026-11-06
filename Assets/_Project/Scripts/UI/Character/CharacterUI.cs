using UnityEngine;

// UI gắn dưới nhân vật: đọc runtime để hiện HP/giáp, không đổi vị trí hoặc scale đã chỉnh trên prefab.
[RequireComponent(typeof(CharacterView))]
public sealed class CharacterUI : MonoBehaviour
{
    #region References & Spawn State

    [SerializeField] private RectTransform vitalsRoot; // Canvas World Space con; script này nằm trên vỏ để vẫn chạy khi Canvas bị ẩn.
    [SerializeField] private BarView healthBar; // View chung, chỉ nhận dữ liệu HP.
    private CharacterView character; // Vỏ cùng GameObject, cung cấp runtime và trạng thái visual.
    private CharacterRuntimeData runtime; // Giữ đúng lượt spawn để gỡ event trước khi pool tái sử dụng vỏ.
    private bool showsVitals; // Tính lại khi bật: Player hoặc boss mới hiện bar.
    private BarView playerHealthBar; // UIManager chỉ nối view HUD; nhân vật sở hữu việc cập nhật HP/giáp.

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
        if (playerHealthBar != null) playerHealthBar.SetVisible(false);
    }

    private void OnDisable() => Unbind();
    private void OnDestroy() => Unbind();

    #endregion

    #region Vitals Presentation

    public void SetPlayerHealthBar(BarView view)
    {
        if (playerHealthBar != null) playerHealthBar.SetVisible(false);
        playerHealthBar = view;
        RefreshVitals();
    }

    private void OnVisualVisibilityChanged(bool visible) => RefreshVitals(); // Boss bị cull thì bar cũng ẩn theo.

    private void RefreshVitals()
    {
        // HUD của player độc lập với culling của visual và thanh máu world space.
        bool hasPlayer = runtime != null && runtime.Team == CharacterTeam.Player &&
            isActiveAndEnabled && character != null && character.isActiveAndEnabled &&
            ReferenceEquals(character.RuntimeData, runtime);
        if (playerHealthBar != null)
        {
            playerHealthBar.SetVisible(hasPlayer);
            if (hasPlayer)
            {
                playerHealthBar.SetValue(runtime.CurrentHealth, runtime.FinalStats.maxHealth);
                playerHealthBar.SetShield(runtime.CurrentShield, runtime.MaxShield);
            }
        }
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
