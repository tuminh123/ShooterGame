using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kiểm tra miền Shop trong Edit Mode bằng Save/Progression thật trên một FILE SAVE TẠM riêng (xóa khi xong),
/// nên không chạm hồ sơ người chơi. Giờ được giả bằng <see cref="FakeClock"/> để tua ngày.
/// </summary>
public static class ShopVerification
{
    #region Entry

    private const string SaveFile = "ShopVerification.es3";
    private static readonly List<UnityEngine.Object> Temporary = new List<UnityEngine.Object>();

    [MenuItem("Shooter Game/Verification/Check Shop")]
    public static void Run()
    {
        Require(!Application.isPlaying, "Run Check Shop in Edit Mode.");
        Require(!ES3.FileExists(SaveFile), "Temporary save already exists: " + SaveFile);
        try
        {
            CheckConfiguredPacks();
            CheckCurrencyPurchase();
            CheckDailyLimitAndFree();
            CheckStorePurchaseAndReload();
            CheckServerWithoutTime();
            Debug.Log("[Shop] PASS: configured packs, currency purchase in one save (cost + rewards), not enough currency, " +
                      "same currency cost and reward, daily limit + reset, free pack, store pack (simulated), lifetime limit + reload, server clock without time.");
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

    // Mọi gói trong project hợp lệ và mọi phần thưởng trao được với database nhân vật/trang bị thật.
    private static void CheckConfiguredPacks()
    {
        using (var world = new World(Array.Empty<ShopPackSO>(), new FakeClock()))
            foreach (ShopPackSO pack in Assets<ShopPackSO>())
            {
                pack.Validate();
                world.Progression.ValidateRewards(pack.Rewards);
            }
        ES3.DeleteFile(SaveFile);
    }

    private static void CheckCurrencyPurchase()
    {
        CharacterDataSO hero = Hero();
        ShopPackSO shards = Pack("shards", new CurrencyPrice(CurrencyType.Gold, 100), PurchaseLimit.None, new CharacterShardReward(hero, 5));
        ShopPackSO exchange = Pack("exchange", new CurrencyPrice(CurrencyType.Gold, 100), PurchaseLimit.None, new CurrencyReward(CurrencyType.Gold, 300));
        using (var world = new World(new[] { shards, exchange }, new FakeClock()))
        {
            ShopPackRuntime pack = world.Shop.Find("shards");
            world.SetGold(100); // Đúng 100 vàng, bất kể save mới có vàng khởi đầu hay không.
            long startShards = world.Progression.GetStatus(hero.CharacterType).Shards;

            ShopPurchaseResult bought = world.Buy(pack);
            Require(bought.Succeeded && bought.Saved, "Currency purchase failed.");
            Require(world.Progression.GetBalance(CurrencyType.Gold) == 0, "Price was not paid.");
            Require(world.Progression.GetStatus(hero.CharacterType).Shards == startShards + 5, "Shards were not granted.");

            Require(world.Shop.GetBlock(pack) == PurchaseBlock.NotEnoughCurrency, "A pack was buyable without enough currency.");
            Require(world.Buy(pack).Block == PurchaseBlock.NotEnoughCurrency, "Bought without enough currency.");
            Require(world.Progression.GetStatus(hero.CharacterType).Shards == startShards + 5, "A failed purchase changed shards.");

            // Giá và phần thưởng cùng loại tiền: trừ 100, cộng 300 trong một lần.
            world.Progression.Add(CurrencyType.Gold, 100);
            Require(world.Buy(world.Shop.Find("exchange")).Succeeded && world.Progression.GetBalance(CurrencyType.Gold) == 300,
                "Same-currency cost and reward were not applied together.");
        }
        ES3.DeleteFile(SaveFile);
    }

    private static void CheckDailyLimitAndFree()
    {
        var clock = new FakeClock { Now = Utc(2026, 11, 2, 8) };
        ShopPackSO free = Pack("free", new FreePrice(), PurchaseLimit.Daily, new CurrencyReward(CurrencyType.Gold, 500));
        using (var world = new World(new[] { free }, clock))
        {
            ShopPackRuntime pack = world.Shop.Find("free");
            Require(world.Shop.HasFreePack && pack.Request.IsFree, "A free daily pack should be claimable.");
            long gold = world.Progression.GetBalance(CurrencyType.Gold);
            Require(world.Buy(pack).Succeeded && world.Progression.GetBalance(CurrencyType.Gold) == gold + 500, "Free pack was not granted.");
            Require(world.Shop.GetBlock(pack) == PurchaseBlock.LimitReached && !world.Shop.HasFreePack, "Daily limit was not applied.");
            Require(pack.ResetsAt == Utc(2026, 11, 3, 0), "Daily reset time is wrong.");
            clock.Now = clock.Now.AddDays(1);
            world.Shop.Refresh();
            Require(world.Shop.GetBlock(pack) == PurchaseBlock.None && pack.Bought == 0, "Daily limit did not reset on a new day.");
        }
        ES3.DeleteFile(SaveFile);
    }

    private static void CheckStorePurchaseAndReload()
    {
        var clock = new FakeClock { Now = Utc(2026, 11, 2, 8) };
        ShopPackSO store = Pack("store", new StorePrice("pack_test", "100.000 VND"), PurchaseLimit.Lifetime,
            new CurrencyReward(CurrencyType.Diamond, 200), new CharacterShardReward(Hero(), 25));
        using (var world = new World(new[] { store }, clock))
        {
            ShopPackRuntime pack = world.Shop.Find("store");
            Require(pack.Request.Kind == PaymentKind.Store && pack.Request.StoreLabel == "100.000 VND", "Store price was not described.");
            long diamonds = world.Progression.GetBalance(CurrencyType.Diamond);
            Require(world.Buy(pack).Succeeded && world.Progression.GetBalance(CurrencyType.Diamond) == diamonds + 200,
                "Simulated store purchase failed.");
            Require(world.Shop.GetBlock(pack) == PurchaseBlock.LimitReached, "Lifetime limit was not applied.");
        }
        using (var world = new World(new[] { store }, clock))
            Require(world.Shop.GetBlock(world.Shop.Find("store")) == PurchaseBlock.LimitReached, "Purchase count was not saved.");
        ES3.DeleteFile(SaveFile);
    }

    // Gói dùng giờ Server mà chưa có giờ: đóng; có giờ (Clock.Changed) thì mở ngay.
    private static void CheckServerWithoutTime()
    {
        var clock = new FakeClock { Now = Utc(2026, 11, 2, 8), HasServerTime = false };
        ShopPackSO pack = Pack("server", new FreePrice(), PurchaseLimit.Daily, new CurrencyReward(CurrencyType.Gold, 1));
        var data = new SerializedObject(pack);
        data.FindProperty("clock").intValue = (int)ClockSource.Server;
        data.ApplyModifiedPropertiesWithoutUndo();
        using (var world = new World(new[] { pack }, clock))
        {
            ShopPackRuntime runtime = world.Shop.Find("server");
            Require(world.Shop.GetBlock(runtime) == PurchaseBlock.Closed && !world.Shop.HasFreePack, "A Server pack opened without server time.");
            clock.HasServerTime = true;
            clock.RaiseChanged();
            Require(world.Shop.GetBlock(runtime) == PurchaseBlock.None, "A Server pack did not open when server time arrived.");
        }
        ES3.DeleteFile(SaveFile);
    }

    #endregion

    #region World

    // Save + Progression thật, đồng hồ giả. Dùng lại cùng một FakeClock giữa hai World để thử nạp lại save.
    private sealed class World : IDisposable
    {
        private readonly SaveManager save;
        public readonly ProgressionManager Progression;
        public readonly ShopManager Shop;

        public World(IEnumerable<ShopPackSO> packs, FakeClock clock)
        {
            save = new SaveManager(new SaveManager.Settings(SaveFile));
            save.Initialize();
            Progression = new ProgressionManager(save, Assets<CharacterDatabaseSO>().Single(), Assets<LevelDatabaseSO>().Single(),
                Assets<EquipmentDatabaseSO>().Single());
            Progression.Initialize();
            Shop = new ShopManager(new ShopManager.Settings(packs.ToList()), save, clock, Progression, Progression);
            Shop.Initialize();
        }

        // Cả hai cách trả hiện hoàn tất ngay (không chờ store thật), nên lấy kết quả đồng bộ được trong Edit Mode.
        public ShopPurchaseResult Buy(ShopPackRuntime pack) => Shop.BuyAsync(pack).GetAwaiter().GetResult();

        public void SetGold(long amount)
        {
            long current = Progression.GetBalance(CurrencyType.Gold);
            if (current > amount) Progression.TrySpend(CurrencyType.Gold, current - amount, out _);
            else if (current < amount) Progression.Add(CurrencyType.Gold, amount - current);
        }

        public void Dispose()
        {
            Shop.Dispose();
            Progression.Dispose();
            save.Dispose();
        }
    }

    /// <summary>Đồng hồ giả: giờ máy luôn có; giờ server chỉ có khi <see cref="HasServerTime"/>.</summary>
    private sealed class FakeClock : IGameClock
    {
        public DateTime Now = new DateTime(2026, 11, 2, 8, 0, 0, DateTimeKind.Utc);
        public bool HasServerTime = true;

        public event Action Changed;

        public bool TryGetUtcNow(ClockSource source, out DateTime utcNow)
        {
            utcNow = Now;
            return source == ClockSource.Device || HasServerTime;
        }

        public void RaiseChanged() => Changed?.Invoke();
    }

    #endregion

    #region Helpers

    private static ShopPackSO Pack(string id, IShopPrice price, PurchaseLimit limit, params IReward[] rewards)
    {
        var pack = ScriptableObject.CreateInstance<ShopPackSO>();
        Temporary.Add(pack);
        var data = new SerializedObject(pack);
        data.FindProperty("id").stringValue = id;
        data.FindProperty("price").managedReferenceValue = price;
        data.FindProperty("limit").intValue = (int)limit;
        data.FindProperty("limitCount").intValue = 1;
        SerializedProperty list = data.FindProperty("rewards");
        list.arraySize = rewards.Length;
        for (int i = 0; i < rewards.Length; i++) list.GetArrayElementAtIndex(i).managedReferenceValue = rewards[i];
        data.ApplyModifiedPropertiesWithoutUndo();
        pack.Validate();
        return pack;
    }

    private static CharacterDataSO Hero() => Assets<CharacterDataSO>().First(item => item.Team == CharacterTeam.Player);

    private static DateTime Utc(int year, int month, int day, int hour) => new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static IEnumerable<T> Assets<T>() where T : UnityEngine.Object
        => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Shop] " + message);
    }

    #endregion
}
