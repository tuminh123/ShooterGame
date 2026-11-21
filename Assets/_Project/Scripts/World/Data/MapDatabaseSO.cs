using System;
using System.Collections.Generic;
using UnityEngine;

// Kho sprite nền theo key; dữ liệu level tự quyết định key cần dùng.
[CreateAssetMenu(fileName = "MapDatabase", menuName = "Shooter Game/Data/Map Database")]
public sealed class MapDatabaseSO : ScriptableObject
{

    #region Map Data

    // Một mục dữ liệu hiển thị trong Inspector, không chứa điều kiện mở khóa map.
    [Serializable]
    private sealed class MapEntry
    {
        // Lưu ý: Key phân biệt chữ hoa/thường; dùng cùng một cách viết trong Level Data.
        [Tooltip("Khóa map phân biệt hoa/thường; phải khớp Map Key trong StageDataSO. Không để trống hoặc trùng.")]
        [SerializeField] private string key;
        [Tooltip("Sprite nền được gán cho các chunk của map. Kích thước lưới do WorldManager.Settings (Chunk Size) quyết định.")]
        [SerializeField] private Sprite sprite;

        /// <summary>Khóa map phân biệt hoa/thường; phải khớp Map Key trong StageDataSO. Không để trống hoặc trùng.</summary>
        public string Key => key;
        /// <summary>Sprite nền được gán cho các chunk của map. Kích thước lưới do WorldManager.Settings (Chunk Size) quyết định.</summary>
        public Sprite Sprite => sprite;
    }

    [Tooltip("Danh mục key → sprite nền. Mỗi key chỉ xuất hiện một lần và phải có sprite.")]
    [SerializeField] private List<MapEntry> maps = new List<MapEntry>();

    #endregion

    #region Map Lookup

    // Trả về true và sprite tương ứng nếu có đúng một mục hợp lệ cho key.
    // Lưu ý: Key rỗng, không tồn tại, bị trùng hoặc thiếu sprite trả về false và sprite = null.
    public bool TryGetMap(string key, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrWhiteSpace(key) || maps == null)
            return false;

        bool found = false;
        foreach (MapEntry entry in maps)
        {
            if (entry == null || !string.Equals(entry.Key, key, StringComparison.Ordinal))
                continue;

            if (found)
            {
                sprite = null;
                return false;
            }

            found = true;
            sprite = entry.Sprite;
        }

        return sprite != null;
    }

    #endregion

    #region Editor Validation

    // Unity kiểm tra dữ liệu khi chỉnh asset trong Inspector, không tự thay key hoặc sprite.
    private void OnValidate()
    {
        if (maps == null)
            return;

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < maps.Count; i++)
        {
            MapEntry entry = maps[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
            {
                Debug.LogWarning($"MapDatabase: entry {i} has an empty key.", this);
                continue;
            }

            if (!keys.Add(entry.Key))
                Debug.LogWarning($"MapDatabase: duplicate key '{entry.Key}'.", this);
            if (entry.Sprite == null)
                Debug.LogWarning($"MapDatabase: key '{entry.Key}' has no sprite.", this);
        }
    }

    #endregion

}
