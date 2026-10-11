using UnityEditor;
using UnityEngine;

namespace Defend.PhaseFiveEffects.Editor
{
    /// <summary>Creates six isolated, reusable damage resources without changing model assets.</summary>
    public static class DamageEffectsAuthoring
    {
        public static void Build()
        {
            BuildEffect("EnemyHit", DamageEffectKind.EnemyHit, 0.08f, 5, 0, false);
            BuildEffect("EnemyDeath", DamageEffectKind.EnemyDeath, 0.60f, 4, 5, true);
            BuildEffect("HeavyContact", DamageEffectKind.HeavyContact, 0.25f, 7, 3, false);
            BuildEffect("BuildingHit", DamageEffectKind.BuildingHit, 0.08f, 6, 0, false);
            BuildEffect("BuildingDestruction", DamageEffectKind.BuildingDestruction, 0.50f, 4, 7, true);
            BuildEffect("RepairComplete", DamageEffectKind.RepairComplete, 0.22f, 3, 0, false);
        }

        private static void BuildEffect(string identifier, DamageEffectKind kind, float lifetime,
            int sparkCount, int dustCount, bool hasDebris)
        {
            DamageEffect effect = EffectAuthoring.Root<DamageEffect>(identifier, lifetime,
                hasDebris ? EffectBudgetGroup.Debris : EffectBudgetGroup.ShortEffect,
                hasDebris ? DamageEffect.DebrisPieceCount : 1);
            Transform flashRoot = new GameObject("LocalFlashGeometry").transform;
            flashRoot.SetParent(effect.transform, false);
            bool repair = kind == DamageEffectKind.RepairComplete;
            Material flashMaterial = EffectAuthoring.Material(repair ? "GreenGlow" : "WarmGlow");
            BuildFlash(flashRoot, kind, flashMaterial);

            Transform[] pieces = new Transform[hasDebris ? DamageEffect.DebrisPieceCount : 0];
            for (int index = 0; index < pieces.Length; index++)
            {
                string materialName = kind == DamageEffectKind.BuildingDestruction && index % 3 == 0 ?
                    "OriginalArmor" : index % 2 == 0 ? "OriginalMetal" : "OriginalDark";
                pieces[index] = EffectAuthoring.Shape(effect.transform, "DebrisPiece" + (index + 1),
                    PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.08f,
                    EffectAuthoring.Material(materialName));
                pieces[index].gameObject.SetActive(false);
            }

            ParticleSystem sparks = EffectAuthoring.Burst(effect.transform, "ShortSparks",
                flashMaterial, repair ? new Color(0.25f, 1, 0.55f, 1) : new Color(1, 0.56f, 0.16f, 1),
                sparkCount, repair ? 0.55f : 1.45f, repair ? 0.035f : 0.025f,
                Mathf.Min(lifetime, repair ? 0.20f : 0.16f));
            sparks.transform.localPosition = Vector3.up * 0.035f;
            ParticleSystem dust = null;
            if (dustCount > 0)
            {
                dust = EffectAuthoring.Burst(effect.transform, "GroundDust",
                    EffectAuthoring.Material("DustSoft"), new Color(0.48f, 0.51f, 0.54f, 0.30f),
                    dustCount, 0.40f, 0.12f, Mathf.Min(lifetime, 0.42f));
                dust.transform.localPosition = Vector3.up * 0.04f;
            }
            effect.Configure(kind, flashRoot, pieces, sparks, sparkCount, dust, dustCount);
            flashRoot.gameObject.SetActive(false);
            foreach (Collider collider in effect.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            EditorUtility.SetDirty(effect);
            EffectAuthoring.Save(effect, "Damage", identifier);
        }

        private static void BuildFlash(Transform parent, DamageEffectKind kind, Material material)
        {
            if (kind == DamageEffectKind.RepairComplete || kind == DamageEffectKind.BuildingHit)
            {
                float distance = kind == DamageEffectKind.RepairComplete ? 0.38f : 0.14f;
                float length = kind == DamageEffectKind.RepairComplete ? 0.17f : 0.075f;
                for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    EffectAuthoring.Shape(parent, "CornerHorizontal", PrimitiveType.Cube,
                        new Vector3(x * (distance - length * 0.5f), 0.025f, z * distance),
                        new Vector3(length, 0.018f, 0.02f), material);
                    EffectAuthoring.Shape(parent, "CornerVertical", PrimitiveType.Cube,
                        new Vector3(x * distance, 0.025f, z * (distance - length * 0.5f)),
                        new Vector3(0.02f, 0.018f, length), material);
                }
                return;
            }
            float radius = kind == DamageEffectKind.HeavyContact ? 0.19f : 0.12f;
            int rayCount = kind == DamageEffectKind.HeavyContact ? 5 : 4;
            for (int index = 0; index < rayCount; index++)
            {
                float angle = index * 360f / rayCount;
                Quaternion rotation = Quaternion.Euler(0, angle, 0);
                Transform ray = EffectAuthoring.Shape(parent, "ImpactRay" + (index + 1),
                    PrimitiveType.Cube, Vector3.up * 0.045f + rotation * Vector3.right * (radius * 0.50f),
                    new Vector3(radius * 0.90f, 0.018f, 0.025f), material);
                ray.localRotation = rotation;
            }
            EffectAuthoring.Shape(parent, "SmallWhiteContact", PrimitiveType.Cube,
                Vector3.up * 0.05f, new Vector3(0.07f, 0.025f, 0.07f),
                EffectAuthoring.Material("WhiteGlow"));
        }
    }
}
