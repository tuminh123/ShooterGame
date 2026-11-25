/// <summary>
/// Một phần thưởng cấu hình trong asset (tiền, nhân vật, mảnh, trang bị). Một gói thưởng được lắp từ nhiều <see cref="IReward"/>.
/// </summary>
/// <remarks>
/// Họ strategy chỉ chứa tham số, chọn thẳng trong data bằng <c>[SerializeReference]</c> (giống <see cref="ILevelFormula"/>).
/// Reward chỉ MÔ TẢ: tự ghi phần của mình vào <see cref="RewardBatch"/>, còn việc trao do Progression làm cho cả gói
/// (<see cref="IRewardGrant"/>). Nhờ vậy data không biết Manager và không nơi nào phải kiểm tra kiểu bằng is/as.
/// Thêm loại thưởng có kho riêng: hệ thống giữ tài nguyên + class IReward + một hàm Add trong RewardBatch + một bước trong RewardGranter.
/// </remarks>
public interface IReward
{
    /// <summary>Kiểm tra tham số của riêng reward; ném lỗi nếu cấu hình sai.</summary>
    void Validate();

    /// <summary>Ghi phần thưởng này vào gói.</summary>
    void AddTo(RewardBatch batch);
}
