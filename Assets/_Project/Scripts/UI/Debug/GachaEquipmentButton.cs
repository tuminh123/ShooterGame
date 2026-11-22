using System;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Nút debug của panel test: mỗi lần bấm nhận một bản của <b>mọi</b> mẫu đồ trong Equipment Database,
/// mỗi bản một độ hiếm ngẫu nhiên, để test túi đồ, set và skill mà không cần nguồn rơi đồ thật.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class GachaEquipmentButton : MonoBehaviour
{
    #region Dependencies And State

    private IEquipmentGrant grant;
    private EquipmentDatabaseSO database;
    private Button button;
    private readonly System.Random random = new System.Random();

    #endregion

    #region Injection And Initialization

    /// <summary>SceneContext inject vai trò nhận đồ và database (để biết đủ các mẫu đồ).</summary>
    [Inject]
    public void Construct(IEquipmentGrant equipmentGrant, EquipmentDatabaseSO equipmentDatabase)
    {
        grant = equipmentGrant;
        database = equipmentDatabase;
    }

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(GrantAll);
    }

    private void OnDestroy() => button.onClick.RemoveListener(GrantAll);

    #endregion

    #region Gacha

    private void GrantAll()
    {
        if (grant == null || database == null)
            return;
        int rarityCount = Enum.GetValues(typeof(EquipmentRarity)).Length;
        int unsaved = 0;
        foreach (EquipmentDataSO item in database.Items)
        {
            var rarity = (EquipmentRarity)random.Next(rarityCount);
            grant.Grant(item, rarity, out bool saved);
            if (!saved)
                unsaved++;
        }
        Debug.Log($"[Test] Gacha: nhận {database.Items.Count} món, độ hiếm ngẫu nhiên.", this);
        if (unsaved > 0)
            Debug.LogWarning($"[Test] Gacha: {unsaved} món chưa ghi được file; SaveManager sẽ thử ghi lại.", this);
    }

    #endregion
}
