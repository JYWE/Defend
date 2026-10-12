using System.Collections.Generic;
using UnityEngine;

namespace Defend.Unity
{
    /// <summary>
    /// 受击闪白效果。
    /// 将该组件挂在敌人根对象上即可。
    /// </summary>
    public sealed class HitFlashEffect : MonoBehaviour
    {
        private static readonly int WhiteOverlayId =
            Shader.PropertyToID("_WhiteOverlay");

        [Header("Hit Flash")]

        /// <summary>
        /// 当前闪白强度。
        /// 外部将其设置为 1 即可触发一次完整闪白。
        /// </summary>
        [Range(0f, 1f)]
        public float WhiteOverlay = 0f;

        /// <summary>
        /// 每秒衰减的闪白强度。
        /// </summary>
        [Min(0f)]
        public float DecayPerSecond = 6f;

        private readonly List<Material> materials =
            new List<Material>();

        private float lastAppliedValue = -1f;

        private void Awake()
        {
            CacheMaterials();

            // 立即同步初始值。
            ApplyWhiteOverlay(WhiteOverlay);
        }

        private void Update()
        {
            float currentValue =
                Mathf.Clamp01(WhiteOverlay);

            // 先应用当前值，确保外部设置为 1 后，
            // 下一帧可以完整显示一次闪白。
            if (!Mathf.Approximately(
                    currentValue,
                    lastAppliedValue))
            {
                ApplyWhiteOverlay(currentValue);
            }

            if (currentValue <= 0f)
            {
                WhiteOverlay = 0f;
                return;
            }

            // 使用线性衰减。
            WhiteOverlay =
                Mathf.MoveTowards(
                    currentValue,
                    0f,
                    DecayPerSecond *
                    Time.deltaTime);
        }

        /// <summary>
        /// 获取自身和所有子物体上的材质实例。
        /// </summary>
        private void CacheMaterials()
        {
            materials.Clear();

            HashSet<Material> uniqueMaterials =
                new HashSet<Material>();

            Renderer[] renderers =
                GetComponentsInChildren<Renderer>(true);

            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer renderer =
                    renderers[rendererIndex];

                if (renderer == null)
                {
                    continue;
                }

                // 先读取共享材质，只筛选支持闪白参数的材质。
                Material[] sharedMaterials =
                    renderer.sharedMaterials;

                Material[] instanceMaterials = null;

                for (int materialIndex = 0;
                     materialIndex < sharedMaterials.Length;
                     materialIndex++)
                {
                    Material sharedMaterial =
                        sharedMaterials[materialIndex];

                    if (sharedMaterial == null ||
                        !sharedMaterial.HasProperty(
                            WhiteOverlayId))
                    {
                        continue;
                    }

                    // 只有确认需要闪白时才获取实例材质，
                    // 避免修改项目中的共享材质资源。
                    if (instanceMaterials == null)
                    {
                        instanceMaterials =
                            renderer.materials;
                    }

                    if (materialIndex >= instanceMaterials.Length)
                    {
                        continue;
                    }

                    Material instanceMaterial =
                        instanceMaterials[materialIndex];

                    if (instanceMaterial == null ||
                        !instanceMaterial.HasProperty(
                            WhiteOverlayId))
                    {
                        continue;
                    }

                    if (uniqueMaterials.Add(instanceMaterial))
                    {
                        materials.Add(instanceMaterial);
                    }
                }
            }
        }

        /// <summary>
        /// 将闪白强度写入所有相关材质。
        /// </summary>
        private void ApplyWhiteOverlay(
            float value)
        {
            value =
                Mathf.Clamp01(value);

            for (int index = 0;
                 index < materials.Count;
                 index++)
            {
                Material material =
                    materials[index];

                if (material == null)
                {
                    continue;
                }

                material.SetFloat(
                    WhiteOverlayId,
                    value);
            }

            lastAppliedValue = value;
        }

        /// <summary>
        /// 可选的代码调用接口。
        /// 等价于把 WhiteOverlay 设置为 1。
        /// </summary>
        public void Flash()
        {
            WhiteOverlay = 1f;
        }
        private void OnEnable()
        {
            if (materials.Count == 0)
            {
                CacheMaterials();
            }

            lastAppliedValue = -1f;
            ApplyWhiteOverlay(WhiteOverlay);
        }

        public void ResetFlash()
        {
            WhiteOverlay = 0f;
            ApplyWhiteOverlay(0f);
        }
    }
}