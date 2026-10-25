using System;
using Terresquall;
using UnityEngine;
using Zenject;

// * Điểm cấu hình prefab player, joystick và các dịch vụ class thuần cho scene.
public sealed class GameSceneInstaller : MonoInstaller
{

    #region Scene References

    [SerializeField] private Player playerPrefab;       // * Prefab tạo tại Vector3.zero khi vào game.
    [SerializeField] private VirtualJoystick joystick;  // * Joystick trên Canvas, inject cho Player được tạo.
    [SerializeField] private string playerSaveFile = GameConstants.SaveKeys.PlayerFile;  // * Tên file save người chơi; không chứa dữ liệu của lượt chơi.

    #endregion

    #region Gameplay Databases

    [SerializeField] private CharacterDatabaseSO characterDatabase;  // * Danh mục template nhân vật, dùng để dựng dictionary theo ID.
    [SerializeField] private LevelDatabaseSO levelDatabase;          // * Config vĩnh viễn theo nhân vật và config trận dùng chung.
    [SerializeField] private EquipmentDatabaseSO equipmentDatabase;  // * Tra bonus từ ID trang bị đã lưu.

    #endregion

    #region Service Bindings

    // * Một instance mỗi dịch vụ; Zenject quản lý Initialize, LateTick và Dispose.
    public override void InstallBindings()
    {
        if (playerPrefab == null || joystick == null)
            throw new InvalidOperationException("Assign Player Prefab and Joystick in GameSceneInstaller.");
        if (characterDatabase == null || levelDatabase == null)
            throw new InvalidOperationException("Assign Character Database and Level Database in GameSceneInstaller.");
        if (equipmentDatabase == null)
            throw new InvalidOperationException("Assign Equipment Database in GameSceneInstaller.");
        SignalBusInstaller.Install(Container);
        Container.BindInstance(new SaveManager.Settings(playerSaveFile));
        Container.BindInterfacesAndSelfTo<SaveManager>().AsSingle().NonLazy();
        Container.Bind<CharacterDatabaseSO>().FromInstance(characterDatabase).AsSingle();
        Container.Bind<LevelDatabaseSO>().FromInstance(levelDatabase).AsSingle();
        Container.Bind<EquipmentDatabaseSO>().FromInstance(equipmentDatabase).AsSingle();
        Container.BindInterfacesAndSelfTo<PlayerEquipmentService>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<CharacterLevelService>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<RunLevelService>().AsSingle().NonLazy();
        Container.Bind<LevelManager>().AsSingle().NonLazy();
        Container.BindInstance(new PlayerManager.Settings(playerPrefab));
        Container.Bind<VirtualJoystick>().FromInstance(joystick).AsSingle();
        Container.BindInterfacesAndSelfTo<PlayerManager>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<PoolManager>().AsSingle().NonLazy();
        Container.BindInterfacesAndSelfTo<MovementManager>().AsSingle().NonLazy();
        Container.BindInitializableExecutionOrder<PlayerManager>(-100);
        Container.BindInitializableExecutionOrder<SaveManager>(-200);
        // * Zenject dispose theo priority giảm dần: thu hồi pool trước, buffer Jobs sau cùng.
        Container.BindDisposableExecutionOrder<PoolManager>(100);
        Container.BindDisposableExecutionOrder<PlayerManager>(50);
        Container.BindDisposableExecutionOrder<SaveManager>(-50);
        Container.BindDisposableExecutionOrder<MovementManager>(-100);
    }

    #endregion

}
