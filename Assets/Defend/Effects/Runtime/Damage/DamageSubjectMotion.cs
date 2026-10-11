using UnityEngine;

namespace Defend.PhaseFiveEffects
{
    /// <summary>Owns a reversible preview pose on one art transform; newer requests supersede older owners.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class DamageSubjectMotion : MonoBehaviour
    {
        private uint generation;
        private bool ownsPose;
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private Vector3 originalScale;

        public uint Acquire()
        {
            Restore();
            unchecked { generation++; }
            if (generation == 0) generation = 1;
            originalPosition = transform.localPosition;
            originalRotation = transform.localRotation;
            originalScale = transform.localScale;
            ownsPose = true;
            return generation;
        }

        public bool Apply(uint owner, Vector3 offset, Quaternion rotation, Vector3 scale)
        {
            if (!ownsPose || owner != generation || !isActiveAndEnabled) return false;
            transform.localPosition = originalPosition + originalRotation * offset;
            transform.localRotation = originalRotation * rotation;
            transform.localScale = Vector3.Scale(originalScale, scale);
            return true;
        }

        public void Release(uint owner)
        {
            if (ownsPose && owner == generation) Restore();
        }

        private void Restore()
        {
            if (!ownsPose) return;
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
            transform.localScale = originalScale;
            ownsPose = false;
        }

        private void OnDisable()
        {
            Restore();
            unchecked { generation++; }
        }

        private void OnDestroy() { Restore(); }
    }
}
