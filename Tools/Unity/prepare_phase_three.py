from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = (root / 'Assets/Defend/Scripts/Editor/CombatWhiteboxSceneBuilder.cs').read_text(encoding='utf-8-sig')
source = source.replace('CombatWhiteboxSceneBuilder', 'PhaseThreeSceneBuilder').replace('CombatWhitebox.unity', 'TwinPassDefense.unity').replace('CombatWhiteboxPanel', 'PhaseThreePanel').replace('创建或打开战斗白盒场景', '创建或打开双隘防线关卡')
source = source.replace('Content + "/Definitions/Combat', 'Content + "/Definitions/PhaseThree')
source = source.replace('WhiteboxBattleSettings.asset', 'TwinPassBattleSettings.asset').replace('CombatEnergySettings.asset', 'TwinPassEnergySettings.asset')
source = source.replace('BuildingKind.Core };', 'BuildingKind.Core, BuildingKind.BurstTurret, BuildingKind.AreaTurret };').replace('new BuildingDefinitionAsset[4]', 'new BuildingDefinitionAsset[6]').replace('new BuildingView[4]', 'new BuildingView[6]')
source = source.replace('definitions[index] = RequireAsset<BuildingDefinitionAsset>(Content + "/Definitions/Buildings/" + kinds[index] + ".asset");', 'definitions[index] = PhaseThreeContent.CreateBuilding(kinds[index]);')
source = source.replace('prefabs[index] = RequireAsset<GameObject>(Content + "/Prefabs/Buildings/" + kinds[index] + ".prefab").GetComponent<BuildingView>();', 'prefabs[index] = PhaseThreeContent.CreateBuildingPrefab(definitions[index]);')
source = source.replace('BattleSettingsAsset battleSettings = LoadOrCreate<BattleSettingsAsset>(Content + "/Definitions/PhaseThree/TwinPassBattleSettings.asset");', 'BattleSettingsAsset battleSettings = PhaseThreeContent.CreateBattleSettings();')
start = source.index('            foreach (Vector3 point in new[]')
end = source.index('            Camera camera', start)
source = source[:start] + '''            foreach (var point in battleSettings.CreateSettings().SpawnPoints)
                CreateShape("敌人入口", PrimitiveType.Cube, map, new Vector3(point.X + 0.5f, 0.03f, point.Z + 0.5f), new Vector3(1, 0.08f, 1), entryMaterial);
            foreach (var cell in battleSettings.CreateSettings().BlockedCells)
                CreateShape("岩体 " + cell.X + "," + cell.Z, PrimitiveType.Cube, map, new Vector3(cell.X + 0.5f, 0.35f, cell.Z + 0.5f),
                    new Vector3(1, 0.7f, 1), MaterialAsset("PhaseThreeRock", new Color(0.21f, 0.25f, 0.29f)));
''' + source[end:]
start = source.index('                PlaceAuthored(prefabs[0]')
end = source.index('            };', start)
source = source[:start] + '''                PlaceAuthored(prefabs[0], buildings, 30, 18),
                PlaceAuthored(prefabs[1], buildings, 26, 18), PlaceAuthored(prefabs[1], buildings, 26, 23),
                PlaceAuthored(prefabs[2], buildings, 24, 17), PlaceAuthored(prefabs[2], buildings, 24, 24),
                PlaceAuthored(prefabs[3], buildings, 30, 20)
''' + source[end:]
source = source.replace('            BattlePresentation battlePresentation', '''            worldPresentation.ConfigureRanges(PhaseThreeContent.CreateRange("选中范围", map), PhaseThreeContent.CreateRange("预览范围", map));
            BattlePresentation battlePresentation''')
source = source.replace('serialized.FindProperty("enableTestLoad").boolValue = false;', 'serialized.FindProperty("enableTestLoad").boolValue = false;\n            serialized.FindProperty("startingCredits").intValue = 1500;\n            serialized.FindProperty("maximumLossAtRange").floatValue = 0.35f;')
source = source.replace('WhiteboxEnemy.prefab', 'PhaseThreeEnemy.prefab').replace('GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);\n            Object.DestroyImmediate(root);\n            return saved.GetComponent<EnemyView>();', '''SerializedObject enemyData = new SerializedObject(view);
            SetReference(enemyData, "bodyRenderer", visual.Find("Body").GetComponent<Renderer>());
            enemyData.ApplyModifiedPropertiesWithoutUndo();
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved.GetComponent<EnemyView>();''')
source = source.replace('new Button[3];\n            Text[] labels = new Text[3];', 'new Button[5];\n            Text[] labels = new Text[5];').replace('index < 3; index++) placements', 'index < 5; index++) placements')
source = source.replace('new[] { "反应堆", "中继器", "炮塔" }[index], 18, 111 + index * 79, 214, 65', 'new[] { "反应堆", "中继器", "连射炮", "重击炮", "范围炮" }[index], 18, 92 + index * 64, 214, 56')
source = source.replace('12, 370, 226, 182', '12, 428, 226, 226').replace('14, 18, 198, 148, 17', '14, 12, 198, 202, 15')
source = source.replace('            Label("EnergyHelp", shop, font, "炮塔需要能量才能射击。\\n连接反应堆与中继器，保持供电。", 18, 576, 214, 73, 15, MutedColor);', '')
source = source.replace('"核心防御区"', '"双隘防线"').replace('"红色标记为来袭方向  ·  滚轮缩放  ·  中键拖动"', '"西侧双入口 · 岩带引导通道 · 滚轮缩放 · 中键拖动"')
source = source.replace('20, 93, 296, 322, 16', '20, 88, 296, 402, 15').replace('20, 423, 296, 26', '20, 497, 296, 22').replace('20 + index * 101, 462, 94, 43', '20 + index * 101, 527, 94, 35').replace('20, 526, 142, 44', '20, 573, 142, 34').replace('174, 526, 142, 44', '174, 573, 142, 34').replace('20, 584, 296, 44', '20, 618, 296, 34')
source = source.replace('            BattleControlPanel panel = root.GetComponent<BattleControlPanel>();', '''            RectTransform result = Panel("ResultPanel", root.transform, 302, 236, 910, 498, PanelColor);
            Text resultDetails = Label("ResultDetails", result, font, "本局结算", 28, 24, 850, 450, 18, TextColor);
            result.gameObject.SetActive(false);
            BattleControlPanel panel = root.GetComponent<BattleControlPanel>();''')
source = source.replace('            SetReference(data, "resourcesText", resources);', '            SetReference(data, "resultPanel", result.gameObject); SetReference(data, "resultDetails", resultDetails);\n            SetReference(data, "resourcesText", resources);')
(root / 'Assets/Defend/Scripts/Editor/PhaseThreeSceneBuilder.cs').write_text(source, encoding='utf-8')
