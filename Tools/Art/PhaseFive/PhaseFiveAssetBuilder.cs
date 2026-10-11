using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Independent art import and inspection. Does not change playable scenes or game code.</summary>
public static class PhaseFiveAssetBuilder
{
    const string Root = "Assets/Defend/Content/Art/PhaseFive";
    const string Evidence = "Docs/Art/PhaseFive/Checks";
    static readonly string[] AssetNames = { "Reactor", "Relay", "RapidTurret", "HeavyTurret", "AreaTurret", "Core", "StandardEnemy", "FastEnemy", "HeavyEnemy", "RockLowA", "RockLowB", "IndustrialFloor", "EntranceMarker" };
    static readonly string[] MaterialNames = { "ArmorIvory", "StructureDark", "MetalSteel", "EnergyCyan", "EnemyOrange", "RubberDark", "GroundConcrete", "RockSlate" };
    static readonly Color[] Colors = { new Color(.62f,.69f,.72f), new Color(.045f,.061f,.077f), new Color(.20f,.26f,.31f), new Color(.015f,.62f,.76f), new Color(.83f,.185f,.037f), new Color(.022f,.028f,.035f), new Color(.24f,.28f,.31f), new Color(.12f,.15f,.18f) };
    static readonly float[] Metallic = { .22f,.42f,.65f,.18f,.22f,.02f,.08f,.05f };
    static readonly float[] Roughness = { .42f,.48f,.32f,.25f,.46f,.78f,.78f,.86f };
    static readonly Matrix4x4 ForwardCorrection = Matrix4x4.Rotate(Quaternion.Euler(0,180,0));

    [Serializable] public class AssetRecord
    {
        public string name, source, prefab;
        public float[] rawDimensions, prefabDimensions, minimum, forward;
        public int triangles, renderers, materialSlots;
        public string[] materials, pivots;
        public bool identityTransforms, grounded, positiveForward, matchingDimensions, validNormals, sharedMaterialReferences;
    }
    [Serializable] public class ImportReport
    {
        public string unityVersion, scene, originalActiveScene, restoredActiveScene;
        public bool editorStopped, originalSceneDirty, previewSceneSaved, savedPrefabsReopened;
        public int assetCount, sharedMaterialCount;
        public AssetRecord[] assets;
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\','/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static Dictionary<string,Material> SharedMaterials()
    {
        Folder(Root+"/Materials");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP Lit unavailable.");
        var result = new Dictionary<string,Material>();
        for (int i=0;i<MaterialNames.Length;i++)
        {
            string name = "PhaseFive_"+MaterialNames[i];
            string path = Root+"/Materials/"+name+".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); material.name=name; AssetDatabase.CreateAsset(material,path); }
            // Blender palette is linear RGB; Unity color properties are authored in display space.
            material.SetColor("_BaseColor",Colors[i].gamma);
            material.SetFloat("_Metallic",Metallic[i]);
            material.SetFloat("_Smoothness",1-Roughness[i]);
            if (i==3) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",Colors[i].gamma*.7f); }
            else { material.DisableKeyword("_EMISSION"); material.SetColor("_EmissionColor",Color.black); }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            result.Add(name,material);
        }
        return result;
    }

    static Bounds RenderBounds(GameObject instance)
    {
        var renderers = instance.GetComponentsInChildren<Renderer>();
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }
    static float[] Values(Vector3 value) { return new[]{value.x,value.y,value.z}; }

    static void CopyHierarchy(Transform source, Transform target, string assetName, Dictionary<string,Material> materials)
    {
        var sourceFilter = source.GetComponent<MeshFilter>();
        if (sourceFilter != null)
        {
            var mesh = Object.Instantiate(sourceFilter.sharedMesh);
            mesh.name = assetName+"_"+source.name+"_UnityAxes";
            var toLocal = target.worldToLocalMatrix * ForwardCorrection * source.localToWorldMatrix;
            mesh.vertices = mesh.vertices.Select(v => toLocal.MultiplyPoint3x4(v)).ToArray();
            var normalMatrix = toLocal.inverse.transpose;
            mesh.normals = mesh.normals.Select(v => normalMatrix.MultiplyVector(v).normalized).ToArray();
            var tangents = mesh.tangents;
            if (tangents.Length>0) mesh.tangents = tangents.Select(t => { var v=toLocal.MultiplyVector(new Vector3(t.x,t.y,t.z)).normalized; return new Vector4(v.x,v.y,v.z,t.w); }).ToArray();
            mesh.RecalculateBounds();
            string meshPath = Root+"/Meshes/"+mesh.name+".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) AssetDatabase.CreateAsset(mesh,meshPath);
            else { EditorUtility.CopySerialized(mesh,existing); Object.DestroyImmediate(mesh); mesh=existing; EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); }
            target.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer = target.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials=source.GetComponent<Renderer>().sharedMaterials.Select(m => materials[m.name]).ToArray();
        }
        foreach (Transform child in source)
        {
            var created = new GameObject(child.name).transform;
            created.SetParent(target,false);
            created.position=ForwardCorrection.MultiplyPoint3x4(child.position);
            created.rotation=Quaternion.identity;
            created.localScale=Vector3.one;
            CopyHierarchy(child,created,assetName,materials);
        }
    }

    public static object ImportAndBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) throw new InvalidOperationException("Editor must be stopped and ready.");
        var original = SceneManager.GetActiveScene();
        var report = new ImportReport { unityVersion=Application.unityVersion, originalActiveScene=original.path,
            originalSceneDirty=original.isDirty, editorStopped=true, sharedMaterialCount=8 };
        Folder(Root+"/Prefabs"); Folder(Root+"/Meshes"); Folder(Root+"/Scenes");
        var materials=SharedMaterials();
        var records=new List<AssetRecord>();
        var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (string name in AssetNames)
            {
                string category = Array.IndexOf(AssetNames,name)<6 ? "Buildings" : Array.IndexOf(AssetNames,name)<9 ? "Enemies" : "Environment";
                string path=Root+"/Models/"+category+"/"+name+".fbx";
                if (!File.Exists(path)) throw new FileNotFoundException(path);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                importer.globalScale=1; importer.useFileScale=true; importer.bakeAxisConversion=true;
                importer.importCameras=false; importer.importLights=false; importer.importAnimation=false;
                importer.isReadable=true; importer.importNormals=ModelImporterNormals.Import;
                importer.meshCompression=ModelImporterMeshCompression.Off;
                foreach (var entry in materials) importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),entry.Key),entry.Value);
                importer.SaveAndReimport();
                var imported=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var source=(GameObject)PrefabUtility.InstantiatePrefab(imported,preview);
                var rawBounds=RenderBounds(source);
                var target=new GameObject(name);
                SceneManager.MoveGameObjectToScene(target,preview);
                CopyHierarchy(source.transform,target.transform,name,materials);
                string prefabPath=Root+"/Prefabs/"+name+".prefab";
                PrefabUtility.SaveAsPrefabAsset(target,prefabPath);
                Object.DestroyImmediate(target); Object.DestroyImmediate(source);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var reopened=(GameObject)PrefabUtility.InstantiatePrefab(prefab,preview);
                var bounds=RenderBounds(reopened);
                var transforms=reopened.GetComponentsInChildren<Transform>();
                var forward=transforms.First(t=>t.name=="ForwardReference").position;
                var filters=reopened.GetComponentsInChildren<MeshFilter>();
                var renderers=reopened.GetComponentsInChildren<Renderer>();
                var record=new AssetRecord {
                    name=name, source=path, prefab=prefabPath, rawDimensions=Values(rawBounds.size), prefabDimensions=Values(bounds.size),
                    minimum=Values(bounds.min), forward=Values(forward), triangles=filters.Sum(f=>f.sharedMesh.triangles.Length/3),
                    renderers=renderers.Length, materialSlots=renderers.Sum(r=>r.sharedMaterials.Length),
                    materials=renderers.SelectMany(r=>r.sharedMaterials).Select(m=>AssetDatabase.GetAssetPath(m)).Distinct().OrderBy(v=>v).ToArray(),
                    pivots=transforms.Where(t=>t.name.Contains("Pivot") || t.name.Contains("Yaw") || t.name.Contains("Recoil") || t.name.Contains("Muzzle")).Select(t=>t.name).ToArray(),
                    identityTransforms=transforms.All(t=>Vector3.Distance(t.localScale,Vector3.one)<.0001f && Quaternion.Angle(t.localRotation,Quaternion.identity)<.01f),
                    grounded=Mathf.Abs(bounds.min.y)<.001f, positiveForward=forward.z>.49f && Mathf.Abs(forward.x)<.001f,
                    matchingDimensions=Vector3.Distance(rawBounds.size,bounds.size)<.0001f,
                    validNormals=filters.All(f=>f.sharedMesh.normals.All(n=> !float.IsNaN(n.x) && !float.IsNaN(n.y) && !float.IsNaN(n.z) && n.sqrMagnitude>.99f)),
                    sharedMaterialReferences=renderers.All(r=>r.sharedMaterials.All(m=>m!=null && materials.Values.Contains(m)))
                };
                if (!record.identityTransforms || !record.grounded || !record.positiveForward || !record.matchingDimensions || !record.validNormals || !record.sharedMaterialReferences)
                    throw new InvalidOperationException("Import validation failed: "+name);
                records.Add(record);
                Object.DestroyImmediate(reopened);
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        report.assets=records.ToArray(); report.assetCount=records.Count; report.savedPrefabsReopened=true;
        report.scene=BuildInspectionScene(materials);
        report.previewSceneSaved=true;
        report.restoredActiveScene=SceneManager.GetActiveScene().path;
        Directory.CreateDirectory(Evidence);
        File.WriteAllText(Evidence+"/UnityImport.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        return report;
    }

    static string BuildInspectionScene(Dictionary<string,Material> materials)
    {
        string path=Root+"/Scenes/PhaseFiveModelInspection.unity";
        var original=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            var world=new GameObject("第五阶段模型检视 · 独立美术资产");
            for (int index=0;index<AssetNames.Length;index++)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+AssetNames[index]+".prefab");
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                instance.transform.SetParent(world.transform,false);
                if(index<6) instance.transform.position=new Vector3((index%3-1)*1.55f,0,1.65f-(index/3)*1.6f);
                else if(index<9) instance.transform.position=new Vector3((index-7)*1.55f,0,-1.6f);
                else instance.transform.position=new Vector3((index-10.5f)*1.25f,0,-3.2f);
            }
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name="检视台面 · 不代表实际地图";
            floor.transform.position=new Vector3(0,-.09f,-.8f);
            floor.transform.localScale=new Vector3(6.4f,.16f,7.0f);
            floor.GetComponent<Renderer>().sharedMaterial=materials["PhaseFive_GroundConcrete"];
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            var light=new GameObject("检视主光",typeof(Light)).GetComponent<Light>();
            light.type=LightType.Directional; light.intensity=1.5f; light.color=new Color(1,.95f,.88f);
            light.shadows=LightShadows.Soft; light.transform.rotation=Quaternion.Euler(45,-30,0);
            var fill=new GameObject("检视补光",typeof(Light)).GetComponent<Light>();
            fill.type=LightType.Directional; fill.intensity=.5f; fill.color=new Color(.7f,.85f,1);
            fill.transform.rotation=Quaternion.Euler(60,130,0);
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.42f,.47f,.52f);
            RenderSettings.fog=false;
            var camera=new GameObject("检视相机 · 70度正交",typeof(Camera)).GetComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=4.05f;
            camera.transform.rotation=Quaternion.Euler(70,0,0);
            camera.transform.position=new Vector3(0,0,-.8f)-camera.transform.forward*12;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.10f,.14f,.18f);
            camera.nearClipPlane=.05f; camera.farClipPlane=40;
            EditorSceneManager.SaveScene(scene,path);
        }
        finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene,true); }
        return path;
    }
}
