from pathlib import Path
import json
root=Path.cwd()
def edit(path,old,new):
 p=root/path;s=p.read_text(encoding='utf-8-sig');assert old in s,(path,old[:100]);p.write_text(s.replace(old,new),encoding='utf-8',newline='\n')
world='Assets/Defend/Scripts/Simulation/World/GameWorld.cs'
edit(world,'private long nextBuildingId = 1;', 'private readonly List<WorldEventSnapshot> events = new List<WorldEventSnapshot>();\n        public IReadOnlyList<WorldEventSnapshot> Events { get; }\n        internal void Record(WorldEventKind kind, long id, double x, double z, int amount = 0)\n            => events.Add(new WorldEventSnapshot(kind, id, x, z, amount));\n        private long nextBuildingId = 1;')
edit(world,'Buildings = buildings.AsReadOnly();','Buildings = buildings.AsReadOnly();\n            Events = events.AsReadOnly();')
edit(world,'CurrentTick = checked(CurrentTick + 1);','events.Clear();\n            CurrentTick = checked(CurrentTick + 1);')
edit(world,'healthDamage = (int)(((long)amount * 1000 - absorbed + 999) / 1000);','healthDamage = (int)(((long)amount * 1000 - absorbed + 999) / 1000);\n                if (absorbed > 0)\n                {\n                    Record(WorldEventKind.ShieldHit, building.Id, building.CenterX, building.CenterZ);\n                    if (building.StoredEnergy == 0) Record(WorldEventKind.ShieldBreak, building.Id, building.CenterX, building.CenterZ);\n                }')
edit(world,'building.Health = Math.Max(0, building.Health - healthDamage);','if (healthDamage > 0) Record(WorldEventKind.BuildingHit, building.Id, building.CenterX, building.CenterZ, Math.Min(healthDamage, building.Health));\n            building.Health = Math.Max(0, building.Health - healthDamage);')
edit(world,'building.DestructionCount++;','building.DestructionCount++;\n                Record(WorldEventKind.BuildingDestroyed, building.Id, building.CenterX, building.CenterZ);')
edit(world,'building.LastRepairTick = CurrentTick;','building.LastRepairTick = CurrentTick;\n            Record(WorldEventKind.Repair, building.Id, building.CenterX, building.CenterZ);')
battle='Assets/Defend/Scripts/Simulation/Combat/BattleSystem.cs'
edit(battle,'victim.Health -= damage;','victim.Health -= damage;\n                    world.Record(WorldEventKind.EnemyHit, victim.Id, victim.X, victim.Z, damage);\n                    if (!victim.IsAlive) world.Record(WorldEventKind.EnemyDeath, victim.Id, victim.X, victim.Z);')
trans='Assets/Defend/Scripts/Simulation/Energy/EnergyTransport.cs'
edit(trans,'internal Action<BuildingState, BuildingState, long, long> TransferCompleted;', 'internal Action<BuildingState, BuildingState, long, long> TransferCompleted;\n        private readonly List<EnergyTransferSnapshot> departures = new List<EnergyTransferSnapshot>();\n        private readonly List<EnergyTransferSnapshot> deliveries = new List<EnergyTransferSnapshot>();\n        public IReadOnlyList<EnergyTransferSnapshot> Departures { get; }\n        public IReadOnlyList<EnergyTransferSnapshot> Deliveries { get; }\n        public void BeginStep() { departures.Clear(); deliveries.Clear(); }')
edit(trans,'Snapshots = snapshots.AsReadOnly();','Snapshots = snapshots.AsReadOnly();\n            Departures = departures.AsReadOnly();\n            Deliveries = deliveries.AsReadOnly();')
edit(trans,'arrivals.Add(batch);','arrivals.Add(batch);\n            departures.Add(new EnergyTransferSnapshot(batch));')
edit(trans,'target.TotalReceived = checked(target.TotalReceived + batch.Amount);','target.TotalReceived = checked(target.TotalReceived + batch.Amount);\n                deliveries.Add(new EnergyTransferSnapshot(batch));')
energy='Assets/Defend/Scripts/Simulation/Energy/EnergySystem.cs'
edit(energy,'public int ActiveBatchCount', 'public IReadOnlyList<EnergyTransferSnapshot> Departures => transport.Departures;\n        public IReadOnlyList<EnergyTransferSnapshot> Deliveries => transport.Deliveries;\n        public int ActiveBatchCount')
edit(energy,'lastTick = tick;','lastTick = tick;\n            transport.BeginStep();')
for path in ['Assets/Defend/Scripts/Unity/Defend.Unity.asmdef','Assets/Defend/Scripts/Editor/Defend.Editor.asmdef']:
 p=root/path;d=json.loads(p.read_text());d['references'].append('Defend.PhaseFiveEffects');p.write_text(json.dumps(d,indent=2)+'\n')
pool='Assets/Defend/Effects/Runtime/Shared/EffectPool.cs'
edit(pool,'TryPlay(string id, EffectRequest request)','TryPlay(string id, EffectRequest request, float duration = 0)')
edit(pool,'instance.Begin(request);','// 运输时长由真实出发与到达逻辑步决定；归还后下次使用恢复 Prefab 时长。\n            instance.Lifetime = duration > 0 ? duration : bucket.Prefab.Lifetime;\n            instance.Begin(request);')
bv='Assets/Defend/Scripts/Unity/Presentation/BuildingView.cs'
edit(bv,'public BuildingDefinitionAsset Definition => definition;','public BuildingDefinitionAsset Definition => definition;\n        public Transform VisualRoot => visualRoot;')
wp='Assets/Defend/Scripts/Unity/Presentation/WorldPresentation.cs'
edit(wp,'[SerializeField] private BuildingView[] buildingPrefabs;','[SerializeField] private BuildingView[] buildingPrefabs;\n        [SerializeField] private bool externalTransferEffects;\n        public BuildingView FindView(long id) => buildingViews.TryGetValue(id, out var view) ? view : null;')
edit(wp,'RefreshTransfers(world, displayTick);','if (!externalTransferEffects) RefreshTransfers(world, displayTick);')
ev='Assets/Defend/Scripts/Unity/Presentation/EnemyView.cs'
edit(ev,'private MaterialPropertyBlock bodyProperties;','[SerializeField] private bool industrialModel;\n        public EnemyKind Kind { get; private set; }\n        public Transform ModelRoot => facingRoot;\n        private MaterialPropertyBlock bodyProperties;')
edit(ev,'Vector3 nextPosition =','Kind = state.Kind;\n            Vector3 nextPosition =')
edit(ev,'transform.localScale = state.Kind == EnemyKind.Heavy ?', 'transform.localScale = industrialModel ? Vector3.one : state.Kind == EnemyKind.Heavy ?')
edit(ev,'if (bodyRenderer != null)','if (bodyRenderer != null && !industrialModel)')
edit(ev,'(attacking ? direction.normalized * 0.16f : Vector3.zero)','(attacking && !industrialModel ? direction.normalized * 0.16f : Vector3.zero)')
bp='Assets/Defend/Scripts/Unity/Presentation/BattlePresentation.cs'
edit(bp,'[SerializeField] private EnemyView enemyPrefab;','[SerializeField] private EnemyView enemyPrefab;\n        [SerializeField] private EnemyView fastEnemyPrefab;\n        [SerializeField] private EnemyView heavyEnemyPrefab;\n        [SerializeField] private bool externalCombatEffects;\n        private readonly Dictionary<EnemyKind, ObjectPool<EnemyView>> variantPools = new Dictionary<EnemyKind, ObjectPool<EnemyView>>();\n        public EnemyView FindView(long id) => enemyViews.TryGetValue(id, out var view) ? view : null;\n        private ObjectPool<EnemyView> Pool(EnemyKind kind) => variantPools.TryGetValue(kind, out var value) ? value : enemyPool;\n        private void AddVariant(EnemyKind kind, EnemyView prefab)\n        {\n            if (prefab == null || variantPools.ContainsKey(kind)) return;\n            variantPools.Add(kind, new ObjectPool<EnemyView>(() => Instantiate(prefab, enemyRoot),\n                view => { view.ResetForReuse(); view.gameObject.SetActive(true); }, view => view.gameObject.SetActive(false),\n                view => { if (view != null) Destroy(view.gameObject); }, false, 16, 256));\n        }')
edit(bp,'foreach (EnemyView view in enemyViews.Values) enemyPool.Release(view);','AddVariant(EnemyKind.Fast, fastEnemyPrefab);\n            AddVariant(EnemyKind.Heavy, heavyEnemyPrefab);\n            foreach (EnemyView view in enemyViews.Values) Pool(view.Kind).Release(view);')
edit(bp,'view = enemyPool.Get();','view = Pool(state.Kind).Get();')
edit(bp,'enemyPool.Release(item.Value);','Pool(item.Value.Kind).Release(item.Value);')
edit(bp,'RefreshTargetMarkers(world);','if (externalCombatEffects) return;\n            RefreshTargetMarkers(world);')
edit(bp,'private void OnDestroy() { enemyPool?.Clear();','private void OnDestroy() { foreach (var pool in variantPools.Values) pool.Clear(); enemyPool?.Clear();')
print('Simulation result records and optional presentation bindings written')
