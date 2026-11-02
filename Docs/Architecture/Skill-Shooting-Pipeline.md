# Character skills and independent run skills

## Boundaries

- `CharacterManager` tracks characters in the scene; it does not schedule skills.
- `CharacterView.Skills` (`CharacterSkills`) owns only innate skills from `CharacterDataSO.Skills`.
- `CharacterControllerBase.Tick` advances innate skills before calling the derived movement hook. The view only forwards its frame update to the controller.
- The scene `SkillManager` independently owns run skills received from an external source. It has no character, character factory, position anchor or character stats dependency.
- `SkillRuntime` is a live behavior and cooldown entry. It has no owner/character dependency. Each collection decides when to tick and dispose its entries.
- `SkillFactory` currently creates only character-bound behaviors through explicit constructor calls. Combat and bonus calculations remain inside the behaviors.

## Innate skill flow

1. `CharacterView.Initialize` creates `CharacterSkills` from the character data before activating the pooled shell.
2. The factory creates fresh behaviors bound to that spawn; shared asset templates are never executed or mutated.
3. The base controller advances only the character's innate collection. The collection checks active/alive/spawn identity before each entry.
4. Disable freezes innate cooldowns. Death, despawn and destruction dispose only that character's innate skills and persistent modifiers.
5. Character cleanup never calls `SkillManager` or removes run registrations.

## Run skills: integration deferred

The card/reward source is not implemented. There are currently no gameplay callers that grant run skills.

The integration entry point is:

```csharp
skillManager.AddSkill(selectedDefinition, runtimeBehavior);
```

- A future external source supplies the `SkillDataSO` configuration and a separately created `ISkill` instance.
- The manager registers the instance and advances its cooldown through Zenject's tick. It does not create character-bound behaviors or discover assets automatically.
- The shared behavior template cannot be registered directly. Duplicate live definitions are rejected within the run collection.
- Position sourcing, standalone run-skill damage/range, card selection and progression are intentionally not connected yet. Do not obtain them from character runtime as an implicit fallback.
- Character disable, death, despawn or replacement does not reset or remove run skills.
- `LevelManager.RunReset` clears only run skills. Explicit run-exit coordination can call `ClearRunSkills()` later. Innate skills and their timers are untouched.

## Common scheduling rules

- New entries start ready. A successful `TryExecute()` starts cooldown; failure leaves the entry ready for a future tick.
- A skill attempts at most once per tick. Zero delta does not execute skills.
- Run registrations added during a manager tick wait until its next tick.
- Execution failure disposes the failed entry and is logged while other skills continue.
- Cleanup marks entries inactive before releasing modifiers and attempts every affected entry even when a disposer fails.

## Character behaviors

- `NormalAttackSkill` finds an opponent, checks contact and asks its character to deal damage.
- `ShootingSkill` owns cast/phase state and uses character stats only as part of this innate character-bound implementation.
- `RegenerationSkill` heals its injured owner; the sample heals 1 HP every 2 seconds after success.
- `EmergencyShieldSkill` grants 30 shield at <=20% HP, with a 5-second cooldown.
- `StatBonusSkill` applies once and owns an idempotent modifier lease. It grants only newly added defense-derived shield capacity, never refilling previously spent shield. Synchronous despawn during application releases the lease.

## Authoring

1. Configure a `SkillDataSO` using the inspector behavior selector.
2. For a new character behavior, implement its gameplay logic and add its constructor call to `SkillFactory`.
3. Equip innate assets in `CharacterDataSO.Skills`.
4. Connect the future card source to the independent manager entry point when run behavior creation and its inputs are defined.

Skill upgrades, permanent skill progression and selection UI are not implemented. Character/EXP level progression remains in the existing level services.
