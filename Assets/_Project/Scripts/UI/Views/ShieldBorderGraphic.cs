using UnityEngine;
using UnityEngine.UI;

// Một Graphic có material gán sẵn trong prefab, không cần Image/sprite hay object tạo lúc chạy.
public sealed class ShieldBorderGraphic : MaskableGraphic
{
    [SerializeField, Min(0.5f)] private float thickness = 3f;
    private float ratio;

    public void SetThickness(float value)
    {
        thickness = Mathf.Max(0.5f, value);
        // Padding và độ dày cùng nguồn dữ liệu: đổi Thickness không phải chỉnh RectTransform lần nữa.
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.one * -thickness;
        rectTransform.offsetMax = Vector2.one * thickness;
        SetVerticesDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnableVertexData();
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        EnableVertexData();
    }

    private void EnableVertexData()
    {
        // Truyền kích thước/tỉ lệ bằng UV1 để mọi nhân vật dùng chung material.
        // Material.SetFloat riêng từng bar sẽ làm giáp của các bar ảnh hưởng lẫn nhau
        // hoặc buộc clone material; UV1 tránh cả hai trường hợp đó.
        if (canvas == null) return;
        canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
        canvas.rootCanvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
    }

    public void SetRatio(float value)
    {
        value = Mathf.Clamp01(value);
        if (ratio != value)
        {
            ratio = value;
            SetVerticesDirty();
        }
        // Hết giáp thì ngừng vẽ, không giữ một quad hoàn toàn trong suốt trên GPU.
        enabled = ratio > 0f;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (ratio <= 0f) return;
        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;
        var data = new Vector4(rect.width, rect.height, ratio,
            Mathf.Clamp(thickness, 0.5f, Mathf.Min(rect.width, rect.height) * 0.5f - 0.001f));
        AddVertex(vh, rect.xMin, rect.yMin, Vector2.zero, data);
        AddVertex(vh, rect.xMin, rect.yMax, Vector2.up, data);
        AddVertex(vh, rect.xMax, rect.yMax, Vector2.one, data);
        AddVertex(vh, rect.xMax, rect.yMin, Vector2.right, data);
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }

    private void AddVertex(VertexHelper vh, float x, float y, Vector2 uv, Vector4 data)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = new Vector3(x, y, 0f);
        vertex.color = color;
        vertex.uv0 = new Vector4(uv.x, uv.y, 0f, 0f);
        vertex.uv1 = data;
        vh.AddVert(vertex);
    }
}
