# Skill, normal attack and projectiles

The current system is documented in [Skill-Shooting-Pipeline](../../../../Docs/Architecture/Skill-Shooting-Pipeline.md).

- CharacterDataSO has ActiveSkills and PassiveSkills arrays of SkillDataSO assets.
- Each skill asset contains a SkillData struct (level/maxLevel/cooldown), a SkillVisual class, and an ISkill behavior with only Execute.
- SkillManager owns per-character/per-slot data, cooldowns and disposable lifetimes. Controllers request skill use directly; no Combat layer is added.
- Enemy normal attack is active slot zero on ForagingMaggot. Configure its cooldown in Skills/EnemyNormalAttack.asset.
- Normal attack damage comes from CharacterDataSO BaseStats plus runtime bonuses and crit. There is no skill damage multiplier.
- EnemySpawnManager retains only movement recoil settings for the attack.
- Shooting remains temporarily separate: CharacterDataSO.Shooting references assets in DataSO/Shooting. HolyBolt continues auto-firing through ShootingService.
- Projectile geometry, flight, hit processing, pooling and status effects retain their existing pipelines.

Temporary disable pauses skill timers. Death/despawn removes skill registrations and their passive resources. Pool reuse starts fresh runtime data.
