using Terresquall;
using UnityEngine;

/// <summary>Hành vi của người chơi: hướng di chuyển hoàn toàn do joystick (và bàn phím trong Editor/PC) quyết định.</summary>
/// <remarks>Không giữ HP, không tạo visual, không có AI. Mọi khả năng khác dùng mặc định của <see cref="CharacterControllerBase"/>.</remarks>
public sealed class PlayerController : CharacterControllerBase
{
    private readonly VirtualJoystick joystick; // * Input của scene; không lưu tham chiếu scene trên prefab hoặc SO.

    public PlayerController(VirtualJoystick joystick)
    {
        this.joystick = joystick;
    }

    protected override void TickMovement(CharacterView view, CharacterRuntimeData runtime)
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

    protected override void OnDeath(CharacterView view, CharacterRuntimeData runtime)
    {
        // Giữ vỏ cho Lose; GameManager chuyển Lost sau khi visual phát xong.
        view.Move(Vector2.zero, 0f);
    }
}
