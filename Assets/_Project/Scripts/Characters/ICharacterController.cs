// * Hợp đồng hành vi cho class C# thuần; không kế thừa MonoBehaviour và không giữ dữ liệu mẫu.
// * View chuyển tiếp Tick/chết; mỗi controller quyết định cách sử dụng runtime của cá thể hiện tại.
public interface ICharacterController
{
    void Tick(CharacterView view, CharacterRuntimeData runtime);
    void OnDeath(CharacterView view, CharacterRuntimeData runtime);
}
