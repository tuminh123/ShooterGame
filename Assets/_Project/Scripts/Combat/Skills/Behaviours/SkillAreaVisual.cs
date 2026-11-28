using UnityEngine;

/// <summary>
/// Visual vẽ bằng mesh cho vùng/lưỡi của skill (aura băng, cưa xoay), không cần sprite riêng.
/// Prefab có thể đặt thêm VFX particle làm object con (ví dụ bông tuyết của aura băng); VFX con được scale theo bán kính
/// cùng mesh và ẩn/hiện cùng mesh.
/// </summary>
/// <remarks>
/// Chỉ là hình ảnh: sát thương và làm chậm thuộc hành vi C# thuần (<see cref="FrostAuraSkill"/>, <see cref="OrbitSawSkill"/>).
/// Được pool tái sử dụng. Không đổi tên class: prefab tham chiếu script qua GUID.
/// Vỏ thụ động: không tự đăng ký tick; khi bật thì tự vào <see cref="SkillAreaVisuals"/> để CombatManager gọi <see cref="LateTick"/>.
/// </remarks>
public sealed class SkillAreaVisual : MonoBehaviour
{
    #region Frame Registration

    private SkillAreaVisuals tracker; // Giữ qua các lượt pool; Initialize gán lại mỗi lượt thuê.
    private int activationFrame = -1;

    private void OnEnable()
    {
        activationFrame = Time.frameCount;
        tracker?.Add(this);
    }

    #endregion

    #region Geometry & Owner Binding
    [SerializeField] private bool sawBlade;
    [Tooltip("Ẩn mesh tự vẽ, chỉ hiện các object con (ví dụ cầu lửa xoay dùng particle con).")]
    [SerializeField] private bool hideMesh;
    [Tooltip("Màu viền của vùng tròn (không áp cho lưỡi cưa). Tâm dùng cùng màu, mờ hơn 5 lần.")]
    [SerializeField] private Color areaColor = new Color(0.25f, 0.75f, 1f, 0.2f);
    private CharacterView owner;
    private bool anchored; // Đứng yên tại anchor (vùng trên đất, báo vùng) thay vì bám chủ.
    private Vector2 anchor;
    private Vector2 offset;
    private float rotation;
    private Mesh mesh;
    private Material material;
    private MeshRenderer meshRenderer;
    private Renderer[] renderers; // Mesh và mọi VFX con trong prefab: ẩn/hiện cùng nhau khi skill tạm dừng.
    private Color baseColor = Color.white; // Màu material gốc, trả lại khi visual được thuê cho skill bám chủ.

    private void Awake() => EnsureGeometry();

    private void EnsureGeometry()
    {
        if (meshRenderer != null) return;
        MeshFilter filter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        // Hai kiểu hình: lưỡi cưa (đặc, có răng, nằm trên nhân vật) và vùng băng (mờ, tròn, nằm dưới nhân vật).
        int sortingOrder;
        Color tint;
        Color centerColor;
        float toothRadius;
        string meshName;
        if (sawBlade)
        {
            sortingOrder = 3;
            tint = new Color(0.85f, 0.9f, 1f, 1f);
            centerColor = new Color(0.25f, 0.35f, 0.45f, 1f);
            toothRadius = 0.72f; // Đỉnh lõm giữa các răng cưa.
            meshName = "Orbit Saw Mesh";
        }
        else
        {
            sortingOrder = -1; // Default layer: băng dưới nhân vật, trên layer Ground.
            tint = areaColor;
            centerColor = new Color(Mathf.Min(1f, areaColor.r + 0.15f), Mathf.Min(1f, areaColor.g + 0.15f), areaColor.b, areaColor.a * 0.2f);
            toothRadius = 1f; // Vùng băng tròn đều, không có răng.
            meshName = "Frost Aura Mesh";
        }

        meshRenderer.sortingOrder = sortingOrder;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        material = new Material(Shader.Find("Sprites/Default"));
        meshRenderer.sharedMaterial = material;
        const int segments = 64;
        var vertices = new Vector3[segments + 1];
        var colors = new Color[segments + 1];
        var triangles = new int[segments * 3];
        colors[0] = centerColor;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            // Cứ 4 đỉnh thì 2 đỉnh lõm vào để tạo răng; vùng băng có toothRadius = 1 nên vẫn tròn.
            float radius = 1f;
            if (i % 4 < 2)
                radius = toothRadius;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            colors[i + 1] = tint;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % segments + 1;
        }
        mesh = new Mesh { name = meshName };
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        filter.sharedMesh = mesh;
        renderers = GetComponentsInChildren<Renderer>(true); // Lấy sau khi đã thêm MeshRenderer để gồm cả mesh.
        baseColor = material.color;
    }

    /// <summary>
    /// Gắn visual cho chủ mới. Thường gọi khi còn tắt (beforeActivate của pool); nếu đang bật thì vào danh sách ngay.
    /// </summary>
    public void Initialize(CharacterView character, float radius, SkillAreaVisuals frameTracker)
    {
        EnsureGeometry();
        tracker = frameTracker ?? throw new System.ArgumentNullException(nameof(frameTracker));
        if (isActiveAndEnabled) tracker.Add(this);
        owner = character;
        anchored = false;
        offset = Vector2.zero;
        rotation = 0f;
        transform.localScale = Vector3.one * radius;
        material.color = baseColor;
        SetVisible(true); // Lượt thuê trước có thể đã ẩn (skill tạm dừng) rồi mới trả về pool.
        ApplyPose();
    }

    /// <summary>
    /// Gắn visual đứng yên tại <paramref name="position"/> (vùng trên đất, báo vùng của đòn từ trời), tô màu <paramref name="color"/>.
    /// Prefab hình tròn: màu nhân với màu đỉnh của mesh (tâm nhạt, viền đậm).
    /// </summary>
    public void InitializeAt(Vector2 position, float radius, Color color, SkillAreaVisuals frameTracker)
    {
        EnsureGeometry();
        tracker = frameTracker ?? throw new System.ArgumentNullException(nameof(frameTracker));
        if (isActiveAndEnabled) tracker.Add(this);
        owner = null;
        anchored = true;
        anchor = position;
        offset = Vector2.zero;
        rotation = 0f;
        transform.localScale = Vector3.one * radius;
        material.color = new Color(color.r * 4f, color.g * 4f, color.b * 4f, color.a * 4f); // Mesh vùng có alpha đỉnh 0.2–0.04.
        SetVisible(true);
        ApplyPose();
    }

    /// <summary>Đổi màu (độ mờ dần của vùng báo, vòng sóng…).</summary>
    public void SetColor(Color color) => material.color = new Color(color.r * 4f, color.g * 4f, color.b * 4f, color.a * 4f);

    /// <summary>Dời tâm của visual đứng yên (vòng sóng bám chủ khi đang lan…).</summary>
    public void SetAnchor(Vector2 position)
    {
        anchor = position;
        ApplyPose();
    }

    public void SetRadius(float radius) => transform.localScale = Vector3.one * radius;
    public void SetPose(Vector2 localOffset, float angle)
    {
        offset = localOffset;
        rotation = angle;
    }
    /// <summary>Ẩn/hiện mesh cùng mọi VFX con; không tắt GameObject để vẫn giữ đăng ký tick và vị trí bám chủ.</summary>
    public void SetVisible(bool visible)
    {
        if (renderers == null) return;
        foreach (Renderer part in renderers)
            if (part != null) part.enabled = visible && !(hideMesh && part == meshRenderer);
    }

    /// <summary>Bám theo chủ sau khi di chuyển đã áp dụng. <see cref="SkillAreaVisuals"/> gọi mỗi frame.</summary>
    internal void LateTick()
    {
        if (!isActiveAndEnabled || Time.frameCount <= activationFrame) return;
        ApplyPose();
    }

    private void ApplyPose()
    {
        // Visual không làm con của character: cleanup không reparent một hierarchy đang bị Unity hủy.
        // Theo vị trí mới sau movement; quỹ đạo không bị flip/scale của sprite nhân vật ảnh hưởng.
        if (anchored)
        {
            transform.SetPositionAndRotation(new Vector3(anchor.x, anchor.y, 0f), Quaternion.Euler(0f, 0f, rotation));
            return;
        }
        if (owner == null || owner.RuntimeData == null) return;
        Vector3 center = owner.HitPosition;
        transform.SetPositionAndRotation(new Vector3(center.x + offset.x, center.y + offset.y, center.z),
            Quaternion.Euler(0f, 0f, rotation));
    }

    private void OnDisable()
    {
        tracker?.Remove(this);
        owner = null;
        anchored = false;
        offset = Vector2.zero;
    }

    private void OnDestroy()
    {
        OnDisable();
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
    #endregion
}
