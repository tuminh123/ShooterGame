using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace LayerLab.ArtMakerUnity
{
    [MovedFrom(true, sourceNamespace: "", sourceAssembly: "LayerLab.ArtMakerUnity", sourceClassName: "FishingEquip")]
    [DisallowMultipleComponent]
    public sealed class FishingEquip : MonoBehaviour
    {
        static readonly HashSet<int> FishingStates = new HashSet<int>
        {
            Animator.StringToHash("Fishing_Throwing"), Animator.StringToHash("Fishing_Idle"),
            Animator.StringToHash("Fishing_Real"), Animator.StringToHash("Fishing_Catch")
        };

        Animator animator;
        Transform rightHand;
        Transform leftHand;
        Transform rod;
        Transform fishingPoint;
        Transform pickaxe;
        Transform shownTool;
        public event System.Action ShownToolChanged;
        public bool IsRodShown => shownTool && shownTool == rod;
        public bool IsPickaxeShown => shownTool && shownTool == pickaxe;
        bool rodAvailable = true;
        bool pickaxeAvailable = true;
        bool mining;
        public enum Preview { Combat, Rod, Pickaxe }
        Preview preview;
        bool manualOverride, wasFishing, wasMining;

        public void SelectPreview(Preview choice)
        {
            preview = choice;
            manualOverride = true;
            RefreshTool();
        }

        public void ResetPreview()
        {
            preview = Preview.Combat;
            manualOverride = false;
        }

        // Set by the interaction that begins or ends mining.
        public void SetMining(bool active) => mining = active;

        public void SetToolAvailable(bool isRod, bool available)
        {
            RestoreEquipment();
            if (isRod) rodAvailable = available;
            else pickaxeAvailable = available;
        }
        readonly Dictionary<GameObject, bool> previous = new Dictionary<GameObject, bool>();

        void Awake()
        {
            Transform rider = transform.Find("Rider");
            if (!rider) rider = transform;
            animator = rider.GetComponent<Animator>();
            rightHand = rider.Find("HandRight");
            leftHand = rider.Find("HandLeft");
            pickaxe = rider.Find("HandRight/Tool_Pick");
            rod = rider.Find("HandRight/Tool_Fishing");
            fishingPoint = rider.Find("HandRight/Tool_Fishing_Point");
        }

        void LateUpdate() => RefreshTool();

        void RefreshTool()
        {
            bool fishing = rod && animator && animator.isActiveAndEnabled && animator.isInitialized && animator.layerCount > 0
                && (FishingStates.Contains(animator.GetCurrentAnimatorStateInfo(0).shortNameHash)
                    || (animator.IsInTransition(0) && FishingStates.Contains(animator.GetNextAnimatorStateInfo(0).shortNameHash)));
            if (fishing != wasFishing || mining != wasMining)
            {
                ResetPreview();
                wasFishing = fishing;
                wasMining = mining;
            }
            bool useRod = manualOverride ? preview == Preview.Rod : fishing;
            bool usePick = manualOverride ? preview == Preview.Pickaxe : mining;
            Transform desired = useRod && rodAvailable ? rod : usePick && pickaxeAvailable ? pickaxe : null;
            if (shownTool == desired) return;
            RestoreEquipment();
            if (desired) ShowTool(desired);
        }

        void ShowTool(Transform tool)
        {
            shownTool = tool;
            SetVisible(rightHand, true);
            foreach (Transform slot in rightHand)
            {
                if (slot != fishingPoint) SetVisible(slot, slot == tool);
            }
            if (leftHand) SetVisible(leftHand, false);
            ShownToolChanged?.Invoke();
        }

        void SetVisible(Transform slot, bool visible)
        {
            previous.Add(slot.gameObject, slot.gameObject.activeSelf);
            slot.gameObject.SetActive(visible);
        }

        // 랜덤 선택기가 양손 슬롯을 새로 적용한 직후 호출한다.
        public void EquipmentChanged()
        {
            if (previous.Count == 0) return;
            previous.Clear();
            ShowTool(shownTool);
        }

        void OnDisable() => RestoreEquipment();

        public void RestoreEquipment()
        {
            if (previous.Count == 0) return;
            shownTool = null;
            foreach (var slot in previous)
                if (slot.Key) slot.Key.SetActive(slot.Value);
            previous.Clear();
            ShownToolChanged?.Invoke();
        }
    }

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(FishingEquip))]
    internal sealed class FishingEquipEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            UnityEditor.EditorGUILayout.HelpBox(
                "Equips the fishing rod while fishing and restores the previous equipment afterward.",
                UnityEditor.MessageType.Info);
        }
    }
#endif
}
