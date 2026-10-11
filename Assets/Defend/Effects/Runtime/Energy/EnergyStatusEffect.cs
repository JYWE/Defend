using UnityEngine;

namespace Defend.PhaseFiveEffects
{
    public enum EnergyStatusKind { Reactor, Relay, Shortage, Disconnected, HeavyLock, Search }

    /// <summary>Explicitly stopped status glyphs. Time belongs to the common effect pool.</summary>
    public sealed class EnergyStatusEffect : PhaseFiveEffect
    {
        [SerializeField] private EnergyStatusKind kind;
        [SerializeField] private Transform geometry;
        [SerializeField] private LineRenderer[] strokes;
        [SerializeField] private float[] widths;

        public void Configure(EnergyStatusKind statusKind, Transform visual, LineRenderer[] lines)
        {
            kind = statusKind;
            geometry = visual;
            strokes = lines;
            widths = new float[lines.Length];
            for (int index = 0; index < lines.Length; index++) widths[index] = lines[index].widthMultiplier;
            OnReset();
        }

        protected override void OnBegin()
        {
            if (geometry != null) geometry.gameObject.SetActive(true);
            for (int index = 0; index < strokes.Length; index++) strokes[index].enabled = true;
        }

        protected override void OnAdvance(float normalizedTime, float deltaTime)
        {
            float wave = .5f + .5f * Mathf.Sin(normalizedTime * Mathf.PI * 2f);
            if (geometry != null)
            {
                geometry.localScale = Vector3.one * (kind == EnergyStatusKind.HeavyLock ? 1.015f - wave * .03f : 1f);
                geometry.localRotation = kind == EnergyStatusKind.Relay
                    ? Quaternion.Euler(0, normalizedTime * 360f, 0) : Quaternion.identity;
            }
            for (int index = 0; index < strokes.Length; index++)
            {
                float alpha = .62f + .30f * wave;
                if (kind == EnergyStatusKind.Reactor)
                    alpha = .35f + .65f * (.5f + .5f * Mathf.Sin((normalizedTime - index * .18f) * Mathf.PI * 2f));
                if (kind == EnergyStatusKind.Search) alpha = .48f + .42f * wave;
                strokes[index].startColor = strokes[index].endColor = new Color(1, 1, 1, alpha);
                strokes[index].widthMultiplier = widths[index];
            }
        }

        protected override void OnReset()
        {
            if (geometry != null)
            {
                geometry.localScale = Vector3.one;
                geometry.localRotation = Quaternion.identity;
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
