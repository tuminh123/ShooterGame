using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Kiểm tra pool object thuần trong Edit Mode: <see cref="Pool{T}"/> (object giả), <see cref="EffectRuntimePool"/> và
/// <see cref="CharacterRuntimePool"/> với template thật. Không chạm save hay scene.
/// </summary>
public static class PoolVerification
{
    #region Entry

    [MenuItem("Shooter Game/Verification/Check Object Pools")]
    public static void Run()
    {
        Require(!Application.isPlaying, "Run Check Object Pools in Edit Mode.");
        CheckPoolBasics();
        CheckPoolFailures();
        int cycles = CheckPoolStress();
        CheckEffectRuntimePool();
        CheckCharacterRuntimePool();
        CheckStatSources();
        Debug.Log("[Pools] PASS: Pool<T> rent/return/reuse/prewarm/clear, double + foreign + null return rejected, reset failure keeps counts, " +
                  cycles + " random cycles keep Created = Available + Leased; EffectRuntimePool reuse + state reset + version kept + " +
                  "double return throws; CharacterRuntimePool generation, stale return, listener + stat lease cleanup, failed init, external runtime, dispose; " +
                  "stat sources single/multi add-update-remove, 1000 add/remove cycles, disposed lease inert, Changed listener add/remove semantics.");
    }

    #endregion

    #region Stat Sources (StatSheet + StatModifierLease)

    private static void CheckStatSources()
    {
        CharacterDataSO template = Assets<CharacterDataSO>().First(item => item.Team == CharacterTeam.Enemy);
        var runtime = new CharacterRuntimeData(template);
        float baseSpeed = runtime.FinalStats.moveSpeed;
        float baseHealth = runtime.FinalStats.maxHealth;
        Require(baseSpeed > 0f, "Template needs a move speed for this check.");

        StatModifierLease slow = runtime.Stats.AddSource(new StatModifier(StatType.MoveSpeed, StatOperation.Percent, -50f));
        Require(Mathf.Approximately(runtime.FinalStats.moveSpeed, baseSpeed * 0.5f), "Single-modifier source was not applied.");
        slow.Update(new StatModifier(StatType.MoveSpeed, StatOperation.Percent, -20f));
        Require(Mathf.Approximately(runtime.FinalStats.moveSpeed, baseSpeed * 0.8f), "Single-modifier update was not applied.");
        slow.Dispose();
        Require(Mathf.Approximately(runtime.FinalStats.moveSpeed, baseSpeed), "Removing the source did not restore the stat.");
        slow.Dispose();
        slow.Update(new StatModifier(StatType.MoveSpeed, StatOperation.Percent, -90f));
        Require(Mathf.Approximately(runtime.FinalStats.moveSpeed, baseSpeed), "A disposed lease still changed stats.");

        StatModifierLease multi = runtime.Stats.AddSource(
            new StatModifier(StatType.MaxHealth, StatOperation.Add, 10f),
            new StatModifier(StatType.MoveSpeed, StatOperation.Percent, 10f));
        Require(Mathf.Approximately(runtime.FinalStats.maxHealth, baseHealth + 10f) &&
                Mathf.Approximately(runtime.FinalStats.moveSpeed, baseSpeed * 1.1f), "Multi-modifier source was not applied.");
        multi.Update(new StatModifier(StatType.MaxHealth, StatOperation.Add, 20f), new StatModifier(StatType.MoveSpeed, StatOperation.Percent, 0f));
        Require(Mathf.Approximately(runtime.FinalStats.maxHealth, baseHealth + 20f), "Multi-modifier update was not applied.");
        multi.Dispose();

        // Mảng một phần tử được dùng lại giữa các nguồn: chạy nhiều vòng, chồng nhiều nguồn, kết quả vẫn phải đúng.
        var leases = new List<StatModifierLease>();
        for (int cycle = 0; cycle < 1000; cycle++)
        {
            leases.Add(runtime.Stats.AddSource(new StatModifier(StatType.MoveSpeed, StatOperation.Percent, -10f)));
            if (leases.Count > 3 || cycle % 7 == 0)
            {
                leases[0].Dispose();
                leases.RemoveAt(0);
            }
            float expected = baseSpeed * Mathf.Max(0f, 1f - 0.1f * leases.Count);
            Require(Mathf.Approximately(runtime.FinalStats.moveSpeed, expected), "Recycled stat arrays mixed sources at cycle " + cycle + ".");
        }
        foreach (StatModifierLease lease in leases) lease.Dispose();
        Require(Mathf.Approximately(runtime.FinalStats.moveSpeed, baseSpeed) && Mathf.Approximately(runtime.FinalStats.maxHealth, baseHealth),
            "Stats were not restored after all sources were removed.");

        // Changed giữ ngữ nghĩa event thường: thêm trùng gọi hai lần, gỡ một lần còn một, listener tự gỡ giữa lúc báo vẫn an toàn.
        int calls = 0;
        Action counter = () => calls++;
        runtime.Changed += counter;
        runtime.Changed += counter;
        runtime.Stats.AddSource(new StatModifier(StatType.Damage, StatOperation.Add, 1f)).Dispose(); // 2 lần báo × 2 listener.
        Require(calls == 4, "Duplicate listener was not called twice per change (calls " + calls + ").");
        runtime.Changed -= counter;
        calls = 0;
        runtime.Stats.AddSource(new StatModifier(StatType.Damage, StatOperation.Add, 1f)).Dispose();
        Require(calls == 2, "Removing one duplicate removed both (calls " + calls + ").");
        Action selfRemoving = null;
        selfRemoving = () => runtime.Changed -= selfRemoving;
        runtime.Changed += selfRemoving;
        runtime.Changed -= counter;
        calls = 0;
        runtime.Stats.AddSource(new StatModifier(StatType.Damage, StatOperation.Add, 1f)).Dispose();
        Require(calls == 0, "A removed listener was still called.");
    }

    #endregion

    #region Pool<T>

    private sealed class Item : IPoolable
    {
        public int Resets;
        public int Value;
        public bool ThrowOnReset;

        public void ResetForPool()
        {
            Resets++;
            Value = 0;
            if (ThrowOnReset) throw new InvalidOperationException("Reset failed on purpose.");
        }
    }

    private static void CheckPoolBasics()
    {
        int factoryCalls = 0;
        var pool = new Pool<Item>(() => { factoryCalls++; return new Item(); });
        Require(pool.AvailableCount == 0 && pool.LeasedCount == 0 && pool.CreatedCount == 0, "A new pool is not empty.");

        Item first = pool.Rent();
        first.Value = 7;
        Require(factoryCalls == 1 && pool.CreatedCount == 1 && pool.LeasedCount == 1 && pool.IsLeased(first), "Rent did not create and lease.");
        Require(pool.Return(first) && first.Resets == 1 && first.Value == 0, "Return did not reset the item.");
        Require(pool.AvailableCount == 1 && pool.LeasedCount == 0 && !pool.IsLeased(first), "Return did not store the item.");

        Item again = pool.Rent();
        Require(ReferenceEquals(first, again) && factoryCalls == 1, "Rent did not reuse the returned item.");
        pool.Return(again);

        pool.Prewarm(5);
        Require(pool.AvailableCount == 5 && pool.CreatedCount == 5 && factoryCalls == 5, "Prewarm did not create only the missing items.");
        pool.Prewarm(3);
        Require(pool.AvailableCount == 5 && factoryCalls == 5, "A smaller prewarm created items.");

        var leased = new List<Item> { pool.Rent(), pool.Rent() };
        int resetsBefore = leased.Sum(item => item.Resets);
        pool.Clear();
        Require(pool.AvailableCount == 0 && pool.LeasedCount == 0 && pool.CreatedCount == 0, "Clear did not empty the pool.");
        Require(leased.Sum(item => item.Resets) == resetsBefore + 2, "Clear did not reset leased items.");
    }

    private static void CheckPoolFailures()
    {
        var pool = new Pool<Item>(() => new Item());
        Item item = pool.Rent();
        Require(pool.Return(item), "First return failed.");
        Require(!pool.Return(item) && item.Resets == 1, "A double return was accepted or reset the item twice.");
        Require(!pool.Return(new Item()), "A foreign item was accepted.");
        Require(!pool.Return(null), "A null item was accepted.");
        Require(pool.AvailableCount == 1 && pool.CreatedCount == 1, "Rejected returns changed the counts.");

        Item broken = pool.Rent();
        broken.ThrowOnReset = true;
        bool threw = false;
        try { pool.Return(broken); }
        catch (InvalidOperationException) { threw = true; }
        Require(threw && pool.AvailableCount == 1 && pool.LeasedCount == 0, "A failing reset lost the item or kept it leased.");

        threw = false;
        try { new Pool<Item>(() => null).Rent(); }
        catch (InvalidOperationException) { threw = true; }
        Require(threw, "A null factory result was accepted.");

        threw = false;
        try { pool.Prewarm(-1); }
        catch (ArgumentOutOfRangeException) { threw = true; }
        Require(threw, "A negative prewarm was accepted.");
    }

    // Thuê/trả ngẫu nhiên (seed cố định): số đếm luôn khớp, không object nào vừa rảnh vừa đang mượn.
    private static int CheckPoolStress()
    {
        const int cycles = 20000;
        var pool = new Pool<Item>(() => new Item());
        var leased = new List<Item>();
        var random = new System.Random(42);
        for (int i = 0; i < cycles; i++)
        {
            if (leased.Count == 0 || random.Next(3) > 0 && leased.Count < 64) leased.Add(pool.Rent());
            else
            {
                int index = random.Next(leased.Count);
                Require(pool.Return(leased[index]), "Stress return failed at cycle " + i + ".");
                leased.RemoveAt(index);
            }
            Require(pool.CreatedCount == pool.AvailableCount + pool.LeasedCount && pool.LeasedCount == leased.Count,
                "Counts drifted at cycle " + i + ".");
        }
        Require(leased.Distinct().Count() == leased.Count, "One item was leased twice.");
        Require(pool.CreatedCount <= 64, "Stress created more items than ever leased at once: " + pool.CreatedCount + ".");
        return cycles;
    }

    #endregion

    #region EffectRuntimePool

    private static void CheckEffectRuntimePool()
    {
        var pool = new EffectRuntimePool();
        pool.Prewarm(4, 2);
        Require(pool.AvailableSlow == 4 && pool.AvailableDamageOverTime == 2, "Effect prewarm counts are wrong.");

        SlowEffectRuntime slow = pool.RentSlow();
        SetField(slow, "SlowFraction", 0.4f);
        ulong version = (ulong)GetField(slow, "Version");
        SetField(slow, "Version", version + 3);
        pool.Return(slow);
        Require((float)GetField(slow, "SlowFraction") == 0f && GetField(slow, "Owner") == null, "Slow runtime state was not reset.");
        Require((ulong)GetField(slow, "Version") == version + 3, "Reset cleared the version (old handles could match again).");
        Require(pool.AvailableSlow == 4, "Slow runtime was not stored back.");

        bool threw = false;
        try { pool.Return(slow); }
        catch (InvalidOperationException) { threw = true; }
        Require(threw, "A double effect return did not throw.");

        DamageOverTimeEffectRuntime dot = pool.RentDamageOverTime();
        SetField(dot, "DamagePerTick", 5f);
        pool.Return(dot);
        Require((float)GetField(dot, "DamagePerTick") == 0f && pool.AvailableDamageOverTime == 2, "Damage-over-time runtime was not reset/stored.");

        var rented = new HashSet<SlowEffectRuntime>();
        for (int i = 0; i < 4; i++) rented.Add(pool.RentSlow());
        Require(rented.Contains(slow) && pool.AvailableSlow == 0, "Prewarmed slow runtimes were not reused.");
        foreach (SlowEffectRuntime runtime in rented) pool.Return(runtime);
    }

    #endregion

    #region CharacterRuntimePool

    private static void CheckCharacterRuntimePool()
    {
        CharacterDataSO template = Assets<CharacterDataSO>().First(item => item.Team == CharacterTeam.Enemy);
        var pool = new CharacterRuntimePool();
        pool.Prewarm(2);
        Require(pool.AvailableCount == 2 && pool.ActiveCount == 0 && pool.CreatedCount == 2, "Character prewarm counts are wrong.");

        CharacterRuntimeData runtime = pool.Rent(template, default);
        ulong first = runtime.Generation;
        Require(runtime.IsAlive && runtime.Team == template.Team && pool.ActiveCount == 1, "Rent did not initialize the runtime.");

        int oldListener = 0;
        runtime.Changed += () => oldListener++;
        StatModifierLease oldLease = runtime.Stats.AddSource(new StatModifier(StatType.MoveSpeed, StatOperation.Percent, -50f));
        Require(!pool.TryReturn(runtime, first + 1), "A wrong generation was accepted.");
        Require(pool.TryReturn(runtime, first) && pool.ActiveCount == 0, "The right generation was refused.");
        Require(!pool.TryReturn(runtime, first), "A double return was accepted.");

        CharacterRuntimeData reused = pool.Rent(template, default);
        Require(ReferenceEquals(runtime, reused) && reused.Generation == first + 1, "Runtime was not reused with a new generation.");
        int afterRent = oldListener;
        reused.TriggerChanged();
        oldLease.Dispose(); // Lease của lượt cũ: không được gỡ nhầm nguồn của lượt mới, không ném lỗi.
        Require(oldListener == afterRent, "A listener of the previous spawn was still called.");
        Require(Mathf.Approximately(reused.FinalStats.moveSpeed, template.BaseStats.moveSpeed), "A stat lease of the previous spawn leaked.");
        Require(!pool.TryReturn(reused, first), "A stale generation was accepted after reuse.");

        int available = pool.AvailableCount, active = pool.ActiveCount;
        bool threw = false;
        try { pool.Rent(null, default); }
        catch (ArgumentNullException) { threw = true; }
        Require(threw && pool.AvailableCount == available && pool.ActiveCount == active, "A failed initialization leaked a runtime.");

        var external = new CharacterRuntimeData(template);
        Require(!pool.TryReturn(external, external.Generation), "A runtime created outside the pool was accepted.");

        pool.Dispose();
        Require(!pool.TryReturn(reused, reused.Generation), "Return after dispose was accepted.");
        threw = false;
        try { pool.Rent(template, default); }
        catch (ObjectDisposedException) { threw = true; }
        Require(threw, "Rent after dispose did not throw.");
    }

    // Phát Changed của lượt mới (thêm rồi gỡ một nguồn rỗng) để biết listener của lượt cũ còn bị gọi không.
    private static void TriggerChanged(this CharacterRuntimeData runtime)
    {
        runtime.Stats.AddSource(new StatModifier(StatType.MaxHealth, StatOperation.Add, 0f)).Dispose();
    }

    #endregion

    #region Helpers

    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static object GetField(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Instance);
            if (field != null) return field.GetValue(target);
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }

    private static void SetField(object target, string name, object value)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Instance);
            if (field == null) continue;
            field.SetValue(target, value);
            return;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }

    private static IEnumerable<T> Assets<T>() where T : UnityEngine.Object
        => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Pools] " + message);
    }

    #endregion
}
