using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DebugConsole : MonoBehaviour
{
    enum DisplayType
    {
        None,
        Help,
        Autocomplete,
        Output
    }

    private static GUIStyle _logStyle;
    private static DebugConsole _instance;

    private bool _showConsole = false;
    private string _consoleInput;

    private DisplayType _displayType;

    private List<string> _commandOutput;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureDebugConsole()
    {
        if (_instance != null)
            return;

        GameObject consoleObject = new GameObject("DebugConsole");
        DontDestroyOnLoad(consoleObject);
        consoleObject.AddComponent<DebugConsole>();
    }

    private void Awake()
    {
        // The legacy scene puts this component on the managers object. Persisting
        // that object also persisted the entire old match when returning to menu.
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
        _consoleInput = "";

        new DebugCommand("?", "Lists all available debug commands.", "?", () =>
        {
            _displayType = DisplayType.Help;
        });
        new DebugCommand("toggle_fov", "Toggles the FOV parameter on/off.", "toggle_fov", () =>
        {
            bool fov = !GameManager.instance.gameGlobalParameters.enableFOV;
            GameManager.instance.gameGlobalParameters.enableFOV = fov;
            EventManager.TriggerEvent("UpdateGameParameter:enableFOV", fov);
        });
        new DebugCommand<int>("add_gold", "Adds a given amount of gold to the current player.", "add_gold <amount>", (x) =>
        {
            int owner;
            if (!TryPreparePlayerResources(out owner)) return;
            Globals.GAME_RESOURCES[owner][InGameResource.Gold].AddAmount(x);
            EventManager.TriggerEvent("UpdatedResources");
        });
        new DebugCommand<int>("add_wood", "Adds a given amount of wood to the current player.", "add_wood <amount>", (x) =>
        {
            int owner;
            if (!TryPreparePlayerResources(out owner)) return;
            Globals.GAME_RESOURCES[owner][InGameResource.Wood].AddAmount(x);
            EventManager.TriggerEvent("UpdatedResources");
        });
        new DebugCommand<int>("add_stone", "Adds a given amount of stone to the current player.", "add_stone <amount>", (x) =>
        {
            int owner;
            if (!TryPreparePlayerResources(out owner)) return;
            Globals.GAME_RESOURCES[owner][InGameResource.Stone].AddAmount(x);
            EventManager.TriggerEvent("UpdatedResources");
        });
        new DebugCommand("list_players", "Lists all current players (with their IDs).", "list_players", () =>
        {
            if (_commandOutput == null)
                _commandOutput = new List<string>();
            else
                _commandOutput.Clear();
            int i = 0;
            foreach (PlayerData p in GameManager.instance.gamePlayersParameters.players)
                _commandOutput.Add($"Player #{i++} - {p.name}");
            _displayType = DisplayType.Output;
        });
        new DebugCommand<int>("set_player_id", "Sets the current player (by ID).", "set_player_id <id>", (x) =>
        {
            GameManager.instance.gamePlayersParameters.myPlayerId = x;
            EventManager.TriggerEvent("SetPlayer", x);
        });
        new DebugCommand<string, int>(
        "instantiate_characters",
        "Instantiates multiple instances of a character unit (by reference code), using a Poisson disc sampling for random positioning.",
        "instantiate_characters <code> <amount>", (code, amount) =>
        {
            Vector3 center;
            if (!TryGetSpawnCenter(out center)) return;
            SpawnCharacters(code, amount, center);
        });
        new DebugCommand("list_era_units_v1", "Lists the Era Imperial v1 unit codes.", "list_era_units_v1", () =>
        {
            if (_commandOutput == null)
                _commandOutput = new List<string>();
            else
                _commandOutput.Clear();

            if (Globals.CHARACTER_DATA == null)
            {
                Debug.LogError("Character data is not loaded yet. Open the playable GameScene/Core scene, press Play, then run the debug command again.");
                return;
            }

            foreach (string code in EraUnitCodesV1())
            {
                string status = Globals.CHARACTER_DATA.ContainsKey(code) ? "loaded" : "missing";
                _commandOutput.Add($"{code} - {status}");
            }

            _displayType = DisplayType.Output;
        });
        new DebugCommand("spawn_era_units_v1", "Spawns one of each Era Imperial v1 unit near the camera.", "spawn_era_units_v1", () =>
        {
            Vector3 center;
            if (!TryGetSpawnCenter(out center)) return;

            Vector3 offset = Vector3.left * 6f;
            foreach (string code in EraUnitCodesV1())
            {
                SpawnCharacters(code, 1, center + offset);
                offset += Vector3.right * 3f;
            }
        });
        new DebugCommand<int>("give_era_resources", "Adds the same resource amount to gold, wood and stone.", "give_era_resources <amount>", (amount) =>
        {
            int owner;
            if (!TryPreparePlayerResources(out owner)) return;
            Globals.GAME_RESOURCES[owner][InGameResource.Gold].AddAmount(amount);
            Globals.GAME_RESOURCES[owner][InGameResource.Wood].AddAmount(amount);
            Globals.GAME_RESOURCES[owner][InGameResource.Stone].AddAmount(amount);
            EventManager.TriggerEvent("UpdatedResources");
        });
        new DebugCommand<int>("set_unit_formation_type", "Sets the unit formation type (by index).", "set_unit_formation_type <formation_index>", (x) =>
        {
            Globals.UNIT_FORMATION_TYPE = (UnitFormationType)x;
            EventManager.TriggerEvent("UpdatedUnitFormationType");
        });

        new DebugCommand<int>("set_construction_hp", "Sets the selected unit construction HP.", "set_construction_hp <hp>", (x) =>
        {
            if (Globals.SELECTED_UNITS.Count == 0) return;
            Building b = (Building) Globals.SELECTED_UNITS[0].GetComponent<BuildingManager>().Unit;
            if (b == null) return;
            b.SetConstructionHP(x);
        });

        new DebugCommand<string>("unlock_tech", "Unlocks the given technology tree node.", "unlock_tech <code>", (x) =>
        {
            TechnologyNodeData node;
            if (TechnologyNodeData.TECH_TREE_NODES.TryGetValue(x, out node))
                node.Unlock();
        });

        _displayType = DisplayType.None;
    }

    private static IEnumerable<string> EraUnitCodesV1()
    {
        yield return "aldeao_v1";
        yield return "soldado_espada_v1";
        yield return "arqueiro_v1";
        yield return "lanceiro_v1";
        yield return "cavaleiro_v1";
    }

    private static bool TryPreparePlayerResources(out int owner)
    {
        owner = 0;
        if (GameManager.instance == null || GameManager.instance.gamePlayersParameters == null)
        {
            Debug.LogError("GameManager is not ready. Open the playable GameScene/Core scene, press Play, then run the debug command again.");
            return false;
        }

        GamePlayersParameters playersParameters = GameManager.instance.gamePlayersParameters;
        int playersCount = playersParameters.players != null && playersParameters.players.Length > 0
            ? playersParameters.players.Length
            : 1;

        owner = Mathf.Clamp(playersParameters.myPlayerId, 0, playersCount - 1);
        if (Globals.GAME_RESOURCES == null || Globals.GAME_RESOURCES.Length < playersCount || Globals.GAME_RESOURCES[owner] == null)
            Globals.InitializeGameResources(playersCount);

        return true;
    }

    private static bool TryGetSpawnCenter(out Vector3 center)
    {
        center = Vector3.zero;
        int owner;
        if (!TryPreparePlayerResources(out owner)) return false;

        if (Utils.MainCamera == null)
        {
            Debug.LogError("Main camera is not ready. Open the playable GameScene/Core scene, press Play, then run the debug command again.");
            return false;
        }

        center = Utils.MiddleOfScreenPointToWorld();
        return true;
    }

    private static void SpawnCharacters(string code, int amount, Vector3 center)
    {
        int owner;
        if (!TryPreparePlayerResources(out owner)) return;

        CharacterData d;
        if (Globals.CHARACTER_DATA == null)
        {
            Debug.LogError("Character data is not loaded yet. Open the playable GameScene/Core scene, press Play, then run the debug command again.");
            return;
        }

        if (!Globals.CHARACTER_DATA.TryGetValue(code, out d))
        {
            Debug.LogError($"CharacterData not found for code '{code}'. Run Era Imperial > Unit Prefab Assistant > Criar unidades v1.");
            return;
        }

        List<Vector3> positions = Utils.SamplePositions(amount, 1.5f, Vector2.one * 15, center);
        foreach (Vector3 pos in positions)
        {
            Character c = new Character(d, owner);
            c.ComputeProduction();
            c.Transform.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(pos);
        }
    }

    private void OnEnable()
    {
        if (_instance != this) return;
        EventManager.AddListener("<Input>ShowDebugConsole", _OnShowDebugConsole);
    }

    private void OnDisable()
    {
        EventManager.RemoveListener("<Input>ShowDebugConsole", _OnShowDebugConsole);
    }

    private void _OnShowDebugConsole()
    {
        _showConsole = true;
        EventManager.TriggerEvent("PausedGame");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1) || Input.GetKeyDown(KeyCode.BackQuote))
            _OnShowDebugConsole();
    }

    private void OnGUI()
    {
        if (_logStyle == null)
        {
            _logStyle = new GUIStyle(GUI.skin.label);
            _logStyle.fontSize = 12;
        }

        if (_showConsole)
        {
            // add fake boxes in the background to increase the opacity
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");

            // show main input field
            string newInput = GUI.TextField(new Rect(0, 0, Screen.width, 24), _consoleInput);

            // show log area
            float y = 24;
            GUI.Box(new Rect(0, y, Screen.width, Screen.height - 24), "");
            if (_displayType == DisplayType.Help)
                _ShowHelp(y);
            else if (_displayType == DisplayType.Autocomplete)
                _ShowAutocomplete(y, newInput);
            else if (_displayType == DisplayType.Output)
                _ShowOutput(y);

            // reset display state to "none" if input changes
            if (_displayType != DisplayType.None && _consoleInput.Length != newInput.Length)
                _displayType = DisplayType.None;

            // update input variable
            _consoleInput = newInput;

            // check for special keys
            Event e = Event.current;
            if (e.isKey)
            {
                if (e.keyCode == KeyCode.Tab)
                    _displayType = DisplayType.Autocomplete;
                else if (e.keyCode == KeyCode.Return && _consoleInput.Length > 0)
                    _OnReturn();
                else if (e.keyCode == KeyCode.Escape)
                {
                    _showConsole = false;
                    EventManager.TriggerEvent("ResumedGame");
                }
            }
        }
    }

    private void _ShowHelp(float y)
    {
        foreach (DebugCommandBase command in DebugCommandBase.DebugCommands.Values)
        {
            GUI.Label(
                new Rect(2, y, Screen.width, 20),
                $"{command.Format} - {command.Description}",
                _logStyle
            );
            y += 16;
        }
    }

    private void _ShowAutocomplete(float y, string newInput)
    {
        IEnumerable<string> autocompleteCommands =
                            DebugCommandBase.DebugCommands.Keys
                            .Where(k => k.StartsWith(newInput.ToLower()));
        foreach (string k in autocompleteCommands)
        {
            DebugCommandBase c = DebugCommandBase.DebugCommands[k];
            GUI.Label(
                new Rect(2, y, Screen.width, 20),
                $"{c.Format} - {c.Description}",
                _logStyle
            );
            y += 16;
        }
    }

    private void _ShowOutput(float y)
    {
        foreach (string line in _commandOutput)
        {
            GUI.Label(new Rect(2, y, Screen.width, 20), line, _logStyle);
            y += 16;
        }
    }

    private void _OnReturn()
    {
        _HandleConsoleInput();
        _consoleInput = "";
    }

    private void _HandleConsoleInput()
    {
        // parse input
        string[] inputParts = _consoleInput.Split(' ');
        string mainKeyword = inputParts[0];
        // check against available commands
        DebugCommandBase command;
        if (DebugCommandBase.DebugCommands.TryGetValue(mainKeyword.ToLower(), out command))
        {
            // try to invoke command if it exists
            if (command is DebugCommand dc)
                dc.Invoke();
            else
            {
                if (inputParts.Length < 2)
                {
                    Debug.LogError("Missing parameter!");
                    return;
                }

                if (command is DebugCommand<string> dcString)
                {
                    dcString.Invoke(inputParts[1]);
                }
                else if (command is DebugCommand<int> dcInt)
                {
                    int i;
                    if (int.TryParse(inputParts[1], out i))
                        dcInt.Invoke(i);
                    else
                    {
                        Debug.LogError($"'{command.Id}' requires an int parameter!");
                        return;
                    }
                }
                else if (command is DebugCommand<float> dcFloat)
                {
                    float f;
                    if (float.TryParse(inputParts[1], out f))
                        dcFloat.Invoke(f);
                    else
                    {
                        Debug.LogError($"'{command.Id}' requires a float parameter!");
                        return;
                    }
                }
                else if (command is DebugCommand<string, int> dcStringInt)
                {
                    if (inputParts.Length < 3)
                    {
                        Debug.LogError("Missing parameter!");
                        return;
                    }

                    int i;
                    if (int.TryParse(inputParts[2], out i))
                        dcStringInt.Invoke(inputParts[1], i);
                    else
                    {
                        Debug.LogError($"'{command.Id}' requires a string and an int parameter!");
                        return;
                    }
                }
            }
        }
    }
}
