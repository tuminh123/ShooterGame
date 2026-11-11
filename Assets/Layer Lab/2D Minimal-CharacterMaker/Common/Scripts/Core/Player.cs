using UnityEngine;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Singleton player controller that holds the PartsManager for character customization.
    /// </summary>
    public class Player : MonoBehaviour
    {
        private const string ANIM_IDLE = "Idle";

        /// <summary>
        /// Singleton instance of the Player.
        /// </summary>
        public static Player Instance { get; private set; }

        [SerializeField] private PartsManager partsManager;

        /// <summary>
        /// The PartsManager component used for character customization.
        /// </summary>
        public PartsManager PartsManager => partsManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Initializes the player by setting up the PartsManager and playing the Idle animation.
        /// </summary>
        public void Init()
        {
            if (partsManager != null)
            {
                partsManager.Init();
                partsManager.PlayAnimation(ANIM_IDLE);
            }
        }
    }
}
