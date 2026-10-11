using System;
using System.Linq;
using Defend.Unity;
using Defend.PhaseFiveEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class PhaseFiveCombatValidation
{
 public static object CreateScene() { Defend.Editor.PhaseFiveCombatSceneBuilder.CreateOrOpen(); return Inspect(); }
 public static object Inspect() {
  var scene=SceneManager.GetActiveScene();
  var roots=scene.GetRootGameObjects();
  var renderers=roots.SelectMany(x=>x.GetComponentsInChildren<Renderer>(true)).ToArray();
  return new {scene=scene.path,dirty=scene.isDirty, buildings=UnityEngine.Object.FindObjectsByType<BuildingView>(FindObjectsInactive.Include).Length,
   ui=UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Length,
   materialsMissing=renderers.Count(x=>x.sharedMaterials.Any(m=>m==null)), renderers=renderers.Length,
   errors=roots.Sum(r=>r.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))) };
 }
 public static object State() {
  var session=UnityEngine.Object.FindAnyObjectByType<GameSessionRoot>();
  var art=UnityEngine.Object.FindAnyObjectByType<CombatArtPresentation>();
  return new {tick=session.World.CurrentTick,state=session.Battle.State.ToString(),enemies=session.Battle.Enemies.Count,kills=session.Battle.Kills,
   shots=art.ObservedShots,deliveries=art.ObservedDeliveries,deaths=art.ObservedDeaths,repairs=art.ObservedRepairs,
   hits=art.ObservedBuildingHits,shieldHits=art.ObservedShieldHits,shieldBreaks=art.ObservedShieldBreaks,
   active=art.Pool.ActiveCount,particles=art.Pool.ParticleCount,dropped=art.Pool.Dropped,reused=art.Pool.Reused,
   states=art.ActiveStates,transfers=art.ActiveTransfers,paused=session.Paused,seconds=art.PresentationSeconds,
   balance=session.World.Energy.GetTotals().BalanceError,ui=UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include).Length};
 }
 public static object StartWave() { UnityEngine.Object.FindAnyObjectByType<GameSessionRoot>().StartWave(); return State(); }
 public static object Pause() { var s=UnityEngine.Object.FindAnyObjectByType<GameSessionRoot>();if(!s.Paused)s.TogglePause();return State(); }
 public static object Resume() { var s=UnityEngine.Object.FindAnyObjectByType<GameSessionRoot>();if(s.Paused)s.TogglePause();return State(); }
 public static object Screenshot() {System.IO.Directory.CreateDirectory("Logs/PhaseFiveCombat");ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Logs/PhaseFiveCombat/combat-frame.png"));return State();}

 public static object Layout() { var s=UnityEngine.Object.FindAnyObjectByType<GameSessionRoot>(); return s.World.Buildings.Select(b=>new {b.Id,b.X,b.Z,kind=b.Kind.ToString()}).ToArray(); }
 public static object CloseView() { var v=UnityEngine.Object.FindAnyObjectByType<WorldCameraViewport>();var d=new SerializedObject(v);d.FindProperty("zoomFactor").floatValue=.38f;d.ApplyModifiedPropertiesWithoutUndo();return State(); }
 public static object ConfigureDiagnostic() {
  var s=UnityEngine.Object.FindAnyObjectByType<GameSessionRoot>();
  var original=AssetDatabase.LoadAssetAtPath<BattleSettingsAsset>(Defend.Editor.PhaseFourSceneBuilder.BattleSettingsPath);
  var copy=UnityEngine.Object.Instantiate(original);copy.name="第五阶段临时验证配置";
  JsonUtility.FromJsonOverwrite("{\"waves\":[{\"enemyCount\":15,\"enemyHealth\":100,\"spawnIntervalTicks\":8,\"clearReward\":0,\"enemyPattern\":[0,1,2],\"entranceIndex\":0,\"briefing\":\"特效集成检查\"}],\"spawnPoints\":[{\"x\":18,\"y\":16}]}",copy);
  s.ConfigureBattle(copy,UnityEngine.Object.FindAnyObjectByType<BattlePresentation>(),UnityEngine.Object.FindAnyObjectByType<BattleControlPanel>());s.Restart();return State();
 }

 public static object BuyWeapons() {
  var s=UnityEngine.Object.FindAnyObjectByType<GameSessionRoot>();
  s.Submit(Defend.Simulation.WorldCommand.Place(Defend.Simulation.BuildingKind.BurstTurret,24,20));
  s.Submit(Defend.Simulation.WorldCommand.Place(Defend.Simulation.BuildingKind.AreaTurret,24,22));return State();
 }

 public static object SaveDefaults() {
  if(Application.isPlaying) throw new Exception("Exit play mode first");
  if(SceneManager.GetActiveScene().path != Defend.Editor.PhaseFiveCombatSceneBuilder.ScenePath) throw new Exception("Wrong scene");
  var v=UnityEngine.Object.FindAnyObjectByType<WorldCameraViewport>();var d=new SerializedObject(v);d.FindProperty("zoomFactor").floatValue=.55f;d.ApplyModifiedPropertiesWithoutUndo();
  foreach(var t in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include)) {
   if(t.name=="MapTitle"){var data=new SerializedObject(t);data.FindProperty("m_Text").stringValue="工业防线";data.ApplyModifiedPropertiesWithoutUndo();}
  }
  UnityEditor.SceneManagement.EditorSceneManager.SaveScene(SceneManager.GetActiveScene());return Inspect();
 }
 public static object StartProbe() {var p=new GameObject("第五阶段集成检查").AddComponent<PhaseFiveCombatProbe>();p.BeginEditor(System.IO.Path.GetFullPath("Logs/PhaseFiveCombat/EditorProbe"));return State();}

 public static object CaptureCombatDetail() {
  var s=UnityEngine.Object.FindAnyObjectByType<GameSessionRoot>();var art=UnityEngine.Object.FindAnyObjectByType<CombatArtPresentation>();
  if(!s.Paused)s.TogglePause();
  s.Submit(Defend.Simulation.WorldCommand.Place(Defend.Simulation.BuildingKind.BurstTurret,24,20));
  s.Submit(Defend.Simulation.WorldCommand.Place(Defend.Simulation.BuildingKind.AreaTurret,24,22));
  for(int i=0;i<200;i++)s.SingleStep();s.StartWave();
  bool fired=false;
  for(int i=0;i<600;i++) {s.SingleStep();if(s.World.Buildings.Any(b=>b.Kind==Defend.Simulation.BuildingKind.AreaTurret && b.ShotsFired>0)){fired=true;break;}}
  if(!fired)throw new Exception("Area weapon did not fire");s.SingleStep();s.SingleStep();
  var views=UnityEngine.Object.FindAnyObjectByType<BattlePresentation>();views.Refresh(s.World,s.World.CurrentTick+1);
  art.Refresh(s.World.CurrentTick,s.SelectedBuildingId,true,0);
  var camera=Camera.main;var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;var oldSize=camera.orthographicSize;var oldRect=camera.rect;var oldAspect=camera.aspect;
  var target=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);target.Create();
  var previous=RenderTexture.active;var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
  try {
   camera.transform.rotation=Quaternion.Euler(58,0,0);camera.transform.position=new Vector3(24.5f,0,20.5f)-camera.transform.forward*40;camera.orthographicSize=5.7f;camera.rect=new Rect(0,0,1,1);camera.aspect=1.6f;
   UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
   RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,1000),0,0);pixels.Apply();System.IO.File.WriteAllBytes("Logs/PhaseFiveCombat/combat-detail.png",pixels.EncodeToPNG());
  } finally {RenderTexture.active=previous;camera.transform.SetPositionAndRotation(oldPosition,oldRotation);camera.orthographicSize=oldSize;camera.rect=oldRect;camera.aspect=oldAspect;UnityEngine.Object.Destroy(pixels);target.Release();UnityEngine.Object.Destroy(target);}
  return State();
 }

 public static object DenseCheck() {
  var s=UnityEngine.Object.FindAnyObjectByType<GameSessionRoot>();var a=UnityEngine.Object.FindAnyObjectByType<CombatArtPresentation>();
  var data=new SerializedObject(s);var originalEnergy=(EnergyExperimentSettings)data.FindProperty("experimentSettings").objectReferenceValue;
  var energy=UnityEngine.Object.Instantiate(originalEnergy);var ed=new SerializedObject(energy);ed.FindProperty("startingCredits").intValue=100000;ed.ApplyModifiedPropertiesWithoutUndo();
  data.FindProperty("experimentSettings").objectReferenceValue=energy;data.ApplyModifiedPropertiesWithoutUndo();
  var originalBattle=AssetDatabase.LoadAssetAtPath<BattleSettingsAsset>(Defend.Editor.PhaseFourSceneBuilder.BattleSettingsPath);
  var battle=UnityEngine.Object.Instantiate(originalBattle);JsonUtility.FromJsonOverwrite("{\"waves\":[{\"enemyCount\":200,\"enemyHealth\":100000,\"spawnIntervalTicks\":1,\"clearReward\":0,\"enemyPattern\":[0,1,2],\"entranceIndex\":0}],\"spawnPoints\":[{\"x\":18,\"y\":16}]}",battle);
  s.ConfigureBattle(battle,UnityEngine.Object.FindAnyObjectByType<BattlePresentation>(),UnityEngine.Object.FindAnyObjectByType<BattleControlPanel>());s.Restart();s.TogglePause();
  for(int x=23;x<39 && s.World.Buildings.Count<100;x++)for(int z=4;z<37 && s.World.Buildings.Count<100;z++) {
   var kind=s.World.Buildings.Count%5==0?Defend.Simulation.BuildingKind.Reactor:Defend.Simulation.BuildingKind.Relay;
   if(s.World.PlacementIssue(kind,x,z)!=null)continue;s.Submit(Defend.Simulation.WorldCommand.Place(kind,x,z));s.SingleStep();
  }
  s.StartWave();for(int i=0;i<230;i++)s.SingleStep();
  a.Refresh(s.World.CurrentTick,0,true,0);
  s.World.Energy.AssertInvariants();var enemies=UnityEngine.Object.FindObjectsByType<EnemyView>();
  if(s.World.Buildings.Count!=100 || s.Battle.Enemies.Count!=200 || enemies.Length!=200)throw new Exception("Dense population mismatch");
  if(enemies.Any(v=>v.transform.localScale!=Vector3.one))throw new Exception("Enemy scaled twice");
  if(a.Pool.PeakBudget[0]>64 || a.Pool.PeakBudget[1]>128 || a.Pool.PeakBudget[2]>128 || a.Pool.PeakBudget[3]>32)throw new Exception("Effect budget exceeded");
  var report=new {buildings=s.World.Buildings.Count,enemies=enemies.Length,kinds=enemies.GroupBy(v=>v.Kind).Select(g=>new {kind=g.Key.ToString(),count=g.Count()}).ToArray(),effects=a.Pool.ActiveCount,peak=a.Pool.PeakActive,particles=a.Pool.PeakParticles,budgets=a.Pool.PeakBudget,dropped=a.Pool.Dropped,balance=s.World.Energy.GetTotals().BalanceError};
  data.Update();data.FindProperty("experimentSettings").objectReferenceValue=originalEnergy;data.ApplyModifiedPropertiesWithoutUndo();s.ConfigureBattle(originalBattle,UnityEngine.Object.FindAnyObjectByType<BattlePresentation>(),UnityEngine.Object.FindAnyObjectByType<BattleControlPanel>());s.Restart();
  if(UnityEngine.Object.FindObjectsByType<EnemyView>().Length!=0)throw new Exception("Dense enemies not reclaimed");UnityEngine.Object.Destroy(energy);UnityEngine.Object.Destroy(battle);return report;
 }

 public static object DisableBarShadows() {
  if(Application.isPlaying)throw new Exception("Exit play mode first");int edited=0;
  foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{Defend.Editor.PhaseFiveCombatSceneBuilder.Content})){
   var path=AssetDatabase.GUIDToAssetPath(guid);var root=PrefabUtility.LoadPrefabContents(path);
   try{
    var building=root.GetComponent<BuildingView>();var enemy=root.GetComponent<EnemyView>();var model=building!=null?building.VisualRoot:enemy.ModelRoot;
    foreach(var r in root.GetComponentsInChildren<Renderer>(true))if(!r.transform.IsChildOf(model)){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;}
    if(building!=null && root.GetComponent<Collider>()==null)throw new Exception("Missing selection collider: "+path);
    PrefabUtility.SaveAsPrefabAsset(root,path);edited++;
   }finally{PrefabUtility.UnloadPrefabContents(root);}
  }
  return new{edited};
 }

 public static object CheckHeavyContact() {
  var s=UnityEngine.Object.FindAnyObjectByType<GameSessionRoot>();bool seen=false;
  for(int i=0;i<100 && !s.Battle.IsFinished;i++){s.SingleStep();if(UnityEngine.Object.FindObjectsByType<DamageEffect>().Any(e=>e.name.StartsWith("HeavyContact") && e.IsRunning)){seen=true;break;}}
  if(!seen)throw new Exception("No confirmed heavy contact effect");s.SingleStep();
  var poses=UnityEngine.Object.FindObjectsByType<DamageSubjectMotion>();
  if(poses.Length==0)throw new Exception("Heavy contact pivot not bound");
  return new {confirmedContact=seen,boundPivots=poses.Length,tick=s.World.CurrentTick,balance=s.World.Energy.GetTotals().BalanceError};
 }
}
