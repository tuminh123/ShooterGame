using System;

/// <summary>Một gói shop đang chạy: kỳ giới hạn hiện tại và số lần đã mua. Ra ngoài chỉ đọc; chỉ ShopManager đổi trạng thái.</summary>
public sealed class ShopPackRuntime
{
    #region State

    private long cycle; // Mã kỳ giới hạn của số lần đã mua đang giữ.

    /// <summary>Cấu hình của gói.</summary>
    public ShopPackSO Data { get; }

    /// <summary>Giá đã mô tả (cách trả, khoản tiền, sản phẩm store); tạo một lần vì asset không đổi lúc chạy.</summary>
    public PurchaseRequest Request { get; }

    /// <summary>Nguồn giờ của gói có giờ (Server chưa trả lời thì đóng).</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Số lần đã mua trong kỳ.</summary>
    public int Bought { get; private set; }

    /// <summary>Lúc kỳ giới hạn reset (UTC); không có kỳ thì DateTime.MaxValue.</summary>
    public DateTime ResetsAt { get; private set; } = DateTime.MaxValue;

    /// <summary>Số lần còn mua được trong kỳ; không giới hạn thì int.MaxValue.</summary>
    public int Remaining => Data.Limit == PurchaseLimit.None ? int.MaxValue : Math.Max(0, Data.LimitCount - Bought);

    #endregion

    #region Constructor

    internal ShopPackRuntime(ShopPackSO data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        Request = data.CreateRequest();
    }

    #endregion

    #region Update (ShopManager)

    /// <summary>Cập nhật kỳ giới hạn; sang kỳ mới thì số lần đã mua về 0. Trả về UI có cần vẽ lại không.</summary>
    /// <remarks>Không đánh dấu cần ghi: lần nạp sau tính lại đúng kết quả này từ mã kỳ đã lưu, nên chỉ ghi khi mua.</remarks>
    internal bool Refresh(DateTime utcNow, bool hasTime)
    {
        bool changed = hasTime != IsOpen;
        IsOpen = hasTime;
        if (!hasTime) return changed;
        long current = Data.GetCycle(utcNow, out DateTime resetsAt);
        ResetsAt = resetsAt;
        if (current == cycle) return changed;
        cycle = current;
        changed |= Bought != 0;
        Bought = 0;
        return changed;
    }

    /// <summary>Tính thêm một lần mua (trước snapshot, để cùng lần ghi với phần thưởng).</summary>
    internal void MarkBought() => Bought++;

    /// <summary>Hoàn lại lần mua vừa tính khi bước trao quà ném lỗi trước khi đổi gì.</summary>
    internal void UndoBought() => Bought--;

    #endregion

    #region Save

    /// <summary>Nạp số lần đã mua.</summary>
    internal void Restore(ShopSaveData.Pack saved)
    {
        cycle = saved.cycle;
        Bought = saved.bought;
    }

    /// <summary>Snapshot để ghi.</summary>
    internal ShopSaveData.Pack ToSaveData() => new ShopSaveData.Pack { packId = Data.Id, cycle = cycle, bought = Bought };

    #endregion
}
