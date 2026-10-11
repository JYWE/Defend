using System;
using UnityEngine;

namespace Defend.PhaseFiveEffects.Editor
{
    /// <summary>Small Play Mode checks for overlapping ownership, interruption and destroyed subjects.</summary>
    public static class DamageEffectsValidation
    {
        [Serializable]
        public sealed class MotionResult
        {
            public bool NewOwnerStartsFromOriginalPose;
            public bool OldOwnerCannotMoveNewPose;
            public bool OldOwnerCannotRestoreNewPose;
            public bool LatestOwnerRestoresOriginalPose;
            public bool DisableRestoresOriginalPose;
            public bool DisabledOwnerBecomesInvalid;
            public bool DestroyedSubjectReferenceBecomesNull;
            public bool Passed;
        }

        public static MotionResult CheckMotionOwnership()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Run damage motion validation in Play Mode to exercise OnDisable.");
            var result = new MotionResult();
            var subject = new GameObject("TemporaryDamageMotionValidation");
            Transform target = subject.transform;
            Vector3 position = new Vector3(0.3f, 0.2f, -0.1f);
            Quaternion rotation = Quaternion.Euler(0, 23, 0);
            Vector3 scale = new Vector3(0.8f, 1.1f, 0.9f);
            target.localPosition = position;
            target.localRotation = rotation;
            target.localScale = scale;
            try
            {
                DamageSubjectMotion motion = subject.AddComponent<DamageSubjectMotion>();
                uint firstOwner = motion.Acquire();
                motion.Apply(firstOwner, Vector3.forward * 0.08f, Quaternion.Euler(3, 0, 0),
                    new Vector3(1, 0.3f, 1));
                uint secondOwner = motion.Acquire();
                result.NewOwnerStartsFromOriginalPose = SamePose(target, position, rotation, scale);
                motion.Apply(secondOwner, Vector3.forward * 0.03f, Quaternion.Euler(0, 0, 4),
                    new Vector3(1, 0.5f, 1));
                Vector3 secondPosition = target.localPosition;
                Quaternion secondRotation = target.localRotation;
                Vector3 secondScale = target.localScale;
                bool acceptedOldWrite = motion.Apply(firstOwner, Vector3.up, Quaternion.identity, Vector3.one);
                result.OldOwnerCannotMoveNewPose = !acceptedOldWrite &&
                    SamePose(target, secondPosition, secondRotation, secondScale);
                motion.Release(firstOwner);
                result.OldOwnerCannotRestoreNewPose = SamePose(target, secondPosition, secondRotation, secondScale);
                motion.Release(secondOwner);
                result.LatestOwnerRestoresOriginalPose = SamePose(target, position, rotation, scale);
                uint thirdOwner = motion.Acquire();
                motion.Apply(thirdOwner, Vector3.down * 0.2f, Quaternion.Euler(2, 0, 1), Vector3.one * 0.5f);
                subject.SetActive(false);
                result.DisableRestoresOriginalPose = SamePose(target, position, rotation, scale);
                subject.SetActive(true);
                result.DisabledOwnerBecomesInvalid = !motion.Apply(thirdOwner, Vector3.up,
                    Quaternion.identity, Vector3.one);
                uint fourthOwner = motion.Acquire();
                motion.Apply(fourthOwner, Vector3.forward, Quaternion.identity, Vector3.one);
                UnityEngine.Object.DestroyImmediate(subject);
                result.DestroyedSubjectReferenceBecomesNull = motion == null;
                result.Passed = result.NewOwnerStartsFromOriginalPose && result.OldOwnerCannotMoveNewPose &&
                    result.OldOwnerCannotRestoreNewPose && result.LatestOwnerRestoresOriginalPose &&
                    result.DisableRestoresOriginalPose && result.DisabledOwnerBecomesInvalid &&
                    result.DestroyedSubjectReferenceBecomesNull;
                return result;
            }
            finally
            {
                if (subject != null) UnityEngine.Object.DestroyImmediate(subject);
            }
        }

        private static bool SamePose(Transform target, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            return (target.localPosition - position).sqrMagnitude < 0.00000001f &&
                Quaternion.Angle(target.localRotation, rotation) < 0.001f &&
                (target.localScale - scale).sqrMagnitude < 0.00000001f;
        }
    }
}
