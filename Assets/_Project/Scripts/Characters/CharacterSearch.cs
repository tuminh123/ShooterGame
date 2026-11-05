using System;
using UnityEngine;

// Facade gameplay: chỉ chọn cách tìm qua factory; strategy giữ logic truy vấn và chuyển kết quả.
public sealed class CharacterSearch : ICharacterSearch
{
    #region Dependencies

    private readonly ICharacterSearchFactory factory; // Hợp đồng chọn cách tìm; không phụ thuộc factory hình học hoặc manager cụ thể.

    public CharacterSearch(ICharacterSearchFactory factory) => this.factory = factory;

    #endregion

    #region Team & Pair Helpers

    // Chuyển phe nguồn thành phe mục tiêu trong mô hình Player/Enemy hiện tại.
    public static CharacterTeam OpponentOf(CharacterTeam team)
    {
        if (team == CharacterTeam.Player) return CharacterTeam.Enemy;
        if (team == CharacterTeam.Enemy) return CharacterTeam.Player;
        throw new ArgumentOutOfRangeException(nameof(team));
    }

    // Cặp đã biết thì kiểm tra trực tiếp hai hộp, không cần tìm lại trong toàn bộ grid.
    public static bool Overlaps(CharacterView first, CharacterView second)
    {
        return first != second && new CharacterTarget(first).TryGetCharacter(out _) &&
            new CharacterTarget(second).TryGetCharacter(out _) &&
            SpatialGeometry.BoxesOverlap(first.GetSpatialEntity(), second.GetSpatialEntity());
    }

    #endregion

    #region Strategy Dispatch

    // Điểm vào chung cho mọi strategy trả một mục tiêu.
    public bool TryFind(CharacterSearchType type, CharacterSearchRequest request,
        out CharacterTarget target, out Vector2 hitPosition)
        => factory.GetSingle(type).TryFind(request, out target, out hitPosition);

    // Caller giữ buffer qua nhiều lượt; giá trị trả về là số phần tử hợp lệ trong mảng.
    public int FindAll(CharacterSearchType type, CharacterSearchRequest request, ref CharacterTarget[] results)
        => factory.GetMultiple(type).FindAll(request, ref results);

    #endregion

    #region Convenience Queries

    // Tìm gần nhất; facade chỉ chuẩn bị dữ liệu và debug, strategy quyết định cách tìm.
    public CharacterTarget FindNearest(Vector2 center, float radius, CharacterTeam targetTeam, CharacterView exclude = null)
    {
        var request = new CharacterSearchRequest(center, center, radius, targetTeam, exclude);
        TryFind(CharacterSearchType.Nearest, request, out CharacterTarget target, out _);
        DrawSearchCircle(center, radius);
        return target;
    }

    // Ghi mọi mục tiêu vào mảng của caller, không dùng ArrayPool hoặc wrapper kết quả.
    public int FindAll(Vector2 center, float radius, CharacterTeam targetTeam,
        ref CharacterTarget[] results, CharacterView exclude = null)
    {
        var request = new CharacterSearchRequest(center, center, radius, targetTeam, exclude);
        int count = FindAll(CharacterSearchType.AllInCircle, request, ref results);
        DrawSearchCircle(center, radius);
        return count;
    }

    // Quét đường bay và trả tâm đạn tại điểm trúng đầu tiên.
    public CharacterTarget FindFirstOnPath(Vector2 origin, float radius, Vector2 direction,
        float distance, CharacterTeam targetTeam, out Vector2 hitPosition)
    {
        if (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f)
            throw new ArgumentOutOfRangeException(nameof(distance));
        var request = new CharacterSearchRequest(origin, origin + direction.normalized * distance, radius, targetTeam);
        bool found = TryFind(CharacterSearchType.FirstOnPath, request, out CharacterTarget target, out hitPosition);
        DrawSearchPath(origin, radius, direction, distance, found ? (Vector2?)hitPosition : null);
        return target;
    }

    #endregion

    #region Search Gizmos

    // Query chạy ngoài OnDrawGizmos nên dùng Debug.DrawLine; các lời gọi này bị loại khỏi player build.
    private const float SearchGizmoDuration = 0.1f; // Giữ nét đủ lâu để nhìn thấy giữa hai lượt query.
    private const int SearchGizmoSegments = 32; // Số đoạn thẳng xấp xỉ một vòng tròn debug.

    // Vẽ phạm vi tìm tròn bằng màu vàng.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void DrawSearchCircle(Vector2 center, float radius)
    {
        DrawSearchRing(center, radius, Color.yellow);
    }

    // Vẽ đường quét màu cyan và vùng trúng màu đỏ nếu có kết quả.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void DrawSearchPath(Vector2 origin, float radius, Vector2 direction,
        float distance, Vector2? hitPosition = null)
    {
        Vector2 heading = direction.normalized;
        Vector2 end = origin + heading * distance;
        Vector2 side = new Vector2(-heading.y, heading.x) * radius;
        DrawSearchRing(origin, radius, Color.cyan);
        DrawSearchRing(end, radius, Color.cyan);
        Debug.DrawLine(origin + side, end + side, Color.cyan, SearchGizmoDuration, false);
        Debug.DrawLine(origin - side, end - side, Color.cyan, SearchGizmoDuration, false);
        Debug.DrawLine(origin, end, Color.cyan, SearchGizmoDuration, false);

        if (hitPosition.HasValue)
            DrawSearchRing(hitPosition.Value, radius, Color.red);
    }

    // Xấp xỉ vòng tròn bằng các đoạn thẳng, không cấp phát mesh hoặc GameObject debug.
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private static void DrawSearchRing(Vector2 center, float radius, Color color)
    {
        Vector2 previous = center + Vector2.right * radius;
        for (int i = 1; i <= SearchGizmoSegments; i++)
        {
            float angle = i * (Mathf.PI * 2f / SearchGizmoSegments);
            Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            Debug.DrawLine(previous, next, color, SearchGizmoDuration, false);
            previous = next;
        }
    }

    #endregion
}
