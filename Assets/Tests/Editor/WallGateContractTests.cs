using NUnit.Framework;

public sealed class WallGateContractTests
{
    private static WallGateContract Ready(WallGatePieceKind kind, string code, string family)
    {
        return new WallGateContract
        {
            code = code,
            familyCode = family,
            pieceKind = kind,
            runtimeEnabled = true,
            assetsReady = true,
            connectionRadius = 0.5f
        };
    }

    [Test]
    public void PlannedEntryNeverEntersPlacementRuntime()
    {
        WallGateContract planned = new WallGateContract
        {
            code = "wall_future",
            familyCode = "stone_future",
            runtimeEnabled = false,
            assetsReady = false
        };

        WallGatePlacementResult result = WallGateContractRules.EvaluatePlacement(planned, new WallGatePlacementObservation
        {
            footprintClear = true,
            terrainSupported = true,
            endpointAvailable = true
        });

        Assert.That(result, Is.EqualTo(WallGatePlacementResult.PlannedOnly));
    }

    [Test]
    public void PlacementRequiresClearFootprintAndSupportedTerrain()
    {
        WallGateContract wall = Ready(WallGatePieceKind.Segment, "wall_segment", "stone");
        Assert.That(WallGateContractRules.EvaluatePlacement(wall, new WallGatePlacementObservation
        {
            footprintClear = false,
            terrainSupported = true
        }), Is.EqualTo(WallGatePlacementResult.InvalidFootprint));

        Assert.That(WallGateContractRules.EvaluatePlacement(wall, new WallGatePlacementObservation
        {
            footprintClear = true,
            terrainSupported = true,
            terrainSlope = 20f
        }), Is.EqualTo(WallGatePlacementResult.InvalidTerrain));

        Assert.That(WallGateContractRules.EvaluatePlacement(wall, new WallGatePlacementObservation
        {
            footprintClear = true,
            terrainSupported = true,
            terrainSlope = -1f
        }), Is.EqualTo(WallGatePlacementResult.InvalidTerrain));
    }

    [Test]
    public void ConnectionRequiresSameFamilyAndEndpointDistance()
    {
        WallGateContract first = Ready(WallGatePieceKind.Segment, "wall_a", "stone");
        WallGateContract second = Ready(WallGatePieceKind.Corner, "wall_b", "stone");
        WallGateContract wood = Ready(WallGatePieceKind.Segment, "wall_c", "wood");

        Assert.That(WallGateContractRules.EvaluateConnection(first, second, 0.25f, true, true),
            Is.EqualTo(WallGateConnectionResult.Connected));
        Assert.That(WallGateContractRules.EvaluateConnection(first, second, 0.75f, true, true),
            Is.EqualTo(WallGateConnectionResult.InvalidDistance));
        Assert.That(WallGateContractRules.EvaluateConnection(first, wood, 0.1f, true, true),
            Is.EqualTo(WallGateConnectionResult.IncompatibleFamily));
    }

    [Test]
    public void GateOpeningRequiresOwnerAndClearance()
    {
        WallGateContract gate = Ready(WallGatePieceKind.Gate, "gate", "stone");
        Assert.That(WallGateContractRules.EvaluateOpening(gate, WallGateOpeningState.Closed,
            WallGateOpeningState.Open, false, true), Is.EqualTo(WallGateOpeningResult.InvalidOwner));
        Assert.That(WallGateContractRules.EvaluateOpening(gate, WallGateOpeningState.Closed,
            WallGateOpeningState.Open, true, false), Is.EqualTo(WallGateOpeningResult.Blocked));
        Assert.That(WallGateContractRules.EvaluateOpening(gate, WallGateOpeningState.Closed,
            WallGateOpeningState.Open, true, true), Is.EqualTo(WallGateOpeningResult.Allowed));
    }

    [Test]
    public void PlacementAndConnectionRequireMatchingOwnershipAndSpacing()
    {
        WallGateContract first = Ready(WallGatePieceKind.Segment, "wall_a", "stone");
        WallGateContract second = Ready(WallGatePieceKind.Segment, "wall_b", "stone");
        first.ownerId = 3;
        first.minimumSpacing = 2f;
        Assert.That(WallGateContractRules.EvaluatePlacement(first, new WallGatePlacementObservation
        {
            footprintClear = true, terrainSupported = true, ownerId = 4
        }), Is.EqualTo(WallGatePlacementResult.InvalidOwner));
        Assert.That(WallGateContractRules.EvaluatePlacement(first, new WallGatePlacementObservation
        {
            footprintClear = true, terrainSupported = true, ownerId = 3,
            spacingAvailable = true, nearestPieceDistance = 1f
        }), Is.EqualTo(WallGatePlacementResult.InvalidSpacing));
        Assert.That(WallGateContractRules.EvaluatePlacement(first, new WallGatePlacementObservation
        {
            footprintClear = true, terrainSupported = true, ownerId = 3
        }), Is.EqualTo(WallGatePlacementResult.InvalidSpacing));

        second.ownerId = 4;
        Assert.That(WallGateContractRules.EvaluateConnection(first, second, 0.1f, true, true),
            Is.EqualTo(WallGateConnectionResult.InvalidOwner));
    }

    [Test]
    public void ConnectionRejectsNonFiniteEndpointDistance()
    {
        WallGateContract first = Ready(WallGatePieceKind.Segment, "wall_a", "stone");
        WallGateContract second = Ready(WallGatePieceKind.Segment, "wall_b", "stone");
        Assert.That(WallGateContractRules.EvaluateConnection(first, second, float.NaN, true, true),
            Is.EqualTo(WallGateConnectionResult.InvalidDistance));
        Assert.That(WallGateContractRules.EvaluateConnection(first, second, float.PositiveInfinity, true, true),
            Is.EqualTo(WallGateConnectionResult.InvalidDistance));
    }

    [Test]
    public void DestroyedPiecesCannotConnectOrOpen()
    {
        WallGateContract gate = Ready(WallGatePieceKind.Gate, "gate", "stone");
        gate.lifecycle = WallGateLifecycleState.Destroyed;
        Assert.That(WallGateContractRules.EvaluateOpening(gate, WallGateOpeningState.Closed,
            WallGateOpeningState.Open, true, true), Is.EqualTo(WallGateOpeningResult.InvalidLifecycle));
        Assert.That(WallGateContractRules.EvaluateConnection(gate,
            Ready(WallGatePieceKind.Segment, "wall", "stone"), 0.1f, true, true),
            Is.EqualTo(WallGateConnectionResult.InvalidLifecycle));
    }
}
