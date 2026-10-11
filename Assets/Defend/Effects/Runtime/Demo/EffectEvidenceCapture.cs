using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Defend.PhaseFiveEffects
{
    /// <summary>Opt-in Play Mode capture. Fixed presentation time is separate from performance sampling.</summary>
    public sealed class EffectEvidenceCapture : MonoBehaviour
    {
        public EffectShowcase Demo;
        public bool Recording;
        public int Frame { get; private set; }
        public string OutputDirectory;
        public int FrameCount = 96, Width = 1280, Height = 720;
        public float FramesPerSecond = 30;
        Camera cameraToRecord;
        RenderTexture target;
        Texture2D pixels;

        public void Begin(string outputDirectory, bool closeView)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Capture requires Play Mode.");
            Cleanup();
            OutputDirectory = outputDirectory; Directory.CreateDirectory(outputDirectory);
            Demo.ExternalClock = true; Demo.SingleEffect = ""; Demo.Restart(); Demo.SetCloseView(closeView);
            cameraToRecord = closeView ? Demo.CloseCamera : Demo.OverviewCamera;
            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32); target.Create();
            pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            Frame = 0; Recording = true;
        }
        void LateUpdate()
        {
            if (!Recording) return;
            Demo.AdvanceDemo(1f / FramesPerSecond);
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            RenderPipeline.SubmitRenderRequest(cameraToRecord, request);
            var previous = RenderTexture.active;
            try { RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply(false); }
            finally { RenderTexture.active = previous; }
            File.WriteAllBytes(Path.Combine(OutputDirectory, "frame_" + Frame.ToString("D4") + ".png"), pixels.EncodeToPNG());
            Frame++;
            if (Frame >= FrameCount) { Recording = false; Cleanup(); Demo.ExternalClock = false; }
        }
        void Cleanup()
        {
            if (target != null) { target.Release(); Destroy(target); target = null; }
            if (pixels != null) { Destroy(pixels); pixels = null; }
        }
        void OnDisable() { Recording = false; Cleanup(); if (Demo != null) Demo.ExternalClock = false; }
    }
}
