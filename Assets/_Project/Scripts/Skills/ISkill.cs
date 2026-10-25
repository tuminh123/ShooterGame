

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

    #endregion

}
