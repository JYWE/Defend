using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Defend.PhaseFiveEffects.Editor
{
    public static class EffectAuthoring
    {
        public const string RootPath = "Assets/Defend/Content/Art/PhaseFiveEffects";
        public static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
        }
        public static T Root<T>(string id, float lifetime, EffectBudgetGroup group = EffectBudgetGroup.ShortEffect, int cost = 1) where T : PhaseFiveEffect
        {
            T effect = new GameObject(id).AddComponent<T>();
            effect.Lifetime = lifetime; effect.BudgetGroup = group; effect.BudgetUnits = cost;
            return effect;
        }
        public static Material Material(string name)
        {
            if (name.StartsWith("Original"))
            {
                string suffix = name == "OriginalArmor" ? "ArmorIvory" : name == "OriginalMetal" ? "MetalSteel" : "StructureDark";
                return AssetDatabase.LoadAssetAtPath<Material>("Assets/Defend/Content/Art/PhaseFive/Materials/PhaseFive_" + suffix + ".mat");
            }
            return AssetDatabase.LoadAssetAtPath<Material>(RootPath + "/Materials/" + name + ".mat");
        }
        public static LineRenderer Line(Transform parent, string name, Material mat, float width, Vector3[] points, bool loop = false)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false);
            var line = obj.AddComponent<LineRenderer>(); line.sharedMaterial = mat;
            line.useWorldSpace = false; line.loop = loop; line.widthMultiplier = width;
            line.positionCount = points.Length; line.SetPositions(points); line.numCornerVertices = 2; line.numCapVertices = 2;
            line.startColor = line.endColor = Color.white;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
            return line;
        }
        public static ParticleSystem Burst(Transform parent, string name, Material material, Color color, int count, float speed, float size, float lifetime)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false);
            var particles = obj.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.playOnAwake = false; main.loop = false;
            main.duration = Mathf.Max(.01f, lifetime); main.startLifetime = lifetime;
            main.startSpeed = speed; main.startSize = size; main.startColor = color; main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var colorLife = particles.colorOverLifetime; colorLife.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); colorLife.color = gradient;
            var sizeLife = particles.sizeOverLifetime; sizeLife.enabled = true; sizeLife.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, .15f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            return particles;
        }
        public static Transform Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material mat)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; obj.transform.localScale = scale;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            var renderer = obj.GetComponent<Renderer>(); renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return obj.transform;
        }
        public static void Save(PhaseFiveEffect effect, string category, string id)
        {
            Folder(RootPath + "/Prefabs/" + category);
            PrefabUtility.SaveAsPrefabAsset(effect.gameObject, RootPath + "/Prefabs/" + category + "/" + id + ".prefab");
            Object.DestroyImmediate(effect.gameObject);
        }
    }
}
