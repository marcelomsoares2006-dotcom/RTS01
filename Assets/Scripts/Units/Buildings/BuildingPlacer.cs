using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingPlacer : MonoBehaviour
{
    public static BuildingPlacer instance;

    private Building _placedBuilding = null;

    private Ray _ray;
    private RaycastHit _raycastHit;
    private Vector3 _lastPlacementPosition;

    private UnitManager _builderManager;
    private readonly BuildingCatalogResolver _catalogResolver =
        BuildingCatalogResolver.LoadFromResources();

    private void Start()
    {
        instance = this;

        // if no previous game data, spawn initial buildings
        if (GameData.Instance == null)
        {
            Transform spawnpoints = GameObject.Find("Spawnpoints").transform;

            BuildingData initialBuilding = GameManager.instance.gameGlobalParameters.initialBuilding;
            GamePlayersParameters p = GameManager.instance.gamePlayersParameters;
            Vector3 pos;
            for (int i = 0; i < p.players.Length; i++)
            {
                pos = spawnpoints.GetChild(i).position;
                SpawnBuilding(initialBuilding, i, pos);
                if (i == p.myPlayerId)
                    Camera.main.GetComponent<CameraManager>().SetPosition(pos);
            }
        }
    }

    void Update()
    {
        if (GameManager.instance.gameIsPaused) return;

        if (_placedBuilding != null)
        {
            if (Input.GetKeyUp(KeyCode.Escape))
            {
                _CancelPlacedBuilding();
                return;
            }

            _ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(
                _ray,
                out _raycastHit,
                1000f,
                Globals.TERRAIN_LAYER_MASK
            ))
            {
                BuildingData placementData = _placedBuilding.Data as BuildingData;
                float gridSize = placementData != null ? placementData.placementGridSize : 0f;
                _placedBuilding.SetPosition(BuildingConstructionRules.SnapPosition(
                    _raycastHit.point,
                    gridSize));
                if (_lastPlacementPosition != _raycastHit.point)
                {
                    _placedBuilding.CheckValidPlacement();
                    Dictionary<InGameResource, int> prod = _placedBuilding.ComputeProduction();
                    EventManager.TriggerEvent("UpdatedPlacedBuildingPosition", new object[] { prod, _raycastHit.point });
                }
                _lastPlacementPosition = _raycastHit.point;
            }

            if (
                _placedBuilding.HasValidPlacement &&
                Input.GetMouseButtonUp(0) &&
                !EventSystem.current.IsPointerOverGameObject()
            )
            {
                _PlaceBuilding();
            }

            if (Input.mouseScrollDelta.y != 0f)
            {
                BuildingData placementData = _placedBuilding.Data as BuildingData;
                float step = placementData != null ? placementData.rotationStepDegrees : 15f;
                float nextY = _placedBuilding.Transform.eulerAngles.y
                    + Mathf.Sign(Input.mouseScrollDelta.y) * step;
                _placedBuilding.Transform.rotation = Quaternion.Euler(
                    0f,
                    BuildingConstructionRules.SnapRotation(nextY, step),
                    0f);
            }
        }
    }

    private void OnEnable()
    {
        EventManager.AddListener("<Input>Build", _OnBuildInput);
    }

    private void OnDisable()
    {
        EventManager.RemoveListener("<Input>Build", _OnBuildInput);
    }

    private void _OnBuildInput(object data)
    {
        // check to see if there is at least one selected unit
        // that can build -> we arbitrarily choose the first one
        if (Globals.SELECTED_UNITS.Count == 0) return;

        UnitManager um = null;
        foreach (UnitManager selected in Globals.SELECTED_UNITS)
        {
            if (selected is CharacterManager cm && ((CharacterData) cm.Unit.Data).buildPower > 0)
            {
                um = cm;
                break;
            }
        }
        if (um == null) return;
        _builderManager = um;


        string buildingCode = (string)data;
        for (int i = 0; i < Globals.BUILDING_DATA.Length; i++)
        {
            if (Globals.BUILDING_DATA[i].code == buildingCode)
            {
                SelectPlacedBuilding(i);
                return;
            }
        }
    }

    /// <summary>
    /// Catalog-aware entry point for construction commands. The legacy input
    /// path remains compatible until the match state exposes era and population
    /// directly to the UI and AI.
    /// </summary>
    public BuildingAvailabilityResult TrySelectPlacedBuilding(
        string buildingCode,
        BuildingEra currentEra,
        int population,
        IEnumerable<string> unlockedTechnologyCodes = null,
        IEnumerable<string> ownedBuildingCodes = null)
    {
        BuildingAvailabilityResult availability = _catalogResolver.Validate(
            buildingCode,
            currentEra,
            population,
            unlockedTechnologyCodes,
            ownedBuildingCodes);
        if (availability != BuildingAvailabilityResult.Available)
            return availability;

        BuildingData buildingData;
        if (!_catalogResolver.TryGetBuildingData(buildingCode, out buildingData))
            return BuildingAvailabilityResult.NotFound;

        SelectPlacedBuilding(buildingData);
        return BuildingAvailabilityResult.Available;
    }

    public void SpawnBuilding(BuildingData data, int owner, Vector3 position)
    {
        SpawnBuilding(data, owner, position, new List<ResourceValue>() { });
    }
    public void SpawnBuilding(BuildingData data, int owner, Vector3 position, List<ResourceValue> production)
    {
        // keep a reference to the previously placed building, if there is one
        Building prevPlacedBuilding = _placedBuilding;

        // instantiate building
        _placedBuilding = new Building(data, owner, production);
        _placedBuilding.SetPosition(position);
        // finish up the placement
        _PlaceBuilding(false);
        _placedBuilding.SetConstructionHP(_placedBuilding.MaxHP);

        // restore the previous state
        _placedBuilding = prevPlacedBuilding;
    }

    void _PreparePlacedBuilding(int buildingDataIndex)
    {
        _PreparePlacedBuilding(Globals.BUILDING_DATA[buildingDataIndex]);
    }
    void _PreparePlacedBuilding(BuildingData buildingData)
    {
        // destroy the previous "phantom" if there is one
        if (_placedBuilding != null && !_placedBuilding.IsFixed)
        {
            _DestroyPreviewBuilding(_placedBuilding);
        }
        Building building = new Building(
            buildingData,
            GameManager.instance.gamePlayersParameters.myPlayerId
        );
        _placedBuilding = building;
        _lastPlacementPosition = Vector3.zero;
        EventManager.TriggerEvent("PlaceBuildingOn");
    }

    void _CancelPlacedBuilding()
    {
        if (_placedBuilding == null)
            return;

        // destroy the "phantom" building
        _DestroyPreviewBuilding(_placedBuilding);
        _placedBuilding = null;
        EventManager.TriggerEvent("PlaceBuildingOff");
    }

    private void _DestroyPreviewBuilding(Building building)
    {
        if (building == null)
            return;

        // Unit registers itself during construction, before Place() is called.
        // Remove cancelled previews from that registry so they cannot remain
        // selectable or be serialized as real buildings after their GameObject
        // is destroyed.
        if (Unit.UNITS_BY_OWNER != null &&
            Unit.UNITS_BY_OWNER.TryGetValue(building.Owner, out List<Unit> units))
        {
            units.Remove(building);
            if (units.Count == 0)
                Unit.UNITS_BY_OWNER.Remove(building.Owner);
        }

        if (building.Transform != null)
            Destroy(building.Transform.gameObject);
    }

    void _PlaceBuilding(bool canChain = true)
    {
        // Validate the final placement and stockpile before any worker is
        // assigned and before Building.Place consumes the construction cost.
        if (_placedBuilding == null)
            return;
        BuildingData placedData = _placedBuilding.Data as BuildingData;
        BuildingConstructionGateResult gate = BuildingConstructionGate.EvaluateLegacy(
            placedData, _placedBuilding.Owner, _placedBuilding.HasValidPlacement);
        if (gate != BuildingConstructionGateResult.Allowed)
        {
            EventManager.TriggerEvent("PlaceBuildingOff");
            _DestroyPreviewBuilding(_placedBuilding);
            _placedBuilding = null;
            _builderManager = null;
            return;
        }

        // if there is a worker assigned to this construction,
        // warn its behaviour tree and deselect the building
        if (_builderManager != null)
        {
            _builderManager.Select();
            _builderManager
                .GetComponent<CharacterBT>()
                .StartBuildingConstruction(_placedBuilding.Transform);
            _builderManager = null;

            _placedBuilding.Place();

            EventManager.TriggerEvent("PlaceBuildingOff");
            _placedBuilding = null;
        }
        else
        {
            _placedBuilding.Place();
            if (canChain)
            {
                if (_placedBuilding.CanBuy())
                    _PreparePlacedBuilding(_placedBuilding.DataIndex);
                else
                {
                    EventManager.TriggerEvent("PlaceBuildingOff");
                    _placedBuilding = null;
                }
            }
        }
    }

    public void SelectPlacedBuilding(int buildingDataIndex)
    {
        _PreparePlacedBuilding(buildingDataIndex);
    }
    public void SelectPlacedBuilding(BuildingData buildingData, UnitManager builderManager = null)
    {
        _builderManager = builderManager;
        _PreparePlacedBuilding(buildingData);
    }
}
