/// <summary>
/// Nơi tập trung các chuỗi cố định dùng chung trong game, chia theo từng nhóm chức năng.
/// Lưu ý: Khi thêm giá trị mới, đặt vào nhóm phù hợp để tránh viết chuỗi trực tiếp ở nhiều script.
/// </summary>
public static class GameConstants
{
    /// <summary>Tên layer phải khớp ProjectSettings/TagManager; prefab vỏ được gán lại theo phe mỗi lần spawn.</summary>
    public static class PhysicsLayers
    {
        /// <summary>Layer của phe Player.</summary>
        public const string Player = "Player";
        /// <summary>Layer của phe Enemy.</summary>
        public const string Enemy = "Enemy";
    }

    /// <summary>Tên file và key Easy Save dành riêng cho tiến trình người chơi.</summary>
    public static class SaveKeys
    {
        #region Player Save Storage

        /// <summary>File trong persistentDataPath, có thể đổi từ GameSceneInstaller.</summary>
        public const string PlayerFile = "PlayerSave.es3";
        /// <summary>Legacy schema 2/3; chỉ SaveMigration đọc.</summary>
        public const string PlayerData = "player_data";
        /// <summary>Section lưu phiên bản định dạng save, dùng để quyết định có cần migration hay không.</summary>
        public const string Format = "save_format";
        /// <summary>Section lưu nhân vật đang chọn.</summary>
        public const string PlayerSelection = "player_selection";
        /// <summary>Section lưu level/sao vĩnh viễn của từng nhân vật.</summary>
        public const string CharacterLevels = "character_levels";
        /// <summary>Section lưu ID trang bị đang mặc.</summary>
        public const string Equipment = "equipment";
        /// <summary>Section lưu số dư các loại tiền.</summary>
        public const string Currencies = "currencies";
        /// <summary>Section lưu tiến trình vượt màn và thưởng thắng lần đầu.</summary>
        public const string StageProgress = "stage_progress";

        #endregion
    }

    /// <summary>
    /// Key tra sprite trong MapDatabaseSO; Level Data dùng các hằng số này khi chọn map.
    /// Lưu ý: Giá trị phải khớp key trong database; đổi tên hằng số không tự đổi dữ liệu asset.
    /// </summary>
    public static class MapKeys
    {

        #region Map Sprite Keys

        /// <summary>Nền đất cỏ.</summary>
        public const string Grass = "grass";
        /// <summary>Nền đá sân đền cổ.</summary>
        public const string Temple = "temple";
        /// <summary>Nền đất cát khô.</summary>
        public const string Desert = "desert";
        /// <summary>Nền đất rừng tối.</summary>
        public const string Forest = "forest";
        /// <summary>Nền đất núi lửa.</summary>
        public const string Volcanic = "volcanic";

        #endregion

    }
}
