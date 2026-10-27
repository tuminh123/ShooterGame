# Review code tìm kiếm, combat và skill

Bản chụp code hiện tại, gồm 10 script mới và 13 script sửa. Có cả thay đổi Enemy chạm gây damage, Enemy bị đẩy lùi và Player nháy đỏ. Không thay đổi các script gameplay khi tạo bản review này.

Luồng bắn: CharacterSkills → ProjectileSkill → CharacterSearch → CharacterRuntimeData.CreateDamage → ProjectileService → ProjectileView → CharacterView.ReceiveDamage → CharacterRuntimeData.ReceiveDamage → OnDeath / VFX.

Luồng tiếp xúc: EnemyController.Tick → CharacterView.Overlaps → CharacterRuntimeData.CreateDamage → Player.ReceiveDamage → nháy đỏ → Enemy.ApplyKnockback → tiếp tục đuổi.

Các diff dưới đây so với Git HEAD; một số file cũng chứa thay đổi đã thực hiện trước phần combat, chẳng hạn debug tránh nhau. Code đầy đủ là trạng thái hiện tại để review mối liên hệ. Không chạy test hoặc Play Mode.

## Script tạo mới

### CharacterSearch.cs

Nguồn: [Assets/_Project/Scripts/Characters/CharacterSearch.cs](../Assets/_Project/Scripts/Characters/CharacterSearch.cs)

```csharp
using System;
using System.Buffers;
using UnityEngine;

// * Kết quả chỉ tồn tại trong phạm vi using trên main thread; không giữ qua frame/await.
// ! Không sao chép biến kết quả hoặc Dispose hai bản sao: buffer chỉ có một chủ sở hữu.
// * ref struct không tạo object quản lý kết quả trên managed heap mỗi lần quét.
public ref struct CharacterSearchResult
{
    private Collider2D[] hits;
    private CharacterRuntimeData[] identities;
    public int Count { get; private set; }

    internal CharacterSearchResult(Collider2D[] hits, CharacterRuntimeData[] identities, int count)
    {
        this.hits = hits;
        this.identities = identities;
        Count = count;
    }

    public CharacterTarget this[int index]
    {
        get
        {
            if (hits == null || index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            CharacterView view = null;
            if (hits[index] != null)
                hits[index].TryGetComponent(out view);
            return new CharacterTarget(view, identities[index]);
        }
    }

    public void Dispose()
    {
        if (hits == null)
            return;
        ArrayPool<Collider2D>.Shared.Return(hits, clearArray: true);
        ArrayPool<CharacterRuntimeData>.Shared.Return(identities, clearArray: true);
        hits = null;
        identities = null;
        Count = 0;
    }
}

// * Truy vấn hình học dùng chung; không gây damage, không chạy AI hoặc spawn VFX.
// * Mỗi Character có một hitbox trên vỏ gốc. Visual con không nằm trên layer gameplay.
public static class CharacterSearch
{
    private static int lastSyncFrame = -1;
    private static bool physicsDirty = true;

    // * Spawn/despawn hoặc hệ di chuyển gọi khi thay đổi hình học trong cùng frame.
    public static void InvalidatePhysics() => physicsDirty = true;

    // * Dùng chung cho query vùng và kiểm tra khoảng cách giữa hai hitbox đã biết.
    // * Collider di chuyển bằng Transform cần được đồng bộ trước khi đọc hình học physics.
    public static void SynchronizePhysics()
    {
        if (!physicsDirty && lastSyncFrame == Time.frameCount)
            return;
        Physics2D.SyncTransforms();
        physicsDirty = false;
        lastSyncFrame = Time.frameCount;
    }

    public static CharacterTeam OpponentOf(CharacterTeam team)
    {
        if (team == CharacterTeam.Player) return CharacterTeam.Enemy;
        if (team == CharacterTeam.Enemy) return CharacterTeam.Player;
        throw new ArgumentOutOfRangeException(nameof(team));
    }

    public static CharacterTarget FindNearest(Vector2 center, float radius,
        CharacterTeam targetTeam, CharacterView exclude = null)
    {
        using (var results = FindAll(center, radius, targetTeam, exclude))
        {
            CharacterTarget nearest = default;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < results.Count; i++)
            {
                CharacterTarget candidate = results[i];
                if (!candidate.TryGetCharacter(out CharacterView view))
                    continue;
                float distance = ((Vector2)view.HitPosition - center).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = candidate;
                }
            }
            return nearest;
        }
    }

    public static CharacterSearchResult FindAll(Vector2 center, float radius,
        CharacterTeam targetTeam, CharacterView exclude = null)
    {
        if (!IsFinite(center.x) || !IsFinite(center.y) || !IsFinite(radius) || radius < 0f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Search geometry must be finite and radius nonnegative.");
        string layer = targetTeam == CharacterTeam.Player ? GameConstants.PhysicsLayers.Player :
            targetTeam == CharacterTeam.Enemy ? GameConstants.PhysicsLayers.Enemy :
            throw new ArgumentOutOfRangeException(nameof(targetTeam));
        int layerIndex = LayerMask.NameToLayer(layer);
        if (layerIndex < 0)
            throw new InvalidOperationException($"Add the {layer} layer in Project Settings.");
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = 1 << layerIndex, useTriggers = true };

        // * Gộp đồng bộ Transform; đạn không có collider nên di chuyển đạn không làm bẩn physics.
        SynchronizePhysics();

        Collider2D[] hits = ArrayPool<Collider2D>.Shared.Rent(32);
        CharacterRuntimeData[] identities = null;
        try
        {
            int count;
            while (true)
            {
                count = Physics2D.OverlapCircle(center, radius, filter, hits);
                if (count < hits.Length) break;
                // * Đầy buffer chưa chắc đã đủ kết quả; quét lại trước khi có bất kỳ tác động nào.
                Collider2D[] larger = ArrayPool<Collider2D>.Shared.Rent(checked(hits.Length * 2));
                ArrayPool<Collider2D>.Shared.Return(hits, clearArray: true);
                hits = larger;
            }

            identities = ArrayPool<CharacterRuntimeData>.Shared.Rent(Mathf.Max(1, count));
            int validCount = 0;
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || !hit.TryGetComponent(out CharacterView view) || view == exclude ||
                    !view.isActiveAndEnabled || view.RuntimeData == null ||
                    !view.RuntimeData.IsAlive || view.RuntimeData.Team != targetTeam)
                    continue;
                hits[validCount] = hit;
                // * Chốt danh tính lúc quét để callback damage không đổi kết quả sang cá thể pool mới.
                identities[validCount++] = view.RuntimeData;
            }
            return new CharacterSearchResult(hits, identities, validCount);
        }
        catch
        {
            ArrayPool<Collider2D>.Shared.Return(hits, clearArray: true);
            if (identities != null)
                ArrayPool<CharacterRuntimeData>.Shared.Return(identities, clearArray: true);
            throw;
        }
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
```

### CharacterTarget.cs

Nguồn: [Assets/_Project/Scripts/Characters/CharacterTarget.cs](../Assets/_Project/Scripts/Characters/CharacterTarget.cs)

```csharp
using UnityEngine;

// * Tham chiếu đến một lượt spawn, không chỉ đến GameObject mà pool có thể tái sử dụng.
// * Runtime mới mỗi lượt spawn đóng vai trò danh tính; không cần thêm ID vào template SO.
public readonly struct CharacterTarget
{
    private readonly CharacterView view;
    private readonly CharacterRuntimeData runtime;

    public CharacterTarget(CharacterView view)
    {
        this.view = view;
        runtime = view != null ? view.RuntimeData : null;
    }

    internal CharacterTarget(CharacterView view, CharacterRuntimeData runtime)
    {
        this.view = view;
        this.runtime = runtime;
    }

    public bool TryGetCharacter(out CharacterView character)
    {
        character = view;
        return view != null && view.isActiveAndEnabled && runtime != null &&
            ReferenceEquals(view.RuntimeData, runtime) && runtime.IsAlive;
    }
}
```

### DamageInfo.cs

Nguồn: [Assets/_Project/Scripts/Characters/DamageInfo.cs](../Assets/_Project/Scripts/Characters/DamageInfo.cs)

```csharp
using System;

// * Đầu ra đã tính ở Character nguồn; đạn chỉ giữ bản sao, không tính lại theo stat mới.
public readonly struct DamageInfo
{
    public float Amount { get; }
    public CharacterTeam SourceTeam { get; }
    public int CritTier { get; }

    public DamageInfo(float amount, CharacterTeam sourceTeam, int critTier = 0)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
            throw new ArgumentOutOfRangeException(nameof(amount));
        Amount = amount;
        SourceTeam = sourceTeam;
        CritTier = critTier;
    }
}

// * Trả lượng HP thực sự mất để phần hiển thị dùng kết quả, không tự tính lại công thức.
public readonly struct DamageResult
{
    public float HealthLost { get; }
    public bool Accepted => HealthLost > 0f;
    public bool Killed { get; }

    public DamageResult(float healthLost, bool killed)
    {
        HealthLost = healthLost;
        Killed = killed;
    }
}

public interface IDamageReceiver
{
    DamageResult ReceiveDamage(DamageInfo damage);
}
```

### SkillContext.cs

Nguồn: [Assets/_Project/Scripts/Skills/SkillContext.cs](../Assets/_Project/Scripts/Skills/SkillContext.cs)

```csharp
// * Dependency của skill runtime, không serialize tham chiếu scene lên mẫu skill.
public sealed class SkillContext
{
    public CharacterView Owner { get; }
    public ProjectileService Projectiles { get; }

    public SkillContext(CharacterView owner, ProjectileService projectiles)
    {
        Owner = owner;
        Projectiles = projectiles;
    }
}
```

### CharacterSkills.cs

Nguồn: [Assets/_Project/Scripts/Skills/CharacterSkills.cs](../Assets/_Project/Scripts/Skills/CharacterSkills.cs)

```csharp
using System;
using System.Collections.Generic;

// * Bộ skill của một lượt spawn; cả Player và Enemy dùng cùng vòng đời.
// * Chỉ giữ bản runtime; mẫu trên CharacterDataSO luôn được để nguyên.
public sealed class CharacterSkills : IDisposable
{
    private readonly List<ISkill> skills = new List<ISkill>();

    public CharacterSkills(CharacterDataSO template, SkillContext context)
    {
        try
        {
            AddTemplates(template.PassiveSkills, context);
            AddTemplates(template.ActiveSkills, context);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private void AddTemplates(IReadOnlyList<ISkill> templates, SkillContext context)
    {
        for (int i = 0; i < templates.Count; i++)
        {
            if (templates[i] == null)
                continue;
            ISkill runtime = templates[i].CreateRuntime(context);
            if (runtime == null || ReferenceEquals(runtime, templates[i]))
                throw new InvalidOperationException("A skill template must create a separate runtime instance.");
            skills.Add(runtime);
            runtime.Activate();
        }
    }

    public void Tick(float deltaTime)
    {
        for (int i = 0; i < skills.Count; i++)
            if (skills[i].IsActive)
                skills[i].Tick(deltaTime);
    }

    public void Dispose()
    {
        for (int i = skills.Count - 1; i >= 0; i--)
            skills[i].Deactivate();
        skills.Clear();
    }
}
```

### ProjectileSkill.cs

Nguồn: [Assets/_Project/Scripts/Skills/ProjectileSkill.cs](../Assets/_Project/Scripts/Skills/ProjectileSkill.cs)

```csharp
using System;
using UnityEngine;

// * Skill tự động bắn vào mục tiêu gần nhất; dùng được cho mọi Character có gán mẫu này.
// * Chỉ field cấu hình được serialize; context/cooldown là dữ liệu riêng của bản runtime.
[Serializable]
public sealed class ProjectileSkill : ISkill
{
    #region Template Configuration

    [SerializeField] private ProjectileView projectilePrefab;
    [SerializeField, Min(0.01f)] private float cooldown = 0.6f;
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;
    [SerializeField, Min(0.01f)] private float targetSearchInterval = 0.1f;
    [SerializeField] private Vector2 muzzleOffset = new Vector2(0f, 0.02f);

    #endregion

    [NonSerialized] private SkillContext context;
    [NonSerialized] private float nextShotTime;
    [NonSerialized] private float nextSearchTime;
    [NonSerialized] private bool isActive;
    public bool IsActive => isActive;

    public ISkill CreateRuntime(SkillContext runtimeContext)
    {
        if (runtimeContext == null || runtimeContext.Projectiles == null || projectilePrefab == null)
            throw new InvalidOperationException("Projectile skill needs context, ProjectileService and a projectile prefab.");
        return new ProjectileSkill
        {
            projectilePrefab = projectilePrefab,
            cooldown = Mathf.Max(0.01f, cooldown),
            damageMultiplier = Mathf.Max(0f, damageMultiplier),
            targetSearchInterval = Mathf.Max(0.01f, targetSearchInterval),
            muzzleOffset = muzzleOffset,
            context = runtimeContext
        };
    }

    public void Activate()
    {
        if (context == null)
            throw new InvalidOperationException("Activate a runtime skill, not the shared template.");
        isActive = true;
    }

    public void Deactivate() => isActive = false;

    public void Tick(float deltaTime)
    {
        CharacterView owner = context.Owner;
        if (!isActive || deltaTime <= 0f || owner == null || owner.RuntimeData == null ||
            !owner.RuntimeData.IsAlive || Time.time < nextShotTime || Time.time < nextSearchTime)
            return;

        // * Không có mục tiêu thì thử lại theo nhịp tìm kiếm, không quét liên tục từng frame.
        nextSearchTime = Time.time + targetSearchInterval;
        CharacterRuntimeData runtime = owner.RuntimeData;
        CharacterTarget target = CharacterSearch.FindNearest(owner.HitPosition,
            runtime.FinalStats.range, CharacterSearch.OpponentOf(runtime.Team), owner);
        if (!target.TryGetCharacter(out _))
            return;

        DamageInfo damage = runtime.CreateDamage(damageMultiplier);
        context.Projectiles.Fire(projectilePrefab, (Vector2)owner.HitPosition + muzzleOffset, target, damage);
        // * Không bắn bù nhiều viên trong một frame khi game bị chậm.
        nextShotTime = Time.time + cooldown;
    }
}
```

### ProjectileService.cs

Nguồn: [Assets/_Project/Scripts/Skills/ProjectileService.cs](../Assets/_Project/Scripts/Skills/ProjectileService.cs)

```csharp
using UnityEngine;

// * Tạo đạn và VFX bằng pool hiện có; không tính damage hoặc quyết định ai cần tấn công.
public sealed class ProjectileService
{
    private readonly PoolManager pools;

    public ProjectileService(PoolManager pools) => this.pools = pools;

    public void Fire(ProjectileView prefab, Vector2 position, CharacterTarget target, DamageInfo damage)
    {
        if (!target.TryGetCharacter(out CharacterView character))
            return;
        Vector2 direction = (Vector2)character.HitPosition - position;
        if (direction.sqrMagnitude < 0.000001f)
            direction = Vector2.right;
        // * Chuẩn bị trạng thái khi object còn inactive để không chạy một frame với damage/hướng cũ.
        pools.Spawn(prefab.gameObject, position, Quaternion.identity, PoolGroup.Bullets,
            beforeActivate: clone => clone.GetComponent<ProjectileView>()
                .Initialize(this, target, damage, direction.normalized));
    }

    internal void Release(ProjectileView projectile) => pools.Despawn(projectile.gameObject);

    internal void PlayImpact(ImpactVfx prefab, Vector3 position)
    {
        if (prefab == null)
            return;
        pools.Spawn(prefab.gameObject, position, Quaternion.identity, PoolGroup.Other,
            beforeActivate: clone => clone.GetComponent<ImpactVfx>().Initialize(pools));
    }
}
```

### ProjectileView.cs

Nguồn: [Assets/_Project/Scripts/Skills/ProjectileView.cs](../Assets/_Project/Scripts/Skills/ProjectileView.cs)

```csharp
using UnityEngine;

public enum ProjectileFlight
{
    Straight,
    Homing
}

// * Đạn không có collider/Rigidbody: hình tròn query là vùng trúng của đạn.
// * Mục tiêu ban đầu chỉ điều hướng; Character thực sự nằm trong vùng trúng mới nhận damage.
public sealed class ProjectileView : MonoBehaviour
{
    #region Projectile Configuration

    [SerializeField] private ProjectileFlight flight;
    [SerializeField, Min(0.01f)] private float speed = 2.4f;
    [SerializeField, Min(0.01f)] private float hitRadius = 0.035f;
    [SerializeField, Min(0.01f)] private float lifetime = 3f;
    [SerializeField, Min(0.01f)] private float visualWidth = 0.18f;
    [SerializeField] private ImpactVfx impactPrefab;

    #endregion

    private ProjectileService service;
    private CharacterTarget target;
    private DamageInfo damage;
    private Vector2 direction;
    private float age;
    private bool flying;

    internal void Initialize(ProjectileService service, CharacterTarget target, DamageInfo damage, Vector2 direction)
    {
        this.service = service;
        this.target = target;
        this.damage = damage;
        this.direction = direction;
        age = 0f;
        flying = true;
        // * Chuẩn hóa kích thước theo world unit, không phụ thuộc độ phân giải ảnh nguồn.
        if (TryGetComponent(out SpriteRenderer sprite) && sprite.sprite != null)
            transform.localScale = Vector3.one * (visualWidth / sprite.sprite.bounds.size.x);
        FaceFlightDirection();
    }

    private void Update()
    {
        if (!flying || Time.deltaTime <= 0f)
            return;
        age += Time.deltaTime;
        if (age >= Mathf.Max(0.01f, lifetime))
        {
            flying = false;
            service.Release(this);
            return;
        }

        if (flight == ProjectileFlight.Homing && target.TryGetCharacter(out CharacterView character))
        {
            Vector2 toTarget = character.HitPosition - transform.position;
            if (toTarget.sqrMagnitude > 0.000001f)
                direction = toTarget.normalized;
        }
        // * Mục tiêu mất/chết/đổi lượt spawn: giữ direction cuối, không tự chọn mục tiêu mới.
        FaceFlightDirection();
        if (TryHit())
            return;

        // * Vẫn dùng các hình tròn tại vị trí, không cast đường bay.
        // * Chia bước dài thành các bước nhỏ để các vùng kiểm tra không bị cách quá xa khi FPS giảm.
        // * Trần bước tránh đột biến query khi hitch; frame quá dài làm đạn tiến ít hơn thay vì xuyên mục tiêu.
        float radius = Mathf.Max(0.01f, hitRadius);
        float distance = Mathf.Min(Mathf.Max(0f, speed) * Time.deltaTime, radius * 32f);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / radius));
        Vector3 step = (Vector3)(direction * (distance / steps));
        for (int i = 0; i < steps; i++)
        {
            transform.position += step;
            if (TryHit())
                return;
        }
    }

    private bool TryHit()
    {
        CharacterTarget contact = CharacterSearch.FindNearest(transform.position,
            Mathf.Max(0.01f, hitRadius), CharacterSearch.OpponentOf(damage.SourceTeam));
        if (!contact.TryGetCharacter(out CharacterView character))
            return false;

        // * Đóng lượt đạn trước callback: không gây damage lặp dù callback tạo đòn đánh mới.
        flying = false;
        Vector3 hitPosition = transform.position;
        ProjectileService owner = service;
        ImpactVfx impact = impactPrefab;
        try
        {
            DamageResult result = character.ReceiveDamage(damage);
            if (result.Accepted)
                owner.PlayImpact(impact, hitPosition);
        }
        finally
        {
            owner.Release(this);
        }
        return true;
    }

    private void FaceFlightDirection()
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void OnDisable()
    {
        flying = false;
        target = default;
        damage = default;
        service = null;
    }

    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        using (new UnityEditor.Handles.DrawingScope(Color.cyan, Matrix4x4.identity))
            UnityEditor.Handles.DrawWireDisc(transform.position, Vector3.forward, Mathf.Max(0.01f, hitRadius));
    }
    #endif
}
```

### ImpactVfx.cs

Nguồn: [Assets/_Project/Scripts/Skills/ImpactVfx.cs](../Assets/_Project/Scripts/Skills/ImpactVfx.cs)

```csharp
using UnityEngine;

// * Flash trúng đòn ngắn, chỉ thay đổi hình ảnh; không mang collider hoặc gây damage.
[RequireComponent(typeof(SpriteRenderer))]
public sealed class ImpactVfx : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 0.14f;
    [SerializeField, Min(0.01f)] private float visualWidth = 0.16f;
    private PoolManager pools;
    private SpriteRenderer sprite;
    private float age;
    private float baseScale;

    internal void Initialize(PoolManager pools)
    {
        this.pools = pools;
        if (sprite == null)
            sprite = GetComponent<SpriteRenderer>();
        age = 0f;
        sprite.color = Color.white;
        baseScale = sprite.sprite != null ? visualWidth / sprite.sprite.bounds.size.x : 1f;
        transform.localScale = Vector3.one * baseScale;
    }

    private void Update()
    {
        if (pools == null || Time.deltaTime <= 0f)
            return;
        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / Mathf.Max(0.01f, duration));
        sprite.color = new Color(1f, 1f, 1f, 1f - progress);
        transform.localScale = Vector3.one * baseScale * Mathf.Lerp(0.6f, 1.25f, progress);
        if (progress >= 1f)
            pools.Despawn(gameObject);
    }

    private void OnDisable() => pools = null;
}
```

### CharacterSkillEditor.cs

Nguồn: [Assets/_Project/Editor/CharacterSkillEditor.cs](../Assets/_Project/Editor/CharacterSkillEditor.cs)

```csharp
using UnityEditor;

// * Thêm mẫu SerializeReference trong Inspector, không phải chỉnh YAML bằng tay.
[CustomEditor(typeof(CharacterDataSO))]
public sealed class CharacterSkillEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (UnityEngine.GUILayout.Button("Add Projectile Skill"))
        {
            serializedObject.Update();
            SerializedProperty skills = serializedObject.FindProperty("activeSkills");
            int index = skills.arraySize;
            skills.arraySize++;
            skills.GetArrayElementAtIndex(index).managedReferenceValue = new ProjectileSkill();
            serializedObject.ApplyModifiedProperties();
        }
        EditorGUILayout.HelpBox("Each template creates a separate skill runtime per spawn. Assign a projectile prefab before playing.", MessageType.Info);
    }
}
```

## Script sửa đổi

### BaseStats.cs

Nguồn: [Assets/_Project/Scripts/Characters/Stats/BaseStats.cs](../Assets/_Project/Scripts/Characters/Stats/BaseStats.cs)

Phần thay đổi so với HEAD:

```diff
diff --git a/Assets/_Project/Scripts/Characters/Stats/BaseStats.cs b/Assets/_Project/Scripts/Characters/Stats/BaseStats.cs
index 85a5d25..a6dd132 100644
--- a/Assets/_Project/Scripts/Characters/Stats/BaseStats.cs
+++ b/Assets/_Project/Scripts/Characters/Stats/BaseStats.cs
@@ -11,6 +11,7 @@ public struct BaseStats
     [UnityEngine.Min(0f)] public float damage;     // * Sát thương cơ bản, nhân với hệ số skill trước khi tính crit và defense.
     [UnityEngine.Min(0f)] public float defense;    // * Điểm phòng thủ; sát thương nhận = sát thương đầu vào × 100 / (100 + defense).
     [UnityEngine.Min(0f)] public float moveSpeed;  // * Tốc độ chạy, tính bằng đơn vị Unity mỗi giây.
+    [UnityEngine.Min(0f)] public float range;      // * Bán kính tìm mục tiêu; không phải bán kính đạn hoặc vùng nổ.
 
     #endregion
 
@@ -37,6 +38,7 @@ public struct BaseStats
         damage = 10f,
         defense = 0f,
         moveSpeed = 3f,
+        range = 2f,
         critChance = 0.05f,
         critDamageMultiplier = 1.5f,
         dodge = 0f,
```

<details><summary>Code đầy đủ hiện tại</summary>

```csharp

// * Bộ dữ liệu chỉ số cơ bản; Serializable cho phép Unity lưu và hiển thị khi được chứa trong field phù hợp.
// * CharacterRuntimeData sao chép bộ chỉ số này cho từng Player/Enemy và áp dụng bonus riêng của cá thể.
[System.Serializable]
public struct BaseStats
{

    #region Base Attributes

    [UnityEngine.Min(1f)] public float maxHealth;  // * Máu tối đa; máu hiện tại sẽ nằm riêng trong dữ liệu runtime.
    [UnityEngine.Min(0f)] public float damage;     // * Sát thương cơ bản, nhân với hệ số skill trước khi tính crit và defense.
    [UnityEngine.Min(0f)] public float defense;    // * Điểm phòng thủ; sát thương nhận = sát thương đầu vào × 100 / (100 + defense).
    [UnityEngine.Min(0f)] public float moveSpeed;  // * Tốc độ chạy, tính bằng đơn vị Unity mỗi giây.
    [UnityEngine.Min(0f)] public float range;      // * Bán kính tìm mục tiêu; không phải bán kính đạn hoặc vùng nổ.

    #endregion

    #region Combat Chances & Multipliers

    [UnityEngine.Min(0f)] public float critChance;            // * 1 = 100%, 1.5 = 150%; cho phép vượt 100% để tăng bậc crit.
    [UnityEngine.Min(1f)] public float critDamageMultiplier;  // * Tổng hệ số crit bậc 1; 1.5 tương ứng 150% sát thương đòn thường.

    #endregion

    #region Status Effect Accuracy & Resistance

    [UnityEngine.Min(0f)] public float dodge;     // * Điểm kháng hiệu ứng bất lợi; không né sát thương hoặc va chạm đạn.
    [UnityEngine.Min(0f)] public float accuracy;  // * Điểm chính xác hiệu ứng; đối kháng với dodge của mục tiêu.

    #endregion

    #region Default Template

    // * Trả về bản sao bộ chỉ số mặc định; new BaseStats() và default(BaseStats) vẫn có toàn bộ field bằng 0.
    public static BaseStats Default => new BaseStats
    {
        maxHealth = 100f,
        damage = 10f,
        defense = 0f,
        moveSpeed = 3f,
        range = 2f,
        critChance = 0.05f,
        critDamageMultiplier = 1.5f,
        dodge = 0f,
        accuracy = 0f
    };

    #endregion

}
```

</details>

### StatBonus.cs

Nguồn: [Assets/_Project/Scripts/Characters/Stats/StatBonus.cs](../Assets/_Project/Scripts/Characters/Stats/StatBonus.cs)

Phần thay đổi so với HEAD:

```diff
diff --git a/Assets/_Project/Scripts/Characters/Stats/StatBonus.cs b/Assets/_Project/Scripts/Characters/Stats/StatBonus.cs
index e7c2bfc..c99f3ce 100644
--- a/Assets/_Project/Scripts/Characters/Stats/StatBonus.cs
+++ b/Assets/_Project/Scripts/Characters/Stats/StatBonus.cs
@@ -62,6 +62,7 @@ public struct StatBonus
         source.damage = UnityEngine.Mathf.Max(0f, source.damage + damage);
         source.defense = UnityEngine.Mathf.Max(0f, source.defense + defense);
         source.moveSpeed = UnityEngine.Mathf.Max(0f, source.moveSpeed + moveSpeed);
+        source.range = UnityEngine.Mathf.Max(0f, source.range); // * Chưa có nguồn bonus range trong gameplay hiện tại.
         source.critChance = UnityEngine.Mathf.Max(0f, source.critChance + critChance);
         source.critDamageMultiplier = UnityEngine.Mathf.Max(1f, source.critDamageMultiplier + critDamageMultiplier);
         source.dodge = UnityEngine.Mathf.Max(0f, source.dodge + dodge);
```

<details><summary>Code đầy đủ hiện tại</summary>

```csharp
using System;

// * Lượng chỉ số cộng thêm từ level; mặc định toàn bộ bằng 0, khác với bộ chỉ số gốc BaseStats.
[Serializable]
public struct StatBonus
{
    #region Additive Bonuses

    public float maxHealth;             // * Số HP tối đa cộng thêm, không tự hồi máu hiện tại.
    public float damage;                // * Sát thương cơ bản cộng thêm.
    public float defense;               // * Điểm phòng thủ cộng thêm.
    public float moveSpeed;             // * Tốc độ cộng thêm, đơn vị Unity mỗi giây.
    public float critChance;            // * Cộng theo điểm phần trăm; 0.05 tương ứng thêm 5 điểm phần trăm crit.
    public float critDamageMultiplier;  // * Phần cộng vào hệ số; +0.25 biến 1.5 thành 1.75.
    public float dodge;                 // * Điểm kháng hiệu ứng bất lợi cộng thêm.
    public float accuracy;              // * Điểm chính xác hiệu ứng cộng thêm.

    #endregion

    #region Bonus Aggregation

    // * Cộng hai nguồn bonus theo từng field; không nhân phần trăm hoặc thay đổi template.
    public static StatBonus operator +(StatBonus left, StatBonus right)
    {
        return new StatBonus
        {
            maxHealth = left.maxHealth + right.maxHealth,
            damage = left.damage + right.damage,
            defense = left.defense + right.defense,
            moveSpeed = left.moveSpeed + right.moveSpeed,
            critChance = left.critChance + right.critChance,
            critDamageMultiplier = left.critDamageMultiplier + right.critDamageMultiplier,
            dodge = left.dodge + right.dodge,
            accuracy = left.accuracy + right.accuracy
        };
    }

    // * Nhân một bộ bonus với số lần đã lên cấp; không cần duyệt bảng từng level.
    public static StatBonus operator *(StatBonus bonus, int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        return new StatBonus
        {
            maxHealth = bonus.maxHealth * count,
            damage = bonus.damage * count,
            defense = bonus.defense * count,
            moveSpeed = bonus.moveSpeed * count,
            critChance = bonus.critChance * count,
            critDamageMultiplier = bonus.critDamageMultiplier * count,
            dodge = bonus.dodge * count,
            accuracy = bonus.accuracy * count
        };
    }

    // * Tạo bản chỉ số mới từ chỉ số gốc và bonus; giới hạn theo quy tắc chỉ số, không thay đổi bản gốc.
    // ! Chỉ xử lý cộng thẳng; buff phần trăm và máu hiện tại sẽ do hệ thống runtime phụ trách.
    public BaseStats ApplyTo(BaseStats source)
    {
        source.maxHealth = UnityEngine.Mathf.Max(1f, source.maxHealth + maxHealth);
        source.damage = UnityEngine.Mathf.Max(0f, source.damage + damage);
        source.defense = UnityEngine.Mathf.Max(0f, source.defense + defense);
        source.moveSpeed = UnityEngine.Mathf.Max(0f, source.moveSpeed + moveSpeed);
        source.range = UnityEngine.Mathf.Max(0f, source.range); // * Chưa có nguồn bonus range trong gameplay hiện tại.
        source.critChance = UnityEngine.Mathf.Max(0f, source.critChance + critChance);
        source.critDamageMultiplier = UnityEngine.Mathf.Max(1f, source.critDamageMultiplier + critDamageMultiplier);
        source.dodge = UnityEngine.Mathf.Max(0f, source.dodge + dodge);
        source.accuracy = UnityEngine.Mathf.Max(0f, source.accuracy + accuracy);
        return source;
    }

    #endregion
}
```

</details>

### CharacterRuntimeData.cs

Nguồn: [Assets/_Project/Scripts/Characters/Stats/CharacterRuntimeData.cs](../Assets/_Project/Scripts/Characters/Stats/CharacterRuntimeData.cs)

Phần thay đổi so với HEAD:

```diff
diff --git a/Assets/_Project/Scripts/Characters/Stats/CharacterRuntimeData.cs b/Assets/_Project/Scripts/Characters/Stats/CharacterRuntimeData.cs
index e1062fc..37f9a95 100644
--- a/Assets/_Project/Scripts/Characters/Stats/CharacterRuntimeData.cs
+++ b/Assets/_Project/Scripts/Characters/Stats/CharacterRuntimeData.cs
@@ -111,7 +111,31 @@ public sealed class CharacterRuntimeData
 
     #region Health Changes
 
-    // * Số âm trừ HP, số dương hồi HP; công thức sát thương sẽ thuộc combat service.
+    // * Character tính đầu ra trước khi bắn; crit theo bậc cộng dồn trong tài liệu chỉ số.
+    public DamageInfo CreateDamage(float multiplier)
+    {
+        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f)
+            throw new ArgumentOutOfRangeException(nameof(multiplier));
+        float chance = Mathf.Max(0f, FinalStats.critChance);
+        int tier = Mathf.FloorToInt(chance);
+        if (UnityEngine.Random.value < chance - tier)
+            tier++;
+        float critMultiplier = 1f + tier * (Mathf.Max(1f, FinalStats.critDamageMultiplier) - 1f);
+        return new DamageInfo(Mathf.Max(0f, FinalStats.damage) * multiplier * critMultiplier, Team, tier);
+    }
+
+    // * Luật nhận damage thuộc Character; nguồn đánh không biết defense hoặc HP của mục tiêu.
+    public DamageResult ReceiveDamage(DamageInfo damage)
+    {
+        if (!IsAlive || damage.SourceTeam == Team || damage.Amount <= 0f)
+            return default;
+        float before = CurrentHealth;
+        float received = damage.Amount * (100f / (100f + Mathf.Max(0f, FinalStats.defense)));
+        ApplyHealthChange(-received);
+        return new DamageResult(before - CurrentHealth, !IsAlive);
+    }
+
+    // * Số âm trừ HP, số dương hồi HP; hàm này chỉ thay đổi HP, không tự quyết định despawn.
     public void ApplyHealthChange(float amount)
     {
         if (float.IsNaN(amount) || float.IsInfinity(amount))
```

<details><summary>Code đầy đủ hiện tại</summary>

```csharp
using System;
using UnityEngine;

// * Dữ liệu riêng của một nhân vật đang chơi; chỉ sao chép giá trị, không sửa template SO.
public sealed class CharacterRuntimeData
{
    #region Character Identity

    public CharacterType CharacterType { get; }  // * Type được sao chép từ template khi tạo nhân vật.
    public CharacterTeam Team { get; } // * Phe riêng của cá thể, sao chép từ template; boss thuộc Enemy.

    public string DisplayName { get; }  // * Tên được sao chép từ template khi tạo nhân vật.

    #endregion

    #region Character Stats & Health

    public BaseStats BaseStats { get; }  // * Chỉ số gốc sao chép từ CharacterDataSO; giữ làm mốc để tính lại bonus.

    public BaseStats FinalStats { get; private set; }  // * Chỉ số thực tế sau khi cộng bonus; Player đọc tốc độ di chuyển từ đây.

    public float CurrentHealth { get; private set; }  // * HP hiện tại, tách riêng khỏi maxHealth trong bộ chỉ số.

    public bool IsAlive => CurrentHealth > 0f;  // * Nhân vật còn sống khi HP lớn hơn 0.

    #endregion

    #region Levels & Stat Bonuses

    public int PermanentLevel { get; private set; }  // * Level vĩnh viễn lấy từ hồ sơ đã load, giữ lại giữa các lượt chơi.

    public int RunLevel { get; private set; }  // * Level trong lượt hiện tại; StartNewRun đưa level này về 1.

    public StatBonus PermanentBonus { get; private set; }  // * Tổng chỉ số cộng thêm từ level vĩnh viễn: bonus mỗi cấp × (PermanentLevel - 1).

    public StatBonus RunBonus { get; private set; }  // * Tổng chỉ số cộng thêm từ level trong trận: bonus mỗi cấp × (RunLevel - 1).

    public StatBonus EquipmentBonus { get; private set; }  // * Tổng bonus của trang bị đang mặc; bằng 0 khi chưa mặc trang bị.
    public StatBonus SpawnBonus { get; } // * Bonus chốt tại lúc spawn; quái đang sống không nhận các mốc tăng sức mạnh tiếp theo.

    #endregion

    #region Runtime Notifications

    public event Action Changed;  // * Đọc FinalStats/CurrentHealth sau thông báo để UI lấy trạng thái mới nhất.

    #endregion

    #region Runtime Initialization

    // * Tạo dữ liệu riêng từ template và các nguồn bonus; nhân vật mới bắt đầu đầy HP.
    public CharacterRuntimeData(
        CharacterDataSO template,
        int permanentLevel,
        int runLevel,
        StatBonus permanentBonus,
        StatBonus runBonus,
        StatBonus equipmentBonus,
        StatBonus spawnBonus = default)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        CharacterType = template.CharacterType;
        Team = template.Team;
        DisplayName = template.DisplayName;
        BaseStats = template.BaseStats;
        SpawnBonus = spawnBonus;

        Recalculate(permanentLevel, runLevel, permanentBonus, runBonus, equipmentBonus);

        CurrentHealth = FinalStats.maxHealth;
    }

    #endregion

    #region Stat Recalculation

    // * Luôn tính lại từ gốc, không cộng bonus lên kết quả cũ hoặc tự hồi HP khi tăng MaxHealth.
    internal void Recalculate(
        int permanentLevel,
        int runLevel,
        StatBonus permanentBonus,
        StatBonus runBonus,
        StatBonus equipmentBonus)
    {
        if (permanentLevel < 1 || runLevel < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(permanentLevel), "Levels must be at least 1.");
        }

        PermanentLevel = permanentLevel;
        RunLevel = runLevel;

        PermanentBonus = permanentBonus;
        RunBonus = runBonus;
        EquipmentBonus = equipmentBonus;

        // * Cộng các nguồn bonus rồi áp dụng một lần lên bản chỉ số gốc.
        FinalStats = (permanentBonus + runBonus + equipmentBonus + SpawnBonus).ApplyTo(BaseStats);

        // * Giữ HP hiện tại; nếu MaxHealth giảm thì HP không được vượt giới hạn mới.
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0f, FinalStats.maxHealth);

        Changed?.Invoke();
    }

    #endregion

    #region Health Changes

    // * Character tính đầu ra trước khi bắn; crit theo bậc cộng dồn trong tài liệu chỉ số.
    public DamageInfo CreateDamage(float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier < 0f)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        float chance = Mathf.Max(0f, FinalStats.critChance);
        int tier = Mathf.FloorToInt(chance);
        if (UnityEngine.Random.value < chance - tier)
            tier++;
        float critMultiplier = 1f + tier * (Mathf.Max(1f, FinalStats.critDamageMultiplier) - 1f);
        return new DamageInfo(Mathf.Max(0f, FinalStats.damage) * multiplier * critMultiplier, Team, tier);
    }

    // * Luật nhận damage thuộc Character; nguồn đánh không biết defense hoặc HP của mục tiêu.
    public DamageResult ReceiveDamage(DamageInfo damage)
    {
        if (!IsAlive || damage.SourceTeam == Team || damage.Amount <= 0f)
            return default;
        float before = CurrentHealth;
        float received = damage.Amount * (100f / (100f + Mathf.Max(0f, FinalStats.defense)));
        ApplyHealthChange(-received);
        return new DamageResult(before - CurrentHealth, !IsAlive);
    }

    // * Số âm trừ HP, số dương hồi HP; hàm này chỉ thay đổi HP, không tự quyết định despawn.
    public void ApplyHealthChange(float amount)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount))
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        float health = Mathf.Clamp(CurrentHealth + amount, 0f, FinalStats.maxHealth);

        if (health == CurrentHealth)
        {
            return;
        }

        CurrentHealth = health;

        Changed?.Invoke();
    }

    #endregion
}
```

</details>

### CharacterView.cs

Nguồn: [Assets/_Project/Scripts/Characters/CharacterView.cs](../Assets/_Project/Scripts/Characters/CharacterView.cs)

Phần thay đổi so với HEAD:

```diff
diff --git a/Assets/_Project/Scripts/Characters/CharacterView.cs b/Assets/_Project/Scripts/Characters/CharacterView.cs
index 30804e9..93d9a88 100644
--- a/Assets/_Project/Scripts/Characters/CharacterView.cs
+++ b/Assets/_Project/Scripts/Characters/CharacterView.cs
@@ -4,8 +4,8 @@ using Zenject;
 
 // * Vỏ Unity dùng chung cho mọi nhân vật; không quyết định input hay AI.
 // * Một vỏ được pool tái sử dụng cho nhiều cá thể, mỗi lượt được gắn runtime/controller mới trước khi bật.
-[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
-public sealed class CharacterView : MonoBehaviour
+[RequireComponent(typeof(BoxCollider2D))]
+public sealed class CharacterView : MonoBehaviour, IDamageReceiver
 {
     private MovementManager movement; // * Dịch vụ áp dụng chuyển động, nhận lệnh từ hành vi hiện tại qua Move.
     private PoolManager pools; // * Pool của vỏ và visual; visual được thu hồi riêng khi tháo vỏ.
@@ -14,15 +14,26 @@ public sealed class CharacterView : MonoBehaviour
     private GameObject visual; // * Bản sao visual con, lấy từ prefab được khai báo trong CharacterDataSO.
     private SpriteRenderer[] sprites; // * Cache để lật hướng hiển thị, không tìm component trong từng frame.
     private Animator[] animators; // * Reset animation khi bật lại để không giữ animation của lượt sử dụng trước.
+    private BoxCollider2D hitbox; // * Một hitbox trên vỏ gốc; tìm kiếm dùng TryGetComponent tại cùng object.
+    private ProjectileService projectiles;
+    private CharacterSkills skills; // * Runtime skill riêng của lượt spawn, không sửa danh sách mẫu trên SO.
+    private Color[] originalSpriteColors; // * Màu visual trước hiệu ứng, cần phục hồi trước khi trả visual về pool.
+    private float damageFlashRemaining;
+
+    [Header("Damage Feedback")]
+    [SerializeField, Min(0.01f)] private float damageFlashDuration = 0.12f;
+    [SerializeField] private Color damageFlashColor = new Color(1f, 0.2f, 0.2f, 1f);
 
     public CharacterRuntimeData RuntimeData { get; private set; } // * Dữ liệu cá thể đang sử dụng vỏ; null khi đã thu hồi.
+    public Vector3 HitPosition => hitbox != null ? transform.TransformPoint(hitbox.offset) : transform.position;
     public event Action<CharacterView> Released; // * Báo chủ sở hữu khi despawn chủ động hoặc bị hủy; tắt tạm thời không kết thúc cá thể.
 
     [Inject]
-    public void Construct(MovementManager movement, PoolManager pools)
+    public void Construct(MovementManager movement, PoolManager pools, ProjectileService projectiles)
     {
         this.movement = movement;
         this.pools = pools;
+        this.projectiles = projectiles;
     }
 
     // * Factory gọi khi vỏ còn nằm dưới nhánh Preparing đang tắt; OnEnable chưa được phép chạy hành vi.
@@ -45,13 +56,8 @@ public sealed class CharacterView : MonoBehaviour
             throw new InvalidOperationException($"Add the {layerName} layer in Project Settings.");
         gameObject.layer = characterLayer;
 
-        Rigidbody2D body = GetComponent<Rigidbody2D>();
-        body.bodyType = RigidbodyType2D.Kinematic;
-        body.gravityScale = 0f;
-        body.useFullKinematicContacts = true;
-        body.velocity = Vector2.zero;
-        body.angularVelocity = 0f;
-        BoxCollider2D hitbox = GetComponent<BoxCollider2D>();
+        // * Di chuyển bằng MovementManager; collider chỉ phục vụ query, không cần Rigidbody2D.
+        hitbox = GetComponent<BoxCollider2D>();
         hitbox.enabled = true;
         hitbox.isTrigger = true;
         hitbox.size = template.ColliderSize;
@@ -70,6 +76,11 @@ public sealed class CharacterView : MonoBehaviour
         animators = visual.GetComponentsInChildren<Animator>(true);
         foreach (SpriteRenderer sprite in sprites)
             sprite.flipX = false;
+        originalSpriteColors = new Color[sprites.Length];
+        for (int i = 0; i < sprites.Length; i++)
+            originalSpriteColors[i] = sprites[i].color;
+        damageFlashRemaining = 0f;
+        skills = new CharacterSkills(template, new SkillContext(this, projectiles));
     }
 
     private void OnEnable()
@@ -77,6 +88,7 @@ public sealed class CharacterView : MonoBehaviour
         if (RuntimeData == null)
             return;
         movementHandle = movement.Register(transform);
+        CharacterSearch.InvalidatePhysics();
         foreach (Animator animator in animators)
         {
             animator.Rebind();
@@ -88,8 +100,12 @@ public sealed class CharacterView : MonoBehaviour
     {
         if (RuntimeData == null)
             return;
+        UpdateDamageFlash(Time.deltaTime);
         // * Unity gọi Update trên vỏ; phần quyết định hướng chạy thuộc class hành vi thuần.
         controller.Tick(this, RuntimeData);
+        // * Controller có thể despawn ngay trong Tick; luôn kiểm tra lại lượt đang hoạt động.
+        if (RuntimeData != null && RuntimeData.IsAlive && Time.deltaTime > 0f)
+            skills?.Tick(Time.deltaTime);
     }
 
     // * Mặc định nhìn theo hướng chạy; Enemy có thể truyền hướng đến Player khi đang lùi ra khỏi hàng xóm.
@@ -111,19 +127,93 @@ public sealed class CharacterView : MonoBehaviour
             sprite.flipX = flip;
     }
 
-    private void OnTriggerEnter2D(Collider2D other) => ForwardContact(other);
-    private void OnCollisionEnter2D(Collision2D other) => ForwardContact(other.collider);
+    #if UNITY_EDITOR
+    // * Bật Gizmos và chọn vỏ Enemy khi đang chơi để xem phạm vi quét tránh nhau.
+    // * Chỉ vẽ cho cá thể đang hoạt động; vỏ chưa khởi tạo hoặc đã trả về pool không có vòng debug.
+    // * Controller giữ bán kính và cách vẽ riêng của Enemy, vỏ chỉ chuyển tiếp callback từ Unity Editor.
+    private void OnDrawGizmosSelected()
+    {
+        if (isActiveAndEnabled && RuntimeData != null && RuntimeData.IsAlive &&
+            controller is EnemyController enemy)
+            enemy.DrawSeparationGizmo(transform.position);
+    }
+    #endif
+
+    // * View chỉ nối hitbox tới luật nhận damage của Character runtime.
+    // * Hành vi riêng quyết định chết; Enemy thu hồi, Player dừng lại.
+    public DamageResult ReceiveDamage(DamageInfo damage)
+    {
+        if (!isActiveAndEnabled || RuntimeData == null)
+            return default;
+        CharacterRuntimeData current = RuntimeData;
+        DamageResult result = current.ReceiveDamage(damage);
+        // * Chỉ nháy khi Player thực sự mất HP; hit bị từ chối không tạo phản hồi giả.
+        // * Làm trước xử lý chết để đòn cuối vẫn được hiển thị trên vỏ Player còn giữ lại.
+        if (result.Accepted && current.Team == CharacterTeam.Player && ReferenceEquals(RuntimeData, current))
+            StartDamageFlash();
+        if (result.Killed && ReferenceEquals(RuntimeData, current))
+            controller.OnDeath(this, current);
+        return result;
+    }
+
+    #region Contact Geometry & Damage Feedback
+
+    // * Enemy đã có tham chiếu Player nên chỉ so hai hitbox, không quét lại toàn bộ layer.
+    // * Không dùng callback trigger hoặc Rigidbody2D; bounds loại nhanh cặp ở xa trước phép đo chính xác.
+    public bool Overlaps(CharacterView other)
+    {
+        if (other == null || other == this || !isActiveAndEnabled || !other.isActiveAndEnabled ||
+            hitbox == null || other.hitbox == null || !hitbox.enabled || !other.hitbox.enabled)
+            return false;
+        CharacterSearch.SynchronizePhysics();
+        if (!hitbox.bounds.Intersects(other.hitbox.bounds))
+            return false;
+        ColliderDistance2D contact = hitbox.Distance(other.hitbox);
+        return contact.isValid && contact.distance <= 0f;
+    }
+
+    private void StartDamageFlash()
+    {
+        damageFlashRemaining = Mathf.Max(0.01f, damageFlashDuration);
+        for (int i = 0; i < sprites.Length; i++)
+        {
+            if (sprites[i] == null)
+                continue;
+            Color color = originalSpriteColors[i] * damageFlashColor;
+            color.a = originalSpriteColors[i].a;
+            sprites[i].color = color;
+        }
+    }
+
+    private void UpdateDamageFlash(float deltaTime)
+    {
+        if (damageFlashRemaining <= 0f || deltaTime <= 0f)
+            return;
+        damageFlashRemaining -= deltaTime;
+        if (damageFlashRemaining <= 0f)
+            RestoreSpriteColors();
+    }
 
-    private void ForwardContact(Component other)
+    private void RestoreSpriteColors()
     {
-        CharacterView target = other.GetComponentInParent<CharacterView>();
-        if (RuntimeData != null && RuntimeData.IsAlive && target != null && target != this)
-            controller.OnContact(this, RuntimeData, target);
+        damageFlashRemaining = 0f;
+        if (sprites == null || originalSpriteColors == null)
+            return;
+        for (int i = 0; i < sprites.Length; i++)
+            if (sprites[i] != null)
+                sprites[i].color = originalSpriteColors[i];
     }
 
+    #endregion
+
     // * Unity đang thay đổi trạng thái cả hierarchy trong OnDisable: chỉ dừng movement, không thu hồi/reparent visual.
     // * Giữ data/hành vi để việc tắt rồi bật tạm thời có thể tiếp tục cùng cá thể.
-    private void OnDisable() => StopMovement();
+    private void OnDisable()
+    {
+        StopMovement();
+        RestoreSpriteColors();
+        CharacterSearch.InvalidatePhysics();
+    }
 
     private void StopMovement()
     {
@@ -136,10 +226,13 @@ public sealed class CharacterView : MonoBehaviour
     private void OnDestroy()
     {
         StopMovement();
+        skills?.Dispose();
+        skills = null;
         controller = null;
         RuntimeData = null;
         visual = null;
         sprites = null;
+        originalSpriteColors = null;
         animators = null;
         NotifyReleased();
     }
@@ -149,9 +242,15 @@ public sealed class CharacterView : MonoBehaviour
     internal void ClearSpawn()
     {
         StopMovement();
+        // * Visual có pool riêng: luôn trả màu gốc trước khi visual được cá thể khác mượn.
+        RestoreSpriteColors();
+        skills?.Dispose();
+        skills = null;
+        CharacterSearch.InvalidatePhysics();
         controller = null;
         RuntimeData = null;
         sprites = null;
+        originalSpriteColors = null;
         animators = null;
         if (visual != null)
         {
```

<details><summary>Code đầy đủ hiện tại</summary>

```csharp
using System;
using UnityEngine;
using Zenject;

// * Vỏ Unity dùng chung cho mọi nhân vật; không quyết định input hay AI.
// * Một vỏ được pool tái sử dụng cho nhiều cá thể, mỗi lượt được gắn runtime/controller mới trước khi bật.
[RequireComponent(typeof(BoxCollider2D))]
public sealed class CharacterView : MonoBehaviour, IDamageReceiver
{
    private MovementManager movement; // * Dịch vụ áp dụng chuyển động, nhận lệnh từ hành vi hiện tại qua Move.
    private PoolManager pools; // * Pool của vỏ và visual; visual được thu hồi riêng khi tháo vỏ.
    private MovementHandle movementHandle; // * Đăng ký của lần hoạt động hiện tại; vô hiệu ngay khi tắt vỏ.
    private ICharacterController controller; // * Hành vi được gắn lúc spawn, không serialize vào prefab.
    private GameObject visual; // * Bản sao visual con, lấy từ prefab được khai báo trong CharacterDataSO.
    private SpriteRenderer[] sprites; // * Cache để lật hướng hiển thị, không tìm component trong từng frame.
    private Animator[] animators; // * Reset animation khi bật lại để không giữ animation của lượt sử dụng trước.
    private BoxCollider2D hitbox; // * Một hitbox trên vỏ gốc; tìm kiếm dùng TryGetComponent tại cùng object.
    private ProjectileService projectiles;
    private CharacterSkills skills; // * Runtime skill riêng của lượt spawn, không sửa danh sách mẫu trên SO.
    private Color[] originalSpriteColors; // * Màu visual trước hiệu ứng, cần phục hồi trước khi trả visual về pool.
    private float damageFlashRemaining;

    [Header("Damage Feedback")]
    [SerializeField, Min(0.01f)] private float damageFlashDuration = 0.12f;
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.2f, 0.2f, 1f);

    public CharacterRuntimeData RuntimeData { get; private set; } // * Dữ liệu cá thể đang sử dụng vỏ; null khi đã thu hồi.
    public Vector3 HitPosition => hitbox != null ? transform.TransformPoint(hitbox.offset) : transform.position;
    public event Action<CharacterView> Released; // * Báo chủ sở hữu khi despawn chủ động hoặc bị hủy; tắt tạm thời không kết thúc cá thể.

    [Inject]
    public void Construct(MovementManager movement, PoolManager pools, ProjectileService projectiles)
    {
        this.movement = movement;
        this.pools = pools;
        this.projectiles = projectiles;
    }

    // * Factory gọi khi vỏ còn nằm dưới nhánh Preparing đang tắt; OnEnable chưa được phép chạy hành vi.
    public void Initialize(CharacterDataSO template, CharacterRuntimeData runtime, ICharacterController behavior)
    {
        if (gameObject.activeInHierarchy || RuntimeData != null)
            throw new InvalidOperationException("Initialize a released, inactive character shell.");
        if (template == null || runtime == null || behavior == null || template.VisualPrefab == null)
            throw new ArgumentException("Character needs data, runtime, behavior and a visual prefab.");
        if (template.CharacterType != runtime.CharacterType || template.Team != runtime.Team)
            throw new ArgumentException("Character template and runtime must match.");

        // * Vỏ pool có thể vừa là Enemy rồi được dùng làm Player; luôn gán lại layer chứa hitbox.
        // * Chỉ đổi layer của vỏ; visual con không tham gia truy vấn hàng xóm hoặc va chạm gameplay.
        string layerName = template.Team == CharacterTeam.Enemy
            ? GameConstants.PhysicsLayers.Enemy
            : GameConstants.PhysicsLayers.Player;
        int characterLayer = LayerMask.NameToLayer(layerName);
        if (characterLayer < 0)
            throw new InvalidOperationException($"Add the {layerName} layer in Project Settings.");
        gameObject.layer = characterLayer;

        // * Di chuyển bằng MovementManager; collider chỉ phục vụ query, không cần Rigidbody2D.
        hitbox = GetComponent<BoxCollider2D>();
        hitbox.enabled = true;
        hitbox.isTrigger = true;
        hitbox.size = template.ColliderSize;
        hitbox.offset = template.ColliderOffset;

        RuntimeData = runtime;
        controller = behavior;
        // * Parent vẫn đang tắt nên visual cũng được chuẩn bị trước khi xuất hiện trong scene.
        visual = pools.Spawn(template.VisualPrefab, transform.position, transform.rotation, PoolGroup.Other, transform);
        if (visual == null)
            throw new InvalidOperationException("Could not spawn the character visual.");
        visual.transform.localPosition = template.VisualPrefab.transform.localPosition;
        visual.transform.localRotation = template.VisualPrefab.transform.localRotation;
        visual.transform.localScale = template.VisualPrefab.transform.localScale;
        sprites = visual.GetComponentsInChildren<SpriteRenderer>(true);
        animators = visual.GetComponentsInChildren<Animator>(true);
        foreach (SpriteRenderer sprite in sprites)
            sprite.flipX = false;
        originalSpriteColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
            originalSpriteColors[i] = sprites[i].color;
        damageFlashRemaining = 0f;
        skills = new CharacterSkills(template, new SkillContext(this, projectiles));
    }

    private void OnEnable()
    {
        if (RuntimeData == null)
            return;
        movementHandle = movement.Register(transform);
        CharacterSearch.InvalidatePhysics();
        foreach (Animator animator in animators)
        {
            animator.Rebind();
            animator.Update(0f);
        }
    }

    private void Update()
    {
        if (RuntimeData == null)
            return;
        UpdateDamageFlash(Time.deltaTime);
        // * Unity gọi Update trên vỏ; phần quyết định hướng chạy thuộc class hành vi thuần.
        controller.Tick(this, RuntimeData);
        // * Controller có thể despawn ngay trong Tick; luôn kiểm tra lại lượt đang hoạt động.
        if (RuntimeData != null && RuntimeData.IsAlive && Time.deltaTime > 0f)
            skills?.Tick(Time.deltaTime);
    }

    // * Mặc định nhìn theo hướng chạy; Enemy có thể truyền hướng đến Player khi đang lùi ra khỏi hàng xóm.
    // * facingDirection chỉ là lệnh hiển thị của frame hiện tại, không thêm dữ liệu cấu hình vào template chung.
    public void Move(Vector2 direction, float speed, float maxDistance = float.PositiveInfinity,
        float? facingDirection = null)
    {
        FaceDirection(facingDirection ?? direction.x);
        movement?.SetMovement(movementHandle, direction, speed, maxDistance);
    }

    private void FaceDirection(float horizontalDirection)
    {
        if (Mathf.Abs(horizontalDirection) <= 0.01f || sprites == null)
            return;

        bool flip = horizontalDirection < 0f;
        foreach (SpriteRenderer sprite in sprites)
            sprite.flipX = flip;
    }

    #if UNITY_EDITOR
    // * Bật Gizmos và chọn vỏ Enemy khi đang chơi để xem phạm vi quét tránh nhau.
    // * Chỉ vẽ cho cá thể đang hoạt động; vỏ chưa khởi tạo hoặc đã trả về pool không có vòng debug.
    // * Controller giữ bán kính và cách vẽ riêng của Enemy, vỏ chỉ chuyển tiếp callback từ Unity Editor.
    private void OnDrawGizmosSelected()
    {
        if (isActiveAndEnabled && RuntimeData != null && RuntimeData.IsAlive &&
            controller is EnemyController enemy)
            enemy.DrawSeparationGizmo(transform.position);
    }
    #endif

    // * View chỉ nối hitbox tới luật nhận damage của Character runtime.
    // * Hành vi riêng quyết định chết; Enemy thu hồi, Player dừng lại.
    public DamageResult ReceiveDamage(DamageInfo damage)
    {
        if (!isActiveAndEnabled || RuntimeData == null)
            return default;
        CharacterRuntimeData current = RuntimeData;
        DamageResult result = current.ReceiveDamage(damage);
        // * Chỉ nháy khi Player thực sự mất HP; hit bị từ chối không tạo phản hồi giả.
        // * Làm trước xử lý chết để đòn cuối vẫn được hiển thị trên vỏ Player còn giữ lại.
        if (result.Accepted && current.Team == CharacterTeam.Player && ReferenceEquals(RuntimeData, current))
            StartDamageFlash();
        if (result.Killed && ReferenceEquals(RuntimeData, current))
            controller.OnDeath(this, current);
        return result;
    }

    #region Contact Geometry & Damage Feedback

    // * Enemy đã có tham chiếu Player nên chỉ so hai hitbox, không quét lại toàn bộ layer.
    // * Không dùng callback trigger hoặc Rigidbody2D; bounds loại nhanh cặp ở xa trước phép đo chính xác.
    public bool Overlaps(CharacterView other)
    {
        if (other == null || other == this || !isActiveAndEnabled || !other.isActiveAndEnabled ||
            hitbox == null || other.hitbox == null || !hitbox.enabled || !other.hitbox.enabled)
            return false;
        CharacterSearch.SynchronizePhysics();
        if (!hitbox.bounds.Intersects(other.hitbox.bounds))
            return false;
        ColliderDistance2D contact = hitbox.Distance(other.hitbox);
        return contact.isValid && contact.distance <= 0f;
    }

    private void StartDamageFlash()
    {
        damageFlashRemaining = Mathf.Max(0.01f, damageFlashDuration);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
                continue;
            Color color = originalSpriteColors[i] * damageFlashColor;
            color.a = originalSpriteColors[i].a;
            sprites[i].color = color;
        }
    }

    private void UpdateDamageFlash(float deltaTime)
    {
        if (damageFlashRemaining <= 0f || deltaTime <= 0f)
            return;
        damageFlashRemaining -= deltaTime;
        if (damageFlashRemaining <= 0f)
            RestoreSpriteColors();
    }

    private void RestoreSpriteColors()
    {
        damageFlashRemaining = 0f;
        if (sprites == null || originalSpriteColors == null)
            return;
        for (int i = 0; i < sprites.Length; i++)
            if (sprites[i] != null)
                sprites[i].color = originalSpriteColors[i];
    }

    #endregion

    // * Unity đang thay đổi trạng thái cả hierarchy trong OnDisable: chỉ dừng movement, không thu hồi/reparent visual.
    // * Giữ data/hành vi để việc tắt rồi bật tạm thời có thể tiếp tục cùng cá thể.
    private void OnDisable()
    {
        StopMovement();
        RestoreSpriteColors();
        CharacterSearch.InvalidatePhysics();
    }

    private void StopMovement()
    {
        movement?.Unregister(movementHandle);
        movementHandle = default;
    }

    // * Khi bị Destroy trực tiếp, Unity hủy cả visual con; chỉ gỡ tham chiếu và báo chủ sở hữu.
    // * Không trả visual về pool từ callback này vì hierarchy có thể đang trong quá trình hủy/tắt.
    private void OnDestroy()
    {
        StopMovement();
        skills?.Dispose();
        skills = null;
        controller = null;
        RuntimeData = null;
        visual = null;
        sprites = null;
        originalSpriteColors = null;
        animators = null;
        NotifyReleased();
    }

    // * Chỉ factory gọi chủ động TRƯỚC khi tắt vỏ, hoặc khi khởi tạo lỗi mà vỏ chưa bật.
    // * Visual được thu hồi xong rồi chủ sở hữu mới được thông báo, sau đó factory trả vỏ về pool.
    internal void ClearSpawn()
    {
        StopMovement();
        // * Visual có pool riêng: luôn trả màu gốc trước khi visual được cá thể khác mượn.
        RestoreSpriteColors();
        skills?.Dispose();
        skills = null;
        CharacterSearch.InvalidatePhysics();
        controller = null;
        RuntimeData = null;
        sprites = null;
        originalSpriteColors = null;
        animators = null;
        if (visual != null)
        {
            GameObject previousVisual = visual;
            visual = null;
            pools.Despawn(previousVisual);
        }
        NotifyReleased();
    }

    private void NotifyReleased()
    {
        Action<CharacterView> released = Released;
        Released = null;
        released?.Invoke(this);
    }
}
```

</details>

### ICharacterController.cs

Nguồn: [Assets/_Project/Scripts/Characters/ICharacterController.cs](../Assets/_Project/Scripts/Characters/ICharacterController.cs)

Phần thay đổi so với HEAD:

```diff
diff --git a/Assets/_Project/Scripts/Characters/ICharacterController.cs b/Assets/_Project/Scripts/Characters/ICharacterController.cs
index 63714a6..803172f 100644
--- a/Assets/_Project/Scripts/Characters/ICharacterController.cs
+++ b/Assets/_Project/Scripts/Characters/ICharacterController.cs
@@ -1,7 +1,7 @@
 // * Hợp đồng hành vi cho class C# thuần; không kế thừa MonoBehaviour và không giữ dữ liệu mẫu.
-// * View chuyển tiếp Tick/va chạm; mỗi controller quyết định cách sử dụng runtime của cá thể hiện tại.
+// * View chuyển tiếp Tick/chết; mỗi controller quyết định cách sử dụng runtime của cá thể hiện tại.
 public interface ICharacterController
 {
     void Tick(CharacterView view, CharacterRuntimeData runtime);
-    void OnContact(CharacterView view, CharacterRuntimeData runtime, CharacterView other);
+    void OnDeath(CharacterView view, CharacterRuntimeData runtime);
 }
```

<details><summary>Code đầy đủ hiện tại</summary>

```csharp
// * Hợp đồng hành vi cho class C# thuần; không kế thừa MonoBehaviour và không giữ dữ liệu mẫu.
// * View chuyển tiếp Tick/chết; mỗi controller quyết định cách sử dụng runtime của cá thể hiện tại.
public interface ICharacterController
{
    void Tick(CharacterView view, CharacterRuntimeData runtime);
    void OnDeath(CharacterView view, CharacterRuntimeData runtime);
}
```

</details>

### PlayerController.cs

Nguồn: [Assets/_Project/Scripts/Characters/Player/PlayerController.cs](../Assets/_Project/Scripts/Characters/Player/PlayerController.cs)

Phần thay đổi so với HEAD:

```diff
diff --git a/Assets/_Project/Scripts/Characters/Player/PlayerController.cs b/Assets/_Project/Scripts/Characters/Player/PlayerController.cs
index 5952a58..21fd0cd 100644
--- a/Assets/_Project/Scripts/Characters/Player/PlayerController.cs
+++ b/Assets/_Project/Scripts/Characters/Player/PlayerController.cs
@@ -30,8 +30,9 @@ public sealed class PlayerController : ICharacterController
         view.Move(input, runtime.FinalStats.moveSpeed * input.magnitude);
     }
 
-    public void OnContact(CharacterView view, CharacterRuntimeData runtime, CharacterView other)
+    public void OnDeath(CharacterView view, CharacterRuntimeData runtime)
     {
-        // * Chưa có luật va chạm riêng cho Player; combat sẽ xử lý sát thương khi được bổ sung.
+        // * Giữ vỏ cho màn hình game over sau này; skill ngừng tick khi chủ sở hữu hết HP.
+        view.Move(Vector2.zero, 0f);
     }
 }
```

<details><summary>Code đầy đủ hiện tại</summary>

```csharp
using Terresquall;
using UnityEngine;

// * Hành vi riêng của người chơi: hướng di chuyển hoàn toàn do joystick/bàn phím quyết định.
// * Không giữ HP, không tự tạo visual và không chứa AI của Enemy.
public sealed class PlayerController : ICharacterController
{
    private readonly VirtualJoystick joystick; // * Input của scene; không lưu tham chiếu scene trên prefab hoặc SO.

    public PlayerController(VirtualJoystick joystick)
    {
        this.joystick = joystick;
    }

    public void Tick(CharacterView view, CharacterRuntimeData runtime)
    {
        // * Player hết HP thì ngừng di chuyển; luật game over sẽ được bổ sung trong hành vi Player.
        if (!runtime.IsAlive)
        {
            view.Move(Vector2.zero, 0f);
            return;
        }
        Vector2 input = joystick != null ? joystick.GetAxis() : Vector2.zero;
#if UNITY_EDITOR || UNITY_STANDALONE
        // * Hỗ trợ kiểm tra bằng bàn phím; giới hạn độ dài sau khi cộng để đi chéo không nhanh hơn.
        input += new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        input = Vector2.ClampMagnitude(input, 1f);
        // * MovementManager chuẩn hóa hướng nên nhân tốc độ với độ nghiêng để giữ điều khiển analog.
        view.Move(input, runtime.FinalStats.moveSpeed * input.magnitude);
    }

    public void OnDeath(CharacterView view, CharacterRuntimeData runtime)
    {
        // * Giữ vỏ cho màn hình game over sau này; skill ngừng tick khi chủ sở hữu hết HP.
        view.Move(Vector2.zero, 0f);
    }
}
```

</details>

### EnemyController.cs

Nguồn: [Assets/_Project/Scripts/Characters/Enemies/EnemyController.cs](../Assets/_Project/Scripts/Characters/Enemies/EnemyController.cs)

Phần thay đổi so với HEAD:

```diff
diff --git a/Assets/_Project/Scripts/Characters/Enemies/EnemyController.cs b/Assets/_Project/Scripts/Characters/Enemies/EnemyController.cs
index a4410c7..193031b 100644
--- a/Assets/_Project/Scripts/Characters/Enemies/EnemyController.cs
+++ b/Assets/_Project/Scripts/Characters/Enemies/EnemyController.cs
@@ -3,22 +3,27 @@ using UnityEngine;
 // * Hành vi riêng của quái: tự tìm hướng đến Player hiện tại, hoàn toàn không đọc joystick.
 public sealed class EnemyController : ICharacterController
 {
-    #region Separation Configuration & Shared Query Buffer
+    #region Separation Configuration
 
     // * Cấu hình chung hiển thị trên EnemySpawnManager; mỗi controller chỉ giữ bản sao giá trị cần dùng.
     [System.Serializable]
     public sealed class SeparationSettings
     {
-        [Min(0.01f)] public float radius = 0.3f; // * Khoảng cách mong muốn giữa hai gốc nhân vật, tính bằng world unit.
+        [Min(0.01f)] public float radius = 0.2f; // * Khoảng cách mong muốn giữa hai tâm tránh nhau, tính bằng world unit.
+        public float offsetY = -0.05f; // * Cộng vào world Y của vỏ; âm dịch xuống, dương dịch lên, không dời visual/collider.
         [Min(0f)] public float strength = 2f; // * Trọng số hướng ra xa hàng xóm so với hướng đuổi Player có độ dài 1.
         [Min(0.01f)] public float scanInterval = 0.1f; // * Số giây game giữa hai lần quét; không quét lại trong mọi Update.
     }
 
-    // * Mọi Enemy dùng chung mảng kết quả; xử lý xong lượt hiện tại trước khi Enemy tiếp theo ghi đè.
-    // ! Chỉ gọi truy vấn trên main thread, không chuyển buffer này vào Job hoặc giữ kết quả qua frame.
-    // ! Mảng cố định nhận tối đa 32 collider gồm cả chính nó; vùng quá đông có thể bỏ sót hàng xóm.
-    private static readonly Collider2D[] neighbors = new Collider2D[32];
-    private static int lastSyncFrame = -1; // * Gộp SyncTransforms cho các lượt quét trong cùng frame, không gọi lại cho từng quái.
+    // * Luật chạm thuộc hành vi Enemy; không đưa cooldown/knockback vào data chung của Character.
+    [System.Serializable]
+    public sealed class ContactSettings
+    {
+        [Min(0.01f)] public float cooldown = 0.8f; // * Khoảng chờ riêng mỗi Enemy, không phải miễn thương toàn Player.
+        [Min(0f)] public float damageMultiplier = 1f; // * Nhân với damage và crit do Character nguồn tính.
+        [Min(0f)] public float knockbackDistance = 0.35f; // * Chính Enemy lùi ra xa Player sau khi gây damage thành công.
+        [Min(0.01f)] public float knockbackDuration = 0.18f; // * Dịch chuyển bằng MovementManager, không dùng lực Rigidbody.
+    }
 
     #endregion
 
@@ -26,28 +31,36 @@ public sealed class EnemyController : ICharacterController
 
     private readonly PlayerManager players; // * Tra mục tiêu hiện tại để không giữ Transform Player cũ sau khi đổi nhân vật.
     private readonly CharacterFactory characters; // * Thu hồi visual trước vỏ qua một đầu mối, không gọi pool trực tiếp cho nhân vật.
-    private readonly ContactFilter2D neighborFilter; // * Chỉ nhận layer Enemy và chủ động cho phép collider trigger của vỏ.
     private readonly float separationRadius; // * Giá trị cấu hình chốt khi tạo controller, không sửa template hoặc dùng chung trạng thái cá thể.
+    private readonly float separationOffsetY; // * Offset tâm quét, không bị scale hoặc flip của visual ảnh hưởng.
     private readonly float separationStrength; // * Trọng số tách đã chốt cho cá thể; bằng 0 sẽ bỏ qua truy vấn hàng xóm.
     private readonly float scanInterval; // * Chu kỳ quét của cá thể, tối thiểu 0.01 giây dù cấu hình bị sửa ngoài Inspector.
     private Vector2 separationDirection; // * Hướng ra xa đã tính ở lượt quét trước; dùng lại giữa các lần quét.
     private float nextScanTime; // * Mốc Time.time cho lượt quét tiếp; pause không làm đồng hồ này chạy tiếp.
     private bool scanScheduled; // * Lần Tick đầu mới biết vỏ hiện tại để chia lịch; controller mới không giữ cache của lượt spawn cũ.
-
-    public EnemyController(PlayerManager players, CharacterFactory characters, SeparationSettings separation = null)
+    private readonly float contactCooldown;
+    private readonly float contactDamageMultiplier;
+    private readonly float knockbackDistance;
+    private readonly float knockbackDuration;
+    private float nextContactTime; // * Cooldown riêng của lượt spawn hiện tại.
+    private float knockbackRemaining; // * Thời gian game còn lại; pause không làm phản lực chạy tiếp.
+    private Vector2 knockbackDirection; // * Hướng lùi chốt lúc trúng, không đổi theo Player trong lúc lùi.
+
+    public EnemyController(PlayerManager players, CharacterFactory characters,
+        SeparationSettings separation = null, ContactSettings contact = null)
     {
         this.players = players;
         this.characters = characters;
         separation = separation ?? new SeparationSettings();
         separationRadius = Mathf.Max(0.01f, separation.radius);
+        separationOffsetY = separation.offsetY;
         separationStrength = Mathf.Max(0f, separation.strength);
         scanInterval = Mathf.Max(0.01f, separation.scanInterval);
-        neighborFilter = new ContactFilter2D
-        {
-            useLayerMask = true,
-            layerMask = LayerMask.GetMask(GameConstants.PhysicsLayers.Enemy),
-            useTriggers = true
-        };
+        contact = contact ?? new ContactSettings();
+        contactCooldown = Mathf.Max(0.01f, contact.cooldown);
+        contactDamageMultiplier = Mathf.Max(0f, contact.damageMultiplier);
+        knockbackDistance = Mathf.Max(0f, contact.knockbackDistance);
+        knockbackDuration = Mathf.Max(0.01f, contact.knockbackDuration);
     }
 
     #endregion
@@ -62,6 +75,12 @@ public sealed class EnemyController : ICharacterController
             characters.Despawn(view);
             return;
         }
+        if (Time.deltaTime <= 0f)
+            return;
+        // * Phản lực đang chạy có ưu tiên hơn đuổi/tách; hai lệnh không triệt tiêu nhau.
+        // * Vẫn hoàn tất lùi ngay cả khi đòn vừa rồi làm Player chết.
+        if (ApplyKnockback(view))
+            return;
         CharacterView target = players.CurrentPlayer;
         if (target == null || target.RuntimeData == null || !target.RuntimeData.IsAlive)
         {
@@ -70,6 +89,9 @@ public sealed class EnemyController : ICharacterController
             return;
         }
 
+        if (TryContactAttack(view, runtime, target) && ApplyKnockback(view))
+            return;
+
         UpdateSeparation(view);
         Vector2 toPlayer = target.transform.position - view.transform.position;
         // * Hướng đuổi luôn được đọc mới mỗi frame; hướng tách chỉ cập nhật khi đến lịch quét.
@@ -102,37 +124,26 @@ public sealed class EnemyController : ICharacterController
         float elapsedIntervals = Mathf.Floor((Time.time - nextScanTime) / scanInterval) + 1f;
         nextScanTime += elapsedIntervals * scanInterval;
 
-        // * MovementManager ghi Transform trong LateTick, trong khi Auto Sync Transforms đang tắt.
-        // * Đồng bộ trước lượt quét đầu tiên của frame để Physics2D nhận vị trí đã di chuyển.
-        if (lastSyncFrame != Time.frameCount)
-        {
-            Physics2D.SyncTransforms();
-            lastSyncFrame = Time.frameCount;
-        }
+        // * CharacterSearch quản lý đồng bộ physics và buffer mượn dùng chung cho mọi kiểu tìm kiếm.
         separationDirection = CalculateSeparation(view);
     }
 
-    // * OverlapCircle ghi vào mảng có sẵn, không cấp phát danh sách/mảng mới cho từng lượt quét.
-    // * Vùng query chạm hình collider; kiểm tra lại khoảng cách giữa hai gốc để lực tách về 0 đúng bán kính.
+    // * Mượn buffer trong phạm vi using, tính xong hướng tách thì trả ngay; không giữ danh sách riêng.
+    // * Vùng query chạm hình collider; kiểm tra lại khoảng cách giữa hai tâm để lực tách về 0 đúng bán kính.
     private Vector2 CalculateSeparation(CharacterView view)
     {
-        Vector2 position = view.transform.position;
-        int count = Physics2D.OverlapCircle(position, separationRadius, neighborFilter, neighbors);
+        Vector2 position = GetSeparationCenter(view.transform.position);
+        using var neighbors = CharacterSearch.FindAll(position, separationRadius, CharacterTeam.Enemy, view);
         float radiusSquared = separationRadius * separationRadius;
         Vector2 direction = Vector2.zero;
-        for (int i = 0; i < count; i++)
+        for (int i = 0; i < neighbors.Count; i++)
         {
-            Collider2D neighborCollider = neighbors[i];
-            neighbors[i] = null; // * Không để mảng static giữ tham chiếu scene/collider sau khi đã xử lý.
-            if (neighborCollider == null || neighborCollider.gameObject == view.gameObject ||
-                !neighborCollider.TryGetComponent(out CharacterView neighbor))
-                continue;
-            CharacterRuntimeData neighborRuntime = neighbor.RuntimeData;
-            if (neighborRuntime == null || !neighborRuntime.IsAlive || neighborRuntime.Team != CharacterTeam.Enemy)
+            if (!neighbors[i].TryGetCharacter(out CharacterView neighbor))
                 continue;
 
-            // * Vị trí mình trừ vị trí hàng xóm tạo vector đi RA XA con đó, không chọn trái/phải ngẫu nhiên.
-            Vector2 away = position - (Vector2)neighbor.transform.position;
+            // * Dùng cùng quy ước tâm cho cả hai bên để offset không tạo hướng tách lệch một phía.
+            // * Tâm mình trừ tâm hàng xóm tạo vector đi RA XA con đó, không chọn trái/phải ngẫu nhiên.
+            Vector2 away = position - (Vector2)GetSeparationCenter(neighbor.transform.position);
             float distanceSquared = away.sqrMagnitude;
             if (distanceSquared >= radiusSquared)
                 continue;
@@ -150,7 +161,15 @@ public sealed class EnemyController : ICharacterController
         return Vector2.ClampMagnitude(direction, 1f);
     }
 
-    // * Hai gốc trùng nhau không có hướng để chuẩn hóa; chọn hướng ổn định từ ID của cặp.
+    // * Một cách tính tâm dùng chung cho truy vấn, khoảng cách và vòng debug để hình vẽ khớp logic.
+    // * Offset giống nhau triệt tiêu khi trừ hai tâm; nó dời vùng query, không tự đổi khoảng cách giữa quái.
+    private Vector3 GetSeparationCenter(Vector3 position)
+    {
+        position.y += separationOffsetY;
+        return position;
+    }
+
+    // * Hai tâm trùng nhau không có hướng để chuẩn hóa; chọn hướng ổn định từ ID của cặp.
     // * Cùng một cặp nhận hai vector đối nhau nên không cùng chạy sang một phía rồi tiếp tục dính nhau.
     private static Vector2 GetOverlapDirection(int ownId, int neighborId)
     {
@@ -163,13 +182,64 @@ public sealed class EnemyController : ICharacterController
 
     #endregion
 
-    #region Contact Behavior
+    #region Contact Attack & Recoil
 
-    public void OnContact(CharacterView view, CharacterRuntimeData runtime, CharacterView other)
+    private bool TryContactAttack(CharacterView view, CharacterRuntimeData runtime, CharacterView target)
     {
-        if (other.RuntimeData != null && other.RuntimeData.Team == CharacterTeam.Player)
-            characters.Despawn(view);
+        if (Time.time < nextContactTime || !view.Overlaps(target))
+            return false;
+
+        // * Chốt hướng trước callback nhận damage; không phụ thuộc mục tiêu sau khi xử lý chết.
+        Vector2 away = view.HitPosition - target.HitPosition;
+        if (away.sqrMagnitude <= 0.000001f)
+            away = GetOverlapDirection(view.GetInstanceID(), target.GetInstanceID());
+        nextContactTime = Time.time + contactCooldown;
+        DamageResult result = target.ReceiveDamage(runtime.CreateDamage(contactDamageMultiplier));
+        if (!result.Accepted || knockbackDistance <= 0f)
+            return false;
+
+        knockbackDirection = away.normalized;
+        knockbackRemaining = knockbackDuration;
+        separationDirection = Vector2.zero;
+        return true;
+    }
+
+    private bool ApplyKnockback(CharacterView view)
+    {
+        if (knockbackRemaining <= 0f)
+            return false;
+        float stepTime = Mathf.Min(Time.deltaTime, knockbackRemaining);
+        knockbackRemaining -= stepTime;
+        // * MovementManager tích phân theo deltaTime cả frame; tỷ lệ giữ bước cuối đúng quãng lùi cấu hình.
+        float speed = (knockbackDistance / knockbackDuration) * (stepTime / Time.deltaTime);
+        CharacterView player = players.CurrentPlayer;
+        float facing = player != null ? player.transform.position.x - view.transform.position.x : -knockbackDirection.x;
+        view.Move(knockbackDirection, speed, facingDirection: facing);
+        return true;
+    }
+
+    #endregion
+
+    #if UNITY_EDITOR
+    #region Separation Debug
+
+    // * CharacterView gọi khi chọn Enemy trong Scene view; dùng đúng bán kính đã chốt cho lượt spawn này.
+    // * Vẽ trên mặt phẳng XY theo world unit như OverlapCircle, không nhân theo scale của vỏ hoặc visual.
+    // ! Chỉ hiển thị phạm vi quét, không thực hiện truy vấn Physics2D hoặc thay đổi hành vi di chuyển.
+    public void DrawSeparationGizmo(Vector3 position)
+    {
+        // * DrawingScope tự khôi phục màu và ma trận để không ảnh hưởng gizmo của đối tượng khác.
+        using (new UnityEditor.Handles.DrawingScope(Color.yellow, Matrix4x4.identity))
+            UnityEditor.Handles.DrawWireDisc(GetSeparationCenter(position), Vector3.forward, separationRadius);
     }
 
+    #endregion
+    #endif
+
+    #region Death Behavior
+
+    // * Chết mới thu hồi; bỏ hoàn toàn hành vi test chạm Player thì biến mất.
+    public void OnDeath(CharacterView view, CharacterRuntimeData runtime) => characters.Despawn(view);
+
     #endregion
 }
```

<details><summary>Code đầy đủ hiện tại</summary>

```csharp
using UnityEngine;

// * Hành vi riêng của quái: tự tìm hướng đến Player hiện tại, hoàn toàn không đọc joystick.
public sealed class EnemyController : ICharacterController
{
    #region Separation Configuration

    // * Cấu hình chung hiển thị trên EnemySpawnManager; mỗi controller chỉ giữ bản sao giá trị cần dùng.
    [System.Serializable]
    public sealed class SeparationSettings
    {
        [Min(0.01f)] public float radius = 0.2f; // * Khoảng cách mong muốn giữa hai tâm tránh nhau, tính bằng world unit.
        public float offsetY = -0.05f; // * Cộng vào world Y của vỏ; âm dịch xuống, dương dịch lên, không dời visual/collider.
        [Min(0f)] public float strength = 2f; // * Trọng số hướng ra xa hàng xóm so với hướng đuổi Player có độ dài 1.
        [Min(0.01f)] public float scanInterval = 0.1f; // * Số giây game giữa hai lần quét; không quét lại trong mọi Update.
    }

    // * Luật chạm thuộc hành vi Enemy; không đưa cooldown/knockback vào data chung của Character.
    [System.Serializable]
    public sealed class ContactSettings
    {
        [Min(0.01f)] public float cooldown = 0.8f; // * Khoảng chờ riêng mỗi Enemy, không phải miễn thương toàn Player.
        [Min(0f)] public float damageMultiplier = 1f; // * Nhân với damage và crit do Character nguồn tính.
        [Min(0f)] public float knockbackDistance = 0.35f; // * Chính Enemy lùi ra xa Player sau khi gây damage thành công.
        [Min(0.01f)] public float knockbackDuration = 0.18f; // * Dịch chuyển bằng MovementManager, không dùng lực Rigidbody.
    }

    #endregion

    #region Dependencies & Per-Spawn State

    private readonly PlayerManager players; // * Tra mục tiêu hiện tại để không giữ Transform Player cũ sau khi đổi nhân vật.
    private readonly CharacterFactory characters; // * Thu hồi visual trước vỏ qua một đầu mối, không gọi pool trực tiếp cho nhân vật.
    private readonly float separationRadius; // * Giá trị cấu hình chốt khi tạo controller, không sửa template hoặc dùng chung trạng thái cá thể.
    private readonly float separationOffsetY; // * Offset tâm quét, không bị scale hoặc flip của visual ảnh hưởng.
    private readonly float separationStrength; // * Trọng số tách đã chốt cho cá thể; bằng 0 sẽ bỏ qua truy vấn hàng xóm.
    private readonly float scanInterval; // * Chu kỳ quét của cá thể, tối thiểu 0.01 giây dù cấu hình bị sửa ngoài Inspector.
    private Vector2 separationDirection; // * Hướng ra xa đã tính ở lượt quét trước; dùng lại giữa các lần quét.
    private float nextScanTime; // * Mốc Time.time cho lượt quét tiếp; pause không làm đồng hồ này chạy tiếp.
    private bool scanScheduled; // * Lần Tick đầu mới biết vỏ hiện tại để chia lịch; controller mới không giữ cache của lượt spawn cũ.
    private readonly float contactCooldown;
    private readonly float contactDamageMultiplier;
    private readonly float knockbackDistance;
    private readonly float knockbackDuration;
    private float nextContactTime; // * Cooldown riêng của lượt spawn hiện tại.
    private float knockbackRemaining; // * Thời gian game còn lại; pause không làm phản lực chạy tiếp.
    private Vector2 knockbackDirection; // * Hướng lùi chốt lúc trúng, không đổi theo Player trong lúc lùi.

    public EnemyController(PlayerManager players, CharacterFactory characters,
        SeparationSettings separation = null, ContactSettings contact = null)
    {
        this.players = players;
        this.characters = characters;
        separation = separation ?? new SeparationSettings();
        separationRadius = Mathf.Max(0.01f, separation.radius);
        separationOffsetY = separation.offsetY;
        separationStrength = Mathf.Max(0f, separation.strength);
        scanInterval = Mathf.Max(0.01f, separation.scanInterval);
        contact = contact ?? new ContactSettings();
        contactCooldown = Mathf.Max(0.01f, contact.cooldown);
        contactDamageMultiplier = Mathf.Max(0f, contact.damageMultiplier);
        knockbackDistance = Mathf.Max(0f, contact.knockbackDistance);
        knockbackDuration = Mathf.Max(0.01f, contact.knockbackDuration);
    }

    #endregion

    #region Chase & Separation Movement

    public void Tick(CharacterView view, CharacterRuntimeData runtime)
    {
        // * Chết/thu hồi là quyết định của Enemy; vỏ không chứa luật sống chết theo phe.
        if (!runtime.IsAlive)
        {
            characters.Despawn(view);
            return;
        }
        if (Time.deltaTime <= 0f)
            return;
        // * Phản lực đang chạy có ưu tiên hơn đuổi/tách; hai lệnh không triệt tiêu nhau.
        // * Vẫn hoàn tất lùi ngay cả khi đòn vừa rồi làm Player chết.
        if (ApplyKnockback(view))
            return;
        CharacterView target = players.CurrentPlayer;
        if (target == null || target.RuntimeData == null || !target.RuntimeData.IsAlive)
        {
            separationDirection = Vector2.zero;
            view.Move(Vector2.zero, 0f);
            return;
        }

        if (TryContactAttack(view, runtime, target) && ApplyKnockback(view))
            return;

        UpdateSeparation(view);
        Vector2 toPlayer = target.transform.position - view.transform.position;
        // * Hướng đuổi luôn được đọc mới mỗi frame; hướng tách chỉ cập nhật khi đến lịch quét.
        // * Giữ độ lớn nhỏ hơn 1 khi hai hướng triệt tiêu một phần để quái có thể chậm lại trong đám đông.
        Vector2 direction = Vector2.ClampMagnitude(
            toPlayer.normalized + separationDirection * separationStrength, 1f);
        // * MovementManager chuẩn hóa hướng: nhân tốc độ với độ lớn để giữ mức giảm tốc ở trên.
        // * Tổng tốc độ không vượt moveSpeed; giới hạn bước đi vẫn ngăn chạy vượt khoảng cách đến Player.
        // * Hướng nhìn vẫn theo Player, không bị lực tách làm flip ngược lúc quái phải lùi ra xa hàng xóm.
        view.Move(direction, runtime.FinalStats.moveSpeed * direction.magnitude, toPlayer.magnitude, toPlayer.x);
    }

    // * Chia pha theo ID vỏ thành 16 nhóm; quái spawn cùng lúc không mặc định quét cùng một frame.
    // * Giữa hai mốc chỉ giữ hướng tách, không giữ tham chiếu hàng xóm có thể đã despawn/đổi chủ.
    private void UpdateSeparation(CharacterView view)
    {
        if (separationStrength <= 0f || Time.deltaTime <= 0f)
            return;
        if (!scanScheduled)
        {
            uint phase = unchecked((uint)view.GetInstanceID()) % 16u;
            nextScanTime = Time.time + scanInterval * (phase / 16f);
            scanScheduled = true;
        }
        if (Time.time < nextScanTime)
            return;

        // * Bỏ qua các mốc đã trễ thay vì quét bù nhiều lần khi FPS giảm.
        // * Cộng theo lịch cũ để các pha không tự dồn về cùng một mốc sau các frame chậm.
        float elapsedIntervals = Mathf.Floor((Time.time - nextScanTime) / scanInterval) + 1f;
        nextScanTime += elapsedIntervals * scanInterval;

        // * CharacterSearch quản lý đồng bộ physics và buffer mượn dùng chung cho mọi kiểu tìm kiếm.
        separationDirection = CalculateSeparation(view);
    }

    // * Mượn buffer trong phạm vi using, tính xong hướng tách thì trả ngay; không giữ danh sách riêng.
    // * Vùng query chạm hình collider; kiểm tra lại khoảng cách giữa hai tâm để lực tách về 0 đúng bán kính.
    private Vector2 CalculateSeparation(CharacterView view)
    {
        Vector2 position = GetSeparationCenter(view.transform.position);
        using var neighbors = CharacterSearch.FindAll(position, separationRadius, CharacterTeam.Enemy, view);
        float radiusSquared = separationRadius * separationRadius;
        Vector2 direction = Vector2.zero;
        for (int i = 0; i < neighbors.Count; i++)
        {
            if (!neighbors[i].TryGetCharacter(out CharacterView neighbor))
                continue;

            // * Dùng cùng quy ước tâm cho cả hai bên để offset không tạo hướng tách lệch một phía.
            // * Tâm mình trừ tâm hàng xóm tạo vector đi RA XA con đó, không chọn trái/phải ngẫu nhiên.
            Vector2 away = position - (Vector2)GetSeparationCenter(neighbor.transform.position);
            float distanceSquared = away.sqrMagnitude;
            if (distanceSquared >= radiusSquared)
                continue;
            if (distanceSquared <= 0.000001f)
            {
                direction += GetOverlapDirection(view.GetInstanceID(), neighbor.GetInstanceID());
                continue;
            }

            float distance = Mathf.Sqrt(distanceSquared);
            float weight = 1f - distance / separationRadius;
            direction += away / distance * weight;
        }
        // * Cộng ảnh hưởng hàng xóm rồi giới hạn, tránh mật độ cao tự làm lực tách tăng vô hạn.
        return Vector2.ClampMagnitude(direction, 1f);
    }

    // * Một cách tính tâm dùng chung cho truy vấn, khoảng cách và vòng debug để hình vẽ khớp logic.
    // * Offset giống nhau triệt tiêu khi trừ hai tâm; nó dời vùng query, không tự đổi khoảng cách giữa quái.
    private Vector3 GetSeparationCenter(Vector3 position)
    {
        position.y += separationOffsetY;
        return position;
    }

    // * Hai tâm trùng nhau không có hướng để chuẩn hóa; chọn hướng ổn định từ ID của cặp.
    // * Cùng một cặp nhận hai vector đối nhau nên không cùng chạy sang một phía rồi tiếp tục dính nhau.
    private static Vector2 GetOverlapDirection(int ownId, int neighborId)
    {
        uint pairHash = unchecked((uint)Mathf.Min(ownId, neighborId) * 73856093u ^
            (uint)Mathf.Max(ownId, neighborId) * 19349663u);
        float angle = (pairHash % 360u) * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        return ownId < neighborId ? direction : -direction;
    }

    #endregion

    #region Contact Attack & Recoil

    private bool TryContactAttack(CharacterView view, CharacterRuntimeData runtime, CharacterView target)
    {
        if (Time.time < nextContactTime || !view.Overlaps(target))
            return false;

        // * Chốt hướng trước callback nhận damage; không phụ thuộc mục tiêu sau khi xử lý chết.
        Vector2 away = view.HitPosition - target.HitPosition;
        if (away.sqrMagnitude <= 0.000001f)
            away = GetOverlapDirection(view.GetInstanceID(), target.GetInstanceID());
        nextContactTime = Time.time + contactCooldown;
        DamageResult result = target.ReceiveDamage(runtime.CreateDamage(contactDamageMultiplier));
        if (!result.Accepted || knockbackDistance <= 0f)
            return false;

        knockbackDirection = away.normalized;
        knockbackRemaining = knockbackDuration;
        separationDirection = Vector2.zero;
        return true;
    }

    private bool ApplyKnockback(CharacterView view)
    {
        if (knockbackRemaining <= 0f)
            return false;
        float stepTime = Mathf.Min(Time.deltaTime, knockbackRemaining);
        knockbackRemaining -= stepTime;
        // * MovementManager tích phân theo deltaTime cả frame; tỷ lệ giữ bước cuối đúng quãng lùi cấu hình.
        float speed = (knockbackDistance / knockbackDuration) * (stepTime / Time.deltaTime);
        CharacterView player = players.CurrentPlayer;
        float facing = player != null ? player.transform.position.x - view.transform.position.x : -knockbackDirection.x;
        view.Move(knockbackDirection, speed, facingDirection: facing);
        return true;
    }

    #endregion

    #if UNITY_EDITOR
    #region Separation Debug

    // * CharacterView gọi khi chọn Enemy trong Scene view; dùng đúng bán kính đã chốt cho lượt spawn này.
    // * Vẽ trên mặt phẳng XY theo world unit như OverlapCircle, không nhân theo scale của vỏ hoặc visual.
    // ! Chỉ hiển thị phạm vi quét, không thực hiện truy vấn Physics2D hoặc thay đổi hành vi di chuyển.
    public void DrawSeparationGizmo(Vector3 position)
    {
        // * DrawingScope tự khôi phục màu và ma trận để không ảnh hưởng gizmo của đối tượng khác.
        using (new UnityEditor.Handles.DrawingScope(Color.yellow, Matrix4x4.identity))
            UnityEditor.Handles.DrawWireDisc(GetSeparationCenter(position), Vector3.forward, separationRadius);
    }

    #endregion
    #endif

    #region Death Behavior

    // * Chết mới thu hồi; bỏ hoàn toàn hành vi test chạm Player thì biến mất.
    public void OnDeath(CharacterView view, CharacterRuntimeData runtime) => characters.Despawn(view);

    #endregion
}
```

</details>

### EnemySpawnManager.cs

Nguồn: [Assets/_Project/Scripts/Characters/Enemies/EnemySpawnManager.cs](../Assets/_Project/Scripts/Characters/Enemies/EnemySpawnManager.cs)

Phần thay đổi so với HEAD:

```diff
diff --git a/Assets/_Project/Scripts/Characters/Enemies/EnemySpawnManager.cs b/Assets/_Project/Scripts/Characters/Enemies/EnemySpawnManager.cs
index 3d864d3..c3bddfa 100644
--- a/Assets/_Project/Scripts/Characters/Enemies/EnemySpawnManager.cs
+++ b/Assets/_Project/Scripts/Characters/Enemies/EnemySpawnManager.cs
@@ -15,6 +15,9 @@ public class EnemySpawnManager : MonoBehaviour
     [Header("Enemy Separation")]
     [SerializeField] private EnemyController.SeparationSettings separation = new EnemyController.SeparationSettings(); // * Một bộ cấu hình cho quái do spawner tạo; không thêm trạng thái né lên CharacterDataSO.
 
+    [Header("Enemy Contact Attack")]
+    [SerializeField] private EnemyController.ContactSettings contactAttack = new EnemyController.ContactSettings(); // * Damage khi chạm và phản lực sau hit; mỗi controller chốt cấu hình tại spawn.
+
     [Header("Spawn Area")]
     // * Chiều rộng X và chiều cao Y của toàn vùng sinh, tính bằng world unit; chỉ sinh trên viền.
     [Tooltip("World-space width and height of the rectangular spawn perimeter.")]
@@ -123,7 +126,7 @@ public class EnemySpawnManager : MonoBehaviour
         StatBonus bonus = progress.GetBonus(template, Time.time);
         var runtime = new CharacterRuntimeData(template, 1, 1, default, default, default, bonus);
         // * Controller mới mỗi lần spawn: hướng tách cache và lịch quét không đi theo vỏ được tái sử dụng.
-        CharacterView enemy = factory.Spawn(template, runtime, new EnemyController(players, factory, separation),
+        CharacterView enemy = factory.Spawn(template, runtime, new EnemyController(players, factory, separation, contactAttack),
             new Vector3(point.x, point.y, spawnZ));
 
         if (enemy != null)
```

<details><summary>Code đầy đủ hiện tại</summary>

```csharp
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// * Sinh quái theo nhịp ở các điểm trên viền hình chữ nhật và giới hạn số quái đang hoạt động.
public class EnemySpawnManager : MonoBehaviour
{

    #region Spawn Settings

    [Header("References")]
    [SerializeField] private Transform spawnCenter;   // * Tâm vùng sinh quái; mặc định lấy camera chính nếu chưa gán trong Inspector.
    [SerializeField] private WaveConfigSO waveConfig; // * Chọn loại quái/nhịp/trần theo wave; mọi loại dùng cùng vỏ từ CharacterFactory.

    [Header("Enemy Separation")]
    [SerializeField] private EnemyController.SeparationSettings separation = new EnemyController.SeparationSettings(); // * Một bộ cấu hình cho quái do spawner tạo; không thêm trạng thái né lên CharacterDataSO.

    [Header("Enemy Contact Attack")]
    [SerializeField] private EnemyController.ContactSettings contactAttack = new EnemyController.ContactSettings(); // * Damage khi chạm và phản lực sau hit; mỗi controller chốt cấu hình tại spawn.

    [Header("Spawn Area")]
    // * Chiều rộng X và chiều cao Y của toàn vùng sinh, tính bằng world unit; chỉ sinh trên viền.
    [Tooltip("World-space width and height of the rectangular spawn perimeter.")]
    [SerializeField] private Vector2 areaSize = new Vector2(10f, 6f); // * Kích thước toàn vùng spawn theo world unit, chỉ lấy điểm trên viền.
    // * Số điểm mỗi cạnh, tính cả góc; các góc chung chỉ thêm một lần.
    // * Tổng điểm = 4 * pointsPerSide - 4; giá trị 4 tạo 12 điểm.
    [Tooltip("Includes the two corners on each side. 4 creates 12 unique spawn points.")]
    [SerializeField, Min(2)] private int pointsPerSide = 4; // * Số điểm mỗi cạnh gồm hai góc; 4 tạo tổng cộng 12 điểm riêng biệt.
    [SerializeField] private float spawnZ = 0f;  // * Tọa độ Z của quái khi sinh; không phải khoảng cách ngẫu nhiên theo chiều sâu.

    #endregion

    #region Spawn State & Dependencies

    // * Danh sách tọa độ XY trên viền; được xóa nội dung và dùng lại khi tính vùng mới.
    private readonly List<Vector2> spawnPoints = new List<Vector2>(); // * Buffer điểm spawn được xóa và tái sử dụng khi tâm di chuyển.
    private readonly List<CharacterView> activeEnemies = new List<CharacterView>(); // * Được xóa ngay khi Released để tránh đếm nhầm vỏ đã dùng lại làm Player.
    private readonly EnemySpawnProgress progress = new EnemySpawnProgress(); // * Đồng hồ riêng theo loại, giữ nguyên khi đổi wave hoặc không còn quái sống.
    private float nextSpawnTime;  // * Mốc Time.time sớm nhất được kiểm tra sinh tiếp, tính bằng giây.
    private float runStartedAt; // * Mốc bắt đầu để chọn wave theo thời gian game, không dùng thời gian thực.
    private WaveConfigSO.Wave currentWave; // * Nhận biết chuyển wave để áp dụng nhịp mới ngay.
    private LevelManager levels; // * Nghe StartNewRun để reset wave, đồng hồ và thu hồi quái của lượt trước.
    private CharacterFactory factory; // * Ghép runtime/hành vi với prefab vỏ chung.
    private PlayerManager players; // * Truyền mục tiêu cho EnemyController và dừng spawn khi chưa có Player sống.

    [Inject]
    public void Construct(CharacterFactory factory, PlayerManager players, LevelManager levels)
    {
        if (this.levels != null)
            this.levels.RunReset -= ResetRun;
        this.factory = factory;
        this.players = players;
        this.levels = levels;
        this.levels.RunReset += ResetRun;
    }

    private void Start()
    {
        if (waveConfig == null)
            throw new System.InvalidOperationException("Assign Wave Config on EnemySpawnManager.");
        waveConfig.Validate();
        ResetRun();
    }

    // * Reset chỉ khi bắt đầu lượt mới; tắt/bật spawner hoặc đổi wave không làm reset đồng hồ loại quái.
    private void ResetRun()
    {
        while (activeEnemies.Count > 0)
        {
            int last = activeEnemies.Count - 1;
            CharacterView enemy = activeEnemies[last];
            activeEnemies.RemoveAt(last);
            if (enemy == null)
                continue;
            enemy.Released -= OnEnemyReleased;
            factory.Despawn(enemy);
        }
        progress.Reset();
        runStartedAt = Time.time;
        nextSpawnTime = Time.time;
        currentWave = null;
    }

    #endregion

    #region Spawn Loop

    // * Khởi tạo tâm sinh mặc định theo camera có tag MainCamera.
    private void Awake()
    {
        if (spawnCenter == null && Camera.main != null)
            spawnCenter = Camera.main.transform;
    }

    // * Mỗi frame chọn wave theo thời gian game; đến nhịp thì sinh nếu còn chỗ trong trần đang sống.
    private void Update()
    {
        if (waveConfig == null || spawnCenter == null || players.CurrentPlayer == null ||
            !players.RuntimeData.IsAlive || Time.deltaTime <= 0f)
            return;

        WaveConfigSO.Wave wave = waveConfig.GetWave(Time.time - runStartedAt);
        if (wave != currentWave)
        {
            currentWave = wave;
            nextSpawnTime = Time.time;
        }
        if (wave == null || Time.time < nextSpawnTime)
            return;
        nextSpawnTime = Time.time + wave.spawnInterval;

        if (activeEnemies.Count < wave.maxAliveEnemies)
            SpawnEnemy(wave.enemies[Random.Range(0, wave.enemies.Length)]);
    }

    // * Tính lại viền theo tâm hiện tại, chọn ngẫu nhiên một điểm và lấy quái từ Lean Pool.
    private void SpawnEnemy(CharacterDataSO template)
    {
        BuildSpawnPoints();
        if (spawnPoints.Count == 0)
            return;

        // * Chọn đều giữa các điểm có sẵn, không chọn liên tục mọi vị trí trên viền.
        Vector2 point = spawnPoints[Random.Range(0, spawnPoints.Count)];
        // * Bonus chốt vào runtime mới; không cộng vào SO, không cập nhật ngược các cá thể đang sống.
        StatBonus bonus = progress.GetBonus(template, Time.time);
        var runtime = new CharacterRuntimeData(template, 1, 1, default, default, default, bonus);
        // * Controller mới mỗi lần spawn: hướng tách cache và lịch quét không đi theo vỏ được tái sử dụng.
        CharacterView enemy = factory.Spawn(template, runtime, new EnemyController(players, factory, separation, contactAttack),
            new Vector3(point.x, point.y, spawnZ));

        if (enemy != null)
        {
            progress.RecordSpawn(template.CharacterType, Time.time);
            enemy.Released += OnEnemyReleased;
            activeEnemies.Add(enemy);
        }
    }

    #endregion

    #region Spawn Perimeter

    // * Dựng các điểm cách đều trên bốn cạnh, không lặp góc; không sinh điểm trong lòng hình chữ nhật.
    private void BuildSpawnPoints()
    {
        spawnPoints.Clear();

        Vector2 center = (Vector2)spawnCenter.position;
        Vector2 halfSize = areaSize * 0.5f;       // * Nửa kích thước để tính các mép trái/phải/trên/dưới từ tâm.
        int count = Mathf.Max(2, pointsPerSide);  // * Bảo đảm ít nhất hai điểm mỗi cạnh, tránh phép chia cho 0 trong nội suy.

        // * Hai cạnh trên/dưới gồm cả bốn góc; t từ 0 đến 1 là tỷ lệ nội suy dọc cạnh.
        // * Top and bottom edges, including their corners.
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            float x = Mathf.Lerp(center.x - halfSize.x, center.x + halfSize.x, t);
            spawnPoints.Add(new Vector2(x, center.y + halfSize.y));
            spawnPoints.Add(new Vector2(x, center.y - halfSize.y));
        }

        // * Hai cạnh trái/phải bỏ góc vì các góc đã được thêm ở vòng lặp phía trên.
        // * Left and right edges, excluding corners already added above.
        for (int i = 1; i < count - 1; i++)
        {
            float t = i / (float)(count - 1);
            float y = Mathf.Lerp(center.y - halfSize.y, center.y + halfSize.y, t);
            spawnPoints.Add(new Vector2(center.x - halfSize.x, y));
            spawnPoints.Add(new Vector2(center.x + halfSize.x, y));
        }
    }

    #endregion

    #region Active Enemy Tracking

    // * Loại ngay theo vòng đời, không đợi nhịp spawn rồi kiểm tra activeInHierarchy trên vỏ có thể đã đổi chủ.
    private void OnEnemyReleased(CharacterView enemy)
    {
        activeEnemies.Remove(enemy);
    }

    private void OnDestroy()
    {
        if (levels != null)
            levels.RunReset -= ResetRun;
        ResetRun();
    }

    #endregion

    #region Scene Gizmos

    // * Chỉ vẽ hỗ trợ trong Scene view khi chọn đối tượng: viền xanh và các điểm sinh màu vàng.
    // * Không sinh quái; dùng chung hàm tính điểm để hình minh họa khớp vùng sinh.
    private void OnDrawGizmosSelected()
    {
        if (spawnCenter == null && Camera.main != null)
            spawnCenter = Camera.main.transform;
        if (spawnCenter == null)
            return;

        BuildSpawnPoints();
        Vector3 center = spawnCenter.position;
        center.z = spawnZ;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, new Vector3(areaSize.x, areaSize.y, 0f));

        Gizmos.color = Color.yellow;
        foreach (Vector2 point in spawnPoints)
            Gizmos.DrawSphere(new Vector3(point.x, point.y, spawnZ), 0.08f);
    }

    #endregion

}
```

</details>

### CharacterDataSO.cs

Nguồn: [Assets/_Project/Scripts/Characters/Data/CharacterDataSO.cs](../Assets/_Project/Scripts/Characters/Data/CharacterDataSO.cs)

Phần thay đổi so với HEAD:

```diff
diff --git a/Assets/_Project/Scripts/Characters/Data/CharacterDataSO.cs b/Assets/_Project/Scripts/Characters/Data/CharacterDataSO.cs
index c410c11..110bb95 100644
--- a/Assets/_Project/Scripts/Characters/Data/CharacterDataSO.cs
+++ b/Assets/_Project/Scripts/Characters/Data/CharacterDataSO.cs
@@ -45,7 +45,7 @@ public sealed class CharacterDataSO : ScriptableObject
     #region Starting Skill Templates
 
     // ! SerializeReference lưu class thuần có [System.Serializable] triển khai ISkill; không hỗ trợ MonoBehaviour hoặc ScriptableObject.
-    // ! Inspector mặc định chưa có bộ chọn loại class; muốn chọn/thêm loại skill trong Inspector cần custom editor riêng.
+    // * CharacterSkillEditor có nút thêm ProjectileSkill; các field mẫu vẫn chỉnh bằng Inspector.
     [SerializeReference] private List<ISkill> passiveSkills = new List<ISkill>();  // * Các skill bị động mẫu; mỗi phần tử là một đối tượng triển khai ISkill.
     [SerializeReference] private List<ISkill> activeSkills = new List<ISkill>();   // * Các skill chủ động mẫu; phân nhóm bằng danh sách chứa chúng.
 
```

<details><summary>Code đầy đủ hiện tại</summary>

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// * Template chỉ số và skill khởi đầu, dùng chung cấu trúc cho player, enemy và boss.
// ! Không lưu máu hiện tại, cooldown hoặc thay đổi trong trận trên asset; các trạng thái đó thuộc runtime riêng.
[CreateAssetMenu(fileName = "CharacterData", menuName = "Shooter Game/Data/Character")]
public sealed class CharacterDataSO : ScriptableObject
{
    #region Character Identity

    [FormerlySerializedAs("characterId")]
    [SerializeField] private string displayName;  // * Tên hiển thị trên giao diện, đổi tên không ảnh hưởng khóa enum hoặc save.
    [SerializeField] private CharacterType characterType;                    // * Khóa duy nhất để ghép config và lưu tiến trình; chọn một type khác None.
    [SerializeField] private CharacterTeam team; // * Phe của loại nhân vật; boss cũng thuộc Enemy.
    [SerializeField] private GameObject visualPrefab; // * Chỉ chứa hình ảnh/Animator, được gắn làm con của vỏ chung khi spawn.
    [SerializeField] private Vector2 colliderSize = new Vector2(0.16f, 0.16f); // * Kích thước hitbox theo loại, không cần tạo prefab gameplay riêng.
    [SerializeField] private Vector2 colliderOffset = new Vector2(0f, 0.08f); // * Độ lệch hitbox so với gốc vỏ để khớp pivot của sprite.

    public string DisplayName => displayName;  // * Nhãn hiển thị, không dùng làm khóa tra cứu.
    public CharacterType CharacterType => characterType;                       // * Khóa dùng chung cho database, level service và save.
    public CharacterTeam Team => team; // * Dùng phân biệt Player và Enemy khi tạo hành vi và xử lý tiếp xúc.
    public GameObject VisualPrefab => visualPrefab; // * Mẫu visual dùng chung theo loại; không lưu instance đang chơi lên SO.
    public Vector2 ColliderSize => colliderSize; // * Factory áp dụng lại mỗi lần dùng vỏ để không giữ hitbox của loại trước.
    public Vector2 ColliderOffset => colliderOffset; // * Offset được sao chép sang BoxCollider2D của vỏ.

    [Header("Enemy Growth")]
    [SerializeField, Min(0.01f)] private float growthInterval = 50f; // * Số giây game mỗi bậc tăng sức mạnh, tính từ lần đầu loại quái spawn thành công.
    [SerializeField] private StatBonus growthPerStep = new StatBonus { maxHealth = 5f }; // * Bonus mỗi bậc; chỉ áp dụng cho cá thể được spawn sau mốc đó.

    public float GrowthInterval => Mathf.Max(0.01f, growthInterval); // * Chặn chia cho 0 kể cả khi asset được sửa ngoài Inspector.
    public StatBonus GrowthPerStep => growthPerStep; // * Giá trị struct sao chép, không thay đổi SO khi tính bonus.

    #endregion

    #region Base Stats Template

    [SerializeField] private BaseStats baseStats = global::BaseStats.Default;  // * Chỉ số gốc; critChance nhập 1.5 tương ứng 150%.

    // * Trả về bản sao struct; sửa bản sao không làm thay đổi dữ liệu của asset.
    public BaseStats BaseStats => baseStats;

    #endregion

    #region Starting Skill Templates

    // ! SerializeReference lưu class thuần có [System.Serializable] triển khai ISkill; không hỗ trợ MonoBehaviour hoặc ScriptableObject.
    // * CharacterSkillEditor có nút thêm ProjectileSkill; các field mẫu vẫn chỉnh bằng Inspector.
    [SerializeReference] private List<ISkill> passiveSkills = new List<ISkill>();  // * Các skill bị động mẫu; mỗi phần tử là một đối tượng triển khai ISkill.
    [SerializeReference] private List<ISkill> activeSkills = new List<ISkill>();   // * Các skill chủ động mẫu; phân nhóm bằng danh sách chứa chúng.

    public IReadOnlyList<ISkill> PassiveSkills => passiveSkills;  // * Đọc các skill mẫu; runtime cần sao chép cả list và trạng thái skill bên trong.
    public IReadOnlyList<ISkill> ActiveSkills => activeSkills;    // * Không Activate/Deactivate skill mẫu trực tiếp trên asset khi chơi.

    #endregion
}
```

</details>

### ISkill.cs

Nguồn: [Assets/_Project/Scripts/Skills/ISkill.cs](../Assets/_Project/Scripts/Skills/ISkill.cs)

Phần thay đổi so với HEAD:

```diff
diff --git a/Assets/_Project/Scripts/Skills/ISkill.cs b/Assets/_Project/Scripts/Skills/ISkill.cs
index a2b66f5..e5c0022 100644
--- a/Assets/_Project/Scripts/Skills/ISkill.cs
+++ b/Assets/_Project/Scripts/Skills/ISkill.cs
@@ -1,18 +1,22 @@
-
-
-// * Hợp đồng chung cho kỹ năng: cung cấp trạng thái và thao tác bật/tắt.
-// * Interface không tự thực thi hiệu ứng; lớp triển khai sẽ quyết định hành vi.
-public interface ISkill
-{
-
-    #region Skill Contract
-
-    public bool IsActive { get; }  // * Trạng thái kỹ năng đang hoạt động; bên gọi chỉ đọc được qua interface.
-    // * Yêu cầu kích hoạt; tác dụng và điều kiện kích hoạt do lớp triển khai quy định.
-    public void Activate();
-    // * Yêu cầu ngừng kỹ năng; lớp triển khai tự xử lý việc gỡ hiệu ứng nếu có.
-    public void Deactivate();
-
-    #endregion
-
-}
+
+
+// * Hợp đồng chung cho kỹ năng: cung cấp trạng thái và thao tác bật/tắt.
+// * Interface không tự thực thi hiệu ứng; lớp triển khai sẽ quyết định hành vi.
+public interface ISkill
+{
+
+    #region Skill Contract
+
+    public bool IsActive { get; }  // * Trạng thái kỹ năng đang hoạt động; bên gọi chỉ đọc được qua interface.
+    // * Yêu cầu kích hoạt; tác dụng và điều kiện kích hoạt do lớp triển khai quy định.
+    public void Activate();
+    // * Yêu cầu ngừng kỹ năng; lớp triển khai tự xử lý việc gỡ hiệu ứng nếu có.
+    public void Deactivate();
+    // * Mẫu SO tạo một runtime mới; tuyệt đối không Activate/Tick trực tiếp mẫu dùng chung.
+    ISkill CreateRuntime(SkillContext context);
+    // * CharacterSkills gọi theo đồng hồ game, không chạy khi pause hoặc chủ sở hữu đã chết.
+    void Tick(float deltaTime);
+
+    #endregion
+
+}
```

<details><summary>Code đầy đủ hiện tại</summary>

```csharp


// * Hợp đồng chung cho kỹ năng: cung cấp trạng thái và thao tác bật/tắt.
// * Interface không tự thực thi hiệu ứng; lớp triển khai sẽ quyết định hành vi.
public interface ISkill
{

    #region Skill Contract

    public bool IsActive { get; }  // * Trạng thái kỹ năng đang hoạt động; bên gọi chỉ đọc được qua interface.
    // * Yêu cầu kích hoạt; tác dụng và điều kiện kích hoạt do lớp triển khai quy định.
    public void Activate();
    // * Yêu cầu ngừng kỹ năng; lớp triển khai tự xử lý việc gỡ hiệu ứng nếu có.
    public void Deactivate();
    // * Mẫu SO tạo một runtime mới; tuyệt đối không Activate/Tick trực tiếp mẫu dùng chung.
    ISkill CreateRuntime(SkillContext context);
    // * CharacterSkills gọi theo đồng hồ game, không chạy khi pause hoặc chủ sở hữu đã chết.
    void Tick(float deltaTime);

    #endregion

}
```

</details>

### MovementManager.cs

Nguồn: [Assets/_Project/Scripts/Infrastructure/Movement/MovementManager.cs](../Assets/_Project/Scripts/Infrastructure/Movement/MovementManager.cs)

Phần thay đổi so với HEAD:

```diff
diff --git a/Assets/_Project/Scripts/Infrastructure/Movement/MovementManager.cs b/Assets/_Project/Scripts/Infrastructure/Movement/MovementManager.cs
index beca0f3..bf496d7 100644
--- a/Assets/_Project/Scripts/Infrastructure/Movement/MovementManager.cs
+++ b/Assets/_Project/Scripts/Infrastructure/Movement/MovementManager.cs
@@ -185,6 +185,8 @@ public sealed class MovementManager : ILateTickable, IDisposable
             float3 position = buffer[i].Position;
             entries[i].Target.position = new Vector3(position.x, position.y, position.z);
         }
+        // * Đánh dấu để nhóm truy vấn sau đồng bộ một lần, không đồng bộ từng Enemy.
+        CharacterSearch.InvalidatePhysics();
     }
 
     #endregion
```

<details><summary>Code đầy đủ hiện tại</summary>

```csharp
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Zenject;

// * Mã đăng ký di chuyển; mặc định là không hợp lệ, không dùng chỉ số mảng làm danh tính.
public readonly struct MovementHandle
{
    internal readonly MovementManager Owner;  // * Manager sở hữu handle; tránh dùng nhầm handle giữa hai scene/container.
    internal readonly long Id;                // * Mã tăng dần, không tái sử dụng khi pool trả lại cùng một GameObject.

    // * Chỉ manager tạo handle khi đăng ký thành công.
    internal MovementHandle(MovementManager owner, long id)
    {
        Owner = owner;
        Id = id;
    }
}

// * Dịch vụ class thuần cho chuyển động XY của enemy, đạn, boss và các Transform khác.
// * Zenject gọi LateTick sau Update; đối tượng tự quyết định hướng và gửi bằng SetMovement.
// ! API chỉ gọi từ main thread; di chuyển trực tiếp Transform, không giải quyết va chạm vật lý.
public sealed class MovementManager : ILateTickable, IDisposable
{

    #region Movement Data & Jobs

    // * Dữ liệu main thread; Target không được đưa vào job.
    private struct Entry
    {
        public long Id;            // * Danh tính dùng cập nhật bảng tra cứu khi xóa bằng đổi chỗ.
        public Transform Target;   // * Transform cần đọc và áp dụng vị trí trong main thread.
        public Vector2 Velocity;   // * Vận tốc XY đã tính từ hướng chuẩn hóa và tốc độ.
        public float MaxDistance;  // * Giới hạn quãng đường mỗi bước, giúp đối tượng đuổi mục tiêu không đi vượt đích.
    }

    // * Dữ liệu unmanaged liên tiếp trong NativeArray, mỗi phần tử dành cho một đối tượng.
    private struct MovementData
    {
        public float3 Position;    // * Vị trí world-space, giữ nguyên Z khi di chuyển trong mặt phẳng XY.
        public float2 Velocity;    // * Vận tốc world unit/giây theo XY.
        public float MaxDistance;  // * Giới hạn bước đi do đối tượng quyết định; vô cực cho chuyển động tự do.
    }

    // * Burst tối ưu phép tính; Unity chia các chỉ số cho worker, không tạo job riêng từng enemy.
    [BurstCompile]
    private struct MovementJob : IJobParallelFor
    {
        public NativeArray<MovementData> Data;  // * Mỗi Execute chỉ đọc/ghi đúng phần tử index của mình.
        public float DeltaTime;                 // * Thời gian bước mô phỏng lấy từ main thread, tính bằng giây.

        // * Tính bước dịch chuyển có giới hạn, không gọi GameObject/Transform/LeanPool trong job.
        public void Execute(int index)
        {
            MovementData item = Data[index];
            float2 step = item.Velocity * DeltaTime;
            float distance = math.length(step);
            if (distance > item.MaxDistance && distance > 0f)
                step *= item.MaxDistance / distance;
            item.Position += new float3(step.x, step.y, 0f);
            Data[index] = item;
        }
    }

    #endregion

    #region Registry & Native State

    private readonly List<Entry> entries = new List<Entry>(128);  // * Danh sách đặc để duyệt nhanh; chỉ cấp phát thêm khi vượt dung lượng hiện có.
    // * Tra mã đăng ký ra chỉ số hiện tại; xóa phần tử không làm hỏng handle còn sống.
    private readonly Dictionary<long, int> indices = new Dictionary<long, int>(128);
    // * Ngăn đăng ký cùng một Transform nhiều lần khiến đối tượng bị di chuyển hai lần.
    private readonly Dictionary<Transform, long> targets = new Dictionary<Transform, long>(128);
    private NativeArray<MovementData> buffer;  // * Buffer persistent dùng lại qua các frame, tăng gấp đôi khi cần.
    private JobHandle pendingJob;              // * Job gần nhất; luôn Complete trước khi sửa/giải phóng buffer.
    private long nextId;                       // * Mã tiếp theo; không reset trong vòng đời dịch vụ.
    private bool disposed;                     // * Chặn đăng ký hoặc chạy sau khi Zenject đã giải phóng dịch vụ.

    public int Count => entries.Count;  // * Số đối tượng đang đăng ký, phục vụ theo dõi trong debugger/profiler.

    #endregion

    #region Registration & Movement Commands

    // * Đăng ký một lần khi bật/spawn; đối tượng bắt đầu đứng yên tới khi được gửi hướng.
    public MovementHandle Register(Transform target)
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(MovementManager));
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (targets.TryGetValue(target, out long existingId))
            return new MovementHandle(this, existingId);

        long id = checked(++nextId);
        indices.Add(id, entries.Count);
        targets.Add(target, id);
        entries.Add(new Entry { Id = id, Target = target, MaxDistance = float.PositiveInfinity });
        return new MovementHandle(this, id);
    }

    // * Kiểm tra handle còn thuộc đăng ký hiện tại; handle cũ sau despawn không điều khiển object mới.
    public bool IsRegistered(MovementHandle handle)
    {
        return !disposed && ReferenceEquals(handle.Owner, this) && indices.ContainsKey(handle.Id);
    }

    // * direction chỉ quyết định hướng; speed là world unit/giây, maxDistance là giới hạn mỗi bước.
    // * Hướng zero hoặc tốc độ zero sẽ dừng; hướng và tốc độ giữ nguyên tới lần cập nhật tiếp theo.
    public bool SetMovement(MovementHandle handle, Vector2 direction, float speed,
        float maxDistance = float.PositiveInfinity)
    {
        if (!IsRegistered(handle))
            return false;
        if (!math.all(math.isfinite(new float2(direction.x, direction.y))) ||
            !math.isfinite(speed) || float.IsNaN(maxDistance))
            throw new ArgumentException("Movement values must be finite, except an infinite distance limit.");

        int index = indices[handle.Id];
        Entry entry = entries[index];
        entry.Velocity = direction.normalized * Mathf.Max(0f, speed);
        entry.MaxDistance = Mathf.Max(0f, maxDistance);
        entries[index] = entry;
        return true;
    }

    // * Hủy khi OnDisable/despawn; gọi nhiều lần hoặc gửi handle cũ đều an toàn.
    public bool Unregister(MovementHandle handle)
    {
        if (!IsRegistered(handle))
            return false;
        RemoveAt(indices[handle.Id]);
        return true;
    }

    #endregion

    #region Movement Simulation

    // * Zenject gọi mỗi LateUpdate sau khi các đối tượng đã cập nhật hướng trong Update.
    public void LateTick()
    {
        Simulate(Time.deltaTime);
    }

    // * Chạy một bước cho cả nhóm; có thể gọi thủ công để kiểm tra với deltaTime cố định.
    // ! Khi dùng Zenject thì không gọi thêm mỗi frame, tránh di chuyển hai lần.
    public void Simulate(float deltaTime)
    {
        if (disposed)
            return;
        if (!math.isfinite(deltaTime) || deltaTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(deltaTime));

        for (int i = entries.Count - 1; i >= 0; i--)
            if (entries[i].Target == null)
                RemoveAt(i);
        if (entries.Count == 0 || deltaTime == 0f)
            return;

        EnsureCapacity(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            Vector3 position = entry.Target.position;
            buffer[i] = new MovementData
            {
                Position = new float3(position.x, position.y, position.z),
                Velocity = new float2(entry.Velocity.x, entry.Velocity.y),
                MaxDistance = entry.MaxDistance
            };
        }

        pendingJob = new MovementJob { Data = buffer, DeltaTime = deltaTime }
            .Schedule(entries.Count, 64);
        // ! Hoàn tất trong cùng bước để callback pool không thể thay đổi danh sách khi job đang chạy.
        // * Có song song trong nhóm; main thread chờ ở đây trước khi ghi Transform.
        pendingJob.Complete();
        for (int i = 0; i < entries.Count; i++)
        {
            float3 position = buffer[i].Position;
            entries[i].Target.position = new Vector3(position.x, position.y, position.z);
        }
        // * Đánh dấu để nhóm truy vấn sau đồng bộ một lần, không đồng bộ từng Enemy.
        CharacterSearch.InvalidatePhysics();
    }

    #endregion

    #region Buffer & Registry Maintenance

    // * Chỉ cấp phát khi cần tăng dung lượng, giữ buffer cũ khi số lượng giảm.
    private void EnsureCapacity(int count)
    {
        if (buffer.IsCreated && buffer.Length >= count)
            return;
        pendingJob.Complete();
        int capacity = Mathf.NextPowerOfTwo(Mathf.Max(128, count));
        if (buffer.IsCreated)
            buffer.Dispose();
        buffer = new NativeArray<MovementData>(capacity, Allocator.Persistent,
            NativeArrayOptions.UninitializedMemory);
    }

    // * Xóa O(1) bằng chuyển phần tử cuối vào chỗ trống và cập nhật bảng tra handle.
    private void RemoveAt(int index)
    {
        Entry removed = entries[index];
        targets.Remove(removed.Target);
        indices.Remove(removed.Id);
        int last = entries.Count - 1;
        if (index != last)
        {
            Entry moved = entries[last];
            entries[index] = moved;
            indices[moved.Id] = index;
        }
        entries.RemoveAt(last);
    }

    #endregion

    #region Native Memory Cleanup

    // * Zenject tự gọi khi scene/container kết thúc; Complete trước Dispose tránh rò native memory.
    public void Dispose()
    {
        if (disposed)
            return;
        pendingJob.Complete();
        if (buffer.IsCreated)
            buffer.Dispose();
        entries.Clear();
        indices.Clear();
        targets.Clear();
        disposed = true;
    }

    #endregion

}
```

</details>

### GameSceneInstaller.cs

Nguồn: [Assets/_Project/Scripts/Infrastructure/Zenject/GameSceneInstaller.cs](../Assets/_Project/Scripts/Infrastructure/Zenject/GameSceneInstaller.cs)

Phần thay đổi so với HEAD:

```diff
diff --git a/Assets/_Project/Scripts/Infrastructure/Zenject/GameSceneInstaller.cs b/Assets/_Project/Scripts/Infrastructure/Zenject/GameSceneInstaller.cs
index 3702426..b3050c8 100644
--- a/Assets/_Project/Scripts/Infrastructure/Zenject/GameSceneInstaller.cs
+++ b/Assets/_Project/Scripts/Infrastructure/Zenject/GameSceneInstaller.cs
@@ -52,6 +52,7 @@ public sealed class GameSceneInstaller : MonoInstaller
         Container.Bind<VirtualJoystick>().FromInstance(joystick).AsSingle();
         Container.BindInterfacesAndSelfTo<PlayerManager>().AsSingle().NonLazy();
         Container.BindInterfacesAndSelfTo<PoolManager>().AsSingle().NonLazy();
+        Container.Bind<ProjectileService>().AsSingle();
         Container.BindInterfacesAndSelfTo<MovementManager>().AsSingle().NonLazy();
         Container.BindInitializableExecutionOrder<PlayerManager>(-100);
         Container.BindInitializableExecutionOrder<SaveManager>(-200);
```

<details><summary>Code đầy đủ hiện tại</summary>

```csharp
using System;
using Terresquall;
using UnityEngine;
using Zenject;

// * Điểm cấu hình prefab player, joystick và các dịch vụ class thuần cho scene.
public sealed class GameSceneInstaller : MonoInstaller
{
    private SaveManager saves;

    #region Scene References

    [SerializeField] private CharacterView characterPrefab; // * Một prefab vỏ duy nhất dùng cho cả Player, Enemy và boss.
    [SerializeField] private CharacterType defaultPlayerType = CharacterType.BlessedGladiator; // * Loại Player khi save chưa chọn nhân vật.
    [SerializeField] private VirtualJoystick joystick;  // * Joystick trên Canvas, inject cho Player được tạo.
    [SerializeField] private string playerSaveFile = GameConstants.SaveKeys.PlayerFile;  // * Tên file save người chơi; không chứa dữ liệu của lượt chơi.

    #endregion

    #region Gameplay Databases

    [SerializeField] private CharacterDatabaseSO characterDatabase;  // * Danh mục template nhân vật, dùng để dựng dictionary theo ID.
    [SerializeField] private LevelDatabaseSO levelDatabase;          // * Config vĩnh viễn theo nhân vật và config trận dùng chung.
    [SerializeField] private EquipmentDatabaseSO equipmentDatabase;  // * Tra bonus từ ID trang bị đã lưu.

    #endregion

    #region Service Bindings

    // * Một instance mỗi dịch vụ; Zenject quản lý Initialize, LateTick và Dispose.
    public override void InstallBindings()
    {
        if (characterPrefab == null || joystick == null)
            throw new InvalidOperationException("Assign the shared Character Prefab and Joystick in GameSceneInstaller.");
        if (characterDatabase == null || levelDatabase == null)
            throw new InvalidOperationException("Assign Character Database and Level Database in GameSceneInstaller.");
        if (equipmentDatabase == null)
            throw new InvalidOperationException("Assign Equipment Database in GameSceneInstaller.");
        SignalBusInstaller.Install(Container);
        Container.BindInstance(new SaveManager.Settings(playerSaveFile));
        Container.BindInterfacesAndSelfTo<SaveManager>().AsSingle().NonLazy();
        Container.Bind<CharacterDatabaseSO>().FromInstance(characterDatabase).AsSingle();
        Container.Bind<LevelDatabaseSO>().FromInstance(levelDatabase).AsSingle();
        Container.Bind<EquipmentDatabaseSO>().FromInstance(equipmentDatabase).AsSingle();
        Container.BindInterfacesAndSelfTo<PlayerEquipmentService>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<CharacterLevelService>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<RunLevelService>().AsSingle().NonLazy();
        Container.Bind<LevelManager>().AsSingle().NonLazy();
        Container.BindInstance(new CharacterFactory.Settings(characterPrefab));
        Container.BindInterfacesAndSelfTo<CharacterFactory>().AsSingle();
        Container.BindInstance(new PlayerManager.Settings(defaultPlayerType));
        Container.Bind<VirtualJoystick>().FromInstance(joystick).AsSingle();
        Container.BindInterfacesAndSelfTo<PlayerManager>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<PoolManager>().AsSingle().NonLazy();
        Container.Bind<ProjectileService>().AsSingle();
        Container.BindInterfacesAndSelfTo<MovementManager>().AsSingle().NonLazy();
        Container.BindInitializableExecutionOrder<PlayerManager>(-100);
        Container.BindInitializableExecutionOrder<SaveManager>(-200);
        // * Thu hồi nhân vật theo thứ tự visual → vỏ trước khi pool tổng quát và buffer Jobs được giải phóng.
        Container.BindDisposableExecutionOrder<CharacterFactory>(200);
        Container.BindDisposableExecutionOrder<PoolManager>(100);
        Container.BindDisposableExecutionOrder<PlayerManager>(50);
        Container.BindDisposableExecutionOrder<SaveManager>(-50);
        Container.BindDisposableExecutionOrder<MovementManager>(-100);
    }

    #endregion

    // Resolve after installation; SaveManager is bound by this installer itself.
    public override void Start() => saves = Container.Resolve<SaveManager>();

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            saves?.SaveIfDirty();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            saves?.SaveIfDirty();
    }

    private void OnApplicationQuit() => saves?.SaveIfDirty();

}
```

</details>

### CharacterSystemTests.cs

Nguồn: [Assets/_Project/Editor/CharacterSystemTests.cs](../Assets/_Project/Editor/CharacterSystemTests.cs)

Phần thay đổi so với HEAD:

```diff
diff --git a/Assets/_Project/Editor/CharacterSystemTests.cs b/Assets/_Project/Editor/CharacterSystemTests.cs
index 5144317..0df1f27 100644
--- a/Assets/_Project/Editor/CharacterSystemTests.cs
+++ b/Assets/_Project/Editor/CharacterSystemTests.cs
@@ -108,6 +108,7 @@ public sealed class CharacterSystemTests
         var pools = new PoolManager(container);
         container.BindInstance(movement);
         container.BindInstance(pools);
+        container.BindInstance(new ProjectileService(pools));
         CharacterView prefab = Load<GameObject>("Assets/_Project/Prefabs/Character.prefab").GetComponent<CharacterView>();
         var factory = new CharacterFactory(new CharacterFactory.Settings(prefab), pools);
         CharacterDataSO enemyData = Load<CharacterDataSO>(DataPath + "ForagingMaggot_Character.asset");
@@ -161,9 +162,9 @@ public sealed class CharacterSystemTests
             Assert.That(reused, Is.SameAs(enemy));
             Assert.That(reused.RuntimeData.CurrentHealth, Is.EqualTo(enemyData.BaseStats.maxHealth));
 
-            // * Regression theo lỗi thực tế: chạm Player phải đi qua factory, trả visual trước vỏ.
+            // * Hành vi chạm Player chỉ là test cũ; hiện chỉ damage gây chết mới trả visual/vỏ về pool.
             CharacterView target = factory.Spawn(playerData, Runtime(playerData), new PlayerController(null), Vector3.zero);
-            enemyBehavior.OnContact(reused, reused.RuntimeData, target);
+            reused.ReceiveDamage(new DamageInfo(100000f, CharacterTeam.Player));
             Assert.That(reused.RuntimeData, Is.Null);
             Assert.That(reused.transform.childCount, Is.Zero);
             Assert.That(movement.Count, Is.EqualTo(1));
```

<details><summary>Code đầy đủ hiện tại</summary>

```csharp
#if UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Zenject;

// * Kiểm tra các lỗi dễ xuất hiện khi tách data/hành vi/vỏ: HP dùng chung, sai đồng hồ và rò trạng thái pool.
// * Chạy trong Test Runner > EditMode; bài kiểm tra pool tự vào Play Mode để dùng callback Unity thật.
public sealed class CharacterSystemTests
{
    private const string DataPath = "Assets/_Project/DataSO/Characters/"; // * Dữ liệu mẫu thật của dự án để kiểm tra cả tham chiếu asset.
    private const string DatabasePath = "Assets/_Project/DataSO/Databases/"; // * Database dùng chung giữa scene và bài kiểm tra.

    private static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.That(asset, Is.Not.Null, path);
        return asset;
    }

    private static CharacterRuntimeData Runtime(CharacterDataSO data, StatBonus bonus = default)
        => new CharacterRuntimeData(data, 1, 1, default, default, default, bonus);

    [Test]
    public void SpawnBonusAndDamage_AreIsolatedFromOtherInstancesAndTemplate()
    {
        CharacterDataSO data = Load<CharacterDataSO>(DataPath + "ForagingMaggot_Character.asset");
        float baseHealth = data.BaseStats.maxHealth;
        CharacterRuntimeData first = Runtime(data, new StatBonus { maxHealth = 5f });
        CharacterRuntimeData second = Runtime(data, new StatBonus { maxHealth = 10f });
        first.ApplyHealthChange(-7f);

        Assert.That(first.CurrentHealth, Is.EqualTo(baseHealth + 5f - 7f));
        Assert.That(second.CurrentHealth, Is.EqualTo(baseHealth + 10f));
        Assert.That(data.BaseStats.maxHealth, Is.EqualTo(baseHealth));
        Assert.That(first.Team, Is.EqualTo(CharacterTeam.Enemy));
    }

    [Test]
    public void GrowthClock_StartsOnFirstSuccessfulSpawnAndResetsOnlyForNewRun()
    {
        CharacterDataSO first = Load<CharacterDataSO>(DataPath + "ForagingMaggot_Character.asset");
        CharacterDataSO second = Object.Instantiate(first);
        try
        {
            // * Tạo một loại khác trong bản sao thử nghiệm, không chỉnh asset gốc hoặc enum lưu trong save.
            var serialized = new SerializedObject(second);
            serialized.FindProperty("characterType").intValue = (int)CharacterType.BlessedGladiator;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var progress = new EnemySpawnProgress();
            Assert.That(progress.GetBonus(first, 500f).maxHealth, Is.Zero);
            progress.RecordSpawn(first.CharacterType, 30f);
            progress.RecordSpawn(first.CharacterType, 70f);
            progress.RecordSpawn(second.CharacterType, 75f);

            Assert.That(progress.GetBonus(first, 79f).maxHealth, Is.Zero);
            Assert.That(progress.GetBonus(first, 80f).maxHealth, Is.EqualTo(5f));
            CharacterRuntimeData existing = Runtime(first, progress.GetBonus(first, 80f));
            Assert.That(progress.GetBonus(first, 130f).maxHealth, Is.EqualTo(10f));
            Assert.That(progress.GetBonus(second, 130f).maxHealth, Is.EqualTo(5f));
            Assert.That(existing.SpawnBonus.maxHealth, Is.EqualTo(5f));
            progress.Reset();
            Assert.That(progress.GetBonus(first, 500f).maxHealth, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void Waves_SwitchAtBoundaryAndKeepLastWave_EnemyNeedsNoPermanentLevelConfig()
    {
        WaveConfigSO config = Object.Instantiate(Load<WaveConfigSO>(DataPath + "Default_Waves.asset"));
        try
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("waves").arraySize = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            config.Validate();
            WaveConfigSO.Wave first = config.GetWave(0f);
            WaveConfigSO.Wave next = config.GetWave(first.duration);
            Assert.That(next, Is.Not.SameAs(first));
            Assert.That(config.GetWave(100000f), Is.SameAs(next));

            using (var saves = new SaveManager(new SaveManager.Settings("CharacterValidation.es3")))
            using (var levels = new CharacterLevelService(saves,
                Load<CharacterDatabaseSO>(DatabasePath + "CharacterDatabase.asset"),
                Load<LevelDatabaseSO>(DatabasePath + "LevelDatabase.asset")))
            {
                Assert.That(levels.GetCharacter(CharacterType.ForagingMaggot).Team, Is.EqualTo(CharacterTeam.Enemy));
            }
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [UnityTest]
    public IEnumerator SharedShell_ReusesAcrossTeamsAndCleansFailedInitialization()
    {
        yield return new EnterPlayMode();
        var container = new DiContainer();
        var movement = new MovementManager();
        var pools = new PoolManager(container);
        container.BindInstance(movement);
        container.BindInstance(pools);
        container.BindInstance(new ProjectileService(pools));
        CharacterView prefab = Load<GameObject>("Assets/_Project/Prefabs/Character.prefab").GetComponent<CharacterView>();
        var factory = new CharacterFactory(new CharacterFactory.Settings(prefab), pools);
        CharacterDataSO enemyData = Load<CharacterDataSO>(DataPath + "ForagingMaggot_Character.asset");
        CharacterDataSO playerData = Load<CharacterDataSO>(DataPath + "BlessedGladiator_Character.asset");
        try
        {
            // * Bài kiểm tra này kiểm tra gắn/tháo và tiếp xúc, không tick AI nên không cần PlayerManager.
            var enemyBehavior = new EnemyController(null, factory);
            CharacterRuntimeData oldRuntime = Runtime(enemyData);
            CharacterView enemy = factory.Spawn(enemyData, oldRuntime, enemyBehavior, Vector3.right * 100f);
            Assert.That(movement.Count, Is.EqualTo(1));
            Assert.That(enemy.GetComponentsInChildren<SpriteRenderer>().Length, Is.EqualTo(1));
            int releases = 0;
            enemy.Released += view =>
            {
                releases++;
                // * Regression: visual đã ra khỏi hierarchy nhưng vỏ chưa bị tắt khi báo Released.
                Assert.That(view.gameObject.activeInHierarchy, Is.True);
                Assert.That(view.transform.childCount, Is.Zero);
            };
            oldRuntime.ApplyHealthChange(-5f);
            factory.Despawn(enemy);
            factory.Despawn(enemy); // * Thu hồi lặp phải an toàn và không phát lại Released.
            Assert.That(releases, Is.EqualTo(1));
            Assert.That(movement.Count, Is.Zero);
            Assert.That(enemy.RuntimeData, Is.Null);
            Assert.That(enemy.transform.childCount, Is.Zero);

            CharacterRuntimeData fresh = Runtime(playerData);
            CharacterView player = factory.Spawn(playerData, fresh, new PlayerController(null), Vector3.zero);
            Assert.That(player, Is.SameAs(enemy));
            Assert.That(player.RuntimeData, Is.SameAs(fresh));
            Assert.That(fresh.CurrentHealth, Is.EqualTo(playerData.BaseStats.maxHealth));
            Assert.That(player.GetComponent<BoxCollider2D>().size, Is.EqualTo(playerData.ColliderSize));
            Assert.That(player.GetComponentInChildren<SpriteRenderer>().flipX, Is.False);
            // * Tắt tạm thời chỉ dừng movement, không trả visual về pool từ OnDisable và không làm mất data.
            Transform playerVisual = player.transform.GetChild(0);
            player.gameObject.SetActive(false);
            Assert.That(movement.Count, Is.Zero);
            Assert.That(playerVisual.parent, Is.EqualTo(player.transform));
            Assert.That(player.RuntimeData, Is.SameAs(fresh));
            player.gameObject.SetActive(true);
            Assert.That(movement.Count, Is.EqualTo(1));
            factory.Despawn(player);
            Assert.That(releases, Is.EqualTo(1), "Old owner must not receive the next lifetime's event.");

            // * Lỗi trước khi kích hoạt phải trả được vỏ về pool mà không để lại runtime/hành vi dở dang.
            Assert.Throws<System.ArgumentException>(() => factory.Spawn(enemyData, fresh, enemyBehavior, Vector3.zero));
            Assert.That(movement.Count, Is.Zero);
            CharacterView reused = factory.Spawn(enemyData, Runtime(enemyData), enemyBehavior, Vector3.right * 100f);
            Assert.That(reused, Is.SameAs(enemy));
            Assert.That(reused.RuntimeData.CurrentHealth, Is.EqualTo(enemyData.BaseStats.maxHealth));

            // * Hành vi chạm Player chỉ là test cũ; hiện chỉ damage gây chết mới trả visual/vỏ về pool.
            CharacterView target = factory.Spawn(playerData, Runtime(playerData), new PlayerController(null), Vector3.zero);
            reused.ReceiveDamage(new DamageInfo(100000f, CharacterTeam.Player));
            Assert.That(reused.RuntimeData, Is.Null);
            Assert.That(reused.transform.childCount, Is.Zero);
            Assert.That(movement.Count, Is.EqualTo(1));
            factory.Despawn(target);

            // * Thu hồi khi Enemy hết HP là luật trong EnemyController, không nằm trong vỏ hiển thị.
            reused = factory.Spawn(enemyData, Runtime(enemyData), enemyBehavior, Vector3.right * 100f);
            reused.RuntimeData.ApplyHealthChange(-100000f);
            enemyBehavior.Tick(reused, reused.RuntimeData);
            Assert.That(reused.RuntimeData, Is.Null);
            Assert.That(movement.Count, Is.Zero);

            // * Khi đóng scene, factory thu hồi nhân vật trước PoolManager, cũng không reparent trong OnDisable.
            CharacterView closing = factory.Spawn(enemyData, Runtime(enemyData), enemyBehavior, Vector3.right * 100f);
            factory.Dispose();
            Assert.That(closing.RuntimeData, Is.Null);
            Assert.That(closing.transform.childCount, Is.Zero);
            Assert.That(movement.Count, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            factory.Dispose();
            pools.Dispose();
            movement.Dispose();
        }
        yield return new ExitPlayMode();
    }
}
#endif
```

</details>
