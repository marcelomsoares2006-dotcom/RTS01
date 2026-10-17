using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Auditoria isolada de UI/feedback para produção, eras e cerco.</summary>
public static class UiFeedbackCycle6Audit
{
    private const string Report = "Docs/QA/2026-10-17-ui-feedback-cycle6.md";

    [MenuItem("Era Imperial/QA/Cycle 6/UI feedback audit")]
    public static void Run()
    {
        var checks = new List<string>();
        var blocked = new List<string>();
        string[] files = {
            "Assets/Scripts/UI/UIManager.cs",
            "Assets/Scripts/UI/BuildingProductionUiContract.cs",
            "Assets/Scripts/UI/BuildingConstructionUiRules.cs",
            "Assets/Scripts/DebugConsole/DebugConsole.cs"
        };
        foreach (string file in files)
        {
            bool exists = File.Exists(file);
            checks.Add(file + ": " + (exists ? "present" : "missing"));
            if (!exists) blocked.Add("arquivo ausente: " + file);
        }
        string ui = Read(files[0]) + Read(files[1]) + Read(files[2]);
        bool production = ContainsAny(ui, "production", "queue", "BuildingProduction");
        bool era = ContainsAny(ui + Read(files[3]), "AdvancedEra", "EraChanged", "era");
        bool siege = ContainsAny(ui, "impact", "damage", "siege", "projectile");
        checks.Add("binding de produção: " + production);
        checks.Add("binding de era: " + era);
        checks.Add("feedback de cerco: " + siege);
        if (!production) blocked.Add("UI de produção não possui binding estático verificável");
        if (!era) blocked.Add("feedback de avanço de era não possui binding verificável");
        if (!siege) blocked.Add("feedback de impacto/cerco não possui binding verificável");
        blocked.Add("Canvas/UIDocument e eventos em runtime não podem ser aceitos sem PlayMode Unity");
        WriteReport(checks, blocked);
        Debug.Log(blocked.Count == 0
            ? "UI_FEEDBACK_CYCLE6_PASS"
            : "UI_FEEDBACK_CYCLE6_BLOCKED: " + string.Join(" | ", blocked));
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
        File.WriteAllText(path, "# Ciclo 6 — UI e feedback\n\nStatus: **" + (blocked.Count == 0 ? "PASS" : "BLOCKED")
            + "**\n\nSomente auditoria; nenhuma cena/prefab foi alterada.\n\n## Checks\n\n- "
            + string.Join("\n- ", checks) + "\n\n## Bloqueios\n\n- " + string.Join("\n- ", blocked) + "\n");
        AssetDatabase.Refresh();
    }
}
