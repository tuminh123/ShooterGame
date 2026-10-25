using UnityEngine;

// * Hành vi riêng của quái: tự tìm hướng đến Player hiện tại, hoàn toàn không đọc joystick.
public sealed class EnemyController : ICharacterController
{
    private readonly PlayerManager players; // * Tra mục tiêu hiện tại để không giữ Transform Player cũ sau khi đổi nhân vật.
    private readonly CharacterFactory characters; // * Thu hồi visual trước vỏ qua một đầu mối, không gọi pool trực tiếp cho nhân vật.

    public EnemyController(PlayerManager players, CharacterFactory characters)
    {
        this.players = players;
        this.characters = characters;
    }

    public void Tick(CharacterView view, CharacterRuntimeData runtime)
    {
        // * Chết/thu hồi là quyết định của Enemy; vỏ không chứa luật sống chết theo phe.
        if (!runtime.IsAlive)
        {
            characters.Despawn(view);
            return;
        }
        CharacterView target = players.CurrentPlayer;
        Vector2 direction = target != null && target.RuntimeData != null && target.RuntimeData.IsAlive
            ? (Vector2)(target.transform.position - view.transform.position)
            : Vector2.zero;
        // * Giới hạn quãng đường theo khoảng cách còn lại, tránh chạy vượt qua mục tiêu.
        view.Move(direction, runtime.FinalStats.moveSpeed, direction.magnitude);
    }

    public void OnContact(CharacterView view, CharacterRuntimeData runtime, CharacterView other)
    {
        if (other.RuntimeData != null && other.RuntimeData.Team == CharacterTeam.Player)
            characters.Despawn(view);
    }
}
