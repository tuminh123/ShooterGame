# MCP cho Unity 2022.3

Dự án dùng **MCP for Unity 10.3.0** từ kho gốc
[CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp/tree/aa5fc638d623f56178d50329d9ad8c541a57fe66).
Đây là phần mềm cộng đồng, không phải package do Unity Technologies phát hành.
Package khai báo Unity tối thiểu 2021.3; đã kiểm tra biên dịch bằng Unity **2022.3.62f3**.

## Thiết lập trên máy Windows khác sau khi pull

1. Cài Git từ https://git-scm.com/downloads, Unity Hub và Unity **2022.3.62f3**.
2. Mở PowerShell tại thư mục gốc repository và chạy một lần:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/MCP/Setup.ps1
   ```

   Script tải uv **0.13.7** cho Windows x64 từ kho Astral, kiểm tra SHA-256 trước
   khi giải nén, rồi cài Python 3.11 và thư viện theo `Tools/MCP/uv.lock`.
   Cần Internet ở lần cài đầu. Không cần quyền quản trị; script không sửa PATH
   hay execution policy vĩnh viễn. `Bypass` chỉ áp dụng cho tiến trình này.

3. Mỗi khi muốn dùng MCP, mở một terminal và chạy:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/MCP/Start-Server.ps1
   ```

   Giữ terminal này mở. Server chỉ lắng nghe tại `127.0.0.1:8080`.
   Dừng bằng Ctrl+C. Không chạy thêm server bằng nút Start Server trong Unity.

4. Mở dự án bằng Unity, đợi Package Manager tải package và biên dịch xong.
   Script Editor `Assets/_Project/Editor/McpLocalEnvironment.cs` tự thêm Python
   và uv cục bộ vào PATH của riêng tiến trình Unity, đồng thời tắt telemetry
   trong tiến trình đó. Không sửa PATH Windows. Nếu đã mở Unity trước khi chạy
   Setup, chọn **Shooter Game > MCP > Refresh Local Tools**, rồi bấm **Refresh**
   trong MCP Setup (hoặc đóng và mở lại cửa sổ Setup).
   Mở **Window > MCP for Unity > Toggle MCP Window**.
   Chọn **HTTP Local**, URL `http://127.0.0.1:8080`, rồi bấm **Connect**.
   Nếu muốn tắt cả telemetry phía Editor, mở
   **Window > MCP for Unity > Edit EditorPrefs**, tìm
   `MCPForUnity.TelemetryDisabled` và đặt thành `true`.
5. Mở Codex trong repository này, cho phép tin cậy dự án nếu được hỏi, rồi
   khởi động lại phiên Codex để đọc `.codex/config.toml`.
   Kiểm tra kết nối MCP tên `unity`; thử yêu cầu đọc hierarchy hoặc Console trước.

Nếu cổng 8080 đang được sử dụng, kiểm tra tiến trình đang chiếm cổng trước khi
chạy server. Nếu đổi cổng, cập nhật đồng thời `Start-Server.ps1`,
`.codex/config.toml` và URL trong Unity. Khi mở nhiều dự án Unity, chọn đúng
instance ShooterGame trước khi yêu cầu sửa scene.

## Những gì đi theo Git

- `Packages/manifest.json`, `Packages/packages-lock.json`: package Unity khóa vào
  commit `aa5fc638d623f56178d50329d9ad8c541a57fe66` của release v10.3.0.
- `.codex/config.toml`: kết nối HTTP cục bộ dùng chung, không có token hay đường dẫn máy.
- `Tools/MCP/pyproject.toml`, `Tools/MCP/uv.lock`: server cùng commit và các thư viện
  Python được khóa phiên bản; lock có hash các gói tải từ PyPI.
- `Tools/MCP/Setup.ps1`, `Tools/MCP/Start-Server.ps1` và tài liệu này.
- `Assets/_Project/Editor/McpLocalEnvironment.cs` và `.meta`: giúp Unity nhận
  công cụ cục bộ trên từng máy mà không cần cài thêm Python/uv toàn hệ thống.

`.mcp-tools/`, `Tools/MCP/.venv/`, `Library/`, `Logs/` và các file khác bên trong
`.codex/` bị Git bỏ qua. Đăng nhập Codex, Python, công cụ thực thi và EditorPrefs
không được chuyển bằng Git. Máy mới cần chạy Setup một lần và kết nối trong Unity.
Chỉ theo dõi riêng `.codex/config.toml`; không thêm toàn bộ thư mục `.codex`.

## Nguồn và giới hạn kiểm tra an toàn

- Unity package và server lấy trực tiếp từ kho CoplayDev, ghim cùng commit,
  không dùng nhánh `main`, `beta` hoặc phiên bản `latest` tự thay đổi.
- uv lấy từ [release chính chủ Astral](https://github.com/astral-sh/uv/releases/tag/0.13.7).
  SHA-256 của archive Windows x64:
  `0cdc5b38fe5a8168ed6a25814f5ba43c21d313dd3187314fccb37b7cec46e021`.
- Server chạy từ môi trường riêng trong repository, không cài package Python toàn hệ thống.
  Script khởi chạy đặt `UNITY_MCP_DISABLE_TELEMETRY=true` cho server.
- Không cần API key cho kết nối Unity cục bộ. Không bật chế độ remote hoặc
  mở cổng ra Internet để dùng cấu hình này.
- Ngày 2026-11-12: Microsoft Defender bật bảo vệ thời gian thực; đã chạy custom scan
  trên mã nguồn tải về và thư mục công cụ cục bộ. Đây không phải kiểm toán bảo mật
  đầy đủ hay cam kết phần mềm không có mã độc. MCP có quyền sửa dự án khi được AI gọi.

Không tự nâng phiên bản trước khi kiểm tra thay đổi của upstream. Khi chủ động
nâng cấp, cập nhật đồng thời commit Unity, commit server và tạo lại `uv.lock`.

## Kiểm tra đã thực hiện

- Unity 2022.3.62f3 biên dịch package trong dự án kiểm tra tách biệt, thoát với mã 0.
- Dự án ShooterGame đã tạo được `MCPForUnity.Editor.dll` và `MCPForUnity.Runtime.dll`.
- Sau khi bổ sung môi trường Editor cục bộ, bộ dò dependency của MCP chạy trong
  Unity 2022.3 xác nhận Python **3.11.18**, uv **0.13.7** và Git đều available;
  `IsSystemReady=true`. Kiểm tra batchmode kết thúc với mã 0.
- MCP server khởi động ở loopback; client MCP thực hiện initialize, list tools
  và list resources thành công. Chưa xác minh thao tác scene trong ShooterGame;
  cần bấm Connect trong Editor và nạp lại cấu hình Codex như hướng dẫn ở trên.

## macOS / Linux

Bootstrap PowerShell đi kèm chỉ dành cho Windows x64. Trên macOS/Linux, cài
[uv từ Astral](https://docs.astral.sh/uv/getting-started/installation/) và Git,
rồi chạy từ thư mục repository:

```sh
uv sync --project Tools/MCP --locked --no-dev --python 3.11
UNITY_MCP_DISABLE_TELEMETRY=true Tools/MCP/.venv/bin/mcp-for-unity \
  --transport http --http-host 127.0.0.1 --http-url http://127.0.0.1:8080 \
  --default-instance ShooterGame
```

Sau đó kết nối Editor và Codex như trên. Quy trình này chưa được chạy thử trên
macOS/Linux trong lần thiết lập hiện tại.
