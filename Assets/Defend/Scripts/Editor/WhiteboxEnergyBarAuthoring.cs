using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Defend.Editor
{
    /// <summary>在编辑器配置真实 Sprite，并提供对当前可见网格的只读诊断。</summary>
    public static class WhiteboxEnergyBarAuthoring
    {
        public static void Configure(Image image)
        {
            if (image == null) throw new InvalidOperationException("能源检查面板缺少 EnergyFill Image。");
            // 与 Unity 自带 uGUI 创建菜单使用同一个标准资源，引用直接保存到 Prefab。
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            if (sprite == null)
                throw new InvalidOperationException("Unity 内置 UI Sprite 不可用，无法制作正确的储能填充条。");
            image.sprite = sprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }

        /// <summary>
        /// 只读取 CanvasRenderer 实际持有的顶点。调用者应先等待 UI 重建，再比较 0%、50%、100% 的可见宽度。
        /// 本方法不修改填充值、不创建控件，也不重新生成 UI。
        /// </summary>
        public static EnergyBarGeometry ReadVisibleGeometry(Image image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            // Unity 6.6 返回 CanvasRenderer 持有的网格；这里只读取，不能销毁或修改其内容。
            Mesh mesh = image.canvasRenderer.GetMesh();
            List<Vector3> vertices = new List<Vector3>();
            if (mesh != null) mesh.GetVertices(vertices);
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            foreach (Vector3 vertex in vertices)
            {
                minimum = Mathf.Min(minimum, vertex.x);
                maximum = Mathf.Max(maximum, vertex.x);
            }
            return new EnergyBarGeometry
            {
                HasSprite = image.sprite != null,
                FillAmount = image.fillAmount,
                VertexCount = vertices.Count,
                VisibleWidth = vertices.Count == 0 ? 0 : maximum - minimum,
                RectangleWidth = image.rectTransform.rect.width
            };
        }

        [Serializable]
        public struct EnergyBarGeometry
        {
            public bool HasSprite;
            public float FillAmount;
            public int VertexCount;
            public float VisibleWidth;
            public float RectangleWidth;
        }
    }
}
