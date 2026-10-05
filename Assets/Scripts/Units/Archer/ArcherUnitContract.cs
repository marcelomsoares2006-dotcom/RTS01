using UnityEngine;

/// <summary>
/// Contrato mínimo para uma futura unidade Arqueiro.
/// Não cria malha, prefab ou comportamento de cena.
/// </summary>
public interface IArcherUnitContract
{
    string CharacterCode { get; }
    bool HasSourceMesh { get; }
    bool IsReadyForVisualIntegration { get; }
}

/// <summary>
/// Implementação de dados que pode ser usada por ferramentas e testes antes da
/// existência da malha. A integração visual deve continuar bloqueada enquanto
/// HasSourceMesh for falso.
/// </summary>
[CreateAssetMenu(fileName = "ArcherUnitContract", menuName = "Era Imperial/Units/Archer Unit Contract")]
public sealed class ArcherUnitContract : ScriptableObject, IArcherUnitContract
{
    [SerializeField] private string characterCode = "archer";
    [SerializeField] private bool sourceMeshProvided;
    [SerializeField] private bool sourceLicenseConfirmed;

    public string CharacterCode => characterCode;
    public bool HasSourceMesh => sourceMeshProvided;
    public bool IsReadyForVisualIntegration => sourceMeshProvided && sourceLicenseConfirmed;
}
