using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Checks the new assets in Unity Play Mode and captures a real camera frame.</summary>
[InitializeOnLoad]
public static class Stage345RuntimeCheck
{
    const string Key = "Stage345.Runtime.";
    const string Report = "Docs/CharacterMeshAudit/Stage3/unity-runtime.txt";
    const string Screenshot = "Docs/CharacterMeshAudit/Stage3/unity-review.png";
    static readonly (string name, bool human)[] NewActors =
    {
        ("Horse",false),("RedDeer",false),("ArmoredKnight",true),("DarkKnight",true)
    };
    static bool sampled;
    static Vector3[][] first;
    static Quaternion[][] firstBones;
    static AnimationClip[] clips;
    static double nextTick;

    static Stage345RuntimeCheck()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    [MenuItem("Tools/Character Rigging/Stages 3-5/Validate Play Mode")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop current Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(Stage345AssetSetup.ScenePath)) throw new FileNotFoundException(Stage345AssetSetup.ScenePath);
        EditorSceneManager.OpenScene(Stage345AssetSetup.ScenePath, OpenSceneMode.Single);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Stage345AssetSetup.ScenePath);
        File.WriteAllText(Report, "Unity " + Application.unityVersion + "\n");
        SessionState.SetBool(Key+"Active",true);
        SessionState.SetInt(Key+"Errors",0);
        SessionState.SetFloat(Key+"Deadline",(float)EditorApplication.timeSinceStartup+180);
        sampled=false;first=null;firstBones=null;clips=null;nextTick=EditorApplication.timeSinceStartup+1;
        EditorApplication.EnterPlaymode();
    }

    static void OnLog(string message,string stack,LogType type)
    {
        if (!SessionState.GetBool(Key+"Active",false)) return;
        if (type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert) return;
        SessionState.SetInt(Key+"Errors",SessionState.GetInt(Key+"Errors",0)+1);
        File.AppendAllText(Report,"ERROR "+message+"\n"+stack+"\n");
    }

    static void Need(bool condition,string detail)
    {
        if (!condition) throw new InvalidOperationException(detail);
    }

    static Vector3[] Bake(SkinnedMeshRenderer skin)
    {
        var mesh=new Mesh();skin.BakeMesh(mesh);
        var result=mesh.vertices;UnityEngine.Object.DestroyImmediate(mesh);return result;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key+"Active",false)) return;
        try
        {
            Need(EditorApplication.timeSinceStartup<SessionState.GetFloat(Key+"Deadline",0),"Play Mode timeout.");
            Need(SessionState.GetInt(Key+"Errors",0)==0,"Unity logged a runtime error.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling ||
                EditorApplication.timeSinceStartup<nextTick) return;
            Need(SceneManager.GetActiveScene().path==Stage345AssetSetup.ScenePath,
                 "Wrong Play scene: "+SceneManager.GetActiveScene().path);
            var actors=NewActors.Select(entry=>
                UnityEngine.Object.FindObjectsByType<Animator>()
                .FirstOrDefault(a=>a.name==entry.name)).ToArray();
            Need(actors.All(a=>a!=null),"Missing new actors in review scene.");
            if (!sampled)
            {
                first=new Vector3[actors.Length][];
                firstBones=new Quaternion[actors.Length][];
                clips=new AnimationClip[actors.Length];
                for(int i=0;i<actors.Length;i++)
                {
                    var actor=actors[i];var skin=actor.GetComponentInChildren<SkinnedMeshRenderer>();
                    Need(actor.avatar!=null && actor.avatar.isValid && actor.isHuman==NewActors[i].human,
                         NewActors[i].name+" invalid Avatar.");
                    Need(skin!=null && skin.bones.Length>15,NewActors[i].name+" missing skin.");
                    Need(skin.sharedMaterial!=null && skin.sharedMaterial.mainTexture!=null &&
                         skin.sharedMaterial.shader.isSupported,NewActors[i].name+" missing material/texture.");
                    string stage=i<2?"Stage3":"Stage5";
                    string path=$"Assets/CharacterRigging/{stage}/{NewActors[i].name}/Models/{NewActors[i].name}.fbx";
                    clips[i]=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                        .FirstOrDefault(c=>!c.name.StartsWith("__"));
                    Need(clips[i]!=null,NewActors[i].name+" missing imported clip.");
                    File.AppendAllText(Report,$"CLIP {NewActors[i].name}: curves={AnimationUtility.GetCurveBindings(clips[i]).Length}, length={clips[i].length:F3}s\n");
                    actor.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                    actor.speed=1;
                    File.AppendAllText(Report,$"ANIMATOR {NewActors[i].name}: enabled={actor.enabled}, active={actor.gameObject.activeInHierarchy}, controller={actor.runtimeAnimatorController?.name ?? "null"}, layerWeight={actor.GetLayerWeight(0):F2}\n");
                    actor.Rebind();actor.Update(.001f);
                    actor.Play("PoseCheck",0,0);actor.Update(0);actor.Update(.5f);
                    var controllerState=actor.GetCurrentAnimatorStateInfo(0);
                    var controllerPose=Bake(skin);
                    clips[i].SampleAnimation(actor.gameObject,0);
                    var restPose=Bake(skin);
                    float controllerDelta=controllerPose.Zip(restPose,(a,b)=>Vector3.Distance(a,b)).Max();
                    File.AppendAllText(Report,$"CONTROLLER {NewActors[i].name}: stateHash={controllerState.shortNameHash}, time={controllerState.normalizedTime:F3}, differenceFromRest={controllerDelta:F4}m\n");
                    Need(controllerDelta>.01f && controllerState.normalizedTime>.1f,
                         NewActors[i].name+" controller did not advance PoseCheck.");
                    actor.enabled=false;clips[i].SampleAnimation(actor.gameObject,0);
                    first[i]=Bake(skin);
                    firstBones[i]=skin.bones.Select(b=>b.localRotation).ToArray();
                    File.AppendAllText(Report,$"PASS {NewActors[i].name}: Avatar human={actor.isHuman}, bones={skin.bones.Length}, vertices={first[i].Length}\n");
                }
                sampled=true;
                nextTick=EditorApplication.timeSinceStartup+1;
                return;
            }
            for(int i=0;i<actors.Length;i++)
            {
                var actor=actors[i];actor.speed=1;
                clips[i].SampleAnimation(actor.gameObject,.5f);
                var skin=actor.GetComponentInChildren<SkinnedMeshRenderer>();
                var current=Bake(skin);
                Need(current.Length==first[i].Length,NewActors[i].name+" vertex count changed.");
                float delta=current.Zip(first[i],(a,b)=>Vector3.Distance(a,b)).Max();
                float boneDelta=skin.bones.Select((b,j)=>Quaternion.Angle(b.localRotation,firstBones[i][j])).Max();
                File.AppendAllText(Report,$"SAMPLE {NewActors[i].name}: mesh={delta:F4}m, bone={boneDelta:F3}deg, state={actor.GetCurrentAnimatorStateInfo(0).normalizedTime:F3}\n");
                Need(delta>.01f && delta<2,NewActors[i].name+" invalid deformation "+delta);
                Need(current.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),
                     NewActors[i].name+" nonfinite mesh.");
                File.AppendAllText(Report,$"PASS {NewActors[i].name}: deformation={delta:F4}m\n");
            }
            CheckLODs();CheckMount();Capture();
            Need(SessionState.GetInt(Key+"Errors",0)==0,"Unity logged a runtime error.");
            File.AppendAllText(Report,"STAGE345_RUNTIME_PASSED: 0 runtime errors.\n");
            Finish(0);
        }
        catch(Exception error)
        {
            File.AppendAllText(Report,"FAIL "+error+"\n");Finish(1);
        }
    }

    static void CheckLODs()
    {
        foreach(string name in new[]{"HeraldicKnight","Farmhand","Spearman","RedDeer"})
        {
            string path=$"Assets/CharacterRigging/LODs/{name}/{name}_LODs.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Need(prefab!=null,"Missing LOD prefab "+path);
            var obj=UnityEngine.Object.Instantiate(prefab);
            try
            {
                var group=obj.GetComponent<LODGroup>();Need(group!=null,name+" missing LODGroup.");
                var levels=group.GetLODs();Need(levels.Length==3,name+" wrong LOD count.");
                var baseSkin=levels[0].renderers[0] as SkinnedMeshRenderer;
                Need(baseSkin!=null && baseSkin.rootBone!=null,name+" missing base rig.");
                var baseBones=baseSkin.bones;
                foreach(var level in levels)
                {
                    Need(level.renderers.Length==1 && level.renderers[0]!=null,name+" missing LOD renderer.");
                    var skin=level.renderers[0] as SkinnedMeshRenderer;
                    Need(skin!=null && skin.rootBone==baseSkin.rootBone &&
                         skin.bones.All(b=>b!=null && baseBones.Contains(b)),
                         name+" LOD bones not mapped to base rig.");
                    Need(skin.sharedMesh!=null &&
                         skin.sharedMesh.bindposes.Length==skin.bones.Length,
                         name+" LOD bind poses do not match remapped bones.");
                    Need(skin.sharedMaterial!=null && skin.sharedMaterial.shader.isSupported,
                         name+" LOD material unsupported.");
                }
                File.AppendAllText(Report,"PASS "+name+": three mapped LODs.\n");
            }
            finally {UnityEngine.Object.DestroyImmediate(obj);}
        }
    }

    static void CheckMount()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/CharacterRigging/Stage5/MountedFitPrototype.prefab");
        Need(prefab!=null,"Missing mounted prototype.");
        var rider=prefab.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t=>t.name=="ArmoredKnight_Rider");
        Need(rider!=null && rider.parent!=null && rider.parent.name=="Spine",
             "Rider not attached to horse Spine.");
        Need(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length==2,
             "Mounted prototype missing one of its two skins.");
        File.AppendAllText(Report,"PASS mounted fit prefab: separate rider and horse rigs.\n");
    }

    static void Capture()
    {
        var camera=Camera.main;Need(camera!=null,"No camera.");
        var previous=camera.targetTexture;var active=RenderTexture.active;
        var rt=new RenderTexture(1280,800,24);var image=new Texture2D(1280,800,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,1280,800),0,0);image.Apply();
            var pixels=image.GetPixels32();
            int pink=pixels.Count(p=>p.r>220 && p.g<60 && p.b>220);
            Need(pink<pixels.Length*.005,"Pink pixels in review render.");
            File.WriteAllBytes(Screenshot,image.EncodeToPNG());
            File.AppendAllText(Report,$"PASS camera screenshot: magenta={(float)pink/pixels.Length:P3}.\n");
        }
        finally
        {
            camera.targetTexture=previous;RenderTexture.active=active;
            rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
        }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Key+"Active",false);
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
    }
}
