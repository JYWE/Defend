using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static class PhaseFiveUnityReview
{
    const string Root="Assets/Defend/Content/Art/PhaseFive";
    [Serializable] public class MotionRecord
    {
        public string asset, pivot, motion;
        public int movingVertices;
        public float expectedPositionError, maximumMovement, staticMovement, restoredPositionError;
        public bool passed;
    }
    [Serializable] public class ReviewReport
    {
        public bool editorStopped, sceneReopened, renderSubmitted;
        public string scene, render, evidenceScope;
        public MotionRecord[] motions;
    }
    static Vector3[] Vertices(Transform[] transforms)
    {
        return transforms.SelectMany(t=>t.GetComponents<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>t.TransformPoint(v)))).ToArray();
    }
    static float Difference(Vector3[] first,Vector3[] second)
    { return first.Length==0 ? 0 : first.Zip(second,(a,b)=>Vector3.Distance(a,b)).Max(); }

    static MotionRecord Exercise(GameObject instance,string pivotName,string motion)
    {
        var pivot=instance.GetComponentsInChildren<Transform>().First(t=>t.name==pivotName);
        var descendants=pivot.GetComponentsInChildren<Transform>();
        var fixedTransforms=instance.GetComponentsInChildren<Transform>().Except(descendants).ToArray();
        var before=Vertices(descendants); var fixedBefore=Vertices(fixedTransforms);
        var position=pivot.localPosition; var rotation=pivot.localRotation; var center=pivot.position;
        Vector3[] expected;
        if (motion=="yaw_90_degrees")
        {
            var turn=Quaternion.AngleAxis(90,Vector3.up);
            expected=before.Select(v=>center+turn*(v-center)).ToArray();
            pivot.localRotation=turn;
        }
        else if (motion=="leg_12_degrees")
        {
            var turn=Quaternion.AngleAxis(12,Vector3.right);
            expected=before.Select(v=>center+turn*(v-center)).ToArray();
            pivot.localRotation=turn;
        }
        else
        {
            var offset=motion=="recoil_004_meters" ? new Vector3(0,0,-.04f) : new Vector3(0,0,.08f);
            expected=before.Select(v=>v+offset).ToArray();
            pivot.localPosition+=offset;
        }
        var moved=Vertices(descendants);
        var record=new MotionRecord { asset=instance.name,pivot=pivotName,motion=motion,movingVertices=before.Length,
            expectedPositionError=Difference(moved,expected),maximumMovement=Difference(before,moved),staticMovement=Difference(fixedBefore,Vertices(fixedTransforms)) };
        pivot.localPosition=position; pivot.localRotation=rotation;
        record.restoredPositionError=Difference(before,Vertices(descendants));
        record.passed=record.movingVertices>0 && record.expectedPositionError<.0001f && record.maximumMovement>.001f && record.staticMovement<.0001f && record.restoredPositionError<.0001f;
        if(!record.passed) throw new InvalidOperationException("Motion validation failed: "+instance.name+"/"+pivotName);
        return record;
    }

    public static object Review()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Editor must be stopped.");
        var motionScene=EditorSceneManager.NewPreviewScene();
        var records=new List<MotionRecord>();
        try
        {
            foreach(string name in new[]{"RapidTurret","HeavyTurret","AreaTurret","StandardEnemy","HeavyEnemy"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+name+".prefab");
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,motionScene);
                if(name.EndsWith("Turret")) { records.Add(Exercise(instance,"TurretYaw","yaw_90_degrees")); records.Add(Exercise(instance,"BarrelRecoil","recoil_004_meters")); }
                if(name=="StandardEnemy") foreach(string pivot in new[]{"LegFrontLeftPivot","LegFrontRightPivot","LegRearLeftPivot","LegRearRightPivot"}) records.Add(Exercise(instance,pivot,"leg_12_degrees"));
                if(name=="HeavyEnemy") records.Add(Exercise(instance,"ImpactPivot","impact_008_meters"));
                Object.DestroyImmediate(instance);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(motionScene); }
        string scenePath=Root+"/Scenes/PhaseFiveModelInspection.unity";
        var preview=EditorSceneManager.OpenPreviewScene(scenePath);
        var report=new ReviewReport { editorStopped=true,sceneReopened=true,scene=scenePath,
            render="Docs/Art/PhaseFive/Previews/UnityModelInspection.png",motions=records.ToArray(),
            evidenceScope="Unity Editor offscreen URP render of saved inspection scene; not a gameplay frame or performance test." };
        RenderTexture texture=null; Texture2D pixels=null;
        var previous=RenderTexture.active;
        try
        {
            var camera=preview.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Camera>()).Single();
            camera.scene=preview;
            camera.aspect=1;
            camera.orthographicSize=4.05f;
            texture=new RenderTexture(1400,1400,24,RenderTextureFormat.ARGB32);
            texture.Create();
            var request=new UniversalRenderPipeline.SingleCameraRequest { destination=texture };
            if(!RenderPipeline.SupportsRenderRequest(camera,request)) throw new InvalidOperationException("Current URP does not support single-camera render requests.");
            RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture.active=texture;
            pixels=new Texture2D(1400,1400,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,1400,1400),0,0); pixels.Apply();
            File.WriteAllBytes(report.render,pixels.EncodeToPNG());
            report.renderSubmitted=true;
        }
        finally
        {
            RenderTexture.active=previous;
            if(pixels!=null)Object.DestroyImmediate(pixels);
            if(texture!=null){texture.Release();Object.DestroyImmediate(texture);}
            EditorSceneManager.ClosePreviewScene(preview);
        }
        File.WriteAllText("Docs/Art/PhaseFive/Checks/UnityMotionAndRender.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        return report;
    }
}
