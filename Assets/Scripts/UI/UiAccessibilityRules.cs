using System;
using System.Globalization;

/// <summary>Semantic state shared by keyboard, screen-reader and visual UI adapters.</summary>
public enum UiSemanticState
{
    Hidden,
    Disabled,
    Available,
    Selected,
    Warning
}

public enum UiHealthBand
{
    Unknown,
    Healthy,
    Wounded,
    Critical,
    Destroyed
}

public sealed class UnitHudAccessibilityState
{
    public string code;
    public string label;
    public string status;
    public string healthText;
    public float healthRatio;
    public UiHealthBand healthBand;
    public UiSemanticState semanticState;
}

public sealed class MinimapAccessibilityState
{
    public bool available;
    public string label;
    public float normalizedX;
    public float normalizedY;
    public UiSemanticState semanticState;
}

/// <summary>
/// Pure UI accessibility projections. Runtime panels can consume these values
/// without coupling narration, contrast or keyboard navigation to Unity objects.
/// </summary>
public static class UiAccessibilityRules
{
    public static UnitHudAccessibilityState UnitState(
        string code,
        string displayName,
        int currentHealth,
        int maximumHealth,
        bool selected,
        bool visible = true)
    {
        float ratio = HealthRatio(currentHealth, maximumHealth);
        UiHealthBand band = HealthBand(currentHealth, maximumHealth);
        string name = string.IsNullOrWhiteSpace(displayName) ? code ?? string.Empty : displayName.Trim();
        return new UnitHudAccessibilityState
        {
            code = (code ?? string.Empty).Trim(),
            label = name,
            status = StatusText(selected, visible),
            healthText = HealthText(currentHealth, maximumHealth, band),
            healthRatio = ratio,
            healthBand = band,
            semanticState = !visible ? UiSemanticState.Hidden :
                (selected ? UiSemanticState.Selected : UiSemanticState.Available)
        };
    }

    public static MinimapAccessibilityState MinimapState(
        float worldX,
        float worldZ,
        float terrainWidth,
        float terrainDepth,
        bool enabled = true)
    {
        bool valid = enabled && terrainWidth > 0f && terrainDepth > 0f;
        float x = valid ? Clamp01(worldX / terrainWidth) : 0f;
        float y = valid ? Clamp01(worldZ / terrainDepth) : 0f;
        return new MinimapAccessibilityState
        {
            available = valid,
            label = valid ? "Minimapa. Posição " + Percent(x) + ", " + Percent(y) : "Minimapa indisponível",
            normalizedX = x,
            normalizedY = y,
            semanticState = valid ? UiSemanticState.Available : UiSemanticState.Disabled
        };
    }

    public static float ClampVolumeDb(float value)
    {
        return Math.Max(-80f, Math.Min(0f, value));
    }

    public static string VolumeText(float value)
    {
        float clamped = ClampVolumeDb(value);
        return clamped <= -80f ? "Silenciado" : "Volume " + Math.Round(clamped, 1).ToString("0.0", CultureInfo.InvariantCulture) + " dB";
    }

    public static float HealthRatio(int currentHealth, int maximumHealth)
    {
        if (maximumHealth <= 0) return 0f;
        return Clamp01((float)currentHealth / maximumHealth);
    }

    public static UiHealthBand HealthBand(int currentHealth, int maximumHealth)
    {
        if (maximumHealth <= 0 || currentHealth <= 0) return UiHealthBand.Destroyed;
        float ratio = HealthRatio(currentHealth, maximumHealth);
        if (ratio <= 0.25f) return UiHealthBand.Critical;
        if (ratio < 0.75f) return UiHealthBand.Wounded;
        return UiHealthBand.Healthy;
    }

    public static string HealthText(int currentHealth, int maximumHealth, UiHealthBand band)
    {
        if (band == UiHealthBand.Unknown) return "Saúde desconhecida";
        return "Saúde " + Math.Max(0, currentHealth) + " de " + Math.Max(0, maximumHealth) + ". " + band switch
        {
            UiHealthBand.Healthy => "Saudável",
            UiHealthBand.Wounded => "Ferido",
            UiHealthBand.Critical => "Crítico",
            UiHealthBand.Destroyed => "Destruído",
            _ => "Indisponível"
        };
    }

    private static string StatusText(bool selected, bool visible)
    {
        if (!visible) return "Oculto";
        return selected ? "Selecionado" : "Disponível";
    }

    private static float Clamp01(float value)
    {
        return Math.Max(0f, Math.Min(1f, value));
    }

    private static string Percent(float value)
    {
        return Math.Round(value * 100f).ToString("0") + "%";
    }
}
