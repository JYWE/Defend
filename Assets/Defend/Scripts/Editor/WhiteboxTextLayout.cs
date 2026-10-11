using UnityEngine;
using UnityEngine.UI;

namespace Defend.Editor
{
    /// <summary>首次制作和限定迁移共用的文字排版规则，给 Noto 中文字体留出真实行高。</summary>
    internal static class WhiteboxTextLayout
    {
        public static void Apply(Text label)
        {
            // 动态长文保留裁切边界，不能因溢出覆盖优先级按钮、诊断区或操作面板。
            bool boundedParagraph = label.name == "SelectionDetails" || label.name == "DiagnosticValues" ||
                label.name == "Feedback" || label.name == "ShopDescription";
            label.verticalOverflow = boundedParagraph ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
            switch (label.name)
            {
                case "ProductName": SetLine(label, 5, 44, 26); break;
                case "Subtitle": SetLine(label, 50, 24, 13); break;
                case "Resources": SetLine(label, 22, 36, 18); break;
                case "PhaseBadge": SetLine(label, 24, 32, 16); break;
                case "ShopHeading": SetLine(label, 12, 38, 22); break;
                case "ShopDescription": SetLine(label, 61, 36, 14); break;
                case "SelectionTitle": SetLine(label, 14, 38, 22); break;
                case "MapTitle": SetLine(label, 114, 34, 20); break;
                case "MapSubtitle": SetLine(label, 151, 26, 14); break;
                case "DiagnosticHeading": SetLine(label, 12, 26, 14); break;
                case "InteractionMode": SetLine(label, 9, 28, 16); break;
            }
        }

        private static void SetLine(Text label, float top, float height, int fontSize)
        {
            RectTransform rectangle = label.rectTransform;
            rectangle.anchoredPosition = new Vector2(rectangle.anchoredPosition.x, -top);
            rectangle.sizeDelta = new Vector2(rectangle.sizeDelta.x, height);
            label.fontSize = fontSize;
        }
    }
}
