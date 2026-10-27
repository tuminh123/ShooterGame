using Terresquall;
using UnityEngine;

// * Hành vi riêng của người chơi: hướng di chuyển hoàn toàn do joystick/bàn phím quyết định.
// * Không giữ HP, không tự tạo visual và không chứa AI của Enemy.
public sealed class PlayerController : ICharacterController
{
    private readonly VirtualJoystick joystick; // * Input của scene; không lưu tham chiếu scene trên prefab hoặc SO.

    public PlayerController(VirtualJoystick joystick)
    {
        this.joystick = joystick;
    }

    public void Tick(CharacterView view, CharacterRuntimeData runtime)
    {
        // * Player hết HP thì ngừng di chuyển; luật game over sẽ được bổ sung trong hành vi Player.
        if (!runtime.IsAlive)
        {
            view.Move(Vector2.zero, 0f);
            return;
        }
        Vector2 input = joystick != null ? joystick.GetAxis() : Vector2.zero;
#if UNITY_EDITOR || UNITY_STANDALONE
        // * Hỗ trợ kiểm tra bằng bàn phím; giới hạn độ dài sau khi cộng để đi chéo không nhanh hơn.
        input += new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        input = Vector2.ClampMagnitude(input, 1f);
        // * MovementManager chuẩn hóa hướng nên nhân tốc độ với độ nghiêng để giữ điều khiển analog.
        view.Move(input, runtime.FinalStats.moveSpeed * input.magnitude);
    }

    public void OnDeath(CharacterView view, CharacterRuntimeData runtime)
    {
        // * Giữ vỏ cho màn hình game over sau này; skill ngừng tick khi chủ sở hữu hết HP.
        view.Move(Vector2.zero, 0f);
    }
}
