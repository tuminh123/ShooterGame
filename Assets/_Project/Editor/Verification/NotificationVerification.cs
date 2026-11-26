using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kiểm tra miền Notification trong Edit Mode: bộ đếm (nguồn giả) và luật của các nguồn Heroes, Gear, Shop với Save/Progression/Shop
/// thật trên một FILE SAVE TẠM riêng (xóa khi xong), nên không chạm hồ sơ người chơi.
/// </summary>
public static class NotificationVerification
{
    #region Entry

    private const string SaveFile = "NotificationVerification.es3";
    private static readonly List<UnityEngine.Object> Temporary = new List<UnityEngine.Object>();

    [MenuItem("Shooter Game/Verification/Check Notifications")]
    public static void Run()
    {
        Require(!Application.isPlaying, "Run Check Notifications in Edit Mode.");
        Require(!ES3.FileExists(SaveFile), "Temporary save already exists: " + SaveFile);
        try
        {
            CheckCounter();
            CheckHeroSource();
            CheckGearSource();
            CheckShopSource();
            CheckBeforeInitialize();
            Debug.Log("[Notifications] PASS: one Changed per key per flush, composite Home, item counts, Heroes (full level gold), " +
                      "Gear (equipped only), Shop (free pack until claimed), no reads before Initialize.");
        }
        finally
        {
            ES3.DeleteFile(SaveFile);
            foreach (UnityEngine.Object item in Temporary) UnityEngine.Object.DestroyImmediate(item);
            Temporary.Clear();
        }
    }

    #endregion

    #region Checks

    private static void CheckCounter()
    {
        var quest = new FakeSource(NotificationKey.Quest);
        var login = new FakeSource(NotificationKey.Login);
        var counter = new NotificationCounter();
        counter.Add(quest);
        counter.Add(login);
        counter.AddComposite(NotificationKey.Home, NotificationKey.Quest, NotificationKey.Login);
        var raised = new List<NotificationKey>();
        counter.Changed += raised.Add;

        counter.Flush(); // Lần đầu: mọi khóa đều được báo.
        Require(raised.Contains(NotificationKey.Quest) && raised.Contains(NotificationKey.Login) && raised.Contains(NotificationKey.Home),
            "The first flush must report every key.");

        raised.Clear();
        quest.Counts["daily"] = 2;
        quest.Raise();
        quest.Raise(); // Hai event liền nhau: chỉ báo một lần.
        counter.Flush();
        Require(raised.Count(key => key == NotificationKey.Quest) == 1, "A key was reported more than once in one flush.");
        Require(raised.Contains(NotificationKey.Home) && !raised.Contains(NotificationKey.Login), "Composite Home must follow its parts only.");

        login.Counts["7day"] = 1;
        Require(counter.Get(NotificationKey.Home).Count == 3, "Composite Home must sum Quest and Login.");
        Require(counter.Get(NotificationKey.Quest, "daily").Count == 2 && counter.Get(NotificationKey.Quest, "weekly").Count == 0,
            "Item counts are wrong.");
        Require(!counter.Get(NotificationKey.Gear).Active, "A key without a source must be empty.");

        raised.Clear();
        counter.Flush();
        Require(raised.Count == 0, "Flush without changes must not report.");
        counter.Dispose();
        Require(quest.Disposed && login.Disposed, "Sources were not disposed.");
    }

    // Nhân vật khởi đầu: chưa đủ vàng cho cả level thì không báo, đủ thì báo; có mục theo tên CharacterType.
    private static void CheckHeroSource()
    {
        using (var world = new World())
        {
            CharacterDataSO hero = Hero();
            var selection = new FakeSelection(hero);
            using (var source = new HeroNotificationSource(world.Progression, world.Progression, selection, world.Progression))
            {
                Require(world.Progression.GetStatus(hero.CharacterType).Owned, "Test hero should be owned on a new save.");
                world.SetBalance(CurrencyType.Gold, 0);
                Require(source.Count(null) == 0, "A hero without gold for a full level must not be reported.");
                int changes = 0;
                source.Changed += () => changes++;
                world.SetBalance(CurrencyType.Gold, 10_000_000);
                Require(changes > 0, "Wallet changes must raise Changed.");
                Require(source.Count(null) == 1 && source.Count(hero.CharacterType.ToString()) == 1, "A hero with enough gold must be reported.");
            }
        }
        ES3.DeleteFile(SaveFile);
    }

    // Chỉ đồ đang mặc: thiếu tiền thì không báo, đủ tiền thì báo theo ô.
    private static void CheckGearSource()
    {
        using (var world = new World())
        using (var source = new GearNotificationSource(world.Progression, world.Progression))
        {
            EquipmentSlot slot = world.EnsureEquipped();
            world.SetBalance(CurrencyType.Gold, 0);
            world.SetBalance(CurrencyType.Diamond, 0);
            Require(source.Count(null) == 0, "Equipment without money must not be reported.");
            world.SetBalance(CurrencyType.Gold, 10_000_000);
            world.SetBalance(CurrencyType.Diamond, 10_000_000);
            Require(source.Count(null) >= 1 && source.Count(slot.ToString()) == 1, "Equipped item with enough money must be reported.");
        }
        ES3.DeleteFile(SaveFile);
    }

    // Gói miễn phí: báo tới khi nhận; gói trả tiền không báo dù mua được.
    private static void CheckShopSource()
    {
        ShopPackSO free = Pack("free", new FreePrice());
        ShopPackSO paid = Pack("paid", new CurrencyPrice(CurrencyType.Gold, 1));
        using (var world = new World(free, paid))
        using (var source = new ShopNotificationSource(world.Shop))
        {
            world.SetBalance(CurrencyType.Gold, 100);
            Require(source.Count(null) == 1 && source.Count("free") == 1 && source.Count("paid") == 0, "Only free packs must be reported.");
            Require(world.Shop.BuyAsync(world.Shop.Find("free")).GetAwaiter().GetResult().Succeeded, "Free pack purchase failed.");
            Require(source.Count(null) == 0, "A claimed free pack must not be reported.");
        }
        ES3.DeleteFile(SaveFile);
    }

    // View trong scene bật (OnEnable) trước Initialize của mọi Manager: lúc đó Progression chưa đọc save và ném lỗi nếu bị hỏi.
    // Manager phải trả 0 cho mọi khóa và không hỏi nguồn nào cho tới khi chính nó Initialize.
    private static void CheckBeforeInitialize()
    {
        var save = new SaveManager(new SaveManager.Settings(SaveFile));
        save.Initialize();
        var progression = new ProgressionManager(save, Assets<CharacterDatabaseSO>().Single(), Assets<LevelDatabaseSO>().Single(),
            Assets<EquipmentDatabaseSO>().Single()); // Cố ý CHƯA Initialize.
        var clock = new DeviceOnlyClock();
        var shop = new ShopManager(new ShopManager.Settings(new List<ShopPackSO>()), save, clock, progression, progression);
        var login = new LoginCheckInManager(new LoginCheckInManager.Settings(new List<LoginCheckInCalendarSO>()), save, clock, progression);
        var manager = new NotificationManager(new FakeMissions(), login, shop, progression, progression, new FakeSelection(Hero()),
            progression, progression);
        try
        {
            foreach (NotificationKey key in (NotificationKey[])Enum.GetValues(typeof(NotificationKey)))
                Require(!manager.Get(key).Active, key + " was reported before Initialize.");
            manager.Tick(); // Không được Sync/Flush trước Initialize.

            progression.Initialize();
            shop.Initialize();
            login.Initialize();
            manager.Initialize();
            manager.Get(NotificationKey.Heroes); // Sau Initialize hỏi được bình thường (ném lỗi nếu Progression chưa sẵn sàng).
            manager.Get(NotificationKey.Gear);
        }
        finally
        {
            manager.Dispose();
            login.Dispose();
            shop.Dispose();
            progression.Dispose();
            save.Dispose();
            ES3.DeleteFile(SaveFile);
        }
    }

    #endregion

    #region World

    // Save + Progression thật (+ Shop nếu có gói) trên file save tạm.
    private sealed class World : IDisposable
    {
        private readonly SaveManager save;
        public readonly ProgressionManager Progression;
        public readonly ShopManager Shop;

        public World(params ShopPackSO[] packs)
        {
            save = new SaveManager(new SaveManager.Settings(SaveFile));
            save.Initialize();
            Progression = new ProgressionManager(save, Assets<CharacterDatabaseSO>().Single(), Assets<LevelDatabaseSO>().Single(),
                Assets<EquipmentDatabaseSO>().Single());
            Progression.Initialize();
            if (packs.Length == 0) return;
            Shop = new ShopManager(new ShopManager.Settings(packs.ToList()), save, new DeviceOnlyClock(), Progression, Progression);
            Shop.Initialize();
        }

        public void SetBalance(CurrencyType type, long amount)
        {
            long current = Progression.GetBalance(type);
            if (current > amount) Progression.TrySpend(type, current - amount, out _);
            else if (current < amount) Progression.Add(type, amount - current);
        }

        // Ô đầu tiên đang mặc đồ; save mới chưa mặc gì thì nhận một món và mặc vào.
        public EquipmentSlot EnsureEquipped()
        {
            foreach (EquipmentSlot slot in (EquipmentSlot[])Enum.GetValues(typeof(EquipmentSlot)))
                if (Progression.GetEquipped(slot) != null) return slot;
            EquipmentDataSO data = Assets<EquipmentDatabaseSO>().Single().Items[0];
            EquipmentItem item = Progression.Grant(data, EquipmentRarity.Common, out _);
            Require(Progression.Equip(item, out _), "Could not equip a test item.");
            return data.Slot;
        }

        public void Dispose()
        {
            Shop?.Dispose();
            Progression.Dispose();
            save.Dispose();
        }
    }

    private sealed class FakeSource : INotificationSource
    {
        public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        public bool Disposed;

        public FakeSource(NotificationKey key) => Key = key;

        public NotificationKey Key { get; }
        public event Action Changed;
        public int Count(string item) => item == null ? Counts.Values.Sum() : Counts.TryGetValue(item, out int count) ? count : 0;
        public void Sync() { }
        public void Dispose() => Disposed = true;
        public void Raise() => Changed?.Invoke();
    }

    private sealed class FakeSelection : IPlayerSelection
    {
        public FakeSelection(CharacterDataSO character) => PlayableCharacters = new[] { character };

        public IReadOnlyList<CharacterDataSO> PlayableCharacters { get; }
        public CharacterType SelectedCharacterType => PlayableCharacters[0].CharacterType;
        public event Action<CharacterType> SelectionChanged { add { } remove { } }
        public bool SelectCharacter(CharacterType type) => true;
    }

    // Mission thật cần cả gameplay; ở đây chỉ cần một nguồn trống.
    private sealed class FakeMissions : IMissions
    {
        public IReadOnlyList<MissionBoard> Boards { get; } = Array.Empty<MissionBoard>();
        public bool HasClaimable => false;
        public DateTime UtcNow => DateTime.UtcNow;
        public event Action Changed { add { } remove { } }
        public void Refresh() { }

        public bool TryClaim(MissionRuntime mission, out RewardResult result, out bool saved)
        {
            result = null;
            saved = false;
            return false;
        }
    }

    private sealed class DeviceOnlyClock : IGameClock
    {
        public event Action Changed { add { } remove { } }

        public bool TryGetUtcNow(ClockSource source, out DateTime utcNow)
        {
            utcNow = new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc);
            return true;
        }
    }

    #endregion

    #region Helpers

    private static ShopPackSO Pack(string id, IShopPrice price)
    {
        var pack = ScriptableObject.CreateInstance<ShopPackSO>();
        Temporary.Add(pack);
        var data = new SerializedObject(pack);
        data.FindProperty("id").stringValue = id;
        data.FindProperty("price").managedReferenceValue = price;
        data.FindProperty("limit").intValue = (int)PurchaseLimit.Daily;
        data.FindProperty("limitCount").intValue = 1;
        SerializedProperty rewards = data.FindProperty("rewards");
        rewards.arraySize = 1;
        rewards.GetArrayElementAtIndex(0).managedReferenceValue = new CurrencyReward(CurrencyType.Gold, 10);
        data.ApplyModifiedPropertiesWithoutUndo();
        pack.Validate();
        return pack;
    }

    private static CharacterDataSO Hero() => Assets<CharacterDataSO>().First(item => item.Team == CharacterTeam.Player);

    private static IEnumerable<T> Assets<T>() where T : UnityEngine.Object
        => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Notifications] " + message);
    }

    #endregion
}
