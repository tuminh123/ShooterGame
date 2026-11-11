using UnityEngine;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Main demo controller that initializes and orchestrates all UI panels,
    /// the player, camera, and color systems on scene start.
    /// </summary>
    public class DemoControl : MonoBehaviour
    {
        private const string PathDiscord = "https://discord.gg/qCsVSHHcY7";
        private const string PathFacebook = "https://www.facebook.com/layerlab";
        private const string PathYoutube = "https://www.youtube.com/@LayerlabGameAssets";
        private const string PathAssetStore = "https://assetstore.unity.com/publishers/5232";

        [Header("Core")] [SerializeField] private Player player;
        [SerializeField] private CameraControl cameraControl;

        [Header("UI Panels")] [SerializeField] private AnimationControl animationControl;
        [SerializeField] private PanelPartsControl panelPartsControl;
        [SerializeField] private PanelPartsListControl panelPartsListControl;
        [SerializeField] private ColorPicker colorPicker;
        [SerializeField] private ColorPresetManager colorPresetManager;
        [SerializeField] private ColorFavoriteManager colorFavoriteManager;

        [Header("Preset")]
        [Tooltip("ScriptableObject used for preset slots 1-10 of PanelPartsControl. Falls back to the PanelPartsControl inspector value if left empty.")]
        [SerializeField] private PresetData equipmentPresetData;
        [Tooltip("Index of the preset slot (0-9) to auto-apply on game start. Set to -1 to disable auto-apply.")]
        [SerializeField] private int startupPresetSlot = 0;

        [Header("Randomize")]
        [Tooltip("Forces the helmet to always be equipped when RandomizeCharacter() is called. By default, RandomizeAll unequips the Helmet with 50% probability.")]
        [SerializeField] private bool forceHelmet;

        /// <summary>
        /// The Player instance managed by this demo controller.
        /// </summary>
        public Player Player => player;

        private void Start()
        {
            player.Init();
            if (cameraControl != null) cameraControl.Init(player.transform);
            if (colorPicker != null) colorPicker.Init(player.PartsManager);
            if (colorPresetManager != null) colorPresetManager.Init(player.PartsManager);
            if (colorFavoriteManager != null) colorFavoriteManager.Init();
            if (panelPartsListControl != null) panelPartsListControl.Init(player.PartsManager);
            if (panelPartsControl != null)
                panelPartsControl.Init(player.PartsManager, panelPartsListControl, equipmentPresetData, startupPresetSlot);
            if (animationControl != null) animationControl.Init(player.PartsManager);
        }

        /// <summary>
        /// Randomizes all character parts and colors (Skin, Hair, Eye), then refreshes the UI.
        /// </summary>
        public void RandomizeCharacter()
        {
            var pm = player.PartsManager;
            pm.RandomizeAll();
            colorPresetManager.SetRandomColor(ColorTargetType.Skin);
            colorPresetManager.SetRandomColor(ColorTargetType.Hair);
            colorPresetManager.SetRandomColor(ColorTargetType.Beard);

            // RandomizeAll unequips the Helmet with 50% probability. When the inspector
            // toggle is enabled, force the helmet to remain equipped.
            if (forceHelmet)
            {
                int helmetCount = pm.GetPartsCount(PartsType.Helmet);
                if (helmetCount > 0)
                {
                    int idx = pm.GetActiveIndex(PartsType.Helmet);
                    if (idx < 0) idx = Random.Range(0, helmetCount);
                    pm.EquipParts(PartsType.Helmet, idx);
                    pm.ToggleParts(PartsType.Helmet, true);
                }
            }

            // Refresh the parts list of the currently selected category
            if (panelPartsControl != null)
                panelPartsControl.RefreshCurrentSlot();
        }


        public void OnClickDiscord()
        {
            Application.OpenURL(PathDiscord);
        }

        public void OnClickFacebook()
        {
            Application.OpenURL(PathFacebook);
        }

        public void OnClickYoutube()
        {
            Application.OpenURL(PathYoutube);
        }

        public void OnClickAssetstore()
        {
            Application.OpenURL(PathAssetStore);
        }

        /// <summary>
        /// Resets the character to the initial state (PartsManager.Init() output).
        /// Does not change PanelPartsControl preset index.
        /// </summary>
        public void OnClickResetAll()
        {
            if (player == null) return;
            var pm = player.PartsManager;
            if (pm == null) return;
            pm.ResetAll();
            if (panelPartsControl != null)
                panelPartsControl.RefreshCurrentSlot();
        }
    }
}