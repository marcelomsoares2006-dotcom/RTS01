using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Stage5MountedNavMeshRuntimeCheck
{
    const string Key = "Stage5.MountedNavMesh.";
    const string ScenePath = "Assets/CharacterRigging/Stage5/MountedFitPrototypeNavMesh.unity";
    const string Report = "Docs/CharacterMeshAudit/Stage5/unity-mounted-navmesh.txt";
    static double nextTick;
    static Stage5MountedNavMeshRuntimeCheck() { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; }

    [MenuItem("Tools/Character Rigging/Stages 3-5/Validate Mounted NavMesh Movement")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (!File.Exists(ScenePath)) throw new FileNotFoundException(ScenePath);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        File.WriteAllText(Report, "Unity " + Application.unityVersion + "\n");
        SessionState.SetBool(Key + "Active", true);
        SessionState.SetInt(Key + "Errors", 0);
        SessionState.SetInt(Key + "Phase", 0);
        SessionState.SetFloat(Key + "Deadline", (float)EditorApplication.timeSinceStartup + 90);
        nextTick = EditorApplication.timeSinceStartup + 1;
        EditorApplication.EnterPlaymode();
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Key + "Active", false)) return;
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        SessionState.SetInt(Key + "Errors", SessionState.GetInt(Key + "Errors", 0) + 1);
        File.AppendAllText(Report, "ERROR " + message + "\n" + stack + "\n");
    }

    static void Need(bool value, string detail) { if (!value) throw new InvalidOperationException(detail); }

    static void Tick()
    {
        if (!SessionState.GetBool(Key + "Active", false)) return;
        try
        {
            Need(EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "Deadline", 0), "NavMesh test timeout.");
            Need(SessionState.GetInt(Key + "Errors", 0) == 0, "Runtime error logged.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextTick) return;
            var mount = UnityEngine.Object.FindObjectsByType<Transform>().FirstOrDefault(t => t.name == "MountedFitPrototype");
            var destination = UnityEngine.Object.FindObjectsByType<Transform>().FirstOrDefault(t => t.name == "MountedDestination");
            Need(mount != null && destination != null, "Mounted test objects missing.");
            var agent = mount.GetComponent<NavMeshAgent>();
            var driver = mount.GetComponent<MountedMovementDriver>();
            var sockets = mount.GetComponent<MountedSockets>();
            Need(agent != null && driver != null && sockets != null && sockets.IsConfigured, "Mounted movement components missing.");
            var ik = sockets.riderRoot.GetComponentInChildren<MountedIKController>();
            Need(ik != null && ik.sockets == sockets && ik.riderAnimator != null,
                "Mounted IK controller is not linked to rider and sockets.");
            Need(sockets.leftFootTarget != null && sockets.rightFootTarget != null &&
                 sockets.leftHandTarget != null && sockets.rightHandTarget != null,
                "Mounted IK targets are incomplete.");
            var mountedController = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                "Assets/CharacterRigging/Stage5/Mounted/MountedIdle.controller");
            Need(mountedController != null && mountedController.layers.Length > 0 &&
                 mountedController.layers[0].iKPass, "Mounted Animator IK Pass is disabled.");
            int phase = SessionState.GetInt(Key + "Phase", 0);
            if (phase == 0)
            {
                Globals.NAV_MESH_SURFACE = null;
                agent.enabled = true;
                Need(Globals.TryWarpToNavMesh(agent, mount.position, 4f), "Mounted agent could not join NavMesh.");
                driver.acceptCommands = true;
                agent.isStopped = true;
                agent.ResetPath();
                SessionState.SetVector3(Key + "Start", mount.position);
                SessionState.SetVector3(Key + "RiderStart", sockets.riderRoot.position);
                SessionState.SetVector3(Key + "SaddleStart", sockets.saddleRoot.position);
                SessionState.SetVector3(Key + "RiderLocal", sockets.riderRoot.localPosition);
                SessionState.SetVector3(Key + "SaddleLocal", sockets.saddleRoot.localPosition);
                Vector3 destinationPosition = destination.position;
                Need(driver.MoveTo(destination.position), "SetDestination was rejected.");
                Need(agent.hasPath || agent.pathPending, "NavMeshAgent did not create a path.");
                agent.isStopped = false;
                SessionState.SetVector3(Key + "Destination", destinationPosition);
                SessionState.SetInt(Key + "Phase", 1);
                nextTick = EditorApplication.timeSinceStartup + 2;
                return;
            }
            if (phase == 1)
            {
                Vector3 start = SessionState.GetVector3(Key + "Start", mount.position);
                Vector3 riderStart = SessionState.GetVector3(Key + "RiderStart", sockets.riderRoot.position);
                Vector3 saddleStart = SessionState.GetVector3(Key + "SaddleStart", sockets.saddleRoot.position);
                Vector3 destinationPosition = SessionState.GetVector3(Key + "Destination", destination.position);
                float rootDistance = Vector3.Distance(start, mount.position);
                float riderDistance = Vector3.Distance(riderStart, sockets.riderRoot.position);
                float saddleDistance = Vector3.Distance(saddleStart, sockets.saddleRoot.position);
                Vector3 expectedRider = sockets.riderRoot.parent.TransformPoint(SessionState.GetVector3(Key + "RiderLocal", sockets.riderRoot.localPosition));
                Vector3 expectedSaddle = sockets.saddleRoot.parent.TransformPoint(SessionState.GetVector3(Key + "SaddleLocal", sockets.saddleRoot.localPosition));
                float riderOffsetError = Vector3.Distance(expectedRider, sockets.riderRoot.position);
                float saddleOffsetError = Vector3.Distance(expectedSaddle, sockets.saddleRoot.position);
                Need(rootDistance > .1f, $"Mounted root did not move: {rootDistance:F3}m.");
                Need(agent.pathStatus == NavMeshPathStatus.PathComplete, $"Mounted path is {agent.pathStatus}.");
                Need(Vector3.Distance(mount.position, destinationPosition) < 1.2f,
                    $"Mounted unit did not approach destination: {Vector3.Distance(mount.position, destinationPosition):F3}m.");
                Need(riderOffsetError < .08f, $"Rider drifted from mounted root: {riderOffsetError:F3}m.");
                Need(saddleOffsetError < .08f, $"Saddle drifted from mounted root: {saddleOffsetError:F3}m.");
                Need(Vector3.Distance(sockets.riderRoot.localPosition, SessionState.GetVector3(Key + "RiderLocal", sockets.riderRoot.localPosition)) < .001f,
                    "RiderRoot local offset changed.");
                Need(Vector3.Distance(sockets.saddleRoot.localPosition, SessionState.GetVector3(Key + "SaddleLocal", sockets.saddleRoot.localPosition)) < .001f,
                    "SaddleRoot local offset changed.");
                File.AppendAllText(Report, $"PASS mounted NavMesh movement: root={rootDistance:F3}m riderDelta={riderDistance:F3}m saddleDelta={saddleDistance:F3}m riderOffsetError={riderOffsetError:F3}m saddleOffsetError={saddleOffsetError:F3}m errors=0\n");
                Finish(0);
            }
        }
        catch (Exception error) { File.AppendAllText(Report, "FAIL " + error + "\n"); Finish(1); }
    }

    static void Finish(int code)
    {
        SessionState.SetBool(Key + "Active", false);
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
    }
}
