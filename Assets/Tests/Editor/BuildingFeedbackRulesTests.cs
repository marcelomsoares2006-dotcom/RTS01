using NUnit.Framework;

public sealed class BuildingFeedbackRulesTests
{
    [TestCase(BuildingLifecycleState.Blueprint, BuildingFeedbackVisualCue.Blueprint, BuildingFeedbackAudioCue.None)]
    [TestCase(BuildingLifecycleState.UnderConstruction, BuildingFeedbackVisualCue.UnderConstruction, BuildingFeedbackAudioCue.ConstructionLoop)]
    [TestCase(BuildingLifecycleState.Operational, BuildingFeedbackVisualCue.Operational, BuildingFeedbackAudioCue.OperationalAmbient)]
    [TestCase(BuildingLifecycleState.Damaged, BuildingFeedbackVisualCue.Damaged, BuildingFeedbackAudioCue.DamageWarning)]
    [TestCase(BuildingLifecycleState.Destroyed, BuildingFeedbackVisualCue.Destroyed, BuildingFeedbackAudioCue.Stopped)]
    [TestCase(BuildingLifecycleState.Ruined, BuildingFeedbackVisualCue.Destroyed, BuildingFeedbackAudioCue.Stopped)]
    public void LifecycleMapsToCommunicableVisualAndAudioCues(
        BuildingLifecycleState lifecycle,
        BuildingFeedbackVisualCue visual,
        BuildingFeedbackAudioCue audio)
    {
        Assert.That(BuildingFeedbackRules.VisualFor(lifecycle), Is.EqualTo(visual));
        Assert.That(BuildingFeedbackRules.AudioFor(lifecycle), Is.EqualTo(audio));
    }

    [Test]
    public void SnapshotClampsProgressAndHealth()
    {
        BuildingFeedbackSnapshot snapshot = BuildingFeedbackRules.CreateSnapshot(
            BuildingLifecycleState.Damaged,
            150,
            100,
            -20);

        Assert.That(snapshot.constructionProgress, Is.EqualTo(1f));
        Assert.That(snapshot.healthRatio, Is.EqualTo(0f));
        Assert.That(snapshot.visual, Is.EqualTo(BuildingFeedbackVisualCue.Damaged));
    }

    [Test]
    public void ZeroMaxHealthProducesSafeRatios()
    {
        BuildingFeedbackSnapshot snapshot = BuildingFeedbackRules.CreateSnapshot(
            BuildingLifecycleState.Blueprint,
            10,
            0,
            10);

        Assert.That(snapshot.constructionProgress, Is.EqualTo(0f));
        Assert.That(snapshot.healthRatio, Is.EqualTo(0f));
    }
}
