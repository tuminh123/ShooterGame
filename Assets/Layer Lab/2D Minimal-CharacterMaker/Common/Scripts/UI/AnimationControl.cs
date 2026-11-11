using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Controls character animation playback buttons. Populates animation button labels
    /// from the skeleton's animation list and handles selection highlighting.
    /// </summary>
    public class AnimationControl : MonoBehaviour
    {
        [SerializeField] private Button[] buttonAnimations;
        [SerializeField] private Sprite[] spriteBgs;
        [SerializeField] private string defaultAnimationName = "Idle";
        private PartsManager _partsManager;
        private string[] _animNames;
        private int _selectedIndex = -1;

        /// <summary>
        /// Initializes the animation panel with the given <see cref="PartsManager"/>.
        /// Populates button labels from available animation names and auto-selects the default animation.
        /// </summary>
        /// <param name="pm">The PartsManager that provides animation data and playback control.</param>
        public void Init(PartsManager pm)
        {
            _partsManager = pm;
            var clips = new System.Collections.Generic.List<string>(pm.GetAnimationNames());
            string[] fishing = { "Fishing_Throwing", "Fishing_Idle", "Fishing_Real", "Fishing_Catch" };
            var ordered = clips.FindAll(name => System.Array.IndexOf(fishing, name) < 0);
            foreach (string name in fishing)
                if (clips.Contains(name)) ordered.Add(name);
            _animNames = ordered.ToArray();

            if (buttonAnimations == null || buttonAnimations.Length == 0)
                buttonAnimations = GetComponentsInChildren<Button>();

            for (int i = 0; i < buttonAnimations.Length; i++)
            {
                if (buttonAnimations[i] == null) continue;

                if (i < _animNames.Length)
                {
                    var tmp = buttonAnimations[i].GetComponentInChildren<TMP_Text>();
                    if (tmp != null)
                        tmp.text = ButtonLabel(_animNames[i]);
                    else
                    {
                        var text = buttonAnimations[i].GetComponentInChildren<Text>();
                        if (text != null) text.text = ButtonLabel(_animNames[i]);
                    }
                }

                int index = i;
                buttonAnimations[i].onClick.AddListener(() => OnClickAnimation(index));
            }

            // Play the default animation on startup
            int idleIndex = System.Array.FindIndex(_animNames, n => n.Equals(defaultAnimationName, System.StringComparison.OrdinalIgnoreCase));
            if (idleIndex < 0) idleIndex = 0;
            OnClickAnimation(idleIndex);
        }

        private static string ButtonLabel(string clip)
        {
            if (clip == "Fishing_Real") return "Fishing Reel";
            return clip.Replace('_', ' ');
        }

        private void OnClickAnimation(int index)
        {
            if (_partsManager == null || _animNames == null) return;
            if (index >= _animNames.Length) return;

            _partsManager.PlayAnimation(_animNames[index]);
            SetSelectedButton(index);
        }

        private void SetSelectedButton(int index)
        {
            if (spriteBgs == null || spriteBgs.Length < 2) return;

            // Deselect the previous selection
            if (_selectedIndex >= 0 && _selectedIndex < buttonAnimations.Length && buttonAnimations[_selectedIndex] != null)
                buttonAnimations[_selectedIndex].image.sprite = spriteBgs[0];

            // Apply the new selection
            if (index >= 0 && index < buttonAnimations.Length && buttonAnimations[index] != null)
                buttonAnimations[index].image.sprite = spriteBgs[1];

            _selectedIndex = index;
        }

        private void OnDestroy()
        {
            if (buttonAnimations == null) return;
            foreach (var btn in buttonAnimations)
            {
                if (btn != null) btn.onClick.RemoveAllListeners();
            }
        }
    }
}
