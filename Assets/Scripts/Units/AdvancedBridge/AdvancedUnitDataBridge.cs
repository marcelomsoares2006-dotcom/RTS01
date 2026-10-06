using System;
using System.Collections.Generic;
using EraImperial.Units.Advanced;
using UnityEngine;

/// <summary>
/// Ponte entre os contratos das unidades avançadas (asmdefs EraImperial.Units.*) e o sistema atual de
/// UnitData / CharacterData / BuildingData, que vive no Assembly-CSharp. Fica fora dos asmdefs porque eles
/// não podem referenciar o Assembly-CSharp.
///
/// Regra: um UnitData só é preenchido quando os dados de jogo são válidos E o visual é real (prefab com
/// sockets verificados, malhas e clipes atribuídos). Isso impede que uma unidade sem malha entre no catálogo
/// (Globals carrega todo CharacterData de Resources e Unit instancia data.prefab).
/// </summary>
public static class AdvancedUnitDataBridge
{
    /// <summary>Informa se o contrato pode ser ligado a um UnitData agora, e por que não.</summary>
    public static bool CanBind(AdvancedUnitContract contract, out string reason)
    {
        if (contract == null)
        {
            reason = "contrato nulo";
            return false;
        }

        ValidationReport gameplay = contract.Validate();
        if (!gameplay.IsValid)
        {
            reason = "dados de jogo inválidos: " + gameplay;
            return false;
        }

        VisualReadinessReport visual = contract.EvaluateVisuals();
        if (!visual.IsReady)
        {
            reason = "visual " + visual;
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// Copia os dados da era para o UnitData (CharacterData para dataKind Character, BuildingData para Building)
    /// e liga o prefab real. Lança InvalidOperationException se o contrato não estiver pronto.
    /// </summary>
    public static void Apply(UnitData target, AdvancedUnitContract contract, ImperialEra era)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        if (!CanBind(contract, out string reason))
            throw new InvalidOperationException($"'{(contract != null ? contract.code : "?")}' não pode ser ligado a UnitData: {reason}");

        if (contract.dataKind == UnitDataKind.Character && !(target is CharacterData))
            throw new InvalidOperationException($"'{contract.code}' é Character, mas o destino não é CharacterData.");
        if (contract.dataKind == UnitDataKind.Building && !(target is BuildingData))
            throw new InvalidOperationException($"'{contract.code}' é Building, mas o destino não é BuildingData.");

        ApplyStats(target, contract.BuildUnitDataFields(era));
        target.prefab = contract.visual.prefab.asset as GameObject;
    }

    /// <summary>Copia apenas os números e o custo (sem prefab). Usado pela ponte e por verificações de compatibilidade.</summary>
    public static void ApplyStats(UnitData target, UnitDataFields fields)
    {
        target.code = fields.code;
        target.unitName = fields.unitName;
        target.description = fields.description;
        target.healthpoints = fields.healthpoints;
        target.fieldOfView = fields.fieldOfView;
        target.attackRange = fields.attackRange;
        target.attackDamage = fields.attackDamage;
        target.attackRate = fields.attackRate;

        List<ResourceValue> cost = new List<ResourceValue>();
        if (fields.cost != null)
        {
            foreach (ResourceCost c in fields.cost)
                cost.Add(new ResourceValue((InGameResource)Enum.Parse(typeof(InGameResource), c.resource), c.amount));
        }
        target.cost = cost;
    }
}
