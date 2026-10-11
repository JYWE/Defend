using System;
using System.Collections.Generic;
using Defend.Simulation;
using Defend.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using static Defend.Editor.WhiteboxSceneBuilder;

namespace Defend.Editor
{
    /// <summary>只创建缺失的第二阶段资产。已有场景和预制体保留开发者手工修改。</summary>
    public static class PhaseThreeSceneBuilder
    {
        public const string CombatScenePath = "Assets/Defend/Content/Scenes/Dev/TwinPassDefense.unity";
        public const string PanelPath = "Assets/Defend/Content/Prefabs/UI/PhaseThreePanel.prefab";
        private const string Content = "Assets/Defend/Content";
        private static readonly Color TextColor = new Color(0.88f, 0.94f, 0.98f);
        private static readonly Color MutedColor = new Color(0.63f, 0.73f, 0.8f);
        private static readonly Color Accent = new Color(0.27f, 0.87f, 0.73f);
        private static readonly Color PanelColor = new Color(0.05f, 0.08f, 0.11f);
        private static readonly Color ButtonColor = new Color(0.12f, 0.23f, 0.3f);

        [MenuItem("涌现防线/创建或打开双隘防线关卡")]
        public static void CreateOrOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请退出播放模式后制作战斗场景。");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CombatScenePath) != null)
            {
                EditorSceneManager.OpenScene(CombatScenePath);
                Debug.Log("保留已有战斗白盒场景和手工修改：" + CombatScenePath);
                return;
            }
            EnsureFolder(Content + "/Definitions/PhaseThree");
            EnsureFolder(Content + "/Prefabs/Enemies");
            EnsureFolder(Content + "/Prefabs/VFX");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "CombatWhitebox";
            BuildingKind[] kinds = { BuildingKind.Reactor, BuildingKind.Relay, BuildingKind.Turret, BuildingKind.Core, BuildingKind.BurstTurret, BuildingKind.AreaTurret };
            BuildingDefinitionAsset[] definitions = new BuildingDefinitionAsset[6];
            BuildingView[] prefabs = new BuildingView[6];
            for (int index = 0; index < kinds.Length; index++)
            {
                definitions[index] = PhaseThreeContent.CreateBuilding(kinds[index]);
                prefabs[index] = PhaseThreeContent.CreateBuildingPrefab(definitions[index]);
            }
            EnergyTransferView transfer = RequireAsset<GameObject>(Content + "/Prefabs/VFX/EnergyTransfer.prefab").GetComponent<EnergyTransferView>();
            LineRenderer connection = RequireAsset<GameObject>(Content + "/Prefabs/VFX/EnergyConnection.prefab").GetComponent<LineRenderer>();
            EnergyExperimentSettings energySettings = CreateEnergySettings();
            BattleSettingsAsset battleSettings = PhaseThreeContent.CreateBattleSettings();
            Font font = RequireAsset<Font>(ChineseFontPath);
            BattleControlPanel panelPrefab = CreatePanel(font);
            EnemyView enemyPrefab = CreateEnemy();
            LineRenderer shotPrefab = CreateShot();

            RenderSettings.ambientLight = new Color(0.72f, 0.76f, 0.83f);
            RenderSettings.fog = false;
            Transform map = new GameObject("战场 · 40 × 40").transform;
            CreateShape("Ground", PrimitiveType.Cube, map, new Vector3(20, -0.15f, 20), new Vector3(40, 0.2f, 40),
                MaterialAsset("Ground", new Color(0.065f, 0.095f, 0.125f)));
            CreateGrid(map, MaterialAsset("Grid", new Color(0.11f, 0.17f, 0.21f), true));
            Material entryMaterial = MaterialAsset("EnemyEntrance", new Color(0.74f, 0.24f, 0.26f), true);
            foreach (var point in battleSettings.CreateSettings().SpawnPoints)
                CreateShape("敌人入口", PrimitiveType.Cube, map, new Vector3(point.X + 0.5f, 0.03f, point.Z + 0.5f), new Vector3(1, 0.08f, 1), entryMaterial);
            foreach (var cell in battleSettings.CreateSettings().BlockedCells)
                CreateShape("岩体 " + cell.X + "," + cell.Z, PrimitiveType.Cube, map, new Vector3(cell.X + 0.5f, 0.35f, cell.Z + 0.5f),
                    new Vector3(1, 0.7f, 1), MaterialAsset("PhaseThreeRock", new Color(0.21f, 0.25f, 0.29f)));
            Camera camera = new GameObject("主摄像机 · 70度斜俯视", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.rotation = Quaternion.Euler(70, 0, 0);
            // 让中心射线准确落在地图中央，避免把摄像机坐标误当作地面焦点。
            camera.transform.position = new Vector3(20, 0, 20) - camera.transform.forward * 50;
            camera.orthographic = true;
            camera.orthographicSize = 23;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.041f, 0.061f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 120;
            WhiteboxBackgroundMigration.EnsureBackgroundCamera(scene, camera);
            Light light = new GameObject("白盒定向光", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);

            Transform buildings = new GameObject("建筑 · 可手动编辑初始布局").transform;
            List<BuildingView> placed = new List<BuildingView>
            {
                PlaceAuthored(prefabs[0], buildings, 30, 18),
                PlaceAuthored(prefabs[1], buildings, 26, 18), PlaceAuthored(prefabs[1], buildings, 26, 23),
                PlaceAuthored(prefabs[2], buildings, 24, 17), PlaceAuthored(prefabs[2], buildings, 24, 24),
                PlaceAuthored(prefabs[3], buildings, 30, 20)
            };
            Transform energy = new GameObject("能量运输表现").transform;
            Transform connections = new GameObject("供电连接表现").transform;
            Transform enemies = new GameObject("敌人表现").transform;
            Transform shots = new GameObject("射击表现").transform;
            GameObject preview = CreateShape("放置预览", PrimitiveType.Cube, map, Vector3.zero, new Vector3(0.95f, 0.08f, 0.95f),
                MaterialAsset("PlacementPreview", Accent, true));
            preview.SetActive(false);
            EventSystem eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            GameObject panelObject = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab.gameObject);
            BattleControlPanel panel = panelObject.GetComponent<BattleControlPanel>();
            WorldCameraViewport viewport = camera.gameObject.AddComponent<WorldCameraViewport>();
            viewport.Configure(camera, panel.transform.Find("MapViewport").GetComponent<RectTransform>());
            SerializedObject viewportData = new SerializedObject(viewport);
            viewportData.FindProperty("zoomFactor").floatValue = 1f;
            viewportData.ApplyModifiedPropertiesWithoutUndo();
            GameObject rootObject = new GameObject("GameSessionRoot · 战斗组合入口");
            WorldPresentation worldPresentation = rootObject.AddComponent<WorldPresentation>();
            worldPresentation.Configure(prefabs, placed.ToArray(), transfer, connection, buildings, energy, connections, preview.transform, preview.GetComponent<Renderer>());
            worldPresentation.ConfigureRanges(PhaseThreeContent.CreateRange("选中范围", map), PhaseThreeContent.CreateRange("预览范围", map));
            BattlePresentation battlePresentation = rootObject.AddComponent<BattlePresentation>();
            battlePresentation.Configure(enemyPrefab, shotPrefab, enemies, shots);
            GridInputController input = rootObject.AddComponent<GridInputController>();
            input.Configure(camera, eventSystem);
            GameSessionRoot session = rootObject.AddComponent<GameSessionRoot>();
            session.Configure(definitions, energySettings, worldPresentation, input, null);
            session.ConfigureBattle(battleSettings, battlePresentation, panel);
            EditorSceneManager.SaveScene(scene, CombatScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = rootObject;
            Debug.Log("战斗白盒场景已保存：" + CombatScenePath + "。UI、镜头与初始建筑均可手动编辑。");
        }

        private static T RequireAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("缺少第一阶段共用资源：" + path);
            return asset;
        }

        private static Text Label(string name, Transform parent, Font font, string content,
            float x, float y, float width, float height, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            // 复用第一阶段字体与控件制作，但不沿用其按节点名限定的旧面板坐标迁移。
            Text label = WhiteboxSceneBuilder.Label(name, parent, font, content, x, y, width, height, size, color, style);
            PlaceRectangle(label.rectTransform, x, y, width, height);
            label.fontSize = size;
            label.verticalOverflow = name == "SelectionDetails" || name == "Feedback" || name == "Outcome"
                ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow;
            return label;
        }

        private static EnergyExperimentSettings CreateEnergySettings()
        {
            string path = Content + "/Definitions/PhaseThree/TwinPassEnergySettings.asset";
            EnergyExperimentSettings existing = AssetDatabase.LoadAssetAtPath<EnergyExperimentSettings>(path);
            if (existing != null) return existing;
            EnergyExperimentSettings settings = ScriptableObject.CreateInstance<EnergyExperimentSettings>();
            SerializedObject serialized = new SerializedObject(settings);
            serialized.FindProperty("enableTestLoad").boolValue = false;
            serialized.FindProperty("startingCredits").intValue = 1500;
            serialized.FindProperty("maximumLossAtRange").floatValue = 0.35f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(settings, path);
            return settings;
        }

        private static EnemyView CreateEnemy()
        {
            string path = Content + "/Prefabs/Enemies/PhaseThreeEnemy.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<EnemyView>();
            GameObject root = new GameObject("WhiteboxEnemy");
            EnemyView view = root.AddComponent<EnemyView>();
            Transform visual = Child("VisualRoot · 替换美术入口", root.transform);
            CreateShape("Body", PrimitiveType.Capsule, visual, new Vector3(0, 0.43f, 0), new Vector3(0.52f, 0.43f, 0.52f),
                MaterialAsset("EnemyBody", new Color(0.9f, 0.25f, 0.24f)));
            CreateShape("HealthBackground", PrimitiveType.Cube, root.transform, new Vector3(0, 1.15f, 0), new Vector3(0.75f, 0.045f, 0.12f),
                MaterialAsset("BuildingBase", new Color(0.13f, 0.19f, 0.24f)));
            Transform fill = CreateShape("HealthFill", PrimitiveType.Cube, root.transform, new Vector3(0, 1.15f, 0), new Vector3(0.72f, 0.055f, 0.12f),
                MaterialAsset("EnemyHealth", new Color(1f, 0.46f, 0.3f), true)).transform;
            view.Configure(fill);
            SerializedObject enemyData = new SerializedObject(view);
            SetReference(enemyData, "bodyRenderer", visual.Find("Body").GetComponent<Renderer>());
            enemyData.ApplyModifiedPropertiesWithoutUndo();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<EnemyView>();
        }

        private static LineRenderer CreateShot()
        {
            string path = Content + "/Prefabs/VFX/TurretShot.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<LineRenderer>();
            GameObject root = new GameObject("TurretShot", typeof(LineRenderer));
            LineRenderer line = root.GetComponent<LineRenderer>();
            line.sharedMaterial = MaterialAsset("TurretShot", new Color(1f, 0.78f, 0.24f), true);
            line.positionCount = 2;
            line.startWidth = 0.07f;
            line.endWidth = 0.035f;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<LineRenderer>();
        }

        private static BattleControlPanel CreatePanel(Font font)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            if (existing != null) return existing.GetComponent<BattleControlPanel>();
            GameObject root = new GameObject("PhaseThreePanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(BattleControlPanel));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform mapViewport = new GameObject("MapViewport", typeof(RectTransform)).GetComponent<RectTransform>();
            mapViewport.SetParent(root.transform, false);
            PlaceRectangle(mapViewport, 290, 188, 934, 602);
            RectTransform header = Panel("Header", root.transform, 24, 20, 1552, 88, PanelColor);
            Label("Title", header, font, "涌现防线", 20, 12, 250, 34, 28, TextColor, FontStyle.Bold);
            Label("Subtitle", header, font, "守住核心，建立你的能源防线", 20, 52, 390, 24, 15, MutedColor);
            Text resources = Label("Resources", header, font, "资金  0     建筑  0 / 100", 445, 14, 500, 28, 20, TextColor);
            Text core = Label("CoreStatus", header, font, "核心耐久与护盾", 445, 52, 1040, 26, 18, Accent);
            Text wave = Label("WaveStatus", header, font, "第 0 / 5 波", 966, 14, 560, 29, 18, TextColor);
            RectTransform shop = Panel("ConstructionPanel", root.transform, 24, 124, 250, 666, PanelColor);
            Label("ShopTitle", shop, font, "建造防线", 18, 18, 215, 32, 23, TextColor, FontStyle.Bold);
            Label("ShopHelp", shop, font, "选择建筑，再单击网格放置。", 18, 59, 215, 48, 15, MutedColor);
            Button[] placements = new Button[5];
            Text[] labels = new Text[5];
            for (int index = 0; index < 5; index++) placements[index] = Button("Build" + index, shop, font,
                new[] { "反应堆", "中继器", "连射炮", "重击炮", "范围炮" }[index], 18, 92 + index * 64, 214, 56, ButtonColor, out labels[index]);
            RectTransform status = Panel("BattleStatus", shop, 12, 428, 226, 226, new Color(0.075f, 0.13f, 0.17f));
            Text outcome = Label("Outcome", status, font, "准备防线", 14, 12, 198, 202, 15, Accent);

            Label("MapTitle", root.transform, font, "双隘防线", 306, 130, 380, 30, 23, TextColor, FontStyle.Bold);
            Label("MapHelp", root.transform, font, "西侧双入口 · 岩带引导通道 · 滚轮缩放 · 中键拖动", 306, 164, 888, 24, 15, MutedColor);
            RectTransform inspector = Panel("BuildingInspector", root.transform, 1240, 124, 336, 666, PanelColor);
            Text title = Label("SelectionTitle", inspector, font, "未选择建筑", 20, 18, 296, 35, 23, TextColor, FontStyle.Bold);
            Image track = Panel("EnergyTrack", inspector, 20, 68, 296, 8, new Color(0.13f, 0.2f, 0.25f)).GetComponent<Image>();
            Image fill = Panel("EnergyFill", track.transform, 0, 0, 296, 8, Accent).GetComponent<Image>();
            WhiteboxEnergyBarAuthoring.Configure(fill);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0;
            Text details = Label("SelectionDetails", inspector, font, "单击建筑查看详情。", 20, 88, 296, 402, 15, TextColor);
            Label("PriorityTitle", inspector, font, "供给优先级", 20, 497, 296, 22, 17, MutedColor);
            Button[] priority = new Button[3];
            for (int index = 0; index < 3; index++) priority[index] = Button("Priority" + index, inspector, font,
                new[] { "低", "中", "高" }[index], 20 + index * 101, 527, 94, 35, ButtonColor, out _);
            Button move = Button("Move", inspector, font, "移动", 20, 573, 142, 34, ButtonColor, out _);
            Button sell = Button("Sell", inspector, font, "出售", 174, 573, 142, 34, ButtonColor, out _);
            Button repair = Button("Repair", inspector, font, "维修", 20, 618, 296, 34, new Color(0.12f, 0.3f, 0.24f), out _);
            RectTransform footer = Panel("BattleControls", root.transform, 24, 806, 1552, 74, PanelColor);
            Button start = Button("StartWave", footer, font, "开始第一波", 16, 14, 166, 46, new Color(0.12f, 0.35f, 0.28f), out Text startLabel);
            Button pause = Button("Pause", footer, font, "暂停", 194, 14, 92, 46, ButtonColor, out Text pauseLabel);
            Button restart = Button("Restart", footer, font, "重新开始", 298, 14, 130, 46, ButtonColor, out _);
            Button cancel = Button("Cancel", footer, font, "取消放置", 440, 14, 130, 46, ButtonColor, out _);
            Text mode = Label("Mode", footer, font, "浏览 / 选择", 594, 12, 300, 25, 16, Accent);
            Label("KeyboardHelp", footer, font, "空格暂停  ·  右键或 Esc 取消", 594, 42, 315, 22, 13, MutedColor);
            Text feedback = Label("Feedback", footer, font, "部署防线，准备迎敌。", 938, 12, 596, 51, 15, TextColor);
            RectTransform result = Panel("ResultPanel", root.transform, 302, 236, 910, 498, PanelColor);
            Text resultDetails = Label("ResultDetails", result, font, "本局结算", 28, 24, 850, 450, 18, TextColor);
            result.gameObject.SetActive(false);
            BattleControlPanel panel = root.GetComponent<BattleControlPanel>();
            SerializedObject data = new SerializedObject(panel);
            SetReference(data, "resultPanel", result.gameObject); SetReference(data, "resultDetails", resultDetails);
            SetReference(data, "resourcesText", resources); SetReference(data, "waveText", wave); SetReference(data, "coreText", core);
            SetReference(data, "selectionTitle", title); SetReference(data, "selectionText", details); SetReference(data, "outcomeText", outcome);
            SetReference(data, "feedbackText", feedback); SetReference(data, "modeText", mode); SetReference(data, "pauseLabel", pauseLabel);
            SetReference(data, "waveButtonLabel", startLabel); SetReference(data, "energyFill", fill);
            SetArray(data, "placementButtons", placements); SetArray(data, "placementLabels", labels); SetArray(data, "priorityButtons", priority);
            SetReference(data, "moveButton", move); SetReference(data, "sellButton", sell); SetReference(data, "repairButton", repair);
            SetReference(data, "pauseButton", pause); SetReference(data, "startWaveButton", start); SetReference(data, "restartButton", restart);
            SetReference(data, "cancelButton", cancel);
            data.ApplyModifiedPropertiesWithoutUndo();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<BattleControlPanel>();
        }
    }
}
