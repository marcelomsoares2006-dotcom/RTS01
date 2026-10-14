using UnityEngine;

public class BuildingManager : UnitManager
{
    public AudioSource ambientSource;

    private Building _building;
    public override Unit Unit
    {
        get { return _building; }
        set { _building = value is Building ? (Building)value : null; }
    }
    // Trigger callbacks are not guaranteed to arrive in a strict pair when
    // objects are disabled or destroyed. Tracking colliders avoids a stale
    // counter making every later placement invalid.
    private readonly System.Collections.Generic.HashSet<Collider> _blockingColliders =
        new System.Collections.Generic.HashSet<Collider>();

    private void OnTriggerEnter(Collider other)
    {
        if (
            other.tag == "Terrain" ||
            other.gameObject.layer == Globals.FOV_LAYER
        )
            return;
        _blockingColliders.Add(other);
        CheckPlacement();
    }

    private void OnTriggerExit(Collider other)
    {
        if (
            other.tag == "Terrain" ||
            other.gameObject.layer == Globals.FOV_LAYER
        )
            return;
        _blockingColliders.Remove(other);
        CheckPlacement();
    }

    private void OnDisable()
    {
        _blockingColliders.Clear();
    }

    public bool CheckPlacement()
    {
        if (_building == null) return false;
        if (_building.IsFixed) return false;
        bool validPlacement = HasValidPlacement();
        if (!validPlacement)
            _building.SetMaterials(BuildingPlacement.INVALID);
        else
            _building.SetMaterials(BuildingPlacement.VALID);
        return validPlacement;
    }

    public bool HasValidPlacement()
    {
        if (_blockingColliders.Count > 0) return false;

        BuildingData data = _building.Data as BuildingData;

        // get 4 bottom corner positions
        Vector3 c = _collider.center;
        Vector3 e = _collider.size / 2f;
        float bottomHeight = c.y - e.y + (data != null ? data.terrainProbeStartOffset : 0.5f);
        Vector3[] bottomCorners = new Vector3[]
        {
        new Vector3(c.x - e.x, bottomHeight, c.z - e.z),
        new Vector3(c.x - e.x, bottomHeight, c.z + e.z),
        new Vector3(c.x + e.x, bottomHeight, c.z - e.z),
        new Vector3(c.x + e.x, bottomHeight, c.z + e.z)
        };
        // cast a small ray beneath the corner to check for a close ground
        // (if at least two are not valid, then placement is invalid)
        int validCornersCount = 0;
        foreach (Vector3 corner in bottomCorners)
        {
            if (!Physics.Raycast(
                // Collider coordinates are local to the building. Transforming
                // each probe keeps terrain validation correct after rotation
                // (and also respects the prefab's scale).
                transform.TransformPoint(corner),
                Vector3.up * -1f,
                data != null ? data.terrainProbeDistance : 2f,
                Globals.TERRAIN_LAYER_MASK
            ))
                continue;
            validCornersCount++;
        }
        int requiredCorners = data != null
            ? Mathf.Clamp(data.minimumSupportedCorners, 1, bottomCorners.Length)
            : 2;
        if (validCornersCount < requiredCorners)
            return false;

        // check building is within the current FOV bounds
        if (!Physics.Raycast(
            p + Vector3.up * 100,
            -Vector3.up, 1000,
            Globals.FOV_LAYER_MASK))
            return false;

        return true;
    }

    public bool Build(int buildPower)
    {
        _building.SetConstructionHP(_building.ConstructionHP + buildPower);
        UpdateHealthbar();
        return _building.IsAlive;
    }

    protected override bool IsActive()
    {
        return _building.IsFixed;
    }

    protected override bool IsMovable()
    {
        return false;
    }

    protected override void UpdateHealthbar()
    {
        if (!_healthbarRenderer) return;

        // if in construction: show current construction HP
        // else, show current health
        int hp = (IsActive() && !_building.IsAlive)
            ? _building.ConstructionHP
            : Unit.HP;

        _healthbarRenderer.GetPropertyBlock(_MaterialPropertyBlock);
        _MaterialPropertyBlock.SetFloat("_Health", hp / (float)Unit.MaxHP);
        _healthbarRenderer.SetPropertyBlock(_MaterialPropertyBlock);
    }
}
