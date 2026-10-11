using UnityEngine;

namespace Defend.PhaseFiveEffects
{
    /// <summary>Send and arrival are separate visual requests, never inferred from transport time.</summary>
    public sealed class EnergyEndpointEffect : PhaseFiveEffect
    {
        [SerializeField] private bool arrival;
        [SerializeField] private Transform geometry;
        [SerializeField] private LineRenderer[] strokes;
        [SerializeField] private float[] widths;

        public void Configure(bool isArrival, Transform visual, LineRenderer[] lines)
        {
            arrival = isArrival;
            geometry = visual;
            strokes = lines;
            widths = new float[lines.Length];
            for (int index = 0; index < lines.Length; index++) widths[index] = lines[index].widthMultiplier;
            OnReset();
        }

        protected override void OnBegin()
        {
            geometry.gameObject.SetActive(true);
            for (int index = 0; index < strokes.Length; index++) strokes[index].enabled = true;
        }

        protected override void OnAdvance(float normalizedTime, float deltaTime)
        {
            geometry.localScale = Vector3.one * (arrival
                ? Mathf.Lerp(1.15f, .48f, normalizedTime)
                : Mathf.Lerp(.45f, 1.15f, normalizedTime));
            float remaining = 1f - normalizedTime;
            Color tint = new Color(1, 1, 1, remaining);
            for (int index = 0; index < strokes.Length; index++)
            {
                strokes[index].startColor = strokes[index].endColor = tint;
                strokes[index].widthMultiplier = widths[index] * (.5f + .5f * remaining);
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
                strokes[index].startColor = strokes[index].endColor = Color.white;
                if (widths != null && index < widths.Length) strokes[index].widthMultiplier = widths[index];
            }
        }
    }
}
