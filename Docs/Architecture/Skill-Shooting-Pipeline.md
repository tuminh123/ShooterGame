# Independent skill scheduling and character composition

## Boundaries

- ISkill has only bool TryExecute(). It does not receive a character, target or shared capability context.
- SkillManager is pure C#: two registration lists, cooldowns, a reusable tick snapshot and cleanup tokens. It has no Unity or Character dependency.
- CharacterManager tracks initialized characters and publishes added, removed and died events. CharacterDataSO and character controllers do not refer to skills or shooting.
- Integration/CharacterSkills/CharacterSkillBridge is the composition boundary. It observes character lifetime, creates per-registration behavior instances and supplies callbacks.
- CharacterSkillLoadoutSO maps CharacterType to SkillDataSO assets. The mapping lives outside both systems.

## Configuration and execution

SkillDataSO retains Data, Visual and a SerializeReference ISkill behavior template. Never register that shared template directly. The bridge copies its configuration into a fresh behavior with dependencies appropriate to that character's spawn identity.

SkillManager.Add receives a behavior, cooldown, run/innate scope and an optional generic availability predicate. All skills execute automatically. New registrations start ready; success starts cooldown, failure leaves the skill ready for a future tick. A zero cooldown permits at most one attempt per tick. There are no active/passive or manual-button categories.

The availability predicate supplied by the bridge freezes timers while a character is inactive. It checks runtime identity and alive state. Death/despawn disposes the corresponding tokens before shell reuse. Applying a persistent stat bonus owns an idempotent lease; a late lease returned after synchronous despawn is disposed immediately.

Callbacks may remove or add skills during a tick. A reusable snapshot prevents enumeration corruption; newly added entries wait until the next tick. A behavior exception removes that registration, allows other entries to finish and is reported by the bridge. Cleanup attempts every registration even when a disposer fails.

Level and EXP remain in LevelManager/RunLevelService. The bridge exposes AddRunSkill(character, selectedAsset) for a future selection UI. Duplicate run selections currently return false; no upgrade formula or revival policy is invented. ClearRunSkills removes the run scope, and RunReset invokes it automatically. A future explicit end-run coordinator should call the same method on leaving a run.

## Existing attacks

- BlessedGladiator maps to Skills/HolyBolt.asset.
- ForagingMaggot maps to Skills/EnemyNormalAttack.asset.
- NormalAttackSkill receives an attack callback. The bridge finds a nearby opponent, validates contact and invokes CharacterView.DealDamage. EnemyController responds to a successful attack through its combat hook to apply recoil; it does not activate skills.
- ShootingSkill references ShootingDataSO, which now contains only projectile geometry/configuration. The bridge supplies a fire callback with its own ShootingRuntime for phase/cast count. ShootingService.Fire creates projectiles; it has no scheduler.
- Existing shooting assets keep their GUIDs. Each has a new SkillDataSO wrapper carrying its migrated cooldown.
- Regeneration and EmergencyShield sample assets are available but are not automatically equipped. The former heals when injured, then waits two seconds; the latter grants shield at <=20% HP, then waits five seconds and can repeat while still under the threshold.
- StatBonusSkill applies once, holds its modifier lease and removes it on disposal. New defense granted by this modifier adds only the newly granted shield points, not previously spent shield.

## Shield grants

Explicit controller GrantShield calls may extend shield capacity for the current spawn. They add current shield; recalculation, healing and temporary disable never refill it. Defense-derived starting shield and explicit grants are both consumed before HP. Grants remain until consumed; no shield duration or stacking cap has been configured.

## Authoring

1. Create/configure a SkillDataSO using the existing inspector behavior selector.
2. Add the dependency wiring for a new concrete behavior in CharacterSkillBridge.CreateBehavior.
3. Equip innate assets in DataSO/Databases/CharacterSkillLoadouts.asset.
4. Pass selected run assets through CharacterSkillBridge.AddRunSkill.
5. Persistent behaviors optionally implement IDisposable; ISkill does not force all skills to have cleanup.
