using UnityEngine;

namespace Defend.PhaseFiveEffects
{
    /// <summary>The whole confirmed shot appears immediately, then quickly fades.</summary>
    public sealed class WeaponTracerEffect : PhaseFiveEffect
    {
        [SerializeField] private LineRenderer outerLine;
        [SerializeField] private LineRenderer innerLine;
        [SerializeField] private float outerWidth = 0.012f;
        [SerializeField] private float innerWidth = 0.004f;
        private float intensity;

        public void Configure(LineRenderer outer, LineRenderer inner, float width, float coreWidth)
        {
            outerLine = outer;
            innerLine = inner;
            outerWidth = width;
            innerWidth = coreWidth;
            OnReset();
        }

        protected override void OnBegin()
        {
            intensity = Mathf.Clamp(Context.Intensity, 0.25f, 1.5f);
            InitializeLine(outerLine, outerWidth);
            InitializeLine(innerLine, innerWidth);
        }

        private void InitializeLine(LineRenderer line, float width)
        {
            if (line == null) return;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, Context.Origin);
            line.SetPosition(1, Context.Destination);
            line.widthMultiplier = width * intensity;
            line.startColor = line.endColor = Color.white;
            line.enabled = true;
        }

        protected override void OnAdvance(float normalizedTime, float deltaTime)
        {
            float remaining = 1f - normalizedTime;
            Color tint = new Color(1f, 1f, 1f, remaining * remaining);
            if (outerLine != null)
            {
                outerLine.startColor = outerLine.endColor = tint;
                outerLine.widthMultiplier = outerWidth * intensity * remaining;
            }
            if (innerLine != null)
            {
                innerLine.startColor = innerLine.endColor = tint;
                innerLine.widthMultiplier = innerWidth * intensity * remaining;
            }
        }

        protected override void OnReset()
        {
            ResetLine(outerLine, outerWidth);
            ResetLine(innerLine, innerWidth);
        }

        private static void ResetLine(LineRenderer line, float width)
        {
            if (line == null) return;
            line.enabled = false;
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.zero);
            line.widthMultiplier = width;
            line.startColor = line.endColor = Color.white;
        }
    }
}
