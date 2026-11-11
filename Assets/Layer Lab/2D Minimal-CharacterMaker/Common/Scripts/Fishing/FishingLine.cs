using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace LayerLab.ArtMakerUnity
{
    // Giữ thông tin migration cho serializer, không yêu cầu API Updater sửa assembly của project.
    [MovedFrom(false, sourceNamespace: "", sourceAssembly: "LayerLab.ArtMakerUnity", sourceClassName: "FishingLine")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [DefaultExecutionOrder(100)]
    public sealed class FishingLine : MonoBehaviour
    {
        [SerializeField] Transform endPoint;
        [SerializeField] Vector2 rodTip = new Vector2(0.60f, -0.08f);
        [SerializeField, Min(0.001f)] float width = 0.045f;
        [SerializeField, Range(0f, 0.5f)] float sag = 0.25f;
        readonly Vector3[] points = new Vector3[33];
        [SerializeField] Color color = new Color(0.88f, 0.92f, 0.94f, 0.9f);

        [SerializeField] Material lineMaterial;

        static readonly int Throwing = Animator.StringToHash("Fishing_Throwing");
        static readonly int Waiting = Animator.StringToHash("Fishing_Idle");
        static readonly int Reeling = Animator.StringToHash("Fishing_Real");
        static readonly int Catching = Animator.StringToHash("Fishing_Catch");

        Animator animator;
        SpriteRenderer rod;
        LineRenderer line;

        void CreateLine()
        {
            rod = GetComponent<SpriteRenderer>();
            animator = GetComponentInParent<Animator>();
            var child = new GameObject("Fishing Line");
            child.hideFlags = HideFlags.HideAndDontSave;
            child.layer = gameObject.layer;
            child.transform.SetParent(transform, false);
            line = child.AddComponent<LineRenderer>();
            line.enabled = false;
            if (lineMaterial) line.sharedMaterial = lineMaterial;
            line.useWorldSpace = true;
            line.positionCount = points.Length;
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            line.numCapVertices = 2;
        }

        void LateUpdate()
        {
            if (!line) CreateLine();
            if (!endPoint || !endPoint.gameObject.activeInHierarchy)
            {
                line.enabled = false;
                return;
            }
            float extension = GetExtension(out bool casting);
            line.enabled = extension > 0f && rod.enabled && rod.sprite && !rod.forceRenderingOff;
            if (!line.enabled) return;

            UpdateCurve(rod, extension, casting);
            line.SetPositions(points);
            Vector3 characterScale = animator ? animator.transform.lossyScale : transform.lossyScale;
            float planarScale = (Mathf.Abs(characterScale.x) + Mathf.Abs(characterScale.y)) * 0.5f;
            line.widthMultiplier = width * planarScale;
            line.startColor = line.endColor = color;
            line.sortingLayerID = rod.sortingLayerID;
            line.sortingOrder = rod.sortingOrder - 1;
        }

        float GetExtension(out bool casting)
        {
            casting = false;
            int state;
            float time;
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
            {
                if (!UnityEditor.AnimationMode.IsPropertyAnimated(transform, "m_LocalPosition.x")) return 0f;
                var window = System.Array.Find(Resources.FindObjectsOfTypeAll<UnityEditor.AnimationWindow>(), w => w.previewing);
                if (!window || !window.previewing || !window.animationClip || window.animationClip.length <= 0f) return 0f;
                state = Animator.StringToHash(window.animationClip.name);
                time = window.time / window.animationClip.length;
            }
            else
#endif
            {
                if (!animator || !animator.isActiveAndEnabled || !animator.isInitialized || animator.layerCount == 0) return 0f;
                var info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                state = info.shortNameHash;
                time = info.loop ? Mathf.Repeat(info.normalizedTime, 1f) : Mathf.Clamp01(info.normalizedTime);
            }
            if (state == Waiting || state == Reeling) return 1f;
            if (state == Catching) return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.075f, 0.575f, time));
            casting = state == Throwing;
            return casting ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.82f, time)) : 0f;
        }

        void UpdateCurve(SpriteRenderer renderer, float extension, bool casting)
        {
            Vector3 start = TipPosition(renderer);
            Vector3 end = Vector3.Lerp(start, endPoint.position, extension);
            if (casting)
                end.y += Mathf.Abs(endPoint.position.x - start.x) * 0.2f * 4f * extension * (1f - extension);
            float drop = Mathf.Abs(end.x - start.x) * sag;
            for (int i = 0; i < points.Length; i++)
            {
                float t = (float)i / (points.Length - 1);
                points[i] = Vector3.Lerp(start, end, t) + Vector3.down * (4f * drop * t * (1f - t));
            }
        }

        Vector3 TipPosition(SpriteRenderer renderer)
        {
            Vector2 tip = rodTip;
            if (renderer.flipX) tip.x = -tip.x;
            if (renderer.flipY) tip.y = -tip.y;
            return transform.TransformPoint(tip);
        }

        void OnDisable()
        {
            if (!line) return;
            var child = line.gameObject;
            line.enabled = false;
            line = null;
#if UNITY_EDITOR
            if (!Application.IsPlaying(gameObject))
            {
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (child) DestroyImmediate(child);
                };
                return;
            }
#endif
            Destroy(child);
        }
    }
}
