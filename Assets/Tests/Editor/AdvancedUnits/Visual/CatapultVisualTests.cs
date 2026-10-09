using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Siege;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EraImperial.AdvancedUnits.Tests
{
    /// <summary>
    /// Visual e integracao da Catapulta (EditMode). O asmdef de testes nao enxerga o Assembly-CSharp: componentes do jogo sao
    /// acessados por nome e por SerializedObject.
    /// </summary>
    public class CatapultVisualTests
    {
        private const string Root = "Assets/CharacterRigging/Catapult";
        private const string WrapperPath = "Assets/Resources/Prefabs/Units/Characters/Catapult.prefab";
        private const string VisualPath = Root + "/Prefabs/CatapultVisual.prefab";
        private const string DeathPath = Root + "/Prefabs/CatapultDestroyed.prefab";
        private const string BoulderPath = Root + "/Prefabs/CatapultBoulder.prefab";
        private const string FirePath = Root + "/Prefabs/CatapultFire.prefab";
        private const string ImpactPath = Root + "/Prefabs/CatapultImpact.prefab";
        private const string ContractPath = "Assets/Data/AdvancedUnits/Catapult.asset";
        private static readonly string[] Clips = { "Idle", "Move", "Reload", "Fire", "Hit", "Destroy" };

        private static GameObject Load(string path)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(go, Is.Not.Null, path);
            return go;
        }

        private static Transform Find(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        private static AnimationClip Clip(string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath($"{Root}/Models/Catapult@{name}.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        }

        private static CatapultContract Contract()
        {
            CatapultContract c = AssetDatabase.LoadAssetAtPath<CatapultContract>(ContractPath);
            Assert.That(c, Is.Not.Null);
            return c;
        }

        [Test]
        public void Wrapper_HasEveryRequiredComponent_WithRealReferences()
        {
            GameObject wrapper = Load(WrapperPath);
            foreach (string component in new[] { "CharacterManager", "CharacterBT", "AdvancedUnitEraBinding", "AdvancedUnitAnimatorDriver", "CatapultCombatController", "AdvancedUnitDeathEffect" })
                Assert.That(wrapper.GetComponent(component), Is.Not.Null, component);

            BoxCollider box = wrapper.GetComponent<BoxCollider>();
            Assert.That(box.size.z, Is.GreaterThan(3f));
            Assert.That(box.size.x, Is.GreaterThan(2f));

            UnityEngine.AI.NavMeshAgent agent = wrapper.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Assert.That(agent.speed, Is.EqualTo(2.5f).Within(0.01f), "catapulta lenta");
            Assert.That(agent.radius, Is.GreaterThanOrEqualTo(0.9f));

            SerializedObject combat = new SerializedObject(wrapper.GetComponent("CatapultCombatController"));
            Assert.That(combat.FindProperty("contract").objectReferenceValue, Is.EqualTo(Contract()));
            Assert.That(combat.FindProperty("animator").objectReferenceValue, Is.Not.Null);
            Assert.That(combat.FindProperty("projectileSpawn").objectReferenceValue, Is.Not.Null);
            Assert.That(combat.FindProperty("releaseDelay").floatValue, Is.GreaterThan(0.2f));
            Assert.That(combat.FindProperty("sourceHeight").floatValue, Is.GreaterThan(0.5f));

            SerializedObject death = new SerializedObject(wrapper.GetComponent("AdvancedUnitDeathEffect"));
            Assert.That(death.FindProperty("deathPrefab").objectReferenceValue, Is.EqualTo(Load(DeathPath)));
            Assert.That(new SerializedObject(wrapper.GetComponent("AdvancedUnitEraBinding")).FindProperty("contract").objectReferenceValue, Is.EqualTo(Contract()));

            SerializedObject manager = new SerializedObject(wrapper.GetComponent("CharacterManager"));
            Assert.That(manager.FindProperty("meshRenderer").objectReferenceValue, Is.Not.Null, "anel do time");
            Assert.That(manager.FindProperty("animator").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void Rig_HasTheGenericSkeleton_AndTheRealSockets()
        {
            GameObject visual = Load(VisualPath);
            foreach (string bone in new[] { "Root", "Arm", "Wheel_FL", "Wheel_FR", "Wheel_BL", "Wheel_BR", "Winch" })
                Assert.That(Find(visual, bone), Is.Not.Null, bone);
            Transform spawn = Find(visual, "ProjectileSpawn");
            Assert.That(spawn, Is.Not.Null);
            Assert.That(spawn.IsChildOf(Find(visual, "Arm")), "ProjectileSpawn acompanha o braco");
            Animator animator = visual.GetComponent<Animator>();
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.avatar.isValid && !animator.avatar.isHuman, "avatar Generic");
            Assert.That(animator.applyRootMotion, Is.False);
        }

        [Test]
        public void Animator_HasStandardParameters_AndTheRequestedFlow()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "/Animation/Catapult.controller");
            Assert.That(controller, Is.Not.Null);
            foreach (string p in new[] { "Running", "Attacking", "Hit", "Speed", "Fire", "Damaged" })
                Assert.That(controller.parameters.Any(x => x.name == p), Is.True, p);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState State(string n) { return machine.states.First(s => s.state.name == n).state; }
            Assert.That(machine.defaultState.name, Is.EqualTo("Idle"));
            Assert.That(State("Idle").transitions.Any(t => t.destinationState == State("Fire") && t.conditions.Any(c => c.parameter == "Fire")));
            Assert.That(State("Fire").transitions.Any(t => t.destinationState == State("Reload")));
            Assert.That(State("Reload").transitions.Any(t => t.destinationState == State("Idle")));
            Assert.That(State("Idle").transitions.Any(t => t.destinationState == State("Move") && t.conditions.Any(c => c.parameter == "Speed")));
            Assert.That(State("Idle").transitions.Any(t => t.destinationState == State("Hit") && t.conditions.Any(c => c.parameter == "Damaged")));
            foreach (string n in new[] { "Idle", "Move", "Reload", "Fire", "Hit" })
                Assert.That(State(n).motion, Is.EqualTo(Clip(n)), n);

            Animator death = Load(DeathPath).GetComponent<Animator>();
            AnimatorController deathController = (AnimatorController)death.runtimeAnimatorController;
            Assert.That(deathController.layers[0].stateMachine.defaultState.motion, Is.EqualTo(Clip("Destroy")), "morte toca Destroy sozinha");
        }

        [Test]
        public void Clips_AreImported_WithFireReleaseEvent()
        {
            foreach (string name in Clips)
            {
                AnimationClip clip = Clip(name);
                Assert.That(clip.length, Is.GreaterThan(0.3f), name);
                Assert.That(clip.isHumanMotion, Is.False, name);
            }
            Assert.That(Clip("Idle").isLooping && Clip("Move").isLooping, Is.True);
            AnimationEvent release = AnimationUtility.GetAnimationEvents(Clip("Fire")).FirstOrDefault(e => e.functionName == "CatapultRelease");
            Assert.That(release, Is.Not.Null);
            SerializedObject combat = new SerializedObject(Load(WrapperPath).GetComponent("CatapultCombatController"));
            Assert.That(combat.FindProperty("releaseDelay").floatValue, Is.EqualTo(release.time).Within(0.05f), "releaseDelay = instante em que a pedra sai");
        }

        [Test]
        public void Lods_AreStrictlyDecreasing_AndWithinBudget()
        {
            GameObject visual = Load(VisualPath);
            LODGroup group = visual.GetComponentInChildren<LODGroup>();
            Assert.That(group.lodCount, Is.EqualTo(3));
            SkinnedMeshRenderer[] smrs = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(r => r.name).ToArray();
            Assert.That(smrs.Length, Is.EqualTo(3));
            int[] tris = smrs.Select(r => r.sharedMesh.triangles.Length / 3).ToArray();
            Assert.That(tris[0], Is.LessThanOrEqualTo(8000));
            Assert.That(tris[1], Is.LessThanOrEqualTo(3000));
            Assert.That(tris[2], Is.LessThanOrEqualTo(1000));
            Assert.That(tris[0], Is.GreaterThan(tris[1]));
            Assert.That(tris[1], Is.GreaterThan(tris[2]));
            LOD[] lods = group.GetLODs();
            Assert.That(lods[0].screenRelativeTransitionHeight, Is.GreaterThan(lods[1].screenRelativeTransitionHeight));
            Assert.That(lods[1].screenRelativeTransitionHeight, Is.GreaterThan(lods[2].screenRelativeTransitionHeight));
            foreach (SkinnedMeshRenderer smr in smrs)
            {
                Assert.That(smr.sharedMesh.boneWeights.Count(w => Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f) > 0.02f), Is.EqualTo(0), smr.name + " pesos normalizados");
                Assert.That(smr.sharedMesh.uv.Length, Is.EqualTo(smr.sharedMesh.vertexCount), smr.name + " UVs");
            }
        }

        [Test]
        public void Materials_AreStandardOrParticleShaders_WithoutErrors()
        {
            foreach (string path in new[] { WrapperPath, VisualPath, DeathPath, BoulderPath, FirePath, ImpactPath })
            {
                foreach (Renderer renderer in Load(path).GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, path + "/" + renderer.name);
                        Assert.That(material.shader.isSupported && !ShaderUtil.ShaderHasError(material.shader), Is.True, material.name);
                        Assert.That(material.shader.name, Does.Not.Contain("Hidden/InternalErrorShader"));
                    }
            }
            foreach (SkinnedMeshRenderer smr in Load(VisualPath).GetComponentsInChildren<SkinnedMeshRenderer>(true))
                foreach (Material material in smr.sharedMaterials)
                {
                    Assert.That(material.shader.name, Is.EqualTo("Standard"));
                    Assert.That(material.mainTexture, Is.Not.Null, material.name);
                }
        }

        [Test]
        public void Contract_IsVisualReady_AndBoundToRealAssets()
        {
            CatapultContract contract = Contract();
            Assert.That(contract.Validate().IsValid, Is.True, contract.Validate().ToString());
            Assert.That(contract.EvaluateVisuals().IsReady, Is.True, contract.EvaluateVisuals().ToString());
            GameObject wrapper = Load(WrapperPath);
            Assert.That(contract.visual.prefab.asset, Is.EqualTo(wrapper));
            foreach (SocketRequirement s in contract.visual.sockets) Assert.That(s.IsBoundTo(wrapper), Is.True, s.socketName);
            Assert.That(contract.visual.sockets.Select(s => s.socketName), Is.SupersetOf(new[] { "ProjectileSpawn", "ArmPivot" }));
            Assert.That(contract.visual.animations.Select(a => a.state), Is.SupersetOf(Clips));
            foreach (AnimationRequirement a in contract.visual.animations) Assert.That(a.clip, Is.EqualTo(Clip(a.state)), a.state);
            Assert.That(contract.visual.effects.Select(e => e.role).Any(r => r.Contains("disparo")), Is.True);
            Assert.That(contract.visual.effects.Select(e => e.role).Any(r => r.Contains("impacto")), Is.True);
            foreach (VisualAssetReference e in contract.visual.effects) Assert.That(e.asset, Is.Not.Null, e.role);
            Assert.That(contract.visual.meshes.All(m => m.asset != null), Is.True);
        }

        [Test]
        public void Projectile_IsSeparate_Ballistic_AndHasNoRequiredScripts()
        {
            ProjectileContract projectile = Contract().Projectile;
            Assert.That(projectile.EvaluateVisuals().IsReady, Is.True);
            Assert.That(projectile.prefab.asset, Is.EqualTo(Load(BoulderPath)));
            Assert.That(projectile.impactEffect.asset, Is.EqualTo(Load(ImpactPath)));
            Assert.That(projectile.ballistic && projectile.arcHeight > 0f, Is.True);
            GameObject boulder = Load(BoulderPath);
            Assert.That(boulder.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty, "AdvancedProjectile e adicionado em runtime");
            Mesh mesh = boulder.GetComponentInChildren<MeshFilter>().sharedMesh;
            Assert.That(mesh.bounds.size.x, Is.InRange(0.33f, 0.47f), "pedra de ~0,4 m");
            Assert.That(Load(WrapperPath).GetComponentsInChildren<MeshFilter>(true).Any(f => f.sharedMesh == mesh), Is.False, "pedra nao faz parte do wrapper");
            Assert.That(Load(FirePath).GetComponentsInChildren<ParticleSystem>(true).Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(Load(ImpactPath).GetComponentsInChildren<ParticleSystem>(true).Length, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void CharacterData_IsRegistered_WithAncientStats_AndProgressionHoldsAcrossFourEras()
        {
            CatapultContract contract = Contract();
            ScriptableObject data = AssetDatabase.LoadMainAssetAtPath("Assets/Resources/ScriptableObjects/Units/Characters/Catapult.asset") as ScriptableObject;
            Assert.That(data, Is.Not.Null, "CharacterData registrado so porque o contrato e valido e o visual e real");
            SerializedObject so = new SerializedObject(data);
            UnitDataFields ancient = contract.BuildUnitDataFields(ImperialEra.Ancient);
            Assert.That(so.FindProperty("code").stringValue, Is.EqualTo("catapult"));
            Assert.That(so.FindProperty("healthpoints").intValue, Is.EqualTo(ancient.healthpoints));
            Assert.That(so.FindProperty("attackDamage").intValue, Is.EqualTo(ancient.attackDamage));
            Assert.That(so.FindProperty("prefab").objectReferenceValue, Is.EqualTo(Load(WrapperPath)));

            var tiers = contract.GetTiers();
            Assert.That(tiers.Select(t => t.Era), Is.EqualTo(ImperialEras.Ordered));
            for (int i = 1; i < tiers.Count; i++)
            {
                Assert.That(tiers[i].HealthPoints, Is.GreaterThan(tiers[i - 1].HealthPoints));
                Assert.That(tiers[i].AttackDamage, Is.GreaterThan(tiers[i - 1].AttackDamage));
                Assert.That(tiers[i].AttackRange, Is.GreaterThanOrEqualTo(tiers[i - 1].AttackRange));
                Assert.That(tiers[i].AttackCooldown, Is.LessThanOrEqualTo(tiers[i - 1].AttackCooldown));
            }
        }

        [Test]
        public void Model_IsProceduralAndLicenceIsDocumented()
        {
            string license = System.IO.File.ReadAllText("Docs/CharacterMeshAudit/Catapult/LICENSE.md");
            Assert.That(license, Does.Contain("obra do proprio projeto"));
            Assert.That(license, Does.Contain("BuildCatapult.py"));
            Assert.That(System.IO.File.Exists("Tools/CharacterRigging/BuildCatapult.py"), Is.True);
            Assert.That(System.IO.Directory.Exists("Tools/CharacterRigging/Inputs/Catapult"), Is.False, "sem entradas de terceiros");
        }
    }
}
