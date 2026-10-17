using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Auditoria isolada de projéteis, colliders e destruição estrutural.</summary>
public static class SiegeVisualCycle6Audit
{
    private const string Report = "Docs/QA/2026-10-17-siege-visual-cycle6.md";

    [MenuItem("Era Imperial/QA/Cycle 6/Siege visual audit")]
    public static void Run()
    {
        var checks = new List<string>();
        var blocked = new List<string>();
        string[] files = {
            "Assets/Scripts/Combat/CombatAttackRuntime.cs",
            "Assets/Scripts/Units/AdvancedBridge/Runtime/AdvancedProjectile.cs",
            "Assets/Scripts/Units/AdvancedBridge/Runtime/ProjectileAttackController.cs",
            "Assets/Scripts/Units/Buildings/BuildingRepairDemolitionContract.cs",
            "Assets/Scripts/Units/Buildings/BuildingDefenseContracts.cs"
        };
        string all = string.Empty;
        foreach (string file in files)
        {
            bool exists = File.Exists(file);
            checks.Add(file + ": " + (exists ? "present" : "missing"));
            if (!exists) blocked.Add("arquivo ausente: " + file);
            all += Read(file);
        }
        bool projectile = ContainsAny(all, "Projectile", "projectile", "CombatProjectilePayload");
        bool collision = ContainsAny(all, "Collider", "OnCollision", "Raycast", "Overlap");
        bool destruction = ContainsAny(all, "Destroy", "Demolition", "destruction", "SetAlive");
        checks.Add("contrato de projétil: " + projectile);
        checks.Add("colisão/consulta física: " + collision);
        checks.Add("destruição estrutural: " + destruction);
        if (!projectile) blocked.Add("contrato de projétil não localizado");
        if (!collision) blocked.Add("collider/consulta física não localizado");
        if (!destruction) blocked.Add("contrato de destruição estrutural não localizado");
        blocked.Add("sem PlayMode não há prova de GameObject de projétil, collider, impacto ou ruína visual");
        WriteReport(checks, blocked);
        Debug.Log(blocked.Count == 0
            ? "SIEGE_VISUAL_CYCLE6_PASS"
            : "SIEGE_VISUAL_CYCLE6_BLOCKED: " + string.Join(" | ", blocked));
    }

    private static string Read(string path) => File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    private static bool ContainsAny(string value, params string[] terms)
    {
        foreach (string term in terms)
            if (value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
    private static void WriteReport(List<string> checks, List<string> blocked)
    {
        string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, Report);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, "# Ciclo 6 — cerco visual\n\nStatus: **" + (blocked.Count == 0 ? "PASS" : "BLOCKED")
            + "**\n\nAuditoria somente leitura; nenhum prefab/cena/asset de produção foi alterado.\n\n## Checks\n\n- "
            + string.Join("\n- ", checks) + "\n\n## Bloqueios\n\n- " + string.Join("\n- ", blocked) + "\n");
        AssetDatabase.Refresh();
    }
}
