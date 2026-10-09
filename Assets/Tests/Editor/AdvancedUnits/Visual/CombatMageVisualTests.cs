using System.Linq;
using EraImperial.Units.Advanced;
using EraImperial.Units.Mage;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace EraImperial.AdvancedUnits.Tests.Visual
{
    /// <summary>
    /// Verificacoes EditMode do visual do Mago de combate: wrapper/componentes/referencias, Avatar e Animator, materiais,
    /// LODs e o vinculo contrato -> assets. Este asmdef nao enxerga o Assembly-CSharp: componentes sao acessados por nome.
    /// </summary>
    public class CombatMageVisualTests
    {
        private const string Root = "Assets/CharacterRigging/CombatMage";
        private const string WrapperPath = "Assets/Resources/Prefabs/Units/Characters/CombatMage.prefab";
        private const string ContractPath = "Assets/Data/AdvancedUnits/CombatMage.asset";
        private const string ProjectilePath = "Assets/Data/AdvancedUnits/Projectiles/CombatMageBolt.asset";

        private static GameObject Wrapper => AssetDatabase.LoadAssetAtPath<GameObject>(WrapperPath);
        private static CombatMageContract Contract => AssetDatabase.LoadAssetAtPath<CombatMageContract>(ContractPath);
        private static GameObject Visual => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CombatMageVisual.prefab");

        private static AnimationClip Clip(string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(Root + "/Models/CombatMage@" + name + ".fbx")
                .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        // ---------------------------------------------------------------- prefab, componentes e referencias
        [Test]
        public void Wrapper_HasGameComponentsAndAdvancedComponents()
        {
            Assert.That(Wrapper, Is.Not.Null, "CombatMage.prefab");
            foreach (string component in new[] { "CharacterManager", "CharacterBT", "FogRendererToggler", "AdvancedUnitEraBinding",
                         "AdvancedUnitAnimatorDriver", "ProjectileAttackController", "AdvancedUnitDeathEffect" })
                Assert.That(Wrapper.GetComponent(component), Is.Not.Null, component);
            Assert.That(Wrapper.GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(Wrapper.GetComponent<UnityEngine.AI.NavMeshAgent>(), Is.Not.Null);
            Assert.That(Wrapper.transform.Find("Mesh/MinimapIcon"), Is.Not.Null, "caminho exigido por Unit");
            Assert.That(Wrapper.transform.Find("Mesh/CombatMageVisual"), Is.Not.Null);
            BoxCollider box = Wrapper.GetComponent<BoxCollider>();
            Assert.That(box.size.x, Is.LessThan(1f), "colisor ajustado a pegada humanoide");
            Assert.That(Wrapper.GetComponent<UnityEngine.AI.NavMeshAgent>().radius, Is.LessThan(0.5f));
        }

        [Test]
        public void Wrapper_ComponentReferencesPointToRealObjects()
        {
            var attack = new SerializedObject(Wrapper.GetComponent("ProjectileAttackController"));
            Assert.That(attack.FindProperty("contract").objectReferenceValue, Is.SameAs(Contract));
            Assert.That(attack.FindProperty("animator").objectReferenceValue, Is.Not.Null);
            var spawn = attack.FindProperty("projectileSpawn").objectReferenceValue as Transform;
            Assert.That(spawn, Is.Not.Null);
            Assert.That(spawn.name, Is.EqualTo("CastPoint"));
            Assert.That(spawn.IsChildOf(Wrapper.transform));
            float releaseDelay = attack.FindProperty("releaseDelay").floatValue;
            float eventTime = AnimationUtility.GetAnimationEvents(Clip("Cast")).First(e => e.functionName == "CombatMageRelease").time;
            Assert.That(releaseDelay, Is.EqualTo(eventTime).Within(0.05f), "releaseDelay = instante do disparo no clipe Cast");

            var era = new SerializedObject(Wrapper.GetComponent("AdvancedUnitEraBinding"));
            Assert.That(era.FindProperty("contract").objectReferenceValue, Is.SameAs(Contract));

            var death = new SerializedObject(Wrapper.GetComponent("AdvancedUnitDeathEffect"));
            var deathPrefab = death.FindProperty("deathPrefab").objectReferenceValue as GameObject;
            Assert.That(deathPrefab, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(deathPrefab), Is.EqualTo(Root + "/Prefabs/CombatMageDeath.prefab"));

            var manager = new SerializedObject(Wrapper.GetComponent("CharacterManager"));
            Assert.That(manager.FindProperty("meshRenderer").objectReferenceValue, Is.Not.Null, "anel do time");
            Assert.That(manager.FindProperty("animator").objectReferenceValue, Is.Not.Null);
        }

        [Test]
        public void Staff_IsSeparateAndAttachedToTheRightHand()
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(Visual);
            try
            {
                Animator animator = instance.GetComponent<Animator>();
                Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                Transform mount = instance.GetComponentsInChildren<Transform>(true).First(t => t.name == "StaffMount");
                Transform staff = instance.GetComponentsInChildren<Transform>(true).First(t => t.name == "Staff");
                Transform castPoint = instance.GetComponentsInChildren<Transform>(true).First(t => t.name == "CastPoint");
                Assert.That(mount.parent, Is.SameAs(hand));
                Assert.That(staff.IsChildOf(mount));
                Assert.That(castPoint.IsChildOf(staff));
                Assert.That(instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.GetComponentsInChildren<Transform>()).Any(t => t.name == "Staff"), Is.False,
                    "cajado nao faz parte da malha do corpo");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        // ---------------------------------------------------------------- avatar e animator
        [Test]
        public void Avatar_IsValidHumanoid_AndClipsShareIt()
        {
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(Root + "/Models/CombatMage.fbx").OfType<Avatar>().FirstOrDefault();
            Assert.That(avatar, Is.Not.Null);
            Assert.That(avatar.isValid && avatar.isHuman, Is.True);
            foreach (string name in new[] { "Idle", "Walk", "Run", "Cast", "Hit", "Death" })
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(Root + "/Models/CombatMage@" + name + ".fbx");
                Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human), name);
                Assert.That(importer.sourceAvatar, Is.SameAs(avatar), name);
                AnimationClip clip = Clip(name);
                Assert.That(clip, Is.Not.Null, name);
                Assert.That(clip.isHumanMotion, Is.True, name);
            }
        }

        [Test]
        public void Animator_HasStandardParametersAndTransitions()
        {
            Animator animator = Visual.GetComponent<Animator>();
            Assert.That(animator.avatar, Is.Not.Null);
            Assert.That(animator.applyRootMotion, Is.False);
            AnimatorController controller = (AnimatorController)animator.runtimeAnimatorController;
            Assert.That(controller, Is.Not.Null);
            var parameters = controller.parameters.ToDictionary(p => p.name, p => p.type);
            Assert.That(parameters["Speed"], Is.EqualTo(AnimatorControllerParameterType.Float));
            foreach (string trigger in new[] { "Fire", "Damaged", "Hit", "Heal" })
                Assert.That(parameters[trigger], Is.EqualTo(AnimatorControllerParameterType.Trigger), trigger);
            foreach (string flag in new[] { "Running", "Attacking" })
                Assert.That(parameters[flag], Is.EqualTo(AnimatorControllerParameterType.Bool), flag);
            AnimatorState locomotion = controller.layers[0].stateMachine.states.First(s => s.state.name == "Locomotion").state;
            Assert.That(locomotion.transitions.Any(t => t.destinationState.name == "Cast" && t.conditions.Any(c => c.parameter == "Fire")));
            Assert.That(locomotion.transitions.Any(t => t.destinationState.name == "Hit" && t.conditions.Any(c => c.parameter == "Damaged")));
            Assert.That(locomotion.motion, Is.InstanceOf<BlendTree>());
        }

        [Test]
        public void DeathPrefab_PlaysTheDeathClipByItself()
        {
            GameObject death = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/CombatMageDeath.prefab");
            AnimatorController controller = (AnimatorController)death.GetComponent<Animator>().runtimeAnimatorController;
            AnimatorState state = controller.layers[0].stateMachine.defaultState;
            Assert.That(state.name, Is.EqualTo("Death"));
            Assert.That(state.motion, Is.SameAs(Clip("Death")));
        }

        // ---------------------------------------------------------------- materiais
        [Test]
        public void AllRenderers_UseSupportedShaders_AndTexturesAreImported()
        {
            foreach (string path in new[] { WrapperPath, Root + "/Prefabs/CombatMageVisual.prefab", Root + "/Prefabs/CombatMageBolt.prefab",
                         Root + "/Prefabs/CombatMageImpact.prefab", Root + "/Prefabs/CombatMageCast.prefab", Root + "/Prefabs/CombatMageDeath.prefab" })
            {
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(root, Is.Not.Null, path);
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, path + "/" + renderer.name);
                        Assert.That(material.shader.isSupported, Is.True, material.name);
                        Assert.That(material.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"), material.name);
                        Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False, material.name + " " + material.shader.name);
                    }
            }
            Material body = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/CombatMage_Mat.mat");
            Assert.That(body.shader.name, Is.EqualTo("Standard"));
            Assert.That(body.mainTexture, Is.Not.Null);
            Assert.That(body.GetTexture("_BumpMap"), Is.Not.Null);
            Material orb = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/CombatMage_Orb.mat");
            Assert.That(orb.IsKeywordEnabled("_EMISSION"), Is.True);
            foreach (string texture in new[] { "CombatMage_BaseColor", "CombatMage_Normal", "CombatMage_FxGlow", "CombatMage_FxRing", "CombatMage_FxSpark", "CombatMage_FxStreak" })
                Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + texture + ".png"), Is.Not.Null, texture);
        }

        // ---------------------------------------------------------------- LODs
        [Test]
        public void Lods_AreSkinnedToTheSameRig_WithStrictlyDecreasingTriangles()
        {
            LODGroup group = Visual.GetComponentInChildren<LODGroup>();
            Assert.That(group, Is.Not.Null);
            Assert.That(group.lodCount, Is.EqualTo(3));
            LOD[] lods = group.GetLODs();
            Assert.That(lods[0].screenRelativeTransitionHeight, Is.GreaterThan(lods[1].screenRelativeTransitionHeight));
            Assert.That(lods[1].screenRelativeTransitionHeight, Is.GreaterThan(lods[2].screenRelativeTransitionHeight));
            SkinnedMeshRenderer[] renderers = Visual.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(r => r.name).ToArray();
            Assert.That(renderers.Length, Is.EqualTo(3));
            long[] triangles = renderers.Select(r => r.sharedMesh.triangles.Length / 3L).ToArray();
            Assert.That(triangles[0], Is.LessThanOrEqualTo(31000));
            Assert.That(triangles[1], Is.LessThan(triangles[0]));
            Assert.That(triangles[2], Is.LessThan(triangles[1]));
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                Assert.That(renderer.bones.Length, Is.GreaterThanOrEqualTo(22), renderer.name);
                Assert.That(renderer.sharedMaterial.name, Is.EqualTo("CombatMage_Mat"));
                BoneWeight[] weights = renderer.sharedMesh.boneWeights;
                Assert.That(weights.Count(w => Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1f) > 0.02f), Is.EqualTo(0), renderer.name + ": pesos normalizados");
            }
        }

        // ---------------------------------------------------------------- contrato <-> assets
        [Test]
        public void Contract_IsValidAndVisuallyReady_PointingToTheRealAssets()
        {
            CombatMageContract contract = Contract;
            Assert.That(contract.Validate().IsValid, Is.True, contract.Validate().ToString());
            Assert.That(contract.EvaluateVisuals().IsReady, Is.True, contract.EvaluateVisuals().ToString());
            Assert.That(contract.visual.prefab.asset, Is.SameAs(Wrapper));
            Assert.That(contract.visual.meshes.Length, Is.GreaterThanOrEqualTo(2));
            foreach (VisualAssetReference reference in contract.visual.meshes.Concat(contract.visual.effects))
            {
                Assert.That(reference.asset, Is.Not.Null, reference.role);
                Assert.That(AssetDatabase.GetAssetPath(reference.asset), Is.EqualTo(reference.expectedAssetPath), reference.role);
            }
            foreach (SocketRequirement socket in contract.visual.sockets)
                Assert.That(Wrapper.transform.Find(socket.expectedTransformPath), Is.Not.Null, socket.socketName);
            Assert.That(contract.visual.animations.Select(a => a.state), Is.EqualTo(new[] { "Idle", "Walk", "Run", "Cast", "Hit", "Death" }));
            foreach (AnimationRequirement animation in contract.visual.animations)
                Assert.That(animation.clip, Is.SameAs(Clip(animation.state)), animation.state);
            Assert.That(contract.visual.animations.First(a => a.state == "Cast").animatorParameter, Is.EqualTo("Fire"));
            Assert.That(contract.visual.animations.First(a => a.state == "Hit").animatorParameter, Is.EqualTo("Damaged"));
        }

        [Test]
        public void Projectile_IsSeparateAndLinkedToRealPrefabs()
        {
            ProjectileContract projectile = AssetDatabase.LoadAssetAtPath<ProjectileContract>(ProjectilePath);
            Assert.That(Contract.Projectile, Is.SameAs(projectile));
            Assert.That(projectile.EvaluateVisuals().IsReady, Is.True, projectile.EvaluateVisuals().ToString());
            GameObject bolt = projectile.prefab.asset as GameObject;
            Assert.That(AssetDatabase.GetAssetPath(bolt), Is.EqualTo(Root + "/Prefabs/CombatMageBolt.prefab"));
            Assert.That(bolt.GetComponent("AdvancedProjectile"), Is.Not.Null);
            Assert.That(bolt.GetComponentInChildren<TrailRenderer>(), Is.Not.Null, "rastro");
            Assert.That(bolt.GetComponentsInChildren<ParticleSystem>().Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(AssetDatabase.GetAssetPath(projectile.impactEffect.asset), Is.EqualTo(Root + "/Prefabs/CombatMageImpact.prefab"));
            Assert.That(Contract.visual.effects.Select(e => AssetDatabase.GetAssetPath(e.asset)),
                Is.EquivalentTo(new[] { Root + "/Prefabs/CombatMageCast.prefab", Root + "/Prefabs/CombatMageImpact.prefab" }));
        }

        [Test]
        public void CharacterData_IsRegisteredWithAncientStats_AndNotInBuildSettings()
        {
            ScriptableObject data = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Resources/ScriptableObjects/Units/Characters/CombatMage.asset");
            Assert.That(data, Is.Not.Null);
            var so = new SerializedObject(data);
            Assert.That(so.FindProperty("code").stringValue, Is.EqualTo(AdvancedUnitCodes.CombatMage));
            Assert.That(so.FindProperty("prefab").objectReferenceValue, Is.SameAs(Wrapper));
            Assert.That(contractTier().HealthPoints, Is.EqualTo(so.FindProperty("healthpoints").intValue));
            Assert.That(contractTier().AttackDamage, Is.EqualTo(so.FindProperty("attackDamage").intValue));
            Assert.That(EditorBuildSettings.scenes.Any(s => s.path.Contains("CombatMage")), Is.False);
        }

        private static IEraTier contractTier()
        {
            Contract.TryGetTier(ImperialEra.Ancient, out IEraTier tier);
            return tier;
        }
    }
}
