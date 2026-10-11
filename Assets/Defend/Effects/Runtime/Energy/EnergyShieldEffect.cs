using UnityEngine;

namespace Defend.PhaseFiveEffects
{
    /// <summary>Local shield feedback with no material mutation or durability side effects.</summary>
    public sealed class EnergyShieldEffect : PhaseFiveEffect
    {
        [SerializeField] private bool breaking;
        [SerializeField] private Transform geometry;
        [SerializeField] private LineRenderer[] strokes;
        [SerializeField] private float[] widths;
        [SerializeField] private Vector3[] scatterDirections;
        [SerializeField] private ParticleSystem splinters;
        [SerializeField] private int particleCount;

        public void Configure(bool isBreaking, Transform visual, LineRenderer[] lines,
            Vector3[] directions, ParticleSystem particles, int count)
        {
            breaking = isBreaking;
            geometry = visual;
            strokes = lines;
            scatterDirections = directions;
            splinters = particles;
            particleCount = count;
            widths = new float[lines.Length];
            for (int index = 0; index < lines.Length; index++) widths[index] = lines[index].widthMultiplier;
            OnReset();
        }

        protected override void OnBegin()
        {
            geometry.gameObject.SetActive(true);
            for (int index = 0; index < strokes.Length; index++) strokes[index].enabled = true;
            if (splinters != null && particleCount > 0) splinters.Emit(particleCount);
        }

        protected override void OnAdvance(float normalizedTime, float deltaTime)
        {
            float remaining = 1f - normalizedTime;
            geometry.localScale = Vector3.one * (breaking ? 1f : Mathf.Lerp(.70f, 1.18f, normalizedTime));
            for (int index = 0; index < strokes.Length; index++)
            {
                strokes[index].startColor = strokes[index].endColor = new Color(1, 1, 1, remaining * remaining);
                strokes[index].widthMultiplier = widths[index] * (.4f + .6f * remaining);
                if (breaking && scatterDirections != null && index < scatterDirections.Length)
                    strokes[index].transform.localPosition = scatterDirections[index] * (.18f * normalizedTime);
            }
        }

        protected override void OnReset()
        {
            if (geometry != null)
            {
                geometry.localScale = Vector3.one;
                geometry.gameObject.SetActive(false);
            }
            if (strokes == null) return;
            for (int index = 0; index < strokes.Length; index++)
            {
                if (strokes[index] == null) continue;
                strokes[index].enabled = false;
                strokes[index].transform.localPosition = Vector3.zero;
                strokes[index].startColor = strokes[index].endColor = Color.white;
                if (widths != null && index < widths.Length) strokes[index].widthMultiplier = widths[index];
            }
        }
    }
}
