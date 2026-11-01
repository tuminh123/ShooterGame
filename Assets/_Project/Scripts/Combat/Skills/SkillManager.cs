using System;
using System.Collections.Generic;
using System.Threading;

// * Chỉ sở hữu hai danh sách runtime và lịch chạy; không biết Unity, character, mục tiêu hoặc level.
public sealed class SkillManager : IDisposable
{
    private readonly List<SkillRuntime> innateSkills = new List<SkillRuntime>(); // * Các skill vốn có.
    private readonly List<SkillRuntime> runSkills = new List<SkillRuntime>(); // * Các skill nhận trong màn, được gỡ khi ClearRunSkills.
    private static long nextRuntimeId; // * Bộ đếm dùng chung để runtime của hai manager khác nhau không bị trùng mã.
    private bool ticking; // * Đang duyệt: chỉ đánh dấu skill bị gỡ, chưa xóa phần tử làm dịch chỉ số.
    private bool clearing; // * Đang cleanup: chặn thêm skill và giữ ổn định chỉ số khi callback gỡ skill khác.
    private bool disposed; // * Manager đã kết thúc vòng đời, không nhận skill hoặc Tick tiếp.
    public int InnateCount => CountActive(innateSkills); // * Không đếm phần tử đã đánh dấu gỡ trong callback.
    public int RunCount => CountActive(runSkills); // * Số skill trong màn vẫn đang được quản lý.

    // * Trả một bản sao runtime; dùng Id của bản sao để yêu cầu manager gỡ, không dùng nó làm đồng hồ sống.
    public SkillRuntime Add(ISkill behavior, float cooldown, bool fromRun = false, Func<bool> canRun = null)
    {
        if (disposed) throw new ObjectDisposedException(nameof(SkillManager));
        if (clearing) throw new InvalidOperationException("Cannot register skills during cleanup.");
        if (behavior == null) throw new ArgumentNullException(nameof(behavior));
        ValidateTime(cooldown);

        foreach (SkillRuntime existing in innateSkills) // * Không dùng chung một instance behavior giữa các đăng ký.
            if (ReferenceEquals(existing.Behavior, behavior)) throw new ArgumentException("Create a separate behavior instance.");
        foreach (SkillRuntime existing in runSkills) // * Kiểm tra cả hai nhóm.
            if (ReferenceEquals(existing.Behavior, behavior)) throw new ArgumentException("Create a separate behavior instance.");

        long id = Interlocked.Increment(ref nextRuntimeId); // * Danh tính ổn định, không phụ thuộc cooldown thay đổi theo thời gian.
        if (id <= 0) throw new OverflowException("Skill runtime IDs exhausted.");
        var runtime = new SkillRuntime(id, behavior, cooldown, canRun); // * Trạng thái ban đầu, sẵn sàng thử thực hiện.
        if (fromRun) runSkills.Add(runtime);
        else innateSkills.Add(runtime);
        return runtime;
    }

    // * Duyệt trực tiếp hai danh sách. Skill thêm trong callback sẽ chờ Tick sau; skill bị gỡ không chạy tiếp.
    public void Tick(float deltaTime)
    {
        ValidateTime(deltaTime);
        if (disposed || deltaTime == 0f) return;
        if (ticking || clearing) throw new InvalidOperationException("Cannot tick recursively or during cleanup.");

        int innateCount = innateSkills.Count; // * Chốt số phần tử trước khi callback có thể thêm vào một trong hai nhóm.
        int runCount = runSkills.Count; // * Chốt cả hai nhóm ngay từ đầu, không cho skill mới chạy sớm trong cùng Tick.
        List<Exception> failures = null; // * Chỉ cấp phát khi có lỗi, để vẫn cập nhật các skill còn lại.
        ticking = true;
        try
        {
            UpdateSkills(innateSkills, innateCount, deltaTime, ref failures);
            UpdateSkills(runSkills, runCount, deltaTime, ref failures);
        }
        finally
        {
            ticking = false;
            RemoveInactive();
        }
        if (failures != null) throw new AggregateException("Skill execution failed.", failures);
    }

    private void UpdateSkills(List<SkillRuntime> list, int count, float deltaTime, ref List<Exception> failures)
    {
        for (int i = 0; i < count; i++) // * Chỉ số ổn định vì callback không xóa vật lý phần tử trong lúc Tick.
        {
            SkillRuntime runtime = list[i]; // * Lấy bản sao struct để cập nhật; phải ghi ngược vào list.
            if (!runtime.IsActive) continue;
            try
            {
                if (runtime.CanRun != null && !runtime.CanRun()) continue;
                if (!list[i].IsActive) continue; // * Hàm CanRun có thể đã yêu cầu manager gỡ skill.

                runtime.CooldownRemaining = Math.Max(0f, runtime.CooldownRemaining - deltaTime);
                list[i] = runtime; // * Lưu đồng hồ mới TRƯỚC khi gọi behavior có thể phát sinh callback.
                if (runtime.CooldownRemaining > 0f) continue;

                bool succeeded = runtime.Behavior.TryExecute(); // * Behavior kiểm tra điều kiện riêng và thực hiện hành động.
                if (succeeded && list[i].IsActive)
                {
                    runtime = list[i]; // * Đọc lại trạng thái thật; không ghi đè việc gỡ do callback bằng bản sao cũ.
                    runtime.CooldownRemaining = runtime.Cooldown;
                    list[i] = runtime;
                }
            }
            catch (Exception exception) // * Cô lập skill lỗi, không ngăn các skill khác cập nhật.
            {
                if (failures == null) failures = new List<Exception>();
                failures.Add(exception);
                try { RemoveAt(list, i); }
                catch (Exception cleanup) { failures.Add(cleanup); } // * Ghi nhận cả lỗi cleanup.
            }
        }
    }

    // * Tìm bằng Id, không so sánh toàn bộ struct vì cooldown của bản sao bên ngoài có thể đã cũ.
    public bool Remove(SkillRuntime runtime)
    {
        if (runtime.Id == 0) return false;
        return RemoveFrom(innateSkills, runtime.Id) || RemoveFrom(runSkills, runtime.Id);
    }

    private bool RemoveFrom(List<SkillRuntime> list, long id)
    {
        for (int i = 0; i < list.Count; i++) // * Manager tự tìm trong danh sách mình sở hữu, runtime không giữ tham chiếu ngược.
        {
            if (list[i].Id == id && list[i].IsActive)
            {
                RemoveAt(list, i);
                return true;
            }
        }
        return false;
    }

    // * Chốt trạng thái gỡ trước cleanup. Trong Tick/Clear, giữ phần tử tại chỗ để callback không làm lệch chỉ số.
    private void RemoveAt(List<SkillRuntime> list, int index)
    {
        SkillRuntime runtime = list[index]; // * Bản sao của phần tử cần gỡ.
        if (!runtime.IsActive) return;
        runtime.IsActive = false;
        list[index] = runtime;
        if (!ticking && !clearing) list.RemoveAt(index);
        (runtime.Behavior as IDisposable)?.Dispose();
    }

    public void ClearRunSkills() => Clear(false);

    private void Clear(bool all)
    {
        if (clearing) return;
        clearing = true;
        List<Exception> failures = null; // * Cleanup lỗi vẫn phải tiếp tục gỡ các skill còn lại.
        try
        {
            ClearList(runSkills, ref failures);
            if (all || disposed) ClearList(innateSkills, ref failures); // * Dispose trong callback ClearRun vẫn phải dọn cả nhóm vốn có.
        }
        finally
        {
            clearing = false;
            if (!ticking) RemoveInactive();
        }
        if (failures != null) throw new AggregateException("Skill cleanup failed.", failures);
    }

    private void ClearList(List<SkillRuntime> list, ref List<Exception> failures)
    {
        for (int i = 0; i < list.Count; i++) // * clearing chặn thêm và hoãn xóa vật lý, nên không cần một danh sách chụp khác.
        {
            try { RemoveAt(list, i); }
            catch (Exception exception)
            {
                if (failures == null) failures = new List<Exception>();
                failures.Add(exception);
            }
        }
    }

    private void RemoveInactive()
    {
        innateSkills.RemoveAll(runtime => !runtime.IsActive);
        runSkills.RemoveAll(runtime => !runtime.IsActive);
    }

    private static int CountActive(List<SkillRuntime> list)
    {
        int count = 0; // * Chỉ đếm các runtime chưa bị manager đánh dấu gỡ.
        foreach (SkillRuntime runtime in list)
            if (runtime.IsActive) count++;
        return count;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Clear(true);
    }

    private static void ValidateTime(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            throw new ArgumentOutOfRangeException(nameof(value));
    }
}
