# Đội hình đối xứng và Path

## Formation tự quyết định số enemy
Không còn Group Size trên Wave. Formation có Level để tăng số hàng/lớp và Size để giãn hình, giữ nguyên số slot. Mỗi slot tạo một enemy.

| Hình | Level 1 | Level 2 | Level 3 | Quy luật |
| --- | --- | --- | --- | --- |
| Hàng ngang/dọc | 2 | 3 | 4 | Level + 1 |
| Vuông | 4 | 9 | 16 | (Level + 1)² |
| Tam giác | 3 | 6 | 10 | Hàng 1,2,...,Level + 1 |
| Thoi | 4 | 13 | 25 | Lớp lưới đối xứng qua hai trục |
| Tròn | 3 | 7 | 19 | Cấp đầu 3 điểm; cấp sau có tâm và vòng 6,12,18... |
| Lục giác | 6 | 19 | 37 | Lưới axial; cấp đầu chỉ 6 đỉnh, cấp sau lấp cả tâm |

Size không tự tăng khi tăng Level; muốn rộng hơn hãy tăng Size. Hai chiều Size bằng nhau giữ vuông/tròn; khác nhau tạo chữ nhật/elip. Tam giác dùng trọng tâm làm gốc, đối xứng trái/phải. Các hình không phải lúc nào cũng có cùng kiểu đối xứng.

## Custom
Chọn Custom để thêm/xóa điểm gốc trong danh sách, kéo điểm trắng trong Scene. Điểm theo world unit quanh gốc, trước Rotation và Custom Scale. Chọn tự do, đối xứng trái/phải, trên/dưới, hai trục hoặc qua tâm. Điểm trùng được gộp thành một slot. Điểm sinh đối xứng không sửa trực tiếp; kéo điểm gốc để cập nhật cả bộ.

Nút tạo bản Custom từ hình chuẩn tạo asset mới chứa các vị trí hiện tại để chỉnh tự do. Custom Scale giãn hình mà không tăng số enemy. Level và Size chỉ dùng cho hình chuẩn.

## Wave và runtime
Wave chứa danh sách EnemyFormationSpawn (class thuần Serializable). Mỗi phần tử là đúng một lượt spawn, gồm Formation, Path và Settings riêng. Thứ tự danh sách là thứ tự spawn; thêm/xóa/đưa lên/đưa xuống trong Wave Inspector. Danh sách rỗng nghĩa là toàn Wave spawn thường. Không còn hình/đường mặc định và ô đếm số lần độc lập trên Wave.

Spawn After là số enemy thường trước lượt đầu. Ngân sách nhóm = tổng slot của tất cả các phần tử; Spawn After tối đa = tổng quái − ngân sách. Ví dụ tổng 50, các lượt có 9,6,10 slot và After=20: 20 con thường → nhóm 9 → nhóm 6 → nhóm 10 → 5 con thường. Các lượt cách nhau theo Spawn Interval, không chờ quái chết. Inspector báo lỗi nếu thiếu dữ liệu hoặc vượt ngân sách; không cắt nhóm.

Workbench có bộ chọn lượt, bao gồm tùy chọn thêm lượt mới. Lấy/Gán chỉ tác động lượt đã chọn, giữ các lượt khác và Spawn After. Trước khi lưu, tool tính lại ngân sách toàn danh sách. Tab Test vẫn chỉ mô phỏng một nhóm. Cấu hình cũ có N lần đã được chuyển thành N phần tử độc lập, giữ nguyên asset/thông số/Spawn After; bản sao trước chuyển đổi nằm ở Temp/EnemyFormationSpawnListMigration.

Runtime chốt đường, offset và thông số riêng cho mỗi nhóm. Giữ khối dùng offset đối với tâm trên đường; nối đuôi dùng độ trễ dọc đường. Va player làm cả nhóm tan, mọi thành viên knockback ra xa player rồi choáng và chase bằng moveSpeed riêng. Hết số lượt đường thì tan và chase, không knockback/choáng. Player không bị đẩy.

## Tâm Path bám camera và điểm A
Mặc định tâm Path trùng vị trí X/Y của Formation Camera trên EnemySpawnManager (để trống dùng Main Camera). Bật Use Custom Center trong EnemyPathSO để đặt Center Offset. Đây là độ lệch so với camera, không phải tọa độ world cố định. Ví dụ camera (10,5), offset (2,0), A local (-3,1): nhóm xuất hiện tại A world (9,6).

Cả path và đội hình nguyên vẹn dịch theo camera liên tục. Chỉ lấy vị trí X/Y, không lấy góc xoay/zoom. Không chọn điểm spawn ngẫu nhiên, không tự xoay path về player, không có chặng đi từ điểm spawn thường đến A. Nối đuôi đặt slot đầu tại A và các slot sau lùi theo đường để không chồng lên nhau.

Mỗi nhóm chốt hình đường và offset lúc spawn. Runtime cộng độ dịch camera khi lấy đích. EnemySpawnManager cập nhật bù sau LateUpdate camera/MovementManager, kiểm tra danh tính thành viên và làm mới spatial snapshot. Khi nhóm vỡ hoặc hết đường, các con trở về world chase bình thường và không còn bị kéo theo camera.

## Speed riêng của đội hình
EnemyFormationSO có Speed trực tiếp, mặc định 3 world unit/giây. Các Wave chọn cùng asset dùng chung tốc độ đó. Đây là tốc độ chung trên Path tương đối với camera, không nhân stats của enemy. Camera dịch vẫn là thành phần cộng thêm. Khi tan nhóm, từng enemy dùng lại tốc độ riêng.

Đã bỏ Speed Multiplier, Knockback Multiplier và các ô gốc minh họa thêm trước đó. Knockback khi nhóm vỡ dùng khoảng văng gốc trên EnemySpawnManager, giữ thời gian văng/choáng theo lượt. Preview đọc cùng khoảng văng từ spawner. Cấu hình cũ được giữ con số đã nhập thành Speed trực tiếp, không còn mang nghĩa hệ số. Bản sao ở Temp/FormationDirectSpeedMigration. Speed đã chuyển từ Settings sang Formation; dữ liệu trước khi chuyển ở Temp/FormationSpeedOwnership.

## Tool
Mở Tools > Shooter Game > Formation & Path Workbench.
1. Formation: chọn hình/cấp/size hoặc chỉnh Custom, xem số slot.
2. Path: chỉnh Use Custom Center/Center Offset; kéo A/B, thêm/xóa/đổi thứ tự điểm. Shift + click Scene để chèn. Tâm màu tím, khung camera xanh lá. Các điểm là offset quanh tâm bám camera.
3. Test: ghép hai asset, chỉnh Speed của Formation asset đang chọn. Tự bấm Chạy preview hoặc Phá nhóm để xem văng → choáng → chase. Preview không mô phỏng damage.

Nạp từ scene lấy camera từ spawner. Speed trong preview đọc từ Formation asset. Lưu Formation để áp dụng cho mọi lượt dùng asset đó; Gán và lưu Wave khi cần đổi tham chiếu hình/đường.

Vùng và điểm vàng chỉ là spawn quái thường để đối chiếu, không quyết định vị trí đội hình. Tool dùng camera tham chiếu, hoặc Tâm camera mô phỏng nếu không có camera. Khi kéo camera trong Edit mode, path và nhóm nguyên vẹn trong preview bám theo; marker đã tan tiếp tục world chase. Tool không di chuyển camera, không tự bật Play, không sinh enemy thật và ẩn preview khi Play.

Asset mới nằm trong DataSO/Wave/EnemyFormations và DataSO/Wave/EnemyPaths. Waypoint hiện có được giữ nguyên. Tâm Path vẫn bám camera theo cấu hình hiện tại. Các bản sao dữ liệu cũ nằm trong Temp/CameraPathMigration và Temp/FormationDirectSpeedMigration.
