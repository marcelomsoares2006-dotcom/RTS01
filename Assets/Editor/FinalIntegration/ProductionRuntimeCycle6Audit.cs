using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Auditoria isolada da produção runtime. Não instancia nem altera prefabs/cenas.
/// PASS só é emitido quando há prova de produção concluída em runtime; contratos
/// estáticos insuficientes ficam BLOCKED.
/// </summary>
public static class ProductionRuntimeCycle6Audit
{
    private const string Report = "Docs/QA/2026-10-17-production-runtime-cycle6.md";

    [MenuItem("Era Imperial/QA/Cycle 6/Production runtime audit")]
    public static void Run()
    {
        var blocked = new List<string>();
        var checks = new List<string>();
        Check("BuildingProductionRuntimeDriver.cs", checks, blocked);
        Check("BuildingProductionRuntimeAdapter.cs", checks, blocked);
        Check("BuildingProductionQueue.cs", checks, blocked);
        Check("ScriptableObjects/Units/Building Content Catalog.asset", checks, blocked);

        string driver = Read("Assets/Scripts/Units/Buildings/BuildingProductionRuntimeDriver.cs");
        string adapter = Read("Assets/Scripts/Units/Buildings/BuildingProductionRuntimeAdapter.cs");
        bool hasCompletion = ContainsAny(driver, "Advance", "Complete", "Spawn", "production");
        bool hasRuntimeBinding = ContainsAny(driver, "BuildingManager", "BuildingPlacer", "Instantiate");
        bool hasQueueBinding = ContainsAny(adapter, "BuildingProductionQueue", "TryEnqueue");
        checks.Add("runtime driver possui caminho de conclusão: " + hasCompletion);
        checks.Add("runtime driver possui binding de spawn/BuildingManager: " + hasRuntimeBinding);
        checks.Add("adapter possui binding de fila: " + hasQueueBinding);
        if (!hasRuntimeBinding) blocked.Add("não há prova estática de spawn runtime ligado a BuildingManager/BuildingPlacer");
        if (!hasQueueBinding) blocked.Add("adapter não expõe binding de fila verificável");
        blocked.Add("produção real dentro de uma partida ainda exige PlayMode Unity e confirmação da instância produzida");

        WriteReport(checks, blocked);
        Debug.Log(blocked.Count == 0
            ? "PRODUCTION_RUNTIME_CYCLE6_PASS"
            : "PRODUCTION_RUNTIME_CYCLE6_BLOCKED: " + string.Join(" | ", blocked));
    }

    private static void Check(string relative, List<string> checks, List<string> blocked)
    {
        bool exists = File.Exists(Path.Combine(Application.dataPath, relative.Replace("Assets/", "")));
        checks.Add(relative + ": " + (exists ? "present" : "missing"));
        if (!exists) blocked.Add("arquivo ausente: " + relative);
    }

    private static string Read(string relative)
    {
        string path = Path.Combine(Application.dataPath, relative.Replace("Assets/", ""));
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

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
        File.WriteAllText(path, "# Ciclo 6 — produção runtime\n\n"
            + "Status: **" + (blocked.Count == 0 ? "PASS" : "BLOCKED") + "**\n\n"
            + "Auditoria somente leitura; nenhum prefab, cena ou asset de produção foi alterado.\n\n"
            + "## Checks\n\n- " + string.Join("\n- ", checks)
            + "\n\n## Bloqueios\n\n- " + string.Join("\n- ", blocked)
            + "\n");
        AssetDatabase.Refresh();
    }
}
