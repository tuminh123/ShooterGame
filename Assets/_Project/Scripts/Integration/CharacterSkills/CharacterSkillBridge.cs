using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

// * Lớp kết nối Character với Skill: đọc skill của nhân vật, tạo behavior riêng và cung cấp callback.
// * CharacterManager chỉ báo vòng đời nhân vật; SkillManager chỉ giữ runtime và lịch chạy.
// * Bridge là nơi biết cả hai bên, nhưng không tự đếm cooldown hoặc tự thay manager thực hiện skill.
// * Zenject gọi Initialize một lần, Tick mỗi frame và Dispose khi kết thúc scope.
public sealed class CharacterSkillBridge : IInitializable, ITickable, IDisposable
{
    // * Ghi nhớ các skill đã ghép với MỘT lượt spawn của nhân vật, để có thể yêu cầu manager gỡ đúng chúng.
    // * Hai danh sách dưới giữ bản sao struct dùng cho cleanup bằng Id, không phải đồng hồ runtime hiện hành.
    private sealed class Binding
    {
        public CharacterView Owner; // * Vỏ Unity của nhân vật; pool có thể dùng lại vỏ này cho một lượt spawn khác.
        public CharacterRuntimeData Identity; // * Runtime được chốt khi ghép; dùng để phân biệt các lượt spawn trên cùng một vỏ.
        public bool Active = true; // * Còn được bridge quản lý hay đã bắt đầu tháo; đặt false trước cleanup để chặn callback cũ.
        public readonly List<SkillRuntime> Innate = new List<SkillRuntime>(); // * Bản sao runtime của skill vốn có, chỉ dùng Id để yêu cầu manager gỡ.
        public readonly List<SkillRuntime> Run = new List<SkillRuntime>(); // * Bản sao runtime của skill nhận trong màn; không đọc cooldown sống từ đây.
        public readonly HashSet<SkillDataSO> RunDefinitions = new HashSet<SkillDataSO>(); // * Các asset đã chọn trong màn, để từ chối chọn trùng khi chưa có luật nâng cấp.
        public bool IsCurrent => Active && Owner != null && ReferenceEquals(Owner.RuntimeData, Identity); // * Xác nhận vỏ vẫn thuộc đúng lượt spawn đã ghép.
        public bool CanRun => IsCurrent && Owner.isActiveAndEnabled && Identity.IsAlive; // * Chỉ cho phép chạy khi đúng lượt spawn, đang bật và còn sống; false sẽ làm manager dừng timer.
    }

    private readonly CharacterManager characters; // * Nguồn danh sách nhân vật hiện có và các sự kiện thêm, thu hồi, chết.
    private readonly SkillManager skills; // * Chủ sở hữu runtime thật, cooldown và việc thêm/gỡ skill.
    private readonly ShootingService shooting; // * Dịch vụ phát projectile theo pattern, được gọi qua callback của ShootingSkill.
    private readonly LevelManager levels; // * Nguồn sự kiện reset lượt chơi; bridge không tính level hoặc EXP.
    private readonly Dictionary<CharacterView, Binding> bindings = new Dictionary<CharacterView, Binding>(); // * Tra phần ghép theo vỏ nhân vật để xử lý vòng đời.
    private bool initialized; // * Ngăn Initialize đăng ký sự kiện lặp.
    private bool disposed; // * Bridge đã kết thúc vòng đời; chặn Tick và nhận skill mới.

    // * Nhận các dịch vụ từ Zenject; constructor chỉ lưu tham chiếu, chưa đăng ký sự kiện hoặc chạy skill.
    public CharacterSkillBridge(CharacterManager characters, SkillManager skills, ShootingService shooting,
        LevelManager levels)
    {
        this.characters = characters;
        this.skills = skills;
        this.shooting = shooting;
        this.levels = levels;
    }

    // * Bắt đầu nghe vòng đời nhân vật và reset màn, sau đó ghép cả những nhân vật đã tồn tại trước bridge.
    public void Initialize()
    {
        if (initialized || disposed) return;
        initialized = true;
        characters.CharacterAdded += OnAdded;
        characters.CharacterRemoved += OnRemoved;
        characters.CharacterDied += OnRemoved; // * Chết cũng gỡ skill, kể cả khi vỏ player còn được giữ để hiển thị game over.
        levels.RunReset += ClearRunSkills;

        // * Duyệt bản chụp vì quá trình ghép/cleanup có thể phát callback làm thay đổi danh sách nhân vật.
        foreach (var pair in new List<KeyValuePair<CharacterView, CharacterDataSO>>(characters.Characters)) // * Một cặp nhân vật và cấu hình của nó.
            OnAdded(pair.Key, pair.Value);
    }

    // * Nhân vật được thêm: đọc trực tiếp một danh sách Skills trên CharacterDataSO, không qua loadout riêng.
    private void OnAdded(CharacterView owner, CharacterDataSO definition)
    {
        if (bindings.ContainsKey(owner)) OnRemoved(owner); // * Dọn phần ghép cũ nếu vỏ đã có đăng ký trước đó.
        var binding = new Binding { Owner = owner, Identity = owner.RuntimeData }; // * Chốt danh tính của đúng lượt spawn này.
        bindings.Add(owner, binding); // * Ghi nhận trước khi đăng ký skill để có thể rollback nếu cấu hình lỗi.
        try
        {
            if (definition.Skills == null)
                throw new InvalidOperationException("Character skill list cannot be null.");

            var unique = new HashSet<SkillDataSO>(); // * Kiểm tra asset trùng trong danh sách skill vốn có của nhân vật này.
            foreach (SkillDataSO skill in definition.Skills) // * Mẫu cấu hình dùng chung; Register sẽ tạo behavior instance riêng.
            {
                if (skill == null || !unique.Add(skill))
                    throw new InvalidOperationException("Character skills must be non-null and unique.");

                binding.Innate.Add(Register(binding, skill, false));
            }
        }
        catch
        {
            OnRemoved(owner); // * Nếu đăng ký dở dang bị lỗi, yêu cầu manager gỡ các skill đã thêm thành công trước đó.
            throw;
        }
    }

    // * Bên chọn thưởng truyền asset đã chọn vào đây sau khi LevelManager thông báo lên cấp.
    // * Chỉ nhận cho nhân vật đang hoạt động và còn sống; hiện trả false khi chọn trùng, chưa tự nâng cấp skill.
    public bool AddRunSkill(CharacterView owner, SkillDataSO definition)
    {
        if (disposed || definition == null || !bindings.TryGetValue(owner, out Binding binding) || // * binding là phần ghép đang có của owner.
            !binding.CanRun || !binding.RunDefinitions.Add(definition)) return false;

        try
        {
            binding.Run.Add(Register(binding, definition, true));
            return true;
        }
        catch
        {
            binding.RunDefinitions.Remove(definition); // * Đăng ký thất bại thì bỏ dấu đã chọn, cho phép thử lại sau khi sửa lỗi.
            throw;
        }
    }

    // * Kiểm tra asset, tạo behavior riêng rồi giao toàn bộ việc giữ runtime/cooldown cho SkillManager.
    private SkillRuntime Register(Binding binding, SkillDataSO definition, bool fromRun)
    {
        definition.Validate();
        ISkill instance = CreateBehavior(binding, definition.Behavior); // * Instance được lắp callback riêng, không sửa behavior mẫu trong asset.

        // * Callback chỉ cung cấp quyền cập nhật; SkillManager không cần biết character để kiểm tra điều kiện này.
        try { return skills.Add(instance, definition.Cooldown, fromRun, () => binding.CanRun); }
        catch
        {
            (instance as IDisposable)?.Dispose(); // * Nếu manager chưa nhận được instance, bridge chịu trách nhiệm dọn instance vừa tạo.
            throw;
        }
    }

    // * Chuyển mẫu cấu hình thành instance chạy thật bằng cách cung cấp đúng dependency mà loại skill đó cần.
    // * ISkill chỉ có TryExecute(); mỗi behavior tự sở hữu các field/callback riêng thay vì dùng context chung.
    // * Khi thêm loại behavior mới, bổ sung cách ghép ở đây; không đưa điều kiện character vào SkillManager.
    private ISkill CreateBehavior(Binding binding, ISkill template)
    {
        switch (template)
        {
            case NormalAttackSkill _: // * Đánh tiếp xúc nhận một callback thực hiện đòn đánh.
                return new NormalAttackSkill(() => ContactAttack(binding));

            case ShootingSkill config: // * config là mẫu chứa tham chiếu tới cấu hình projectile/pattern.
            {
                ShootingDataSO shot = config.Shooting; // * Cấu hình bắn dùng chung, không lưu trạng thái của từng lần sở hữu.
                var state = new ShootingRuntime(); // * Phase và số lần bắn riêng của instance này; không chứa cooldown.
                return new ShootingSkill(() => Fire(binding, shot, state));
            }

            case StatBonusSkill config: // * Behavior giữ lease của bonus để có thể gỡ đúng modifier khi manager xóa skill.
                return new StatBonusSkill(config.Bonus, bonus => // * bonus là bộ chỉ số behavior yêu cầu áp dụng.
                {
                    if (!binding.CanRun) return null;
                    float shieldCapacity = binding.Identity.MaxShield; // * Ghi sức chứa giáp trước khi thêm bonus để tính riêng phần giáp mới.
                    IDisposable lease = binding.Identity.AddTemporaryBonus(bonus); // * Token gỡ modifier trên runtime đã chốt, không trỏ sang lượt spawn mới.
                    try
                    {
                        // * Defense mới chỉ cấp thêm số điểm giáp vừa tăng; không hồi lại phần giáp cũ đã tiêu hao.
                        // * Thêm bonus có thể phát callback thu hồi nhân vật, nên phải kiểm tra lại binding.
                        if (binding.CanRun)
                            binding.Owner.GrantShield(Mathf.Max(0f, binding.Identity.MaxShield - shieldCapacity));
                        return lease;
                    }
                    catch
                    {
                        lease.Dispose(); // * Nếu cấp giáp phát sinh lỗi, gỡ modifier vừa áp để không để bonus bị mắc lại.
                        throw;
                    }
                });

            case RegenerationSkill config: // * Hồi máu nhận riêng hàm kiểm tra thiếu HP và hành động hồi HP.
                return new RegenerationSkill(config.HealAmount,
                    () => binding.CanRun && binding.Identity.CurrentHealth < binding.Identity.FinalStats.maxHealth,
                    amount => { if (binding.CanRun) binding.Owner.ChangeHealth(amount); }); // * amount là lượng HP behavior muốn hồi qua controller.

            case EmergencyShieldSkill config: // * Tạo giáp nhận riêng hàm đọc tỷ lệ HP và hành động cấp giáp.
                return new EmergencyShieldSkill(config.HealthThreshold, config.ShieldAmount,
                    () => binding.CanRun
                        ? binding.Identity.CurrentHealth / binding.Identity.FinalStats.maxHealth * 100f
                        : float.PositiveInfinity, // * Binding không còn hợp lệ thì trả giá trị vượt ngưỡng, ngăn behavior kích hoạt.
                    amount => { if (binding.CanRun) binding.Owner.GrantShield(amount); }); // * amount là lượng giáp behavior muốn cấp qua controller.

            default:
                throw new InvalidOperationException($"Add dependency wiring for {template.GetType().Name} in CharacterSkillBridge.");
        }
    }

    // * Callback đánh thường: tìm đối phương trong tầm, xác nhận hai hitbox tiếp xúc rồi gọi combat của character.
    // * Manager chỉ nhận true/false để quyết định bắt đầu cooldown, không biết cách tìm mục tiêu hoặc gây damage.
    private static bool ContactAttack(Binding binding)
    {
        if (!binding.CanRun) return false;
        CharacterTarget target = CharacterSearch.FindNearest(binding.Owner.HitPosition,
            binding.Identity.FinalStats.range, CharacterSearch.OpponentOf(binding.Identity.Team), binding.Owner); // * Mục tiêu có kèm danh tính lượt spawn.
        if (!target.TryGetCharacter(out CharacterView victim) || !CharacterSearch.Overlaps(binding.Owner, victim)) // * victim là mục tiêu còn hợp lệ để kiểm tra tiếp xúc.
            return false;

        return binding.Owner.DealDamage(target).Accepted; // * Đòn được chấp nhận nếu có mất HP hoặc giáp; controller xử lý damage và phản lực.
    }

    // * Callback bắn: dùng state riêng của skill để phát pattern, còn cooldown do SkillManager xử lý.
    private bool Fire(Binding binding, ShootingDataSO shot, ShootingRuntime state)
    {
        if (!binding.CanRun) return false;
        CharacterTarget target = default; // * Có thể không có mục tiêu đối với pattern bắn theo hướng cố định.
        Vector2 position = (Vector2)binding.Owner.HitPosition + shot.MuzzleOffset; // * Vị trí phát đạn trong world space.
        Vector2 direction = shot.DefaultDirection.normalized; // * Hướng bắn mặc định, được thay bằng hướng ngắm nếu tìm được mục tiêu.
        if (shot.RequiresTarget)
        {
            target = CharacterSearch.FindNearest(binding.Owner.HitPosition, binding.Identity.FinalStats.range,
                CharacterSearch.OpponentOf(binding.Identity.Team), binding.Owner);
            if (!target.TryGetCharacter(out CharacterView victim)) return false; // * Không có mục tiêu hợp lệ thì chưa bắn và chưa tiêu cooldown.
            Vector2 toward = (Vector2)victim.HitPosition - position; // * Vector từ nòng tới mục tiêu; tránh chuẩn hóa khi hai vị trí trùng nhau.
            if (toward.sqrMagnitude > 0.000001f) direction = toward.normalized;
        }
        return shooting.Fire(shot, state, binding.Identity, position, direction, target) > 0; // * Có ít nhất một projectile được phát thì tính là thực hiện thành công.
    }

    // * Điểm nối đồng hồ Unity với manager thuần C#; chỉ truyền deltaTime, không đếm cooldown ở bridge.
    public void Tick()
    {
        if (disposed) return;
        try { skills.Tick(Time.deltaTime); }
        catch (Exception exception) { Debug.LogException(exception); } // * Manager đã gom/cô lập lỗi skill; bridge đưa lỗi ra Unity Console.
    }

    // * Nhân vật chết/thu hồi: ngắt binding trước, rồi yêu cầu manager gỡ tất cả runtime thuộc lượt spawn đó.
    private void OnRemoved(CharacterView owner)
    {
        if (!bindings.TryGetValue(owner, out Binding binding)) return; // * Gọi lặp sau khi đã dọn thì không làm gì.
        bindings.Remove(owner);
        binding.Active = false; // * Callback cũ lập tức mất quyền tác động, kể cả khi vỏ sắp được pool dùng lại.
        var runtimes = new List<SkillRuntime>(binding.Innate); // * Gom các bản sao runtime để gỡ bằng Id sau khi tách khỏi binding.
        runtimes.AddRange(binding.Run);
        binding.Innate.Clear();
        binding.Run.Clear();
        binding.RunDefinitions.Clear();
        RemoveSkills(runtimes); // * Chỉ manager thực sự remove và cleanup behavior; runtime không tự Dispose.
    }

    // * Bắt đầu lượt mới: manager dọn nhóm trong màn, bridge bỏ các bản ghi tương ứng; giữ nguyên skill vốn có.
    public void ClearRunSkills()
    {
        try { skills.ClearRunSkills(); }
        finally
        {
            // * Dù một behavior cleanup lỗi, manager vẫn thử dọn hết nhóm; bridge không giữ lại các bản ghi đã kết thúc.
            foreach (Binding binding in bindings.Values) // * Các nhân vật còn được bridge theo dõi.
            {
                binding.Run.Clear();
                binding.RunDefinitions.Clear();
            }
        }
    }

    // * Yêu cầu gỡ từng runtime; một behavior cleanup lỗi không được khiến những skill còn lại bị bỏ sót.
    private void RemoveSkills(List<SkillRuntime> runtimes)
    {
        List<Exception> failures = null; // * Chỉ cấp phát khi có lỗi; gom lại để báo sau khi đã thử gỡ toàn bộ.
        foreach (SkillRuntime runtime in runtimes) // * Struct bản sao; SkillManager tra Id để gỡ bản thật đang được quản lý.
        {
            try { skills.Remove(runtime); }
            catch (Exception exception) // * Lỗi cleanup của một skill, không dừng vòng lặp.
            {
                if (failures == null) failures = new List<Exception>();
                failures.Add(exception);
            }
        }
        if (failures != null) throw new AggregateException("Character skill cleanup failed.", failures);
    }

    // * Kết thúc bridge: ngừng nghe sự kiện và gỡ toàn bộ skill do bridge đã ghép.
    // * Không Dispose các manager được inject vì vòng đời của chúng do Zenject quản lý.
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        characters.CharacterAdded -= OnAdded;
        characters.CharacterRemoved -= OnRemoved;
        characters.CharacterDied -= OnRemoved;
        levels.RunReset -= ClearRunSkills;

        List<Exception> failures = null; // * Gom lỗi nhưng vẫn tiếp tục cleanup các nhân vật khác.
        foreach (CharacterView owner in new List<CharacterView>(bindings.Keys)) // * Duyệt bản chụp vì OnRemoved sẽ xóa khóa khỏi bindings.
        {
            try { OnRemoved(owner); }
            catch (Exception exception) // * Lỗi của một binding, không bỏ qua các binding còn lại.
            {
                if (failures == null) failures = new List<Exception>();
                failures.Add(exception);
            }
        }
        if (failures != null) throw new AggregateException("Skill bridge cleanup failed.", failures);
    }
}
