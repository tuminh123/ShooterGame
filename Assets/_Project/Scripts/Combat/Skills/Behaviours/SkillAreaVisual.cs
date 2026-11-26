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
    private CharacterView owner;
    private Vector2 offset;
    private float rotation;
    private Mesh mesh;
    private Material material;
    private MeshRenderer meshRenderer;
    private Renderer[] renderers; // Mesh và mọi VFX con trong prefab: ẩn/hiện cùng nhau khi skill tạm dừng.

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
            tint = new Color(0.25f, 0.75f, 1f, 0.2f);
            centerColor = new Color(0.4f, 0.9f, 1f, 0.04f);
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
        offset = Vector2.zero;
        rotation = 0f;
        transform.localScale = Vector3.one * radius;
        SetVisible(true); // Lượt thuê trước có thể đã ẩn (skill tạm dừng) rồi mới trả về pool.
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
            if (part != null) part.enabled = visible;
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
        if (owner == null || owner.RuntimeData == null) return;
        Vector3 center = owner.HitPosition;
        transform.SetPositionAndRotation(new Vector3(center.x + offset.x, center.y + offset.y, center.z),
            Quaternion.Euler(0f, 0f, rotation));
    }

    private void OnDisable()
    {
        tracker?.Remove(this);
        owner = null;
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
