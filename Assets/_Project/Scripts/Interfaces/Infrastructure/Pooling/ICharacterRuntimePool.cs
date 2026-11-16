#region CharacterRuntimeLease

/// <summary>Thuê và trả runtime theo generation để tránh thu hồi nhầm lượt.</summary>
public interface ICharacterRuntimeLease
{
    CharacterRuntimeData Rent(CharacterDataSO template, StatBonus bonus);
    bool TryReturn(CharacterRuntimeData runtime, ulong generation);
}

#endregion

#region CharacterRuntimeCapacity

/// <summary>Chuẩn bị dung lượng trước gameplay.</summary>
public interface ICharacterRuntimeCapacity
{
    int AvailableCount { get; }
    int ActiveCount { get; }
    int CreatedCount { get; }
    void Prewarm(int count);
}

#endregion

#region System Contract

/// <summary>Gom hợp đồng của CharacterRuntimePool; consumer nhận vai trò nhỏ cần dùng.</summary>
public interface ICharacterRuntimePool
{
    ICharacterRuntimeLease GetCharacterRuntimeLease();
    ICharacterRuntimeCapacity GetCharacterRuntimeCapacity();
}

#endregion
