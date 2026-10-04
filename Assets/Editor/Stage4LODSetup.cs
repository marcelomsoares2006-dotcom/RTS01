using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Builds separate LOD prefabs; keeps the original character prefabs untouched.</summary>
public static class Stage4LODSetup
{
    static readonly (string name, string prefab)[] Sources =
    {
        ("HeraldicKnight", "Assets/CharacterRigging/HeraldicKnight/Prefabs/HeraldicKnight.prefab"),
        ("Farmhand", "Assets/CharacterRigging/Stage2/Farmhand/Prefabs/Farmhand.prefab"),
        ("Spearman", "Assets/CharacterRigging/Stage2/Spearman/Prefabs/Spearman.prefab"),
        ("RedDeer", "Assets/CharacterRigging/Stage3/RedDeer/Prefabs/RedDeer.prefab")
    };

    [MenuItem("Tools/Character Rigging/Stages 3-5/Build LOD Prefabs")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before building LOD prefabs.");
        foreach (var source in Sources) BuildOne(source.name, source.prefab);
        AssetDatabase.SaveAssets();
    }

    static void BuildOne(string name, string path)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (source == null) throw new InvalidOperationException("Missing prepared prefab: " + path);
        string folder = "Assets/CharacterRigging/LODs/" + name;
        var models = Enumerable.Range(1, 2).Select(i =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}_LOD{i}.fbx")).ToArray();
        if (models.Any(m => m == null)) throw new InvalidOperationException("Missing LOD models for " + name);
        var obj = UnityEngine.Object.Instantiate(source);obj.name = name + "_LODs";
        try
        {
            var baseRenderer = obj.GetComponentInChildren<SkinnedMeshRenderer>();
            if (baseRenderer == null) throw new InvalidOperationException("Missing base skin " + name);
            var boneMap = obj.GetComponentsInChildren<Transform>(true)
                .GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var renderers = new List<Renderer> { baseRenderer };
            for (int lodIndex=0;lodIndex<models.Length;lodIndex++)
            {
                var model=models[lodIndex];
                var lod = UnityEngine.Object.Instantiate(model, obj.transform);
                lod.name = model.name;
                lod.transform.localPosition = Vector3.zero;
                lod.transform.localRotation = Quaternion.identity;
                lod.transform.localScale = Vector3.one;
                var skin = lod.GetComponentInChildren<SkinnedMeshRenderer>();
                if (skin == null) throw new InvalidOperationException("Missing LOD skin " + model.name);
                var oldBones = skin.bones;
                var mapped = oldBones.Select(b =>
                {
                    if (!boneMap.TryGetValue(b.name, out var target))
                        throw new InvalidOperationException("Cannot map LOD bone " + b.name);
                    return target;
                }).ToArray();
                if (!boneMap.TryGetValue(skin.rootBone.name, out var root))
                    throw new InvalidOperationException("Cannot map LOD root bone " + skin.rootBone.name);
                skin.bones = mapped;skin.rootBone = root;
                // FBXs were exported independently. Their original bind matrices
                // reference their own armature, not the base prefab's skeleton.
                // Rebind a mesh copy in this renderer's local space before saving.
                var rebound=UnityEngine.Object.Instantiate(skin.sharedMesh);
                rebound.name=name+"_LOD"+(lodIndex+1)+"_Bound";
                rebound.bindposes=mapped.Select(b=>
                    b.worldToLocalMatrix*skin.transform.localToWorldMatrix).ToArray();
                string meshPath=$"{folder}/{rebound.name}.asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (saved == null)
                {
                    AssetDatabase.CreateAsset(rebound,meshPath);
                    saved=rebound;
                }
                else
                {
                    EditorUtility.CopySerialized(rebound,saved);
                    UnityEngine.Object.DestroyImmediate(rebound);
                }
                skin.sharedMesh=saved;
                skin.sharedMaterial = baseRenderer.sharedMaterial;
                foreach (var animator in lod.GetComponentsInChildren<Animator>(true))
                    UnityEngine.Object.DestroyImmediate(animator);
                renderers.Add(skin);
            }
            var group = obj.GetComponent<LODGroup>();
            if (group == null) group = obj.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(.55f, new[] { renderers[0] }),
                new LOD(.22f, new[] { renderers[1] }),
                new LOD(.04f, new[] { renderers[2] })
            });
            group.RecalculateBounds();
            string targetPath = folder + "/" + name + "_LODs.prefab";
            PrefabUtility.SaveAsPrefabAsset(obj, targetPath);
            Debug.Log("Created skinned LOD prefab: " + targetPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(obj); }
    }
}
