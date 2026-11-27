# Luật hiệu năng

> **Trạng thái:** ✅ Hiện hành · Viết 2026-11-27 từ code hiện tại và các phép đo trong `Vfx-2D-Plan.md` mục 5.
> Dùng để đối chiếu mỗi khi viết hoặc review code. Mỗi luật ghi **Lý do** (cơ chế bên trong) và **Hệ quả** (điều xảy ra nếu vi phạm).
> Luật nào ghi **[đã đo]** là đã gặp thật trong dự án, kèm số liệu.

Mục lục:
0. [Ba chuỗi nhân quả cần hiểu trước](#0-ba-chuỗi-nhân-quả-cần-hiểu-trước)
1. [Nguyên tắc chung](#1-nguyên-tắc-chung)
2. [GC: cấp phát bộ nhớ](#2-gc-cấp-phát-bộ-nhớ)
3. [Pool và vòng đời object](#3-pool-và-vòng-đời-object)
4. [API Unity tốn kém](#4-api-unity-tốn-kém)
5. [Tick, Update và sự kiện](#5-tick-update-và-sự-kiện)
6. [Jobs, Burst, NativeArray](#6-jobs-burst-nativearray)
7. [Render: draw call, particle, overdraw](#7-render-draw-call-particle-overdraw)
8. [UI](#8-ui)
9. [Asset và bộ nhớ](#9-asset-và-bộ-nhớ)
10. [Bất đồng bộ (UniTask, coroutine)](#10-bất-đồng-bộ-unitask-coroutine)
11. [Log và debug](#11-log-và-debug)
12. [Cách đo](#12-cách-đo)
13. [Ngân sách đề xuất](#13-ngân-sách-đề-xuất)
14. [Checklist khi viết và review code](#14-checklist-khi-viết-và-review-code)

---

## 0. Ba chuỗi nhân quả cần hiểu trước

Hầu hết luật trong file này quy về một trong ba chuỗi dưới đây. Các bảng phía sau chỉ ghi gọn và trỏ về đây.

### 0.1 Chuỗi GC: cấp phát → khựng frame

```text
Code chạy `new` (hoặc new ẩn: closure, boxing, mảng trả về, chuỗi)
  → object nằm trên heap managed
  → dùng xong, không ai còn tham chiếu → thành RÁC (vẫn chiếm chỗ)
  → rác cộng dồn tới ngưỡng heap → GC chạy
  → GC (Boehm, không phân thế hệ) quét MỌI object còn sống trên heap để biết cái nào là rác
  → trong lúc quét, game không chạy tiếp được (hoặc mất một phần mỗi frame nếu bật Incremental GC)
  → HỆ QUẢ: frame dài đột ngột (khựng), FPS tụt từng nhịp, cảm giác giật khi đang bắn đông
```

- **Vì sao "ít mỗi lần" vẫn nguy hiểm:** 300 B mỗi viên đạn nhìn nhỏ, nhưng ~15 viên/giây là ~5 KB/giây, ~330 KB/phút. Rác không mất đi, nó **cộng dồn** cho tới khi GC phải chạy. Đông quái, boss bắn dày thì nhịp GC dày hơn.
- **Vì sao heap to làm GC chậm hơn:** thời gian quét tỉ lệ với số object **còn sống**, mà game giữ rất nhiều object sống (pool, prewarm, data). Heap càng lớn, mỗi lần GC càng lâu.
- **Vì sao heap "phình" khó xẹp:** Boehm không dồn bộ nhớ (không compact). Đỉnh rác làm heap xin thêm bộ nhớ từ hệ điều hành; sau đó heap ít khi trả lại và có thể phân mảnh. **Hệ quả:** RAM của app cao hơn cần thiết, trên Android dễ bị hệ điều hành kill khi chạy nền.
- **Rác khác rò bộ nhớ:** rác **được** thu hồi (chỉ tốn thời gian GC). Rò là object **vẫn bị giữ** (ví dụ quên gỡ event), không bao giờ thu hồi được. **Hệ quả của rò:** bộ nhớ tăng mãi theo thời gian chơi, cuối cùng crash do hết RAM.

### 0.2 Chuỗi CPU: lời gọi native và công việc thừa

```text
Script C# (managed) gọi API Unity (native C++): GetComponent, transform.position, SetActive, Update…
  → mỗi lần đi qua ranh giới managed ↔ native có chi phí cố định
  → nhân với số object × số frame
  → HỆ QUẢ: main thread dài ra đều đặn (không giật mà chậm), FPS thấp liên tục, máy nóng → hạ xung → càng chậm
```

### 0.3 Chuỗi GPU: draw call, SetPass, overdraw

```text
Mỗi renderer khác material/texture/sorting không gộp được → một DRAW CALL riêng
Mỗi lần đổi shader/material → một SETPASS (đổi trạng thái GPU, rất tốn trên mobile)
Mỗi lớp trong suốt chồng lên nhau → GPU tô lại cùng một pixel nhiều lần (OVERDRAW)
  → HỆ QUẢ: CPU tốn thời gian gửi lệnh vẽ; GPU mobile (băng thông thấp) tô không kịp
  → FPS tụt khi nhiều hiệu ứng cùng lúc, máy nóng nhanh, tốn pin
```

---

## 1. Nguyên tắc chung

| # | Luật | Lý do | Hệ quả nếu vi phạm |
|---|---|---|---|
| N1 | **Simple → DRY → SOLID** vẫn đứng trước; không tối ưu khi chưa đo | Phần lớn code không nằm ở đường nóng; tối ưu sai chỗ làm code khó đọc mà không nhanh hơn | Code rối, dễ bug, tốn thời gian mà FPS không đổi |
| N2 | Phân biệt đường **nóng / ấm / nguội** (bảng dưới) | Chi phí chỉ đáng kể khi nhân với số lần chạy | Đường nóng không tối ưu → giật; tối ưu đường nguội → phí công |
| N3 | **Đo trước, sửa, đo lại** (mục 12) | Trực giác về hiệu năng thường sai; Editor còn có nhiễu riêng | Sửa không trúng nguyên nhân, hoặc tưởng đã nhanh mà thật ra không |
| N4 | Số đo Editor chỉ để **so sánh tương đối**; số tuyệt đối phải đo trên Android | Editor có overhead riêng (~25 KB rác mỗi frame), CPU/GPU PC khác mobile | Kết luận "đủ nhanh" trên PC nhưng giật trên điện thoại |
| N5 | **Pool chỉ trừ rác do tạo/hủy object**; code chạy mỗi lần lấy ra vẫn có thể tạo rác | Rác đến từ mọi `new`, kể cả `new` ẩn trong code dùng lại object | **[đã đo]** Đạn lấy từ pool đủ số vẫn tạo ~303 B mỗi viên trước khi sửa |

| Đường | Ví dụ | Áp luật |
|---|---|---|
| **Nóng** | Mỗi frame (Tick/LateTick), mỗi viên đạn, mỗi impact, mỗi lần trúng, mỗi quái spawn, mỗi số sát thương | **Toàn bộ** luật. Mục tiêu 0 B cấp phát |
| **Ấm** | Mỗi wave, mỗi lần lên cấp, mở popup trong trận | Tránh cấp phát lớn; cấp phát nhỏ chấp nhận được |
| **Nguội** | Khởi động, loading/prewarm, Home, Shop/Gacha/Character | Ưu tiên dễ đọc; LINQ, chuỗi, lambda đều được |

---

## 2. GC: cấp phát bộ nhớ

Mọi dòng trong bảng dẫn tới **chuỗi 0.1**: tạo rác → GC dồn → khựng frame. Cột "Hệ quả" ghi thêm mức độ và hệ quả riêng nếu có.

| # | Mẫu tạo rác | Lý do (cơ chế) | Hệ quả nếu vi phạm | Thay bằng |
|---|---|---|---|---|
| G1 | **Lambda có capture** (dùng biến local, tham số, `this`) | Callback có thể chạy sau khi hàm kết thúc, nên compiler phải đưa biến bị capture lên heap: sinh **class ẩn** + `new` object đó + `new` delegate trỏ vào nó, **mỗi lần chạy dòng đó** | 2 object rác mỗi lần. **[đã đo]** Phần lớn của ~303 B mỗi viên đạn, ~327 B mỗi impact | Gọi hàm trực tiếp; mẫu "thuê rồi khởi tạo" (3.3); lambda **không capture** hoặc `static` (compiler tạo một lần rồi dùng lại) |
| G2 | **Boxing**: truyền struct/enum vào tham số kiểu `object` | `object` là kiểu tham chiếu; giá trị struct phải được **bọc** vào một object mới trên heap | Mỗi lần gọi là một object rác. **[đã đo]** `Enum.IsDefined(typeof(T), v)`: 40 B, 2 lần cấp phát mỗi lần | So sánh trực tiếp; mảng `Enum.GetValues` lấy một lần (`ProjectileSpawnData`) |
| G3 | Struct gán sang interface (`IFoo x = myStruct`, truyền struct vào tham số interface) | Interface là kiểu tham chiếu → boxing như G2 | Rác mỗi lần; ngoài ra sửa bản boxed **không** sửa struct gốc (bug logic khó thấy) | Generic có ràng buộc `where T : struct, IFoo` |
| G4 | `enum.ToString()`, `string.Format`, `"a" + b`, `$"..."` | Chuỗi bất biến: mỗi phép nối tạo chuỗi mới; `enum.ToString()` còn dùng reflection | Rác theo độ dài chuỗi; `enum.ToString()` còn chậm CPU | Chỉ tạo chuỗi khi giá trị đổi; TMP `SetText("{0}", value)` (mục 8) |
| G5 | **LINQ** (`Where`, `Select`, `Any`, `ToList`, `OrderBy`…) | Mỗi toán tử tạo object iterator + delegate (thường capture) + collection kết quả | Nhiều object rác mỗi lần; chậm hơn vòng `for` | Vòng `for`. Đường nguội dùng thoải mái |
| G6 | `foreach` qua **interface** (`IEnumerable<T>`, `IReadOnlyList<T>`, `ICollection<T>`) | Enumerator của `List<T>`/mảng là struct; truy cập qua interface thì phải trả về `IEnumerator<T>` → boxing | Rác mỗi vòng duyệt | `for (int i = 0; i < list.Count; i++)`. `foreach` qua kiểu cụ thể (`List<T>`, mảng, `Dictionary`, `HashSet`) **không** tạo rác |
| G7 | Hàm Unity **trả mảng mới**: `GetComponents*<T>()`, `FindObjectsOfType`, `mesh.vertices`, `Physics2D.*All` | Native không trả được mảng có sẵn của bạn nên phải tạo mảng managed mới mỗi lần | Rác bằng kích thước mảng mỗi lần. **[đã đo]** `GetComponentsInChildren<Renderer>()` chạy mỗi viên đạn | Overload nhận `List<T>` dùng lại (`GetComponentsInChildren(true, list)`), cache một lần, `GetVertices(list)` |
| G8 | Hàm `params T[]` | Compiler tạo mảng chứa tham số mỗi lần gọi | Một mảng rác mỗi lần | Overload có số tham số cố định |
| G9 | `new List<T>(other)` để chụp danh sách trước khi duyệt | Collection mới + mảng bên trong | Rác tỉ lệ với số phần tử, mỗi lần | Field `List<T>` dùng lại (`Clear` + `AddRange`), hoặc duyệt ngược + swap-remove |
| G10 | Collection `new` bên trong hàm chạy nhiều | Như G9 | Rác mỗi lần | Field tạo một lần, `Clear()` khi dùng; đặt `capacity` để không resize |
| G11 | `Dictionary<TEnum, …>` / `HashSet<TEnum>` | So sánh key qua `EqualityComparer<T>.Default`; tùy runtime có thể boxing enum | Có thể có rác mỗi lần tra (chưa đo trong dự án) | Đo trước; có rác thì dùng key `int` hoặc comparer riêng |
| G12 | `+=` event bằng lambda/method group tạo mới mỗi lần | Mỗi lần viết `+= Handler` là một delegate mới | Rác mỗi lần đăng ký; **gỡ bằng lambda mới thì không gỡ được** (khác instance) → rò (chuỗi 0.1, rò) | Tạo delegate một lần (field), dùng cho cả `+=` và `-=` |
| G13 | `yield return new WaitForSeconds(x)` | `WaitForSeconds` là class | Rác mỗi vòng coroutine | Cache instance; hoặc UniTask (mục 10) |
| G14 | `async Task`, `Task.Run` | `Task` là class, state machine trên heap | Rác mỗi lần await | `UniTask` |
| G15 | `Debug.Log($"...")` | Chuỗi nội suy + Unity lấy stack trace | Rác lớn + CPU nặng, **có cả trong build release** | Mục 11 |
| G16 | `GetComponent<T>()` khi component **không có** (chỉ Editor) | Editor trả object "fake null" để báo lỗi rõ hơn | Rác trong Editor làm **nhiễu số đo** | `TryGetComponent(out T x)` |
| G17 | `renderer.material`, `renderer.materials` | Lần gọi đầu Unity **tạo bản sao material** riêng cho renderer đó | Rác + bộ nhớ native mỗi bản sao; **mất batching** (chuỗi 0.3); quên `Destroy` → rò | `sharedMaterial`; vertex color (`SpriteRenderer.color`); `MaterialPropertyBlock` |
| G18 | `Array.Resize`, `ToArray()`, `string.Split` | Mảng mới | Rác mỗi lần | Kích thước cố định từ đầu; `List` dùng lại |
| G19 | `Tuple<…>` (class) | Class | Rác mỗi lần | `ValueTuple` `(a, b)` (struct) |

### 2.1 Lambda: ví dụ cụ thể

```csharp
// ✗ Đường nóng: capture `data` → mỗi lần: new DisplayClass + new Action
pools.Spawn(prefab, pos, rot, PoolGroup.Bullets, beforeActivate: obj => obj.GetComponent<View>().Init(data));

// Compiler viết lại gần như:
//   var closure = new DisplayClass { data = data };              // rác #1
//   var callback = new Action<GameObject>(closure.Lambda);       // rác #2

// ✓ Không capture → delegate tạo một lần, dùng lại mãi (CharacterRegistry)
private static readonly Action<CharacterView> TickStep = view => view.TickFrame(Time.deltaTime);

// ✓ Thuê rồi khởi tạo (3.3): không cần lambda
GameObject obj = pools.Spawn(prefab, pos, rot, PoolGroup.Bullets);
obj.GetComponent<View>().Launch(data);
```

Đường nguội (UI, setup, loading) dùng lambda thoải mái. Lambda đăng ký **một lần** trong `Awake`/constructor không phải vấn đề.

### 2.2 Tự kiểm một dòng code ở đường nóng

1. Có `new` một `class`, mảng hay chuỗi không?
2. Có lambda dùng biến bên ngoài không?
3. Có truyền struct/enum vào chỗ nhận `object` hoặc interface không?
4. Có gọi hàm Unity trả mảng không?
5. Có `foreach` qua interface, LINQ, nối chuỗi không?

Không chắc thì **đo** (12.2).

---

## 3. Pool và vòng đời object

| # | Luật | Lý do (cơ chế) | Hệ quả nếu vi phạm |
|---|---|---|---|
| P1 | Không `Instantiate`/`Destroy` ở đường nóng; mọi object lặp lại đi qua `IGameObjectPool` | `Instantiate` copy cả cây GameObject + component (nhiều object managed và native), chạy `Awake`/`OnEnable`; `Destroy` để lại rác và công dọn native | Rác lớn + CPU nặng ngay frame đó (chuỗi 0.1 + 0.2). **[đã đo]** 1000 đạn + 1000 impact vượt prewarm → frame 1551 ms, 2,5 MB rác; khi pool đủ: 257 ms, 0 B |
| P2 | Class C# thuần tạo lại trong trận **và Profiler thấy tốn** → `Pool<T>` + `IPoolable.ResetForPool` | Mỗi `new` là rác khi bỏ; pool giữ lại để dùng tiếp | Rác đều đặn (chuỗi 0.1). Pool hóa khi chưa đo thì code phức tạp thêm vô ích (N1) |
| P3 | **Prewarm đủ số đỉnh** cho mọi prefab pool (`PoolPrewarmSettings`, `additionalPrefabs`) | Pool cạn thì LeanPool phải `Instantiate` thêm ngay lúc đó | Khựng đúng lúc đông nhất. **[đã đo]** 100 impact vượt 16/loại → tạo thêm 72 clone, frame 95 ms thay vì 37 ms |
| P4 | Clone không được Zenject inject; Manager truyền dữ liệu vào | Inject mỗi clone tốn reflection; còn tạo phụ thuộc ngầm (luật kiến trúc) | CPU khi spawn; object pool giữ đăng ký ngầm (ví dụ tự đăng ký tick) |
| P5 | Trả object về pool **ngay khi xong** | Object đang mượn không cho lượt khác dùng | Số đang hoạt động tăng → vượt prewarm → P3 |
| P6 | Vỏ + visual con: trả **con trước, vỏ sau**; không đổi cha/trả pool trong `OnEnable`/`OnDisable`/`OnDestroy` | Trả vỏ trước thì con bị tắt theo vỏ và bị kéo vào pool của vỏ; Unity cấm đổi cha khi hierarchy đang bật/tắt | Lỗi "Cannot change GameObject hierarchy while activating or deactivating the parent"; visual lạc trong pool sai, lần sau thiếu visual |
| P7 | Object pool **reset toàn bộ** trạng thái lượt trước | Object được dùng lại nguyên trạng thái cũ | Bug "ma": đạn mang sát thương/hướng cũ, nhân vật giữ màu trạng thái cũ, callback của lượt cũ chạy trên lượt mới |
| P8 | Hạt/vệt world-space của prefab pool phải **Clear** khi lấy ra (`ParticleProjectileVisual`) | Hạt world-space và `TrailRenderer` nhớ vị trí cũ trong thế giới | Vệt kéo một đường từ chỗ cũ tới chỗ mới khi tái dùng |
| P9 | Giữ tham chiếu tới object pool qua **`CharacterHandle`**/version | Cùng một object có thể đã được trả và cho chủ khác mượn | Trúng nhầm mục tiêu mới, effect áp sang quái khác, callback trễ trả nhầm object |
| P10 | Không trả về pool khi object còn đang chạy code của chính nó; hoãn tới lượt ngoài cùng (`SkillRuntime.Enter/Exit`) | Callback lồng nhau (đòn đánh → chết → dọn) có thể trả object giữa chừng | Object bị reset hoặc cho mượn lại khi hàm của nó chưa chạy xong → NullReference, trạng thái hỏng |

### 3.1 Chọn cách đưa dữ liệu vào object pool

| `OnEnable` của vỏ | Cách khởi tạo | Lý do | Ví dụ |
|---|---|---|---|
| **Dùng dữ liệu** (đăng ký movement, hitbox…) | `beforeActivate` (vỏ còn tắt) | `OnEnable` chạy ngay khi pool bật vỏ; dữ liệu phải có trước, nếu không nó đọc trạng thái cũ | `CharacterView` |
| **Không làm gì** | **Thuê rồi gọi hàm khởi tạo ngay** (3.2) | Không cần gán trước khi bật → không cần lambda → không rác (G1) | `ProjectileView.Launch`, `EffectView.Play` |

### 3.2 Mẫu "thuê rồi khởi tạo"

```csharp
GameObject shell = pools.Spawn(prefab, position, Quaternion.identity, PoolGroup.Bullets);
if (shell == null) return false;
var view = shell.GetComponent<ProjectileView>();
try { view.Launch(context, data); }       // gán dữ liệu + đăng ký ở đây, không ở OnEnable
catch { Release(view); throw; }           // lỗi thì trả vỏ ngay
```

- **Vì sao an toàn:** `Launch` chạy trong cùng lời gọi với `Spawn`, nên không frame nào thấy vỏ chưa có dữ liệu.
- **Vì sao phải `catch` trả vỏ:** không trả thì vỏ "trống" nằm bật trong scene. **Hệ quả:** object lạc, pool thiếu dần, lưới an toàn báo sót khi về Home.
- **Visual con thuê khi vỏ đã bật** thì truyền sẵn vị trí/góc thế giới đúng: `transform.TransformPoint(prefab.localPosition)`, `transform.rotation * prefab.localRotation`.
  **Lý do:** visual bật ngay lúc gắn, `OnEnable` của nó phát hạt luôn. **Hệ quả nếu không:** hạt đầu tiên phát sai hướng hoặc sai chỗ.

### 3.3 Hỏi trạng thái thay vì callback

| Luật | Lý do | Hệ quả nếu vi phạm |
|---|---|---|
| Object do Manager tick thì **hỏi mỗi frame** (`bool Advance(dt)`), không phát event hoàn tất | Event cần delegate (thường capture → G1) và có thể đến trễ, sau khi object đã đổi chủ | Rác mỗi lần; phải thêm version để chặn callback cũ; dễ trả nhầm object |
| Danh sách đang chạy: `List` + **duyệt ngược + swap-remove** | Xóa khi duyệt xuôi làm lệch chỉ số; chụp bản sao tạo rác (G9) | `InvalidOperationException` (collection modified) hoặc rác mỗi frame |

---

## 4. API Unity tốn kém

Các dòng dưới thuộc **chuỗi 0.2** (CPU), một số thêm **chuỗi 0.1** (rác).

| # | Tránh ở đường nóng | Lý do (cơ chế) | Hệ quả nếu vi phạm | Thay bằng |
|---|---|---|---|---|
| U1 | `GetComponent*` mỗi frame | Mỗi lần duyệt danh sách component, qua ranh giới native | CPU tăng theo số object | Cache trong `Awake`/khởi tạo |
| U2 | `FindObjectOfType`, `FindObjectsOfType`, `GameObject.Find`, `Resources.FindObjectsOfTypeAll` | **Duyệt toàn bộ scene** | Chậm tỉ lệ với số object trong scene (hàng nghìn khi đã prewarm); bản `s` còn tạo mảng (G7) | Truyền tham chiếu qua Manager/context; registry |
| U3 | `Resources.Load`, `AssetDatabase` | Đọc đĩa và giải nén đồng bộ trên main thread | Khựng dài; `AssetDatabase` không có trong build → lỗi biên dịch build | Tham chiếu asset qua SO/Settings |
| U4 | `SendMessage`, `BroadcastMessage` | Tìm hàm theo tên bằng reflection trên mọi component | Chậm, không kiểm tra được lúc biên dịch | Gọi trực tiếp; C# event |
| U5 | `gameObject.tag == "X"` | Getter `tag` tạo chuỗi mới | Rác mỗi lần (G4) | `CompareTag("X")`; dự án ưu tiên enum/`Team` |
| U6 | `Animator.SetTrigger("Name")`, `material.SetFloat("_Prop", …)` bằng chuỗi | Mỗi lần băm chuỗi thành id | CPU thừa | `static readonly int Hash = Animator.StringToHash(...)`, `Shader.PropertyToID(...)` |
| U7 | `SetActive` bật/tắt liên tục để ẩn/hiện | Bật/tắt chạy `OnEnable`/`OnDisable` cả nhánh, cập nhật hierarchy, có thể rebuild UI | CPU nặng; khi dùng với pool còn kích hoạt logic đăng ký/hủy đăng ký ngoài ý muốn | `renderer.enabled`, `canvas.enabled` (`EnemyVisibility`, `SkillAreaVisual.SetVisible`) |
| U8 | Đọc/ghi `transform.position` nhiều lần trong vòng lặp | Mỗi lần là một lời gọi native; ghi còn đánh dấu cả nhánh con phải cập nhật | CPU thừa; ghi nhiều lần một frame làm tính lại ma trận nhiều lần | Đọc một lần vào biến; `SetPositionAndRotation` |
| U9 | `Physics2D`/Collider cho gameplay | Dự án đã có hệ không gian Burst/Jobs | Hai hệ va chạm song song; thêm chi phí physics; lệch kết quả | `ISpatialQuery`, `ISpatialQueryBuilder`, gom query theo lô |
| U10 | `Camera.main` trong vòng lặp lớn | Từ Unity 2020.2 đã cache, nhưng vẫn là lời gọi native | CPU nhỏ nhưng thừa | Cache vào biến cục bộ |
| U11 | `Time.deltaTime` đọc nhiều lần | Mỗi lần là một lời gọi property native | Nhỏ; code rối | `float deltaTime = Time.deltaTime;` đầu hàm |
| U12 | Tạo material/mesh/texture lúc chạy mà không `Destroy` | Object native **không** được GC dọn | **Rò bộ nhớ native**, tăng mãi tới khi crash | Tạo một lần, dùng lại; `Destroy` trong `OnDestroy` (`SkillAreaVisual`) |

---

## 5. Tick, Update và sự kiện

### 5.1 Tick

| # | Luật | Lý do (cơ chế) | Hệ quả nếu vi phạm |
|---|---|---|---|
| T1 | **Không `Update`/`LateUpdate` trên object pool hoặc object số lượng lớn**; Manager tick hộ | Mỗi `Update` là một lần native gọi vào managed, kể cả khi hàm rỗng | Hàng trăm object = hàng trăm lời gọi mỗi frame (chuỗi 0.2); thứ tự chạy không kiểm soát được (đọc vị trí cũ) |
| T2 | Không tự đăng ký `TickableManager` (Check Service Contracts chặn) | Object pool giữ đăng ký ngay cả khi đang nằm trong pool | Tick chạy trên object rảnh; phải tự lọc; dễ giữ tham chiếu sau khi hủy (rò) |
| T3 | Manager tick bằng `for` trên `List`, delegate tĩnh (`CharacterRegistry.TickStep`) | Delegate tạo một lần | Delegate/closure mới mỗi frame → rác (G1) |
| T4 | Việc không cần mỗi frame thì chạy theo chu kỳ (ví dụ kiểm tra sang ngày mới mỗi giây) | Kết quả không đổi giữa các frame | CPU thừa mỗi frame |
| T5 | Thoát sớm khi không có gì để làm (`if (list.Count == 0) return;`) | Bỏ được phần chuẩn bị vô ích | CPU thừa nhỏ nhưng cộng dồn |
| T6 | Prefab VFX/visual trong pool không có script `Update` (kể cả script trang trí xoay/nhún); chuyển động trang trí dùng module của Particle System hoặc do chủ visual tick hộ | T1 | Mỗi bản trong pool một lần gọi mỗi frame; `VfxSpin`/`VfxBob` từng vi phạm luật này qua đạn Ice và đã bị xóa (2026-11-27) |

### 5.2 Thứ tự frame (đã chốt)

```text
Tick:     Battle → Combat (effect → skill → đạn) → Stage → Character → World → Notification
LateTick: Simulation (áp di chuyển) → World (camera) → Stage → Character → Combat (vùng skill, VFX) → Battle → UI
```

| Luật | Lý do | Hệ quả nếu vi phạm |
|---|---|---|
| Gửi lệnh di chuyển ở Tick; đọc vị trí đã áp ở LateTick | Simulation áp di chuyển đầu LateTick | Đọc ở Tick là vị trí frame trước → VFX/UI trễ một frame, rung khi di chuyển nhanh |

### 5.3 Event

| # | Luật | Lý do (cơ chế) | Hệ quả nếu vi phạm |
|---|---|---|---|
| E1 | Mọi `+=` có `-=` tương ứng (Dispose/OnDisable/Clear) | Nguồn event giữ tham chiếu tới object nghe | **Rò bộ nhớ**: object không bao giờ được dọn; handler chạy trên object đã chết → NullReference/MissingReference |
| E2 | Gỡ bằng **đúng instance delegate** đã đăng ký | `-=` so sánh theo instance; lambda mới là instance khác | `-=` không gỡ gì cả → như E1 |
| E3 | Event ở đường nóng không truyền `EventArgs` class mới | Mỗi lần phát là một object | Rác mỗi lần phát (G1-G10) |
| E4 | Không static event bus / SignalBus | Luật kiến trúc; static giữ tham chiếu qua scene | Rò khi đổi scene; luồng khó theo dõi |

---

## 6. Jobs, Burst, NativeArray

| # | Luật | Lý do (cơ chế) | Hệ quả nếu vi phạm |
|---|---|---|---|
| J1 | `NativeArray` cấp phát `Allocator.Persistent` lúc khởi tạo/prewarm, **không mỗi frame** | Cấp phát native gọi allocator hệ thống, chậm hơn managed | CPU mỗi frame; dễ quên Dispose → rò |
| J2 | Không resize buffer giữa trận; prewarm dung lượng | Resize = cấp phát mới + copy + Dispose cũ | Khựng đúng lúc đông (lúc cần resize) |
| J3 | Mọi `NativeArray` có `Dispose` trong `Dispose` của chủ; constructor lỗi giữa chừng cũng dọn | Native memory **không** được GC dọn | **Rò bộ nhớ native**; Unity báo "A Native Collection has not been disposed" |
| J4 | Gom nhiều truy vấn vào **một job** (`ProjectileSystem.ExecuteQueries`) | Mỗi lần schedule/complete có chi phí cố định | Nhiều job nhỏ chậm hơn một job lớn, mất lợi ích đa luồng |
| J5 | `Complete()` ngay trước khi đọc kết quả | Job chạy song song cho tới lúc complete | Complete quá sớm → main thread chờ → mất song song |
| J6 | Code trong job chỉ dùng struct/unmanaged; bật `[BurstCompile]` | Burst chỉ biên dịch code unmanaged | Không biên dịch Burst được → chạy chậm hơn nhiều lần, hoặc lỗi |
| J7 | Không biên dịch lại script khi đang Play Mode | Domain reload xóa object mà không gọi `Dispose` | Cảnh báo rò NativeArray (giả), trạng thái Editor hỏng; dễ nhầm là bug thật |

---

## 7. Render: draw call, particle, overdraw

Các dòng dưới thuộc **chuỗi 0.3** (GPU).

### 7.1 Draw call và batching

| # | Luật | Lý do (cơ chế) | Hệ quả nếu vi phạm |
|---|---|---|---|
| R1 | Object cùng loại dùng **chung material**; sprite cùng **atlas** | Unity chỉ gộp batch khi cùng material và texture | Mỗi object một draw call → CPU gửi lệnh nhiều, GPU đổi trạng thái nhiều |
| R2 | Không `renderer.material` lúc chạy (G17) | Bản sao material = material khác | Vỡ batch + rác + rò nếu không Destroy |
| R3 | Đổi màu sprite bằng `SpriteRenderer.color` (vertex color) | Màu nằm trong dữ liệu đỉnh, material vẫn chung | Đổi bằng material thì vỡ batch (`CharacterFeedback` đang làm đúng) |
| R4 | Sorting order theo chuẩn: nền < vùng (0, mesh -1) < nhân vật (1) < đạn/impact (10) < VFX trạng thái (15) < vỏ nhân vật (20) | Unity vẽ theo order; xen kẽ material giữa các order làm phải đổi trạng thái liên tục | Batch vỡ, SetPass tăng; hình bị che sai lớp |
| R5 | Đo **Batches/SetPass/Draw calls** khi thêm hình ảnh mới | Mỗi hiệu ứng mới cộng thêm lệnh vẽ | Không biết thay đổi nào làm tụt FPS. **[đã đo]** đạn/impact particle: +12 batches, +26 draw call so với sprite |

### 7.2 Particle

| # | Luật | Lý do (cơ chế) | Hệ quả nếu vi phạm |
|---|---|---|---|
| V1 | Mỗi đạn ≤ 1–2 Particle System; impact càng ít lớp càng tốt | Mỗi Particle System ≈ 1 draw call + chi phí mô phỏng riêng | **[đã đo]** 100 impact (5–7 lớp) → 812 draw call; 1000 → ~5000, frame ~277 ms |
| V2 | `maxParticles` bằng lượng phát thật | Unity cấp buffer theo `maxParticles` | Tốn bộ nhớ vô ích; đỉnh hạt bất ngờ khi cấu hình phát sai |
| V3 | Impact: không loop, `duration` và thời gian sống hạt ≤ 0,6 s | Impact giữ vỏ pool tới khi mọi hạt tắt | Impact sống lâu → nhiều impact đồng thời → vượt prewarm (P3), nhiều draw call |
| V4 | Không shader **GrabPass/Distortion** | GrabPass chụp lại màn hình mỗi lần vẽ | Rất nặng trên GPU mobile; FPS tụt mạnh khi có hiệu ứng |
| V5 | Tắt soft particle (`_Usedepth = 0` với shader Hovl) | Soft particle đọc depth texture mà camera 2D không tạo | Hiển thị sai (mờ/mất hạt), hoặc buộc bật depth texture → thêm một pass render |
| V6 | Tắt shadow, light probe, reflection probe, motion vector trên renderer hạt | Game 2D unlit không dùng | Tốn CPU/GPU vô ích |
| V7 | Particle dùng **scaled time** | Theo pause/slow motion của gameplay | Hiệu ứng chạy tiếp khi pause; impact xong khi game đang dừng |
| V8 | VFX mới phải có số prewarm (P3) | | Khựng lần đầu xuất hiện đông |
| V9 | Khi vượt ngân sách, bỏ lớp "chi tiết" (khói, mảnh vụn) trước | Lớp phụ tốn draw call mà ít đóng góp hình ảnh trên màn nhỏ | Giữ lại thì tốn GPU vô ích |

### 7.3 Overdraw

| # | Luật | Lý do (cơ chế) | Hệ quả nếu vi phạm |
|---|---|---|---|
| O1 | Hạn chế quad/hạt trong suốt kích thước lớn phủ màn hình | GPU phải tô mọi pixel của mỗi lớp trong suốt | Ví dụ: vùng băng bán kính 5 phủ cả màn hình bằng mesh trong suốt → mỗi pixel tô thêm 1 lần. Nhiều lớp như vậy → GPU mobile tụt FPS, máy nóng |
| O2 | Hạn chế nhiều lớp Additive/Alpha chồng nhau | Mỗi lớp đọc + ghi lại pixel (tốn băng thông) | Như O1 |
| O3 | Kiểm tra bằng Scene view chế độ **Overdraw** khi thêm hiệu ứng lớn | Thấy được vùng tô nhiều lần | Không phát hiện được nguyên nhân GPU nặng |

---

## 8. UI

| # | Luật | Lý do (cơ chế) | Hệ quả nếu vi phạm |
|---|---|---|---|
| UI1 | Chia **Canvas tĩnh** và **Canvas động** | Một phần tử trong Canvas đổi làm Canvas đó tạo lại mesh (rebuild) cho **toàn bộ** phần tử | Đổi một con số làm rebuild cả HUD → CPU tăng vọt mỗi lần đổi |
| UI2 | Cập nhật chữ **chỉ khi giá trị đổi** | Gán text luôn đánh dấu rebuild, kể cả khi giá trị giống cũ | Rebuild + rác chuỗi mỗi frame |
| UI3 | Số dùng TMP `SetText("{0}", value)` | Overload số của TMP ghi thẳng vào buffer ký tự | `value.ToString()` tạo chuỗi rác mỗi lần (G4) |
| UI4 | Tắt **Raycast Target** trên phần tử không bấm | EventSystem kiểm tra mọi Graphic bật raycast mỗi lần chạm | CPU thừa; phần tử trang trí chặn bấm vào nút phía dưới |
| UI5 | Không `LayoutGroup`/`ContentSizeFitter` trên phần tử đổi liên tục | Layout tính lại cả nhánh mỗi lần đổi | CPU tăng mạnh khi danh sách/HUD cập nhật |
| UI6 | Ẩn/hiện thường xuyên bằng `Canvas.enabled`/`CanvasGroup`; `SetActive` cho panel ít mở | `SetActive` chạy `OnEnable`/`OnDisable` và rebuild (U7) | Khựng khi mở/đóng panel liên tục |
| UI7 | Ô/danh sách lặp (ô skill, thẻ nhân vật, số sát thương) đi qua pool | P1 | Instantiate mỗi lần hiện → khựng + rác |
| UI8 | Tween (PrimeTween) không capture ở đường nóng | G1 | Rác mỗi lần chạy tween |
| UI9 | Không dùng chữ tiếng Việt có dấu trên UI | Fallback font của TMP ghi glyph mới vào asset lúc chạy | Asset font bị sửa (file luôn modified); tốn thời gian sinh glyph lúc chạy |

---

## 9. Asset và bộ nhớ

| # | Luật | Lý do (cơ chế) | Hệ quả nếu vi phạm |
|---|---|---|---|
| A1 | Texture: nén ASTC cho Android; tắt **Read/Write**; tắt **mipmap** cho sprite/UI 2D | Read/Write giữ thêm một bản copy trên RAM; mipmap thêm ~33% bộ nhớ mà camera 2D cố định không dùng | RAM tăng gấp đôi cho mỗi texture; build nặng; dễ bị Android kill |
| A2 | Kích thước texture vừa đủ hiển thị (camera ortho 1,8 → ~533 px mỗi đơn vị) | Pixel thừa không hiện trên màn mà vẫn tốn RAM và băng thông | RAM/GPU thừa, load chậm |
| A3 | Sprite qua **Sprite Atlas** theo nhóm hiện cùng nhau | Cùng texture → gộp batch (R1) | Mỗi sprite một draw call |
| A4 | Không giữ tham chiếu tới asset không dùng trong SO load lúc khởi động | Tham chiếu kéo asset (và mọi thứ nó tham chiếu) vào bộ nhớ | RAM cao vô ích; khởi động chậm |
| A5 | Pack bên thứ ba: chỉ tham chiếu bản sao trong `_Project`; không tham chiếu thẳng prefab demo | Build chỉ gồm thứ được tham chiếu | Demo scene/asset lớn lọt vào build |
| A6 | Theo dõi log `[PoolPrewarm] Ready: N objects`; prewarm vừa đủ | Mỗi clone prewarm chiếm RAM và thời gian loading | Prewarm thừa → loading lâu, RAM cao; prewarm thiếu → P3 |

---

## 10. Bất đồng bộ (UniTask, coroutine)

| # | Luật | Lý do (cơ chế) | Hệ quả nếu vi phạm |
|---|---|---|---|
| AS1 | Dùng `UniTask`, không `Task` | `Task` là class + state machine trên heap | Rác mỗi lần await (G14) |
| AS2 | Lambda truyền vào `UniTask.Create`, `ContinueWith`, `Forget` ở đường nóng không capture | G1 | Rác mỗi lần |
| AS3 | Mọi tác vụ async có `CancellationToken` gắn vòng đời chủ (`GameManager.lifetime`) | Tác vụ chạy tiếp sau khi chủ đã bị hủy/trả pool | Chạy trên object đã chết → MissingReference; giữ tham chiếu → rò |
| AS4 | Không `await` mỗi frame cho việc Manager tick làm được | Mỗi await có chi phí lập lịch | CPU thừa, thứ tự chạy khó kiểm soát |
| AS5 | Coroutine: cache `WaitForSeconds`; ưu tiên tick của Manager trên object pool | G13; coroutine dừng khi object tắt | Rác mỗi vòng; coroutine bị cắt ngang khi trả pool → trạng thái dở dang |

---

## 11. Log và debug

| # | Luật | Lý do (cơ chế) | Hệ quả nếu vi phạm |
|---|---|---|---|
| L1 | Không `Debug.Log` ở đường nóng | Mỗi log tạo chuỗi, lấy stack trace, ghi file; **vẫn chạy trong build release** | Rác lớn + CPU nặng; trên máy thật mỗi log có thể tốn hàng ms |
| L2 | Log chẩn đoán bọc trong hàm có `[System.Diagnostics.Conditional("UNITY_EDITOR")]` hoặc define riêng | Compiler xóa cả lời gọi lẫn tham số (chuỗi) khi không có define | Không bọc thì chuỗi tham số vẫn được tạo, dù log bị tắt |
| L3 | Gizmo/vẽ debug (`CharacterSearch.DrawSearchPath`) không chạy hoặc không cấp phát ngoài Editor | Chỉ cần khi debug | CPU/rác thừa trong build |
| L4 | `Debug.LogException` trong `catch` ở đường nóng chấp nhận được | Chỉ chạy khi có lỗi | Không có hệ quả khi chạy bình thường |

---

## 12. Cách đo

### 12.1 Công cụ

| Công cụ | Dùng để | Ghi chú |
|---|---|---|
| **Profiler** (CPU, Memory, Rendering) | Tìm hàm tốn, cột **GC Alloc** | Deep Profile chỉ để tìm nguồn; số bị phóng đại |
| **ProfilerRecorder** (`Unity.Profiling`) | Đo bằng code: "GC Allocated In Frame", "GC Allocation In Frame Count", "Main Thread", "Batches Count", "SetPass Calls Count", "Draw Calls Count" | Đọc `LastValue` ở frame **sau** |
| **Stats** (Game view) | Batches, SetPass nhanh | |
| **Frame Debugger** | Xem vì sao batch bị vỡ | |
| **Development Build + Profiler trên Android** | Số thật | Bắt buộc trước khi kết luận về FPS |

Không dùng được trong Editor của dự án **[đã đo]**:

| Cách | Vì sao không dùng được |
|---|---|
| `GC.GetAllocatedBytesForCurrentThread()` | Mono của Unity không cài đặt, luôn trả 0 |
| `GarbageCollector.GCMode = Disabled` | Editor ném lỗi |
| Chênh lệch `Profiler.GetMonoUsedSizeLong()` | Độ mịn chỉ khoảng 8 KB |
| Đo trong cùng frame với lúc MCP biên dịch đoạn code test | Nhiễu rất lớn |

### 12.2 Đo cấp phát của một thao tác (chính xác tới byte)

Tạm dừng game, mỗi frame luân phiên **làm N lần → dọn → nghỉ**, ghi bộ đếm GC từng frame, lấy **trung vị** 30 chu kỳ, trừ frame nghỉ.
Lý do có frame nghỉ: Editor tự tạo ~25,5 KB rác mỗi frame. Đó là nền, phải trừ đi.

```csharp
var bytesRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
var countRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");
// Callback mỗi frame (ví dụ Camera.onPreCull lọc Camera.main):
//   ghi bytesRec.LastValue / countRec.LastValue cho pha của frame TRƯỚC
//   pha = step % 3: 0 → làm thao tác 64 lần; 1 → dọn; 2 → nghỉ
// Sau 30 chu kỳ: (trung vị pha 0 − trung vị pha 2) / 64 = byte mỗi thao tác
```

Callback đo phải tự nó không cấp phát: mảng kết quả tạo trước, delegate tạo một lần. Nếu không, chính phép đo sẽ tạo rác.

### 12.3 Đo theo frame (chơi thật)

- Kịch bản cố định: trận mới, cấp skill cố định, chờ N giây cho quái dày, ghi **600 frame**.
- Báo cáo trung bình / p50 / p95 / max của: main thread, GC mỗi frame, batches, SetPass, draw call, số object đang hoạt động.
- So trước/sau trên **cùng kịch bản**; chạy đối chứng (ví dụ bản sprite) khi muốn biết chi phí của một thay đổi hình ảnh.
- Bỏ qua spike vài trăm ms trùng lúc MCP biên dịch lệnh.

### 12.4 Stress test

Spawn N đạn + N impact trong một frame, chạy **hai lần**: lần 1 thấy chi phí vượt prewarm (Instantiate), lần 2 thấy chi phí thật khi pool đủ.
Kiểm tra: không lỗi, thu hồi hết, 0 object lạc, về Home không có cảnh báo `[PoolManager] Rời gameplay còn sót`.

### 12.5 Lưu ý khi test qua MCP

| Lưu ý | Lý do | Hệ quả nếu bỏ qua |
|---|---|---|
| Đặt `Application.runInBackground = true` khi test | Editor không được focus thì gần như không chạy frame | Prewarm/trận đứng yên, tưởng game treo |
| Thoát Play Mode trước khi biên dịch | J7 | Cảnh báo rò NativeArray giả, trạng thái hỏng |

---

## 13. Ngân sách đề xuất

> Mức khởi điểm, chỉnh lại sau khi đo trên thiết bị Android mục tiêu.

| Hạng mục | Ngân sách | Vượt thì sao |
|---|---|---|
| GC từ code game trong trận bình thường | **0 B/frame** (trong mức prewarm) | Chuỗi 0.1: khựng định kỳ |
| Bắn đạn, phát impact, VFX trạng thái khi pool đủ | **0 B mỗi lần** | Như trên, tỉ lệ với số đạn |
| Instantiate giữa trận | **0** | P3: khựng đúng lúc đông |
| Frame time trên thiết bị mục tiêu | ≤ 16,6 ms (60 FPS) hoặc ≤ 33,3 ms (30 FPS) | Tụt FPS |
| Draw call trận đông | Theo dõi; hiện ~75. Vượt ~150 thì xem lại particle/atlas | Chuỗi 0.3 |
| Particle System mỗi đạn / impact | ≤ 2 / ≤ 5 | V1 |
| Impact đồng thời | ≤ số prewarm (hiện 16 mỗi loại) | P3 |

---

## 14. Checklist khi viết và review code

**Đường nóng (mỗi frame, mỗi viên đạn, mỗi lần trúng, mỗi spawn):**
- [ ] Không `new` class/mảng/chuỗi; collection là field dùng lại (G9, G10)
- [ ] Không lambda có capture; delegate tạo một lần (G1, G12)
- [ ] Không boxing: không `Enum.IsDefined(typeof…)`, không struct/enum vào `object`/interface (G2, G3)
- [ ] Không LINQ, `foreach` qua interface, nối chuỗi, `params` (G4–G8)
- [ ] Không `GetComponent*` trả mảng; component đã cache (G7, U1)
- [ ] Không `Find*`, `Resources.Load`, `SendMessage` (U2–U4)
- [ ] Không `Debug.Log` (L1)
- [ ] Không `Instantiate`/`Destroy`; đi qua pool; đã có số prewarm (P1, P3)

**Object pool:**
- [ ] Reset hết trạng thái; hạt/vệt được Clear khi lấy ra (P7, P8)
- [ ] Trả con trước vỏ; không đổi cha trong `OnEnable`/`OnDisable`/`OnDestroy` (P6)
- [ ] Giữ tham chiếu qua `CharacterHandle`/version (P9)
- [ ] `OnEnable` rỗng → "thuê rồi khởi tạo" có `catch` trả vỏ; `OnEnable` có việc → `beforeActivate` (3.1, 3.2)

**Tick và event:**
- [ ] Không `Update` trên object số lượng lớn; Manager tick hộ (T1)
- [ ] Mọi `+=` có `-=` với đúng instance delegate (E1, E2)

**Render và VFX:**
- [ ] `sharedMaterial`/vertex color, không `renderer.material` (G17, R2, R3)
- [ ] Sorting order theo R4
- [ ] Particle: ít lớp, `maxParticles` vừa đủ, không GrabPass, `_Usedepth = 0`, không shadow, impact ≤ 0,6 s (V1–V6)
- [ ] Đã thêm prewarm cho prefab mới (V8)

**Native và job:**
- [ ] `NativeArray` cấp phát lúc khởi tạo, có `Dispose`, không resize giữa trận (J1–J3)

**Trước khi merge thay đổi có ảnh hưởng hiệu năng:**
- [ ] Có số đo trước/sau (mục 12) ghi vào tài liệu của tính năng
- [ ] Check Object Pools, Check Service Contracts, Report Missing Scripts, Zenject Validate đạt
- [ ] Chơi thử về Home không có cảnh báo sót object
