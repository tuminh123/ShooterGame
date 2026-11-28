using UnityEngine;

/// <summary>
/// Đường thẳng phát sáng vẽ bằng LineRenderer tạo trong code (sét nảy, tia laser), không cần texture riêng.
/// </summary>
/// <remarks>
/// Được pool tái sử dụng; <see cref="CombatVfx"/> thuê, đếm thời gian và trả về. Tia kéo dài (Beam) giữ tham chiếu kèm
/// <see cref="Version"/> để cập nhật điểm đầu/cuối mỗi frame và biết khi nào visual đã bị thu hồi.
/// </remarks>
public sealed class SkillLineVisual : MonoBehaviour
{
    private LineRenderer line;
    private Material material;
    private Color color;
    private float duration, remaining;

    /// <summary>Tăng mỗi lượt thuê: tham chiếu cũ so version để không sửa visual đã sang lượt khác.</summary>
    public int Version { get; private set; }

    private void Awake() => EnsureLine();

    private void EnsureLine()
    {
        if (line != null) return;
        line = gameObject.GetComponent<LineRenderer>();
        if (line == null) line = gameObject.AddComponent<LineRenderer>();
        material = new Material(Shader.Find("Sprites/Default"));
        line.sharedMaterial = material;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.numCapVertices = 2;
        line.sortingOrder = 4; // Trên nhân vật.
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    /// <summary>Bắt đầu lượt hiển thị: nối hai điểm, mờ dần trong <paramref name="seconds"/> giây.</summary>
    internal void Show(Vector3 from, Vector3 to, Color lineColor, float width, float seconds)
    {
        EnsureLine();
        Version++;
        color = lineColor;
        duration = remaining = Mathf.Max(0.01f, seconds);
        line.startWidth = line.endWidth = width;
        SetPoints(from, to);
        Apply(1f);
    }

    /// <summary>Dời hai đầu (tia bám theo chủ).</summary>
    public void SetPoints(Vector3 from, Vector3 to)
    {
        line.SetPosition(0, new Vector3(from.x, from.y, 0f));
        line.SetPosition(1, new Vector3(to.x, to.y, 0f));
    }

    /// <summary>Giữ visual thêm <paramref name="seconds"/> giây ở độ sáng đầy (tia đang bắn).</summary>
    public void Hold(float seconds)
    {
        remaining = duration = Mathf.Max(remaining, seconds);
        Apply(1f);
    }

    /// <summary>Trừ thời gian; trả true khi đã mờ hết (CombatVfx trả về pool).</summary>
    internal bool Advance(float deltaTime)
    {
        remaining -= deltaTime;
        if (remaining <= 0f) return true;
        Apply(remaining / duration);
        return false;
    }

    private void Apply(float alpha)
    {
        Color faded = new Color(color.r, color.g, color.b, color.a * alpha);
        line.startColor = line.endColor = faded;
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
