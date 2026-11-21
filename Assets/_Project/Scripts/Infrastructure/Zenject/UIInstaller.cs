using System;
using Zenject;

/// <summary>
/// Installer riêng của tầng UI: bind dịch vụ UI tự sở hữu (<c>UI/UIContracts.cs</c>) và thứ tự frame của UI.
/// Chạy sau <see cref="GameSceneInstaller"/> trong danh sách installer của SceneContext.
/// </summary>
/// <remarks>
/// UI là tầng lá: không miền gameplay nào dùng binding ở đây. Panel UI là MonoBehaviour trong scene nên được
/// SceneContext inject tự động; chỉ dịch vụ dùng chung và controller cần chạy theo frame mới được bind.
/// Role gameplay mà UI dùng (IGameState, IRunReader, ISkillChoice…) do Manager tự bind qua
/// <c>BindInterfacesTo</c> trong GameSceneInstaller.
/// </remarks>
public sealed class UIInstaller : MonoInstaller
{
    #region Lifecycle Order

    // UI chạy SAU mọi Manager (danh sách LateTickOrder của GameSceneInstaller dùng số 10..60):
    // đọc trạng thái cuối frame, ví dụ mở bảng chọn skill sau khi RunManager đã xử lý loot/lên cấp.
    private const int FirstUiOrder = 1000;

    // Đọc từ trên xuống = thứ tự chạy trong nhóm UI.
    private static readonly Type[] LateTickOrder =
    {
        typeof(SkillChoiceController), // Hoãn roll/mở bảng thưởng ra khỏi callback gameplay; chờ hết slow motion.
        typeof(GameplayController),    // Banner cảnh báo spawn theo StageTime; kiểm tra popup pause đã đóng.
    };

    #endregion

    #region Installation

    /// <summary>Bind dịch vụ UI dùng chung và cho Zenject tick các controller UI theo thứ tự ở trên.</summary>
    public override void InstallBindings()
    {
        // Chuyển màn có overlay và token pause; HomeController/PauseUI dùng qua IScreenTransition.
        Container.BindInterfacesTo<ScreenTransitionController>().FromComponentInHierarchy().AsSingle();

        // Controller là MonoBehaviour trong scene: chỉ lộ ILateTickable để Zenject gọi LateTick, không tự đăng ký tick.
        Container.BindInterfacesTo<SkillChoiceController>().FromComponentInHierarchy().AsSingle();
        Container.BindInterfacesTo<GameplayController>().FromComponentInHierarchy().AsSingle();
        for (int i = 0; i < LateTickOrder.Length; i++)
            Container.BindLateTickableExecutionOrder(LateTickOrder[i], FirstUiOrder + i * 10);
    }

    #endregion
}
