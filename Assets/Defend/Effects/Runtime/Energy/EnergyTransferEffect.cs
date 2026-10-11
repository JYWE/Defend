using UnityEngine;

namespace Defend.PhaseFiveEffects
{
    /// <summary>A short moving bead along confirmed, immutable transport endpoints.</summary>
    public sealed class EnergyTransferEffect : PhaseFiveEffect
    {
        [SerializeField] private Transform pulseHead;
        [SerializeField] private LineRenderer shortTrail;
        [SerializeField] private Vector3 headScale = new Vector3(.035f, .025f, .075f);
        [SerializeField] private float trailLength = .18f;
        [SerializeField] private float trailWidth = .018f;
        private Vector3 origin;
        private Vector3 destination;
        private Vector3 direction;
        private float distance;
        private bool confirmed;

        public void Configure(Transform head, LineRenderer trail)
        {
            pulseHead = head;
            shortTrail = trail;
            headScale = head.localScale;
            OnReset();
        }

        public override bool CanPlay(EffectRequest request)
        {
            if (!request.ConfirmedTransfer || !Finite(request.Origin) || !Finite(request.Destination)) return false;
            float squaredDistance = (request.Destination - request.Origin).sqrMagnitude;
            return !float.IsInfinity(squaredDistance) && !float.IsNaN(squaredDistance) && squaredDistance > .000001f;
        }

        private static bool Finite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        protected override void OnBegin()
        {
            // Also suppress direct Begin calls that bypass the pool's CanPlay gate.
            confirmed = CanPlay(Context);
            pulseHead.gameObject.SetActive(confirmed);
            shortTrail.enabled = confirmed;
            if (!confirmed) return;
            origin = Context.Origin;
            destination = Context.Destination;
            distance = Vector3.Distance(origin, destination);
            direction = (destination - origin) / distance;
            pulseHead.rotation = Quaternion.LookRotation(direction,
                Mathf.Abs(direction.y) > .99f ? Vector3.forward : Vector3.up);
            pulseHead.localScale = headScale;
            shortTrail.useWorldSpace = true;
            shortTrail.positionCount = 2;
        }

        protected override void OnAdvance(float normalizedTime, float deltaTime)
        {
            if (!confirmed) return;
            Vector3 position = Vector3.Lerp(origin, destination, normalizedTime);
            pulseHead.position = position;
            shortTrail.SetPosition(0, position - direction * Mathf.Min(trailLength, distance * normalizedTime));
            shortTrail.SetPosition(1, position);
            float fade = Mathf.Clamp01((1f - normalizedTime) * 7f);
            shortTrail.startColor = new Color(1, 1, 1, .12f * fade);
            shortTrail.endColor = new Color(1, 1, 1, .8f * fade);
            shortTrail.widthMultiplier = trailWidth;
            pulseHead.localScale = headScale * (.6f + .4f * fade);
        }

        protected override void OnReset()
        {
            confirmed = false;
            origin = destination = direction = Vector3.zero;
            distance = 0;
            if (pulseHead != null)
            {
                pulseHead.gameObject.SetActive(false);
                pulseHead.localPosition = Vector3.zero;
                pulseHead.localRotation = Quaternion.identity;
                pulseHead.localScale = headScale;
            }
            if (shortTrail == null) return;
            shortTrail.enabled = false;
            shortTrail.positionCount = 2;
            shortTrail.SetPosition(0, Vector3.zero);
            shortTrail.SetPosition(1, Vector3.zero);
            shortTrail.widthMultiplier = trailWidth;
            shortTrail.startColor = shortTrail.endColor = Color.white;
        }
    }
}
