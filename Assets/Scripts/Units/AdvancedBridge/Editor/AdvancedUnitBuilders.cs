using System;
using System.Collections.Generic;
using EraImperial.Units.Advanced;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Ferramentas de editor compartilhadas pelas montagens das unidades avancadas (Mage, Paladin, Catapult, Crossbow):
/// wrapper jogavel clonado do Soldier (mesma receita do Archer), anel de time, parametros de Animator exigidos pelo
/// behavior tree, vinculo contrato -> assets reais e registro condicionado no catalogo (Resources).
/// </summary>
public static class AdvancedUnitBuilders
{
    public const string SoldierPrefabPath = "Assets/Resources/Prefabs/Units/Characters/Soldier.prefab";
    public const string CharacterDataFolder = "Assets/Resources/ScriptableObjects/Units/Characters";
    public const string DataRoot = "Assets/Data/AdvancedUnits";

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    public static Transform FindDeep(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    public static string RelativePath(Transform root, Transform node)
    {
        List<string> parts = new List<string>();
        for (Transform t = node; t != null && t != root; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    // ------------------------------------------------------------------ team ring / materials

    public static Material GetOrCreateTeamRingMaterial()
    {
        EnsureFolder(DataRoot + "/Materials");
        string path = DataRoot + "/Materials/AdvancedTeamRing.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        Shader shader = Shader.Find("Unlit/Color");
        if (shader == null) throw new InvalidOperationException("Shader Unlit/Color nao encontrado.");
        material = new Material(shader) { color = Color.white };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    /// <summary>Anel plano (disco vazado) usado como renderer de UnitManager: define MeshSize e recebe a cor do time.</summary>
    public static Mesh GetOrCreateRingMesh(float outerRadius)
    {
        EnsureFolder(DataRoot + "/Meshes");
        string path = DataRoot + "/Meshes/TeamRing_" + outerRadius.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".asset";
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        Mesh mesh = existing != null ? existing : new Mesh { name = "TeamRing" };

        const int segments = 48;
        float inner = outerRadius * 0.9f;
        Vector3[] vertices = new Vector3[segments * 2];
        int[] triangles = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            vertices[i * 2] = dir * outerRadius;
            vertices[i * 2 + 1] = dir * inner;
            int next = (i + 1) % segments;
            int o0 = i * 2, i0 = i * 2 + 1, o1 = next * 2, i1 = next * 2 + 1;
            int t = i * 6;
            // ordem horaria vista de cima (+Y): faces voltadas para cima; com Unlit/Color e culling, o inverso some
            triangles[t] = o0; triangles[t + 1] = i0; triangles[t + 2] = o1;
            triangles[t + 3] = i0; triangles[t + 4] = i1; triangles[t + 5] = o1;
        }
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (existing == null) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh); // reescreve a malha existente (mesmo GUID) com a ordem corrigida
        return mesh;
    }

    // ------------------------------------------------------------------ wrapper

    /// <summary>
    /// Clona o wrapper do Soldier (CharacterManager, NavMeshAgent, BoxCollider, FOV, barra de vida, icone do minimapa),
    /// troca a malha do soldado pelo prefab visual e liga o anel de time como renderer de UnitManager.
    /// Devolve a instancia raiz (nao salva): adicione os componentes da unidade e chame SaveWrapper.
    /// </summary>
    public static GameObject BuildWrapperRoot(string unitName, GameObject visualPrefab, float ringRadius, out GameObject visualInstance)
    {
        GameObject soldier = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPrefabPath);
        if (soldier == null) throw new InvalidOperationException("Soldier.prefab ausente.");
        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(soldier);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        root.name = unitName;

        Transform mesh = root.transform.Find("Mesh");
        Transform icon = mesh.Find("MinimapIcon");
        Vector3 iconPosition = icon.position; Quaternion iconRotation = icon.rotation; Vector3 iconScale = icon.lossyScale;
        UnityEngine.Object.DestroyImmediate(mesh.GetComponent<MeshRenderer>());
        UnityEngine.Object.DestroyImmediate(mesh.GetComponent<MeshFilter>());
        mesh.localPosition = Vector3.zero; mesh.localRotation = Quaternion.identity; mesh.localScale = Vector3.one;
        icon.position = iconPosition; icon.rotation = iconRotation; icon.localScale = iconScale;

        visualInstance = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, mesh);
        visualInstance.transform.localPosition = Vector3.zero;
        visualInstance.transform.localRotation = Quaternion.identity;

        GameObject ring = new GameObject("TeamRing");
        ring.transform.SetParent(mesh, false);
        ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        ring.AddComponent<MeshFilter>().sharedMesh = GetOrCreateRingMesh(ringRadius);
        MeshRenderer ringRenderer = ring.AddComponent<MeshRenderer>();
        ringRenderer.sharedMaterial = GetOrCreateTeamRingMaterial();
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;

        MonoBehaviour manager = root.GetComponent("CharacterManager") as MonoBehaviour;
        if (manager == null) throw new InvalidOperationException("Soldier.prefab sem CharacterManager.");
        SerializedObject serialized = new SerializedObject(manager);
        serialized.FindProperty("meshRenderer").objectReferenceValue = ringRenderer;
        serialized.FindProperty("animator").objectReferenceValue = visualInstance.GetComponentInChildren<Animator>();
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return root;
    }

    public static GameObject SaveWrapper(GameObject root, string path)
    {
        EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------ animator

    /// <summary>
    /// Declara os parametros que o behavior tree e os controladores escrevem: Running, Attacking (bool), Hit (trigger,
    /// ignorado), Speed (float), Fire, Damaged, Heal (trigger). Evita avisos "Parameter does not exist".
    /// </summary>
    public static void AddStandardParameters(AnimatorController controller)
    {
        AddParameter(controller, "Running", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "Attacking", AnimatorControllerParameterType.Bool);
        AddParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        AddParameter(controller, "Fire", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Damaged", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "Heal", AnimatorControllerParameterType.Trigger);
    }

    public static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in controller.parameters)
            if (p.name == name) return;
        controller.AddParameter(name, type);
    }

    // ------------------------------------------------------------------ contract linking

    public static T LoadContract<T>(string assetName) where T : AdvancedUnitContract
    {
        string path = DataRoot + "/" + assetName + ".asset";
        T contract = AssetDatabase.LoadAssetAtPath<T>(path);
        if (contract == null)
            throw new InvalidOperationException("Contrato ausente: " + path + " (rode Era Imperial > Advanced Units > Criar assets de contrato).");
        return contract;
    }

    /// <summary>Atribui o asset real a uma referencia visual pelo papel (role) e atualiza o caminho esperado.</summary>
    public static void BindVisualReference(AdvancedUnitContract contract, string listName, string role, UnityEngine.Object asset)
    {
        SerializedObject so = new SerializedObject(contract);
        SerializedProperty list = so.FindProperty("visual." + listName);
        bool found = false;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            if (element.FindPropertyRelative("role").stringValue != role) continue;
            element.FindPropertyRelative("asset").objectReferenceValue = asset;
            element.FindPropertyRelative("expectedAssetPath").stringValue = AssetDatabase.GetAssetPath(asset);
            found = true;
        }
        if (!found) throw new InvalidOperationException($"{contract.code}: referencia '{role}' nao existe em visual.{listName}.");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(contract);
    }

    public static void BindPrefab(AdvancedUnitContract contract, GameObject wrapperPrefab)
    {
        SerializedObject so = new SerializedObject(contract);
        SerializedProperty prefab = so.FindProperty("visual.prefab");
        prefab.FindPropertyRelative("asset").objectReferenceValue = wrapperPrefab;
        prefab.FindPropertyRelative("expectedAssetPath").stringValue = AssetDatabase.GetAssetPath(wrapperPrefab);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(contract);
    }

    /// <summary>Atualiza o caminho esperado do socket para o caminho real (relativo a raiz do wrapper).</summary>
    public static void BindSocket(AdvancedUnitContract contract, string socketName, string pathFromWrapperRoot)
    {
        SerializedObject so = new SerializedObject(contract);
        SerializedProperty list = so.FindProperty("visual.sockets");
        bool found = false;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            if (element.FindPropertyRelative("socketName").stringValue != socketName) continue;
            element.FindPropertyRelative("expectedTransformPath").stringValue = pathFromWrapperRoot;
            found = true;
        }
        if (!found) throw new InvalidOperationException($"{contract.code}: socket '{socketName}' nao existe no contrato.");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(contract);
    }

    public static void BindAnimation(AdvancedUnitContract contract, string state, AnimationClip clip)
    {
        SerializedObject so = new SerializedObject(contract);
        SerializedProperty list = so.FindProperty("visual.animations");
        bool found = false;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            if (element.FindPropertyRelative("state").stringValue != state) continue;
            element.FindPropertyRelative("clip").objectReferenceValue = clip;
            found = true;
        }
        if (!found) throw new InvalidOperationException($"{contract.code}: animacao '{state}' nao existe no contrato.");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(contract);
    }

    public static void BindProjectile(ProjectileContract projectile, GameObject prefab, GameObject impactEffect)
    {
        SerializedObject so = new SerializedObject(projectile);
        SerializedProperty p = so.FindProperty("prefab");
        p.FindPropertyRelative("asset").objectReferenceValue = prefab;
        p.FindPropertyRelative("expectedAssetPath").stringValue = AssetDatabase.GetAssetPath(prefab);
        if (impactEffect != null)
        {
            SerializedProperty e = so.FindProperty("impactEffect");
            e.FindPropertyRelative("asset").objectReferenceValue = impactEffect;
            e.FindPropertyRelative("expectedAssetPath").stringValue = AssetDatabase.GetAssetPath(impactEffect);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(projectile);
    }

    // ------------------------------------------------------------------ catalog

    /// <summary>
    /// Registra o CharacterData em Resources SOMENTE se o contrato for valido e o visual estiver 100% real.
    /// Do contrario nao cria nada e devolve null com o motivo.
    /// </summary>
    public static CharacterData RegisterCharacterData(AdvancedUnitContract contract, string assetName, out string reason)
    {
        if (!AdvancedUnitDataBridge.CanBind(contract, out reason)) return null;
        if (contract.dataKind != UnitDataKind.Character) { reason = "dataKind nao e Character"; return null; }

        EnsureFolder(CharacterDataFolder);
        string path = CharacterDataFolder + "/" + assetName + ".asset";
        CharacterData data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<CharacterData>();
            AssetDatabase.CreateAsset(data, path);
        }
        AdvancedUnitDataBridge.Apply(data, contract, ImperialEra.Ancient);
        data.canProduce = new InGameResource[0];
        data.skills = new List<SkillData>();
        data.buildRange = 0f;
        data.buildPower = 0;
        data.buildRate = 0f;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        reason = null;
        return data;
    }

    public static void ConfigureAgent(GameObject root, float speed, float acceleration, float stoppingDistance, float angularSpeed = 360f)
    {
        NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
        agent.speed = speed;
        agent.acceleration = acceleration;
        agent.stoppingDistance = stoppingDistance;
        agent.angularSpeed = angularSpeed;
    }
}
