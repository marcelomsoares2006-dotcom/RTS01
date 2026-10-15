using System.Collections.Generic;
using UnityEngine;

public enum BuildingPlacement
{
    VALID,
    INVALID,
    FIXED
};

public class Building : Unit
{

    private BuildingManager _buildingManager;
    private BuildingPlacement _placement;
    private List<Material> _materials;

    private MeshFilter _rendererMesh;
    private Mesh[] _constructionMeshes;
    private int _constructionHP;
    // Construction cost is spent when placement is confirmed, but remains
    // attached to an unfinished building so cancellation can refund it and
    // save/load can preserve the reservation exactly.
    private List<BuildingMaintenanceCost> _reservedConstructionCosts;
    private List<CharacterManager> _constructors;
    private List<CharacterManager> _garrisonedUnits;
    private int _garrisonAttackDamageBonus;
    private List<Transform> _smokeVfx;
    private BuildingBT _bt;

    private AudioClip _ambientSound;
    private AudioClip _constructionSound;
    private BuildingFeedbackEmitter _feedbackEmitter;
    private BuildingEraDecoration _eraDecoration;

    private bool _isAlive;

    public Building(BuildingData data, int owner) : this(data, owner, new List<ResourceValue>() { }) { }
    public Building(BuildingData data, int owner, List<ResourceValue> production) :
        base(data, owner, production)
    {
        _buildingManager = _transform.GetComponent<BuildingManager>();
        _bt = _transform.GetComponent<BuildingBT>();
        _bt.enabled = false;

        _constructionHP = 0;
        _reservedConstructionCosts = new List<BuildingMaintenanceCost>();
        _constructors = new List<CharacterManager>();
        _garrisonedUnits = new List<CharacterManager>();
        _garrisonAttackDamageBonus = 0;
        _smokeVfx = new List<Transform>();
        _isAlive = false;

        _materials = new List<Material>();
        foreach (Material material in _buildingManager.meshRenderer.materials)
            _materials.Add(new Material(material));
        SetMaterials();
        _placement = BuildingPlacement.VALID;

        _rendererMesh = _buildingManager.meshRenderer.GetComponent<MeshFilter>();
        _constructionMeshes = data.constructionMeshes;

        // The era detail kit is now part of the live building lifecycle for
        // the two authored legacy structures it supports. Its generated
        // pieces are visual-only and have no colliders, so placement and
        // navigation remain governed by the original footprint.
        if (data != null && (data.code == "house" || data.code == "tower")
            && _rendererMesh != null && _rendererMesh.sharedMesh != null)
        {
            _eraDecoration = _transform.GetComponent<BuildingEraDecoration>();
            if (_eraDecoration == null)
                _eraDecoration = _transform.gameObject.AddComponent<BuildingEraDecoration>();
            _eraDecoration.Initialize(data.code, _rendererMesh.transform, _rendererMesh.sharedMesh);
            _eraDecoration.SetFinished(false);
        }

        if (_buildingManager.ambientSource != null)
        {
            if (_owner == GameManager.instance.gamePlayersParameters.myPlayerId)
            {
                _constructionSound =
                    GameManager.instance.gameSoundParameters.constructionSiteSound;
                _buildingManager.ambientSource.clip = _constructionSound;
                _ambientSound = data.ambientSound;
            }
            else
            {
                _buildingManager.ambientSource.enabled = false;
            }
        }
        else
            Debug.LogWarning($"'{data.unitName}' prefab is missing an ambient audio source!");

        _feedbackEmitter = _transform.GetComponent<BuildingFeedbackEmitter>();
        if (_feedbackEmitter == null)
            _feedbackEmitter = _transform.gameObject.AddComponent<BuildingFeedbackEmitter>();
        _feedbackEmitter.Bind(this, _buildingManager, _constructionSound, _ambientSound);
        PublishFeedback();
    }

    public void SetMaterials() { SetMaterials(_placement); }
    public void SetMaterials(BuildingPlacement placement)
    {
        List<Material> materials;
        if (placement == BuildingPlacement.VALID)
        {
            Material refMaterial = Resources.Load("Materials/Valid") as Material;
            materials = new List<Material>();
            for (int i = 0; i < _materials.Count; i++)
                materials.Add(refMaterial);
        }
        else if (placement == BuildingPlacement.INVALID)
        {
            Material refMaterial = Resources.Load("Materials/Invalid") as Material;
            materials = new List<Material>();
            for (int i = 0; i < _materials.Count; i++)
                materials.Add(refMaterial);
        }
        else if (placement == BuildingPlacement.FIXED)
            materials = _materials;
        else
            return;
        _buildingManager.meshRenderer.materials = materials.ToArray();
    }

    public override void Place(bool fromSavedData = false)
    {
        base.Place(fromSavedData);
        if (!fromSavedData)
            _reservedConstructionCosts = CaptureConstructionCost();
        // set placement state
        _placement = BuildingPlacement.FIXED;
        // change building materials
        SetMaterials();
        // change building construction ratio
        SetConstructionHP(0);
        PublishFeedback();
    }

    public void SetConstructionHP(int constructionHP, bool fromSavedData = false)
    {
        if (_isAlive) return;

        // Clamp values restored from save data and supplied by debug tooling.
        _constructionHP = Mathf.Clamp(constructionHP, 0, MaxHP);
        float constructionRatio = MaxHP > 0
            ? _constructionHP / (float) MaxHP
            : 1f;

        if (_constructionMeshes != null && _constructionMeshes.Length > 0)
        {
            int meshIndex = Mathf.Clamp(
                (int)(_constructionMeshes.Length * constructionRatio) - 1,
                0,
                _constructionMeshes.Length - 1);
            _rendererMesh.sharedMesh = _constructionMeshes[meshIndex];
        }

        if (MaxHP > 0 && constructionRatio >= 1)
            _SetAlive(fromSavedData);
        else
            PublishFeedback();
    }

    public void CheckValidPlacement()
    {
        if (_placement == BuildingPlacement.FIXED) return;
        _placement = _buildingManager.CheckPlacement()
            ? BuildingPlacement.VALID
            : BuildingPlacement.INVALID;
    }

    public bool AddConstructor(CharacterManager m)
    {
        if (m == null || _isAlive || m.IsConstructor || _constructors.Contains(m))
            return false;

        PruneInvalidConstructors();
        if (!IsFixed || HasConstructorsFull)
            return false;

        _constructors.Add(m);
        EventManager.TriggerEvent("UpdatedConstructors", this);

        // when adding first constructor, add some smoke VFX
        // + play construction sound
        if (_constructors.Count == 1)
        {
            List<Vector2> vfxPositions = Utils.SampleOffsets(3, 1.5f, Vector2.one * 4f);
            foreach (Vector2 offset in vfxPositions)
                _smokeVfx.Add(VFXManager.instance.Spawn(
                    VfxType.Smoke,
                    Transform.position + new Vector3(offset.x, 0, offset.y)));

            if (_buildingManager.ambientSource != null)
                _buildingManager.ambientSource.Play();
        }
        PublishFeedback();
        return true;
    }

    /// <summary>Reconnects a worker saved while this building was incomplete.</summary>
    public bool RestoreConstructor(CharacterManager constructor)
    {
        if (!AddConstructor(constructor))
            return false;
        constructor.SetIsConstructor(true);
        constructor.SetRendererVisibility(false);
        CharacterBT bt = constructor.GetComponent<CharacterBT>();
        if (bt != null)
            bt.StartBuildingConstruction(Transform);
        return true;
    }

    public void RemoveConstructor(int index)
    {
        // UI callbacks can outlive a constructor list refresh. Treat stale
        // indices as a no-op instead of throwing and aborting the whole click.
        if (index < 0 || index >= _constructors.Count)
            return;

        CharacterManager constructor = _constructors[index];
        CharacterBT bt = constructor != null
            ? constructor.GetComponent<CharacterBT>()
            : null;
        if (bt != null)
            bt.StopBuildingConstruction();
        _constructors.RemoveAt(index);
        EventManager.TriggerEvent("UpdatedConstructors", this);

        // when removing last constructor:
        if (_constructors.Count == 0)
        {
            // - remove smoke VFX
            foreach (Transform vfx in _smokeVfx)
                VFXManager.instance.Unspawn(VfxType.Smoke, vfx);
            _smokeVfx.Clear();
            // - stop construction sound
            if (_buildingManager.ambientSource != null)
                _buildingManager.ambientSource.Pause();
        }
        PublishFeedback();
    }

    /// <summary>
    /// Releases every worker still attached to this building. This is used
    /// when construction completes and by cancellation/destruction flows so
    /// no worker remains hidden or locked in constructor mode.
    /// </summary>
    public void ReleaseConstructors()
    {
        if (_constructors == null || _constructors.Count == 0)
            return;

        List<CharacterManager> constructors = new List<CharacterManager>(_constructors);
        _constructors.Clear();
        foreach (CharacterManager constructor in constructors)
        {
            if (constructor == null)
                continue;
            CharacterBT bt = constructor.GetComponent<CharacterBT>();
            if (bt != null)
                bt.StopBuildingConstruction();
            else
            {
                constructor.SetIsConstructor(false);
                constructor.SetRendererVisibility(true);
            }
        }
        EventManager.TriggerEvent("UpdatedConstructors", this);
        if (_buildingManager.ambientSource != null)
            _buildingManager.ambientSource.Pause();
        PublishFeedback();
    }

    /// <summary>
    /// Attempts to register a friendly unit inside this operational building.
    /// The content contract is required deliberately: an absent contract never
    /// silently turns every building into a garrison.
    /// </summary>
    public BuildingGarrisonResult TryEnterGarrison(CharacterManager unit, BuildingContentSpec spec)
    {
        if (unit == null || unit.Unit == null)
            return BuildingGarrisonResult.InvalidUnit;
        PruneInvalidGarrisonedUnits();
        if (_garrisonedUnits.Contains(unit))
            return BuildingGarrisonResult.AlreadyGarrisoned;

        BuildingGarrisonResult result = BuildingGarrisonRules.ValidateEntry(
            spec,
            Owner,
            IsAlive,
            unit.Unit.Owner,
            unit.Unit.Code,
            _garrisonedUnits.Count);
        if (result != BuildingGarrisonResult.Accepted)
            return result;

        _garrisonedUnits.Add(unit);
        _garrisonAttackDamageBonus = Mathf.Max(
            _garrisonAttackDamageBonus,
            spec.garrisonAttackDamageBonus);
        return BuildingGarrisonResult.Accepted;
    }

    /// <summary>Removes a previously accepted unit without throwing on stale input.</summary>
    public BuildingGarrisonResult TryExitGarrison(CharacterManager unit, BuildingContentSpec spec)
    {
        if (unit == null || unit.Unit == null)
            return BuildingGarrisonResult.InvalidUnit;
        PruneInvalidGarrisonedUnits();
        bool isGarrisoned = _garrisonedUnits.Contains(unit);
        BuildingGarrisonResult result = BuildingGarrisonRules.ValidateExit(
            spec,
            Owner,
            unit.Unit.Owner,
            unit.Unit.Code,
            isGarrisoned);
        if (result != BuildingGarrisonResult.Accepted)
            return result;

        _garrisonedUnits.Remove(unit);
        return BuildingGarrisonResult.Accepted;
    }

    public IReadOnlyList<CharacterManager> GarrisonedUnits
    {
        get
        {
            PruneInvalidGarrisonedUnits();
            return _garrisonedUnits.AsReadOnly();
        }
    }

    private void PruneInvalidGarrisonedUnits()
    {
        if (_garrisonedUnits == null) _garrisonedUnits = new List<CharacterManager>();
        for (int i = _garrisonedUnits.Count - 1; i >= 0; i--)
        {
            CharacterManager unit = _garrisonedUnits[i];
            if (unit == null || unit.Unit == null)
                _garrisonedUnits.RemoveAt(i);
        }
    }

    private void PruneInvalidConstructors()
    {
        for (int i = _constructors.Count - 1; i >= 0; i--)
        {
            if (_constructors[i] != null)
                continue;
            _constructors.RemoveAt(i);
        }
    }

    private void _SetAlive(bool fromSavedData = false)
    {
        _isAlive = true;
        // The placement preview may have left the renderer using the red
        // Invalid material. Once construction reaches full HP the authored
        // materials must be restored before the building enters gameplay.
        SetMaterials(BuildingPlacement.FIXED);
        if (_eraDecoration != null)
            _eraDecoration.SetFinished(true);
        _reservedConstructionCosts.Clear();
        _bt.enabled = true;
        ComputeProduction();
        Globals.UpdateNavMeshSurface();

        // A building can finish from one worker's tick while other workers
        // are still attached. Release all of them atomically before the
        // completion feedback is emitted.
        ReleaseConstructors();

        // when finishing construction, remove smoke VFX
        foreach (Transform vfx in _smokeVfx)
            VFXManager.instance.Unspawn(VfxType.Smoke, vfx);
        _smokeVfx.Clear();
        BuildingProductionRuntimeDriver.Attach(this);
        if (_owner == GameManager.instance.gamePlayersParameters.myPlayerId)
        {
            if (!fromSavedData)
                EventManager.TriggerEvent("PlaySoundByName", "onBuildingPlacedSound");
            // replace construction sound
            if (_ambientSound)
            {
                _buildingManager.ambientSource.clip = _ambientSound;
                _buildingManager.ambientSource.Play();
            }
            else
            {
                if (_buildingManager.ambientSource != null)
                    _buildingManager.ambientSource.Stop();
            }
        }
        PublishFeedback(!fromSavedData);
    }

    /// <summary>
    /// Re-evaluates presentation after combat or save restoration changes HP.
    /// UnitManager calls this without coupling combat to visual/audio details.
    /// </summary>
    public void NotifyHealthChanged()
    {
        PublishFeedback();
    }

    private void PublishFeedback(bool notifyCompletion = false)
    {
        if (_feedbackEmitter == null)
            return;
        BuildingFeedbackSnapshot snapshot = BuildingFeedbackRules.CreateSnapshot(
            LifecycleState,
            _constructionHP,
            MaxHP,
            HP);
        _feedbackEmitter.Publish(snapshot);
        if (notifyCompletion && snapshot.lifecycle == BuildingLifecycleState.Operational)
            EventManager.TriggerEvent("BuildingFeedbackOperational", this);
    }

    public int ConstructionHP { get => _constructionHP; }
    public IReadOnlyList<BuildingMaintenanceCost> ReservedConstructionCosts
    {
        get { return _reservedConstructionCosts.AsReadOnly(); }
    }
    public void SetReservedConstructionCosts(IEnumerable<BuildingMaintenanceCost> costs)
    {
        _reservedConstructionCosts = new List<BuildingMaintenanceCost>();
        if (costs == null) return;
        foreach (BuildingMaintenanceCost cost in costs)
            if (cost != null && cost.amount > 0)
                _reservedConstructionCosts.Add(new BuildingMaintenanceCost(cost.resource, cost.amount));
    }
    private List<BuildingMaintenanceCost> CaptureConstructionCost()
    {
        List<BuildingMaintenanceCost> result = new List<BuildingMaintenanceCost>();
        if (Data == null || Data.cost == null) return result;
        float costReducerBuy = TechnologyNodeActioners.GetMultiplier("cost_reducer_buy");
        foreach (ResourceValue value in Data.cost)
        {
            if (value == null || value.amount <= 0) continue;
            EconomyResourceType resource;
            if (!TryMapResource(value.code, out resource)) continue;
            int spentAmount = (int)(value.amount * costReducerBuy);
            if (spentAmount <= 0) continue;
            BuildingMaintenanceCost existing = result.Find(x => x.resource == resource);
            if (existing == null) result.Add(new BuildingMaintenanceCost(resource, spentAmount));
            else
            {
                long total = (long)existing.amount + spentAmount;
                existing.amount = total > int.MaxValue ? int.MaxValue : (int)total;
            }
        }
        return result;
    }
    private static bool TryMapResource(InGameResource source, out EconomyResourceType target)
    {
        switch (source)
        {
            case InGameResource.Wood: target = EconomyResourceType.Wood; return true;
            case InGameResource.Gold: target = EconomyResourceType.Gold; return true;
            case InGameResource.Stone: target = EconomyResourceType.Stone; return true;
            default: target = default(EconomyResourceType); return false;
        }
    }
    public List<CharacterManager> Constructors { get => _constructors; }
    public bool HasConstructorsFull
    {
        get
        {
            PruneInvalidConstructors();
            BuildingData data = _data as BuildingData;
            int limit = data != null ? Mathf.Max(1, data.maxConstructors) : 3;
            return _constructors.Count >= limit;
        }
    }
    public bool HasValidPlacement { get => _placement == BuildingPlacement.VALID; }
    public bool IsFixed { get => _placement == BuildingPlacement.FIXED; }
    public override bool IsAlive { get => _isAlive; }
    public BuildingLifecycleState LifecycleState
    {
        get
        {
            return BuildingConstructionRules.GetLifecycleState(
                IsFixed,
                _isAlive,
                HP,
                MaxHP,
                _constructionHP);
        }
    }
    public int DataIndex
    {
        get
        {
            for (int i = 0; i < Globals.BUILDING_DATA.Length; i++)
                if (Globals.BUILDING_DATA[i].code == _data.code)
                    return i;
            return -1;
        }
    }

    public override int CombatAttackDamage
    {
        get
        {
            PruneInvalidGarrisonedUnits();
            return BuildingDefenseRules.ResolveAttackDamage(
                AttackDamage,
                _garrisonedUnits == null ? 0 : _garrisonedUnits.Count,
                _garrisonAttackDamageBonus);
        }
    }
}
