using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Validates root-controlled movement without requiring horse root motion.</summary>
[InitializeOnLoad]
public static class Stage5MountedLocomotionRuntimeCheck
{
    const string Key = "Stage5.MountedLocomotion.";
    const string ScenePath = "Assets/CharacterRigging/Stage5/MountedReview.unity";
    const string Report = "Docs/CharacterMeshAudit/Stage5/unity-mounted-locomotion.txt";
    static double nextTick;

    [MenuItem("Tools/Character Rigging/Stages 3-5/Validate Mounted Locomotion")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode first.");
        if (!File.Exists(ScenePath)) throw new FileNotFoundException(ScenePath);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        File.WriteAllText(Report, "Unity " + Application.unityVersion + "\n");
        SessionState.SetBool(Key + "Active", true);
        SessionState.SetInt(Key + "Errors", 0);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 60);
        nextTick = EditorApplication.timeSinceStartup + 1;
        EditorApplication.EnterPlaymode();
    }

    static Stage5MountedLocomotionRuntimeCheck()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Key + "Active", false)) return;
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        SessionState.SetInt(Key + "Errors", SessionState.GetInt(Key + "Errors", 0) + 1);
        File.AppendAllText(Report, "ERROR " + message + "\n" + stack + "\n");
    }

    static void Need(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException(detail);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key + "Active", false)) return;
        try
        {
            Need(EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "Deadline", 0), "Mounted locomotion timeout.");
            Need(SessionState.GetInt(Key + "Errors", 0) == 0, "Runtime error logged.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextTick) return;
            var mount = UnityEngine.Object.FindObjectsByType<Transform>()
                .FirstOrDefault(t => t.name == "MountedFitPrototype");
            Need(mount != null, "Mounted prefab missing.");
            var sockets = mount.GetComponent<MountedSockets>();
            Need(sockets != null && sockets.IsConfigured, "Mounted sockets missing.");
            var rider = mount.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "ArmoredKnight_Rider");
            Need(rider != null && rider.parent == sockets.riderRoot, "Rider hierarchy invalid.");
            if (!SessionState.GetBool(Key + "Moving", false))
            {
                SessionState.SetBool(Key + "Moving", true);
                SessionState.SetFloat(Key + "StartTime", Time.realtimeSinceStartup);
                SessionState.SetVector3(Key + "InitialMount", mount.position);
                SessionState.SetVector3(Key + "InitialRider", rider.position);
                SessionState.SetVector3(Key + "InitialSaddle", sockets.saddleRoot.position);
                nextTick = EditorApplication.timeSinceStartup + .1;
                return;
            }
            if (Time.realtimeSinceStartup - SessionState.GetFloat(Key + "StartTime", 0) < 2f)
            {
                mount.position += Vector3.right * 0.5f * Mathf.Max(Time.deltaTime, .01f);
                nextTick = EditorApplication.timeSinceStartup + .02;
                return;
            }
            Vector3 initialMount = SessionState.GetVector3(Key + "InitialMount", mount.position);
            Vector3 initialRider = SessionState.GetVector3(Key + "InitialRider", rider.position);
            Vector3 initialSaddle = SessionState.GetVector3(Key + "InitialSaddle", sockets.saddleRoot.position);
            float distance = Vector3.Distance(initialMount, mount.position);
            float riderDelta = Vector3.Distance(initialRider, rider.position);
            float saddleDelta = Vector3.Distance(initialSaddle, sockets.saddleRoot.position);
            Need(distance > .25f, $"Mounted root did not move enough: {distance:F3}m.");
            Need(Mathf.Abs(distance - riderDelta) < .03f, $"Rider drifted from root: root={distance:F3}m rider={riderDelta:F3}m.");
            Need(Mathf.Abs(distance - saddleDelta) < .03f, $"Saddle drifted from root: root={distance:F3}m saddle={saddleDelta:F3}m.");
            Need(distance < 2f, $"Mounted root moved an implausible distance: {distance:F3}m.");
            File.AppendAllText(Report, $"PASS synthetic mounted root motion: root={distance:F3}m rider={riderDelta:F3}m saddle={saddleDelta:F3}m errors=0\n");
            Finish(0);
        }
        catch (Exception error)
        {
            File.AppendAllText(Report, "FAIL " + error + "\n");
            Finish(1);
        }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Key + "Active", false);
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
    }
}
