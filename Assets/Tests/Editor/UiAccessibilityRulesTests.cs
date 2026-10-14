using NUnit.Framework;

public sealed class UiAccessibilityRulesTests
{
    [Test]
    public void HealthProjectionUsesStableBandsAndText()
    {
        UnitHudAccessibilityState healthy = UiAccessibilityRules.UnitState(
            "archer", "Arqueiro", 100, 100, true);
        Assert.That(healthy.healthBand, Is.EqualTo(UiHealthBand.Healthy));
        Assert.That(healthy.healthRatio, Is.EqualTo(1f));
        Assert.That(healthy.status, Is.EqualTo("Selecionado"));
        Assert.That(healthy.healthText, Does.Contain("Saudável"));

        UnitHudAccessibilityState critical = UiAccessibilityRules.UnitState(
            "archer", "Arqueiro", 1, 100, false);
        Assert.That(critical.healthBand, Is.EqualTo(UiHealthBand.Critical));
        Assert.That(critical.healthText, Does.Contain("Crítico"));
    }

    [Test]
    public void InvalidHealthCannotProduceNaNOrInfinity()
    {
        UnitHudAccessibilityState destroyed = UiAccessibilityRules.UnitState(
            "", "", -10, 0, false, true);
        Assert.That(destroyed.healthRatio, Is.EqualTo(0f));
        Assert.That(destroyed.healthBand, Is.EqualTo(UiHealthBand.Destroyed));
        Assert.That(destroyed.healthText, Does.Contain("Destruído"));
    }

    [Test]
    public void MinimapProjectionClampsCoordinatesAndDescribesAvailability()
    {
        MinimapAccessibilityState position = UiAccessibilityRules.MinimapState(120f, -5f, 100f, 100f);
        Assert.That(position.available, Is.True);
        Assert.That(position.normalizedX, Is.EqualTo(1f));
        Assert.That(position.normalizedY, Is.EqualTo(0f));
        Assert.That(position.label, Does.Contain("100%"));

        MinimapAccessibilityState unavailable = UiAccessibilityRules.MinimapState(0f, 0f, 0f, 100f);
        Assert.That(unavailable.semanticState, Is.EqualTo(UiSemanticState.Disabled));
        Assert.That(unavailable.label, Is.EqualTo("Minimapa indisponível"));
    }

    [Test]
    public void AudioProjectionClampsMixerRangeAndExposesMuteState()
    {
        Assert.That(UiAccessibilityRules.ClampVolumeDb(4f), Is.EqualTo(0f));
        Assert.That(UiAccessibilityRules.ClampVolumeDb(-100f), Is.EqualTo(-80f));
        Assert.That(UiAccessibilityRules.VolumeText(-80f), Is.EqualTo("Silenciado"));
        Assert.That(UiAccessibilityRules.VolumeText(-6f), Is.EqualTo("Volume -6.0 dB"));
    }
}
