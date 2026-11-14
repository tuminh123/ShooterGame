using System;
using UnityEditor;
using UnityEngine;

// Chỉ các kiểu DataSO của dự án đăng ký editor này; không can thiệp asset của plugin hoặc Unity.
public abstract class ProjectDataSOEditor : Editor
{
    #region Inspector Lifecycle

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawSections();
        serializedObject.ApplyModifiedProperties();
        foreach (var item in targets) DataInspectorGUI.Validate(() => ValidateData(item));
        using (new EditorGUI.DisabledScope(targets.Length != 1))
        {
            if (target is EnemyFormationSO formation && GUILayout.Button("Open Formation Preview in Scene"))
                EnemyFormationWorkbench.Open(formation);
            if (target is EnemyPathSO path && GUILayout.Button("Open Path Preview in Scene"))
                EnemyFormationWorkbench.Open(path);
        }
    }

    private void Field(string path, string label = null) => DataInspectorGUI.Field(serializedObject, path, label);
    private void Section(string title, string description, DataInspectorTone tone, params string[] fields)
        => DataInspectorGUI.DrawSection(title, description, () => { foreach (var field in fields) Field(field); }, tone);

    #endregion

    #region Data Sections

    private void DrawSections()
    {
        if (target is CharacterDataSO)
        {
            Section("CHARACTER INFO", "Nhận diện template. Character Type ghép database, cấu hình cấp và save; tên hiển thị có thể đổi độc lập.",
                DataInspectorTone.Info, "displayName", "characterType", "team", "isBoss");
            Section("VISUAL / HITBOX / HEALTH BAR", "Hình ảnh, hitbox và thanh máu có kích thước riêng. Mở công cụ để căn chỉnh trực tiếp trong Scene.",
                DataInspectorTone.Visual, "visualSettings");
            if (GUILayout.Button("Mở công cụ chỉnh Visual / Hitbox")) HitboxEditorWindow.Open();
            Section("BASE STATS", "Chỉ số cấp 1 trước bonus. Defense là giáp tiêu hao; các tỷ lệ dùng thang 100 = 100%.",
                DataInspectorTone.Stats, "baseStats");
            Section("INNATE SKILLS", "Skill vốn có của nhân vật. Cấp ban đầu đọc từ từng SkillDataSO; skill nhận trong màn nằm ở Skill Database.",
                DataInspectorTone.Advanced, "skills");
        }
        else if (target is CharacterDatabaseSO)
            Section("CHARACTER CATALOG", "Mỗi Character Type chỉ khai báo một template. Bao gồm Player, Enemy thường và Boss; không để reference trống.",
                DataInspectorTone.Info, "characters");
        else if (target is CharacterLevelConfigSO)
        {
            Section("CHARACTER GROWTH", "Cấp 1 chưa có bonus; cấp L nhận Bonus Per Level × (L - 1), giới hạn ở Max Level.",
                DataInspectorTone.Stats, "characterType", "maxLevel", "bonusPerLevel");
            Section("PERMANENT UPGRADE COST", "Chi phí = làm tròn lên(Initial Cost × Multiplier^(cấp hiện tại - 1)). Chỉ dùng nâng vĩnh viễn, không phải EXP trận.",
                DataInspectorTone.Rewards, "initialUpgradeCost", "upgradeCostMultiplier");
        }
        else if (target is RunLevelConfigSO)
        {
            Section("RUN LEVEL GROWTH", "Cấp và bonus trong một lượt chơi. Bonus cố định này cộng thêm bên cạnh phần thưởng skill được chọn.",
                DataInspectorTone.Stats, "maxLevel", "bonusPerLevel");
            Section("EXPERIENCE FORMULA", "EXP từ cấp L lên L+1 = Initial Experience + Increase Per Level × (L - 1). Đây là EXP cho từng lần lên cấp.",
                DataInspectorTone.Rewards, "initialExperience", "experienceIncreasePerLevel");
        }
        else if (target is LevelDatabaseSO)
        {
            Section("CHARACTER LEVEL CONFIGS", "Ghép cấu hình tăng trưởng bằng Character Type, không dùng vị trí trong danh sách. Mỗi type chỉ có một config.",
                DataInspectorTone.Stats, "characterLevels");
            Section("SHARED RUN LEVELS", "Công thức EXP và bonus cấp trong trận dùng chung cho Player; tiến trình runtime được reset theo lượt chơi.",
                DataInspectorTone.Rewards, "runLevelConfig");
        }
        else if (target is StageDatabaseSO)
        {
            Section("STAGE CATALOG", "Toàn bộ StageSO của game, sắp xếp theo thứ tự hiển thị. StageId phải duy nhất để lưu tiến trình đúng màn.",
                DataInspectorTone.Spawn, "stages");
            Section("DEFAULT STAGE", "Màn được StageManager chọn khi khởi tạo. Để trống dùng màn đầu danh sách; màn được gán phải thuộc catalog.",
                DataInspectorTone.Info, "defaultStage");
        }
        else if (target is EquipmentDatabaseSO)
            Section("EQUIPMENT CATALOG", "ID được lưu trong save và phải ổn định, duy nhất. Bonus cộng thẳng; tỷ lệ dùng điểm phần trăm.",
                DataInspectorTone.Stats, "items");
        else if (target is CombatAssetsSO)
        {
            Section("SHARED VIEW SHELLS", "Vỏ dùng chung cho đạn và VFX; mỗi loại có visual con riêng, được ghép khi spawn.",
                DataInspectorTone.Info, "bulletPrefab", "effectPrefab");
            Section("BULLET VISUALS & HITBOXES", "Mỗi Bullet Type xuất hiện một lần. Hit Radius/Offset dùng world unit và không theo scale hình ảnh.",
                DataInspectorTone.Visual, "bullets");
            Section("IMPACT / EFFECT VISUALS", "VFX độc lập với hiệu ứng gameplay như độc/cháy. Prefab cần IEffectVisual trên root để báo hoàn tất.",
                DataInspectorTone.Visual, "effects");
        }
        else if (target is LootDatabaseSO)
        {
            Section("PICKUP SHELL & RANGE", "Một vỏ dùng cho mọi loại loot. Bán kính nhặt tính từ Player, theo world unit.",
                DataInspectorTone.Info, "pickupPrefab", "pickupRadius");
            Section("DROP TABLE", "Xác suất theo thang 0..1: 0.25 = 25%. Amount là lượng thưởng của một vật phẩm; mỗi loại khai báo một lần.",
                DataInspectorTone.Rewards, "drops");
        }
        else if (target is SkillDatabaseSO)
            Section("GAMEPLAY SKILL CATALOG", "Chỉ dùng skill nguồn Gameplay. Bật Available In Run Choices để skill được roll ở bảng thưởng lên cấp.",
                DataInspectorTone.Advanced, "skills");
        else if (target is MapDatabaseSO)
            Section("MAP CATALOG", "Key phân biệt hoa/thường và phải khớp Map Key của Stage. Sprite chỉ đổi hình nền; kích thước chunk cấu hình ở manager nền.",
                DataInspectorTone.Visual, "maps");
        else if (target is PoolPrewarmSettingsSO) DrawPrewarm();
        else if (target is EnemyFormationSO) DrawFormation();
        else if (target is EnemyPathSO) DrawPath();
        else if (target is BossSpawnDataSO) DrawSpecialSpawns();
        else if (target is StatusEffectDataSO) DrawLegacyEffect();
    }

    #endregion

    #region Pool Preparation

    private void DrawPrewarm()
    {
        Section("LOADING BATCH", "Số object tạo mỗi batch trước khi nhường frame cho Loading cập nhật tiến độ.",
            DataInspectorTone.Info, "objectsPerFrame");
        Section("SHARED GAMEOBJECT SHELLS", "Số object chuẩn bị trước trận, không phải giới hạn spawn. Bullets và Effects còn chuẩn bị visual cho từng loại.",
            DataInspectorTone.Info, "characterShells", "bullets", "effectShells", "pickups");
        Section("VISUALS PER CHARACTER TEMPLATE", "Số clone tính riêng cho mỗi template. Nhiều template dùng chung prefab sẽ cộng số lượng cần chuẩn bị.",
            DataInspectorTone.Visual, "playerVisuals", "enemyVisuals", "bossVisuals");
        Section("MANAGED RUNTIME POOLS", "Dữ liệu C# thuần được chuẩn bị riêng với GameObject. Tăng theo số nhân vật/hiệu ứng đồng thời dự kiến.",
            DataInspectorTone.Advanced, "characterRuntimes", "slowRuntimes", "damageOverTimeRuntimes");
        Section("ADDITIONAL PREFABS", "Bổ sung vùng skill, VFX hoặc object thế giới. Count = 0 để bỏ qua entry; Count > 0 cần prefab hợp lệ.",
            DataInspectorTone.Visual, "additionalPrefabs");
    }

    #endregion

    #region Formation & Path Geometry

    private void DrawFormation()
    {
        using (DataInspectorGUI.Section("FORMATION GEOMETRY", "Đặc/Rỗng là cách bố trí hình học, độc lập với cả khối Path, tuần tự Path hoặc Area tại Player.", DataInspectorTone.Visual))
            DataInspectorGUI.FormationShapeFields(serializedObject);
        var shape = serializedObject.FindProperty("shape");
        if (shape.hasMultipleDifferentValues) return;
        bool custom = shape.intValue == (int)FormationType.Custom;
        if (custom)
            Section("CUSTOM POINTS & SYMMETRY", "Điểm quanh tâm (0,0), theo world unit. Đối xứng tự sinh điểm còn thiếu; điểm trùng trên trục được gộp.",
                DataInspectorTone.Advanced, "customPoints", "symmetry", "customScale");
        else
            Section("LEVEL & WORLD SIZE", "Số quái tự tính từ Shape và Level. Rỗng Level 1: tròn/tam giác 3, vuông/thoi 4, lục giác 6; mỗi cấp sau gấp đôi. Size chỉ giãn hình.",
                DataInspectorTone.Stats, "level", "size");
        Section("ROTATION", "Xoay hình quanh tâm theo độ; không tự xoay sprite từng thành viên.", DataInspectorTone.Visual, "rotation");
        if (!serializedObject.isEditingMultipleObjects)
            DataInspectorGUI.Validate(() => EditorGUILayout.HelpBox("Số slot khi dùng Formation: " + ((EnemyFormationSO)target).SlotCount, MessageType.Info));
    }

    private void DrawPath()
    {
        using (DataInspectorGUI.Section("CAMERA-RELATIVE CENTER", "Path luôn bám camera theo world X/Y; Center Offset chỉ được dùng khi bật Use Custom Center.", DataInspectorTone.Info))
        {
            Field("useCustomCenter");
            var custom = serializedObject.FindProperty("useCustomCenter");
            if (custom.hasMultipleDifferentValues || custom.boolValue) Field("centerOffset");
        }
        Section("WAYPOINTS: A → B", "Thứ tự danh sách là đường di chuyển. Điểm đầu A là nơi spawn; tối thiểu 2 điểm, không để hai điểm liên tiếp trùng nhau.",
            DataInspectorTone.Spawn, "waypoints");
        using (DataInspectorGUI.Section("CURVE SAMPLING", "Tắt Smooth để nối thẳng; bật để đường cong đi qua waypoint. Nhiều mẫu hơn làm đường lấy mẫu mịn hơn.", DataInspectorTone.Advanced))
        {
            Field("smooth");
            var smooth = serializedObject.FindProperty("smooth");
            if (smooth.hasMultipleDifferentValues || smooth.boolValue) Field("samplesPerSegment");
        }
    }

    #endregion

    #region Special Spawns & Legacy Effects

    private void DrawSpecialSpawns()
    {
        using (DataInspectorGUI.Section("ELITE / BOSS ENTRIES", "Elite dùng mốc giây từ đầu Stage. Boss dùng event wave; toàn Stage có tối đa một Boss.", DataInspectorTone.Spawn))
        {
            var entries = serializedObject.FindProperty("entries");
            // Dùng list chuẩn của Unity cho sửa nhiều asset; tránh áp loại của asset đầu lên các asset khác.
            if (serializedObject.isEditingMultipleObjects) Field("entries");
            else
            {
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("ENTRY " + (i + 1), EditorStyles.boldLabel);
                            if (GUILayout.Button("Xóa", GUILayout.Width(50))) { entries.DeleteArrayElementAtIndex(i); break; }
                        }
                        string prefix = entry.propertyPath + ".";
                        Field(prefix + "type"); Field(prefix + "character"); Field(prefix + "enemyLevel");
                        if (entry.FindPropertyRelative("type").enumValueIndex == (int)SpecialEnemyType.Elite)
                        {
                            Field(prefix + "eliteStageTime", "Xuất hiện sau (giây)");
                            Field(prefix + "eliteScale"); Field(prefix + "eliteHealthMultiplier");
                        }
                    }
                }
                if (GUILayout.Button("Thêm Elite / Boss"))
                {
                    entries.arraySize++;
                    var added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
                    added.FindPropertyRelative("type").enumValueIndex = (int)SpecialEnemyType.Elite;
                    added.FindPropertyRelative("character").objectReferenceValue = null;
                    added.FindPropertyRelative("enemyLevel").intValue = 1;
                    added.FindPropertyRelative("eliteStageTime").floatValue = 0f;
                    added.FindPropertyRelative("eliteScale").floatValue = 1.5f;
                    added.FindPropertyRelative("eliteHealthMultiplier").floatValue = 2f;
                }
            }
        }
        Section("BOSS TRIGGER & ARENA", "Boss cần CharacterData có Is Boss bật và một WaveConfig trong Stage. Arena Size là chiều rộng/cao world unit.",
            DataInspectorTone.Advanced, "bossAppearanceCondition", "arenaSize");
    }

    private void DrawLegacyEffect()
    {
        EditorGUILayout.HelpBox("Asset hiệu ứng SO cũ được giữ để bảo toàn dữ liệu. Skill hiện tại dùng effect nhúng trong Behaviour Data; chỉnh asset này không tự đổi cấu hình effect nhúng.", MessageType.Info);
        Section("LEGACY EFFECT SETTINGS", "Thời hạn theo giây gameplay; xác suất ở đây dùng thang 0..100, khác xác suất loot 0..1.",
            DataInspectorTone.Legacy, "duration", "applicationChance", "useEffectStats");
        if (target is SlowEffectDataSO)
            Section("SLOW", "Tỷ lệ giảm tốc theo thang 0..1: 0.3 = giảm 30% tốc độ.", DataInspectorTone.Stats, "slowFraction");
        if (target is DamageOverTimeEffectDataSO)
            Section("PERIODIC DAMAGE", "Cấu hình cũ dùng hệ số sát thương mỗi nhịp, không phải damage cố định của effect nhúng mới.",
                DataInspectorTone.Stats, "tickInterval", "damageMultiplierPerTick");
    }

    #endregion

    #region Read-Only Configuration Checks

    private static void ValidateData(UnityEngine.Object data)
    {
        // Chỉ gọi hàm đọc/kiểm tra hiện có, không sửa giá trị hoặc tạo runtime trong Inspector.
        if (data is CharacterDataSO character) { character.ValidateStats(); character.VisualSettings.Validate(); }
        else if (data is CharacterDatabaseSO characters) characters.CreateLookup();
        else if (data is CharacterLevelConfigSO growth) growth.GetTotalBonus(1);
        else if (data is RunLevelConfigSO run) run.GetTotalBonus(1);
        else if (data is LevelDatabaseSO levels) levels.CreateTypeLookup();
        else if (data is EquipmentDatabaseSO equipment) equipment.CreateBonusLookup();
        else if (data is CombatAssetsSO combat) combat.Validate();
        else if (data is LootDatabaseSO loot) loot.CreateLookup();
        else if (data is SkillDatabaseSO skills) skills.Validate();
        else if (data is StageDatabaseSO stages) stages.Validate();
        else if (data is PoolPrewarmSettingsSO pools) pools.Validate();
        else if (data is EnemyFormationSO formation) formation.Validate();
        else if (data is EnemyPathSO path) path.Validate();
        else if (data is BossSpawnDataSO bosses) bosses.Validate();
        else if (data is StatusEffectDataSO effect) effect.Validate();
        else if (data is MapDatabaseSO)
        {
            var maps = new SerializedObject(data).FindProperty("maps");
            var keys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < maps.arraySize; i++)
            {
                var entry = maps.GetArrayElementAtIndex(i);
                string key = entry.FindPropertyRelative("key").stringValue;
                if (string.IsNullOrWhiteSpace(key) || !keys.Add(key) || entry.FindPropertyRelative("sprite").objectReferenceValue == null)
                    throw new InvalidOperationException("Map " + (i + 1) + ": cần key duy nhất, không trống và có sprite nền.");
            }
        }
    }

    #endregion
}

#region Project-Only Editor Registration

[CustomEditor(typeof(CharacterDataSO)), CanEditMultipleObjects] public sealed class CharacterDataInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(CharacterDatabaseSO)), CanEditMultipleObjects] public sealed class CharacterDatabaseInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(CharacterLevelConfigSO)), CanEditMultipleObjects] public sealed class CharacterLevelInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(RunLevelConfigSO)), CanEditMultipleObjects] public sealed class RunLevelInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(LevelDatabaseSO)), CanEditMultipleObjects] public sealed class LevelDatabaseInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(EquipmentDatabaseSO)), CanEditMultipleObjects] public sealed class EquipmentDatabaseInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(CombatAssetsSO)), CanEditMultipleObjects] public sealed class CombatAssetsInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(LootDatabaseSO)), CanEditMultipleObjects] public sealed class LootDatabaseInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(SkillDatabaseSO)), CanEditMultipleObjects] public sealed class SkillDatabaseInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(StageDatabaseSO)), CanEditMultipleObjects] public sealed class StageDatabaseInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(MapDatabaseSO)), CanEditMultipleObjects] public sealed class MapDatabaseInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(PoolPrewarmSettingsSO)), CanEditMultipleObjects] public sealed class PoolPrewarmInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(EnemyFormationSO)), CanEditMultipleObjects] public sealed class EnemyFormationInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(EnemyPathSO)), CanEditMultipleObjects] public sealed class EnemyPathInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(BossSpawnDataSO)), CanEditMultipleObjects] public sealed class BossSpawnInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(StatusEffectDataSO), true), CanEditMultipleObjects] public sealed class StatusEffectInspector : ProjectDataSOEditor { }

#endregion
