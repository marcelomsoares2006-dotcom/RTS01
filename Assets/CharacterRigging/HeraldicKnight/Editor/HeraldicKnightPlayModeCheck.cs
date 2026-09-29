using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Etapa 1, verification 3: plays the demo scene, drives the four Animator states and captures
/// real Unity renders (front camera + rear camera) into TestResults/heraldic-*.png.
/// Batch: -executeMethod HeraldicKnightPlayModeCheck.Run (exits with 0 on success).
/// Also lets Play start from the demo scene although EraImperialProjectSetup forces Core.unity.
/// </summary>
[InitializeOnLoad]
public static class HeraldicKnightPlayModeCheck
{
    private const string Prefix = "HeraldicKnight.PlayCheck.";
    private const string ReportPath = "TestResults/heraldic-playmode.txt";

    private static int stage;
    private static double nextStep;
    private static Vector3[] firstSample;
    private static float maxDelta, maxHipsDrift;
    private static Vector3 knightStart;
    private static double selectedAt;

    static HeraldicKnightPlayModeCheck()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Application.logMessageReceived += OnLog;
    }

    // EraImperialProjectSetup sets playModeStartScene = Core.unity for the game. For the isolated
    // demo scene we clear it just before entering Play and restore Core afterwards.
    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode &&
            SceneManager.GetActiveScene().path == HeraldicKnightSetup.ScenePath &&
            EditorSceneManager.playModeStartScene != null)
        {
            SessionState.SetString(Prefix + "RestoreStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene = null;
        }
        else if (change == PlayModeStateChange.EnteredEditMode)
        {
            string restore = SessionState.GetString(Prefix + "RestoreStart", "");
            if (restore.Length > 0)
            {
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(restore);
                SessionState.EraseString(Prefix + "RestoreStart");
            }
        }
    }

    [MenuItem("Tools/Character Rigging/Heraldic Knight/Run Play Mode Check")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(Prefix + "Running", false))
            throw new InvalidOperationException("Stop the current Play Mode/check before starting another.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        EditorSceneManager.OpenScene(HeraldicKnightSetup.ScenePath, OpenSceneMode.Single);
        Directory.CreateDirectory("TestResults");
        File.WriteAllText(ReportPath, "Heraldic Knight Play Mode check\nUnity " + Application.unityVersion +
            "  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\nGraphics: " + SystemInfo.graphicsDeviceType +
            " / " + SystemInfo.graphicsDeviceName + "\n");
        SessionState.SetBool(Prefix + "Running", true);
        SessionState.SetInt(Prefix + "Errors", 0);
        SessionState.SetFloat(Prefix + "Deadline", (float)EditorApplication.timeSinceStartup + 300f);
        stage = 0;
        nextStep = 0;
        firstSample = null;
        maxDelta = maxHipsDrift = 0;
        EditorApplication.EnterPlaymode();
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Prefix + "Running", false)) return;
        if (type == LogType.Warning) { File.AppendAllText(ReportPath, "WARNING (console): " + message + "\n"); return; }
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        SessionState.SetInt(Prefix + "Errors", SessionState.GetInt(Prefix + "Errors", 0) + 1);
        File.AppendAllText(ReportPath, "ERROR (console): " + message + "\n" + stack + "\n");
    }

    private static void Record(string message)
    {
        File.AppendAllText(ReportPath, message + "\n");
        Debug.Log("[Heraldic play check] " + message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static Vector3[] Bake(SkinnedMeshRenderer renderer)
    {
        var mesh = new Mesh();
        renderer.BakeMesh(mesh, true);
        var result = mesh.vertices.Select(v => renderer.transform.TransformPoint(v)).ToArray();
        UnityEngine.Object.DestroyImmediate(mesh);
        return result;
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Prefix + "Running", false)) return;
        try
        {
            if (SessionState.GetInt(Prefix + "Errors", 0) > 0)
                throw new InvalidOperationException("Console errors were logged.");
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Prefix + "Deadline", 0))
                throw new TimeoutException("Play mode check exceeded five minutes; active scene: " + SceneManager.GetActiveScene().path);
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (EditorApplication.timeSinceStartup < nextStep) return;

            var demo = UnityEngine.Object.FindAnyObjectByType<HeraldicKnightDemo>();
            if (demo == null) return;
            var animator = demo.knight;
            var renderer = animator.GetComponentInChildren<SkinnedMeshRenderer>();
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var camera = Camera.main;

            if (stage == 0)
            {
                Require(SceneManager.GetActiveScene().path == HeraldicKnightSetup.ScenePath,
                    "Play started in " + SceneManager.GetActiveScene().path + " instead of the demo scene.");
                Require(animator.isHuman && animator.avatar.isValid, "Animator avatar is not a valid Humanoid at runtime.");
                Require(renderer.sharedMaterial.shader.isSupported && renderer.sharedMaterial.mainTexture != null, "Material/texture invalid.");
                Require(camera != null, "No main camera.");
                Record($"Scene {SceneManager.GetActiveScene().name} playing; avatar human={animator.isHuman}; " +
                       $"material={renderer.sharedMaterial.name}/{renderer.sharedMaterial.shader.name}; bones={renderer.bones.Length}");
                demo.autoCycle = false;
                knightStart = animator.transform.position;
                stage = 1;
                nextStep = EditorApplication.timeSinceStartup + 0.5;
                return;
            }

            int index = (stage - 1) / 4;
            int step = (stage - 1) % 4;
            if (index >= HeraldicKnightDemo.States.Length)
            {
                Record($"Root drift: knight transform moved {Vector3.Distance(knightStart, animator.transform.position):F3} m; " +
                       $"max horizontal hips offset from root {maxHipsDrift:F3} m.");
                Require(Vector3.Distance(knightStart, animator.transform.position) < 0.01f, "Knight root moved away from spawn.");
                Require(maxHipsDrift < 0.6f, "Hips drifted more than 0.6 m from the root.");
                Record("HERALDIC_PLAYMODE_PASSED: 4 states, 0 console errors.");
                SessionState.SetBool(Prefix + "Running", false);
                Finish(0);
                return;
            }
            string state = HeraldicKnightDemo.States[index];
            bool attack = index >= 2;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            float hipsOffset = Vector2.Distance(new Vector2(hips.position.x, hips.position.z),
                                                new Vector2(animator.transform.position.x, animator.transform.position.z));
            maxHipsDrift = Mathf.Max(maxHipsDrift, hipsOffset);

            if (step == 0)
            {
                demo.Select(index);
                selectedAt = EditorApplication.timeSinceStartup;
                stage++;
                nextStep = EditorApplication.timeSinceStartup + 0.1;
            }
            else if (step == 1)
            {
                if (!info.IsName(state) || animator.IsInTransition(0))
                {
                    Require(EditorApplication.timeSinceStartup - selectedAt < 5,
                        $"{state}: Animator did not reach the state within 5 s (current hash {info.shortNameHash}).");
                    return;
                }
                firstSample = Bake(renderer);
                maxDelta = 0f;
                stage++;
                nextStep = EditorApplication.timeSinceStartup + (attack ? (index == 2 ? 0.55 : 1.1) : 0.3);
            }
            else if (step == 2)
            {
                Require(info.IsName(state), $"{state}: left the state too early.");
                var sample = Bake(renderer);
                maxDelta = Mathf.Max(maxDelta, firstSample.Zip(sample, (a, b) => Vector3.Distance(a, b)).Max());
                Require(maxDelta > 0.02f, $"{state}: mesh did not deform ({maxDelta:F4} m).");
                float height = sample.Max(v => v.y) - sample.Min(v => v.y);
                float footY = sample.Min(v => v.y);
                Capture(camera, $"TestResults/heraldic-{index + 1}-{state}-front.png");
                var rear = new GameObject("Rear capture").AddComponent<Camera>();
                rear.CopyFrom(camera);
                rear.transform.position = new Vector3(-1.9f, 1.5f, -3.4f);
                rear.transform.LookAt(new Vector3(0f, 0.9f, 0f));
                Capture(rear, $"TestResults/heraldic-{index + 1}-{state}-rear.png");
                UnityEngine.Object.DestroyImmediate(rear.gameObject);
                Record($"{state}: state OK, normalizedTime={info.normalizedTime:F2}, clipLength={info.length:F3}s, loop={info.loop}, " +
                       $"maxVertexDelta={maxDelta:F3} m, deformedHeight={height:F3} m, lowestVertexY={footY:F3} m, " +
                       $"hipsOffsetFromRoot={hipsOffset:F3} m");
                stage++;
                // Attacks: wait until the clip should have ended and the exit transition finished.
                nextStep = EditorApplication.timeSinceStartup + (attack ? info.length * (1f - info.normalizedTime) + 0.6 : 0.1);
            }
            else
            {
                if (attack)
                {
                    Require(info.IsName("Walking") && !animator.IsInTransition(0),
                        $"{state}: did not return to Walking after finishing (current hash {info.shortNameHash}).");
                    Record($"{state}: finished once (non-looping) and returned to Walking.");
                }
                else
                {
                    Require(info.IsName(state) && info.loop, $"{state}: loop state lost.");
                    Record($"{state}: still in loop state (normalizedTime={info.normalizedTime:F2}).");
                }
                stage++;
                nextStep = EditorApplication.timeSinceStartup + 0.1;
            }
        }
        catch (Exception exception)
        {
            Record("FAILED: " + exception.Message);
            SessionState.SetBool(Prefix + "Running", false);
            Finish(1);
        }
    }

    private static void Finish(int exitCode)
    {
        SessionState.SetBool(Prefix + "Running", false);
        if (Application.isBatchMode) EditorApplication.Exit(exitCode);
        else if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
    }

    private static void Capture(Camera camera, string path)
    {
        const int width = 1024, height = 768;
        var target = new RenderTexture(width, height, 24) { antiAliasing = 4 };
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previousTarget = camera.targetTexture, previousActive = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            int magenta = texture.GetPixels32().Count(p => p.r > 220 && p.g < 40 && p.b > 220);
            Record($"{path}: magenta pixels={(float)magenta / (width * height):P3}");
            Require(magenta < width * height / 200, "Pink/missing-shader pixels in render.");
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
