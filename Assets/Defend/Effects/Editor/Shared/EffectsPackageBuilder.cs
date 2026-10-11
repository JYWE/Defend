using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Defend.PhaseFiveEffects.Editor
{
    public static class EffectsPackageBuilder
    {
        public const string ScenePath = EffectAuthoring.RootPath + "/Scenes/PhaseFiveEffectsShowcase.unity";
        const string ModelRoot = "Assets/Defend/Content/Art/PhaseFive/Prefabs/";
        [MenuItem("Defend/Art/Build Phase Five Effects")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop the Editor before authoring.");
            BuildMaterials();
            var previous=SceneManager.GetActiveScene();
            var authoringScene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(authoringScene);
            try { WeaponEffectsAuthoring.Build(); DamageEffectsAuthoring.Build(); EnergyEffectsAuthoring.Build(); BuildWrappers(); }
            finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(authoringScene,true); }
            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { EffectAuthoring.RootPath + "/Prefabs" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Select(p => p.GetComponent<PhaseFiveEffect>()).Where(p => p != null).OrderBy(p => p.name).ToArray();
            string libraryPath = EffectAuthoring.RootPath + "/EffectLibrary.asset";
            var library = AssetDatabase.LoadAssetAtPath<EffectLibrary>(libraryPath);
            if (library == null) { library = ScriptableObject.CreateInstance<EffectLibrary>(); AssetDatabase.CreateAsset(library, libraryPath); }
            library.Entries = prefabs.Select(p => new EffectLibrary.Entry { Id = p.name, Prefab = p }).ToArray();
            EditorUtility.SetDirty(library); AssetDatabase.SaveAssetIfDirty(library);
            BuildScene(library);
        }
        static void BuildMaterials()
        {
            EffectAuthoring.Folder(EffectAuthoring.RootPath + "/Materials");
            var shader = Shader.Find("Defend/PhaseFive/IndustrialEffect");
            if (shader == null) throw new InvalidOperationException("Effect shader not imported.");
            string[] names = { "CyanGlow", "WarmGlow", "WhiteGlow", "DustSoft", "GreenGlow", "AmberGlow", "ShieldCyan" };
            Color[] colors = { new Color(.08f,.8f,1,1), new Color(1,.37f,.08f,1), new Color(1,.91f,.72f,1), new Color(.38f,.43f,.46f,.38f), new Color(.2f,1,.48f,1), new Color(1,.66f,.12f,1), new Color(.1f,.65f,1,.65f) };
            for (int i=0; i<names.Length; i++)
            {
                string path=EffectAuthoring.RootPath+"/Materials/"+names[i]+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null) { mat=new Material(shader); AssetDatabase.CreateAsset(mat,path); }
                mat.SetColor("_BaseColor", colors[i]); mat.SetFloat("_Softness", i==3 ? 1 : 0);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)(i==3 ? BlendMode.OneMinusSrcAlpha : BlendMode.One));
                EditorUtility.SetDirty(mat); AssetDatabase.SaveAssetIfDirty(mat);
            }
        }
        static void BuildWrappers()
        {
            EffectAuthoring.Folder(EffectAuthoring.RootPath + "/Prefabs/Models");
            foreach (string name in new[]{"Reactor","Relay","RapidTurret","HeavyTurret","AreaTurret","Core","StandardEnemy","FastEnemy","HeavyEnemy"})
            {
                var wrapper=new GameObject(name+"EffectModel");
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelRoot+name+".prefab"));
                visual.transform.SetParent(wrapper.transform,false);
                var modelBounds=visual.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
                foreach(var point in new[]{"EnergyPort","StatusAnchor","HitAnchor"})
                {
                    var t=new GameObject(point).transform; t.SetParent(wrapper.transform,false);
                    t.localPosition=point=="EnergyPort"?new Vector3(0,modelBounds.max.y+.06f,.16f):point=="StatusAnchor"?new Vector3(0,modelBounds.max.y+.10f,0):new Vector3(0,.25f,0);
                }
                if(name.Contains("Turret")) wrapper.AddComponent<WeaponMotionRig>();
                PrefabUtility.SaveAsPrefabAsset(wrapper,EffectAuthoring.RootPath+"/Prefabs/Models/"+wrapper.name+".prefab");
                Object.DestroyImmediate(wrapper);
            }
        }
        static Transform Model(string name, Transform parent, Vector3 position, bool facingBack=false)
        {
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(EffectAuthoring.RootPath+"/Prefabs/Models/"+name+"EffectModel.prefab"));
            obj.transform.SetParent(parent,false); obj.transform.position=position;
            if(facingBack) obj.transform.rotation=Quaternion.Euler(0,180,0);
            return obj.transform;
        }
        static Transform Find(Transform parent,string name) { foreach(var t in parent.GetComponentsInChildren<Transform>(true)) if(t.name==name)return t; return null; }
        static void Label(string text, Vector3 position, float size, Color color, Transform parent)
        {
            var go=new GameObject(text); go.layer=5; go.transform.SetParent(parent,false); go.transform.position=position; go.transform.rotation=Quaternion.Euler(70,0,0);
            var mesh=go.AddComponent<TextMesh>(); mesh.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Defend/Content/Art/Fonts/NotoSansCJKsc-Regular.otf");
            mesh.text=text; mesh.fontSize=64; mesh.characterSize=size; mesh.anchor=TextAnchor.MiddleCenter; mesh.alignment=TextAlignment.Center; mesh.color=color;
            go.GetComponent<MeshRenderer>().sharedMaterial=mesh.font.material;
        }
        static void BuildScene(EffectLibrary library)
        {
            EffectAuthoring.Folder(EffectAuthoring.RootPath+"/Scenes");
            var original=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            try
            {
                var root=new GameObject("第五阶段动态特效展示");
                var pool=root.AddComponent<EffectPool>(); pool.Library=library; pool.AutomaticAdvance=false;
                var demo=root.AddComponent<EffectShowcase>(); demo.Pool=pool;
                demo.Subjects=new Transform[9]; demo.Targets=new Transform[9]; demo.Muzzles=new Transform[9];
                demo.SubjectAnchors=new Transform[9]; demo.TargetAnchors=new Transform[9]; demo.TargetPorts=new Transform[9];
                string[] subjects={"RapidTurret","HeavyTurret","AreaTurret","StandardEnemy","Relay","Core","Reactor","Relay","Core"};
                string[] targets={"StandardEnemy","HeavyEnemy","FastEnemy","FastEnemy","Relay","Relay","Relay","Reactor","Relay"};
                string[] labels={"01  连射炮 · 即时射击","02  重炮 · 机械后坐","03  范围炮 · 单次冲击","04  受击 · 敌人破坏","05  建筑受击 · 残骸","06  维修确认","07  能源发送 · 到货","08  缺能 · 断连状态","09  护盾命中 · 破裂"};
                for(int i=0;i<9;i++)
                {
                    Vector3 center=new Vector3((i%3-1)*4.0f,0,3.2f-(i/3)*3.2f);
                    EffectAuthoring.Shape(root.transform,"展示台 "+(i+1),PrimitiveType.Cube,center+Vector3.down*.08f,new Vector3(3.7f,.14f,2.95f),EffectAuthoring.Material("OriginalDark"));
                    var edge=EffectAuthoring.Line(root.transform,"台面边线 "+(i+1),EffectAuthoring.Material("CyanGlow"),.008f,new[]{new Vector3(-1.78f,.005f,-1.43f),new Vector3(1.78f,.005f,-1.43f),new Vector3(1.78f,.005f,1.43f),new Vector3(-1.78f,.005f,1.43f)},true); edge.transform.position=center;
                    demo.Subjects[i]=Model(subjects[i],root.transform,center+new Vector3(-.7f,0,-.1f));
                    demo.Targets[i]=Model(targets[i],root.transform,center+new Vector3(.65f,0,.55f),i<3);
                    if(i<3) demo.Subjects[i].rotation=Quaternion.Euler(0,64,0);
                    if(i>=3 && i<=5 || i==8) demo.Targets[i].gameObject.SetActive(false);
                    demo.Muzzles[i]=Find(demo.Subjects[i],"MuzzleLeft")??Find(demo.Subjects[i],"Muzzle")??Find(demo.Subjects[i],"EnergyPort");
                    demo.SubjectAnchors[i]=Find(demo.Subjects[i],"StatusAnchor"); demo.TargetAnchors[i]=Find(demo.Targets[i],"StatusAnchor");
                    demo.TargetPorts[i]=Find(demo.Targets[i],"EnergyPort");
                    Label(labels[i],center+new Vector3(0,.025f,-1.1f),.039f,new Color(.73f,.86f,.9f),root.transform);
                }
                Label("工业防线  /  动态特效",new Vector3(0,.1f,5.25f),.085f,Color.white,root.transform);
                Label("空格 暂停/恢复     R 重播     S 停止     Tab 单项     Enter 组合     1 全景     2 近景",new Vector3(0,.06f,-5.1f),.032f,new Color(.58f,.73f,.8f),root.transform);
                var light=new GameObject("主光",typeof(Light)).GetComponent<Light>(); light.type=LightType.Directional; light.intensity=1.5f; light.color=new Color(1,.96f,.9f); light.transform.rotation=Quaternion.Euler(45,-30,0); light.shadows=LightShadows.Soft;
                var fill=new GameObject("补光",typeof(Light)).GetComponent<Light>(); fill.type=LightType.Directional; fill.intensity=.55f; fill.color=new Color(.65f,.85f,1); fill.transform.rotation=Quaternion.Euler(60,130,0);
                RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.38f,.44f,.5f); RenderSettings.fog=false;
                demo.OverviewCamera=Camera("70度正交全景",new Vector3(0,0,.15f),70,6.3f); demo.OverviewCamera.tag="MainCamera";
                demo.CloseCamera=Camera("武器近景",new Vector3(0,.25f,3.2f),36,1.5f); demo.CloseCamera.enabled=false;
                demo.CloseCamera.cullingMask=~(1<<5);
                EditorSceneManager.SaveScene(scene,ScenePath);
            }
            finally { SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene,true); }
        }
        static Camera Camera(string name,Vector3 target,float angle,float size)
        {
            var camera=new GameObject(name,typeof(Camera),typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=size; camera.transform.rotation=Quaternion.Euler(angle,0,0); camera.transform.position=target-camera.transform.forward*20;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.025f,.039f,.054f); camera.nearClipPlane=.05f; camera.farClipPlane=45;
            return camera;
        }
    }
}
