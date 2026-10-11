using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Independent art inspection only. Never modifies playable scenes or runtime behavior.</summary>
public static class PhaseFiveAssetInspection
{
    const string Root = "Assets/Defend/Content/Art/PhaseFive";
    const string Evidence = "Docs/Art/PhaseFive/Checks";
    public static object Baseline()
    {
        var result = new {
            playing = EditorApplication.isPlaying, compiling = EditorApplication.isCompiling,
            activeScene = SceneManager.GetActiveScene().path,
            scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(i => {
                var scene = SceneManager.GetSceneAt(i);
                return new { scene.path, scene.isDirty, scene.isLoaded, roots = scene.rootCount };
            }).ToArray()
        };
        return result;
    }

    public static object ProbeImport()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Editor must be stopped.");
        string path = Root + "/Models/Environment/IndustrialFloor.fbx";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.globalScale = 1;
        importer.useFileScale = true;
        importer.bakeAxisConversion = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.isReadable = true;
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var preview = EditorSceneManager.NewPreviewScene();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
        try
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var forward = instance.GetComponentsInChildren<Transform>().First(t => t.name == "ForwardReference");
            return new { dimensions = new[]{ bounds.size.x,bounds.size.y,bounds.size.z },
                minimum = new[]{ bounds.min.x,bounds.min.y,bounds.min.z },
                forward = new[]{forward.position.x,forward.position.y,forward.position.z},
                transforms = instance.GetComponentsInChildren<Transform>().Select(t => new {
                    t.name, rotation = new[]{t.localEulerAngles.x,t.localEulerAngles.y,t.localEulerAngles.z},
                    scale = new[]{t.localScale.x,t.localScale.y,t.localScale.z} }).ToArray(),
                meshes = instance.GetComponentsInChildren<MeshFilter>().Select(m => new {m.name, vertices=m.sharedMesh.vertexCount,
                    triangles=m.sharedMesh.triangles.Length/3}).ToArray(),
                fileScale=importer.fileScale, globalScale=importer.globalScale };
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    public static object RestoreExistingPreloadedInput()
    {
        // Restore the single existing reference from the directly captured pre-task file.
        // No other PlayerSettings field or asset is changed.
        string path="Assets/InputSystem_Actions.inputactions";
        var original=AssetDatabase.LoadAllAssetsAtPath(path).FirstOrDefault(value=> {
            string guid; long local;
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value,out guid,out local)
                && guid=="052faaac586de48259a63d0c4782560b" && local==-944628639613478452L;
        });
        if(original==null) throw new InvalidOperationException("Original preloaded input asset did not resolve.");
        var current=PlayerSettings.GetPreloadedAssets();
        if(current.Length>0 && (current.Length!=1 || current[0]!=original)) throw new InvalidOperationException("Preloaded assets changed independently; refusing to replace them.");
        PlayerSettings.SetPreloadedAssets(new[]{original});
        return new { restored=path, count=PlayerSettings.GetPreloadedAssets().Length };
    }
}
