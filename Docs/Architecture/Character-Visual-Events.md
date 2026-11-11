# Character visual events

Gameplay publishes `CharacterVisualState` through `ICharacterVisualEventSource`.
`CharacterView` implements the interface; the `CharacterVisualHandle` on each
visual prefab subscribes during spawn and unsubscribes before pool return.
The interface also exposes movement/alive/facing snapshots for initial binding.

## Ownership

- `CharacterView.Move` publishes Idle/Move and facing changes only when they change.
  Player input and enemy AI keep their existing direction policies.
- Only bosses request Attack/Cast. `CharacterView` rejects these requests for
  Player and ordinary enemies. Their skills still execute and deal damage.
  For bosses, `SkillRuntime` requests Attack after a successful NormalAttack.
  Successful Shooting requests the skill asset's Shooting Visual State, default Cast.
  Only Attack/Cast are accepted for that field; Idle suppresses the request.
  Passive buffs, aura, regeneration and orbit behaviours do not request an action.
- Accepted nonlethal damage requests Hit. A lethal Player hit requests Lose;
  enemy death requests Death before death listeners execute. Win/Lose are Player-only.
  Animation requests do not deal damage or spawn bullets.
- `CharacterVisualHandle` owns facing and Animator playback. The switch maps enum
  states to prefab-configured full Animator state paths, hashed once on binding/Awake.
  A Cast mapping that is unavailable falls back to Attack.

## Prefabs

Both `Assets/_Project/Prefabs/CharacterType` prefabs have this outer hierarchy:

```text
Visual prefab root (CharacterVisualHandle, authored positive scale)
  FacingRoot (scale X +1 or -1)
    Model
      existing animated hierarchy
```

Blessed Gladiator retains the Animator on Rider below Model. Foraging Maggot's
Animator moved from the prefab root to Model with its original children, keeping
all clip paths relative to that Animator unchanged. Existing prefab root IDs,
GUIDs, root scales, character-data references, health UI and gameplay hitboxes
remain intact. Whole-model mirroring replaces per-sprite flipX changes.

| Prefab | Native facing | Idle | Move | Attack | Cast | Hit | Death |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Blessed Gladiator | Right | Idle | Walk | Disabled | Disabled | Stun | Dead1 |
| Foraging Maggot | Left | Idle | Walk | Disabled | Disabled | Hit | Dead |

Ordinary enemies damage the player on hitbox contact, recoil away and resume
chasing. This behaviour remains in NormalAttackSkill/EnemyController and does not
play an Attack animation. Player projectile shooting also leaves locomotion
animation unchanged. Boss AI and dedicated boss prefab setup are future work.

Inspector state names use full paths, e.g. `Base Layer.Walk`. The component keeps
the authored FacingRoot scale magnitude and applies only its X sign. It does not
mirror the shared Character root or its health canvas/hitbox.

## Playback and lifetime

Idle/Move are cached while an action plays. A new Attack/Cast can replay the same
enum; locomotion cannot interrupt actions. Hit does not interrupt another action.
When an action reaches one normalized cycle, playback returns to the latest
movement state. This also supports the pack's clips marked as looping.

Death, Win and Lose reject further state/facing changes, play one cycle and hold
their final pose. EnemyController still waits for Death before despawning.
GameManager.WinRun/LoseRun pause gameplay immediately and enter EndingWin/EndingLose,
which have no result-panel route in UIManager. CharacterView.PlayResultAnimation
locks Player movement/combat and forwards Win/Lose to CharacterVisualHandle.
Only its completion callback enters Won/Lost and opens the existing ResultController.
The callback checks run version and Player runtime generation to reject stale results.
If the visual is unavailable or its state is missing, the result is shown immediately
instead of leaving the game stuck; missing states produce a warning.

Blessed Gladiator maps Win to `Base Layer.Victory` and Lose to `Base Layer.Defeat`
in the existing pack controller. Enemy prefabs retain their Death mappings.

The Animator keeps its state on temporary visual disable. Events received while
hidden are cached for the next enable. On a new pooled spawn, binding resets the
Animator and local playback state; unbinding drops callbacks and restores facing
scale, Animator speed and update mode. Normal actions and enemy Death use gameplay
time; Player Win/Lose temporarily use UnscaledTime so they finish while gameplay is paused.

Implementation was not compiled or tested at the user's request; verify playback
and visual proportions in Unity Play Mode.
