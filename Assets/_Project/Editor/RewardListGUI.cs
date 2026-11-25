using System;
using UnityEditor;
using UnityEngine;

/// <summary>Vẽ danh sách thưởng lắp từ nhiều <see cref="IReward"/>; dùng chung cho Mission và Stage.</summary>
internal static class RewardListGUI
{
    internal static void Draw(SerializedProperty list)
    {
        Type[] types = StrategySelector.GetTypes<IReward>();
        for (int i = 0; i < list.arraySize; i++)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                if (GUILayout.Button("Xóa", GUILayout.Width(50))) { list.DeleteArrayElementAtIndex(i); break; }
                StrategySelector.Draw(list.GetArrayElementAtIndex(i), types, "Reward " + (i + 1));
            }
        }
        if (!GUILayout.Button("Thêm reward")) return;
        list.arraySize++;
        // Unity có thể nhân bản phần tử cuối khi tăng size; luôn tạo một ô chọn mới.
        list.GetArrayElementAtIndex(list.arraySize - 1).managedReferenceValue = null;
    }
}
