using UnityEngine;

namespace LayerLab.ArtMakerUnity
{
    /// <summary>
    /// Receives animation events from Unity Animator clips and forwards them
    /// as C# events for external listeners.
    /// </summary>
    public class AnimationEventReceiver : MonoBehaviour
    {
        /// <summary>Raised at the airborne projectile release timing.</summary>
        public event System.Action OnProjectileReleaseEvent;

        public void OnProjectileRelease()
        {
            OnProjectileReleaseEvent?.Invoke();
        }

        /// <summary>
        /// Raised when the attack hit animation event is triggered.
        /// </summary>
        public event System.Action OnAttackHitEvent;

        /// <summary>
        /// Raised when the skill hit animation event is triggered.
        /// </summary>
        public event System.Action OnSkillHitEvent;

        /// <summary>
        /// Called by the Animator when the attack hit event fires.
        /// </summary>
        public void OnAttackHit()
        {
            OnAttackHitEvent?.Invoke();
        }

        /// <summary>
        /// Called by the Animator when the skill hit event fires.
        /// </summary>
        public void OnSkillHit()
        {
            OnSkillHitEvent?.Invoke();
        }
    }
}