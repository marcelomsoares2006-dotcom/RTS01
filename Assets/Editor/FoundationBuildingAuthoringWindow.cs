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
    private const string WorkshopCode = "siege_workshop";
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
    private const string WorkshopUnityModel =
        "Assets/Resources/Imports/Units/Buildings/siege-workshop/SiegeWorkshop_authoring.obj";
    private const string WorkshopGlbModel =
        "Assets/Resources/Imports/Units/Buildings/siege-workshop/SiegeWorkshop_authoring.glb";
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
        DrawAssetRow(WorkshopCode, WorkshopUnityModel, WorkshopGlbModel);

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

        using (new EditorGUI.DisabledScope(!CanPromoteReviewedDrafts()))
        {
            if (GUILayout.Button("Promote reviewed drafts to playable assets", GUILayout.Height(26f)))
                PromoteReviewedDrafts();
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
            && AuditResolvedModel(StableCode, StableUnityModel, StableGlbModel, false)
            && AuditResolvedModel(WorkshopCode, WorkshopUnityModel, WorkshopGlbModel, false);
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
        bool workshop = AuditResolvedModel(WorkshopCode, WorkshopUnityModel, WorkshopGlbModel, true);
        _messages.Add(townCenter && dropoff && lumberCamp && miningCamp && mill && barracks && archeryRange && stable && workshop
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
        if (AssetDatabase.LoadAllAssetsAtPath(unityModelPath).OfType<Mesh>().Any())
            return unityModelPath;
        if (AssetDatabase.LoadAllAssetsAtPath(glbPath).OfType<Mesh>().Any())
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
        string workshopSource = ResolveModelPath(WorkshopUnityModel, WorkshopGlbModel);
        if (townCenterSource == null || dropoffSource == null || lumberCampSource == null || miningCampSource == null || millSource == null || barracksSource == null || archeryRangeSource == null || stableSource == null || workshopSource == null
            || !AuditModel(TownCenterCode, townCenterSource, true)
            || !AuditModel(ResourceDropoffCode, dropoffSource, true)
            || !AuditModel(LumberCampCode, lumberCampSource, true)
            || !AuditModel(MiningCampCode, miningCampSource, true)
            || !AuditModel(MillCode, millSource, true)
            || !AuditModel(BarracksCode, barracksSource, true)
            || !AuditModel(ArcheryRangeCode, archeryRangeSource, true)
            || !AuditModel(StableCode, stableSource, true)
            || !AuditModel(WorkshopCode, workshopSource, true))
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
        CreateDraft(WorkshopCode, "SiegeWorkshop", workshopSource);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        _messages.Add("Draft assets created. Catalog status and BuildingData references remain unchanged.");
        Repaint();
    }

    private bool CanPromoteReviewedDrafts()
    {
        foreach (FoundationSpec spec in FoundationSpecs())
        {
            string draftPrefab = PrefabFolder + "/" + ToFileName(spec.DisplayName) + "_DRAFT.prefab";
            string draftData = DataFolder + "/" + ToFileName(spec.DisplayName) + "_DRAFT.asset";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(draftPrefab) == null
                || AssetDatabase.LoadAssetAtPath<BuildingData>(draftData) == null)
                return false;
            if (!IsPromotableDraft(spec.Code, draftPrefab, draftData)) return false;
        }
        return true;
    }

    private void PromoteReviewedDrafts()
    {
        if (!CanPromoteReviewedDrafts())
        {
            _messages.Add("ERROR: every draft must pass the serialized component review before promotion.");
            Repaint();
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Promote foundation assets",
                "This moves all nine reviewed drafts to their final paths, links BuildingData in the catalog and marks them Playable. Confirm that placement and construction PlayMode review has already passed.",
                "Promote reviewed assets", "Cancel"))
            return;

        _messages.Clear();
        foreach (FoundationSpec spec in FoundationSpecs())
        {
            string draftPrefab = PrefabFolder + "/" + ToFileName(spec.DisplayName) + "_DRAFT.prefab";
            string draftData = DataFolder + "/" + ToFileName(spec.DisplayName) + "_DRAFT.asset";
            string finalPrefab = PrefabFolder + "/" + ToFileName(spec.DisplayName) + ".prefab";
            string finalData = DataFolder + "/" + ToFileName(spec.DisplayName) + ".asset";
            string prefabError = AssetDatabase.MoveAsset(draftPrefab, finalPrefab);
            string dataError = AssetDatabase.MoveAsset(draftData, finalData);
            if (!string.IsNullOrEmpty(prefabError) || !string.IsNullOrEmpty(dataError))
            {
                _messages.Add("ERROR: could not promote " + spec.Code + ": "
                    + (string.IsNullOrEmpty(prefabError) ? dataError : prefabError));
                continue;
            }

            BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(finalData);
            if (data != null)
            {
                data.name = ToFileName(spec.DisplayName);
                data.description = GetDescription(spec.Code).Replace("Unity authoring draft", "Playable foundation");
                EditorUtility.SetDirty(data);
            }
            _messages.Add("Promoted " + spec.Code + " to final prefab and BuildingData.");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        LinkCatalogReferencesAndPromoteStatus();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        _messages.Add("Catalog references linked and foundation entries marked Playable. Run Validate-FoundationAssets.ps1 after Unity finishes importing.");
        Repaint();
    }

    private static bool IsPromotableDraft(string code, string prefabPath, string dataPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(dataPath);
        if (prefab == null || data == null || data.prefab == null || data.constructionMeshes == null || data.constructionMeshes.Length == 0)
            return false;
        if (prefab.layer != 12 || !string.Equals(prefab.tag, "Unit", StringComparison.Ordinal)) return false;
        if (prefab.GetComponent<BuildingManager>() == null || prefab.GetComponent<BuildingBT>() == null)
            return false;
        if (prefab.GetComponent<Collider>() == null || prefab.GetComponent<Rigidbody>() == null
            || prefab.GetComponent<NavMeshObstacle>() == null)
            return false;
        MeshFilter filter = prefab.GetComponentsInChildren<MeshFilter>(true)
            .FirstOrDefault(candidate => candidate != null && candidate.sharedMesh != null);
        MeshRenderer renderer = prefab.GetComponentsInChildren<MeshRenderer>(true)
            .FirstOrDefault(candidate => candidate != null && candidate.sharedMaterials.Length > 0);
        return filter != null && renderer != null && string.Equals(data.code, code, StringComparison.Ordinal);
    }

    private static void LinkCatalogReferencesAndPromoteStatus()
    {
        const string catalogPath = "Assets/Resources/ScriptableObjects/Units/Building Content Catalog.asset";
        UnityEngine.Object catalog = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(catalogPath);
        if (catalog == null) throw new InvalidOperationException("Foundation authoring: catalog asset is missing: " + catalogPath);

        SerializedObject serializedCatalog = new SerializedObject(catalog);
        SerializedProperty entries = serializedCatalog.FindProperty("entries");
        if (entries == null || !entries.isArray) throw new InvalidOperationException("Foundation authoring: catalog entries array is missing.");
        foreach (FoundationSpec spec in FoundationSpecs())
        {
            string dataPath = DataFolder + "/" + ToFileName(spec.DisplayName) + ".asset";
            BuildingData data = AssetDatabase.LoadAssetAtPath<BuildingData>(dataPath);
            bool found = false;
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                SerializedProperty code = entry.FindPropertyRelative("code");
                if (code == null || !string.Equals(code.stringValue, spec.Code, StringComparison.OrdinalIgnoreCase)) continue;
                SerializedProperty status = entry.FindPropertyRelative("status");
                SerializedProperty buildingData = entry.FindPropertyRelative("buildingData");
                if (status == null || buildingData == null) throw new InvalidOperationException("Foundation authoring: catalog schema changed for " + spec.Code + ".");
                status.enumValueIndex = (int)BuildingContentStatus.Playable;
                buildingData.objectReferenceValue = data;
                found = true;
                break;
            }
            if (!found) throw new InvalidOperationException("Foundation authoring: catalog entry is missing: " + spec.Code);
        }
        serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private sealed class FoundationSpec
    {
        public readonly string Code;
        public readonly string DisplayName;
        public FoundationSpec(string code, string displayName) { Code = code; DisplayName = displayName; }
    }

    private static IEnumerable<FoundationSpec> FoundationSpecs()
    {
        yield return new FoundationSpec(TownCenterCode, "Town Center");
        yield return new FoundationSpec(ResourceDropoffCode, "Resource Dropoff");
        yield return new FoundationSpec(LumberCampCode, "Lumber Camp");
        yield return new FoundationSpec(MiningCampCode, "Mining Camp");
        yield return new FoundationSpec(MillCode, "Mill");
        yield return new FoundationSpec(BarracksCode, "Barracks");
        yield return new FoundationSpec(ArcheryRangeCode, "Archery Range");
        yield return new FoundationSpec(StableCode, "Stable");
        yield return new FoundationSpec(WorkshopCode, "SiegeWorkshop");
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
        bool sourceHasMesh = source != null
            && source.transform.Find("Mesh") != null
            && source.GetComponentsInChildren<MeshFilter>(true).Any(filter => filter.sharedMesh != null);
        GameObject instance = sourceHasMesh
            ? PrefabUtility.InstantiatePrefab(source) as GameObject
            : CreateImportedMeshRoot(modelPath, code);
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

    private static GameObject CreateImportedMeshRoot(string modelPath, string code)
    {
        Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().FirstOrDefault();
        if (mesh == null) return null;
        GameObject root = new GameObject(code + "_ImportedModel");
        GameObject meshObject = new GameObject("Mesh");
        meshObject.transform.SetParent(root.transform, false);
        MeshFilter filter = meshObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
        Material[] materials = AssetDatabase.LoadAllAssetsAtPath(modelPath)
            .OfType<Material>().ToArray();
        if (materials.Length == 0)
        {
            GameObject house = AssetDatabase.LoadAssetAtPath<GameObject>(HousePrefab);
            materials = house == null
                ? new[] { new Material(Shader.Find("Standard")) }
                : house.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(item => item.sharedMaterials)
                    .Where(item => item != null)
                    .Take(1).ToArray();
        }
        renderer.sharedMaterials = materials;
        return root;
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

        InstantiateSupportPrefab(MinimapIconPrefab, meshRoot, "MinimapIcon");
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
            .FirstOrDefault(candidate => IsAuthoringGeometry(candidate.GetComponent<Renderer>())
                && candidate.sharedMesh != null);
        if (renderer == null || meshFilter == null)
            throw new InvalidOperationException("Authored model has no usable renderer/mesh: " + code);

        BuildingManager manager = root.GetComponent<BuildingManager>()
            ?? root.AddComponent<BuildingManager>();
        manager.meshRenderer = renderer;
        AudioSource audio = root.GetComponent<AudioSource>()
            ?? root.AddComponent<AudioSource>();
        if (audio != null) audio.playOnAwake = false;
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
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
        }

        // The non-trigger collider is intentionally a child so BuildingManager
        // continues to use the root trigger for placement overlap callbacks.
        Transform collision = root.transform.Find("__PlacementCollision");
        GameObject collisionObject = collision == null
            ? new GameObject("__PlacementCollision")
            : collision.gameObject;
        collisionObject.transform.SetParent(root.transform, false);
        BoxCollider solid = collisionObject.GetComponent<BoxCollider>()
            ?? collisionObject.AddComponent<BoxCollider>();
        if (solid != null)
        {
            solid.isTrigger = false;
            solid.center = Vector3.zero;
            solid.size = footprint.size;
        }

        NavMeshObstacle obstacle = root.GetComponent<NavMeshObstacle>()
            ?? root.AddComponent<NavMeshObstacle>();
        if (obstacle != null)
        {
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = footprint.center;
            obstacle.size = footprint.size;
            obstacle.carving = true;
        }
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
            new ResourceValue(InGameResource.Wood, GetConstructionCost(code))
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
            case WorkshopCode:
                return "Unity authoring draft for the Castle Age Siege Workshop. Requires foundation acceptance review.";
            default:
                return "Unity authoring draft. Requires foundation acceptance review.";
        }
    }

    private static int GetConstructionCost(string code)
    {
        if (string.Equals(code, TownCenterCode, StringComparison.Ordinal)) return 400;
        if (string.Equals(code, BarracksCode, StringComparison.Ordinal)
            || string.Equals(code, ArcheryRangeCode, StringComparison.Ordinal)
            || string.Equals(code, StableCode, StringComparison.Ordinal)) return 150;
        if (string.Equals(code, WorkshopCode, StringComparison.Ordinal)) return 300;
        return 50;
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
