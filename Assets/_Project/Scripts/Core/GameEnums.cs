using UnityEngine;

/// <summary>
/// Mọi enum của code game nằm trong file này, chia vùng theo miền, để dễ tìm và dễ thêm.
/// </summary>
/// <remarks>
/// Asset lưu enum bằng SỐ, nên khi thêm giá trị mới hãy thêm ở CUỐI enum (hoặc gán số rõ ràng);
/// không đổi số của giá trị đã dùng. Enum chỉ dùng trong Editor nằm ở Assembly-CSharp-Editor.
/// </remarks>

#region Character

/// <summary>Các cách tìm mục tiêu ở tầng gameplay; độc lập với loại job ở tầng Spatial.</summary>
public enum CharacterSearchType { Nearest, AllInCircle, FirstOnPath }

/// <summary>Loại phản hồi hình ảnh mà controller báo cho view (nháy trắng khi trúng đòn, màu của trạng thái).</summary>
public enum CharacterFeedbackKind { Hit, Poison, Burn, Slow }

/// <summary>
/// Độ hiếm của nhân vật: nhãn, màu thẻ và màu nền màn Character, hệ số giá nâng level (LevelDatabaseSO).
/// Cùng thứ tự và cùng số với <see cref="EquipmentRarity"/>; số phải liên tục từ 0 theo thứ tự hiếm dần vì hệ số giá lưu theo vị trí.
/// </summary>
/// <remarks>
/// 2026-11-23: chen Uncommon vào số 1 (Rare/Epic/Legendary dời lên +1) để khớp trang bị. Chỉ asset lưu số này
/// (CharacterData, CharacterScreenTheme, LevelDatabase); save không lưu độ hiếm nhân vật. Các asset đã được chuyển cùng lúc.
/// Từ nay chỉ thêm loại mới ở cuối, không đổi số cũ.
/// </remarks>
public enum CharacterRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
    Legendary = 4
}

/// <summary>Phe dùng để phân biệt player với enemy; boss cũng thuộc phe Enemy.</summary>
public enum CharacterTeam
{
    Player = 0,  // Phe người chơi.
    Enemy = 1    // Phe quái, gồm cả boss.
}

/// <summary>
/// Khóa ổn định của loại nhân vật: dùng cho template, cấu hình level và tiến trình đã lưu.
/// Không đổi giá trị số đã có (save lưu số); nhân vật mới nhận một giá trị mới.
/// </summary>
public enum CharacterType
{
    None = 0,
    BlessedGladiator = 1,
    ForagingMaggot = 2,
    Boss = 3
}

/// <summary>Các chỉ số của nhân vật. Giữ nguyên thứ tự tám giá trị đầu: asset buff lưu số, đổi thứ tự sẽ đổi chỉ số bị buff.</summary>
public enum StatType
{
    MaxHealth, Damage, Defense, MoveSpeed, CritChance,
    CritDamageMultiplier, EffectResistance, EffectHit
}

/// <summary>Percent là phần trăm tương đối; Add trên chỉ số % là cộng điểm phần trăm.</summary>
public enum StatOperation { Add, Percent }

/// <summary>Giá trị dùng chung giữa gameplay và prefab; tên state Animator được cấu hình riêng trên visual.</summary>
public enum CharacterVisualState
{
    Idle,
    Move,
    Attack,
    Cast,
    Hit,
    Death,
    Win,
    Lose
}

/// <summary>
/// Loại điều kiện của một bước tiến trình skill (Player): so với level vĩnh viễn hay số sao đã lưu.
/// Lưu bằng số trong asset: thêm loại mới ở cuối, không đổi số đã dùng.
/// </summary>
public enum SkillConditionType
{
    CharacterLevel = 0,
    Stars = 1
}

#endregion

#region Combat

/// <summary>Loại hình ảnh đạn. Giá trị đã serialize trong asset: chỉ thêm ở cuối.</summary>
public enum BulletType { Holy, Fire, Ice, Poison }

/// <summary>Loại VFX chạy một lần. Giá trị số đã serialize trong asset, không được đổi số của giá trị cũ.</summary>
public enum EffectType { None = 0, HolyImpact = 1, FireImpact = 2, IceImpact = 3, PoisonImpact = 4, LightningImpact = 5, WindImpact = 6 }

/// <summary>Loại hành vi skill; dùng để ghép template hành vi với data của từng cấp.</summary>
/// <remarks>Giá trị đã được serialize trong asset: chỉ thêm giá trị mới ở cuối, không đổi thứ tự hay xóa giá trị cũ.</remarks>
public enum SkillBehaviourType { NormalAttack, Shooting, OrbitSaw, SlowAura, Regeneration, EmergencyShield, StatBuff }

/// <summary>Nguồn của skill. Chỉ <see cref="Gameplay"/> mới được cấp/nâng qua bảng chọn khi lên cấp.</summary>
public enum SkillSource { Gameplay, Character, Equipment }

#endregion

#region Game

/// <summary>State trận đấu. GameManager là nơi duy nhất ghi, và phát cho UI qua IGameState.StateChanged.</summary>
public enum GameState
{
    Boot, // Scene đang khởi tạo tài nguyên.
    PreparingBattle, // Dọn lượt cũ và tạo Player cho lượt mới.
    Home, // Sẵn sàng bắt đầu lượt chơi.
    Playing, // Trận đấu đang diễn ra.
    Won, // Animation kết quả xong, UI thắng được mở.
    Lost, // Animation kết quả xong, UI thua được mở.
    Faulted, // Khởi tạo lượt chơi lỗi; gameplay giữ pause.
    EndingWin, // Khóa gameplay và phát animation kết thúc thắng.
    EndingLose // Khóa gameplay và phát animation kết thúc thua.
}

#endregion

#region Mission

/// <summary>Loại nhóm mission, quyết định kỳ reset. Thêm loại mới ở cuối, không đổi số đã dùng.</summary>
public enum MissionCategory { Daily = 0, Weekly = 1, Event = 2 }

/// <summary>
/// Nhóm việc của mission con (và của tín hiệu). Chi tiết của từng nhóm là một enum riêng, giá trị 0 luôn là Any
/// (chỉ dùng ở task: loại nào cũng tính). Asset lưu số: thêm ở cuối, không đổi số đã dùng.
/// </summary>
public enum MissionActivity
{
    Kill = 0,     // Chi tiết: EnemyKind. Subject: CharacterType của quái.
    Battle = 1,   // Chi tiết: BattleDetail.
    Collect = 2,  // Chi tiết: CollectDetail.
    Upgrade = 3,  // Chi tiết: UpgradeDetail. Subject: CharacterType của nhân vật được nâng.
    Mission = 4   // Chi tiết: MissionDetail. Chỉ dùng ở Final Mission.
}

/// <summary>Chi tiết của nhóm Battle: trận kết thúc thế nào. Any chỉ dùng ở task.</summary>
public enum BattleDetail { Any = 0, Win = 1, Lose = 2 }

/// <summary>Chi tiết của nhóm Collect: thứ nhặt được. Any chỉ dùng ở task.</summary>
public enum CollectDetail { Any = 0, Gold = 1 }

/// <summary>Chi tiết của nhóm Upgrade: thứ được nâng. Any chỉ dùng ở task.</summary>
public enum UpgradeDetail { Any = 0, CharacterLevel = 1, CharacterStar = 2, EquipmentLevel = 3 }

/// <summary>Chi tiết của nhóm Mission. Any chỉ dùng ở task.</summary>
public enum MissionDetail { Any = 0, Completed = 1 }

/// <summary>Nhóm quái trong nhiệm vụ giết. Any chỉ dùng làm bộ lọc; asset lưu số: thêm ở cuối, không đổi số đã dùng.</summary>
public enum EnemyKind { Any = 0, Normal = 1, Elite = 2, Boss = 3 }

/// <summary>Trạng thái của một mission trong kỳ hiện tại.</summary>
public enum MissionState { InProgress, Claimable, Claimed }

/// <summary>Màn mà nút Go của mission dẫn tới. None = không có nút (ví dụ mission tổng kết).</summary>
public enum MissionDestination { None, Battle, Heroes, Equipment }

/// <summary>Nơi phần thưởng bay tới trên màn nhận thưởng.</summary>
public enum RewardFlyTarget { Gold, Diamond, Heroes, Bag }

#endregion

#region Pool

/// <summary>
/// Nhóm của object đang hoạt động: chọn holder trong Hierarchy, và quyết định object có bị thu hồi khi rời gameplay không
/// (xem <see cref="PoolManager.DespawnGameplayClones"/>). Không quyết định hành vi của prefab.
/// </summary>
public enum PoolGroup
{
    Enemies,
    Bullets,
    World, // Nền map: tồn tại cả ở Home, không bị thu hồi khi rời gameplay.
    Other,
    UI     // Phần tử UI dùng pool (ô skill…): UI tự quản lý, không bị thu hồi khi rời gameplay.
}

#endregion

#region Progression

/// <summary>ID được lưu trong save. Thêm loại mới ở cuối với ID mới; không đổi ID đã phát hành.</summary>
public enum CurrencyType
{
    Gold = 0,
    Diamond = 1,
    // Không còn dùng: mảnh nay thuộc riêng từng nhân vật (CharacterShards, lưu trong tiến trình nhân vật).
    // Giữ giá trị vì asset và save lưu enum bằng số; không đổi số.
    ShardCommon = 2,
    ShardUncommon = 3,
    ShardRare = 4,
    ShardEpic = 5,
    ShardLegendary = 6
}

/// <summary>Kết quả nhận một nhân vật (gacha): chưa có thì sở hữu luôn, đã có thì thành một bản trùng.</summary>
public enum CharacterGrantResult { NewCharacter, Duplicate }

/// <summary>Loại nâng cấp vĩnh viễn của một nhân vật. Mỗi loại ứng với một track trong ProgressionManager.</summary>
public enum CharacterUpgradeKind { Level, Star }

/// <summary>Lý do không nâng được; <see cref="None"/> nghĩa là nâng được. Thêm giá trị mới ở cuối.</summary>
public enum UpgradeBlock { None, MaxReached, NotEnoughCurrency, NotAvailable, NotEnoughShards }

/// <summary>Ô mặc trang bị; mỗi ô có một chỉ số chính cố định (EquipmentRulesSO). Số được lưu trong save.</summary>
public enum EquipmentSlot { Weapon = 0, Helmet = 1, Armor = 2, Gloves = 3, Boots = 4, Ring = 5 }

/// <summary>
/// Độ hiếm của một bản trang bị (cùng một mẫu đồ có thể ra mọi độ hiếm). Quyết định số dòng phụ ban đầu và chỉ số chính.
/// Tách riêng với CharacterRarity (cùng số, cùng thứ tự) để mỗi miền tự thêm độ hiếm của mình mà không ảnh hưởng miền kia.
/// </summary>
public enum EquipmentRarity { Common = 0, Uncommon = 1, Rare = 2, Epic = 3, Legendary = 4 }

/// <summary>Set trang bị; mỗi set khai báo một lần trong EquipmentDatabaseSO. Thêm set mới ở cuối, không đổi số đã dùng.</summary>
public enum EquipmentSet { None = 0, Samurai = 1, Witch = 2 }

#endregion

#region Battle

/// <summary>Loại phần thưởng của vật phẩm rơi; quyết định LootSystem cộng EXP hay vàng.</summary>
public enum PickupType { Experience, Gold }

#endregion

#region Simulation

/// <summary>Loại truy vấn: bỏ qua, gần nhất, quét đường bay, lấy mọi hitbox trong vòng tròn.</summary>
public enum SpatialQueryType : byte { None, Nearest, Sweep, AllInCircle }

#endregion

#region Stage

public enum SpecialEnemyType { Elite, Boss }

public enum BossAppearanceCondition { AfterAllWavesSpawned, AfterFinalWaveCleared }

/// <summary>Loại cảnh báo spawn; số lớn hơn được ưu tiên khi xuất hiện cùng lúc.</summary>
public enum StageSpawnWarning { SimultaneousWave, Elite, Boss }

/// <summary>Ba cách hoạt động dùng chung hệ thống Pattern.</summary>
public enum EnemyPatternType
{
    [InspectorName("Cả khối theo Path")] Formation = 0,
    [InspectorName("Từng thành viên theo Path")] SequentialPath = 1,
    [InspectorName("Area quanh Player lúc spawn")] Area = 2
}

/// <summary>Area thu đều hoặc xen kẽ các đợt thu và khoảng nghỉ.</summary>
public enum AreaShrinkMode
{
    [InspectorName("Thu liên tục")] Continuous = 0,
    [InspectorName("Thu theo đợt")] Burst = 1
}

/// <summary>Cách phân bố slot hình học, độc lập với cách spawn theo Path hoặc Area.</summary>
public enum FormationFillMode
{
    [InspectorName("Đặc")] Solid = 0,
    [InspectorName("Rỗng")] Hollow = 1
}

/// <summary>Các cách nhân đối xứng điểm Custom quanh trục hoặc tâm.</summary>
public enum FormationSymmetry
{
    [InspectorName("Tự do")] None,
    [InspectorName("Trái / phải")] MirrorHorizontal,
    [InspectorName("Trên / dưới")] MirrorVertical,
    [InspectorName("Cả hai trục")] BothAxes,
    [InspectorName("Qua tâm")] AroundCenter
}

/// <summary>ID hình được lưu trong asset; giữ nguyên giá trị để không lệch dữ liệu cũ.</summary>
public enum FormationType
{
    [InspectorName("Chữ nhật / Vuông")] FilledSquare = 0,
    [InspectorName("Tròn / Elip")] FilledCircle = 2,
    [InspectorName("Hàng ngang")] HorizontalLine = 4,
    [InspectorName("Hàng dọc")] VerticalLine = 5,
    [InspectorName("Tam giác")] Triangle = 6,
    [InspectorName("Hình thoi")] Diamond = 7,
    [InspectorName("Lục giác")] Hexagon = 8,
    [InspectorName("Custom / Tự tạo")] Custom = 9
}

/// <summary>Cách chuyển wave: chờ thời gian hoặc dọn sạch quái của chuỗi.</summary>
public enum WaveAdvanceCondition { AfterDuration, AfterEnemiesCleared }

/// <summary>Cách sinh quái trong một wave.</summary>
public enum WaveSpawnMode
{
    [InspectorName("Lần lượt")] Sequential = 0,
    [InspectorName("Đồng thời")] Simultaneous = 1
}

#endregion

#region UI

/// <summary>Màn con đang mở trong màn Character.</summary>
public enum CharacterScreenPage { Collection, Detail }

/// <summary>Tab đang mở ở màn Detail. Mỗi tab ứng với một loại nâng cấp.</summary>
public enum CharacterScreenTab { Level, Stars }

/// <summary>Trạng thái một thẻ trong lưới Collection. Locked = chưa sở hữu (thẻ xám, thanh mảnh thay cho level/sao).</summary>
public enum CharacterCardState { Normal, Selected, Empty, Locked }

/// <summary>Hướng trượt của <see cref="SlideTransition"/>: phía màn hình mà panel trượt vào từ đó hoặc trượt ra về đó.</summary>
public enum SlideDirection
{
    Left,
    Right,
    Up,
    Down
}

/// <summary>Trạng thái một perk sao trên tab Stars: đã mở, sao kế tiếp sẽ mở, hoặc còn khóa.</summary>
public enum StarPerkState { Unlocked, Next, Locked }

/// <summary>Cách hiện một con số chỉ số trên màn Character (CharacterScreenThemeSO).</summary>
public enum StatFormat
{
    Integer, // 750
    Decimal, // 3.5
    Percent  // 5%
}

/// <summary>
/// Cách tô một lớp hình theo độ hiếm (<see cref="ImageStyle"/>): giữ sprite gốc và đổi màu, hoặc thay bằng sprite kéo vào.
/// Số được lưu trong asset RarityStyleDatabase: thêm cách mới ở cuối, không đổi số cũ.
/// </summary>
public enum ImageStyleMode
{
    Color = 0,  // Giữ sprite gốc của prefab, tô màu.
    Sprite = 1  // Thay bằng sprite kéo vào, giữ nguyên màu của sprite (không tô).
}

#endregion
