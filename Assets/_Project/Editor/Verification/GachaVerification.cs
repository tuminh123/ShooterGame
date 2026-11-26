using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kiểm tra miền Gacha trong Edit Mode: công thức quay (tỉ lệ thực khớp công thức, hard pity, bảo đảm theo nhóm lượt) và
/// GachaManager với Save/Progression thật trên một FILE SAVE TẠM riêng (xóa khi xong), nên không chạm hồ sơ người chơi.
/// </summary>
public static class GachaVerification
{
    #region Entry

    private const string SaveFile = "GachaVerification.es3";
    private const int Seed = 12345;
    private static readonly List<UnityEngine.Object> Temporary = new List<UnityEngine.Object>();

    [MenuItem("Shooter Game/Verification/Check Gacha")]
    public static void Run()
    {
        Require(!Application.isPlaying, "Run Check Gacha in Edit Mode.");
        Require(!ES3.FileExists(SaveFile), "Temporary save already exists: " + SaveFile);
        try
        {
            CheckProjectBanners();
            string rates = CheckRoller();
            CheckManager();
            Debug.Log("[Gacha] PASS: project banners valid, " + rates + ", hard pity and guarantee never missed, " +
                      "not enough currency changes nothing, x10 spends once and saves pity, duplicate heroes become shards.");
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

    private static void CheckProjectBanners()
    {
        foreach (GachaBannerSO banner in Assets<GachaBannerSO>()) banner.Validate();
    }

    // Quay rất nhiều lượt bằng seed cố định: tỉ lệ ra bậc pity phải khớp 1/E của công thức; không bao giờ vượt hard pity
    // và không có nhóm G lượt liên tiếp nào thiếu bậc bảo đảm.
    private static string CheckRoller()
    {
        GachaBannerSO banner = CreateBanner("roller", new EquipmentGachaPool());
        SetEquipmentPool(banner);
        const int pulls = 200000;
        var random = new System.Random(Seed);
        var counter = new GachaSaveData.Banner { bannerId = banner.Id };
        int tops = 0, sinceTop = 0, sinceGuarantee = 0;
        for (int i = 0; i < pulls; i++)
        {
            GachaTier tier = GachaRoller.Roll(banner, counter, random);
            sinceTop++;
            sinceGuarantee++;
            if (tier == banner.PityTier) { tops++; sinceTop = 0; }
            if (tier >= banner.GuaranteeTier) sinceGuarantee = 0;
            Require(sinceTop < banner.HardPity, "Hard pity was missed.");
            Require(sinceGuarantee < banner.GuaranteeEvery, "Guarantee tier was missed in " + banner.GuaranteeEvery + " pulls.");
        }
        float expected = 1f / GachaRoller.AveragePullsForTop(banner);
        float actual = tops / (float)pulls;
        Require(Mathf.Abs(actual - expected) / expected < 0.05f,
            "Legendary rate " + actual.ToString("P2") + " does not match the formula " + expected.ToString("P2") + ".");
        return "Legendary " + actual.ToString("P2") + " vs formula " + expected.ToString("P2");
    }

    private static void CheckManager()
    {
        CharacterDataSO hero = Assets<CharacterDataSO>().First(item => item.Team == CharacterTeam.Player);
        GachaBannerSO heroes = CreateBanner("heroes", new CharacterGachaPool());
        var data = new SerializedObject(heroes);
        SerializedProperty characters = data.FindProperty("pool").FindPropertyRelative("characters");
        characters.arraySize = 1;
        characters.GetArrayElementAtIndex(0).objectReferenceValue = hero;
        data.ApplyModifiedPropertiesWithoutUndo();
        heroes.Validate();
        GachaBannerSO gear = CreateBanner("gear", new EquipmentGachaPool());
        SetEquipmentPool(gear);
        int gearRemaining;

        using (var world = new World(heroes, gear))
        {
            // Pool tướng chỉ có bậc của một nhân vật: không có Legendary thì không hứa hẹn pity.
            Require(heroes.Pool.HasTier(GachaTier.Legendary) || world.Gacha.PullsUntilPity(heroes) == 0, "Pity shown for a tier the pool cannot give.");
            Require(world.Gacha.PullsUntilPity(gear) == gear.HardPity, "A new banner must start with full pity.");
            world.SetDiamonds(10);
            Require(world.Gacha.GetBlock(heroes, 10) == PurchaseBlock.NotEnoughCurrency, "x10 allowed without diamonds.");
            GachaPullResult blocked = world.Gacha.Pull(heroes, 10);
            Require(blocked.Block == PurchaseBlock.NotEnoughCurrency && world.Diamonds == 10, "A blocked pull changed the wallet.");

            world.SetDiamonds(2000);
            int changed = 0;
            world.Gacha.Changed += () => changed++;
            GachaPullResult result = world.Gacha.Pull(heroes, 10);
            Require(result.Block == PurchaseBlock.None && result.Saved && result.Tiers.Count == 10, "x10 did not pull ten times.");
            Require(world.Diamonds == 2000 - heroes.GetCost(10).Amount && changed == 1, "x10 did not spend its price exactly once.");
            int given = result.Rewards.NewCharacters.Count + result.Rewards.Shards.Count;
            Require(given == 10 && result.Rewards.NewCharacters.Count <= 1, "Duplicate heroes did not become shards.");

            world.SetDiamonds(2000);
            GachaPullResult items = world.Gacha.Pull(gear, 10);
            Require(items.Block == PurchaseBlock.None && items.Rewards.Equipment.Count == 10, "Gear x10 did not give ten items.");
            gearRemaining = world.Gacha.PullsUntilPity(gear);
            Require(gearRemaining < gear.HardPity || items.HighestTier == GachaTier.Legendary, "Gear pity did not count the pulls.");
        }

        // Mở lại: bộ đếm pity đọc lại từ save, không về 0.
        using (var world = new World(heroes, gear))
            Require(world.Gacha.PullsUntilPity(gear) == gearRemaining, "Pity counter was not restored from the save.");
    }

    #endregion

    #region World

    private sealed class World : IDisposable
    {
        private readonly SaveManager save;
        private readonly ProgressionManager progression;
        public readonly GachaManager Gacha;

        public World(params GachaBannerSO[] banners)
        {
            save = new SaveManager(new SaveManager.Settings(SaveFile));
            save.Initialize();
            progression = new ProgressionManager(save, Assets<CharacterDatabaseSO>().Single(), Assets<LevelDatabaseSO>().Single(),
                Assets<EquipmentDatabaseSO>().Single());
            progression.Initialize();
            Gacha = new GachaManager(new GachaManager.Settings(banners.ToList(), Seed), save, progression, progression);
            Gacha.Initialize();
        }

        public long Diamonds => progression.GetBalance(CurrencyType.Diamond);

        public void SetDiamonds(long amount)
        {
            long current = Diamonds;
            if (current > amount) progression.TrySpend(CurrencyType.Diamond, current - amount, out _);
            else if (current < amount) progression.Add(CurrencyType.Diamond, amount - current);
        }

        public void Dispose()
        {
            progression.Dispose();
            save.Dispose();
        }
    }

    #endregion

    #region Helpers

    // Bảng tỉ lệ đề xuất: 1 / 5 / 15 / 30 / 49, soft 46 (+5%), hard 60, Epic mỗi 10 lượt.
    private static GachaBannerSO CreateBanner(string id, IGachaPool pool)
    {
        var banner = ScriptableObject.CreateInstance<GachaBannerSO>();
        Temporary.Add(banner);
        var data = new SerializedObject(banner);
        data.FindProperty("id").stringValue = id;
        data.FindProperty("pool").managedReferenceValue = pool;
        SerializedProperty rates = data.FindProperty("rates");
        float[] weights = { 49f, 30f, 15f, 5f, 1f };
        rates.arraySize = weights.Length;
        for (int i = 0; i < weights.Length; i++)
        {
            SerializedProperty rate = rates.GetArrayElementAtIndex(i);
            rate.FindPropertyRelative("tier").intValue = i;
            rate.FindPropertyRelative("weight").floatValue = weights[i];
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        return banner;
    }

    private static void SetEquipmentPool(GachaBannerSO banner)
    {
        var data = new SerializedObject(banner);
        SerializedProperty items = data.FindProperty("pool").FindPropertyRelative("items");
        items.arraySize = 1;
        items.GetArrayElementAtIndex(0).objectReferenceValue = Assets<EquipmentDatabaseSO>().Single().Items[0];
        data.ApplyModifiedPropertiesWithoutUndo();
        banner.Validate();
    }

    private static IEnumerable<T> Assets<T>() where T : UnityEngine.Object
        => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Gacha] " + message);
    }

    #endregion
}
