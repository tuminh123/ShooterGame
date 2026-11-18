/// <summary>
/// Strategy của một <b>hiệu ứng gameplay</b> (làm chậm, cháy, độc…), là cấu hình chỉ đọc.
/// </summary>
/// <remarks>
/// Được gắn vào data của skill bằng <c>[SerializeReference]</c>. Cấu hình không giữ trạng thái
/// của effect đang chạy; trạng thái đó nằm trong <see cref="EffectRuntime"/>, được thuê từ pool qua
/// <see cref="RentRuntime"/>.
/// </remarks>
public interface IGameplayEffect
{
    /// <summary>Thời gian hiệu lực (giây), không phải thời gian của VFX.</summary>
    float Duration { get; }

    /// <summary>Xác suất áp dụng cơ bản, theo phần trăm (0–100).</summary>
    float ApplicationChance { get; }

    /// <summary>Có tính chỉ số chính xác hiệu ứng của nguồn và kháng hiệu ứng của mục tiêu vào xác suất hay không.</summary>
    bool UseEffectStats { get; }

    /// <summary>Nhịp tác động định kỳ (giây). 0 nghĩa là không tác động định kỳ.</summary>
    float TickInterval { get; }

    /// <summary>Kiểm tra thông số. Ném exception nếu cấu hình sai.</summary>
    void Validate();

    /// <summary>Thuê đúng loại runtime từ pool và chép thông số của cấu hình này vào đó.</summary>
    EffectRuntime RentRuntime(EffectRuntimePool pool);
}
