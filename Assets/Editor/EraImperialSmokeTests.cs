using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Three real Play Mode scenarios, including rendering, navigation and save/reload.</summary>
[InitializeOnLoad]
public static class EraImperialSmokeTests
{
    private const string Prefix = "EraImperial.Smoke.";
    private static double nextStep;
    private static Character movingWorker;
    private static Vector3 workerStart;

    static EraImperialSmokeTests()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
    }

    public static void Run()
    {
        Directory.CreateDirectory("TestResults");
        File.WriteAllText("TestResults/playmode-smoke.txt", "Unity " + Application.unityVersion + "\n");
        SessionState.SetBool(Prefix + "Running", true);
        SessionState.SetInt(Prefix + "Stage", 0);
        SessionState.SetInt(Prefix + "Cycle", 0);
        SessionState.SetInt(Prefix + "Errors", 0);
        SessionState.SetFloat(Prefix + "Deadline", (float)EditorApplication.timeSinceStartup + 600f);
        SessionState.SetString(Prefix + "DataPath", Path.GetFullPath("TestResults/SmokeData-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")));
        EditorSceneManager.OpenScene("Assets/Scenes/Core.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void IsolateSaves()
    {
        if (SessionState.GetBool(Prefix + "Running", false))
            BinarySerializable.DATA_DIRECTORY = SessionState.GetString(Prefix + "DataPath", "TestResults/SmokeData");
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Prefix + "Running", false)) return;
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        SessionState.SetInt(Prefix + "Errors", SessionState.GetInt(Prefix + "Errors", 0) + 1);
        File.AppendAllText("TestResults/playmode-smoke.txt", "ERROR: " + message + "\n" + stack + "\n");
    }

    private static void Record(string message)
    {
        File.AppendAllText("TestResults/playmode-smoke.txt", message + "\n");
        Debug.Log("[EraImperial smoke] " + message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Prefix + "Running", false)) return;
        try
        {
            if (SessionState.GetInt(Prefix + "Errors", 0) > 0)
                throw new InvalidOperationException("Runtime errors were logged; aborting immediately.");
            if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Prefix + "Deadline", 0))
                throw new TimeoutException("Smoke test exceeded ten minutes.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (EditorApplication.timeSinceStartup < nextStep) return;
            IsolateSaves();
            int cycle = SessionState.GetInt(Prefix + "Cycle", 0);
            int stage = SessionState.GetInt(Prefix + "Stage", 0);
            if (stage == 0)
            {
                MainMenuManager menu = UnityEngine.Object.FindAnyObjectByType<MainMenuManager>();
                if (menu == null || menu.newGameScrollview.childCount == 0) return;
                Require(GameManager.instance == null, "Game manager survived returning to the menu.");
                string mapName = cycle == 1 ? "Map2" : "Map1";
                MapData map = Resources.Load<MapData>("ScriptableObjects/Maps/" + mapName);
                Require(map != null, "Missing map metadata.");
                if (cycle == 2)
                {
                    CoreDataHandler.instance.SetGameUID(SessionState.GetString(Prefix + "SavedUID", ""));
                    CoreBooter.instance.LoadMap(mapName);
                }
                else
                {
                    Transform picker = menu.newGameScrollview.Cast<Transform>().First(item =>
                        item.Find("Data/Name").GetComponent<Text>().text == map.mapName);
                    picker.GetComponent<Button>().onClick.Invoke();
                    menu.StartNewGame();
                }
                Record($"Scenario {cycle + 1}: {(cycle == 2 ? "reload saved" : "new match")} {mapName}");
                SessionState.SetInt(Prefix + "Stage", 1);
                nextStep = EditorApplication.timeSinceStartup + 8;
            }
            else if (stage == 1)
            {
                if (GameManager.instance == null || Camera.main == null) return;
                Require(Globals.NAV_MESH_SURFACE != null, "NavMeshSurface was not initialized.");
                Require(NavMesh.CalculateTriangulation().vertices.Length > 0, "Navigation mesh is empty.");
                Require(Unit.UNITS_BY_OWNER != null && Unit.UNITS_BY_OWNER.Values.Sum(units => units.Count) >= 2,
                    "Initial buildings were not spawned/restored.");
                foreach (Terrain terrain in Terrain.activeTerrains)
                {
                    Require(terrain.materialTemplate != null && terrain.materialTemplate.shader.isSupported,
                        "Unsupported terrain material.");
                    Require(terrain.terrainData.treePrototypes.All(tree => tree != null && tree.prefab != null),
                        "Terrain still has missing tree prefabs.");
                }
                Require(UnityEngine.Object.FindObjectsByType<MinimapManager>().Any(minimap => minimap.enabled),
                    "Minimap has been disabled.");
                var spawnpoints = GameObject.Find("Spawnpoints").transform;
                Vector3 source = spawnpoints.GetChild(0).position;
                Vector3 target = spawnpoints.GetChild(1).position;
                Require(NavMesh.SamplePosition(source, out NavMeshHit start, 20, NavMesh.AllAreas), "No walkable start.");
                Require(NavMesh.SamplePosition(target, out NavMeshHit end, 20, NavMesh.AllAreas), "No walkable end.");
                var path = new NavMeshPath();
                Require(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) &&
                    path.status == NavMeshPathStatus.PathComplete, "No complete path between player spawnpoints.");
                if (cycle < 2)
                {
                    movingWorker = new Character(Globals.CHARACTER_DATA["worker"], GameManager.instance.gamePlayersParameters.myPlayerId);
                    var manager = movingWorker.Transform.GetComponent<CharacterManager>();
                    Require(manager.agent.Warp(start.position), "Worker could not be placed on navigation mesh.");
                    workerStart = movingWorker.Transform.position;
                    Require(manager.MoveTo(end.position, false), "Worker movement command was refused.");
                    SessionState.SetInt(Prefix + "Stage", 3);
                    nextStep = EditorApplication.timeSinceStartup + 3;
                    return;
                }
                FinishScenario(cycle);
            }
            else if (stage == 3)
            {
                Require(movingWorker != null && movingWorker.Transform != null, "Worker disappeared.");
                float distance = Vector3.Distance(workerStart, movingWorker.Transform.position);
                Require(distance > 0.5f, "Worker did not move after its order.");
                Record($"Scenario {cycle + 1}: worker moved {distance:F2} meters.");
                FinishScenario(cycle);
            }
            else
            {
                if (SceneManager.GetSceneByName("GameScene").isLoaded) return;
                Require(SceneManager.GetSceneByName("MainMenu").isLoaded, "Menu did not load.");
                Require(Time.timeScale == 1f, "Returning from pause did not restore the clock.");
                Require(SessionState.GetInt(Prefix + "Errors", 0) == 0, "Runtime errors were logged.");
                Record($"Scenario {cycle + 1}: PASSED including paused return to menu.");
                if (cycle == 2)
                {
                    Record("PLAYMODE_SMOKE_PASSED: 3 scenarios, 0 runtime errors.");
                    SessionState.SetBool(Prefix + "Running", false);
                    EditorApplication.Exit(0);
                }
                else
                {
                    SessionState.SetInt(Prefix + "Cycle", cycle + 1);
                    SessionState.SetInt(Prefix + "Stage", 0);
                }
            }
        }
        catch (Exception exception)
        {
            Record("FAILED: " + exception);
            SessionState.SetBool(Prefix + "Running", false);
            EditorApplication.Exit(1);
        }
    }

    private static void FinishScenario(int cycle)
    {
                Capture(Camera.main, $"TestResults/scenario-{cycle + 1}-game.png");
                Capture(GameManager.instance.minimapCamera, $"TestResults/scenario-{cycle + 1}-minimap.png");
                if (cycle == 0)
                {
                    SessionState.SetString(Prefix + "SavedUID", CoreDataHandler.instance.GameUID);
                    DataHandler.SaveGameData();
                    Require(File.Exists(Path.Combine(GameData.GetFolderPath(), GameData.DATA_FILE_NAME)), "Save file not written.");
                }
                if (cycle == 2)
                {
                    Require(GameData.Instance != null, "Saved game did not deserialize.");
                    Require(Unit.UNITS_BY_OWNER.Values.SelectMany(units => units).Any(unit => unit is Character),
                        "Saved worker was not restored.");
                }
                Record($"Scenario {cycle + 1}: rendered game/minimap; navigation path complete; units={Unit.UNITS_BY_OWNER.Values.Sum(units => units.Count)}.");
                // Test the formerly stuck transition while paused.
                EventManager.TriggerEvent("PausedGame");
                CoreBooter.instance.LoadMenu();
                SessionState.SetInt(Prefix + "Stage", 2);
                nextStep = EditorApplication.timeSinceStartup + 5;
    }

    private static void Capture(Camera camera, string path)
    {
        var target = new RenderTexture(1024, 768, 24);
        var texture = new Texture2D(1024, 768, TextureFormat.RGB24, false);
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        Rect previousRect = camera.rect;
        try
        {
            camera.targetTexture = target;
            camera.rect = new Rect(0, 0, 1, 1);
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            int magenta = texture.GetPixels32().Count(pixel => pixel.r > 220 && pixel.g < 40 && pixel.b > 220);
            float fraction = (float)magenta / (1024 * 768);
            Record($"{path}: magenta pixels={fraction:P3}");
            Require(fraction < 0.01f, "Pink error-shader pixels exceed 1% of the render.");
        }
        finally
        {
            camera.targetTexture = previousTarget;
            camera.rect = previousRect;
            RenderTexture.active = previousActive;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
