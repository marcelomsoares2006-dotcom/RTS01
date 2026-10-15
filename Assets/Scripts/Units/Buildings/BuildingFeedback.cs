using System;
using UnityEngine;

/// <summary>
/// Sound and visual cues exposed by a building to UI, accessibility and
/// presentation systems. The cues intentionally refer to existing construction
/// and building audio; they do not require a new model or effect asset.
/// </summary>
public enum BuildingFeedbackAudioCue
{
    None,
    ConstructionLoop,
    OperationalAmbient,
    DamageWarning,
    Stopped
}

public enum BuildingFeedbackVisualCue
{
    Blueprint,
    UnderConstruction,
    Operational,
    Damaged,
    Destroyed
}

[Serializable]
public struct BuildingFeedbackSnapshot
{
    public BuildingLifecycleState lifecycle;
    [Range(0f, 1f)] public float constructionProgress;
    [Range(0f, 1f)] public float healthRatio;
    public BuildingFeedbackVisualCue visual;
    public BuildingFeedbackAudioCue audio;

    public BuildingFeedbackSnapshot(
        BuildingLifecycleState lifecycle,
        float constructionProgress,
        float healthRatio)
    {
        this.lifecycle = lifecycle;
        this.constructionProgress = Mathf.Clamp01(constructionProgress);
        this.healthRatio = Mathf.Clamp01(healthRatio);
        visual = BuildingFeedbackRules.VisualFor(lifecycle);
        audio = BuildingFeedbackRules.AudioFor(lifecycle);
    }
}

/// <summary>
/// Pure mapping from construction state to presentation cues. Keeping this
/// separate from Unity objects makes state communication deterministic and
/// testable in editor tests and save/load adapters.
/// </summary>
public static class BuildingFeedbackRules
{
    public static BuildingFeedbackVisualCue VisualFor(BuildingLifecycleState state)
    {
        switch (state)
        {
            case BuildingLifecycleState.Blueprint:
                return BuildingFeedbackVisualCue.Blueprint;
            case BuildingLifecycleState.UnderConstruction:
                return BuildingFeedbackVisualCue.UnderConstruction;
            case BuildingLifecycleState.Damaged:
                return BuildingFeedbackVisualCue.Damaged;
            case BuildingLifecycleState.Destroyed:
            case BuildingLifecycleState.Ruined:
                return BuildingFeedbackVisualCue.Destroyed;
            default:
                return BuildingFeedbackVisualCue.Operational;
        }
    }

    public static BuildingFeedbackAudioCue AudioFor(BuildingLifecycleState state)
    {
        switch (state)
        {
            case BuildingLifecycleState.UnderConstruction:
                return BuildingFeedbackAudioCue.ConstructionLoop;
            case BuildingLifecycleState.Damaged:
                // Reuse the existing construction-site clip until a dedicated
                // damage clip is authored; the cue remains explicit to callers.
                return BuildingFeedbackAudioCue.DamageWarning;
            case BuildingLifecycleState.Operational:
                return BuildingFeedbackAudioCue.OperationalAmbient;
            case BuildingLifecycleState.Destroyed:
            case BuildingLifecycleState.Ruined:
                return BuildingFeedbackAudioCue.Stopped;
            default:
                return BuildingFeedbackAudioCue.None;
        }
    }

    public static BuildingFeedbackSnapshot CreateSnapshot(
        BuildingLifecycleState state,
        int constructionHealth,
        int maxHealth,
        int currentHealth)
    {
        float constructionProgress = maxHealth > 0
            ? constructionHealth / (float)maxHealth
            : 0f;
        float healthRatio = maxHealth > 0
            ? currentHealth / (float)maxHealth
            : 0f;
        return new BuildingFeedbackSnapshot(state, constructionProgress, healthRatio);
    }
}

/// <summary>
/// Runtime presentation adapter. It is added to legacy building prefabs at
/// runtime, so existing scenes remain valid while every building exposes the
/// same state/progress contract. The only effect used is the existing mesh and
/// the existing construction audio source.
/// </summary>
public sealed class BuildingFeedbackEmitter : MonoBehaviour
{
    private Building _building;
    private BuildingManager _manager;
    private AudioClip _constructionSound;
    private AudioClip _operationalSound;
    private BuildingFeedbackSnapshot _lastSnapshot;
    private bool _hasSnapshot;

    public event Action<BuildingFeedbackSnapshot> FeedbackChanged;

    public BuildingFeedbackSnapshot Snapshot { get { return _lastSnapshot; } }
    public bool HasSnapshot { get { return _hasSnapshot; } }

    public void Bind(
        Building building,
        BuildingManager manager,
        AudioClip constructionSound,
        AudioClip operationalSound)
    {
        _building = building;
        _manager = manager;
        _constructionSound = constructionSound;
        _operationalSound = operationalSound;
    }

    public void Publish(BuildingFeedbackSnapshot snapshot)
    {
        bool stateChanged = !_hasSnapshot || _lastSnapshot.lifecycle != snapshot.lifecycle;
        _lastSnapshot = snapshot;
        _hasSnapshot = true;
        ApplyVisual(snapshot);
        if (stateChanged)
            ApplyAudio(snapshot);
        FeedbackChanged?.Invoke(snapshot);
    }

    private void ApplyVisual(BuildingFeedbackSnapshot snapshot)
    {
        if (_manager == null || _manager.meshRenderer == null)
            return;

        Renderer renderer = _manager.meshRenderer;
        renderer.enabled = snapshot.visual != BuildingFeedbackVisualCue.Destroyed;

        // Placement materials are transient UI feedback. They must never
        // survive into the operational state, even if a construction event
        // arrives out of order during spawn/load. Restore the authored
        // material set before applying the health overlay.
        if (snapshot.visual == BuildingFeedbackVisualCue.Operational
            && _building != null)
            _building.SetMaterials(BuildingPlacement.FIXED);

        // Damage is communicated without inventing a texture or model. A
        // property block preserves owner materials and is safe for both the
        // legacy and current Unity materials.
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.Clear();
        if (snapshot.visual == BuildingFeedbackVisualCue.Damaged)
        {
            Color tint = Color.Lerp(
                Color.white,
                new Color(1f, 0.35f, 0.35f),
                1f - snapshot.healthRatio);
            block.SetColor("_Color", tint);
            block.SetColor("_BaseColor", tint);
        }
        renderer.SetPropertyBlock(block);
    }

    private void ApplyAudio(BuildingFeedbackSnapshot snapshot)
    {
        if (_manager == null || _manager.ambientSource == null)
            return;

        AudioSource source = _manager.ambientSource;
        switch (snapshot.audio)
        {
            case BuildingFeedbackAudioCue.ConstructionLoop:
                SetLoop(source, _constructionSound);
                break;
            case BuildingFeedbackAudioCue.DamageWarning:
                // Existing construction-site audio is the project-approved
                // fallback until a dedicated damage clip is authored.
                SetLoop(source, _constructionSound);
                break;
            case BuildingFeedbackAudioCue.OperationalAmbient:
                SetLoop(source, _operationalSound);
                break;
            case BuildingFeedbackAudioCue.Stopped:
            case BuildingFeedbackAudioCue.None:
                source.Stop();
                break;
        }
    }

    private static void SetLoop(AudioSource source, AudioClip clip)
    {
        if (clip == null)
        {
            source.Stop();
            return;
        }
        source.clip = clip;
        source.loop = true;
        if (!source.isPlaying)
            source.Play();
    }
}
