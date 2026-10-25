using System.Collections.Generic;
using UnityEngine;

// * Đồng hồ thuộc loại quái, không thuộc controller hoặc vỏ: despawn/đổi wave không làm mất tiến trình.
// * Chỉ lưu mốc đầu và tính bonus khi spawn, không chạy Update cho từng loại hoặc cập nhật quái còn sống.
public sealed class EnemySpawnProgress
{
    private readonly Dictionary<CharacterType, float> firstSpawnTimes = new Dictionary<CharacterType, float>(); // * Mốc spawn thành công đầu tiên của từng loại trong lượt hiện tại.

    public StatBonus GetBonus(CharacterDataSO template, float gameTime)
    {
        if (!firstSpawnTimes.TryGetValue(template.CharacterType, out float firstSpawn))
            return default;
        int steps = Mathf.FloorToInt(Mathf.Max(0f, gameTime - firstSpawn) / template.GrowthInterval);
        return template.GrowthPerStep * steps;
    }

    public void RecordSpawn(CharacterType type, float gameTime)
    {
        // * Spawn thêm cùng loại không ghi đè mốc cũ; không khởi động đồng hồ khi spawn thất bại.
        if (!firstSpawnTimes.ContainsKey(type))
            firstSpawnTimes.Add(type, gameTime);
    }

    public void Reset() => firstSpawnTimes.Clear();
}
