# Tìm mục tiêu, skill và đạn

## Luồng đang được gắn trong scene

`CharacterDataSO.ActiveSkills` của Blessed Gladiator có một mẫu `ProjectileSkill`.
Khi spawn, `CharacterSkills` tạo các bản runtime qua `ISkill.CreateRuntime`, Activate và Tick chúng.
Player không chứa logic bắn trong input/controller. Enemy cũng có thể dùng skill nếu được gán mẫu.
Khi trả Character về pool, bộ runtime skill được Deactivate và giải phóng tham chiếu.

```text
ProjectileSkill đến cooldown
→ CharacterSearch tìm Enemy gần nhất trong range
→ CharacterRuntimeData tính damage × hệ số skill × crit
→ ProjectileService chuẩn bị đạn khi còn inactive, rồi kích hoạt
→ Đạn bay và kiểm tra các hình tròn nhỏ tại vị trí của nó
→ Character thực sự trúng nhận DamageInfo
→ Runtime bên nhận tính defense, trừ HP
→ Controller bên nhận xử lý chết
→ VFX trúng đòn và đạn được thu hồi qua pool
```

## Cấu hình trong Inspector

- `BlessedGladiator_Character.asset`: Base Stats / Range = 2. Active Skills / Projectile Skill: cooldown = 0.6 giây, damage multiplier = 1, search interval = 0.1 giây.
- `HolyBolt.prefab`: bay thẳng, speed = 2.4, hit radius = 0.035, lifetime = 3 giây. Đây là prefab đang gán cho skill.
- `HolyBolt_Homing.prefab`: cùng cấu hình nhưng flight = Homing. Gán prefab này vào mẫu skill để đổi sang đạn đuổi.
- `HolyImpact.prefab`: flash vàng, tồn tại 0.14 giây. Không có damage/collider.
- Inspector Character có nút `Add Projectile Skill`. Gán prefab đạn cho phần tử mới trước khi chơi; nhiều phần tử tương ứng nhiều skill độc lập.
- Range hiện chưa có bonus tăng từ level/trang bị. Không thêm tốc độ đánh hoặc radius đạn vào BaseStats.

## Tìm kiếm và ArrayPool

`CharacterSearch.FindAll` mượn mảng collider, query và lọc trực tiếp trên mảng đó, không chép sang List riêng của mỗi Enemy.
Một mảng runtime identity cũng được mượn để chốt danh tính của từng kết quả. Nó bảo vệ trước trường hợp callback trả một vỏ về pool rồi dùng vỏ đó cho cá thể khác.
Nếu buffer collider đầy, mượn buffer lớn hơn và query lại trước khi bắt đầu xử lý kết quả.

```csharp
using (var results = CharacterSearch.FindAll(center, radius, targetTeam, owner))
{
    for (int i = 0; i < results.Count; i++)
        if (results[i].TryGetCharacter(out var target))
        {
            // Tính tránh nhau, gửi damage hoặc áp hiệu ứng tùy bên sử dụng.
        }
}
```

Kết quả là `ref struct`: không giữ qua await/frame, không copy quyền sở hữu, không Dispose thủ công bên trong using.
Kết thúc using trả cả hai buffer và xóa tham chiếu. Pool có thể cấp phát lúc chưa có mảng phù hợp; không cam kết không cấp phát ngay từ lần gọi đầu.
`FindNearest` chỉ trả `CharacterTarget`, đã trả buffer trước khi hàm kết thúc.
Các lượt tìm kiếm lồng nhau mượn buffer khác, không ghi đè kết quả đang được xử lý.

Mỗi Character có đúng một hitbox trên object gốc, cùng `CharacterView`. Visual không nằm trên layer Player/Enemy.
FindAll chọn theo collider chạm hình tròn; FindNearest so khoảng cách đến tâm hitbox. Không coi đó là khoảng cách đến chân sprite.
Enemy tránh nhau vẫn kiểm tra lại khoảng cách giữa các tâm tránh nhau và giữ offset Y cũ.

## Physics và vòng đời

Character và đạn không có Rigidbody2D. Collider Character chỉ dùng để được query tìm thấy.
MovementManager cập nhật Transform trong LateTick và đánh dấu physics bẩn. CharacterSearch đồng bộ trước query khi đổi frame hoặc có spawn/despawn/di chuyển đánh dấu trong cùng frame.
Nếu code khác tự teleport hoặc chỉnh hitbox, gọi `CharacterSearch.InvalidatePhysics()`.

Đạn Straight chốt hướng khi bắn. Homing đọc vị trí mới của đúng lượt spawn mục tiêu; mục tiêu mất/chết thì giữ hướng cuối và bay tiếp.
Đạn có thể trúng Enemy khác chắn đường. Không tự tìm lại mục tiêu điều hướng khi mất mục tiêu ban đầu.
Mỗi bước bay kiểm tra overlap tại vị trí; không sử dụng cast đường bay. Bước lớn được chia nhỏ theo hit radius, tối đa 32 bước mỗi frame. Hitch quá lớn làm đạn tiến ít hơn trong frame đó; không phải mô phỏng va chạm liên tục tuyệt đối với mọi mục tiêu đang di chuyển.
Mỗi đạn thường chỉ gây một hit; đặt trạng thái kết thúc trước khi gọi nhận damage. Hết lifetime cũng trả về pool.

## Damage và phần mở rộng

Công thức damage và crit nằm trong CharacterRuntimeData, không nằm trong tìm kiếm hoặc đạn.
Crit theo bậc: `1 + tier × (critDamageMultiplier - 1)`; cho phép critChance vượt 100%.
Damage nhận = đầu vào × `100 / (100 + defense)`. Không dùng dodge để né damage.
Enemy hết HP được controller thu hồi ngay. Player hết HP dừng; chưa thêm màn hình game over.
Enemy chạm hitbox Player gây damage từ chỉ số của Enemy, không tự despawn. EnemyController kiểm tra trực tiếp khoảng cách giữa hai collider đã biết, không tạo mảng mục tiêu hoặc callback trigger.

Trong EnemySpawnManager / Enemy Contact Attack có cooldown mặc định 0.8 giây, hệ số damage 1, quãng đẩy lùi Enemy 0.35 unit và thời gian lùi 0.18 giây. Knockback chỉ bắt đầu khi Player thực sự mất HP, có ưu tiên hơn hướng đuổi/tách; xong thì Enemy tiếp tục đuổi. Cooldown riêng từng Enemy, không tạo miễn thương chung cho Player.

Player thực sự nhận damage sẽ đổi tint đỏ trong 0.12 giây; đòn liên tiếp làm mới thời gian nháy. Chỉnh màu/thời gian ở Character.prefab / Damage Feedback. Màu gốc và alpha được bảo toàn, phục hồi trước khi trả visual về pool. Không thêm material hoặc shader cho hiệu ứng này.

Hệ thống tìm kiếm có thể dùng cho vùng damage, làm chậm hoặc hồi máu sau này. Chưa triển khai các hiệu ứng bất lợi, skill AoE hoặc hệ thống buff ở bước này.
Hai MonoBehaviour khung `PassiveSkill` và `ProactiveSkills` cũ không được gắn vào luồng mới; skill runtime triển khai ISkill bằng class Serializable thuần.

## Kiểm tra

Không chạy test hoặc Play Mode theo yêu cầu của người dùng. Kiểm tra biên dịch C# và tham chiếu asset độc lập với Unity Play Mode.
Bài test cũ có gọi OnContact đã được cập nhật theo API nhận damage, và bổ sung binding ProjectileService để không lỗi khi biên dịch/chạy về sau; không thêm hoặc chạy bài test mới.
