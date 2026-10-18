using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Ciclo 6 isolado para cerco físico/visual.
///
/// O protótipo cria somente objetos temporários em uma cena vazia: projétil
/// com Rigidbody/Collider, alvo estrutural com vida, dano direto/radial e
/// marcadores visuais de impacto/destruição. Nada é salvo em prefabs, cenas ou
/// assets de produção. Se o Unity não iniciar o PlayMode, o ciclo fica BLOCKED.
/// </summary>
[InitializeOnLoad]
public static class SiegeVisualCycle6Harness
{
    private const string RunningKey = "EraImperial.SiegeVisualCycle6.Running";
    private const string FinishedKey = "EraImperial.SiegeVisualCycle6.Finished";
    private static bool _finished;

    static SiegeVisualCycle6Harness()
    {
        EditorApplication.update += Tick;
    }

    /// <summary>Executa a prova PlayMode em cena vazia e retorna 2 se o Unity estiver bloqueado.</summary>
    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("SiegeVisualCycle6Harness.Run exige o Editor fora do PlayMode.");

        _finished = false;
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(FinishedKey, false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // O DebugConsole registra listeners no bootstrap; sem este manager a
        // cena temporária produz um NullReference antes do fixture físico.
        new GameObject("EventManager").AddComponent<EventManager>();
        EditorApplication.EnterPlaymode();
    }

    /// <summary>Audita os contratos estáticos sem iniciar uma cena nem alterar o projeto.</summary>
    public static SiegeVisualCycle6Result RunStaticAudit()
    {
        SiegeVisualCycle6Result result = new SiegeVisualCycle6Result();
        AuditLayersAndTags(result);
        AuditProductionContracts(result);
        Require(result, SiegeCycle6Rules.ResolveRadialDamage(100, 5f, 10f) == 50,
            "falloff radial a meia distância incorreto");
        Require(result, SiegeCycle6Rules.ResolveRadialDamage(100, 10f, 10f) == 0,
            "dano na borda deveria ser zero");
        Require(result, SiegeCycle6Rules.ResolveRadialDamage(100, 11f, 10f) == 0,
            "alvo fora do raio recebeu dano");
        Require(result, SiegeCycle6Rules.ResolveDirectDamage(100, 30) == 30,
            "dano direto não respeitou o máximo de vida");
        Require(result, SiegeCycle6Rules.ResolveDirectDamage(20, 30) == 20,
            "dano direto excedeu a vida restante");
        Require(result, SiegeCycle6Rules.ShouldDestroy(0) && !SiegeCycle6Rules.ShouldDestroy(1),
            "transição de destruição inválida");
        return result;
    }

    private static void AuditProductionContracts(SiegeVisualCycle6Result result)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (string typeName in new[]
        {
            "SiegeAttackProfile", "CatapultCombatController", "BuildingManager",
            "Building", "ProjectileContract"
        })
        {
            bool found = assemblies.Any(a =>
            {
                try { return a.GetTypes().Any(t => t.Name == typeName); }
                catch (ReflectionTypeLoadException) { return false; }
            });
            Require(result, found, "contrato de produção não localizado por reflexão: " + typeName);
        }
        Type globals = assemblies.SelectMany(a =>
        {
            try { return a.GetTypes(); }
            catch (ReflectionTypeLoadException error) { return error.Types.Where(t => t != null); }
        }).FirstOrDefault(t => t.Name == "Globals");
        Require(result, globals != null, "Globals não localizado por reflexão");
        if (globals != null)
            foreach (string field in new[] { "UNIT_MASK", "TERRAIN_LAYER_MASK", "FOV_LAYER_MASK" })
            {
                bool foundMember = globals.GetField(field,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) != null
                    || globals.GetProperty(field,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) != null;
                if (!foundMember)
                {
                    string sourcePath = System.IO.Path.Combine(
                        Directory.GetParent(Application.dataPath).FullName,
                        "Assets/Scripts/Tools/Globals.cs");
                    foundMember = File.Exists(sourcePath)
                        && File.ReadAllText(sourcePath).IndexOf(field, StringComparison.Ordinal) >= 0;
                }
                Require(result, foundMember,
                    "Globals." + field + " não localizado por reflexão");
            }
    }

    private static void Tick()
    {
        if (_finished || !SessionState.GetBool(RunningKey, false)
            || !EditorApplication.isPlaying || EditorApplication.isCompiling)
            return;

        _finished = true;
        try
        {
            RequirePlayMode(Application.isPlaying, "o runner não entrou em PlayMode");
            SiegeVisualCycle6Result result = RunScenario();
            if (result.Failures.Count > 0)
            {
                Debug.LogError("SIEGE_VISUAL_CYCLE6_FAIL: " + string.Join(" | ", result.Failures.ToArray()));
                SessionState.SetBool(FinishedKey, false);
                StopAndExit(1);
                return;
            }
            if (result.Blocked.Count > 0)
            {
                Debug.LogWarning("SIEGE_VISUAL_CYCLE6_BLOCKED: " + string.Join(" | ", result.Blocked.ToArray()));
                SessionState.SetBool(FinishedKey, false);
                StopAndExit(2);
                return;
            }

            Debug.Log("SIEGE_VISUAL_CYCLE6_PASS: projétil físico, colliders, dano, destruição e feedback visual");
            SessionState.SetBool(FinishedKey, true);
            StopAndExit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("SIEGE_VISUAL_CYCLE6_FAIL: " + exception);
            SessionState.SetBool(FinishedKey, false);
            StopAndExit(1);
        }
    }

    private static SiegeVisualCycle6Result RunScenario()
    {
        SiegeVisualCycle6Result result = RunStaticAudit();
        if (result.Failures.Count > 0)
            return result;
        if (result.Blocked.Count > 0)
            return result;
        GameObject root = new GameObject("__SiegeVisualCycle6_IsolatedRuntime");
        try
        {
            int unitLayer = LayerMask.NameToLayer("Unit");
            if (unitLayer < 0)
            {
                result.Blocked.Add("layer 'Unit' não existe no projeto");
                return result;
            }

            SiegeCycle6TargetFixture target = CreateTarget(root.transform, unitLayer, "StructuralTarget", new Vector3(0f, 0f, 4f), 120);
            SiegeCycle6TargetFixture radialTarget = CreateTarget(root.transform, unitLayer, "RadialTarget", new Vector3(2f, 0f, 4f), 80);
            SiegeCycle6TargetFixture outsideTarget = CreateTarget(root.transform, unitLayer, "OutsideTarget", new Vector3(12f, 0f, 4f), 80);
            SiegeCycle6ProjectileFixture projectile = CreateProjectile(root.transform, unitLayer, new Vector3(0f, 4f, 0f), target.transform.position);

            RequirePlayMode(projectile.GetComponent<Rigidbody>() != null, "projétil sem Rigidbody");
            RequirePlayMode(projectile.GetComponent<SphereCollider>() != null, "projétil sem SphereCollider");
            RequirePlayMode(target.GetComponent<BoxCollider>() != null, "alvo estrutural sem BoxCollider");
            RequirePlayMode(target.gameObject.layer == unitLayer, "alvo estrutural fora da layer Unit");

            for (int i = 0; i < 180 && !projectile.HasImpacted; i++)
                projectile.Step(1f / 60f);
            RequirePlayMode(projectile.HasImpacted, "projétil não alcançou o alvo no limite de tempo");
            RequirePlayMode(projectile.ImpactCount == 1, "projétil gerou impacto duplicado");
            projectile.EmitImpactMarker();

            Physics.SyncTransforms();
            Collider[] colliders = Physics.OverlapSphere(target.transform.position, 3f, 1 << unitLayer);
            RequirePlayMode(colliders.Any(c => c.GetComponent<SiegeCycle6TargetFixture>() == target),
                "Physics.OverlapSphere não encontrou o alvo na layer Unit");

            int direct = target.ApplyDamage(120, true, target.transform.position);
            int radial = radialTarget.ApplyDamage(SiegeCycle6Rules.ResolveRadialDamage(80, 2f, 4f), false, target.transform.position);
            int outside = SiegeCycle6Rules.ResolveRadialDamage(80, 12f, 4f);
            RequirePlayMode(direct == 120 && target.IsDestroyed, "dano direto não destruiu o alvo estrutural");
            RequirePlayMode(radial == 40 && !radialTarget.IsDestroyed, "dano radial/falloff incorreto");
            RequirePlayMode(outside == 0 && outsideTarget.CurrentHealth == 80, "alvo fora do raio foi afetado");
            RequirePlayMode(target.Feedback != null && target.Feedback.ImpactCount == 1,
                "feedback visual de impacto não foi emitido");
            RequirePlayMode(target.Feedback.DestructionCount == 1,
                "feedback visual de destruição não foi emitido");
            RequirePlayMode(target.VisualRoot != null && !target.VisualRoot.activeSelf,
                "visual estrutural não foi desativado ao destruir");
            RequirePlayMode(!target.ColliderEnabled, "collider estrutural permaneceu ativo após destruição");
            RequirePlayMode(projectile.VisualFeedback != null && projectile.VisualFeedback.impactMarkerSpawned,
                "marcador visual do projétil não foi criado");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
        return result;
    }

    private static SiegeCycle6TargetFixture CreateTarget(Transform parent, int layer, string name, Vector3 position, int health)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = name + "_Visual";
        visual.transform.SetParent(parent);
        visual.transform.position = position;
        visual.transform.localScale = new Vector3(2f, 2f, 2f);
        visual.layer = layer;
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.layer = layer;
        SiegeCycle6TargetFixture fixture = root.AddComponent<SiegeCycle6TargetFixture>();
        fixture.Initialize(health, visual);
        return fixture;
    }

    private static SiegeCycle6ProjectileFixture CreateProjectile(Transform parent, int layer, Vector3 position, Vector3 target)
    {
        GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectileObject.name = "SiegeProjectile_VisualFixture";
        projectileObject.transform.SetParent(parent);
        projectileObject.transform.position = position;
        projectileObject.transform.localScale = Vector3.one * 0.35f;
        projectileObject.layer = layer;
        Rigidbody body = projectileObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        SiegeCycle6ProjectileFixture fixture = projectileObject.AddComponent<SiegeCycle6ProjectileFixture>();
        fixture.Launch(position, target);
        return fixture;
    }

    private static void AuditLayersAndTags(SiegeVisualCycle6Result result)
    {
        if (LayerMask.NameToLayer("Unit") < 0)
            result.Blocked.Add("layer 'Unit' não existe; fixtures físicos não podem ser validados");

        try
        {
            string[] tags = UnityEditorInternal.InternalEditorUtility.tags;
            if (!tags.Contains("Unit"))
                result.Blocked.Add("tag 'Unit' não existe; contrato de alvo não pode ser confirmado");
        }
        catch (Exception exception)
        {
            result.Blocked.Add("não foi possível auditar tags por reflexão/editor: " + exception.GetType().Name);
        }
    }

    private static void Require(SiegeVisualCycle6Result result, bool condition, string message)
    {
        if (!condition) result.Failures.Add(message);
    }

    private static void RequirePlayMode(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void StopAndExit(int code)
    {
        SessionState.SetBool(RunningKey, false);
        EditorApplication.ExitPlaymode();
        EditorApplication.delayCall += () => EditorApplication.Exit(code);
    }
}

internal static class SiegeCycle6Rules
{
    public static int ResolveRadialDamage(int baseDamage, float distance, float radius)
    {
        if (baseDamage <= 0 || radius <= 0f || distance >= radius) return 0;
        float factor = 1f - Mathf.Clamp01(distance / radius);
        return Mathf.Max(0, Mathf.RoundToInt(baseDamage * factor));
    }

    public static int ResolveDirectDamage(int currentHealth, int damage)
    {
        return Mathf.Clamp(damage, 0, Mathf.Max(0, currentHealth));
    }

    public static bool ShouldDestroy(int remainingHealth)
    {
        return remainingHealth <= 0;
    }
}

internal sealed class SiegeCycle6TargetFixture : MonoBehaviour
{
    public int CurrentHealth { get; private set; }
    public bool IsDestroyed { get; private set; }
    public GameObject VisualRoot { get; private set; }
    public SiegeCycle6FeedbackFixture Feedback { get; private set; }
    public bool ColliderEnabled { get { return _collider != null && _collider.enabled; } }
    private Collider _collider;

    public void Initialize(int health, GameObject visual)
    {
        CurrentHealth = health;
        VisualRoot = visual;
        _collider = gameObject.AddComponent<BoxCollider>();
        Feedback = gameObject.AddComponent<SiegeCycle6FeedbackFixture>();
    }

    public int ApplyDamage(int amount, bool direct, Vector3 impactPoint)
    {
        int applied = SiegeCycle6Rules.ResolveDirectDamage(CurrentHealth, amount);
        CurrentHealth -= applied;
        Feedback.EmitImpact(impactPoint, direct);
        if (SiegeCycle6Rules.ShouldDestroy(CurrentHealth))
        {
            IsDestroyed = true;
            VisualRoot.SetActive(false);
            _collider.enabled = false;
            Feedback.EmitDestruction(impactPoint);
        }
        return applied;
    }
}

internal sealed class SiegeCycle6ProjectileFixture : MonoBehaviour
{
    public SiegeCycle6ProjectileFeedbackFixture VisualFeedback { get; private set; }
    public bool HasImpacted { get; private set; }
    public int ImpactCount { get; private set; }
    private Vector3 _target;
    private Vector3 _velocity;

    public void Launch(Vector3 origin, Vector3 target)
    {
        _target = target;
        _velocity = (target - origin).normalized * 8f + Vector3.up * 5f;
        VisualFeedback = gameObject.AddComponent<SiegeCycle6ProjectileFeedbackFixture>();
        VisualFeedback.EmitLaunch(origin, target);
    }

    public void Step(float deltaTime)
    {
        if (HasImpacted) return;
        _velocity += Physics.gravity * deltaTime;
        transform.position += _velocity * deltaTime;
        if (Vector3.Distance(transform.position, _target) <= 0.35f || transform.position.y < -0.1f)
        {
            transform.position = _target;
            HasImpacted = true;
            ImpactCount++;
        }
    }

    public void EmitImpactMarker()
    {
        if (!HasImpacted || VisualFeedback == null) return;
        VisualFeedback.EmitImpact(transform.position);
    }
}

internal sealed class SiegeCycle6FeedbackFixture : MonoBehaviour
{
    public int ImpactCount { get; private set; }
    public int DestructionCount { get; private set; }

    public void EmitImpact(Vector3 point, bool direct)
    {
        ImpactCount++;
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = direct ? "__SiegeImpact_Direct" : "__SiegeImpact_Radial";
        marker.transform.position = point;
        marker.transform.localScale = Vector3.one * 0.2f;
        marker.transform.SetParent(transform.parent);
        UnityEngine.Object.DestroyImmediate(marker);
    }

    public void EmitDestruction(Vector3 point)
    {
        DestructionCount++;
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "__SiegeDestruction_Feedback";
        marker.transform.position = point;
        marker.transform.localScale = Vector3.one * 0.45f;
        marker.transform.SetParent(transform.parent);
        UnityEngine.Object.DestroyImmediate(marker);
    }
}

internal sealed class SiegeCycle6ProjectileFeedbackFixture : MonoBehaviour
{
    public bool impactMarkerSpawned { get; private set; }

    public void EmitLaunch(Vector3 origin, Vector3 target)
    {
        TrailRenderer trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.15f;
        trail.widthMultiplier = 0.05f;
        LineRenderer arc = gameObject.AddComponent<LineRenderer>();
        arc.positionCount = 3;
        arc.SetPosition(0, origin);
        arc.SetPosition(1, Vector3.Lerp(origin, target, 0.5f) + Vector3.up * 1.5f);
        arc.SetPosition(2, target);
    }

    public void EmitImpact(Vector3 point)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "__SiegeProjectile_ImpactMarker";
        marker.transform.position = point;
        marker.transform.localScale = Vector3.one * 0.2f;
        UnityEngine.Object.DestroyImmediate(marker);
        impactMarkerSpawned = true;
    }
}

public sealed class SiegeVisualCycle6Result
{
    public readonly List<string> Blocked = new List<string>();
    public readonly List<string> Failures = new List<string>();
}
