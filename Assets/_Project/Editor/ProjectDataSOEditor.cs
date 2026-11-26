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
            Section("CHARACTER SCREEN (UI)", "Ảnh thẻ Collection và nhân vật dạng UI cho màn Detail. Character UI Exporter tự xuất và gán hai field này.",
                DataInspectorTone.Visual, "rarity", "portrait", "uiVisualPrefab");
            if (GUILayout.Button("Mở Character UI Exporter")) CharacterUIExporter.Open();
            Section("BASE STATS", "Chỉ số cấp 1 trước bonus. Defense là giáp tiêu hao; các tỷ lệ dùng thang 100 = 100%.",
                DataInspectorTone.Stats, "baseStats");
            Section("SKILLS", "MỌI skill của nhân vật, mỗi skill một lần. Skill không có bước mở khóa bên dưới là skill có sẵn; cấp ban đầu đọc từ từng SkillDataSO. Danh sách rỗng thì màn Character không có ô skill.",
                DataInspectorTone.Advanced, "skills");
            Section("SKILL PROGRESSION (PLAYER)", "Mỗi bước chọn một skill trong SKILLS (dropdown): skill đạt Skill Level khi thỏa điều kiện. Character Level ≥ N chỉ dùng để mở khóa (Skill Level = 1); Stars ≥ N mở hoặc nâng cấp, một sao có thể nâng nhiều skill. Cấp skill = cấp cao nhất trong các bước đã thỏa. Enemy bỏ qua phần này.",
                DataInspectorTone.Advanced, "skillProgression");
            Section("STAR STATS (PLAYER)", "Chỉ số dự phòng của sao 1..5 (phần tử 0 = sao 1), chỉ có tác dụng khi sao đó không có bước skill nào. Cộng thẳng vào bonus vĩnh viễn; Value = 0 là không có.",
                DataInspectorTone.Stats, "starStats");
            if (GUILayout.Button("Random chỉ số sao (sao chưa có skill)"))
                RandomizeStarStats();
            DrawCharacterWarnings();
        }
        else if (target is CharacterDatabaseSO)
            Section("CHARACTER CATALOG", "Mỗi Character Type chỉ khai báo một template. Bao gồm Player, Enemy thường và Boss; không để reference trống.",
                DataInspectorTone.Info, "characters");
        else if (target is CharacterLevelConfigSO)
        {
            Section("CHARACTER GROWTH", "Cấp 1 chưa có bonus; cấp L nhận Bonus Per Level × (L - 1), giới hạn ở Max Level.",
                DataInspectorTone.Stats, "characterType", "maxLevel", "bonusPerLevel");
            Section("PERMANENT UPGRADE CLICKS", "Mỗi mốc chia đều cho Clicks Per Level lần bấm: giá mỗi lần = Gold cần cho mốc ÷ số lần (làm tròn lên), lần cuối chỉ tốn phần còn thiếu. Gold cần cho mỗi mốc (độ dài thanh) đặt chung theo độ hiếm trong Level Database → PERMANENT LEVEL COST. Chỉ dùng cho Player.",
                DataInspectorTone.Rewards, "clicksPerLevel");
        }
        else if (target is InBattleLevelConfigSO)
        {
            Section("RUN LEVEL GROWTH", "Cấp và bonus trong một lượt chơi. Bonus cố định này cộng thêm bên cạnh phần thưởng skill được chọn.",
                DataInspectorTone.Stats, "maxLevel", "bonusPerLevel");
            using (DataInspectorGUI.Section("EXPERIENCE FORMULA", "EXP cần cho từng mốc Lv → Lv+1 trong trận. Chọn kiểu công thức rồi chỉnh tham số; bảng bên dưới là EXP của các mốc.", DataInspectorTone.Rewards))
            {
                LevelFormulaField("experienceFormula", "Experience Formula");
                if (!serializedObject.isEditingMultipleObjects)
                {
                    var battle = (InBattleLevelConfigSO)target;
                    DrawFormulaPreview(battle.ExperienceFormula, battle.MaxLevel);
                }
            }
        }
        else if (target is LevelDatabaseSO)
        {
            Section("CHARACTER LEVEL CONFIGS", "Ghép cấu hình tăng trưởng bằng Character Type, không dùng vị trí trong danh sách. Mỗi type chỉ có một config.",
                DataInspectorTone.Stats, "characterLevels");
            using (DataInspectorGUI.Section("PERMANENT LEVEL COST", "Gold cần cho mỗi mốc level vĩnh viễn Lv → Lv+1 = công thức chung × hệ số theo độ hiếm của nhân vật (Character Data → Rarity), làm tròn lên. Chung mọi nhân vật Player; hệ số phải tăng dần (nhân vật hiếm hơn tốn hơn).", DataInspectorTone.Rewards))
            {
                Field("permanentLevelCurrency");
                RarityFormulaFields<CharacterRarity>("permanentLevelCost");
                if (!serializedObject.isEditingMultipleObjects)
                    DrawRarityPreview(((LevelDatabaseSO)target).PermanentLevelCost, PreviewMaxLevel);
            }
            using (DataInspectorGUI.Section("CHARACTER COLLECTION", "Mảnh theo độ hiếm, chung mọi nhân vật cùng độ hiếm. Unlock Shards = mảnh để ghép nhân vật chưa có; Shards Per Copy = mảnh nhận khi phân rã một bản trùng (cũng là giá trị bản trùng khi nâng sao). Star Cost: mảnh để mở sao thứ n (bảng dưới: mốc Lv n → n+1 = sao n). Starter = sở hữu sẵn.", DataInspectorTone.Rewards))
            {
                Field("starterCharacters");
                Field("collectionRules");
                EditorGUILayout.LabelField("Star Cost (mảnh)", EditorStyles.boldLabel);
                RarityFormulaFields<CharacterRarity>("starCost");
                if (!serializedObject.isEditingMultipleObjects)
                    DrawRarityPreview(((LevelDatabaseSO)target).StarCost, CharacterLevelConfigSO.MaxStars + 1);
            }
            Section("SHARED RUN LEVELS", "Công thức EXP và bonus cấp trong trận dùng chung cho Player; tiến trình runtime được reset theo lượt chơi.",
                DataInspectorTone.Rewards, "inBattleLevelConfig");
        }
        else if (target is StageDatabaseSO)
        {
            Section("STAGE CATALOG", "Toàn bộ StageSO của game, sắp xếp theo thứ tự hiển thị. StageId phải duy nhất để lưu tiến trình đúng màn.",
                DataInspectorTone.Spawn, "stages");
            Section("DEFAULT STAGE", "Màn được StageManager chọn khi khởi tạo. Để trống dùng màn đầu danh sách; màn được gán phải thuộc catalog.",
                DataInspectorTone.Info, "defaultStage");
        }
        else if (target is EquipmentDatabaseSO)
        {
            Section("EQUIPMENT RULES", "Luật dùng chung: chỉ số chính theo ô × độ hiếm, dòng phụ, level tối đa, giá nâng.",
                DataInspectorTone.Stats, "rules");
            Section("EQUIPMENT CATALOG", "Mọi mẫu đồ của game. ID được lưu trong save: đổi hoặc xóa ID thì bản đồ đã lưu bị xóa khỏi túi khi load.",
                DataInspectorTone.Info, "items");
            Section("EQUIPMENT SETS", "Mỗi set (trừ None) một mục: skill duy nhất (nguồn Equipment, 1 cấp) bật khi mặc đủ Required Pieces món cùng set.",
                DataInspectorTone.Advanced, "sets");
            Section("STARTER ITEMS", "Đồ nhận một lần khi save chưa từng nhận (save mới hoặc sau Reset). Dòng phụ được roll lúc nhận.",
                DataInspectorTone.Rewards, "starterItems");
        }
        else if (target is EquipmentDataSO)
        {
            Section("EQUIPMENT INFO", "ID ổn định được lưu trong save; tên, mô tả và icon chỉ để hiển thị.",
                DataInspectorTone.Info, "id", "displayName", "description", "icon");
            Section("SLOT & SET", "Ô quyết định chỉ số chính (xem Equipment Rules). Set = None nếu món không thuộc set nào.",
                DataInspectorTone.Stats, "slot", "set");
            Section("SKILL", "Một skill, một cấp (definition nguồn Equipment). Có skill khi món đạt Skill Unlock Level; để trống nếu không có.",
                DataInspectorTone.Advanced, "skill", "skillUnlockLevel");
        }
        else if (target is EquipmentRulesSO)
        {
            Section("MAIN STAT BY SLOT", "Mỗi ô một chỉ số chính. Giá trị ở Lv.L = Base Value + Per Level × (L - 1), theo từng độ hiếm.",
                DataInspectorTone.Stats, "slots");
            Section("SUBSTATS", "Số dòng phụ ban đầu theo độ hiếm. Mốc thứ k (Lv = k × Interval): ít hơn k dòng thì mở dòng mới, đủ rồi thì cộng một lần roll vào một dòng ngẫu nhiên.",
                DataInspectorTone.Advanced, "rarities", "maxLevel", "milestoneInterval", "maxSubstats", "substatRolls");
            using (DataInspectorGUI.Section("LEVEL UP COST", "Giá Gold mỗi mốc Lv → Lv+1 = công thức chung × hệ số độ hiếm của món, làm tròn lên. Hệ số phải tăng dần (món hiếm hơn tốn hơn). Thiếu tiền thì nút Level Up rung và báo.", DataInspectorTone.Rewards))
            {
                Field("levelUpCurrency");
                RarityFormulaFields<EquipmentRarity>("levelUpCost");
                if (!serializedObject.isEditingMultipleObjects)
                {
                    var rules = (EquipmentRulesSO)target;
                    DrawRarityPreview(rules.LevelUpCost, rules.MaxLevel);
                }
            }
        }
        else if (target is CombatAssetsSO)
        {
            Section("SHARED VIEW SHELLS", "Vỏ dùng chung cho đạn và VFX; mỗi loại có visual con riêng, được ghép khi spawn.",
                DataInspectorTone.Info, "bulletPrefab", "effectPrefab");
            Section("BULLET VISUALS & HITBOXES", "Mỗi Bullet Type xuất hiện một lần. Hit Radius/Offset dùng world unit và không theo scale hình ảnh.",
                DataInspectorTone.Visual, "bullets");
            Section("IMPACT / EFFECT VISUALS", "VFX độc lập với hiệu ứng gameplay như độc/cháy. Prefab cần IEffectVisual trên root để CombatVfx biết khi nào phát xong.",
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
            Section("GAMEPLAY SKILL CATALOG", "Chỉ dùng skill nguồn Gameplay. Bật Available In Battle Choices để skill được roll ở bảng thưởng lên cấp.",
                DataInspectorTone.Advanced, "skills");
        else if (target is MapDatabaseSO)
            Section("MAP CATALOG", "Key phân biệt hoa/thường và phải khớp Map Key của Stage. Sprite chỉ đổi hình nền; kích thước chunk cấu hình ở manager nền.",
                DataInspectorTone.Visual, "maps");
        else if (target is MissionDatabaseSO) DrawMissionDatabase();
        else if (target is MissionDataSO) DrawMission();
        else if (target is LoginCheckInCalendarSO) DrawLoginCheckIn();
        else if (target is ShopPackSO) DrawShopPack();
        else if (target is GachaBannerSO) DrawGachaBanner();
        else if (target is PoolPrewarmSettingsSO) DrawPrewarm();
        else if (target is EnemyFormationSO) DrawFormation();
        else if (target is EnemyPathSO) DrawPath();
        else if (target is BossSpawnDataSO) DrawSpecialSpawns();
    }

    #endregion

    #region Level Formulas

    // Mốc hiện trong bảng xem trước: vài mốc đầu, giữa và mốc cuối trước Max Level, để thấy được độ dốc của đường giá.
    private static readonly int[] PreviewLevels = { 1, 2, 3, 5, 10, 20, 50, 100 };
    private const float PreviewLevelWidth = 80f;
    // Bảng giá nhân vật dùng chung cho mọi Max Level (mỗi nhân vật một trần riêng), nên xem trước tới Lv100.
    private const int PreviewMaxLevel = 101;

    /// <summary>
    /// Vẽ một field <see cref="ILevelFormula"/> (lưu bằng SerializeReference): danh sách chọn kiểu công thức
    /// rồi các tham số của đúng công thức đó.
    /// </summary>
    private void LevelFormulaField(string path, string label)
    {
        SerializedProperty formula = serializedObject.FindProperty(path);
        // Sửa nhiều asset cùng lúc mà mỗi asset chọn kiểu khác nhau thì không gộp được: dùng field chuẩn của Unity.
        if (serializedObject.isEditingMultipleObjects) { EditorGUILayout.PropertyField(formula, new GUIContent(label), true); return; }
        StrategySelector.Draw(formula, StrategySelector.GetTypes<ILevelFormula>(), label);
    }

    /// <summary>
    /// Vẽ một field <see cref="RarityScaledFormula{TRarity}"/>: công thức chung + một ô hệ số cho mỗi độ hiếm (ghi đúng tên độ hiếm).
    /// Số ô hệ số luôn khớp số độ hiếm; ô mới thêm (khi enum có thêm độ hiếm) chép giá trị ô cuối để vẫn tăng dần.
    /// </summary>
    private void RarityFormulaFields<TRarity>(string path) where TRarity : struct, Enum
    {
        LevelFormulaField(path + ".formula", "Formula");
        SerializedProperty multipliers = serializedObject.FindProperty(path + ".rarityMultipliers");
        string[] rarities = Enum.GetNames(typeof(TRarity));
        if (!serializedObject.isEditingMultipleObjects && multipliers.arraySize != rarities.Length)
            multipliers.arraySize = rarities.Length;
        EditorGUILayout.LabelField("Rarity Multipliers", EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            for (int i = 0; i < multipliers.arraySize && i < rarities.Length; i++)
                EditorGUILayout.PropertyField(multipliers.GetArrayElementAtIndex(i), new GUIContent(rarities[i] + " ×"));
        }
    }

    /// <summary>Bảng xem trước: mỗi hàng một mốc (Lv → Lv+1), mỗi cột một độ hiếm. Cấu hình lỗi thì không vẽ (khung lỗi đã báo).</summary>
    private static void DrawRarityPreview<TRarity>(RarityScaledFormula<TRarity> formula, int maxLevel) where TRarity : struct, Enum
    {
        if (formula == null) return;
        string[] rarities = Enum.GetNames(typeof(TRarity));
        try
        {
            formula.Validate();
            EditorGUILayout.LabelField("Preview (lượng cần cho mốc)", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Mốc", EditorStyles.miniBoldLabel, GUILayout.Width(PreviewLevelWidth));
                foreach (string rarity in rarities) EditorGUILayout.LabelField(rarity, EditorStyles.miniBoldLabel);
            }
            foreach (int level in PreviewLevels)
            {
                if (level >= maxLevel) break;
                DrawPreviewRow(level, rarities.Length, i => formula.Evaluate(level, (TRarity)Enum.ToObject(typeof(TRarity), i)));
            }
            // Mốc cuối (Max Level − 1 → Max Level) luôn hiện, để thấy giá lớn nhất.
            if (maxLevel > 1 && Array.IndexOf(PreviewLevels, maxLevel - 1) < 0)
                DrawPreviewRow(maxLevel - 1, rarities.Length, i => formula.Evaluate(maxLevel - 1, (TRarity)Enum.ToObject(typeof(TRarity), i)));
        }
        catch (Exception) { /* Lỗi cấu hình đã hiện ở khung kiểm tra phía dưới Inspector. */ }
    }

    /// <summary>Bảng xem trước của một công thức không có độ hiếm (EXP trong trận).</summary>
    private static void DrawFormulaPreview(ILevelFormula formula, int maxLevel)
    {
        if (formula == null) return;
        try
        {
            formula.Validate();
            EditorGUILayout.LabelField("Preview (lượng cần cho mốc)", EditorStyles.boldLabel);
            foreach (int level in PreviewLevels)
            {
                if (level >= maxLevel) break;
                DrawPreviewRow(level, 1, _ => formula.Evaluate(level));
            }
            if (maxLevel > 1 && Array.IndexOf(PreviewLevels, maxLevel - 1) < 0)
                DrawPreviewRow(maxLevel - 1, 1, _ => formula.Evaluate(maxLevel - 1));
        }
        catch (Exception) { /* Lỗi cấu hình đã hiện ở khung kiểm tra phía dưới Inspector. */ }
    }

    private static void DrawPreviewRow(int level, int columns, Func<int, long> evaluate)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Lv" + level + " → " + (level + 1), GUILayout.Width(PreviewLevelWidth));
            for (int i = 0; i < columns; i++)
                EditorGUILayout.LabelField(evaluate(i).ToString("N0", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    #endregion

    #region Character Stars

    // Điền chỉ số dự phòng cho các sao chưa có skill: chỉ số ngẫu nhiên trong các chỉ số gốc > 0 của nhân vật,
    // giá trị 5–10% chỉ số gốc đó. Kết quả ghi vào asset (Undo được), không random lúc chơi.
    private void RandomizeStarStats()
    {
        foreach (UnityEngine.Object item in targets)
        {
            var character = (CharacterDataSO)item;
            var data = new SerializedObject(character);
            SerializedProperty stars = data.FindProperty("starStats");
            stars.arraySize = CharacterLevelConfigSO.MaxStars;
            BaseStats baseStats = character.BaseStats;
            var pool = new System.Collections.Generic.List<StatType>();
            foreach (StatType stat in (StatType[])Enum.GetValues(typeof(StatType)))
            {
                if (baseStats.Get(stat) > 0f)
                    pool.Add(stat);
            }
            for (int star = 1; star <= CharacterLevelConfigSO.MaxStars && pool.Count > 0; star++)
            {
                if (character.HasStarSkillReward(star))
                    continue;
                StatType stat = pool[UnityEngine.Random.Range(0, pool.Count)];
                float value = baseStats.Get(stat) * UnityEngine.Random.Range(0.05f, 0.10f);
                // Chỉ số lớn làm tròn số nguyên (tối thiểu 1), chỉ số nhỏ như MoveSpeed giữ hai chữ số thập phân.
                value = baseStats.Get(stat) >= 10f ? Mathf.Max(1f, Mathf.Round(value)) : Mathf.Max(0.01f, Mathf.Round(value * 100f) / 100f);
                SerializedProperty entry = stars.GetArrayElementAtIndex(star - 1);
                entry.FindPropertyRelative("stat").intValue = (int)stat;
                entry.FindPropertyRelative("value").floatValue = value;
            }
            data.ApplyModifiedProperties();
        }
    }

    // Cảnh báo (không phải lỗi) cho nhân vật Player: chưa có skill, hoặc sao không có thưởng skill lẫn chỉ số.
    private void DrawCharacterWarnings()
    {
        if (targets.Length != 1)
            return;
        var character = (CharacterDataSO)target;
        if (character.Team != CharacterTeam.Player)
            return;
        if (character.Skills.Count == 0)
            EditorGUILayout.HelpBox("Nhân vật Player chưa có skill nào: màn Character sẽ không có ô skill.", MessageType.Warning);
        var empty = new System.Collections.Generic.List<int>();
        for (int star = 1; star <= CharacterLevelConfigSO.MaxStars; star++)
        {
            if (!character.HasStarSkillReward(star) && !character.TryGetStarStat(star, out _))
                empty.Add(star);
        }
        if (empty.Count > 0)
            EditorGUILayout.HelpBox("Sao " + string.Join(", ", empty) + " chưa có phần thưởng (không có bước skill và Star Stats = 0).", MessageType.Warning);
    }

    #endregion

    #region Missions

    private void DrawMissionDatabase()
    {
        Section("MISSION GROUP", "ID được lưu trong save. Category quyết định kỳ reset: Daily 0 giờ UTC, Weekly thứ Hai 0 giờ UTC, Event theo ngày.\n" +
            "Clock: Device = giờ máy (chỉnh được); Server = giờ server, chống chỉnh giờ, mất mạng thì nhóm bị ẩn.",
            DataInspectorTone.Info, "id", "displayName", "category", "clock");
        SerializedProperty category = serializedObject.FindProperty("category");
        if (category.hasMultipleDifferentValues || category.intValue == (int)MissionCategory.Event)
            Section("EVENT DATES (UTC)", "Dạng yyyy-MM-dd. Ngày End không được tính.", DataInspectorTone.Spawn, "eventStart", "eventEnd");
        Section("MISSIONS", "Thứ tự danh sách là thứ tự trên UI. ID mission không trùng trong cùng database.", DataInspectorTone.Rewards, "missions");
        Section("FINAL MISSION", "Thẻ lớn đầu tab. Mở khi đã nhận hết mission ở trên: Target của Complete Mission phải bằng số mission đó.", DataInspectorTone.Rewards, "finalMission");
    }

    private void DrawMission()
    {
        Section("MISSION INFO", "ID được lưu trong save: đổi ID thì tiến độ đã lưu của mission về 0.",
            DataInspectorTone.Info, "id", "displayName", "description", "icon");
        Section("TASKS", "Mission con, chạy song song, không có thưởng riêng. Xong hết mới nhận được thưởng.", DataInspectorTone.Stats, "tasks");
        using (DataInspectorGUI.Section("REWARDS", "Gói thưởng lắp từ nhiều phần. Nhân vật đã có thì thành mảnh.", DataInspectorTone.Rewards))
        {
            SerializedProperty list = serializedObject.FindProperty("rewards");
            if (serializedObject.isEditingMultipleObjects) { Field("rewards"); return; }
            RewardListGUI.Draw(list);
        }
    }

    #endregion

    #region Login Check-in

    private void DrawLoginCheckIn()
    {
        Section("LOGIN CALENDAR", "ID được lưu trong save. Mỗi ngày nhận một ô kế tiếp; bỏ ngày không mất gì và không quay về ô 1.\n" +
            "Clock: Device = giờ máy (chỉnh được, dùng khi test); Server = chống chỉnh giờ, mất mạng thì lịch bị ẩn.",
            DataInspectorTone.Info, "id", "displayName", "cycle", "clock");
        SerializedProperty cycle = serializedObject.FindProperty("cycle");
        bool mixed = cycle.hasMultipleDifferentValues;
        if (mixed || cycle.intValue == (int)LoginCheckInCycle.Period)
            Section("PERIOD (UTC)", "Chu kỳ Period Days ngày tính từ Period Start (yyyy-MM-dd), chung cho mọi người chơi. Số ô ≤ Period Days.",
                DataInspectorTone.Spawn, "periodDays", "periodStart");
        if (mixed || cycle.intValue == (int)LoginCheckInCycle.Event)
            Section("EVENT DATES (UTC)", "Dạng yyyy-MM-dd, ngày End không được tính. Gia hạn: chỉ sửa End. Số ô ≤ số ngày event.",
                DataInspectorTone.Spawn, "eventStart", "eventEnd");
        using (DataInspectorGUI.Section("DAYS", "Mỗi ô một phần thưởng, theo thứ tự ngày. SevenDay cần đúng 7 ô; Month tối đa 31.", DataInspectorTone.Rewards))
        {
            if (serializedObject.isEditingMultipleObjects) { Field("days"); return; }
            DrawLoginDays(serializedObject.FindProperty("days"));
        }
    }

    private void DrawShopPack()
    {
        Section("SHOP PACK", "ID được lưu trong save và dùng để ghép thẻ trên UI. Icon trống = icon của phần thưởng đầu.\n" +
            "Clock: Device = giờ máy (test); Server = chống chỉnh giờ cho giới hạn Daily/Weekly.",
            DataInspectorTone.Info, "id", "displayName", "icon", "clock");
        using (DataInspectorGUI.Section("PRICE", "CurrencyPrice: vàng/kim cương. StorePrice: tiền thật (hiện giả lập, mua là thành công). FreePrice: miễn phí.",
                   DataInspectorTone.Stats))
        {
            if (serializedObject.isEditingMultipleObjects) Field("price");
            else StrategySelector.Draw(serializedObject.FindProperty("price"), StrategySelector.GetTypes<IShopPrice>(), "Price");
        }
        Section("LIMIT", "Daily reset 0h UTC, Weekly reset thứ Hai 0h UTC. Limit Count = số lần mua mỗi kỳ.",
            DataInspectorTone.Spawn, "limit", "limitCount");
        using (DataInspectorGUI.Section("REWARDS", "Gói thưởng lắp từ nhiều phần. Nhân vật đã có thì thành mảnh.", DataInspectorTone.Rewards))
        {
            if (serializedObject.isEditingMultipleObjects) { Field("rewards"); return; }
            RewardListGUI.Draw(serializedObject.FindProperty("rewards"));
        }
    }

    private void DrawGachaBanner()
    {
        Section("GACHA BANNER", "ID lưu bộ đếm pity trong save; đổi ID thì pity về 0. Icon là ảnh trên thẻ banner.\n" +
            "Chest Closed / Open: rương của màn quay; để trống thì dùng rương mặc định.",
            DataInspectorTone.Info, "id", "displayName", "icon", "chestClosed", "chestOpen");
        Section("PRICE", "Giá x1 và x10 (x10 thường giảm ~10%).", DataInspectorTone.Stats, "currency", "singleCost", "tenCost");
        Section("RATES", "Trọng số từng bậc: xác suất = trọng số / tổng (bậc pool không có bị bỏ). Ví dụ 1 / 5 / 15 / 30 / 49 = 1% / 5% / 15% / 30% / 49%.",
            DataInspectorTone.Rewards, "rates");
        Section("PITY", "Soft pity: từ lượt Soft Pity Start, tỉ lệ bậc pity cộng thêm Soft Pity Step mỗi lượt. Hard pity: lượt Hard Pity chắc chắn ra.\n" +
            "Guarantee: cứ Guarantee Every lượt có ít nhất một món >= Guarantee Tier.",
            DataInspectorTone.Spawn, "pityTier", "softPityStart", "softPityStep", "hardPity", "guaranteeTier", "guaranteeEvery");
        using (DataInspectorGUI.Section("POOL", "CharacterGachaPool: bậc = độ hiếm nhân vật, trùng thành mảnh. EquipmentGachaPool: bậc = độ hiếm món đồ.",
                   DataInspectorTone.Advanced))
        {
            if (serializedObject.isEditingMultipleObjects) { Field("pool"); return; }
            StrategySelector.Draw(serializedObject.FindProperty("pool"), StrategySelector.GetTypes<IGachaPool>(), "Pool");
        }
        // Xem trước kết quả cân bằng: tính từ đúng công thức dùng khi quay.
        var banner = (GachaBannerSO)target;
        if (banner.Pool == null || GachaRoller.TotalWeight(banner) <= 0f) return;
        var preview = new System.Text.StringBuilder();
        foreach (GachaTier tier in (GachaTier[])Enum.GetValues(typeof(GachaTier)))
        {
            float chance = GachaRoller.BaseChance(banner, tier);
            if (chance > 0f) preview.AppendFormat("{0}: {1:0.##}%   ", tier, chance * 100f);
        }
        float average = GachaRoller.AveragePullsForTop(banner);
        if (average > 0f)
            preview.AppendFormat("\nTrung bình {0:0.#} lượt ra {1} (tỉ lệ thực {2:0.##}%).", average, banner.PityTier, 100f / average);
        EditorGUILayout.HelpBox(preview.ToString(), MessageType.Info);
    }

    // Mỗi ô một hộp: tên ngày, nút xóa và bộ chọn kiểu thưởng (SerializeReference).
    private static void DrawLoginDays(SerializedProperty days)
    {
        Type[] types = StrategySelector.GetTypes<IReward>();
        for (int i = 0; i < days.arraySize; i++)
        {
            bool removed = false;
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Day " + (i + 1), EditorStyles.boldLabel);
                    removed = GUILayout.Button("Xóa", GUILayout.Width(50));
                }
                if (!removed) StrategySelector.Draw(days.GetArrayElementAtIndex(i).FindPropertyRelative("reward"), types, "Reward");
            }
            if (!removed) continue;
            days.DeleteArrayElementAtIndex(i);
            break;
        }
        if (!GUILayout.Button("Thêm ngày")) return;
        days.arraySize++;
        // Unity có thể nhân bản phần tử cuối khi tăng size; luôn tạo một ô chọn mới.
        days.GetArrayElementAtIndex(days.arraySize - 1).FindPropertyRelative("reward").managedReferenceValue = null;
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
        using (DataInspectorGUI.Section("FORMATION GEOMETRY", "Đặc/Rỗng là cách bố trí hình học, độc lập với cả khối Path, tuần tự Path hoặc Area quanh Player lúc spawn.", DataInspectorTone.Visual))
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

    #region Special Spawns

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

    #endregion

    #region Read-Only Configuration Checks

    private static void ValidateData(UnityEngine.Object data)
    {
        // Chỉ gọi hàm đọc/kiểm tra hiện có, không sửa giá trị hoặc tạo runtime trong Inspector.
        if (data is CharacterDataSO character)
        {
            character.ValidateStats();
            character.VisualSettings.Validate();
            character.ValidateSkillProgression();
        }
        else if (data is CharacterDatabaseSO characters) characters.CreateLookup();
        else if (data is CharacterLevelConfigSO growth) growth.GetTotalBonus(1);
        else if (data is InBattleLevelConfigSO battle) battle.GetTotalBonus(1);
        else if (data is LevelDatabaseSO levels) levels.CreateTypeLookup();
        else if (data is EquipmentDatabaseSO equipment) equipment.Validate();
        else if (data is EquipmentDataSO equipmentItem) equipmentItem.Validate();
        else if (data is EquipmentRulesSO equipmentRules) equipmentRules.Validate();
        else if (data is CombatAssetsSO combat) combat.Validate();
        else if (data is LootDatabaseSO loot) loot.CreateLookup();
        else if (data is SkillDatabaseSO skills) skills.Validate();
        else if (data is StageDatabaseSO stages) stages.Validate();
        else if (data is MissionDatabaseSO missionDatabase) missionDatabase.Validate();
        else if (data is MissionDataSO mission) mission.Validate();
        else if (data is LoginCheckInCalendarSO loginCalendar) loginCalendar.Validate();
        else if (data is ShopPackSO shopPack) shopPack.Validate();
        else if (data is GachaBannerSO gachaBanner) gachaBanner.Validate();
        else if (data is PoolPrewarmSettingsSO pools) pools.Validate();
        else if (data is EnemyFormationSO formation) formation.Validate();
        else if (data is EnemyPathSO path) path.Validate();
        else if (data is BossSpawnDataSO bosses) bosses.Validate();
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
[CustomEditor(typeof(InBattleLevelConfigSO)), CanEditMultipleObjects] public sealed class InBattleLevelInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(LevelDatabaseSO)), CanEditMultipleObjects] public sealed class LevelDatabaseInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(EquipmentDatabaseSO)), CanEditMultipleObjects] public sealed class EquipmentDatabaseInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(EquipmentDataSO)), CanEditMultipleObjects] public sealed class EquipmentItemInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(EquipmentRulesSO)), CanEditMultipleObjects] public sealed class EquipmentRulesInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(CombatAssetsSO)), CanEditMultipleObjects] public sealed class CombatAssetsInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(LootDatabaseSO)), CanEditMultipleObjects] public sealed class LootDatabaseInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(SkillDatabaseSO)), CanEditMultipleObjects] public sealed class SkillDatabaseInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(StageDatabaseSO)), CanEditMultipleObjects] public sealed class StageDatabaseInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(MissionDatabaseSO)), CanEditMultipleObjects] public sealed class MissionDatabaseInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(MissionDataSO)), CanEditMultipleObjects] public sealed class MissionInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(LoginCheckInCalendarSO)), CanEditMultipleObjects] public sealed class LoginCheckInCalendarInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(ShopPackSO)), CanEditMultipleObjects] public sealed class ShopPackInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(GachaBannerSO)), CanEditMultipleObjects] public sealed class GachaBannerInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(MapDatabaseSO)), CanEditMultipleObjects] public sealed class MapDatabaseInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(PoolPrewarmSettingsSO)), CanEditMultipleObjects] public sealed class PoolPrewarmInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(EnemyFormationSO)), CanEditMultipleObjects] public sealed class EnemyFormationInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(EnemyPathSO)), CanEditMultipleObjects] public sealed class EnemyPathInspector : ProjectDataSOEditor { }
[CustomEditor(typeof(BossSpawnDataSO)), CanEditMultipleObjects] public sealed class BossSpawnInspector : ProjectDataSOEditor { }

#endregion
