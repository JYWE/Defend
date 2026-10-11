using System;
using System.IO;
using Defend.Unity;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Defend.Editor
{
    /// <summary>已有白盒资产的限定迁移：只修字体引用、操作提示和旧网格线宽，不重建布局与建筑。</summary>
    public static class WhiteboxPresentationMigration
    {
        private const string PanelPath = "Assets/Defend/Content/Prefabs/UI/EnergyWhiteboxPanel.prefab";
        private const string LicensePath = "Assets/Defend/Content/Art/Fonts/NotoSansCJK-License.txt";

        [MenuItem("涌现防线/修复白盒字体和网格显示")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请在编辑模式执行字体和网格迁移。");
            AssetDatabase.ImportAsset(WhiteboxSceneBuilder.ChineseFontPath, ImportAssetOptions.ForceSynchronousImport);
            Font font = AssetDatabase.LoadAssetAtPath<Font>(WhiteboxSceneBuilder.ChineseFontPath);
            if (font == null) throw new InvalidOperationException("开源字体尚未成功导入：" + WhiteboxSceneBuilder.ChineseFontPath);
            GameObject panel = PrefabUtility.LoadPrefabContents(PanelPath);
            int changedLabels = 0;
            try
            {
                foreach (Text label in panel.GetComponentsInChildren<Text>(true))
                {
                    if (label.font != font) { label.font = font; changedLabels++; }
                    if (label.name == "KeyboardHelp") label.text = "滚轮缩放 · 中键拖动 · Esc 取消";
                    WhiteboxTextLayout.Apply(label);
                }
                Transform energyFill = panel.transform.Find("BuildingInspector/EnergyTrack/EnergyFill");
                if (energyFill == null) throw new InvalidOperationException("UI Prefab 缺少既定的储能条路径，已停止迁移。");
                WhiteboxEnergyBarAuthoring.Configure(energyFill.GetComponent<Image>());
                PrefabUtility.SaveAsPrefabAsset(panel, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(panel); }

            Scene scene = SceneManager.GetSceneByPath(WhiteboxSceneBuilder.ScenePath);
            bool openedForMigration = !scene.IsValid() || !scene.isLoaded;
            if (openedForMigration) scene = EditorSceneManager.OpenScene(WhiteboxSceneBuilder.ScenePath, OpenSceneMode.Additive);
            int changedGridLines = 0;
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name != "地图 · 40 × 40") continue;
                    Transform grid = root.transform.Find("网格线 · 编辑器资产");
                    if (grid == null) continue;
                    foreach (LineRenderer line in grid.GetComponentsInChildren<LineRenderer>(true))
                    {
                        // 只迁移首次生成器的默认细线；开发者已经手工调整过的宽度保持原样。
                        if (!Mathf.Approximately(line.startWidth, 0.018f) || !Mathf.Approximately(line.endWidth, 0.018f)) continue;
                        line.startWidth = line.endWidth = 0.075f;
                        EditorUtility.SetDirty(line);
                        changedGridLines++;
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (openedForMigration) EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("白盒显示迁移完成：替换字体 " + changedLabels + " 个；文本排版已更新；网格线 " + changedGridLines + " 条。未重建面板或建筑。");
        }

        /// <summary>验证所有 Assets 资源均未引用失败的旧字体后，才删除本次生成器遗留的资源。</summary>
        public static string RemoveUnusedLegacyFont()
        {
            const string legacyPath = "Assets/Defend/Content/Art/Fonts/WhiteboxChinese.fontsettings";
            if (AssetDatabase.LoadMainAssetAtPath(legacyPath) == null) return "旧字体已经不存在。";
            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/", StringComparison.Ordinal) || path == legacyPath || AssetDatabase.IsValidFolder(path)) continue;
                foreach (string dependency in AssetDatabase.GetDependencies(path, false))
                    if (dependency == legacyPath) return "保留旧字体，仍被引用：" + path;
            }
            if (!AssetDatabase.DeleteAsset(legacyPath)) throw new InvalidOperationException("无法删除未引用的旧字体资源。");
            return "已删除没有外部资源引用的旧 WhiteboxChinese.fontsettings 及其元数据。";
        }

        [PostProcessBuild(100)]
        private static void CopyFontLicense(BuildTarget target, string builtPlayerPath)
        {
            // Unity 不会自动打包未被引用的文本资产，Windows 交付时显式附上开源字体许可。
            if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64) return;
            string outputDirectory = Path.GetDirectoryName(builtPlayerPath);
            string noticesDirectory = Path.Combine(outputDirectory, "ThirdPartyNotices");
            Directory.CreateDirectory(noticesDirectory);
            File.Copy(LicensePath, Path.Combine(noticesDirectory, "NotoSansCJK-License.txt"), true);
            File.Copy("Assets/Defend/Content/Art/Fonts/FontSource.txt", Path.Combine(noticesDirectory, "NotoSansCJK-Source.txt"), true);
        }
    }
}
