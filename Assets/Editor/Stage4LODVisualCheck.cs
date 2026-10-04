using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Renders each accepted LOD at identical framing for visual comparison.</summary>
public static class Stage4LODVisualCheck
{
    const string Output = "Docs/CharacterMeshAudit/Stage4/";
    static readonly string[] Names = {"HeraldicKnight","Farmhand","Spearman","RedDeer"};

    [MenuItem("Tools/Character Rigging/Stages 3-5/Build and Capture LOD Comparison")]
    public static void BuildAndRun()
    {
        Stage4LODSetup.Build();
        Run();
    }

    [MenuItem("Tools/Character Rigging/Stages 3-5/Capture LOD Comparison")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before LOD comparison.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var oldScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("LOD Review Camera").AddComponent<Camera>();
        camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.12f,.16f,.20f);
        var light=new GameObject("LOD Review Light").AddComponent<Light>();
        light.type=LightType.Directional;light.intensity=1.3f;
        light.transform.rotation=Quaternion.Euler(45,-35,0);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight=new Color(.65f,.65f,.67f);
        var rt=new RenderTexture(960,720,24);
        var image=new Texture2D(960,720,TextureFormat.RGB24,false);
        var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
        File.WriteAllText(Output+"unity-lod-visual.txt","Unity "+Application.unityVersion+"\n");
        int exitCode=0;
        try
        {
            camera.targetTexture=rt;
            foreach(var name in Names)
            {
                string path=$"Assets/CharacterRigging/LODs/{name}/{name}_LODs.prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Need(prefab!=null,"Missing "+path);
                var obj=UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var group=obj.GetComponent<LODGroup>();Need(group!=null,name+" missing LODGroup.");
                    var levels=group.GetLODs();Need(levels.Length==3,name+" LOD count is not three.");
                    // Disable automatic distance selection while comparing identical framing.
                    group.enabled=false;
                    var baseSkin=levels[0].renderers[0] as SkinnedMeshRenderer;
                    Need(baseSkin!=null,name+" missing base skin.");
                    var center=baseSkin.bounds.center;
                    float size=Mathf.Max(1.3f,baseSkin.bounds.extents.magnitude*1.2f);
                    camera.orthographicSize=size;
                    for(int view=0;view<2;view++)
                    {
                        var axis=view==0?Vector3.forward:Vector3.right;
                        camera.transform.position=center+axis*5;
                        camera.transform.LookAt(center);
                        for(int lod=0;lod<3;lod++)
                        {
                            for(int k=0;k<3;k++) levels[k].renderers[0].enabled=k==lod;
                            camera.Render();RenderTexture.active=rt;
                            image.ReadPixels(new Rect(0,0,960,720),0,0);image.Apply();
                            var pixels=image.GetPixels32();
                            int pink=pixels.Count(p=>p.r>220 && p.g<60 && p.b>220);
                            int foreground=pixels.Count(p=>Mathf.Abs(p.r-31)+
                                Mathf.Abs(p.g-41)+Mathf.Abs(p.b-51)>35);
                            Need(pink<pixels.Length*.005,name+" LOD"+lod+" has pink pixels.");
                            Need(foreground>1500,name+" LOD"+lod+" appears empty.");
                            string side=view==0?"front":"side";
                            File.WriteAllBytes(Output+$"Unity-{name}-LOD{lod}-{side}.png",
                                image.EncodeToPNG());
                            File.AppendAllText(Output+"unity-lod-visual.txt",
                                $"PASS {name} LOD{lod} {side}: foreground={foreground}, magenta={pink}\n");
                        }
                    }
                }
                finally {UnityEngine.Object.DestroyImmediate(obj);}
            }
            File.AppendAllText(Output+"unity-lod-visual.txt","STAGE4_LOD_VISUAL_CAPTURED: 24 images.\n");
        }
        catch(Exception error)
        {
            exitCode=1;
            File.AppendAllText(Output+"unity-lod-visual.txt","FAIL "+error+"\n");
            if (!Application.isBatchMode) throw;
        }
        finally
        {
            camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
            rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
            if (!string.IsNullOrEmpty(oldScene) && File.Exists(oldScene))
                EditorSceneManager.OpenScene(oldScene);
            if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        }
    }

    static void Need(bool condition,string detail)
    {
        if (!condition) throw new InvalidOperationException(detail);
    }
}
