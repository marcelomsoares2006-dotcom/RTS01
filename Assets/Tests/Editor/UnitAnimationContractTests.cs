using NUnit.Framework;

public sealed class UnitAnimationContractTests
{
    [Test]
    public void FirstMilitaryProfilesExposeAllFourRequiredStates()
    {
        Assert.That(UnitAnimationContractCatalog.Validate(UnitAnimationContractCatalog.Archer), Is.EqualTo(UnitAnimationContractResult.Accepted));
        Assert.That(UnitAnimationContractCatalog.Validate(UnitAnimationContractCatalog.Horseman), Is.EqualTo(UnitAnimationContractResult.Accepted));
        Assert.That(UnitAnimationContractCatalog.Validate(UnitAnimationContractCatalog.BatteringRam), Is.EqualTo(UnitAnimationContractResult.Accepted));
    }

    [Test]
    public void ProfilesUseTheCorrectRigBoundary()
    {
        Assert.That(UnitAnimationContractCatalog.Archer.rigKind, Is.EqualTo(UnitAnimationRigKind.Humanoid));
        Assert.That(UnitAnimationContractCatalog.Horseman.rigKind, Is.EqualTo(UnitAnimationRigKind.MountedHumanoid));
        Assert.That(UnitAnimationContractCatalog.BatteringRam.rigKind, Is.EqualTo(UnitAnimationRigKind.SiegeMechanical));
    }

    [Test]
    public void StateResolutionIsDeterministicAndDeathWins()
    {
        Assert.That(UnitAnimationContractCatalog.ResolveState(false, false, false), Is.EqualTo(UnitAnimationState.Idle));
        Assert.That(UnitAnimationContractCatalog.ResolveState(false, false, true), Is.EqualTo(UnitAnimationState.Move));
        Assert.That(UnitAnimationContractCatalog.ResolveState(false, true, true), Is.EqualTo(UnitAnimationState.Attack));
        Assert.That(UnitAnimationContractCatalog.ResolveState(true, true, true), Is.EqualTo(UnitAnimationState.Death));
    }

    [Test]
    public void DeathIsTerminalAndAllProfilesHaveStableClipCodes()
    {
        Assert.That(UnitAnimationContractCatalog.IsTransitionAllowed(UnitAnimationState.Death, UnitAnimationState.Idle), Is.False);
        foreach (UnitAnimationProfile profile in new[] {
            UnitAnimationContractCatalog.Archer,
            UnitAnimationContractCatalog.Horseman,
            UnitAnimationContractCatalog.BatteringRam })
        {
            Assert.That(UnitAnimationContractCatalog.ClipCode(profile, UnitAnimationState.Idle), Is.EqualTo(profile.unitCode + "_idle"));
            Assert.That(UnitAnimationContractCatalog.ClipCode(profile, UnitAnimationState.Move), Is.EqualTo(profile.unitCode + "_move"));
            Assert.That(UnitAnimationContractCatalog.ClipCode(profile, UnitAnimationState.Attack), Is.EqualTo(profile.unitCode + "_attack"));
            Assert.That(UnitAnimationContractCatalog.ClipCode(profile, UnitAnimationState.Death), Is.EqualTo(profile.unitCode + "_death"));
        }
    }

    [Test]
    public void InvalidProfileCannotSkipRequiredStateOrLoopOneShotClip()
    {
        UnitAnimationProfile profile = UnitAnimationContractCatalog.Archer;
        profile.bindings.RemoveAt(3);
        Assert.That(UnitAnimationContractCatalog.Validate(profile), Is.EqualTo(UnitAnimationContractResult.MissingBinding));
        profile = UnitAnimationContractCatalog.Archer;
        profile.bindings[2].loops = true;
        Assert.That(UnitAnimationContractCatalog.Validate(profile), Is.EqualTo(UnitAnimationContractResult.InvalidLoopFlag));
    }
}
