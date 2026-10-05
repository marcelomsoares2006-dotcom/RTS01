using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Validates the mounted hold prefab and captures a multi-angle Unity review.</summary>
[InitializeOnLoad]
public static class Stage5MountedRuntimeCheck
{
    const string Key = "Stage5.MountedRuntime.";
    const string ScenePath = "Assets/CharacterRigging/Stage5/MountedReview.unity";
    const string Report = "Docs/CharacterMeshAudit/Stage5/unity-mounted.txt";
    const string Output = "Docs/CharacterMeshAudit/Stage5/";
    static double nextTick;

    static Stage5MountedRuntimeCheck()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    [MenuItem("Tools/Character Rigging/Stages 3-5/Build and Validate Mounted Preview")]
    public static void BuildAndRun()
    {
        Stage345AssetSetup.BuildMountedFitPrototype();
        Run();
    }

    [MenuItem("Tools/Character Rigging/Stages 3-5/Validate Mounted Preview")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode first.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(ScenePath)) throw new FileNotFoundException(ScenePath);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        File.WriteAllText(Report,"Unity "+Application.unityVersion+"\n");
        SessionState.SetBool(Key+"Active",true);
        SessionState.SetInt(Key+"Errors",0);
        SessionState.SetFloat(Key+"Deadline",(float)EditorApplication.timeSinceStartup+180);
        nextTick=EditorApplication.timeSinceStartup+1;
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

    static void Tick()
    {
        if (!SessionState.GetBool(Key+"Active",false)) return;
        try
        {
            Need(EditorApplication.timeSinceStartup<SessionState.GetFloat(Key+"Deadline",0),
                "Mounted Play Mode timeout.");
            Need(SessionState.GetInt(Key+"Errors",0)==0,"Runtime error logged.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling ||
                EditorApplication.timeSinceStartup<nextTick) return;
            Need(SceneManager.GetActiveScene().path==ScenePath,"Wrong mounted review scene.");
            var mount=UnityEngine.Object.FindObjectsByType<Transform>()
                .FirstOrDefault(t=>t.name=="MountedFitPrototype");
            Need(mount!=null,"Mounted prefab missing in Play Mode.");
            var animators=mount.GetComponentsInChildren<Animator>(true);
            var rider=animators.FirstOrDefault(a=>a.name=="ArmoredKnight_Rider");
            var horse=animators.FirstOrDefault(a=>a.name=="MountedFitPrototype");
            Need(rider!=null && horse!=null,"Horse/rider Animators missing.");
            Need(!horse.enabled && rider.enabled && rider.avatar!=null &&
                 rider.avatar.isValid && !rider.avatar.isHuman,
                "Mounted Animator states are invalid.");
            Need(rider.runtimeAnimatorController!=null &&
                 rider.runtimeAnimatorController.name=="MountedIdle",
                 "Rider does not use mounted hold controller.");
            var sockets=mount.GetComponent<MountedSockets>();
            Need(sockets!=null && sockets.IsConfigured,"Mounted sockets are not configured.");
            Need(rider.transform.parent==sockets.riderRoot &&
                 sockets.riderRoot.parent==sockets.saddleRoot,
                "Rider is not attached through RiderRoot/SaddleRoot.");
            var skins=mount.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Need(skins.Length==2 && skins.All(s=>s.sharedMaterial!=null &&
                 s.sharedMaterial.shader.isSupported),"Missing mounted skins/materials.");
            rider.Rebind();rider.Play("MountedIdle",0,0);rider.Update(0);rider.Update(.5f);
            var state=rider.GetCurrentAnimatorStateInfo(0);
            Need(state.shortNameHash==Animator.StringToHash("MountedIdle") &&
                 state.normalizedTime>.1f,"Mounted hold controller failed to advance.");
            var riderSkin=rider.GetComponentInChildren<SkinnedMeshRenderer>();
            var hip=riderSkin.bones.FirstOrDefault(b=>b.name=="mixamorig:Hips");
            Need(hip!=null,"Rider hips missing.");
            var seatTarget=sockets.saddleRoot.position;
            float seatError=Vector3.Distance(hip.position,seatTarget);
            Need(seatError<.15f,$"Rider no longer aligned with saddle: {seatError:F3}m.");
            var thighs=riderSkin.bones.Where(b=>b.name.EndsWith("UpLeg")).ToArray();
            Need(thighs.Length==2 && thighs.All(b=>Quaternion.Angle(Quaternion.identity,b.localRotation)>10),
                "Mounted legs are not bent.");
            File.AppendAllText(Report,$"PASS mounted controller: time={state.normalizedTime:F3}, rider={rider.transform.position}, hips={hip.position}, seat error={seatError:F3}m, legs bent.\n");
            CaptureAngles();
            Need(SessionState.GetInt(Key+"Errors",0)==0,"Runtime error logged.");
            File.AppendAllText(Report,"STAGE5_MOUNTED_PASSED: 10 camera angles, 0 runtime errors.\n");
            Finish(0);
        }
        catch(Exception error)
        {
            File.AppendAllText(Report,"FAIL "+error+"\n");Finish(1);
        }
    }

    static void CaptureAngles()
    {
        var camera=Camera.main;Need(camera!=null,"Mounted review camera missing.");
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        var oldPosition=camera.transform.position;var oldRotation=camera.transform.rotation;
        var rt=new RenderTexture(960,720,24);var image=new Texture2D(960,720,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture=rt;
            for(int i=0;i<10;i++)
            {
                float angle=i<8?i*45f:45f;
                float height=i==8?5.0f:i==9?1.5f:2.6f;
                var direction=Quaternion.Euler(0,angle,0)*new Vector3(0,0,4.3f);
                camera.transform.position=new Vector3(direction.x,height,direction.z);
                camera.transform.LookAt(new Vector3(0,1.15f,0));
                camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();
                var pixels=image.GetPixels32();
                int magenta=pixels.Count(p=>p.r>220 && p.g<60 && p.b>220);
                Need(magenta<pixels.Length*.005,$"Pink pixels at angle {i}.");
                File.WriteAllBytes(Output+$"MountedUnity-{i:00}.png",image.EncodeToPNG());
            }
            File.AppendAllText(Report,"PASS 10 angle captures: magenta <0.5% each.\n");
        }
        finally
        {
            camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
            camera.transform.position=oldPosition;camera.transform.rotation=oldRotation;
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
