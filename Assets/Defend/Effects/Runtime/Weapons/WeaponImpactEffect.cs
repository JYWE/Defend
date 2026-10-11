using UnityEngine;

namespace Defend.PhaseFiveEffects
{
    public sealed class WeaponImpactEffect : PhaseFiveEffect
    {
        [SerializeField] private Transform contactFlash;
        [SerializeField] private LineRenderer expandingRing;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private int sparkCount = 5;
        [SerializeField] private float ringStartRadius = 0.025f;
        [SerializeField] private float ringEndRadius = 0.18f;
        [SerializeField] private float ringWidth = 0.014f;
        [SerializeField] private float flashDuration = 0.055f;
        private float intensity;

        public void Configure(Transform flash, LineRenderer ring, ParticleSystem particles,
            int count, float startRadius, float endRadius, float width, float flashSeconds)
        {
            contactFlash = flash;
            expandingRing = ring;
            sparks = particles;
            sparkCount = count;
            ringStartRadius = startRadius;
            ringEndRadius = endRadius;
            ringWidth = width;
            flashDuration = flashSeconds;
            OnReset();
        }

        protected override void OnBegin()
        {
            // A hit is already confirmed: use its captured position, never the Subject transform.
            transform.position = Context.Destination;
            transform.rotation = Quaternion.identity;
            intensity = Mathf.Clamp(Context.Intensity, 0.25f, 1.5f);
            if (contactFlash != null)
            {
                contactFlash.gameObject.SetActive(true);
                contactFlash.localScale = Vector3.one * intensity;
            }
            if (expandingRing != null)
            {
                expandingRing.enabled = true;
                expandingRing.transform.localScale = Vector3.one * ringStartRadius;
                expandingRing.widthMultiplier = ringWidth * intensity;
                expandingRing.startColor = expandingRing.endColor = Color.white;
            }
            if (sparks != null) sparks.Emit(sparkCount);
        }

        protected override void OnAdvance(float normalizedTime, float deltaTime)
        {
            float remaining = 1f - normalizedTime;
            if (contactFlash != null)
            {
                float flashAge = normalizedTime * Lifetime / Mathf.Max(0.001f, flashDuration);
                contactFlash.gameObject.SetActive(flashAge < 1f);
                contactFlash.localScale = Vector3.one * intensity * Mathf.Max(0.05f, 1f - flashAge);
            }
            if (expandingRing != null)
            {
                float expansion = 1f - remaining * remaining;
                float radius = Mathf.Lerp(ringStartRadius, Context.Radius > 0 ? Context.Radius : ringEndRadius, expansion);
                expandingRing.transform.localScale = Vector3.one * radius;
                expandingRing.widthMultiplier = ringWidth * intensity * remaining;
                Color tint = new Color(1f, 1f, 1f, remaining);
                expandingRing.startColor = expandingRing.endColor = tint;
            }
        }

        protected override void OnReset()
        {
            if (contactFlash != null)
            {
                contactFlash.gameObject.SetActive(false);
                contactFlash.localScale = Vector3.one;
            }
            if (expandingRing != null)
            {
                expandingRing.enabled = false;
                expandingRing.transform.localScale = Vector3.one * ringStartRadius;
                expandingRing.widthMultiplier = ringWidth;
                expandingRing.startColor = expandingRing.endColor = Color.white;
            }
        }
    }
}
