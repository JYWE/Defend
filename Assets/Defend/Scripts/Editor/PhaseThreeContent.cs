using System.Collections.Generic;
using Defend.Simulation;
using Defend.Unity;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static Defend.Editor.WhiteboxSceneBuilder;

namespace Defend.Editor
{
    /// <summary>第三阶段资源仅在首次制作时赋值，后续打开保留 Inspector 和场景手工修改。</summary>
    public static class PhaseThreeContent
    {
        public const string Definitions = "Assets/Defend/Content/Definitions/PhaseThree";
        public static BuildingDefinitionAsset CreateBuilding(BuildingKind kind)
        {
            EnsureFolder(Definitions);
            string path = Definitions + "/" + kind + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<BuildingDefinitionAsset>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<BuildingDefinitionAsset>();
            int index = (int)kind;
            string[] names = { "反应堆", "中继器", "连射炮", "核心", "重击炮", "范围炮" };
            int[] costs = { 220, 80, 140, 0, 230, 240 };
            long[] capacities = { 60000, 24000, 18000, 80000, 32000, 24000 };
            asset.ConfigureWhitebox(kind, names[index], capacities[index], kind == BuildingKind.Reactor ? 600 : 0,
                0, kind == BuildingKind.Reactor ? 4000 : kind == BuildingKind.Relay ? 2400 : 0, 4,
                costs[index], costs[index] / 2, kind == BuildingKind.Core ? 120 : 40);
            var data = new SerializedObject(asset);
            data.FindProperty("maximumHealth").intValue = kind == BuildingKind.Core ? 700 : kind == BuildingKind.Reactor ? 220 : 180;
            data.FindProperty("transmissionRange").floatValue = kind == BuildingKind.Reactor ? 7 : 8;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static BuildingView CreateBuildingPrefab(BuildingDefinitionAsset definition)
        {
            const string directory = "Assets/Defend/Content/Prefabs/PhaseThree";
            EnsureFolder(directory);
            string path = directory + "/" + definition.Kind + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<BuildingView>();
            bool special = definition.Kind == BuildingKind.BurstTurret || definition.Kind == BuildingKind.AreaTurret;
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Defend/Content/Prefabs/Buildings/" + (special ? BuildingKind.Turret : definition.Kind) + ".prefab");
            var root = Object.Instantiate(template);
            root.name = definition.DisplayName;
            var view = root.GetComponent<BuildingView>();
            var data = new SerializedObject(view);
            SetReference(data, "definition", definition);
            var marker = CreateShape("状态指示灯", PrimitiveType.Sphere, root.transform, new Vector3(0, 1.72f, 0), Vector3.one * 0.18f,
                MaterialAsset("PhaseThreeStatus", Color.white, true));
            SetReference(data, "feedbackRenderer", marker.GetComponent<Renderer>());
            data.ApplyModifiedPropertiesWithoutUndo();
            if (special)
            {
                var visual = root.transform.Find("VisualRoot · 替换美术入口");
                Color color = definition.Kind == BuildingKind.BurstTurret ? new Color(0.95f, 0.3f, 0.6f) : new Color(0.3f, 0.75f, 0.95f);
                var material = MaterialAsset("PhaseThree" + definition.Kind, color);
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = material;
                if (definition.Kind == BuildingKind.BurstTurret)
                    CreateShape("重型炮管", PrimitiveType.Cube, visual, new Vector3(0, 0.86f, 0.35f), new Vector3(0.3f, 0.22f, 0.85f), material);
                else CreateShape("环形发射器", PrimitiveType.Cylinder, visual, new Vector3(0, 0.97f, 0), new Vector3(0.88f, 0.12f, 0.88f), material);
            }
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<BuildingView>();
        }

        public static LineRenderer CreateRange(string name, Transform parent)
        {
            var line = new GameObject(name, typeof(LineRenderer)).GetComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.sharedMaterial = MaterialAsset("PhaseThreeRange", new Color(0.3f, 0.75f, 0.65f), true);
            line.positionCount = 65; line.startWidth = line.endWidth = 0.04f;
            line.useWorldSpace = true;
            line.gameObject.SetActive(false);
            return line;
        }

        public static BattleSettingsAsset CreateBattleSettings()
        {
            string path = Definitions + "/TwinPassBattleSettings.asset";
            var existing = AssetDatabase.LoadAssetAtPath<BattleSettingsAsset>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<BattleSettingsAsset>();
            var data = new SerializedObject(asset);
            data.FindProperty("enemySpeedCellsPerSecond").floatValue = 1.2f;
            data.FindProperty("enemyAttackDamage").intValue = 10;
            var weapons = data.FindProperty("turretDefinitions"); weapons.arraySize = 3;
            Weapon(weapons.GetArrayElementAtIndex(0), BuildingKind.Turret, 7, 18, 1200, 6, 0);
            Weapon(weapons.GetArrayElementAtIndex(1), BuildingKind.BurstTurret, 9, 130, 8000, 40, 0);
            Weapon(weapons.GetArrayElementAtIndex(2), BuildingKind.AreaTurret, 7.5f, 42, 6000, 32, 2.4f);
            var waves = data.FindProperty("waves"); waves.arraySize = 8;
            int[] counts = { 24, 32, 36, 42, 48, 56, 64, 72 };
            int[] health = { 80, 95, 105, 115, 125, 135, 150, 165 };
            int[] intervals = { 50, 44, 40, 36, 32, 28, 24, 22 };
            int[] rewards = { 240, 280, 320, 360, 400, 440, 480, 600 };
            int[] entrances = { 0, 1, 0, 1, -1, -1, -1, -1 };
            EnemyKind[][] patterns = {
                new[] { EnemyKind.Normal }, new[] { EnemyKind.Normal, EnemyKind.Fast, EnemyKind.Fast },
                new[] { EnemyKind.Heavy, EnemyKind.Normal, EnemyKind.Normal }, new[] { EnemyKind.Normal, EnemyKind.Normal, EnemyKind.Fast },
                new[] { EnemyKind.Fast, EnemyKind.Heavy, EnemyKind.Normal }, new[] { EnemyKind.Normal, EnemyKind.Normal, EnemyKind.Heavy },
                new[] { EnemyKind.Fast, EnemyKind.Fast, EnemyKind.Heavy }, new[] { EnemyKind.Heavy, EnemyKind.Normal, EnemyKind.Fast, EnemyKind.Normal }
            };
            string[] briefings = {
                "南侧试探：先给连射炮充能。", "北侧快袭：补足北侧持续火力。", "南侧重装：重击炮能击穿高耐久目标。",
                "北侧集群：范围炮适合密集通道。", "双路来袭：扩建供电，分配两侧优先级。", "持续压迫：关注中继滞留和实际到货。",
                "快速突击：保留维修资金，避免供电中断。", "最后攻势：两侧混合敌群，检查备用线路。"
            };
            for (int index = 0; index < 8; index++)
            {
                var wave = waves.GetArrayElementAtIndex(index);
                wave.FindPropertyRelative("enemyCount").intValue = counts[index];
                wave.FindPropertyRelative("enemyHealth").intValue = health[index];
                wave.FindPropertyRelative("spawnIntervalTicks").intValue = intervals[index];
                wave.FindPropertyRelative("clearReward").intValue = rewards[index];
                wave.FindPropertyRelative("entranceIndex").intValue = entrances[index];
                wave.FindPropertyRelative("briefing").stringValue = briefings[index];
                var pattern = wave.FindPropertyRelative("enemyPattern"); pattern.arraySize = patterns[index].Length;
                for (int item = 0; item < pattern.arraySize; item++) pattern.GetArrayElementAtIndex(item).enumValueIndex = (int)patterns[index][item];
            }
            var entrancesData = data.FindProperty("spawnPoints"); entrancesData.arraySize = 2;
            entrancesData.GetArrayElementAtIndex(0).vector2IntValue = new Vector2Int(0, 10);
            entrancesData.GetArrayElementAtIndex(1).vector2IntValue = new Vector2Int(0, 30);
            var terrain = new List<Vector2Int>();
            for (int z = 0; z < 40; z++)
                if (z < 8 || (z > 12 && z < 28) || z > 32)
                    for (int x = 11; x <= 12; x++) terrain.Add(new Vector2Int(x, z));
            for (int z = 11; z <= 14; z++) terrain.Add(new Vector2Int(24, z));
            for (int z = 26; z <= 29; z++) terrain.Add(new Vector2Int(24, z));
            var cells = data.FindProperty("blockedCells"); cells.arraySize = terrain.Count;
            for (int index = 0; index < terrain.Count; index++) cells.GetArrayElementAtIndex(index).vector2IntValue = terrain[index];
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Weapon(SerializedProperty item, BuildingKind kind, float range, int damage, long energy, int cooldown, float radius)
        {
            item.FindPropertyRelative("kind").enumValueIndex = (int)kind;
            item.FindPropertyRelative("range").floatValue = range;
            item.FindPropertyRelative("damage").intValue = damage;
            item.FindPropertyRelative("energyPerShot").longValue = energy;
            item.FindPropertyRelative("cooldownTicks").intValue = cooldown;
            item.FindPropertyRelative("splashRadius").floatValue = radius;
        }
    }
}
