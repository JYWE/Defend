using System;
using Defend.Simulation;
using Defend.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using static Defend.Editor.WhiteboxSceneBuilder;

namespace Defend.Editor
{
    /// <summary>首次复制第三阶段的已保存场景并定向绑定；已制作的第四阶段场景不重复生成。</summary>
    public static class PhaseFourSceneBuilder
    {
        public const string CombatScenePath = "Assets/Defend/Content/Scenes/Dev/EnergyFrontDefense.unity";
        public const string DefinitionsPath = "Assets/Defend/Content/Definitions/PhaseFour";
        public const string BattleSettingsPath = DefinitionsPath + "/EnergyFrontBattleSettings.asset";
        public const string EnergySettingsPath = DefinitionsPath + "/EnergyFrontEnergySettings.asset";
        public const string EnemyPath = "Assets/Defend/Content/Prefabs/Enemies/PhaseFourEnemy.prefab";
        public const string PanelPath = "Assets/Defend/Content/Prefabs/UI/PhaseFourPanel.prefab";
        public const string TargetMarkerPath = "Assets/Defend/Content/Prefabs/VFX/EnemyTargetMarker.prefab";

        [MenuItem("涌现防线/创建或打开能源防线关卡")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请退出播放模式后制作能源防线场景。");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CombatScenePath) != null)
            {
                EditorSceneManager.OpenScene(CombatScenePath);
                Debug.Log("保留已有能源防线场景和手工修改：" + CombatScenePath);
                return;
            }
            RequireAsset<SceneAsset>(PhaseThreeSceneBuilder.CombatScenePath);
            EnsureFolder(DefinitionsPath);
            if (!AssetDatabase.CopyAsset(PhaseThreeSceneBuilder.CombatScenePath, CombatScenePath))
                throw new InvalidOperationException("无法复制第三阶段场景；原场景未修改。");
            Scene scene = EditorSceneManager.OpenScene(CombatScenePath);
            GameSessionRoot session = RequireSingle<GameSessionRoot>(scene);
            SerializedObject sessionData = new SerializedObject(session);
            BattlePresentation presentation = (BattlePresentation)sessionData.FindProperty("battlePresentation").objectReferenceValue;
            BattleControlPanel oldPanel = (BattleControlPanel)sessionData.FindProperty("battleControlPanel").objectReferenceValue;
            BattleSettingsAsset sourceBattle = (BattleSettingsAsset)sessionData.FindProperty("battleSettings").objectReferenceValue;
            EnergyExperimentSettings sourceEnergy = (EnergyExperimentSettings)sessionData.FindProperty("experimentSettings").objectReferenceValue;
            if (presentation == null || oldPanel == null || sourceBattle == null || sourceEnergy == null)
                throw new InvalidOperationException("复制场景缺少战斗配置或表现引用，请检查第三阶段场景。");

            BattleSettingsAsset settings = CopyBattleSettings(sourceBattle);
            EnergyExperimentSettings energy = CopyAssetIfMissing(sourceEnergy, EnergySettingsPath, out _);
            SerializedObject presentationData = new SerializedObject(presentation);
            EnemyView sourceEnemy = (EnemyView)presentationData.FindProperty("enemyPrefab").objectReferenceValue;
            EnemyView enemy = CopyEnemy(sourceEnemy);
            BattleControlPanel panel = CreatePanelFromScene(oldPanel, scene);
            Transform markers = Child("重装目标标记", presentation.transform);
            presentation.ConfigureTargetMarkers(CreateTargetMarker(), markers);
            presentationData.Update();
            SetReference(presentationData, "enemyPrefab", enemy);
            presentationData.ApplyModifiedPropertiesWithoutUndo();
            sessionData.Update();
            SetReference(sessionData, "battleSettings", settings);
            SetReference(sessionData, "experimentSettings", energy);
            SetReference(sessionData, "battleControlPanel", panel);
            sessionData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(presentation);
            EditorUtility.SetDirty(session);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("能源防线场景未能保存。");
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = session.gameObject;
            Debug.Log("能源防线已从现有双隘防线复制。旧场景、旧配置、初始布局及原有资源均保留。入口：" + CombatScenePath);
        }

        private static BattleSettingsAsset CopyBattleSettings(BattleSettingsAsset source)
        {
            BattleSettingsAsset asset = CopyAssetIfMissing(source, BattleSettingsPath, out bool created);
            if (!created) return asset;
            SerializedObject data = new SerializedObject(asset);
            data.FindProperty("enableEnemyBehavior").boolValue = true;
            data.FindProperty("enemySensingRadius").floatValue = 8;
            data.FindProperty("enemyObservationMemorySeconds").floatValue = 3;
            data.FindProperty("enemyEvaluationIntervalSeconds").floatValue = 0.5f;
            data.FindProperty("enemyMinimumTargetLockSeconds").floatValue = 3;
            data.FindProperty("enemySwitchScoreRatio").floatValue = 1.35f;
            data.FindProperty("enemyMaximumCandidates").intValue = 6;
            data.FindProperty("enemyMaximumEvaluationsPerTick").intValue = 20;
            data.FindProperty("enemyMaximumPathBuildsPerTick").intValue = 2;
            data.FindProperty("enemyMaximumAdditionalTravelCells").intValue = 6;
            SerializedProperty waves = data.FindProperty("waves");
            BattleSettings original = source.CreateSettings();
            int entranceCount = Math.Max(1, original.SpawnPoints.Count);
            int spawnCursor = 0;
            for (int index = 0; index < waves.arraySize; index++)
            {
                WaveDefinition wave = original.Waves[index];
                bool[] heavyEntrances = new bool[entranceCount];
                // 出生游标跨波持续，固定入口的波次同样推进它。交替入口并不保证重装两侧都有。
                for (int spawn = 0; spawn < wave.EnemyCount; spawn++)
                {
                    int entrance = wave.EntranceIndex < 0 ? spawnCursor : wave.EntranceIndex % entranceCount;
                    if (wave.EnemyPattern[spawn % wave.EnemyPattern.Count] == EnemyKind.Heavy) heavyEntrances[entrance] = true;
                    spawnCursor = (spawnCursor + 1) % entranceCount;
                }
                string entranceText = HeavyEntranceDescription(original, heavyEntrances);
                if (entranceText.Length == 0) continue;
                string advice = index % 3 == 0 ? "保留维修资金，关注实际到货。" :
                    index % 3 == 1 ? "备用供电应避开同一薄弱节点。" : "保护中继的接近路线。";
                waves.GetArrayElementAtIndex(index).FindPropertyRelative("briefing").stringValue =
                    entranceText + "有重装来袭，会攻击附近暴露的供电设施。" + advice;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static string HeavyEntranceDescription(BattleSettings settings, bool[] entrances)
        {
            if (settings.SpawnPoints.Count == 2 && settings.SpawnPoints[0].Z != settings.SpawnPoints[1].Z)
            {
                if (entrances[0] && entrances[1]) return "南北两侧";
                int south = settings.SpawnPoints[0].Z < settings.SpawnPoints[1].Z ? 0 : 1;
                return entrances[south] ? "南侧" : entrances[1 - south] ? "北侧" : "";
            }
            string description = "";
            for (int index = 0; index < entrances.Length; index++)
                if (entrances[index]) description += (description.Length > 0 ? "、" : "") + "第 " + (index + 1) + " 入口";
            return description;
        }

        private static EnemyView CopyEnemy(EnemyView source)
        {
            if (source == null) throw new InvalidOperationException("第三阶段敌人 Prefab 引用为空。");
            GameObject asset = CopyAssetIfMissing(source.gameObject, EnemyPath, out bool created);
            if (!created) return asset.GetComponent<EnemyView>();
            GameObject contents = PrefabUtility.LoadPrefabContents(EnemyPath);
            try
            {
                EnemyView view = contents.GetComponent<EnemyView>();
                SerializedObject data = new SerializedObject(view);
                Renderer body = (Renderer)data.FindProperty("bodyRenderer").objectReferenceValue;
                if (body == null) throw new InvalidOperationException("敌人 Prefab 缺少躯体 Renderer。");
                Transform facing = body.transform.parent;
                Material directionMaterial = MaterialAsset("PhaseFourEnemyDirection", new Color(0.9f, 0.55f, 1), true);
                CreateShape("朝向标记", PrimitiveType.Cube, facing, new Vector3(0, 0.64f, 0.35f),
                    new Vector3(0.13f, 0.1f, 0.42f), directionMaterial);
                GameObject strike = CreateShape("实际攻击闪光", PrimitiveType.Cube, facing, new Vector3(0, 0.58f, 0.72f),
                    new Vector3(0.36f, 0.2f, 0.14f), MaterialAsset("PhaseFourEnemyStrike", new Color(1, 0.27f, 0.15f), true));
                strike.SetActive(false);
                view.ConfigureBehaviorVisuals(facing, strike);
                contents.name = "第四阶段敌人";
                PrefabUtility.SaveAsPrefabAsset(contents, EnemyPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            return RequireAsset<GameObject>(EnemyPath).GetComponent<EnemyView>();
        }

        private static EnemyTargetView CreateTargetMarker()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(TargetMarkerPath);
            if (existing != null) return existing.GetComponent<EnemyTargetView>();
            GameObject root = new GameObject("重装目标标记", typeof(EnemyTargetView));
            try
            {
                Material material = MaterialAsset("PhaseFourEnemyTarget", Color.white, true);
                Transform locked = Child("锁定方框", root.transform);
                Transform search = Child("搜索旧位置", root.transform);
                // 方框与十字均为保存的普通几何，不是运行时 UI 或动态线网。
                for (int side = -1; side <= 1; side += 2)
                {
                    CreateShape("横边 " + side, PrimitiveType.Cube, locked, new Vector3(0, 0, side * 0.67f),
                        new Vector3(1.42f, 0.065f, 0.09f), material);
                    CreateShape("竖边 " + side, PrimitiveType.Cube, locked, new Vector3(side * 0.67f, 0, 0),
                        new Vector3(0.09f, 0.065f, 1.42f), material);
                }
                CreateShape("搜索横线", PrimitiveType.Cube, search, Vector3.zero, new Vector3(1.35f, 0.065f, 0.1f), material);
                CreateShape("搜索竖线", PrimitiveType.Cube, search, Vector3.zero, new Vector3(0.1f, 0.065f, 1.35f), material);
                search.localRotation = Quaternion.Euler(0, 45, 0);
                root.GetComponent<EnemyTargetView>().Configure(locked.gameObject, locked.GetComponentsInChildren<Renderer>(),
                    search.gameObject, search.GetComponentsInChildren<Renderer>());
                search.gameObject.SetActive(false);
                return PrefabUtility.SaveAsPrefabAsset(root, TargetMarkerPath).GetComponent<EnemyTargetView>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static BattleControlPanel CreatePanelFromScene(BattleControlPanel panel, Scene scene)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            if (existing != null)
            {
                // 若资产已独立制作，保留其后续修改；只在新复制场景内替换旧面板实例。
                var replacement = (GameObject)PrefabUtility.InstantiatePrefab(existing, scene);
                RectTransform viewport = replacement.transform.Find("MapViewport").GetComponent<RectTransform>();
                RebindViewports(scene, panel.transform.Find("MapViewport"), viewport);
                Object.DestroyImmediate(panel.gameObject);
                return replacement.GetComponent<BattleControlPanel>();
            }
            if (PrefabUtility.IsPartOfPrefabInstance(panel.gameObject))
                PrefabUtility.UnpackPrefabInstance(panel.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            panel.name = "PhaseFourPanel";
            SerializedObject panelData = new SerializedObject(panel);
            Text details = (Text)panelData.FindProperty("selectionText").objectReferenceValue;
            Font font = details.font;
            var strip = Panel("EnemyIntentPanel", panel.transform, 302, 194, 910, 78, new Color(0.09f, 0.075f, 0.14f));
            Text intent = Label("EnemyIntent", strip, font, "重装目标：紫色锁定 · 红色实际出手 · 琥珀色搜索旧位置", 14, 12, 882, 54,
                16, new Color(0.95f, 0.8f, 1));
            PlaceRectangle(intent.rectTransform, 14, 12, 882, 54);
            intent.fontSize = 16;
            intent.verticalOverflow = VerticalWrapMode.Truncate;
            SetReference(panelData, "enemyIntentText", intent);
            panelData.ApplyModifiedPropertiesWithoutUndo();
            PlaceRectangle(panel.transform.Find("MapViewport").GetComponent<RectTransform>(), 290, 280, 934, 510);
            Text title = panel.transform.Find("MapTitle").GetComponent<Text>();
            title.text = "能源防线";
            Text help = panel.transform.Find("MapHelp").GetComponent<Text>();
            help.text = "西侧双入口 · 保护供电节点 · 滚轮缩放 · 中键拖动";
            PrefabUtility.SaveAsPrefabAssetAndConnect(panel.gameObject, PanelPath, InteractionMode.AutomatedAction);
            return panel;
        }

        private static void RebindViewports(Scene scene, Transform oldViewport, RectTransform newViewport)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (WorldCameraViewport viewport in root.GetComponentsInChildren<WorldCameraViewport>(true))
                {
                    SerializedObject data = new SerializedObject(viewport);
                    if (data.FindProperty("mapViewport").objectReferenceValue != oldViewport) continue;
                    SetReference(data, "mapViewport", newViewport);
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
        }

        private static T RequireSingle<T>(Scene scene) where T : Component
        {
            T result = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (T candidate in root.GetComponentsInChildren<T>(true))
                {
                    if (result != null) throw new InvalidOperationException("复制场景包含多个 " + typeof(T).Name + "。");
                    result = candidate;
                }
            if (result == null) throw new InvalidOperationException("复制场景缺少 " + typeof(T).Name + "。");
            return result;
        }

        private static T CopyAssetIfMissing<T>(T source, string destination, out bool created) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(destination);
            created = existing == null;
            if (existing != null) return existing;
            if (source == null) throw new InvalidOperationException("复制来源不存在：" + destination);
            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath) || !AssetDatabase.CopyAsset(sourcePath, destination))
                throw new InvalidOperationException("无法复制资源：" + sourcePath + " → " + destination);
            return RequireAsset<T>(destination);
        }

        private static T RequireAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("缺少必需资源：" + path);
            return asset;
        }
    }
}
