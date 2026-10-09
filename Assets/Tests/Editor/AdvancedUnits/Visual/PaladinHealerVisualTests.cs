using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Paladin;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EraImperial.AdvancedUnits.Tests.Visual
{
    /// <summary>
    /// Testes EditMode do visual do Paladino curador (prefab, componentes, Avatar/Animator, materiais, LODs, contrato x assets).
    /// Este asmdef nao enxerga o Assembly-CSharp: componentes do jogo sao acessados por nome e por SerializedObject.
    /// </summary>
    public class PaladinHealerVisualTests
    {
        const string Root = "Assets/CharacterRigging/PaladinHealer";
        const string WrapperPath = "Assets/Resources/Prefabs/Units/Characters/PaladinHealer.prefab";
        const string VisualPath = Root + "/Prefabs/PaladinHealerVisual.prefab";
        const string DeathPath = Root + "/Prefabs/PaladinHealerDeath.prefab";
        const string EffectPath = "Assets/Resources/Prefabs/Effects/PaladinHealAura.prefab";
        const string ContractPath = "Assets/Data/AdvancedUnits/PaladinHealer.asset";
        static readonly string[] ClipNames = { "Idle", "Walk", "Run", "Attack", "Heal", "Hit", "Death" };

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "Asset ausente: " + path);
            return asset;
        }

        static PaladinHealerContract Contract() { return Load<PaladinHealerContract>(ContractPath); }

        static AnimationClip Clip(string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(Root + "/Models/PaladinHealer@" + name + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform child in t)
            {
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        [Test]
        public void WrapperPrefab_HasGameComponentsAndColliderAndAgent()
        {
            GameObject wrapper = Load<GameObject>(WrapperPath);
            foreach (string component in new[] { "CharacterManager", "CharacterBT", "FogRendererToggler", "AdvancedUnitEraBinding", "AdvancedUnitAnimatorDriver", "PaladinHealBehaviour", "AdvancedUnitDeathEffect" })
                Assert.That(wrapper.GetComponent(component), Is.Not.Null, component);
            BoxCollider box = wrapper.GetComponent<BoxCollider>();
            UnityEngine.AI.NavMeshAgent agent = wrapper.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Assert.That(box, Is.Not.Null);
            Assert.That(agent, Is.Not.Null);
            Assert.That(box.size.y, Is.GreaterThanOrEqualTo(1.9f));
            Assert.That(agent.height, Is.GreaterThanOrEqualTo(1.8f));
            Assert.That(wrapper.transform.Find("Mesh/MinimapIcon"), Is.Not.Null);
            Assert.That(wrapper.transform.Find("Mesh/TeamRing"), Is.Not.Null, "anel de time");
            Assert.That(wrapper.transform.Find("Mesh/PaladinHealerVisual"), Is.Not.Null);
        }

        [Test]
        public void WrapperPrefab_ReferencesAreAssigned_AndMeleeUsesTheBehaviourTree()
        {
            GameObject wrapper = Load<GameObject>(WrapperPath);
            PaladinHealerContract contract = Contract();
            Animator animator = wrapper.GetComponentInChildren<Animator>();

            SerializedObject manager = new SerializedObject(wrapper.GetComponent("CharacterManager"));
            Assert.That(manager.FindProperty("meshRenderer").objectReferenceValue.name, Is.EqualTo("TeamRing"));
            Assert.That(manager.FindProperty("animator").objectReferenceValue, Is.EqualTo(animator));
            Assert.That(manager.FindProperty("selectionCircle").objectReferenceValue, Is.Not.Null);
            Assert.That(manager.FindProperty("fov").objectReferenceValue, Is.Not.Null);

            Assert.That(new SerializedObject(wrapper.GetComponent("AdvancedUnitEraBinding")).FindProperty("contract").objectReferenceValue, Is.EqualTo(contract));
            Assert.That(new SerializedObject(wrapper.GetComponent("AdvancedUnitAnimatorDriver")).FindProperty("animator").objectReferenceValue, Is.EqualTo(animator));
            SerializedObject heal = new SerializedObject(wrapper.GetComponent("PaladinHealBehaviour"));
            Assert.That(heal.FindProperty("contract").objectReferenceValue, Is.EqualTo(contract));
            Assert.That(heal.FindProperty("animator").objectReferenceValue, Is.EqualTo(animator));
            Assert.That(heal.FindProperty("effectOrigin").objectReferenceValue.name, Is.EqualTo("HealAuraCenter"));
            Assert.That(heal.FindProperty("healEffectPrefab").objectReferenceValue, Is.EqualTo(Load<GameObject>(EffectPath)));
            Assert.That(new SerializedObject(wrapper.GetComponent("AdvancedUnitDeathEffect")).FindProperty("deathPrefab").objectReferenceValue, Is.EqualTo(Load<GameObject>(DeathPath)));

            bool hasAttackHandler = wrapper.GetComponents<MonoBehaviour>().Any(m => m.GetType().GetInterfaces().Any(i => i.Name == "IAttackHandler"));
            Assert.That(hasAttackHandler, Is.False, "ataque corpo a corpo segue o behavior tree padrao");
        }

        [Test]
        public void VisualPrefab_HasValidHumanoidAvatarAndAnimatorController()
        {
            GameObject visual = Load<GameObject>(VisualPath);
            Animator animator = visual.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            AnimatorController controller = (AnimatorController)animator.runtimeAnimatorController;
            Assert.That(controller, Is.Not.Null);
            foreach (string p in new[] { "Running", "Attacking", "Hit", "Speed", "Fire", "Damaged", "Heal" })
                Assert.That(controller.parameters.Any(q => q.name == p), Is.True, "parametro " + p);
            Assert.That(controller.parameters.First(p => p.name == "Hit").type, Is.EqualTo(AnimatorControllerParameterType.Trigger));
            Assert.That(controller.parameters.First(p => p.name == "Running").type, Is.EqualTo(AnimatorControllerParameterType.Bool));
            Assert.That(controller.parameters.First(p => p.name == "Attacking").type, Is.EqualTo(AnimatorControllerParameterType.Bool));
            Assert.That(controller.layers.Length, Is.EqualTo(2));
            Assert.That(controller.layers[1].avatarMask, Is.Not.Null);
            Assert.That(controller.layers[0].stateMachine.states.Select(s => s.state.name), Is.SupersetOf(new[] { "Locomotion", "Attack", "HitReact" }));
        }

        [Test]
        public void Clips_AllSevenAreImportedAsHumanoidMotion()
        {
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(Root + "/Models/PaladinHealer.fbx").OfType<Avatar>().FirstOrDefault();
            Assert.That(avatar != null && avatar.isValid && avatar.isHuman, Is.True);
            foreach (string name in ClipNames)
            {
                AnimationClip clip = Clip(name);
                Assert.That(clip, Is.Not.Null, name);
                Assert.That(clip.isHumanMotion, Is.True, name);
                Assert.That(clip.length, Is.GreaterThan(0.3f), name);
                ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(Root + "/Models/PaladinHealer@" + name + ".fbx");
                Assert.That(importer.sourceAvatar, Is.EqualTo(avatar), name);
            }
            Assert.That(Clip("Idle").isLooping && Clip("Walk").isLooping && Clip("Run").isLooping, Is.True);
            Assert.That(Clip("Death").isLooping, Is.False);
        }

        [Test]
        public void Materials_AreValidShaders_NoErrorShader()
        {
            foreach (string path in new[] { VisualPath, DeathPath, EffectPath, WrapperPath })
            {
                GameObject root = Load<GameObject>(path);
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, path + "/" + renderer.name);
                        Assert.That(material.shader, Is.Not.Null);
                        Assert.That(material.shader.isSupported, Is.True, material.name);
                        Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False, material.name);
                        Assert.That(material.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"), material.name);
                    }
            }
            GameObject visual = Load<GameObject>(VisualPath);
            SkinnedMeshRenderer lod0 = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.EndsWith("LOD0"));
            Assert.That(lod0.sharedMaterial.name, Is.EqualTo("PaladinHealer_Mat"));
            Assert.That(lod0.sharedMaterial.mainTexture, Is.Not.Null);
            Assert.That(lod0.sharedMaterial.GetTexture("_BumpMap"), Is.Not.Null);
            Assert.That(lod0.sharedMaterial.GetTexture("_MetallicGlossMap"), Is.Not.Null);
        }

        [Test]
        public void Lods_AreStrictlyDecreasing_WithNormalizedWeights()
        {
            GameObject visual = Load<GameObject>(VisualPath);
            LODGroup group = visual.GetComponentInChildren<LODGroup>();
            Assert.That(group, Is.Not.Null);
            Assert.That(group.lodCount, Is.EqualTo(3));
            LOD[] lods = group.GetLODs();
            Assert.That(lods[0].screenRelativeTransitionHeight, Is.GreaterThan(lods[1].screenRelativeTransitionHeight));
            Assert.That(lods[1].screenRelativeTransitionHeight, Is.GreaterThan(lods[2].screenRelativeTransitionHeight));
            SkinnedMeshRenderer[] smrs = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(r => r.name).ToArray();
            Assert.That(smrs.Length, Is.EqualTo(3));
            long[] tris = smrs.Select(r => r.sharedMesh.triangles.Length / 3L).ToArray();
            Assert.That(tris[0], Is.LessThanOrEqualTo(31000));
            Assert.That(tris[1], Is.LessThan(tris[0]));
            Assert.That(tris[2], Is.LessThan(tris[1]));
            foreach (SkinnedMeshRenderer smr in smrs)
            {
                BoneWeight[] weights = smr.sharedMesh.boneWeights;
                Assert.That(weights.Count(w => Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f) > 0.02f), Is.EqualTo(0), smr.name);
                Assert.That(smr.bones.Length, Is.GreaterThanOrEqualTo(22), smr.name);
            }
        }

        [Test]
        public void SwordAndShield_AreSeparateObjectsOnTheSockets()
        {
            GameObject visual = Load<GameObject>(VisualPath);
            Animator animator = visual.GetComponent<Animator>();
            Transform weapon = FindDeep(visual.transform, "WeaponMount");
            Transform shieldMount = FindDeep(visual.transform, "ShieldMount");
            Transform center = FindDeep(visual.transform, "HealAuraCenter");
            Assert.That(weapon, Is.Not.Null);
            Assert.That(shieldMount, Is.Not.Null);
            Assert.That(center, Is.Not.Null);
            Assert.That(weapon.parent, Is.EqualTo(animator.GetBoneTransform(HumanBodyBones.RightHand)));
            Assert.That(shieldMount.parent, Is.EqualTo(animator.GetBoneTransform(HumanBodyBones.LeftLowerArm)));
            Assert.That(center.localPosition.y, Is.InRange(1.0f, 1.5f), "altura do peito");
            Transform sword = weapon.Find("Sword");
            Transform shield = shieldMount.Find("Shield");
            Assert.That(sword, Is.Not.Null);
            Assert.That(shield, Is.Not.Null);
            string[] swordMaterials = sword.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m.name).ToArray();
            string[] shieldMaterials = shield.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m.name).ToArray();
            Assert.That(swordMaterials, Is.SupersetOf(new[] { "PaladinHealer_Steel", "PaladinHealer_Gold", "PaladinHealer_Leather" }));
            Assert.That(shieldMaterials, Is.SupersetOf(new[] { "PaladinHealer_Steel", "PaladinHealer_Leather", "PaladinHealer_Wood" }));
        }

        [Test]
        public void HealEffectPrefab_IsOneUnitWideInXZ_WithParticlesAndTextures()
        {
            GameObject effect = Load<GameObject>(EffectPath);
            Assert.That(effect.transform.localScale, Is.EqualTo(Vector3.one));
            MeshRenderer[] renderers = effect.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.GreaterThanOrEqualTo(3));
            Bounds bounds = renderers[0].bounds;
            foreach (MeshRenderer r in renderers) bounds.Encapsulate(r.bounds);
            Assert.That(bounds.size.x, Is.InRange(0.95f, 1.001f));
            Assert.That(bounds.size.z, Is.InRange(0.95f, 1.001f));
            ParticleSystem ps = effect.GetComponentInChildren<ParticleSystem>(true);
            Assert.That(ps, Is.Not.Null);
            Assert.That(ps.main.scalingMode, Is.EqualTo(ParticleSystemScalingMode.Shape));
            Assert.That(effect.GetComponent("PaladinHealAuraEffect"), Is.Not.Null);
            foreach (Renderer r in effect.GetComponentsInChildren<Renderer>(true))
                Assert.That(r.sharedMaterial.mainTexture, Is.Not.Null, r.name);
        }

        [Test]
        public void DeathPrefab_PlaysTheDeathClipOnItsOwn()
        {
            GameObject death = Load<GameObject>(DeathPath);
            Animator animator = death.GetComponent<Animator>();
            AnimatorController controller = (AnimatorController)animator.runtimeAnimatorController;
            Assert.That(controller.layers[0].stateMachine.states.Length, Is.EqualTo(1));
            Assert.That(controller.layers[0].stateMachine.defaultState.name, Is.EqualTo("Death"));
            Assert.That(controller.layers[0].stateMachine.defaultState.motion, Is.EqualTo(Clip("Death")));
        }

        [Test]
        public void Contract_MatchesTheRealAssets()
        {
            PaladinHealerContract contract = Contract();
            Assert.That(contract.IsGameplayValid, Is.True, contract.Validate().ToString());
            Assert.That(contract.IsVisualReady, Is.True, contract.EvaluateVisuals().ToString());
            GameObject wrapper = Load<GameObject>(WrapperPath);
            Assert.That(contract.visual.prefab.asset, Is.EqualTo(wrapper));
            foreach (SocketRequirement socket in contract.visual.sockets)
            {
                Assert.That(socket.IsBoundTo(wrapper), Is.True, socket.socketName);
                Assert.That(wrapper.transform.Find(socket.expectedTransformPath).name, Is.EqualTo(socket.socketName));
            }
            Assert.That(contract.visual.sockets.Select(s => s.socketName), Is.EquivalentTo(new[] { "WeaponMount", "ShieldMount", "HealAuraCenter" }));
            Assert.That(contract.visual.animations.Select(a => a.state), Is.EqualTo(ClipNames));
            foreach (AnimationRequirement a in contract.visual.animations)
                Assert.That(a.clip, Is.EqualTo(Clip(a.state)), a.state);
            Assert.That(contract.visual.effects.Select(e => e.asset), Has.All.Not.Null);
            Assert.That(contract.visual.effects.Select(e => e.asset), Does.Contain((Object)Load<GameObject>(EffectPath)));
            Assert.That(contract.visual.meshes.Select(m => m.role), Is.SupersetOf(new[] { "malha-fonte do corpo", "espada", "escudo" }));
            Assert.That(contract.visual.meshes.Select(m => m.asset), Has.All.Not.Null);
            Assert.That(contract.buildSettingsApproved, Is.False);
            Assert.That(EditorBuildSettings.scenes.Any(s => s.path.Contains("PaladinHealer")), Is.False);
        }

        [Test]
        public void CharacterData_IsRegisteredWithTheAncientTierAndReferencesTheWrapper()
        {
            PaladinHealerContract contract = Contract();
            Object data = AssetDatabase.LoadMainAssetAtPath("Assets/Resources/ScriptableObjects/Units/Characters/PaladinHealer.asset");
            Assert.That(data, Is.Not.Null);
            SerializedObject so = new SerializedObject(data);
            Assert.That(so.FindProperty("code").stringValue, Is.EqualTo("paladin_healer"));
            Assert.That(so.FindProperty("prefab").objectReferenceValue, Is.EqualTo(Load<GameObject>(WrapperPath)));
            PaladinEraTier ancient = contract.tiers[0];
            Assert.That(so.FindProperty("healthpoints").intValue, Is.EqualTo(ancient.healthpoints));
            Assert.That(so.FindProperty("attackDamage").intValue, Is.EqualTo(ancient.attackDamage));
            Assert.That(so.FindProperty("attackRate").floatValue, Is.EqualTo(ancient.attackCooldown).Within(0.001f));
        }

        [Test]
        public void Progression_GrowsAcrossTheFourEras()
        {
            PaladinEraTier[] tiers = Contract().tiers;
            Assert.That(tiers.Select(t => t.era), Is.EqualTo(ImperialEras.Ordered));
            for (int i = 1; i < tiers.Length; i++)
            {
                Assert.That(tiers[i].healthpoints, Is.GreaterThan(tiers[i - 1].healthpoints));
                Assert.That(tiers[i].attackDamage, Is.GreaterThan(tiers[i - 1].attackDamage));
                Assert.That(tiers[i].healAmount, Is.GreaterThan(tiers[i - 1].healAmount));
                Assert.That(tiers[i].healRadius, Is.GreaterThan(tiers[i - 1].healRadius));
            }
            Assert.That(tiers.All(t => t.attackRange <= AdvancedUnitRules.MeleeRangeCeiling), Is.True);
        }

        [Test]
        public void LicenseFile_DocumentsEveryThirdPartyOrigin()
        {
            string text = System.IO.File.ReadAllText("Docs/CharacterMeshAudit/PaladinHealer/LICENSE.md");
            Assert.That(text, Does.Contain("Meshy"));
            Assert.That(text, Does.Contain("Mixamo"));
            Assert.That(text, Does.Contain("proceduralmente"));
        }
    }
}
