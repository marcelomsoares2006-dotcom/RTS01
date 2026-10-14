using NUnit.Framework;

public sealed class ConstructionLockstepContractsTests
{
    [Test]
    public void AcceptsMonotonicCommandsAndConfirmsFingerprint()
    {
        ConstructionLockstepCoordinator coordinator = new ConstructionLockstepCoordinator();
        ConstructionReplayCommand first = Command("build-1", 10, "house");
        ConstructionLockstepConfirmation confirmation;
        string before = coordinator.Fingerprint;
        Assert.That(coordinator.Submit(first, before, out confirmation), Is.EqualTo(ConstructionLockstepResult.Accepted));
        Assert.AreEqual(10, confirmation.confirmedThroughTick);
        Assert.IsTrue(confirmation.accepted);
        Assert.AreEqual(coordinator.Fingerprint, confirmation.fingerprint);
    }

    [Test]
    public void RejectsLateDuplicateAndWrongFingerprintWithoutMutation()
    {
        ConstructionLockstepCoordinator coordinator = new ConstructionLockstepCoordinator();
        ConstructionLockstepConfirmation confirmation;
        Assert.That(coordinator.Submit(Command("first", 4, "house"), coordinator.Fingerprint, out confirmation), Is.EqualTo(ConstructionLockstepResult.Accepted));
        string stable = coordinator.Fingerprint;
        Assert.That(coordinator.Submit(Command("late", 3, "tower"), stable, out confirmation), Is.EqualTo(ConstructionLockstepResult.StaleTick));
        Assert.AreEqual(stable, coordinator.Fingerprint);
        Assert.That(coordinator.Submit(Command("first", 5, "tower"), stable, out confirmation), Is.EqualTo(ConstructionLockstepResult.DuplicateCommandId));
        Assert.That(coordinator.Submit(Command("new", 5, "tower"), "wrong", out confirmation), Is.EqualTo(ConstructionLockstepResult.FingerprintMismatch));
        Assert.AreEqual(stable, coordinator.Fingerprint);
    }

    [Test]
    public void RejectsInvalidOwnerAndMissingCommandId()
    {
        ConstructionLockstepCoordinator coordinator = new ConstructionLockstepCoordinator();
        ConstructionLockstepConfirmation confirmation;
        ConstructionReplayCommand owner = Command("owner", 1, "house");
        owner.ownerId = -1;
        Assert.That(coordinator.Submit(owner, coordinator.Fingerprint, out confirmation), Is.EqualTo(ConstructionLockstepResult.InvalidOwner));
        ConstructionReplayCommand missing = Command("", 1, "house");
        Assert.That(coordinator.Submit(missing, coordinator.Fingerprint, out confirmation), Is.EqualTo(ConstructionLockstepResult.MissingCommandId));
        Assert.AreEqual(-1, coordinator.LastAcceptedTick);
    }

    [Test]
    public void SessionOwnerRejectsCommandsFromAnotherOwnerWithoutMutation()
    {
        ConstructionLockstepCoordinator coordinator = new ConstructionLockstepCoordinator(7);
        ConstructionLockstepConfirmation confirmation;
        Assert.That(coordinator.Submit(Command("wrong-owner", 1, "house"), coordinator.Fingerprint, out confirmation), Is.EqualTo(ConstructionLockstepResult.InvalidOwner));
        ConstructionReplayCommand valid = Command("valid", 1, "house");
        valid.ownerId = 7;
        Assert.That(coordinator.Submit(7, valid, coordinator.Fingerprint, out confirmation), Is.EqualTo(ConstructionLockstepResult.Accepted));
        Assert.AreEqual(1, coordinator.LastAcceptedTick);
    }

    private static ConstructionReplayCommand Command(string id, long tick, string code)
    {
        return new ConstructionReplayCommand {
            commandId = id, ownerId = 0, simulationTick = tick,
            issuedAtMilliseconds = tick, priority = 1,
            kind = ConstructionReplayCommandKind.ConstructBuilding,
            targetCode = code, quantity = 1
        };
    }
}
