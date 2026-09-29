using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Canvas")]
    public Canvas canvas;
    public float canvasScaleFactor;

    [Header("Game Parameters")]
    public GameGlobalParameters gameGlobalParameters;
    public GamePlayersParameters gamePlayersParameters;
    public GameSoundParameters gameSoundParameters;
    public GameInputParameters gameInputParameters;
    public GameObject fov;

    [Header("Minimap")]
    public Transform minimapAnchor;
    public Camera minimapCamera;
    public Minimap minimapScript;
    public BoxCollider mapWrapperCollider;
    public int terrainSize;
    private const float _TERRAIN_MID_HEIGHT = 30f;

    [HideInInspector]
    public bool gameIsPaused;

    [HideInInspector]
    public float producingRate = 3f; // in seconds

    [HideInInspector]
    public bool waitingForInput;
    [HideInInspector]
    public string pressedKey;

    private void Awake()
    {
        instance = this;
        Time.timeScale = 1f;
        Globals.NAV_MESH_SURFACE = null;
        Globals.SELECTED_UNITS.Clear();
        Unit.UNITS_BY_OWNER = new Dictionary<int, List<Unit>>();
        TechnologyNodeActioners.ResetMultipliers();
        canvasScaleFactor = canvas.scaleFactor;

        DataHandler.LoadGameData();
        GetComponent<DayAndNightCycler>().enabled = gameGlobalParameters.enableDayAndNightCycle;

        Globals.InitializeGameResources(gamePlayersParameters.players.Length);

        StartCoroutine(_InitializeNavMeshWhenReady());

        // enable/disable FOV depending on game parameters
        fov.SetActive(gameGlobalParameters.enableFOV);

        _SetupMinimap();

        gameIsPaused = false;
    }

    private IEnumerator _InitializeNavMeshWhenReady()
    {
        for (int attempt = 0; attempt < 120; attempt++)
        {
            NavMeshSurface[] surfaces = FindObjectsOfType<NavMeshSurface>();
            foreach (NavMeshSurface surface in surfaces)
            {
                if (surface == null)
                    continue;

                Globals.NAV_MESH_SURFACE = surface;
                Globals.UpdateNavMeshSurface();
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning("Nenhum NavMeshSurface foi encontrado após o carregamento do mapa.");
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            Globals.NAV_MESH_SURFACE = null;
        }
    }

    private void _SetupMinimap()
    {
        Bounds b = GameObject.Find("Terrain").GetComponent<Terrain>().terrainData.bounds;

        terrainSize = (int) b.size.x;
        float p = terrainSize / 2;

        minimapAnchor.position = new Vector3(p, 0, p);
        minimapCamera.orthographicSize = p;
        mapWrapperCollider.center = new Vector3(0, _TERRAIN_MID_HEIGHT, 0);
        mapWrapperCollider.size = new Vector3(b.size.x, 1f, b.size.z);
        minimapScript.terrainSize = Vector2.one * terrainSize;
    }

    private void Update()
    {
        if (Input.anyKeyDown)
        {
            if (waitingForInput)
            {
                if (Input.GetMouseButtonDown(0))
                    pressedKey = "mouse 0";
                else if (Input.GetMouseButtonDown(1))
                    pressedKey = "mouse 1";
                else if (Input.GetMouseButtonDown(2))
                    pressedKey = "mouse 2";
                else
                    pressedKey = Input.inputString;
                waitingForInput = false;
            }
            else
                gameInputParameters.CheckForInput();
        }
    }

    private void OnEnable()
    {
        EventManager.AddListener("PausedGame", _OnPausedGame);
        EventManager.AddListener("ResumedGame", _OnResumedGame);

        EventManager.AddListener("UpdateGameParameter:enableDayAndNightCycle", _OnUpdateDayAndNightCycle);
        EventManager.AddListener("UpdateGameParameter:enableFOV", _OnUpdateFOV);
    }

    private void OnDisable()
    {
        EventManager.RemoveListener("PausedGame", _OnPausedGame);
        EventManager.RemoveListener("ResumedGame", _OnResumedGame);

        EventManager.RemoveListener("UpdateGameParameter:enableDayAndNightCycle", _OnUpdateDayAndNightCycle);
        EventManager.RemoveListener("UpdateGameParameter:enableFOV", _OnUpdateFOV);
    }

    private void _OnPausedGame()
    {
        gameIsPaused = true;
        Time.timeScale = 0;
    }

    private void _OnResumedGame()
    {
        gameIsPaused = false;
        Time.timeScale = 1;
    }

    /* game parameters update */
    private void _OnUpdateDayAndNightCycle(object data)
    {
        bool dayAndNightIsOn = (bool)data;
        GetComponent<DayAndNightCycler>().enabled = dayAndNightIsOn;
    }
    private void _OnUpdateFOV(object data)
    {
        bool fovIsOn = (bool)data;
        fov.SetActive(fovIsOn);
    }

    private void OnApplicationQuit()
    {
#if !UNITY_EDITOR
        DataHandler.SaveGameData();
#endif
    }
}
