using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Ciclo 6 isolado para contratos de UI e feedback.
///
/// O harness não depende de Canvas, prefab ou cena de produção. Ele valida os
/// dados que a UI deverá consumir: bindings, fila de produção, avanço de era e
/// mensagens de impacto de cerco. A execução PlayMode é deliberadamente
/// bloqueada para não declarar que a UI real foi renderizada/operada.
/// </summary>
[InitializeOnLoad]
public static class UIFeedbackPlayModeHarness
{
    private const string RunningKey = "EraImperial.UIFeedbackCycle6.Running";
    private const string FinishedKey = "EraImperial.UIFeedbackCycle6.Finished";
    private static bool _finished;

    static UIFeedbackPlayModeHarness()
    {
        EditorApplication.update += Tick;
    }

    /// <summary>Executa o contrato em uma cena vazia; não salva nem altera ativos.</summary>
    public static void Run()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("UIFeedbackPlayModeHarness.Run exige o Editor fora do PlayMode.");

        _finished = false;
        SessionState.SetBool(RunningKey, true);
        SessionState.SetBool(FinishedKey, false);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("EventManager").AddComponent<EventManager>();
        EditorApplication.EnterPlaymode();
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
            UIFeedbackCycle6Result result = RunScenario();
            if (result.Failed.Count > 0)
            {
                Debug.LogError("UI_FEEDBACK_CYCLE6_FAIL: "
                    + string.Join(" | ", result.Failed.ToArray()));
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

            Debug.Log("UI_FEEDBACK_CYCLE6_PASS: contratos, bindings, fila, era e impacto");
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

    private static UIFeedbackCycle6Result RunScenario()
    {
        UIFeedbackCycle6Result result = new UIFeedbackCycle6Result();
        try
        {
            ValidateBindings();
            ValidateProductionQueue();
            ValidateEraFeedback();
            ValidateSiegeImpactFeedback();

            // Os contratos são PASS independentes. O gate visual fica separado
            // para evitar falso positivo enquanto o Licensing Client impede o
            // Unity de executar a partida real.
            result.Blocked.Add("Canvas/HUD de produção ainda não foi renderizado na partida real");
            result.Blocked.Add("feedback visual/sonoro de avanço de era ainda não foi operado no Unity");
            result.Blocked.Add("mensagem de impacto de cerco ainda não foi ligada a projétil/collider de produção");
        }
        catch (Exception exception)
        {
            result.Failed.Add(exception.Message);
        }
        return result;
    }

    private static void ValidateBindings()
    {
        UIContract contract = UIContract.CreateDefault();
        Require(contract.Bindings.Count == 9, "contrato de UI não possui os 9 bindings esperados");

        string[] required =
        {
            "production.queue", "production.selected", "production.progress",
            "production.population", "era.current", "era.next", "era.progress",
            "siege.impact", "siege.damage"
        };
        foreach (string key in required)
        {
            UIBinding binding = contract.Bindings.SingleOrDefault(item => item.Key == key);
            Require(binding != null, "binding ausente: " + key);
            Require(!string.IsNullOrEmpty(binding.WidgetId), "widget vazio para: " + key);
            Require(binding.ValueType != UIValueType.None, "tipo vazio para: " + key);
        }

        Require(contract.Bindings.Select(item => item.WidgetId).Distinct().Count()
            == contract.Bindings.Count, "bindings compartilham widget e podem sobrescrever estado");
    }

    private static void ValidateProductionQueue()
    {
        ProductionQueueState queue = new ProductionQueueState(5);
        Require(queue.Enqueue("villager", "TownCenter", 10f), "villager não entrou na fila");
        Require(queue.Enqueue("spearman", "Barracks", 20f), "spearman não entrou na fila");
        Require(!queue.Enqueue("archer", "ArcheryRange", 15f), "fila excedeu a capacidade");

        ProductionQueueView view = queue.ToView();
        Require(view.Items.Count == 2, "estado da fila não expôs dois itens");
        Require(view.Items[0].State == ProductionItemState.Queued
            && view.Items[1].State == ProductionItemState.Queued,
            "itens novos não começaram como Queued");

        queue.StartNext();
        queue.Advance(5f);
        view = queue.ToView();
        Require(view.Items[0].State == ProductionItemState.Producing,
            "primeiro item não entrou em Producing");
        Require(Math.Abs(view.Items[0].Progress - 0.5f) < 0.001f,
            "progresso da fila não foi calculado corretamente");
        Require(view.SelectedCode == "villager", "seleção da fila não apontou para o item ativo");

        queue.Advance(5f);
        view = queue.ToView();
        Require(view.Items[0].State == ProductionItemState.Completed,
            "item concluído não foi marcado como Completed");
        Require(view.Items[1].State == ProductionItemState.Producing,
            "fila não iniciou o próximo item após conclusão");
        Require(view.Items[0].Progress >= 0.999f, "item concluído não expôs progresso final");
    }

    private static void ValidateEraFeedback()
    {
        EraFeedbackState era = new EraFeedbackState("Classical", "Medieval", 0f, false);
        EraFeedbackView initial = era.ToView();
        Require(initial.Current == "Classical" && initial.Next == "Medieval",
            "estado inicial de era incorreto");
        Require(initial.Status == EraFeedbackStatus.Locked,
            "era sem requisito não foi exibida como Locked");

        era.SetProgress(0.5f, false);
        EraFeedbackView researching = era.ToView();
        Require(researching.Status == EraFeedbackStatus.Researching
            && Math.Abs(researching.Progress - 0.5f) < 0.001f,
            "progresso de avanço de era não foi exposto corretamente");

        era.SetProgress(1f, true);
        EraFeedbackView ready = era.ToView();
        Require(ready.Status == EraFeedbackStatus.Ready && ready.Progress >= 0.999f,
            "era pronta não foi exposta como Ready");
        Require(ready.Message == "Avanço de era disponível",
            "mensagem de era pronta não é determinística");
    }

    private static void ValidateSiegeImpactFeedback()
    {
        SiegeImpactFeedback impact = SiegeImpactFeedback.Create(42, "catapult_boulder",
            "Barracks", 120, 0.75f, true);
        SiegeImpactView view = impact.ToView();
        Require(view.ProjectileCode == "catapult_boulder", "projétil ausente no feedback");
        Require(view.TargetCode == "Barracks", "alvo ausente no feedback de cerco");
        Require(view.Damage == 120 && view.RadialFactor > 0.7f,
            "dano/falloff incorreto no feedback de cerco");
        Require(view.IsCritical && view.Message == "Impacto crítico em Barracks: 120 dano",
            "mensagem de impacto não é determinística");
    }

    private static void Require(bool condition, string message)
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

internal sealed class UIFeedbackCycle6Result
{
    public readonly List<string> Failed = new List<string>();
    public readonly List<string> Blocked = new List<string>();
}

internal enum UIValueType { None, Text, Number, Progress, Status, Message }
internal enum ProductionItemState { Queued, Producing, Completed }
internal enum EraFeedbackStatus { Locked, Researching, Ready }

internal sealed class UIBinding
{
    public string Key;
    public string WidgetId;
    public UIValueType ValueType;
}

internal sealed class UIContract
{
    public readonly List<UIBinding> Bindings = new List<UIBinding>();

    public static UIContract CreateDefault()
    {
        UIContract contract = new UIContract();
        contract.Bindings.Add(new UIBinding { Key = "production.queue", WidgetId = "hud-production-queue", ValueType = UIValueType.Text });
        contract.Bindings.Add(new UIBinding { Key = "production.selected", WidgetId = "hud-production-selected", ValueType = UIValueType.Text });
        contract.Bindings.Add(new UIBinding { Key = "production.progress", WidgetId = "hud-production-progress", ValueType = UIValueType.Progress });
        contract.Bindings.Add(new UIBinding { Key = "production.population", WidgetId = "hud-production-population", ValueType = UIValueType.Text });
        contract.Bindings.Add(new UIBinding { Key = "era.current", WidgetId = "hud-era-current", ValueType = UIValueType.Text });
        contract.Bindings.Add(new UIBinding { Key = "era.next", WidgetId = "hud-era-next", ValueType = UIValueType.Text });
        contract.Bindings.Add(new UIBinding { Key = "era.progress", WidgetId = "hud-era-progress", ValueType = UIValueType.Progress });
        contract.Bindings.Add(new UIBinding { Key = "siege.impact", WidgetId = "hud-siege-impact", ValueType = UIValueType.Message });
        contract.Bindings.Add(new UIBinding { Key = "siege.damage", WidgetId = "hud-siege-damage", ValueType = UIValueType.Number });
        return contract;
    }
}

internal sealed class ProductionItemView
{
    public readonly string Code;
    public readonly ProductionItemState State;
    public readonly float Progress;

    public ProductionItemView(string code, ProductionItemState state, float progress)
    {
        Code = code;
        State = state;
        Progress = progress;
    }
}

internal sealed class ProductionQueueView
{
    public readonly List<ProductionItemView> Items;
    public readonly string SelectedCode;

    public ProductionQueueView(List<ProductionItemView> items, string selectedCode)
    {
        Items = items;
        SelectedCode = selectedCode;
    }
}

internal sealed class ProductionQueueState
{
    private sealed class Item
    {
        public string Code;
        public float Duration;
        public float Elapsed;
        public ProductionItemState State;
    }

    private readonly int _capacity;
    private readonly List<Item> _items = new List<Item>();

    public ProductionQueueState(int capacity) { _capacity = capacity; }

    public bool Enqueue(string code, string buildingCode, float duration)
    {
        if (_items.Count >= _capacity || string.IsNullOrEmpty(code)
            || string.IsNullOrEmpty(buildingCode) || duration <= 0f) return false;
        _items.Add(new Item { Code = code, Duration = duration, State = ProductionItemState.Queued });
        return true;
    }

    public void StartNext()
    {
        if (_items.Any(item => item.State == ProductionItemState.Producing)) return;
        Item next = _items.FirstOrDefault(item => item.State == ProductionItemState.Queued);
        if (next != null) next.State = ProductionItemState.Producing;
    }

    public void Advance(float seconds)
    {
        if (seconds < 0f) throw new ArgumentOutOfRangeException("seconds");
        StartNext();
        Item active = _items.FirstOrDefault(item => item.State == ProductionItemState.Producing);
        if (active == null) return;
        active.Elapsed = Mathf.Min(active.Duration, active.Elapsed + seconds);
        if (active.Elapsed >= active.Duration)
        {
            active.State = ProductionItemState.Completed;
            StartNext();
        }
    }

    public ProductionQueueView ToView()
    {
        List<ProductionItemView> items = _items.Select(item => new ProductionItemView(
            item.Code, item.State, Mathf.Clamp01(item.Elapsed / item.Duration))).ToList();
        Item active = _items.FirstOrDefault(item => item.State == ProductionItemState.Producing);
        return new ProductionQueueView(items, active == null ? string.Empty : active.Code);
    }
}

internal sealed class EraFeedbackState
{
    private readonly string _current;
    private readonly string _next;
    private float _progress;
    private bool _ready;

    public EraFeedbackState(string current, string next, float progress, bool ready)
    {
        _current = current; _next = next; _progress = progress; _ready = ready;
    }

    public void SetProgress(float progress, bool ready)
    {
        _progress = Mathf.Clamp01(progress); _ready = ready && _progress >= 1f;
    }

    public EraFeedbackView ToView()
    {
        EraFeedbackStatus status = _ready ? EraFeedbackStatus.Ready
            : (_progress > 0f ? EraFeedbackStatus.Researching : EraFeedbackStatus.Locked);
        return new EraFeedbackView(_current, _next, _progress, status,
            status == EraFeedbackStatus.Ready ? "Avanço de era disponível" : string.Empty);
    }
}

internal sealed class EraFeedbackView
{
    public readonly string Current, Next, Message;
    public readonly float Progress;
    public readonly EraFeedbackStatus Status;

    public EraFeedbackView(string current, string next, float progress,
        EraFeedbackStatus status, string message)
    {
        Current = current; Next = next; Progress = progress; Status = status; Message = message;
    }
}

internal sealed class SiegeImpactFeedback
{
    private readonly int _id;
    private readonly string _projectileCode, _targetCode;
    private readonly int _damage;
    private readonly float _radialFactor;
    private readonly bool _critical;

    private SiegeImpactFeedback(int id, string projectileCode, string targetCode,
        int damage, float radialFactor, bool critical)
    {
        _id = id; _projectileCode = projectileCode; _targetCode = targetCode;
        _damage = damage; _radialFactor = radialFactor; _critical = critical;
    }

    public static SiegeImpactFeedback Create(int id, string projectileCode, string targetCode,
        int damage, float radialFactor, bool critical)
    {
        return new SiegeImpactFeedback(id, projectileCode, targetCode, damage,
            Mathf.Clamp01(radialFactor), critical);
    }

    public SiegeImpactView ToView()
    {
        string message = _critical ? "Impacto crítico em " + _targetCode + ": " + _damage + " dano"
            : "Impacto em " + _targetCode + ": " + _damage + " dano";
        return new SiegeImpactView(_id, _projectileCode, _targetCode, _damage,
            _radialFactor, _critical, message);
    }
}

internal sealed class SiegeImpactView
{
    public readonly int Id, Damage;
    public readonly string ProjectileCode, TargetCode, Message;
    public readonly float RadialFactor;
    public readonly bool IsCritical;

    public SiegeImpactView(int id, string projectileCode, string targetCode, int damage,
        float radialFactor, bool critical, string message)
    {
        Id = id; ProjectileCode = projectileCode; TargetCode = targetCode;
        Damage = damage; RadialFactor = radialFactor; IsCritical = critical; Message = message;
    }
}
