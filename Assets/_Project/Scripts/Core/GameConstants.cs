// * Nơi tập trung các chuỗi cố định dùng chung trong game, chia theo từng nhóm chức năng.
// ! Khi thêm giá trị mới, đặt vào nhóm phù hợp để tránh viết chuỗi trực tiếp ở nhiều script.
public static class GameConstants
{
    // * Tên layer phải khớp ProjectSettings/TagManager; prefab vỏ được gán lại theo phe mỗi lần spawn.
    public static class PhysicsLayers
    {
        public const string Player = "Player";
        public const string Enemy = "Enemy";
    }

    // * Tên file và key Easy Save dành riêng cho tiến trình người chơi.
    public static class SaveKeys
    {
        #region Player Save Storage

        public const string PlayerFile = "PlayerSave.es3";  // * File trong persistentDataPath, có thể đổi từ GameSceneInstaller.
        public const string PlayerData = "player_data";     // * Key chứa một PlayerSaveData; đổi key cần migration dữ liệu cũ.

        #endregion
    }

    // * Key tra sprite trong MapDatabaseSO; Level Data dùng các hằng số này khi chọn map.
    // ! Giá trị phải khớp key trong database; đổi tên hằng số không tự đổi dữ liệu asset.
    public static class MapKeys
    {

        #region Map Sprite Keys

        public const string Grass = "grass";        // * Nền đất cỏ.
        public const string Temple = "temple";      // * Nền đá sân đền cổ.
        public const string Desert = "desert";      // * Nền đất cát khô.
        public const string Forest = "forest";      // * Nền đất rừng tối.
        public const string Volcanic = "volcanic";  // * Nền đất núi lửa.

        #endregion

    }
}
