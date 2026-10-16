using System;
using EraImperial.Units.Advanced;
using UnityEngine;

/// <summary>
/// Era atual da partida para unidades avancadas e visuais de ambiente/construcoes.
/// A progressao e aplicada pelo replay apos a politica da arvore tecnologica;
/// partidas novas iniciam em Ancient.
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
        Set(ImperialEra.Ancient);
    }

    // evita vazar o estado de uma partida/execucao para a seguinte (Enter Play Mode sem recarregar dominio)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _current = ImperialEra.Ancient;
        Changed = null;
    }
}
