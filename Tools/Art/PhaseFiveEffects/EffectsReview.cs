using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Defend.PhaseFiveEffects;
using Defend.PhaseFiveEffects.Editor;
using Object = UnityEngine.Object;

public static class EffectsReview
{
    const string Evidence="Docs/Art/PhaseFiveEffects/Checks/";
    static void Save(string name,object value) { Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence+name+".json",Newtonsoft.Json.JsonConvert.SerializeObject(value,Newtonsoft.Json.Formatting.Indented)); }
    static EffectShowcase Demo => Object.FindAnyObjectByType<EffectShowcase>();
    public static object EnableReviewTicks() { Application.runInBackground=true; return new{background=Application.runInBackground}; }
    public static object SceneSnapshot()
    {
        var scene=SceneManager.GetActiveScene();
        Directory.CreateDirectory("Logs/PhaseFiveEffects");
        EditorSceneManager.SaveScene(scene,"Logs/PhaseFiveEffects/OriginalSceneSnapshot.unity",true);
        return new { scene=scene.path,dirty=scene.isDirty,roots=scene.GetRootGameObjects().Select(o=>o.name).ToArray() };
    }
    public static object OpenAfterVerifiedSnapshot()
    {
        EditorSceneManager.OpenScene(EffectsPackageBuilder.ScenePath,OpenSceneMode.Single);
        return new{scene=SceneManager.GetActiveScene().path};
    }
    public static object Build()
    {
        EffectsPackageBuilder.Build();
        var library=AssetDatabase.LoadAssetAtPath<EffectLibrary>(EffectAuthoring.RootPath+"/EffectLibrary.asset");
        var entries=library.Entries.Select(e=>new {id=e.Id,lifetime=e.Prefab.Lifetime,loop=e.Prefab.Looping,group=e.Prefab.BudgetGroup.ToString(),cost=e.Prefab.BudgetUnits,
            particles=e.Prefab.GetComponentsInChildren<ParticleSystem>(true).Sum(p=>p.main.maxParticles),
            playOnAwake=e.Prefab.GetComponentsInChildren<ParticleSystem>(true).Any(p=>p.main.playOnAwake),
            missingMaterials=e.Prefab.GetComponentsInChildren<Renderer>(true).Any(r=>r.sharedMaterials.Any(m=>m==null)),
            missingScripts=e.Prefab.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))}).ToArray();
        bool passed=entries.Length==25&&entries.All(e=>!e.playOnAwake&&!e.missingMaterials&&e.missingScripts==0);
        var result=new {passed,entries,scene=EffectsPackageBuilder.ScenePath}; Save("AssetInspection",result);
        if(!passed)throw new Exception("Asset inspection failed"); return result;
    }
    public static object Open()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop play mode first");
        if(SceneManager.GetActiveScene().isDirty)throw new Exception("Existing scene has unsaved work");
        EditorSceneManager.OpenScene(EffectsPackageBuilder.ScenePath,OpenSceneMode.Single);
        return new{scene=SceneManager.GetActiveScene().path};
    }
    static Transform Find(Transform t,string name)=>t.GetComponentsInChildren<Transform>(true).FirstOrDefault(v=>v.name==name);
    public static object PlayChecks()
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        var demo=Demo; demo.Stop(); demo.ExternalClock=true; var pool=demo.Pool;
        var checks=new Dictionary<string,bool>();
        var renderers=demo.Subjects.SelectMany(s=>s.GetComponentsInChildren<Renderer>(true)).ToArray();
        var materialReferences=renderers.Select(r=>r.sharedMaterials).ToArray();
        var materialColors=materialReferences.Select(a=>a.Select(m=>m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):Color.white).ToArray()).ToArray();
        var request=new EffectRequest {Origin=new Vector3(0,.2f,0),Destination=new Vector3(1,.2f,0),Rotation=Quaternion.identity,Intensity=1,Seed=13,ConfirmedTransfer=true};
        foreach(var entry in pool.Library.Entries)
        {
            var handle=pool.TryPlay(entry.Id,request); bool began=handle.IsValid;
            pool.Advance(.02f); pool.Paused=true; float age=handle.Age; int particles=pool.ParticleCount;
            var systems=pool.GetComponentsInChildren<ParticleSystem>();
            var snapshots=systems.Select(p=>{var a=new ParticleSystem.Particle[p.particleCount];p.GetParticles(a);return a;}).ToArray();
            pool.Advance(.07f); bool frozen=Math.Abs(handle.Age-age)<.00001f&&particles==pool.ParticleCount;
            for(int s=0;s<systems.Length;s++) {var currentParticles=new ParticleSystem.Particle[systems[s].particleCount];systems[s].GetParticles(currentParticles);frozen&=currentParticles.Length==snapshots[s].Length&&currentParticles.Select((v,i)=>v.position==snapshots[s][i].position&&v.remainingLifetime==snapshots[s][i].remainingLifetime).All(v=>v);}
            pool.Paused=false; pool.Advance(.01f); bool resumed=handle.IsValid&&handle.Age>age;
            pool.StopAll();
            checks[entry.Id+"_begin_pause_resume_clear"]=began&&frozen&&resumed&&pool.ActiveCount==0&&pool.GetComponentsInChildren<ParticleSystem>(true).All(p=>p.particleCount==0);
            var reused=pool.TryPlay(entry.Id,request); pool.Advance(entry.Prefab.Lifetime+1);
            checks[entry.Id+"_finite_or_loop_lifetime"]=entry.Prefab.Looping?reused.IsValid:!reused.IsValid;
            pool.StopAll();
        }
        var first=pool.TryPlay("HeavyImpact",request);pool.Stop(first);var next=pool.TryPlay("HeavyImpact",request);
        checks["stale_handle_cannot_stop_reused_object"]=!pool.Stop(first)&&next.IsValid; pool.StopAll();
        request.EventSequence=901;
        var unique=pool.TryPlay("AreaImpact",request);var duplicate=pool.TryPlay("AreaImpact",request);
        checks["duplicate_event_has_one_ring"]=unique.IsValid&&!duplicate.IsValid&&pool.ActiveCount==1;pool.StopAll();request.EventSequence=0;
        request.ConfirmedTransfer=false;
        checks["reservation_is_not_transport"]=!pool.TryPlay("EnergyTransfer",request).IsValid;request.ConfirmedTransfer=true;
        var movingTarget=new GameObject("SnapshotTarget");request.Subject=movingTarget.transform;
        var tracer=pool.TryPlay("HeavyTracer",request);
        var line=pool.GetComponentsInChildren<LineRenderer>().First(l=>l.positionCount==2);
        var endpoint=line.GetPosition(1);movingTarget.transform.position=Vector3.one*10;pool.Advance(.01f);
        checks["tracer_keeps_fixed_endpoint"]=Vector3.Distance(line.GetPosition(1),endpoint)<.0001f;pool.StopAll();Object.DestroyImmediate(movingTarget);request.Subject=null;
        int before=pool.Dropped;
        foreach(var pair in new[]{("AreaImpact",64),("EnemyDeath",16),("EnergyTransfer",128),("EnergyShortage",32)})
        {
            for(int i=0;i<pair.Item2+8;i++)pool.TryPlay(pair.Item1,request);
        }
        checks["global_budgets"]=pool.BudgetUsed(EffectBudgetGroup.ShortEffect)==64&&pool.BudgetUsed(EffectBudgetGroup.Debris)==128&&pool.BudgetUsed(EffectBudgetGroup.EnergyPulse)==128&&pool.BudgetUsed(EffectBudgetGroup.Status)==32&&pool.Dropped-before==32;
        pool.StopAll();checks["budget_released"]=Enumerable.Range(0,4).All(i=>pool.BudgetUsed((EffectBudgetGroup)i)==0);
        request.Subject=demo.Subjects[1];var pivot=Find(request.Subject,"BarrelRecoil");var pose=pivot.localPosition;
        var a=pool.TryPlay("HeavyMuzzle",request);var b=pool.TryPlay("RapidMuzzle",request);var secondPose=pivot.localPosition;
        pool.Stop(a);checks["old_weapon_owner_does_not_reset_new_pose"]=Vector3.Distance(pivot.localPosition,secondPose)<.00001f;
        pool.Stop(b);checks["weapon_pose_restored"]=Vector3.Distance(pivot.localPosition,pose)<.00001f;
        var damage=DamageEffectsValidation.CheckMotionOwnership();checks["damage_motion_ownership"]=damage.Passed;
        request.Subject=demo.Subjects[3];var scale=request.Subject.localScale;
        pool.TryPlay("EnemyDeath",request);pool.Advance(.2f);pool.gameObject.SetActive(false);
        checks["pool_disable_restores_subject"]=Vector3.Distance(request.Subject.localScale,scale)<.0001f&&pool.ActiveCount==0;
        pool.gameObject.SetActive(true);
        checks["shared_material_references_and_colors_preserved"]=renderers.Select((r,i)=>r.sharedMaterials.SequenceEqual(materialReferences[i])&&r.sharedMaterials.Select((m,j)=>!m.HasProperty("_BaseColor")||m.GetColor("_BaseColor")==materialColors[i][j]).All(v=>v)).All(v=>v);
        checks["pooled_reuse_observed"]=pool.Reused>25;
        pool.StopAll(); demo.ExternalClock=false; demo.Restart();
        var result=new{passed=checks.Values.All(v=>v),checkCount=checks.Count,checks,damage,created=pool.Created,reused=pool.Reused,duplicates=pool.Duplicates};Save("PlayModeLifecycle",result);
        if(checks.Values.Any(v=>!v))throw new Exception("Play checks failed: "+string.Join(", ",checks.Where(p=>!p.Value).Select(p=>p.Key)));
        return result;
    }
    public static object CaptureOverview()=>Capture(false);
    public static object CaptureClose()=>Capture(true);
    public static object CaptureDamage()=>CaptureDetail("Damage",new Vector3(-2,.2f,0),2.3f);
    public static object CaptureEnergy()=>CaptureDetail("Energy",new Vector3(-2,.45f,-3.2f),2.3f);
    public static object CaptureShield()=>CaptureDetail("Shield",new Vector3(3.3f,.40f,-3.2f),1.05f);
    static object CaptureDetail(string name,Vector3 position,float size)
    {
        var camera=Demo.CloseCamera; camera.orthographicSize=size;
        camera.transform.position=position-camera.transform.forward*20;
        var capture=Demo.GetComponent<EffectEvidenceCapture>()??Demo.gameObject.AddComponent<EffectEvidenceCapture>();capture.Demo=Demo;capture.Width=1280;capture.Height=720;
        capture.Begin("Logs/PhaseFiveEffects/Frames/"+name,true);return new{recording=true,view=name};
    }
    static object Capture(bool close)
    {
        if(!Application.isPlaying)throw new Exception("Play Mode required");
        var demo=Demo;var capture=demo.GetComponent<EffectEvidenceCapture>()??demo.gameObject.AddComponent<EffectEvidenceCapture>();capture.Demo=demo;
        capture.Width=close?1280:1200;capture.Height=close?720:1000;
        capture.Begin("Logs/PhaseFiveEffects/Frames/"+(close?"Close":"Overview"),close);
        return new{recording=true,frames=capture.FrameCount};
    }
    public static object Measure()
    {
        var demo=Demo;demo.SetCloseView(false);var measure=demo.GetComponent<EffectLoadMeasurement>()??demo.gameObject.AddComponent<EffectLoadMeasurement>();measure.Demo=demo;measure.Begin();return new{measuring=true};
    }
    public static object Status()
    {
        var demo=Demo;var capture=demo.GetComponent<EffectEvidenceCapture>();var measure=demo.GetComponent<EffectLoadMeasurement>();
        return new{playing=Application.isPlaying,paused=EditorApplication.isPaused,background=Application.runInBackground,enabled=demo.enabled,rootActive=demo.gameObject.activeInHierarchy,captureEnabled=capture!=null&&capture.enabled,cycle=demo.CycleTime,cycles=demo.Cycles,active=demo.Pool.ActiveCount,particles=demo.Pool.ParticleCount,recording=capture!=null&&capture.Recording,frame=capture!=null?capture.Frame:0,measuring=measure!=null&&measure.Running};
    }
    public static object Reopen()
    {
        if(Application.isPlaying)throw new Exception("Stop first");
        EditorSceneManager.OpenScene(EffectsPackageBuilder.ScenePath,OpenSceneMode.Single);
        var demo=Demo;var result=new{passed=demo!=null&&demo.Pool.GetComponentsInChildren<PhaseFiveEffect>(true).Length==0,active=demo.Pool.ActiveCount,pooledSceneChildren=demo.Pool.GetComponentsInChildren<PhaseFiveEffect>(true).Length};Save("SceneReopen",result);return result;
    }
    public static object Restore()
    {
        Application.runInBackground=false;
        EditorSceneManager.OpenScene("Assets/Defend/Content/Scenes/Dev/EnergyFrontDefense.unity",OpenSceneMode.Single);
        return new {scene=SceneManager.GetActiveScene().path,background=Application.runInBackground,playing=Application.isPlaying};
    }
    public static object FinalState()
    {
        var shader=Shader.Find("Defend/PhaseFive/IndustrialEffect");
        var result=new {unity=Application.unityVersion,processor=SystemInfo.processorType,graphics=SystemInfo.graphicsDeviceName,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),playing=Application.isPlaying,compiling=EditorApplication.isCompiling,background=Application.runInBackground,activeScene=SceneManager.GetActiveScene().path,shaderHasErrors=shader==null||ShaderUtil.ShaderHasError(shader),runtimeEffectsRemaining=Object.FindObjectsByType<PhaseFiveEffect>(FindObjectsInactive.Include).Length};
        Save("FinalEditorState",result);return result;
    }
}
