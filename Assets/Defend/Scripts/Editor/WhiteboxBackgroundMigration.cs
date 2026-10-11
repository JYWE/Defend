using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Defend.Editor
{
    /// <summary>为局部地图摄像机补充全屏底色清除，防止地图外透明文字逐帧叠画。</summary>
    public static class WhiteboxBackgroundMigration
    {
        private const string CameraName = "BackgroundClearCamera · 全屏底色清除";
        private static readonly string[] ScenePaths =
        {
            WhiteboxSceneBuilder.ScenePath,
            CombatWhiteboxSceneBuilder.CombatScenePath,
            "Assets/Defend/Content/Scenes/Dev/CombatStress.unity"
        };

        /// <summary>由首次场景制作共用。只补缺失摄像机，不改变主镜头、UI 或地图内容。</summary>
        internal static bool EnsureBackgroundCamera(Scene scene, Camera worldCamera)
        {
            if (worldCamera == null || worldCamera.gameObject.scene != scene)
                throw new InvalidOperationException("需要指定同一场景中的地图摄像机。");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera == worldCamera) continue;
                    bool clearsBackground = camera.enabled && camera.gameObject.activeInHierarchy && camera.cullingMask == 0 &&
                        camera.clearFlags == CameraClearFlags.SolidColor && camera.rect == new Rect(0, 0, 1, 1) &&
                        camera.depth < worldCamera.depth && camera.targetDisplay == worldCamera.targetDisplay && camera.targetTexture == null;
                    if (clearsBackground) return false;
                    if (camera.name == CameraName)
                        throw new InvalidOperationException("背景摄像机已经存在但未覆盖整个屏幕，请检查其清除方式、视口和渲染顺序。迁移未覆盖已有设置。");
                }
            }

            GameObject cameraObject = new GameObject(CameraName, typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Undo.RegisterCreatedObjectUndo(cameraObject, "添加全屏底色清除摄像机");
            Camera background = cameraObject.GetComponent<Camera>();
            background.clearFlags = CameraClearFlags.SolidColor;
            background.backgroundColor = worldCamera.backgroundColor;
            background.cullingMask = 0;
            background.rect = new Rect(0, 0, 1, 1);
            background.depth = worldCamera.depth - 100;
            background.targetDisplay = worldCamera.targetDisplay;
            background.orthographic = true;
            background.nearClipPlane = 0.1f;
            background.farClipPlane = 1;
            background.allowHDR = false;
            background.allowMSAA = false;
            background.useOcclusionCulling = false;
            EditorUtility.SetDirty(background);
            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }

        [MenuItem("涌现防线/修复当前场景的全屏背景清除")]
        public static void ApplyToCurrentScene()
        {
            RequireEditMode();
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path)) throw new InvalidOperationException("请先保存当前场景，再进行背景清除迁移。");
            if (EnsureBackgroundCamera(scene, FindWorldCamera(scene))) EditorSceneManager.SaveScene(scene);
            Debug.Log("当前场景已具有全屏背景清除：" + scene.path);
        }

        [MenuItem("涌现防线/修复白盒场景的全屏背景清除")]
        private static void ApplyToKnownScenesMenu() { ApplyToKnownScenes(); }

        public static string ApplyToKnownScenes()
        {
            RequireEditMode();
            // 不把开发者尚未保存的其他修改混进批量迁移；已有磁盘内容原样载入。
            foreach (string path in ScenePaths)
            {
                Scene loaded = SceneManager.GetSceneByPath(path);
                if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                    throw new InvalidOperationException("场景有未保存的编辑，请先保存后再迁移：" + path);
            }
            StringBuilder report = new StringBuilder();
            foreach (string path in ScenePaths)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    bool changed = EnsureBackgroundCamera(scene, FindWorldCamera(scene));
                    if (changed) EditorSceneManager.SaveScene(scene);
                    report.Append(changed ? "已补充：" : "已存在：").AppendLine(path);
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            Debug.Log(report.ToString());
            return report.ToString();
        }

        private static Camera FindWorldCamera(Scene scene)
        {
            Camera found = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (!camera.CompareTag("MainCamera")) continue;
                    if (found != null) throw new InvalidOperationException("场景有多个 MainCamera，迁移无法确定地图镜头：" + scene.path);
                    found = camera;
                }
            if (found == null) throw new InvalidOperationException("场景缺少 MainCamera：" + scene.path);
            return found;
        }

        private static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请退出播放模式后进行背景清除迁移。");
        }
    }
}
