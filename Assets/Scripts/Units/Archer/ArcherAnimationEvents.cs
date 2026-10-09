using UnityEngine;

/// <summary>
/// Fica no mesmo GameObject do Animator (as animation events chegam aqui) e repassa ao controlador do Archer.
/// </summary>
public sealed class ArcherAnimationEvents : MonoBehaviour
{
    private ArcherCombatController _controller;

    private ArcherCombatController Controller
    {
        get
        {
            if (_controller == null) _controller = GetComponentInParent<ArcherCombatController>();
            return _controller;
        }
    }

    public void ArcherArrowGrab() { if (Controller != null) Controller.OnArrowGrab(); }
    public void ArcherArrowNock() { if (Controller != null) Controller.OnArrowNock(); }
    public void ArcherArrowRelease() { if (Controller != null) Controller.OnArrowRelease(); }
    public void ArcherAttackEnd() { if (Controller != null) Controller.OnAttackEnd(); }
}
