#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Editor-only helper that draws OnGUI debug buttons for equipment preset saving and randomization.
    /// Instantiated by <see cref="PanelPartsControl"/> and called from its OnGUI method.
    /// </summary>
    public class EquipmentPresetEditorGUI
    {
        private const int PRESET_SLOT_COUNT = 10;
        private const string PRESET_ASSET_PATH =
            "Assets/Layer Lab/2D Art Maker Unity/AMMinimalGame Character/Data/EquipmentPresetData.asset";

        private readonly PanelPartsControl _panel;

        public EquipmentPresetEditorGUI(PanelPartsControl panel)
        {
            _panel = panel;
        }

        /// <summary>
        /// Draws the equipment preset save buttons in a single horizontal row.
        /// </summary>
        public void DrawGUI()
        {
            float buttonWidth = 55f;
            float buttonHeight = 25f;
            float spacing = 3f;
            float startX = 10f;
            float startY = Screen.height - 60f;

            GUI.Label(new Rect(startX, startY, 200f, 20f), "Equipment Preset Save");
            startY += 22f;

            for (int i = 0; i < PRESET_SLOT_COUNT; i++)
            {
                float x = startX + i * (buttonWidth + spacing);

                bool hasData = false;
                var presetData = _panel.EquipmentPresetData;
                if (presetData != null)
                {
                    var existing = presetData.GetItem(i);
                    hasData = existing != null && !existing.isEmpty;
                }
                string label = hasData ? $"[{i + 1}]" : $"{i + 1}";

                if (GUI.Button(new Rect(x, startY, buttonWidth, buttonHeight), label))
                {
                    SavePresetToSlot(i);
                }
            }
        }

        private void SavePresetToSlot(int slot)
        {
            var pm = _panel.CurrentPartsManager;
            if (pm == null) return;

            EnsurePresetDataAsset();

            var item = pm.ToPresetItem();
            _panel.EquipmentPresetData.SaveItem(slot, item);
            EditorUtility.SetDirty(_panel.EquipmentPresetData);
            AssetDatabase.SaveAssets();
            _panel.CurrentPresetIndex = slot;
            _panel.UpdatePresetDisplay();
            Debug.Log($"[EquipmentPreset] Preset saved to slot {slot + 1}");
        }

        private void EnsurePresetDataAsset()
        {
            if (_panel.EquipmentPresetData != null) return;

            var loaded = AssetDatabase.LoadAssetAtPath<PresetData>(PRESET_ASSET_PATH);
            if (loaded != null)
            {
                _panel.EquipmentPresetData = loaded;
                return;
            }

            string dir = System.IO.Path.GetDirectoryName(PRESET_ASSET_PATH);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                string parent = System.IO.Path.GetDirectoryName(dir);
                string folderName = System.IO.Path.GetFileName(dir);
                AssetDatabase.CreateFolder(parent, folderName);
            }

            var newAsset = ScriptableObject.CreateInstance<PresetData>();
            AssetDatabase.CreateAsset(newAsset, PRESET_ASSET_PATH);
            AssetDatabase.SaveAssets();
            _panel.EquipmentPresetData = newAsset;
            Debug.Log($"[EquipmentPreset] PresetData asset created: {PRESET_ASSET_PATH}");
        }
    }
}
#endif
