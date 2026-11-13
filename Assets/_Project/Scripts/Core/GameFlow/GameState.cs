// State trận đấu được phát cho gameplay và UI qua GameUIEventBus.
public enum GameState
{
    Boot, // Scene đang khởi tạo tài nguyên.
    PreparingRun, // Dọn lượt cũ và tạo Player cho lượt mới.
    Home, // Sẵn sàng bắt đầu lượt chơi.
    Playing, // Trận đấu đang diễn ra.
    Won, // Animation kết quả xong, UI thắng được mở.
    Lost, // Animation kết quả xong, UI thua được mở.
    Faulted, // Khởi tạo lượt chơi lỗi; gameplay giữ pause.
    EndingWin, // Khóa gameplay và phát animation kết thúc thắng.
    EndingLose // Khóa gameplay và phát animation kết thúc thua.
}
