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

/// <summary>Độ hiếm của nhân vật, dùng cho nhãn trên màn Character. ID được lưu trong asset: loại mới thêm vào cuối, không đổi số cũ.</summary>
public enum CharacterRarity
{
    Common = 0,
    Rare = 1,
    Epic = 2,
    Legendary = 3
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
    PreparingRun, // Dọn lượt cũ và tạo Player cho lượt mới.
    Home, // Sẵn sàng bắt đầu lượt chơi.
    Playing, // Trận đấu đang diễn ra.
    Won, // Animation kết quả xong, UI thắng được mở.
    Lost, // Animation kết quả xong, UI thua được mở.
    Faulted, // Khởi tạo lượt chơi lỗi; gameplay giữ pause.
    EndingWin, // Khóa gameplay và phát animation kết thúc thắng.
    EndingLose // Khóa gameplay và phát animation kết thúc thua.
}

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
    Diamond = 1
}

/// <summary>Loại nâng cấp vĩnh viễn của một nhân vật. Mỗi loại ứng với một track trong ProgressionManager.</summary>
public enum CharacterUpgradeKind { Level, Star }

/// <summary>Lý do không nâng được; <see cref="None"/> nghĩa là nâng được.</summary>
public enum UpgradeBlock { None, MaxReached, NotEnoughCurrency, NotAvailable }

#endregion

#region Run

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

/// <summary>Trạng thái một thẻ trong lưới Collection.</summary>
public enum CharacterCardState { Normal, Selected, Empty }

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

#endregion
