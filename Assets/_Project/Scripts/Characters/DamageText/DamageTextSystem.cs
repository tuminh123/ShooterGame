using System;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using Object = UnityEngine.Object;

/// <summary>
/// Số sát thương bay lên trên nhân vật trúng đòn. Mọi số cùng nằm trong <b>một mesh</b> (1 draw call), dựng lại mỗi LateTick
/// từ mảng struct cố định, và dùng hình chữ của font SDF có sẵn: sau khi khởi tạo không cấp phát gì nữa.
/// </summary>
/// <remarks>
/// Do <see cref="CharacterManager"/> tạo và tick (LateTick, sau khi di chuyển đã áp dụng). Nguồn duy nhất là
/// <see cref="CharacterView.NotifyDamageReceived"/>: mọi loại sát thương (đạn, chém, skill, cháy/độc) đều đi qua đó,
/// nên không bỏ sót nguồn nào và miền Combat không cần biết gì về số hiển thị.
/// Chuyển động tính thẳng từ thời gian (không tween từng số); thời gian game nên pause thì số đứng yên.
/// </remarks>
public sealed class DamageTextSystem : IDisposable
{
    #region Types

    /// <summary>Các ký tự có thể vẽ; style kiểm tra font có đủ.</summary>
    public const string Characters = "0123456789.KM!";

    private const int MaxChars = 6; // "999.9K" hoặc "99.9M!" — đủ cho mọi số đã rút gọn.

    private struct Entry
    {
        public bool Active;
        public CharacterHandle Target; // Gộp số và bám theo đúng lượt spawn, không theo vỏ đã tái sử dụng.
        public Vector3 Anchor; // Vị trí nhân vật: cập nhật mỗi frame khi còn sống, chết/thu hồi thì đứng ở chỗ cuối.
        public Vector3 Offset; // Từ gốc nhân vật lên trên hitbox (tính lúc trúng đòn).
        public float Start; // Mốc của chuyển động bay lên.
        public float PopStart; // Mốc của cú nảy (gộp số thì nảy lại).
        public float LastHit; // Mốc tính thời gian sống và mờ dần: gộp số thì sống thêm.
        public float Value;
        public bool Critical;
        public Color32 Color;
        public float Drift;
        public int Length;
    }

    // Hình một ký tự trong atlas: khung quad (đơn vị font, tính từ bút) và UV, đã cộng padding của SDF.
    private struct GlyphQuad
    {
        public bool Valid;
        public float Left, Bottom, Right, Top, Advance;
        public Vector2 UvMin, UvMax;
    }

    #endregion

    #region Fields

    private readonly DamageTextStyleSO style;
    private readonly Entry[] entries;
    private readonly char[] text; // MaxChars ô cho mỗi entry.
    private readonly GlyphQuad[] glyphs = new GlyphQuad[128];
    private readonly float capHeight; // Chiều cao chữ số (đơn vị font): Size của style ứng với giá trị này.

    private readonly GameObject root;
    private readonly Mesh mesh;
    private readonly Vector3[] vertices;
    private readonly Color32[] colors;
    private readonly Vector2[] uv0;
    private readonly Vector2[] uv1; // .y = tỉ lệ SDF (world unit mỗi pixel atlas), shader TMP cần để vẽ cạnh sắc.
    private readonly int[] indices;
    private readonly char[] digits = new char[12];
    private bool meshEmpty = true;
    private bool disposed;

    #endregion

    #region Construction

    /// <summary>Đọc hình chữ số từ font và tạo sẵn mọi buffer + mesh; sau đây không cấp phát nữa.</summary>
    public DamageTextSystem(DamageTextStyleSO style)
    {
        this.style = style != null ? style : throw new ArgumentNullException(nameof(style));
        style.Validate();
        entries = new Entry[style.MaxCount];
        text = new char[style.MaxCount * MaxChars];
        int maxQuads = style.MaxCount * MaxChars;
        vertices = new Vector3[maxQuads * 4];
        colors = new Color32[maxQuads * 4];
        uv0 = new Vector2[maxQuads * 4];
        uv1 = new Vector2[maxQuads * 4];
        indices = new int[maxQuads * 6];
        for (int quad = 0; quad < maxQuads; quad++)
        {
            int v = quad * 4, i = quad * 6;
            indices[i] = v; indices[i + 1] = v + 1; indices[i + 2] = v + 2;
            indices[i + 3] = v + 2; indices[i + 4] = v + 3; indices[i + 5] = v;
        }

        TMP_FontAsset font = style.Font;
        capHeight = font.faceInfo.capLine > 0f ? font.faceInfo.capLine : font.faceInfo.pointSize * 0.7f;
        float padding = ShaderUtilities.GetPadding(style.Material, false, false);
        float atlasWidth = font.atlasWidth, atlasHeight = font.atlasHeight;
        foreach (char character in Characters)
        {
            if (!font.characterLookupTable.TryGetValue(character, out TMP_Character entry) || entry.glyph == null) continue;
            GlyphRect rect = entry.glyph.glyphRect;
            GlyphMetrics metrics = entry.glyph.metrics;
            glyphs[character] = new GlyphQuad
            {
                Valid = true,
                Left = metrics.horizontalBearingX - padding,
                Right = metrics.horizontalBearingX + metrics.width + padding,
                Top = metrics.horizontalBearingY + padding,
                Bottom = metrics.horizontalBearingY - metrics.height - padding,
                Advance = metrics.horizontalAdvance,
                UvMin = new Vector2((rect.x - padding) / atlasWidth, (rect.y - padding) / atlasHeight),
                UvMax = new Vector2((rect.x + rect.width + padding) / atlasWidth, (rect.y + rect.height + padding) / atlasHeight)
            };
        }

        root = new GameObject("DamageText");
        mesh = new Mesh { name = "DamageText" };
        mesh.MarkDynamic();
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = style.Material;
        renderer.sortingLayerName = style.SortingLayer;
        renderer.sortingOrder = style.SortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    #endregion

    #region Show

    /// <summary>
    /// Hiện số cho một đòn đã trừ xong. Cùng mục tiêu trúng liên tiếp trong Merge Window thì cộng dồn vào số đang hiện.
    /// </summary>
    /// <remarks>
    /// Hiện <b>sát thương của đòn</b> (<see cref="DamageInfo.Amount"/>, đã gồm chí mạng), không phải HP thực mất:
    /// đòn kết liễu bị cắt theo HP còn lại sẽ làm đòn chí mạng trông y như đòn thường.
    /// </remarks>
    public void Show(CharacterView target, DamageInfo damage, DamageResult result)
    {
        if (disposed || target == null || target.RuntimeData == null || !result.Accepted) return;
        float now = Time.time;
        var handle = new CharacterHandle(target);
        bool critical = damage.IsCritical && target.RuntimeData.Team != CharacterTeam.Player;
        float amount = damage.Amount;

        int index = FindMergeable(handle, critical, now);
        if (index >= 0)
        {
            ref Entry merged = ref entries[index];
            // Không đặt lại mốc bay lên (số không giật về đầu); chỉ nảy lại và sống thêm.
            merged.PopStart = now;
            merged.LastHit = now;
            merged.Value += amount;
            merged.Length = Format(index, merged.Value, critical);
            return;
        }

        index = FreeSlot();
        ref Entry entry = ref entries[index];
        entry.Active = true;
        entry.Target = handle;
        entry.Anchor = target.transform.position;
        entry.Offset = target.HitPosition - entry.Anchor + Vector3.up * style.HeadOffset;
        entry.Start = entry.PopStart = entry.LastHit = now;
        entry.Value = amount;
        entry.Critical = critical;
        entry.Color = PickColor(target, result, critical);
        entry.Drift = UnityEngine.Random.Range(-style.Drift, style.Drift);
        entry.Length = Format(index, entry.Value, critical);
    }

    /// <summary>Xóa mọi số (về Home, chơi lại) để không còn số đứng yên trên màn hình.</summary>
    public void Clear()
    {
        for (int i = 0; i < entries.Length; i++) entries[i].Active = false;
        if (!disposed && !meshEmpty)
        {
            mesh.Clear();
            meshEmpty = true;
        }
    }

    private Color32 PickColor(CharacterView target, DamageResult result, bool critical)
    {
        if (target.RuntimeData.Team == CharacterTeam.Player) return style.PlayerHit;
        if (result.HealthLost <= 0f) return style.ShieldHit;
        return critical ? style.Critical : style.EnemyHit;
    }

    private int FindMergeable(CharacterHandle handle, bool critical, float now)
    {
        if (style.MergeWindow <= 0f) return -1;
        for (int i = 0; i < entries.Length; i++)
        {
            ref Entry entry = ref entries[i];
            if (entry.Active && entry.Critical == critical && now - entry.LastHit <= style.MergeWindow && entry.Target.Equals(handle))
                return i;
        }
        return -1;
    }

    // Ô trống đầu tiên; đầy thì thay số cũ nhất.
    private int FreeSlot()
    {
        int oldest = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            if (!entries[i].Active) return i;
            if (entries[i].LastHit < entries[oldest].LastHit) oldest = i;
        }
        return oldest;
    }

    #endregion

    #region Format

    // 1-999 → "123"; từ 1.000 → "1.2K" / "123K"; từ 1.000.000 → "1.2M". Chí mạng thêm "!". Không tạo string.
    private int Format(int index, float value, bool critical)
    {
        int offset = index * MaxChars, length = 0;
        char suffix = '\0';
        float shown = value;
        if (value >= 999999.5f) { shown = value / 1000000f; suffix = 'M'; }
        else if (value >= 999.5f) { shown = value / 1000f; suffix = 'K'; }

        if (suffix != '\0' && shown < 99.95f)
        {
            int tenths = Mathf.RoundToInt(shown * 10f);
            length = WriteInteger(offset, length, tenths / 10);
            if (tenths % 10 != 0) { text[offset + length++] = '.'; text[offset + length++] = (char)('0' + tenths % 10); }
        }
        else length = WriteInteger(offset, length, Mathf.Max(1, Mathf.RoundToInt(shown)));

        if (suffix != '\0' && length < MaxChars) text[offset + length++] = suffix;
        if (critical && length < MaxChars) text[offset + length++] = '!';
        return length;
    }

    private int WriteInteger(int offset, int length, int value)
    {
        int count = 0;
        do { digits[count++] = (char)('0' + value % 10); value /= 10; } while (value > 0 && count < digits.Length);
        while (count > 0 && length < MaxChars) text[offset + length++] = digits[--count];
        return length;
    }

    #endregion

    #region LateTick (dựng mesh)

    /// <summary>Cập nhật vị trí, cỡ, độ mờ của mọi số và dựng lại mesh. CharacterManager gọi trong LateTick.</summary>
    public void LateTick()
    {
        if (disposed) return;
        float now = Time.time;
        int quads = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            ref Entry entry = ref entries[i];
            if (!entry.Active) continue;
            float age = now - entry.LastHit;
            if (age >= style.Lifetime || age < 0f)
            {
                entry.Active = false;
                continue;
            }
            // Bám theo nhân vật khi còn sống; chết/thu hồi thì handle hết hiệu lực và số đứng ở chỗ cuối.
            if (entry.Target.TryGetCharacter(out CharacterView target)) entry.Anchor = target.transform.position;
            quads = Append(i, ref entry, now, age / style.Lifetime, quads);
        }

        if (quads == 0)
        {
            if (!meshEmpty) mesh.Clear();
            meshEmpty = true;
            return;
        }
        mesh.Clear();
        int vertexCount = quads * 4;
        mesh.SetVertices(vertices, 0, vertexCount);
        mesh.SetColors(colors, 0, vertexCount);
        mesh.SetUVs(0, uv0, 0, vertexCount);
        mesh.SetUVs(1, uv1, 0, vertexCount);
        mesh.SetIndices(indices, 0, quads * 6, MeshTopology.Triangles, 0, true);
        meshEmpty = false;
    }

    // Bay lên chậm dần (ease out) và trôi ngang theo hướng ngẫu nhiên của số.
    private Vector3 Position(ref Entry entry, float now)
    {
        float t = Mathf.Clamp01((now - entry.Start) / style.Lifetime);
        float ease = 1f - (1f - t) * (1f - t) * (1f - t);
        return entry.Anchor + entry.Offset + new Vector3(entry.Drift * t, style.Rise * ease, 0f);
    }

    // Nảy: nhỏ → quá cỡ → về cỡ thường; chí mạng nảy mạnh hơn.
    private float PopScale(ref Entry entry, float now)
    {
        float duration = style.PopDuration;
        float peak = entry.Critical ? style.PopScale + 0.25f : style.PopScale;
        float age = now - entry.PopStart;
        if (age < duration) return Mathf.Lerp(0.3f, peak, age / duration);
        if (age < duration * 2f) return Mathf.Lerp(peak, 1f, (age - duration) / duration);
        return 1f;
    }

    private int Append(int index, ref Entry entry, float now, float t, int quads)
    {
        float size = style.Size * (entry.Critical ? style.CriticalScale : 1f) * PopScale(ref entry, now);
        float scale = size / capHeight; // World unit cho mỗi đơn vị font.
        float alpha = t <= style.FadeStart ? 1f : 1f - (t - style.FadeStart) / Mathf.Max(0.0001f, 1f - style.FadeStart);
        Color32 color = entry.Color;
        color.a = (byte)(color.a * Mathf.Clamp01(alpha));
        Vector3 center = Position(ref entry, now);

        int offset = index * MaxChars;
        float width = 0f;
        for (int c = 0; c < entry.Length; c++) width += glyphs[text[offset + c]].Advance;
        float pen = -width * 0.5f; // Căn giữa theo chiều ngang.
        float baseline = -capHeight * 0.5f; // Căn giữa theo chiều cao chữ số.

        for (int c = 0; c < entry.Length; c++)
        {
            GlyphQuad glyph = glyphs[text[offset + c]];
            if (!glyph.Valid) continue;
            int v = quads * 4;
            float left = center.x + (pen + glyph.Left) * scale, right = center.x + (pen + glyph.Right) * scale;
            float bottom = center.y + (baseline + glyph.Bottom) * scale, top = center.y + (baseline + glyph.Top) * scale;
            vertices[v] = new Vector3(left, bottom, center.z);
            vertices[v + 1] = new Vector3(left, top, center.z);
            vertices[v + 2] = new Vector3(right, top, center.z);
            vertices[v + 3] = new Vector3(right, bottom, center.z);
            uv0[v] = glyph.UvMin;
            uv0[v + 1] = new Vector2(glyph.UvMin.x, glyph.UvMax.y);
            uv0[v + 2] = glyph.UvMax;
            uv0[v + 3] = new Vector2(glyph.UvMax.x, glyph.UvMin.y);
            var sdf = new Vector2(0f, scale);
            uv1[v] = uv1[v + 1] = uv1[v + 2] = uv1[v + 3] = sdf;
            colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = color;
            pen += glyph.Advance;
            quads++;
        }
        return quads;
    }

    #endregion

    #region Dispose

    /// <summary>Hủy mesh và object vẽ.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Destroy(mesh);
        Destroy(root);
    }

    private static void Destroy(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }

    #endregion
}
