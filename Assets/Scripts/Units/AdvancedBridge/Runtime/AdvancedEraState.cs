using System;
using EraImperial.Units.Advanced;
using UnityEngine;

/// <summary>
/// Era atual da partida para as unidades avancadas. O jogo ainda nao tem uma "era" propria (a arvore tecnologica
/// so trata attack_booster e cost_reducer_buy); este estado e o ponto unico a ser ligado a ela. Padrao: Ancient.
/// Teste em Play Mode: comando do console de debug "set_advanced_era <0..3>".
/// </summary>
public static class AdvancedEraState
{
    private static ImperialEra _current = ImperialEra.Ancient;

    public static ImperialEra Current => _current;
    public static event Action<ImperialEra> Changed;

    public static void Set(ImperialEra era)
    {
        if (_current == era) return;
        _current = era;
        Changed?.Invoke(era);
    }

    public static void Reset()
    {
        _current = ImperialEra.Ancient;
    }

    // evita vazar o estado de uma partida/execucao para a seguinte (Enter Play Mode sem recarregar dominio)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = ImperialEra.Ancient;
        Changed = null;
    }
}
