using System;
using System.Collections.Generic;
using UnityEngine;

// * Kho sprite nền theo key; dữ liệu level tự quyết định key cần dùng.
[CreateAssetMenu(fileName = "MapDatabase", menuName = "Shooter Game/Data/Map Database")]
public sealed class MapDatabaseSO : ScriptableObject
{

    #region Map Data

    // * Một mục dữ liệu hiển thị trong Inspector, không chứa điều kiện mở khóa map.
    [Serializable]
    private sealed class MapEntry
    {
        // ! Key phân biệt chữ hoa/thường; dùng cùng một cách viết trong Level Data.
        public string key;
        public Sprite sprite;  // * Ảnh nền được gán lên SpriteRenderer của chunk.
    }

    [SerializeField] private List<MapEntry> maps = new List<MapEntry>();  // * Thêm mục và kéo sprite vào đây để đăng ký map mới.

    #endregion

    #region Map Lookup

    // * Trả về true và sprite tương ứng nếu có đúng một mục hợp lệ cho key.
    // ! Key rỗng, không tồn tại, bị trùng hoặc thiếu sprite trả về false và sprite = null.
    public bool TryGetMap(string key, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrWhiteSpace(key) || maps == null)
            return false;

        bool found = false;
        foreach (MapEntry entry in maps)
        {
            if (entry == null || !string.Equals(entry.key, key, StringComparison.Ordinal))
                continue;

            if (found)
            {
                sprite = null;
                return false;
            }

            found = true;
            sprite = entry.sprite;
        }

        return sprite != null;
    }

    #endregion

    #region Editor Validation

    // * Unity kiểm tra dữ liệu khi chỉnh asset trong Inspector, không tự thay key hoặc sprite.
    private void OnValidate()
    {
        if (maps == null)
            return;

        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < maps.Count; i++)
        {
            MapEntry entry = maps[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                Debug.LogWarning($"MapDatabase: entry {i} has an empty key.", this);
                continue;
            }

            if (!keys.Add(entry.key))
                Debug.LogWarning($"MapDatabase: duplicate key '{entry.key}'.", this);
            if (entry.sprite == null)
                Debug.LogWarning($"MapDatabase: key '{entry.key}' has no sprite.", this);
        }
    }

    #endregion

}
