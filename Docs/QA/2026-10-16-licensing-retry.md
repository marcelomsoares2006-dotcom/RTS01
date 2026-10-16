# Nova tentativa de licenciamento — 2026-10-16

O processo `Unity.Licensing.Client` foi reiniciado de forma controlada e voltou a responder. A execução seguinte de `Tools/Validate-Project.ps1 -Build` iniciou o Unity, mas o log permaneceu em `Licensing is not yet initialized` e o EditMode excedeu 300 segundos.

Conclusão: o bloqueio continua externo à compilação do projeto e não foi resolvido por uma simples reinicialização do cliente. Foi adicionada a ferramenta `Tools/Repair-UnityLicensingClient.ps1` para repetir o procedimento sem apagar credenciais, licenças ou dados do projeto.

Antes da próxima tentativa, abrir o Unity Hub normalmente, verificar visualmente a conta Student ativa e fechar o Editor pelo fluxo normal. Só depois executar a validação batch.
