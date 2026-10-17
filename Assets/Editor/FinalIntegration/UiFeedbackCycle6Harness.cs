using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Protótipo isolado do ciclo 6: contratos de UI e feedback para produção,
/// avanço de eras e cerco. Não referencia prefabs, cenas ou assets de produção.
///
/// O cenário valida a máquina de estados e os bindings em memória. A conexão
/// com a UI real da partida é um gate separado e continua BLOCKED até existir
/// uma execução PlayMode real com licença e objetos de runtime disponíveis.
/// </summary>
[InitializeOnLoad]
public static class UiFeedbackCycle6Harness
{
    private const string RunningKey = "EraImperial.UiFeedbackCycle6.Running";
    private const string FinishedKey = "EraImperial.UiFeedbackCycle6.Finished";
    private static bool _finished;

    static UiFeedbackCycle6Harness()
    {
        EditorApplication.update += Tick;
    }

    /// <summary>
    /// Executa o protótipo em uma cena vazia e encerra com 0, 1 ou 2:
    /// PASS, FAIL ou BLOCKED, respectivamente.
    /// </summary>
    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("UiFeedbackCycle6Harness.Run exige o Editor fora do PlayMode.");

        _finished = false;
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(FinishedKey, false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    /// <summary>
    /// Permite uma auditoria determinística dos contratos sem iniciar a partida.
    /// O retorno é útil para revisão estática e não afirma integração runtime.
    /// </summary>
    public static UiFeedbackCycle6Result RunContractsOnly()
    {
        UiFeedbackCycle6Result result = new UiFeedbackCycle6Result();
        try
        {
            ValidateBindings(result);
            ValidateProductionQueue(result);
            ValidateEraFeedback(result);
            ValidateSiegeFeedback(result);
            result.Blocked.Add("UI de produção ainda não está ligada a um Canvas/instância real da partida");
            result.Blocked.Add("feedback de avanço de era ainda não está ligado ao comando real e à câmera da partida");
            result.Blocked.Add("mensagens de impacto ainda não recebem eventos de projétil/collider do cerco real");
        }
        catch (Exception exception)
        {
            result.Fail.Add(exception.Message);
        }
        return result;
    }

    private static void Tick()
    {
        if (_finished || !SessionState.GetBool(RunningKey, false)
            || !EditorApplication.isPlaying || EditorApplication.isCompiling)
            return;

        _finished = true;
        try
        {
            Require(Application.isPlaying, "o runner não entrou em PlayMode");
            UiFeedbackCycle6Result result = RunContractsOnly();
            if (result.Fail.Count > 0)
            {
                Debug.LogError("UI_FEEDBACK_CYCLE6_FAIL: "
                    + string.Join(" | ", result.Fail.ToArray()));
                SessionState.SetBool(FinishedKey, false);
                StopAndExit(1);
                return;
            }

            if (result.Blocked.Count > 0)
            {
                Debug.LogWarning("UI_FEEDBACK_CYCLE6_BLOCKED: "
                    + string.Join(" | ", result.Blocked.ToArray()));
                SessionState.SetBool(FinishedKey, false);
                StopAndExit(2);
                return;
            }

            Debug.Log("UI_FEEDBACK_CYCLE6_PASS: contratos e bindings de UI validados");
            SessionState.SetBool(FinishedKey, true);
            StopAndExit(0);
        }
        catch (Exception exception)
        {
            Debug.LogError("UI_FEEDBACK_CYCLE6_FAIL: " + exception);
            SessionState.SetBool(FinishedKey, false);
            StopAndExit(1);
        }
    }

    private static void ValidateBindings(UiFeedbackCycle6Result result)
    {
        UiBinding[] bindings =
        {
            new UiBinding(UiBindingId.ProductionPanel, "ui.production.panel", UiBindingKind.Panel),
            new UiBinding(UiBindingId.ProductionQueue, "ui.production.queue", UiBindingKind.Queue),
            new UiBinding(UiBindingId.ProductionProgress, "ui.production.progress", UiBindingKind.ProgressBar),
            new UiBinding(UiBindingId.EraPanel, "ui.era.panel", UiBindingKind.Panel),
            new UiBinding(UiBindingId.EraProgress, "ui.era.progress", UiBindingKind.ProgressBar),
            new UiBinding(UiBindingId.EraMessage, "ui.era.message", UiBindingKind.Message),
            new UiBinding(UiBindingId.SiegeImpactFeed, "ui.siege.impact-feed", UiBindingKind.Message),
            new UiBinding(UiBindingId.SiegeDamageNumber, "ui.siege.damage-number", UiBindingKind.Message)
        };

        Require(bindings.Length == Enum.GetValues(typeof(UiBindingId)).Length,
            "catálogo de bindings não cobre todas as superfícies previstas");
        Require(bindings.All(binding => !string.IsNullOrWhiteSpace(binding.Key)),
            "existe binding sem chave");
        Require(bindings.Select(binding => binding.Key).Distinct(StringComparer.Ordinal).Count() == bindings.Length,
            "há chaves de binding duplicadas");
        Require(bindings.Select(binding => binding.Id).Distinct().Count() == bindings.Length,
            "há IDs de binding duplicados");
        Require(bindings.Any(binding => binding.Id == UiBindingId.ProductionQueue && binding.Kind == UiBindingKind.Queue),
            "fila de produção não possui binding do tipo Queue");
        Require(bindings.Any(binding => binding.Id == UiBindingId.EraProgress && binding.Kind == UiBindingKind.ProgressBar),
            "progresso de era não possui binding do tipo ProgressBar");
        Require(bindings.Any(binding => binding.Id == UiBindingId.SiegeImpactFeed && binding.Kind == UiBindingKind.Message),
            "cerco não possui binding de mensagens");
        result.Pass.Add("bindings obrigatórios, tipos e unicidade");
    }

    private static void ValidateProductionQueue(UiFeedbackCycle6Result result)
    {
        ProductionQueueUiState state = new ProductionQueueUiState();
        Require(state.Status == ProductionQueueStatus.Empty, "fila não inicia vazia");
        Require(state.TryEnqueue("spearman", 12f), "fila recusou enfileirar unidade");
        Require(state.Status == ProductionQueueStatus.Queued && state.Code == "spearman",
            "fila não expôs estado Queued/código");
        Require(state.TryStart(), "fila recusou iniciar produção");
        Require(state.Status == ProductionQueueStatus.Producing, "fila não expôs estado Producing");
        state.SetProgress(0.5f);
        Require(Mathf.Approximately(state.Progress, 0.5f), "progresso de produção não foi atualizado");
        Require(state.TryComplete(), "fila recusou conclusão");
        Require(state.Status == ProductionQueueStatus.Completed && Mathf.Approximately(state.Progress, 1f),
            "fila não expôs estado Completed");
        Require(state.TryClear(), "fila concluída não pôde ser limpa");
        Require(state.Status == ProductionQueueStatus.Empty, "fila não voltou a Empty");

        Require(state.TryEnqueue("combat_mage", 20f), "fila recusou segunda unidade");
        Require(state.TryCancel(), "fila recusou cancelamento");
        Require(state.Status == ProductionQueueStatus.Cancelled, "fila não expôs estado Cancelled");
        Require(!state.TryComplete(), "fila cancelada aceitou conclusão");
        result.Pass.Add("estados de fila Empty/Queued/Producing/Completed/Cancelled");
    }

    private static void ValidateEraFeedback(UiFeedbackCycle6Result result)
    {
        EraFeedbackState state = new EraFeedbackState();
        Require(state.Status == EraFeedbackStatus.Hidden, "feedback de era não inicia Hidden");
        Require(state.BeginResearch("feudal-tech"), "feedback não iniciou pesquisa");
        Require(state.Status == EraFeedbackStatus.Researching, "estado Researching ausente");
        state.SetResearchProgress(0.75f);
        Require(state.Status == EraFeedbackStatus.Researching && Mathf.Approximately(state.Progress, 0.75f),
            "progresso da pesquisa não foi preservado");
        Require(!state.MarkReady(), "feedback marcou age-up pronto antes de concluir pesquisa");
        state.SetResearchProgress(1f);
        Require(state.MarkReady(), "feedback não marcou age-up pronto");
        Require(state.Status == EraFeedbackStatus.Ready && state.Message == "ERA_READY",
            "mensagem ERA_READY ausente");
        Require(state.BeginAdvance(), "feedback não iniciou avanço");
        Require(state.Status == EraFeedbackStatus.Advancing, "estado Advancing ausente");
        Require(state.CompleteAdvance("Medieval"), "feedback não concluiu avanço");
        Require(state.Status == EraFeedbackStatus.Completed && state.Message == "ERA_COMPLETED:Medieval",
            "mensagem de conclusão da era inválida");
        result.Pass.Add("feedback de era Hidden/Researching/Ready/Advancing/Completed");
    }

    private static void ValidateSiegeFeedback(UiFeedbackCycle6Result result)
    {
        SiegeImpactFeed feed = new SiegeImpactFeed();
        Require(feed.Push(SiegeImpactKind.Direct, 120, "TownCenter"), "impacto direto não foi aceito");
        Require(feed.Push(SiegeImpactKind.Radial, 45, "Barracks"), "impacto radial não foi aceito");
        Require(feed.Push(SiegeImpactKind.Miss, 0, "Wall"), "miss não foi aceito");
        Require(feed.Items.Count == 3, "feed não preservou três mensagens de impacto");
        Require(feed.Items[0].Text == "IMPACT_DIRECT:TownCenter:120", "mensagem direta inválida");
        Require(feed.Items[1].Text == "IMPACT_RADIAL:Barracks:45", "mensagem radial inválida");
        Require(feed.Items[2].Text == "IMPACT_MISS:Wall:0", "mensagem de miss inválida");
        result.Pass.Add("mensagens de impacto direto/radial/miss");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void StopAndExit(int code)
    {
        EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.Exit(code);
    }

    public enum UiBindingId { ProductionPanel, ProductionQueue, ProductionProgress, EraPanel, EraProgress, EraMessage, SiegeImpactFeed, SiegeDamageNumber }
    public enum UiBindingKind { Panel, Queue, ProgressBar, Message }
    public enum ProductionQueueStatus { Empty, Queued, Producing, Completed, Cancelled }
    public enum EraFeedbackStatus { Hidden, Researching, Ready, Advancing, Completed }
    public enum SiegeImpactKind { Direct, Radial, Miss }

    public sealed class UiBinding
    {
        public readonly UiBindingId Id;
        public readonly string Key;
        public readonly UiBindingKind Kind;
        public UiBinding(UiBindingId id, string key, UiBindingKind kind) { Id = id; Key = key; Kind = kind; }
    }

    public sealed class ProductionQueueUiState
    {
        public ProductionQueueStatus Status { get; private set; }
        public string Code { get; private set; }
        public float Duration { get; private set; }
        public float Progress { get; private set; }
        public bool TryEnqueue(string code, float duration)
        {
            if (Status != ProductionQueueStatus.Empty || string.IsNullOrWhiteSpace(code) || duration <= 0f) return false;
            Code = code; Duration = duration; Progress = 0f; Status = ProductionQueueStatus.Queued; return true;
        }
        public bool TryStart() { if (Status != ProductionQueueStatus.Queued) return false; Status = ProductionQueueStatus.Producing; return true; }
        public bool TryComplete() { if (Status != ProductionQueueStatus.Producing) return false; Progress = 1f; Status = ProductionQueueStatus.Completed; return true; }
        public bool TryCancel() { if (Status != ProductionQueueStatus.Queued && Status != ProductionQueueStatus.Producing) return false; Status = ProductionQueueStatus.Cancelled; return true; }
        public bool TryClear() { if (Status != ProductionQueueStatus.Completed && Status != ProductionQueueStatus.Cancelled) return false; Code = null; Duration = 0f; Progress = 0f; Status = ProductionQueueStatus.Empty; return true; }
        public void SetProgress(float value) { if (Status == ProductionQueueStatus.Producing) Progress = Mathf.Clamp01(value); }
    }

    public sealed class EraFeedbackState
    {
        public EraFeedbackStatus Status { get; private set; }
        public float Progress { get; private set; }
        public string Technology { get; private set; }
        public string Message { get; private set; }
        public bool BeginResearch(string technology) { if (Status != EraFeedbackStatus.Hidden || string.IsNullOrWhiteSpace(technology)) return false; Technology = technology; Progress = 0f; Message = "ERA_RESEARCHING:" + technology; Status = EraFeedbackStatus.Researching; return true; }
        public void SetResearchProgress(float value) { if (Status == EraFeedbackStatus.Researching) Progress = Mathf.Clamp01(value); }
        public bool MarkReady() { if (Status != EraFeedbackStatus.Researching || Progress < 0.999f) return false; Message = "ERA_READY"; Status = EraFeedbackStatus.Ready; return true; }
        public bool BeginAdvance() { if (Status != EraFeedbackStatus.Ready) return false; Message = "ERA_ADVANCING"; Status = EraFeedbackStatus.Advancing; return true; }
        public bool CompleteAdvance(string era) { if (Status != EraFeedbackStatus.Advancing || string.IsNullOrWhiteSpace(era)) return false; Message = "ERA_COMPLETED:" + era; Status = EraFeedbackStatus.Completed; return true; }
    }

    public sealed class SiegeImpactFeed
    {
        public readonly List<SiegeImpactMessage> Items = new List<SiegeImpactMessage>();
        public bool Push(SiegeImpactKind kind, int amount, string target)
        {
            if (string.IsNullOrWhiteSpace(target) || amount < 0) return false;
            string prefix = kind == SiegeImpactKind.Direct ? "IMPACT_DIRECT" : kind == SiegeImpactKind.Radial ? "IMPACT_RADIAL" : "IMPACT_MISS";
            Items.Add(new SiegeImpactMessage(kind, amount, target, prefix + ":" + target + ":" + amount));
            return true;
        }
    }

    public sealed class SiegeImpactMessage
    {
        public readonly SiegeImpactKind Kind;
        public readonly int Amount;
        public readonly string Target;
        public readonly string Text;
        public SiegeImpactMessage(SiegeImpactKind kind, int amount, string target, string text) { Kind = kind; Amount = amount; Target = target; Text = text; }
    }

    public sealed class UiFeedbackCycle6Result
    {
        public readonly List<string> Pass = new List<string>();
        public readonly List<string> Fail = new List<string>();
        public readonly List<string> Blocked = new List<string>();
        public string Status { get { return Fail.Count > 0 ? "FAIL" : Blocked.Count > 0 ? "BLOCKED" : "PASS"; } }
    }
}
