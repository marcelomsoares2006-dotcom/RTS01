using UnityEngine;

/// <summary>
/// Serialized bridge between a military prefab and the editor-free animation
/// contract. It keeps profile and clip identities on the prefab while Unity
/// Animator controllers and imported clips are still being authored.
/// </summary>
[DisallowMultipleComponent]
public sealed class MilitaryUnitAnimationBinding : MonoBehaviour
{
    [SerializeField] private string profileCode;
    [SerializeField] private string idleClipCode;
    [SerializeField] private string moveClipCode;
    [SerializeField] private string attackClipCode;
    [SerializeField] private string deathClipCode;

    public string ProfileCode { get { return profileCode; } }

    public string ClipCode(UnitAnimationState state)
    {
        switch (state)
        {
            case UnitAnimationState.Idle: return idleClipCode;
            case UnitAnimationState.Move: return moveClipCode;
            case UnitAnimationState.Attack: return attackClipCode;
            case UnitAnimationState.Death: return deathClipCode;
            default: return string.Empty;
        }
    }

    /// <summary>
    /// Validates the serialized identity against the canonical contract. This
    /// does not require an Animator, Avatar or imported AnimationClip.
    /// </summary>
    public UnitAnimationContractResult Validate()
    {
        UnitAnimationProfile profile = UnitAnimationContractCatalog.Find(profileCode);
        if (profile == null) return UnitAnimationContractResult.MissingProfile;
        if (UnitAnimationContractCatalog.Validate(profile) != UnitAnimationContractResult.Accepted)
            return UnitAnimationContractResult.InvalidClipCode;

        for (int state = (int)UnitAnimationState.Idle;
             state <= (int)UnitAnimationState.Death; state++)
        {
            UnitAnimationState animationState = (UnitAnimationState)state;
            if (ClipCode(animationState) != UnitAnimationContractCatalog.ClipCode(profile, animationState))
                return UnitAnimationContractResult.InvalidClipCode;
        }
        return UnitAnimationContractResult.Accepted;
    }
}
