using UnityEngine;

/// <summary>
/// Demo driver for the Heraldic Knight: cycles the four imported actions (no Idle exists in the
/// source pack). Keys 1-4 select an action, Space toggles the automatic cycle.
/// Attacks are one-shot states; the Animator Controller returns them to Walking by exit time.
/// </summary>
public class HeraldicKnightDemo : MonoBehaviour
{
    public static readonly string[] States = { "Walking", "Running", "Attack", "TripleCombo" };
    public static readonly string[] Triggers = { "Walk", "Run", "Attack", "TripleCombo" };
    private static readonly string[] Labels = { "1 Walking (loop)", "2 Running (loop)", "3 Attack", "4 Triple Combo Attack" };

    public Animator knight;
    public TextMesh label;
    public bool autoCycle = true;
    public float locomotionSeconds = 4f;

    private int current;
    private float stateStarted;
    private bool attackEntered;

    public int Current => current;

    private void Start()
    {
        Select(0);
    }

    public void Select(int index)
    {
        current = Mathf.Clamp(index, 0, States.Length - 1);
        stateStarted = Time.time;
        attackEntered = false;
        // A trigger that cannot fire (e.g. Walk while already walking) would otherwise stay pending
        // and hijack the next action.
        foreach (string trigger in Triggers) knight.ResetTrigger(trigger);
        knight.SetTrigger(Triggers[current]);
        if (label != null) label.text = Labels[current];
    }

    private void Update()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        for (int i = 0; i < States.Length; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { autoCycle = false; Select(i); }
        if (Input.GetKeyDown(KeyCode.Space)) autoCycle = !autoCycle;
#endif
        if (current >= 2)
        {
            AnimatorStateInfo info = knight.GetCurrentAnimatorStateInfo(0);
            if (info.IsName(States[current])) attackEntered = true;
            else if (attackEntered && !knight.IsInTransition(0) && label != null) label.text = Labels[0] + " (after attack)";
        }
        if (!autoCycle) return;
        bool finished = current < 2
            ? Time.time - stateStarted > locomotionSeconds
            : attackEntered && knight.GetCurrentAnimatorStateInfo(0).IsName(States[0]) && !knight.IsInTransition(0);
        if (finished) Select((current + 1) % States.Length);
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(12, 10, 520, 60),
            $"Heraldic Knight demo - {Labels[current]}\nKeys 1-4: choose action   Space: auto cycle {(autoCycle ? "ON" : "OFF")}");
    }
}
