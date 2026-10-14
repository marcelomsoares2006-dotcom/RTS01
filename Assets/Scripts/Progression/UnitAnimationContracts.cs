using System;
using System.Collections.Generic;

/// <summary>
/// The smallest animation vocabulary shared by deterministic unit logic and
/// the eventual Unity Animator binding. It deliberately contains no Animator,
/// Transform, clip or asset reference so it can be audited before Unity exists.
/// </summary>
public enum UnitAnimationState
{
    Idle,
    Move,
    Attack,
    Death
}

public enum UnitAnimationRigKind
{
    Humanoid,
    MountedHumanoid,
    SiegeMechanical
}

public enum UnitAnimationContractResult
{
    Accepted,
    MissingProfile,
    InvalidUnitCode,
    InvalidRigKind,
    MissingBinding,
    DuplicateState,
    InvalidClipCode,
    InvalidLoopFlag,
    InvalidTiming,
    InvalidTransition
}

[Serializable]
public sealed class UnitAnimationBinding
{
    public UnitAnimationState state;
    public string clipCode;
    public bool loops;
    public float minimumDuration;

    public UnitAnimationBinding() { }

    public UnitAnimationBinding(UnitAnimationState state, string clipCode, bool loops,
        float minimumDuration)
    {
        this.state = state;
        this.clipCode = clipCode;
        this.loops = loops;
        this.minimumDuration = minimumDuration;
    }
}

[Serializable]
public sealed class UnitAnimationProfile
{
    public string unitCode;
    public UnitAnimationRigKind rigKind;
    public List<UnitAnimationBinding> bindings = new List<UnitAnimationBinding>();

    public UnitAnimationProfile() { }

    public UnitAnimationProfile(string unitCode, UnitAnimationRigKind rigKind,
        params UnitAnimationBinding[] bindings)
    {
        this.unitCode = unitCode;
        this.rigKind = rigKind;
        if (bindings != null) this.bindings.AddRange(bindings);
    }
}

/// <summary>
/// Editor-free profiles for the first three production units. The clip codes
/// are stable contracts, not claims that clips or rigs have been imported.
/// </summary>
public static class UnitAnimationContractCatalog
{
    public const string ArcherCode = "archer";
    public const string HorsemanCode = "horseman";
    public const string BatteringRamCode = "battering_ram";

    public static UnitAnimationProfile Archer { get { return Create(ArcherCode, UnitAnimationRigKind.Humanoid); } }
    public static UnitAnimationProfile Horseman { get { return Create(HorsemanCode, UnitAnimationRigKind.MountedHumanoid); } }
    public static UnitAnimationProfile BatteringRam { get { return Create(BatteringRamCode, UnitAnimationRigKind.SiegeMechanical); } }

    public static UnitAnimationProfile Find(string unitCode)
    {
        if (string.Equals(unitCode, ArcherCode, StringComparison.OrdinalIgnoreCase)) return Archer;
        if (string.Equals(unitCode, HorsemanCode, StringComparison.OrdinalIgnoreCase)) return Horseman;
        if (string.Equals(unitCode, BatteringRamCode, StringComparison.OrdinalIgnoreCase)) return BatteringRam;
        return null;
    }

    public static UnitAnimationContractResult Validate(UnitAnimationProfile profile)
    {
        if (profile == null) return UnitAnimationContractResult.MissingProfile;
        if (string.IsNullOrWhiteSpace(profile.unitCode)) return UnitAnimationContractResult.InvalidUnitCode;
        if (profile.bindings == null) return UnitAnimationContractResult.MissingBinding;
        for (int i = 0; i < profile.bindings.Count; i++)
        {
            UnitAnimationBinding binding = profile.bindings[i];
            if (binding == null) return UnitAnimationContractResult.MissingBinding;
            if (string.IsNullOrWhiteSpace(binding.clipCode)) return UnitAnimationContractResult.InvalidClipCode;
            if (binding.minimumDuration <= 0f) return UnitAnimationContractResult.InvalidTiming;
            if ((binding.state == UnitAnimationState.Idle || binding.state == UnitAnimationState.Move)
                && !binding.loops) return UnitAnimationContractResult.InvalidLoopFlag;
            if ((binding.state == UnitAnimationState.Attack || binding.state == UnitAnimationState.Death)
                && binding.loops) return UnitAnimationContractResult.InvalidLoopFlag;
            for (int j = 0; j < i; j++)
                if (profile.bindings[j] != null && profile.bindings[j].state == binding.state)
                    return UnitAnimationContractResult.DuplicateState;
        }
        for (int state = (int)UnitAnimationState.Idle; state <= (int)UnitAnimationState.Death; state++)
        {
            bool found = false;
            for (int i = 0; i < profile.bindings.Count; i++)
                if (profile.bindings[i] != null && (int)profile.bindings[i].state == state) { found = true; break; }
            if (!found) return UnitAnimationContractResult.MissingBinding;
        }
        return UnitAnimationContractResult.Accepted;
    }

    /// <summary>Death wins over attack, then movement wins over idle.</summary>
    public static UnitAnimationState ResolveState(bool isDead, bool isAttacking, bool isMoving)
    {
        if (isDead) return UnitAnimationState.Death;
        if (isAttacking) return UnitAnimationState.Attack;
        return isMoving ? UnitAnimationState.Move : UnitAnimationState.Idle;
    }

    public static bool IsTransitionAllowed(UnitAnimationState from, UnitAnimationState to)
    {
        if (from == to) return true;
        if (from == UnitAnimationState.Death) return false;
        if (to == UnitAnimationState.Death) return true;
        return true;
    }

    public static string ClipCode(UnitAnimationProfile profile, UnitAnimationState state)
    {
        if (profile == null || profile.bindings == null) return string.Empty;
        for (int i = 0; i < profile.bindings.Count; i++)
            if (profile.bindings[i] != null && profile.bindings[i].state == state)
                return profile.bindings[i].clipCode == null ? string.Empty : profile.bindings[i].clipCode.Trim();
        return string.Empty;
    }

    private static UnitAnimationProfile Create(string unitCode, UnitAnimationRigKind rigKind)
    {
        return new UnitAnimationProfile(unitCode, rigKind,
            new UnitAnimationBinding(UnitAnimationState.Idle, unitCode + "_idle", true, 0.1f),
            new UnitAnimationBinding(UnitAnimationState.Move, unitCode + "_move", true, 0.1f),
            new UnitAnimationBinding(UnitAnimationState.Attack, unitCode + "_attack", false, 0.1f),
            new UnitAnimationBinding(UnitAnimationState.Death, unitCode + "_death", false, 0.1f));
    }
}
