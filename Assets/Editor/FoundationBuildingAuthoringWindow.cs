// Editor-only authoring aid for the first construction slice.
//
// This tool deliberately creates reviewable draft assets only.  It never
// changes Building Content Catalog status or assigns a catalog BuildingData
// reference automatically.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Creates a safe prefab and BuildingData draft from an authored foundation
/// model.  The resulting assets still require the Unity acceptance checklist
/// before the content catalog can be promoted.
/// </summary>
public sealed class FoundationBuildingAuthoringWindow : EditorWindow
{
    private const string TownCenterCode = "town_center";
    private const string ResourceDropoffCode = "resource_dropoff";
    private const string LumberCampCode = "lumber_camp";
    private const string MiningCampCode = "mining_camp";
    private const string MillCode = "mill";
    private const string BarracksCode = "barracks";
    private const string ArcheryRangeCode = "archery_range";
    private const string StableCode = "stable";
    private const string TownCenterUnityModel =
        "Assets/Resources/Imports/Units/Buildings/town-center/TownCenter_authoring.obj";
    private const string TownCenterGlbModel =
        "Assets/Resources/Imports/Units/Buildings/town-center/TownCenter_authoring.glb";
    private const string ResourceDropoffUnityModel =
        "Assets/Resources/Imports/Units/Buildings/resource-dropoff/ResourceDropoff_authoring.obj";
    private const string ResourceDropoffGlbModel =
        "Assets/Resources/Imports/Units/Buildings/resource-dropoff/ResourceDropoff_authoring.glb";
    private const string LumberCampUnityModel =
        "Assets/Resources/Imports/Units/Buildings/lumber-camp/LumberCamp_authoring.obj";
    private const string LumberCampGlbModel =
        "Assets/Resources/Imports/Units/Buildings/lumber-camp/LumberCamp_authoring.glb";
    private const string MiningCampUnityModel =
        "Assets/Resources/Imports/Units/Buildings/mining-camp/MiningCamp_authoring.obj";
    private const string MiningCampGlbModel =
        "Assets/Resources/Imports/Units/Buildings/mining-camp/MiningCamp_authoring.glb";
    private const string MillUnityModel =
        "Assets/Resources/Imports/Units/Buildings/mill/Mill_authoring.obj";
    private const string MillGlbModel =
        "Assets/Resources/Imports/Units/Buildings/mill/Mill_authoring.glb";
    private const string BarracksUnityModel =
        "Assets/Resources/Imports/Units/Buildings/barracks/Barracks_authoring.obj";
    private const string BarracksGlbModel =
        "Assets/Resources/Imports/Units/Buildings/barracks/Barracks_authoring.glb";
    private const string ArcheryRangeUnityModel =
        "Assets/Resources/Imports/Units/Buildings/archery-range/ArcheryRange_authoring.obj";
    private const string ArcheryRangeGlbModel =
        "Assets/Resources/Imports/Units/Buildings/archery-range/ArcheryRange_authoring.glb";
    private const string StableUnityModel =
        "Assets/Resources/Imports/Units/Buildings/stable/Stable_authoring.obj";
    private const string StableGlbModel =
        "Assets/Resources/Imports/Units/Buildings/stable/Stable_authoring.glb";
    private const string PrefabFolder = "Assets/Resources/Prefabs/Units/Buildings";
    private const string DataFolder = "Assets/Resources/ScriptableObjects/Units/Buildings";
    private const string HousePrefab = "Assets/Resources/Prefabs/Units/Buildings/House.prefab";
    private const string MinimapIconPrefab = "Assets/Resources/Prefabs/MinimapIcon.prefab";
    private const string FovPrefab = "Assets/Resources/Prefabs/FOV.prefab";
    private const string HealthbarPrefab = "Assets/Resources/Prefabs/Units/Healthbar.prefab";

    private readonly List<string> _messages = new List<string>();
    private Vector2 _scroll;

    [MenuItem("Tools/Era Imperial/Buildings/Foundation Authoring")]
    private static void Open()
    {
        GetWindow<FoundationBuildingAuthoringWindow>("Foundation Authoring");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Foundation building authoring", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Creates draft prefabs and BuildingData from Unity-imported authored models. " +
            "It does not promote the catalog or mark any entry playable.",
            MessageType.Info);

        DrawAssetRow(TownCenterCode, TownCenterUnityModel, TownCenterGlbModel);
        DrawAssetRow(ResourceDropoffCode, ResourceDropoffUnityModel, ResourceDropoffGlbModel);
        DrawAssetRow(LumberCampCode, LumberCampUnityModel, LumberCampGlbModel);
        DrawAssetRow(MiningCampCode, MiningCampUnityModel, MiningCampGlbModel);
        DrawAssetRow(MillCode, MillUnityModel, MillGlbModel);
        DrawAssetRow(BarracksCode, BarracksUnityModel, BarracksGlbModel);
        DrawAssetRow(ArcheryRangeCode, ArcheryRangeUnityModel, ArcheryRangeGlbModel);
        DrawAssetRow(StableCode, StableUnityModel, StableGlbModel);

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Audit authored models", GUILayout.Height(26f)))
                AuditModels();

            using (new EditorGUI.DisabledScope(!CanAuthorAll()))
            {
                if (GUILayout.Button("Create draft prefabs + BuildingData", GUILayout.Height(26f)))
                    CreateDraftAssets();
            }
        }

        if (GUILayout.Button("Open Unity acceptance documentation"))
            EditorUtility.DisplayDialog(
                "Foundation acceptance",
                "Read Docs/FOUNDATION_UNITY_AUTHORING.md and Docs/FOUNDATION_BUILDING_ACCEPTANCE.md in the project root.",
                "OK");

        if (_messages.Count == 0) return;
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Last audit", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(100f));
        foreach (string message in _messages)
            EditorGUILayout.HelpBox(message, message.StartsWith("ERROR", StringComparison.Ordinal)
                ? MessageType.Error
                : MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    private static void DrawAssetRow(string code, string unityModelPath, string glbPath)
    {
        string resolvedPath = ResolveModelPath(unityModelPath, glbPath);
        EditorGUILayout.BeginHorizontal("box");
        EditorGUILayout.LabelField(code, GUILayout.Width(150f));
        EditorGUILayout.LabelField(resolvedPath == null
            ? "Unity-native OBJ/FBX missing (GLB fallback unavailable)"
            : resolvedPath.EndsWith(".obj", StringComparison.OrdinalIgnoreCase)
                ? "authored OBJ ready (Unity-native)"
                : "GLB imported by an optional importer");
        EditorGUILayout.EndHorizontal();
    }

    private bool CanAuthorAll()
    {
        return AuditResolvedModel(TownCenterCode, TownCenterUnityModel, TownCenterGlbModel, false)
            && AuditResolvedModel(ResourceDropoffCode, ResourceDropoffUnityModel, ResourceDropoffGlbModel, false)
            && AuditResolvedModel(LumberCampCode, LumberCampUnityModel, LumberCampGlbModel, false)
            && AuditResolvedModel(MiningCampCode, MiningCampUnityModel, MiningCampGlbModel, false)
            && AuditResolvedModel(MillCode, MillUnityModel, MillGlbModel, false)
            && AuditResolvedModel(BarracksCode, BarracksUnityModel, BarracksGlbModel, false)
            && AuditResolvedModel(ArcheryRangeCode, ArcheryRangeUnityModel, ArcheryRangeGlbModel, false)
            && AuditResolvedModel(StableCode, StableUnityModel, StableGlbModel, false);
    }

    private void AuditModels()
    {
        _messages.Clear();
        bool townCenter = AuditResolvedModel(TownCenterCode, TownCenterUnityModel, TownCenterGlbModel, true);
        bool dropoff = AuditResolvedModel(ResourceDropoffCode, ResourceDropoffUnityModel, ResourceDropoffGlbModel, true);
        bool lumberCamp = AuditResolvedModel(LumberCampCode, LumberCampUnityModel, LumberCampGlbModel, true);
        bool miningCamp = AuditResolvedModel(MiningCampCode, MiningCampUnityModel, MiningCampGlbModel, true);
        bool mill = AuditResolvedModel(MillCode, MillUnityModel, MillGlbModel, true);
        bool barracks = AuditResolvedModel(BarracksCode, BarracksUnityModel, BarracksGlbModel, true);
        bool archeryRange = AuditResolvedModel(ArcheryRangeCode, ArcheryRangeUnityModel, ArcheryRangeGlbModel, true);
        bool stable = AuditResolvedModel(StableCode, StableUnityModel, StableGlbModel, true);
        _messages.Add(townCenter && dropoff && lumberCamp && miningCamp && mill && barracks && archeryRange && stable
            ? "Authored model sources pass the audit. Draft creation is available."
            : "Source audit failed. No draft asset was created.");
        Repaint();
    }

    private static bool AuditModel(string code, string modelPath, bool log)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
        {
            if (log) Debug.LogError("Foundation authoring: missing authored model for " + code + ": " + modelPath);
            return false;
        }

        MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
        MeshRenderer[] renderers = model.GetComponentsInChildren<MeshRenderer>(true);
        bool valid = filters.Any(filter => filter != null && filter.sharedMesh != null)
            && renderers.Any(renderer => renderer != null && renderer.sharedMaterials.Length > 0);
        if (!valid && log)
            Debug.LogError("Foundation authoring: " + code + " needs a MeshFilter and MeshRenderer with a material.");
        return valid;
    }

    private static bool AuditResolvedModel(string code, string unityModelPath, string glbPath, bool log)
    {
        string resolvedPath = ResolveModelPath(unityModelPath, glbPath);
        if (resolvedPath == null)
        {
            if (log)
                Debug.LogError("Foundation authoring: no Unity-imported source for " + code
                    + ". Generate the authored OBJ or install an approved GLB importer.");
            return false;
        }
        return AuditModel(code, resolvedPath, log);
    }

    private static string ResolveModelPath(string unityModelPath, string glbPath)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(unityModelPath) != null)
            return unityModelPath;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(glbPath) != null)
            return glbPath;
        return null;
    }

    private void CreateDraftAssets()
    {
        _messages.Clear();
        string townCenterSource = ResolveModelPath(TownCenterUnityModel, TownCenterGlbModel);
        string dropoffSource = ResolveModelPath(ResourceDropoffUnityModel, ResourceDropoffGlbModel);
        string lumberCampSource = ResolveModelPath(LumberCampUnityModel, LumberCampGlbModel);
        string miningCampSource = ResolveModelPath(MiningCampUnityModel, MiningCampGlbModel);
        string millSource = ResolveModelPath(MillUnityModel, MillGlbModel);
        string barracksSource = ResolveModelPath(BarracksUnityModel, BarracksGlbModel);
        string archeryRangeSource = ResolveModelPath(ArcheryRangeUnityModel, ArcheryRangeGlbModel);
        string stableSource = ResolveModelPath(StableUnityModel, StableGlbModel);
        if (townCenterSource == null || dropoffSource == null || lumberCampSource == null || miningCampSource == null || millSource == null || barracksSource == null || archeryRangeSource == null || stableSource == null
            || !AuditModel(TownCenterCode, townCenterSource, true)
            || !AuditModel(ResourceDropoffCode, dropoffSource, true)
            || !AuditModel(LumberCampCode, lumberCampSource, true)
            || !AuditModel(MiningCampCode, miningCampSource, true)
            || !AuditModel(MillCode, millSource, true)
            || !AuditModel(BarracksCode, barracksSource, true)
            || !AuditModel(ArcheryRangeCode, archeryRangeSource, true)
            || !AuditModel(StableCode, stableSource, true))
        {
            _messages.Add("ERROR: source audit failed; no draft assets were created.");
            Repaint();
            return;
        }

        EnsureFolder(PrefabFolder);
        EnsureFolder(DataFolder);
        CreateDraft(TownCenterCode, "Town Center", townCenterSource);
        CreateDraft(ResourceDropoffCode, "Resource Dropoff", dropoffSource);
        CreateDraft(LumberCampCode, "Lumber Camp", lumberCampSource);
        CreateDraft(MiningCampCode, "Mining Camp", miningCampSource);
        CreateDraft(MillCode, "Mill", millSource);
        CreateDraft(BarracksCode, "Barracks", barracksSource);
        CreateDraft(ArcheryRangeCode, "Archery Range", archeryRangeSource);
        CreateDraft(StableCode, "Stable", stableSource);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        _messages.Add("Draft assets created. Catalog status and BuildingData references remain unchanged.");
        Repaint();
    }

    private void CreateDraft(string code, string displayName, string modelPath)
    {
        string prefabPath = PrefabFolder + "/" + ToFileName(displayName) + "_DRAFT.prefab";
        string dataPath = DataFolder + "/" + ToFileName(displayName) + "_DRAFT.asset";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null
            || AssetDatabase.LoadAssetAtPath<BuildingData>(dataPath) != null)
        {
            _messages.Add("ERROR: draft already exists for " + code + "; delete it manually after review before regenerating.");
            return;
        }

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (instance == null)
        {
            _messages.Add("ERROR: Unity could not instantiate authored model for " + code + ".");
            return;
        }

        try
        {
            instance.name = ToFileName(displayName) + "_DRAFT";
            ConfigureDraftPrefab(instance, code);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            if (prefab == null)
            {
                _messages.Add("ERROR: Unity did not save draft prefab for " + code + ".");
                return;
            }

            CreateDraftData(code, displayName, prefab, dataPath);
            _messages.Add("Created review draft: " + prefabPath + " and " + dataPath);
        }
        finally
        {
            DestroyImmediate(instance);
        }
    }

    private static void ConfigureDraftPrefab(GameObject root, string code)
    {
        root.tag = "Unit";
        root.layer = 12;

        // Keep the hierarchy compatible with Unit's runtime lookup contract:
        // Mesh/MinimapIcon is resolved by Unit after the prefab is instantiated.
        // Authored model parts are grouped under Mesh without changing their
        // local transforms, so the imported geometry remains reviewable.
        Transform meshRoot = root.transform.Find("Mesh");
        if (meshRoot == null)
        {
            meshRoot = new GameObject("Mesh").transform;
            meshRoot.SetParent(root.transform, false);
        }

        Transform[] existingChildren = root.transform
            .Cast<Transform>()
            .Where(child => child != meshRoot && child.name != "__PlacementCollision")
            .ToArray();
        foreach (Transform child in existingChildren)
            child.SetParent(meshRoot, true);

        GameObject minimapIcon = InstantiateSupportPrefab(MinimapIconPrefab, meshRoot, "MinimapIcon");
        GameObject selectionCircle = InstantiateSelectionCircle(meshRoot.parent);
        GameObject fov = InstantiateSupportPrefab(FovPrefab, root.transform, "FOV");
        GameObject healthbar = InstantiateSupportPrefab(HealthbarPrefab, root.transform, "Healthbar");
        if (selectionCircle != null) selectionCircle.SetActive(false);
        if (fov != null) fov.SetActive(false);
        if (healthbar != null) healthbar.SetActive(false);

        Renderer renderer = root.GetComponentsInChildren<Renderer>(true)
            .OfType<MeshRenderer>()
            .FirstOrDefault(candidate => IsAuthoringGeometry(candidate)
                && candidate.sharedMaterials.Length > 0);
        MeshFilter meshFilter = root.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(candidate => IsAuthoringGeometry(candidate)
                && candidate.sharedMesh != null);
        if (renderer == null || meshFilter == null)
            throw new InvalidOperationException("Authored model has no usable renderer/mesh: " + code);

        BuildingManager manager = root.GetComponent<BuildingManager>()
            ?? root.AddComponent<BuildingManager>();
        manager.meshRenderer = renderer;
        AudioSource audio = root.GetComponent<AudioSource>()
            ?? root.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        manager.ambientSource = audio;
        manager.contextualSource = audio;
        manager.selectionCircle = selectionCircle;
        manager.fov = fov;
        manager.healthbar = healthbar;
        if (root.GetComponent<BuildingBT>() == null)
            root.AddComponent<BuildingBT>();

        Bounds bounds = CalculateBounds(root, renderer);
        BoxCollider footprint = root.GetComponent<BoxCollider>()
            ?? root.AddComponent<BoxCollider>();
        footprint.isTrigger = true;
        footprint.center = root.transform.InverseTransformPoint(bounds.center);
        footprint.size = ToLocalSize(root.transform, bounds.size);

        Rigidbody body = root.GetComponent<Rigidbody>() ?? root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        // The non-trigger collider is intentionally a child so BuildingManager
        // continues to use the root trigger for placement overlap callbacks.
        Transform collision = root.transform.Find("__PlacementCollision");
        GameObject collisionObject = collision == null
            ? new GameObject("__PlacementCollision")
            : collision.gameObject;
        collisionObject.transform.SetParent(root.transform, false);
        BoxCollider solid = collisionObject.GetComponent<BoxCollider>()
            ?? collisionObject.AddComponent<BoxCollider>();
        solid.isTrigger = false;
        solid.center = Vector3.zero;
        solid.size = footprint.size;

        NavMeshObstacle obstacle = root.GetComponent<NavMeshObstacle>()
            ?? root.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = footprint.center;
        obstacle.size = footprint.size;
        obstacle.carving = true;
    }

    private static GameObject InstantiateSupportPrefab(string assetPath, Transform parent, string objectName)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (source == null)
            throw new InvalidOperationException("Foundation authoring: required runtime support prefab is missing: " + assetPath);

        GameObject instance = UnityEngine.Object.Instantiate(source, parent);
        instance.name = objectName;
        instance.transform.localPosition = source.transform.localPosition;
        instance.transform.localRotation = source.transform.localRotation;
        instance.transform.localScale = source.transform.localScale;
        return instance;
    }

    private static GameObject InstantiateSelectionCircle(Transform parent)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(HousePrefab);
        if (source == null)
            throw new InvalidOperationException("Foundation authoring: House prefab is missing; selection circle cannot be authored.");

        Transform selection = source.transform.Find("SelectionCircle");
        if (selection == null)
            throw new InvalidOperationException("Foundation authoring: House prefab has no SelectionCircle child.");

        GameObject instance = UnityEngine.Object.Instantiate(selection.gameObject, parent);
        instance.name = "SelectionCircle";
        instance.transform.localPosition = selection.localPosition;
        instance.transform.localRotation = selection.localRotation;
        instance.transform.localScale = selection.localScale;
        return instance;
    }

    private static Bounds CalculateBounds(GameObject root, Renderer fallback)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = fallback.bounds;
        for (int i = 0; i < renderers.Length; i++)
            if (IsAuthoringGeometry(renderers[i]) && renderers[i] != fallback)
                bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static bool IsAuthoringGeometry(Renderer renderer)
    {
        if (renderer == null) return false;
        Transform meshRoot = renderer.transform.root.Find("Mesh");
        if (meshRoot == null || !renderer.transform.IsChildOf(meshRoot)) return false;
        return !string.Equals(renderer.gameObject.name, "MinimapIcon", StringComparison.Ordinal);
    }

    private static Vector3 ToLocalSize(Transform root, Vector3 worldSize)
    {
        Vector3 scale = root.lossyScale;
        return new Vector3(
            SafeDivide(worldSize.x, Mathf.Abs(scale.x)),
            SafeDivide(worldSize.y, Mathf.Abs(scale.y)),
            SafeDivide(worldSize.z, Mathf.Abs(scale.z)));
    }

    private static float SafeDivide(float value, float divisor)
    {
        return divisor > 0.0001f ? value / divisor : value;
    }

    private static void CreateDraftData(
        string code,
        string displayName,
        GameObject prefab,
        string dataPath)
    {
        BuildingData data = CreateInstance<BuildingData>();
        data.name = ToFileName(displayName) + "_DRAFT";
        data.code = code;
        data.unitName = displayName;
        data.description = GetDescription(code);
        data.healthpoints = code == TownCenterCode ? 2400 : code == BarracksCode ? 1000 : 700;
        data.prefab = prefab;
        data.cost = new List<ResourceValue>
        {
            new ResourceValue(InGameResource.Wood, code == TownCenterCode ? 400 : code == BarracksCode ? 150 : 50)
        };
        data.maxConstructors = 3;
        data.minimumSupportedCorners = 2;
        data.terrainProbeDistance = 3f;
        data.terrainProbeStartOffset = 0.5f;
        data.fieldOfView = 35f;
        data.placementGridSize = 1f;
        data.rotationStepDegrees = 15f;
        data.maximumTerrainSlopeDegrees = 20f;
        data.minimumDistanceFromBuildings = code == TownCenterCode ? 3f : 1.5f;
        data.canProduce = code == TownCenterCode
            ? new[] { InGameResource.Gold, InGameResource.Wood, InGameResource.Stone }
            : new InGameResource[0];

        List<Mesh> meshes = prefab.GetComponentsInChildren<MeshFilter>(true)
            .Where(filter => filter != null && filter.sharedMesh != null
                && IsAuthoringGeometry(filter.GetComponent<Renderer>()))
            .Select(filter => filter.sharedMesh)
            .Distinct()
            .ToList();
        data.constructionMeshes = meshes.ToArray();
        AssetDatabase.CreateAsset(data, dataPath);
        EditorUtility.SetDirty(data);
        Debug.Log("Foundation authoring created BuildingData draft at " + dataPath
            + ". Catalog promotion remains a manual review step.");
    }

    private static string GetDescription(string code)
    {
        switch (code)
        {
            case TownCenterCode:
                return "Unity authoring draft for the starting Town Center. Requires foundation acceptance review.";
            case ResourceDropoffCode:
                return "Unity authoring draft for the generic resource dropoff. Requires foundation acceptance review.";
            case LumberCampCode:
                return "Unity authoring draft for the Wood-only Lumber Camp dropoff. Requires foundation acceptance review.";
            case MiningCampCode:
                return "Unity authoring draft for the Gold/Stone Mining Camp dropoff. Requires foundation acceptance review.";
            case MillCode:
                return "Unity authoring draft for the Food-only Mill dropoff. Requires foundation acceptance review.";
            case BarracksCode:
                return "Unity authoring draft for the infantry Barracks. Requires foundation acceptance review.";
            case ArcheryRangeCode:
                return "Unity authoring draft for the ranged Archery Range. Requires foundation acceptance review.";
            case StableCode:
                return "Unity authoring draft for the cavalry Stable. Requires foundation acceptance review.";
            default:
                return "Unity authoring draft. Requires foundation acceptance review.";
        }
    }

    private static string ToFileName(string value)
    {
        return value.Replace(" ", string.Empty);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
