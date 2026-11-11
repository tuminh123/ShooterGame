// Trạng thái luồng một scene; pause chọn skill vẫn thuộc GameplayPauseService, không đổi trạng thái trận.
public enum GameState
{
    Boot, // Scene mới vào; save/prewarm chưa được GameManager xác nhận sẵn sàng.
    PreparingRun, // Đang dọn lượt cũ, reset tiến trình và tạo Player; gameplay còn pause.
    Home, // Chờ bắt đầu lượt mới; chưa có panel Home riêng trong scene hiện tại.
    Playing, // Trận đang hoạt động; pause tạm thời vẫn giữ state này.
    Won, // Điều kiện thắng đã được gameplay báo về GameManager.
    Lost, // Player chết hoặc gameplay yêu cầu kết thúc thua.
    Faulted, // Chuẩn bị tài nguyên/trận lỗi; giữ pause và LastError để chẩn đoán.
    EndingWin, // Gameplay dừng, chờ Player phát xong Win trước khi hiện kết quả.
    EndingLose // Gameplay dừng, chờ Player phát xong Lose trước khi hiện kết quả.
}
