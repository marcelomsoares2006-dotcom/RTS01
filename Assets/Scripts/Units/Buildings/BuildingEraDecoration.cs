using EraImperial.Units.Advanced;
using UnityEngine;

/// <summary>
/// Detalhes puramente visuais para Casa e Torre. Nao substitui a malha/material principal,
/// a cor do dono, o preview de posicionamento ou as etapas de construcao.
/// AdvancedEraState ainda e alterado apenas pelo console de debug, nao por uma progressao geral.
/// </summary>
public sealed class BuildingEraDecoration : MonoBehaviour
{
    private readonly GameObject[] _eras = new GameObject[4];
    private bool _finished;
    private bool _initialized;

    public ImperialEra VisibleEra { get; private set; } = ImperialEra.Ancient;
    public bool IsFinished => _finished;

    public void Initialize(string buildingCode, Transform visualParent, Mesh baseMesh)
    {
        if (_initialized || visualParent == null || baseMesh == null) return;
        if (buildingCode != "house" && buildingCode != "tower") return;

        Bounds b = baseMesh.bounds;
        for (int i = 0; i < _eras.Length; i++)
        {
            var group = new GameObject("EraDetails_" + (ImperialEra)i);
            group.transform.SetParent(visualParent, false);
            _eras[i] = group;
            if (buildingCode == "house") CreateHouseDetails(group.transform, b, (ImperialEra)i);
            else CreateTowerDetails(group.transform, b, (ImperialEra)i);
        }

        _initialized = true;
        AdvancedEraState.Changed += Refresh;
        Refresh(AdvancedEraState.Current);
    }

    public void SetFinished(bool finished)
    {
        _finished = finished;
        Refresh(AdvancedEraState.Current);
    }

    private void OnDestroy()
    {
        if (_initialized) AdvancedEraState.Changed -= Refresh;
    }

    private void Refresh(ImperialEra era)
    {
        VisibleEra = era;
        for (int i = 0; i < _eras.Length; i++)
            if (_eras[i] != null) _eras[i].SetActive(_finished && i == (int)era);
    }

    private static void CreateHouseDetails(Transform parent, Bounds b, ImperialEra era)
    {
        float x = b.extents.x * 0.82f;
        float front = b.min.z - 0.025f;
        float bottom = b.min.y + b.size.y * 0.1f;
        float eave = b.min.y + b.size.y * 0.74f;
        float width = Mathf.Max(0.045f, b.size.x * 0.025f);
        switch (era)
        {
            case ImperialEra.Ancient:
                AddBeam(parent, "TimberPostL", new Vector3(-x, bottom + b.size.y * 0.27f, front), new Vector3(width * 1.8f, b.size.y * 0.54f, width), "Wood Log");
                AddBeam(parent, "TimberPostR", new Vector3(x, bottom + b.size.y * 0.27f, front), new Vector3(width * 1.8f, b.size.y * 0.54f, width), "Wood Log");
                break;
            case ImperialEra.Classical:
                AddBeam(parent, "StoneLintel", new Vector3(0, eave, front), new Vector3(b.size.x * 0.84f, width * 2f, width * 1.5f), "Stone");
                AddBeam(parent, "StonePlinth", new Vector3(0, bottom, front), new Vector3(b.size.x * 0.9f, width * 1.8f, width * 2f), "Stone");
                break;
            case ImperialEra.Medieval:
                AddBeam(parent, "TimberLintel", new Vector3(0, eave, front), new Vector3(b.size.x * 0.9f, width * 1.7f, width), "Wood Light");
                AddBeam(parent, "TimberSill", new Vector3(0, bottom + b.size.y * 0.14f, front), new Vector3(b.size.x * 0.9f, width, width), "Wood Light");
                break;
            case ImperialEra.Imperial:
                AddBeam(parent, "MetalEave", new Vector3(0, eave, front), new Vector3(b.size.x * 0.92f, width, width), "Metal");
                AddBeam(parent, "MetalCrest", new Vector3(0, b.max.y + width, b.center.z), new Vector3(width, width * 2f, b.size.z * 0.3f), "Metal");
                break;
        }
    }

    private static void CreateTowerDetails(Transform parent, Bounds b, ImperialEra era)
    {
        float width = Mathf.Max(0.04f, Mathf.Min(b.size.x, b.size.z) * 0.08f);
        float y = b.min.y + b.size.y * 0.73f;
        float front = b.min.z - width * 0.25f;
        switch (era)
        {
            case ImperialEra.Ancient:
                AddBeam(parent, "TimberBand", new Vector3(0, y, front), new Vector3(b.size.x * 1.08f, width, width), "Wood Log");
                break;
            case ImperialEra.Classical:
                AddBeam(parent, "StoneCornice", new Vector3(0, b.max.y - width, front), new Vector3(b.size.x * 1.16f, width * 1.5f, width * 1.5f), "Stone");
                break;
            case ImperialEra.Medieval:
                AddBeam(parent, "ParapetL", new Vector3(-b.extents.x * 0.67f, b.max.y + width * 0.25f, front), new Vector3(width * 1.5f, width * 2f, width * 1.5f), "Stone");
                AddBeam(parent, "ParapetR", new Vector3(b.extents.x * 0.67f, b.max.y + width * 0.25f, front), new Vector3(width * 1.5f, width * 2f, width * 1.5f), "Stone");
                break;
            case ImperialEra.Imperial:
                AddBeam(parent, "MetalBand", new Vector3(0, b.max.y - width, front), new Vector3(b.size.x * 1.15f, width, width * 1.5f), "Metal");
                AddBeam(parent, "Finial", new Vector3(0, b.max.y + width, b.center.z), new Vector3(width, width * 3f, width), "Metal");
                break;
        }
    }

    private static void AddBeam(Transform parent, string name, Vector3 position, Vector3 scale, string materialName)
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = name;
        beam.transform.SetParent(parent, false);
        beam.transform.localPosition = position;
        beam.transform.localScale = scale;
        Collider collider = beam.GetComponent<Collider>();
        collider.enabled = false;
        if (Application.isPlaying) Object.Destroy(collider);
        else Object.DestroyImmediate(collider);
        beam.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>("Materials/Building/" + materialName);
    }
}
