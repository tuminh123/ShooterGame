using Unity.Mathematics;

// Phép toán thuần, dùng chung trong Burst job và kiểm tra cặp hitbox trên main thread.
internal static class SpatialGeometry
{
    #region Overlap Checks

    // Đổi tâm tròn sang trục hộp rồi đo khoảng cách tới điểm gần nhất trên hộp.
    public static bool CircleOverlapsBox(float2 center, float radius, SpatialEntity box)
    {
        float2 offset = center - box.Center;
        float2 local = new float2(math.dot(offset, box.AxisX), math.dot(offset, box.AxisY));
        float2 outside = math.max(math.abs(local) - box.HalfSize, 0f); // Phần khoảng cách nằm ngoài mỗi cạnh hộp.
        return math.lengthsq(outside) <= radius * radius;
    }

    // Hai hộp giao nhau khi hình chiếu không tách rời trên cả bốn trục cạnh.
    public static bool BoxesOverlap(SpatialEntity first, SpatialEntity second)
    {
        return OverlapOnAxis(first, second, first.AxisX) && OverlapOnAxis(first, second, first.AxisY) &&
               OverlapOnAxis(first, second, second.AxisX) && OverlapOnAxis(first, second, second.AxisY);
    }

    // So khoảng cách hai tâm với tổng bán kính hình chiếu trên một trục.
    private static bool OverlapOnAxis(SpatialEntity first, SpatialEntity second, float2 axis)
    {
        float firstRadius = math.abs(math.dot(first.AxisX, axis)) * first.HalfSize.x +
                            math.abs(math.dot(first.AxisY, axis)) * first.HalfSize.y;
        float secondRadius = math.abs(math.dot(second.AxisX, axis)) * second.HalfSize.x +
                             math.abs(math.dot(second.AxisY, axis)) * second.HalfSize.y;
        return math.abs(math.dot(second.Center - first.Center, axis)) <= firstRadius + secondRadius;
    }

    #endregion

    #region Swept Circle Checks

    // Lấy lần giao sớm nhất trên toàn đoạn; fraction = 0 nếu đã chạm từ đầu đoạn.
    public static bool SweepCircle(float2 start, float2 end, float radius, SpatialEntity box, out float fraction)
    {
        fraction = float.PositiveInfinity;
        if (CircleOverlapsBox(start, radius, box)) { fraction = 0f; return true; }
        float2 offset = start - box.Center;
        float2 travel = end - start; // Vector dịch chuyển world của cả đoạn.
        float2 origin = new float2(math.dot(offset, box.AxisX), math.dot(offset, box.AxisY)); // Điểm đầu trong trục hộp.
        float2 delta = new float2(math.dot(travel, box.AxisX), math.dot(travel, box.AxisY)); // Đoạn dịch chuyển trong trục hộp.
        float2 half = box.HalfSize; // Khoảng cách từ tâm tới mỗi cạnh hộp.

        // Quét mặt và bốn góc tròn riêng: chỉ nới AABB sẽ báo trúng sai tại góc hộp.
        for (int sign = -1; sign <= 1; sign += 2)
        {
            if (math.abs(delta.x) > 0.0000001f)
            {
                float t = (sign * (half.x + radius) - origin.x) / delta.x;
                if (t >= 0f && t <= 1f && math.abs(origin.y + delta.y * t) <= half.y) fraction = math.min(fraction, t);
            }
            if (math.abs(delta.y) > 0.0000001f)
            {
                float t = (sign * (half.y + radius) - origin.y) / delta.y;
                if (t >= 0f && t <= 1f && math.abs(origin.x + delta.x * t) <= half.x) fraction = math.min(fraction, t);
            }
            for (int otherSign = -1; otherSign <= 1; otherSign += 2)
                SweepCorner(origin, delta, new float2(sign * half.x, otherSign * half.y), radius, ref fraction);
        }
        return fraction <= 1f;
    }

    // Giải giao đoạn thẳng với góc tròn và giữ nghiệm sớm nhất trong [0, 1].
    private static void SweepCorner(float2 origin, float2 delta, float2 corner, float radius, ref float fraction)
    {
        float a = math.lengthsq(delta); // Hệ số bậc hai; gần 0 nghĩa là đoạn không di chuyển.
        if (a <= 0.000000000001f) return;
        float2 offset = origin - corner;
        float b = math.dot(offset, delta); // Hệ số chiếu vector từ góc tới điểm đầu lên đoạn bay.
        float discriminant = b * b - a * (math.lengthsq(offset) - radius * radius); // Âm nghĩa là không giao đường tròn.
        if (discriminant < 0f) return;
        float t = (-b - math.sqrt(discriminant)) / a; // Nghiệm vào đường tròn đầu tiên.
        if (t >= 0f && t <= 1f) fraction = math.min(fraction, t);
    }

    #endregion
}
