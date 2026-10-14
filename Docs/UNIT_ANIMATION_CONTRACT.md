# Unit animation contract

The first military units now share an editor-free animation boundary in
`Assets/Scripts/Progression/UnitAnimationContracts.cs`:

| Unit | Code | Rig boundary | Required states |
| --- | --- | --- | --- |
| Archer | `archer` | Humanoid | `archer_idle`, `archer_move`, `archer_attack`, `archer_death` |
| Horseman | `horseman` | MountedHumanoid | `horseman_idle`, `horseman_move`, `horseman_attack`, `horseman_death` |
| Battering Ram | `battering_ram` | SiegeMechanical | `battering_ram_idle`, `battering_ram_move`, `battering_ram_attack`, `battering_ram_death` |

`ResolveState` is deterministic: death wins over attack, attack wins over
movement, and movement wins over idle. Idle and move bindings loop; attack and
death bindings are one-shot. Death is terminal in the contract. These rules
express the gameplay state boundary and stable clip names without pretending
that a Unity `Animator`, rig, clip, avatar or animation event already exists.

Unity work remains required for each unit: import or author the model and clips,
choose the declared rig type, validate skin weights and avatar/constraints,
create the Animator Controller, bind the four clip names, and verify attack
timing, root motion, hit events, mounted rider/horse synchronization and death
cleanup in PlayMode. The current repository does not promote any unit asset to
runtime-ready based on this contract alone.
