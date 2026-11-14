using System;
using UnityEngine;

// Cấu hình local của từng loại nhân vật; vỏ pool giữ scale 1, không scale chung UI và hitbox.
[Serializable]
public struct CharacterVisualSettings
{
    [Tooltip("Prefab ngoại hình con của nhân vật; không phải vỏ CharacterView. Bao gồm sprite/animation của mọi bộ phận.")]
    public GameObject Prefab;
    [Tooltip("Nhân với scale gốc của visual prefab; không tự đổi hitbox.")]
    public Vector3 Scale;
    [Tooltip("Offset cộng vào vị trí local gốc của visual prefab, theo đơn vị local; không tự dịch hitbox hoặc thanh máu.")]
    public Vector3 Offset;
    [Tooltip("Hình chữ nhật va chạm local của vỏ nhân vật, độc lập kích thước sprite và visual scale.")]
    public HitboxData Hitbox;
    [Tooltip("Kích thước thanh máu theo đơn vị UI trước khi đổi sang world space.")]
    public Vector2 HealthBarSize;
    [Tooltip("Vị trí thanh máu theo local của vỏ nhân vật.")]
    public Vector3 HealthBarOffset;
    [Tooltip("Hệ số đổi bố cục thanh máu từ đơn vị UI sang world; phải dương. Tăng làm cả thanh lớn hơn.")]
    [Min(0.0001f)] public float HealthBarScale;
    [Tooltip("Độ dày viền giáp theo đơn vị UI trước khi scale; tối thiểu 0.5.")]
    [Min(0.5f)] public float ShieldThickness;

    public static CharacterVisualSettings Default => new CharacterVisualSettings
    {
        Scale = Vector3.one,
        Hitbox = new HitboxData { Size = new Vector2(0.16f, 0.16f), Offset = new Vector2(0f, 0.08f) },
        HealthBarSize = new Vector2(66.54f, 16.06f),
        HealthBarOffset = new Vector3(0f, -0.04f, 0f),
        HealthBarScale = 0.0035f,
        ShieldThickness = 3f
    };

    public void ApplyVisual(Transform visual)
    {
        visual.localPosition = Prefab.transform.localPosition + Offset;
        visual.localRotation = Prefab.transform.localRotation;
        visual.localScale = Vector3.Scale(Prefab.transform.localScale, Scale);
    }

    public void Validate()
    {
        Hitbox.Validate();
        if (!Finite(Scale.x) || !Finite(Scale.y) || !Finite(Scale.z) ||
            Scale.x <= 0f || Scale.y <= 0f || Scale.z <= 0f ||
            !Finite(Offset.x) || !Finite(Offset.y) || !Finite(Offset.z) ||
            !Finite(HealthBarSize.x) || !Finite(HealthBarSize.y) ||
            HealthBarSize.x <= 0f || HealthBarSize.y <= 0f ||
            !Finite(HealthBarOffset.x) || !Finite(HealthBarOffset.y) || !Finite(HealthBarOffset.z) ||
            !Finite(HealthBarScale) || HealthBarScale <= 0f ||
            !Finite(ShieldThickness) || ShieldThickness < 0.5f)
            throw new InvalidOperationException("Character visual settings must be finite and sizes/scales positive.");
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
