using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Danh mục Stage dùng chung; trạng thái chọn màn thuộc StageManager.</summary>
[CreateAssetMenu(fileName = "StageDatabase", menuName = "Shooter Game/Data/Stage Database")]
public sealed class StageDatabaseSO : ScriptableObject
{
    #region Stage Catalog

    [Tooltip("Danh sách toàn bộ StageSO của game. Mỗi Stage cần StageId duy nhất; thứ tự dùng để sắp xếp màn trong UI.")]
    [SerializeField] private List<StageDataSO> stages = new List<StageDataSO>();

    [Tooltip("Màn được chọn lúc khởi tạo scene. Phải nằm trong danh sách Stages; để trống để dùng màn đầu tiên.")]
    [SerializeField] private StageDataSO defaultStage;

    public IReadOnlyList<StageDataSO> Stages => stages;
    public StageDataSO DefaultStage => defaultStage != null ? defaultStage :
        stages != null && stages.Count > 0 ? stages[0] : null;

    #endregion

    #region Validation & Runtime Lookup

    /// <summary>Kiểm tra toàn bộ cấu hình; trả lookup riêng để runtime không ghi lên asset.</summary>
    public Dictionary<string, StageDataSO> CreateLookup()
    {
        if (stages == null || stages.Count == 0)
            throw new InvalidOperationException(name + ": Stage Database cần ít nhất một Stage.");
        var lookup = new Dictionary<string, StageDataSO>(stages.Count, StringComparer.Ordinal);
        foreach (StageDataSO stage in stages)
        {
            if (stage == null)
                throw new InvalidOperationException(name + ": danh sách Stage chứa phần tử trống.");
            stage.Validate();
            if (lookup.ContainsKey(stage.StageId))
                throw new InvalidOperationException(name + ": StageId bị trùng: " + stage.StageId);
            lookup.Add(stage.StageId, stage);
        }
        if (defaultStage != null && !stages.Contains(defaultStage))
            throw new InvalidOperationException(name + ": Default Stage phải thuộc danh sách Stages.");
        return lookup;
    }

    public void Validate() => CreateLookup();

    #endregion
}
