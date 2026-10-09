using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Crossbow;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EraImperial.AdvancedUnits.Tests
{
    /// <summary>
    /// Visual e integracao da Balestra (EditMode). O asmdef nao enxerga o Assembly-CSharp: componentes do jogo sao
    /// acessados por nome e por SerializedObject.
    /// </summary>
    public class CrossbowVisualTests
    {
        private const string Root = "Assets/CharacterRigging/Crossbow";
        private const string Wrapper = "Assets/Resources/Prefabs/Units/Characters/Crossbow.prefab";
        private const string Visual = Root + "/Prefabs/CrossbowVisual.prefab";
        private const string Death = Root + "/Prefabs/CrossbowDeath.prefab";
        private const string Bolt = Root + "/Prefabs/CrossbowBolt.prefab";
        private const string Impact = Root + "/Prefabs/CrossbowImpact.prefab";
        private const string Muzzle = Root + "/Prefabs/CrossbowMuzzle.prefab";
        private const string ContractPath = "Assets/Data/AdvancedUnits/Crossbow.asset";
        private const string BoltContractPath = "Assets/Data/AdvancedUnits/Projectiles/CrossbowBolt.asset";
        private static readonly string[] Clips = { "Idle", "Move", "Reload", "Fire", "Hit", "Destroy" };

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, "asset ausente: " + path);
            return asset;
        }

        private static CrossbowContract Contract() { return Load<CrossbowContract>(ContractPath); }

        [Test]
        public void WrapperPrefab_HasGameplayComponents_AndFootprint()
        {
            GameObject wrapper = Load<GameObject>(Wrapper);
            foreach (string name in new[] { "CharacterManager", "CharacterBT", "FogRendererToggler", "AdvancedUnitEraBinding", "AdvancedUnitAnimatorDriver", "ProjectileAttackController", "AdvancedUnitDeathEffect", "NavMeshAgent", "BoxCollider" })
                Assert.That(wrapper.GetComponent(name), Is.Not.Null, name);
            BoxCollider box = wrapper.GetComponent<BoxCollider>();
            Assert.That(box.size.x, Is.EqualTo(1.8f).Within(0.1f));
            Assert.That(box.size.z, Is.EqualTo(2.45f).Within(0.15f));
            Assert.That(wrapper.GetComponent<UnityEngine.AI.NavMeshAgent>().speed, Is.EqualTo(3.5f).Within(0.01f));
            Assert.That(wrapper.transform.Find("Mesh/MinimapIcon"), Is.Not.Null);
            Assert.That(wrapper.transform.Find("Mesh/TeamRing"), Is.Not.Null, "anel de time");
        }

        [Test]
        public void WrapperComponents_ReferenceContractAnimatorSocketAndDeathPrefab()
        {
            GameObject wrapper = Load<GameObject>(Wrapper);
            CrossbowContract contract = Contract();
            SerializedObject era = new SerializedObject(wrapper.GetComponent("AdvancedUnitEraBinding"));
            Assert.That(era.FindProperty("contract").objectReferenceValue, Is.SameAs(contract));
            SerializedObject attack = new SerializedObject(wrapper.GetComponent("ProjectileAttackController"));
            Assert.That(attack.FindProperty("contract").objectReferenceValue, Is.SameAs(contract));
            Assert.That(attack.FindProperty("animator").objectReferenceValue, Is.Not.Null);
            Transform spawn = (Transform)attack.FindProperty("projectileSpawn").objectReferenceValue;
            Assert.That(spawn, Is.Not.Null);
            Assert.That(spawn.name, Is.EqualTo("BoltSpawn"));
            Assert.That(spawn.IsChildOf(wrapper.transform));
            Assert.That(attack.FindProperty("releaseDelay").floatValue, Is.EqualTo(0.2f).Within(0.001f));
            SerializedObject death = new SerializedObject(wrapper.GetComponent("AdvancedUnitDeathEffect"));
            Assert.That(death.FindProperty("deathPrefab").objectReferenceValue, Is.SameAs(Load<GameObject>(Death)));
            SerializedObject driver = new SerializedObject(wrapper.GetComponent("AdvancedUnitAnimatorDriver"));
            Assert.That(driver.FindProperty("animator").objectReferenceValue, Is.SameAs(attack.FindProperty("animator").objectReferenceValue));
        }

        [Test]
        public void Animator_DeclaresStandardParameters_AndRequiredTransitions()
        {
            GameObject visual = Load<GameObject>(Visual);
            Animator animator = visual.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.applyRootMotion, Is.False);
            AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            foreach (string p in new[] { "Running", "Attacking", "Hit", "Speed", "Fire", "Damaged" })
                Assert.That(controller.parameters.Any(x => x.name == p), Is.True, p);
            AnimatorStateMachine m = controller.layers[0].stateMachine;
            AnimatorState State(string n) { return m.states.First(s => s.state.name == n).state; }
            Assert.That(State("Idle").transitions.Any(t => t.destinationState == State("Fire") && t.conditions.Any(c => c.parameter == "Fire")), Is.True, "Fire");
            Assert.That(State("Fire").transitions.Any(t => t.destinationState == State("Reload")), Is.True, "Fire -> Reload");
            Assert.That(State("Reload").transitions.Any(t => t.destinationState == State("Idle")), Is.True, "Reload -> Idle");
            Assert.That(State("Idle").transitions.Any(t => t.destinationState == State("Move") && t.conditions.Any(c => c.parameter == "Speed")), Is.True, "Speed -> Move");
            AnimatorStateMachine hit = controller.layers[1].stateMachine;
            Assert.That(hit.states.Any(s => s.state.name == "Empty" && s.state.transitions.Any(t => t.conditions.Any(c => c.parameter == "Damaged"))), Is.True, "Damaged -> Hit");
        }

        [Test]
        public void Clips_ExistAndFireHasReleaseEvent()
        {
            foreach (string name in Clips)
            {
                string path = Root + "/Models/Crossbow@" + name + ".fbx";
                AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                Assert.That(clip, Is.Not.Null, name);
                Assert.That(clip.length, Is.GreaterThan(0.3f), name);
            }
            AnimationClip fire = AssetDatabase.LoadAllAssetsAtPath(Root + "/Models/Crossbow@Fire.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            Assert.That(AnimationUtility.GetAnimationEvents(fire).Any(e => e.functionName == "CrossbowRelease"), Is.True);
        }

        [Test]
        public void Materials_AreValid_NoMagentaShaders()
        {
            foreach (string path in new[] { Visual, Wrapper, Death, Bolt, Impact, Muzzle })
            {
                GameObject root = Load<GameObject>(path);
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                    foreach (Material m in r.sharedMaterials)
                    {
                        Assert.That(m, Is.Not.Null, path + "/" + r.name);
                        Assert.That(m.shader, Is.Not.Null);
                        Assert.That(m.shader.isSupported, Is.True, m.name + " " + m.shader.name);
                        Assert.That(ShaderUtil.ShaderHasError(m.shader), Is.False, m.name);
                        Assert.That(m.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"));
                    }
            }
        }

        [Test]
        public void Lods_AreThreeStrictlyDecreasingWithinBudget()
        {
            GameObject visual = Load<GameObject>(Visual);
            LODGroup group = visual.GetComponent<LODGroup>();
            Assert.That(group, Is.Not.Null);
            LOD[] lods = group.GetLODs();
            Assert.That(lods.Length, Is.EqualTo(3));
            long[] tris = lods.Select(l => l.renderers.Sum(r => ((SkinnedMeshRenderer)r).sharedMesh.triangles.Length / 3L)).ToArray();
            Assert.That(tris[0], Is.LessThanOrEqualTo(8000));
            Assert.That(tris[1], Is.LessThanOrEqualTo(3000));
            Assert.That(tris[2], Is.LessThanOrEqualTo(1000));
            Assert.That(tris[0], Is.GreaterThan(tris[1]));
            Assert.That(tris[1], Is.GreaterThan(tris[2]));
            Assert.That(lods[0].screenRelativeTransitionHeight, Is.GreaterThan(lods[1].screenRelativeTransitionHeight));
            Assert.That(lods[1].screenRelativeTransitionHeight, Is.GreaterThan(lods[2].screenRelativeTransitionHeight));
            foreach (Renderer r in lods.SelectMany(l => l.renderers))
            {
                Mesh mesh = ((SkinnedMeshRenderer)r).sharedMesh;
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount), r.name + " UVs");
                Assert.That(mesh.boneWeights.All(w => Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f) < 0.02f), Is.True, r.name + " pesos");
            }
        }

        [Test]
        public void Contract_IsVisuallyReady_AndPointsToRealAssets()
        {
            CrossbowContract contract = Contract();
            Assert.That(contract.Validate().IsValid, Is.True, contract.Validate().ToString());
            Assert.That(contract.IsVisualReady, Is.True, contract.EvaluateVisuals().ToString());
            Assert.That(contract.visual.prefab.asset, Is.SameAs(Load<GameObject>(Wrapper)));
            Assert.That(contract.visual.animations.Select(a => a.state), Is.EqualTo(Clips));
            Assert.That(contract.visual.animations.All(a => a.clip != null), Is.True);
            foreach (SocketRequirement s in contract.visual.sockets)
                Assert.That(Load<GameObject>(Wrapper).transform.Find(s.expectedTransformPath), Is.Not.Null, s.socketName);
            Assert.That(contract.visual.sockets.Select(s => s.socketName), Is.EquivalentTo(new[] { "BoltSpawn", "WeaponMount" }));
            Assert.That(contract.visual.effects.All(e => e.asset != null), Is.True);
            Assert.That(contract.visual.effects.Select(e => e.asset), Does.Contain(Load<GameObject>(Muzzle)));
            Assert.That(contract.visual.effects.Select(e => e.asset), Does.Contain(Load<GameObject>(Death)));
            Assert.That(contract.buildSettingsApproved, Is.False);
        }

        [Test]
        public void Projectile_IsSeparate_PiercingAndBoundToRealPrefabs()
        {
            ProjectileContract bolt = Load<ProjectileContract>(BoltContractPath);
            Assert.That(Contract().Projectile, Is.SameAs(bolt));
            Assert.That(bolt.damageType, Is.EqualTo(DamageType.Piercing));
            Assert.That(bolt.ballistic, Is.False);
            Assert.That(bolt.prefab.asset, Is.SameAs(Load<GameObject>(Bolt)));
            Assert.That(bolt.impactEffect.asset, Is.SameAs(Load<GameObject>(Impact)));
            Assert.That(bolt.EvaluateVisuals().IsReady, Is.True);
            GameObject prefab = Load<GameObject>(Bolt);
            Assert.That(prefab.GetComponentInChildren<SkinnedMeshRenderer>(true), Is.Null, "virote nao e parte da malha da unidade");
            Assert.That(prefab.GetComponentsInChildren<MeshRenderer>(true).Length, Is.GreaterThan(0));
            Assert.That(Load<GameObject>(Impact).GetComponentsInChildren<ParticleSystem>(true).Length, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void ContractTiers_ProgressAcrossFourEras()
        {
            CrossbowEraTier[] t = Contract().tiers;
            Assert.That(t.Select(x => x.era), Is.EqualTo(ImperialEras.Ordered));
            for (int i = 1; i < t.Length; i++)
            {
                Assert.That(t[i].healthpoints, Is.GreaterThan(t[i - 1].healthpoints));
                Assert.That(t[i].attackDamage, Is.GreaterThan(t[i - 1].attackDamage));
                Assert.That(t[i].attackRange, Is.GreaterThanOrEqualTo(t[i - 1].attackRange));
                Assert.That(t[i].attackCooldown, Is.LessThanOrEqualTo(t[i - 1].attackCooldown));
                Assert.That(t[i].armorPenetration, Is.GreaterThan(t[i - 1].armorPenetration));
            }
        }

        [Test]
        public void CharacterData_IsRegisteredOnlyWithTheRealWrapper()
        {
            ScriptableObject data = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/ScriptableObjects/Units/Characters/Crossbow.asset");
            Assert.That(data, Is.Not.Null);
            SerializedObject so = new SerializedObject(data);
            Assert.That(so.FindProperty("code").stringValue, Is.EqualTo("crossbow"));
            Assert.That(so.FindProperty("prefab").objectReferenceValue, Is.SameAs(Load<GameObject>(Wrapper)));
            Assert.That(so.FindProperty("healthpoints").intValue, Is.EqualTo(Contract().tiers[0].healthpoints));
            Assert.That(so.FindProperty("attackRange").floatValue, Is.EqualTo(Contract().tiers[0].attackRange).Within(0.001f));
        }
    }
}
