using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Harness ngoài Assets; chỉ chạy khi được gọi thủ công bằng Run.ps1.
class Program
{
 static int checks;
 static void Equal(float actual,float expected,string label){if(Math.Abs(actual-expected)>.001f)throw new Exception(label+": "+actual+" != "+expected);checks++;}
 static void True(bool condition,string label){if(!condition)throw new Exception(label);checks++;}
 static void Set(object target,string field,object value)
 {
  for(Type type=target.GetType();type!=null;type=type.BaseType)
  {
   FieldInfo member=type.GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
   if(member!=null){member.SetValue(target,value);return;}
  }
  throw new MissingFieldException(target.GetType().Name,field);
 }

 static SkillDataSO Definition(ISkill[] actions,SkillBehaviourData[][] data,SkillSource source=SkillSource.Gameplay,int level=1)
 {
  var result=new SkillDataSO();var levels=new SkillLevelData[data.Length];
  for(int i=0;i<data.Length;i++){levels[i]=new SkillLevelData();Set(levels[i],"behaviours",data[i]);}
  Set(result,"source",source);Set(result,"level",level);Set(result,"behaviours",actions);Set(result,"levels",levels);result.Validate();return result;
 }

 // Giữ fixture startup cũ: bonus damage là một hành vi buff thuộc nhân vật.
 static SkillDataSO Def(StatBonusSkill action,StatBonus bonus=default)
  =>Definition(new ISkill[]{action},new[]{new SkillBehaviourData[]{new StatBonusData{stat=StatType.Damage,value=bonus.damage}}},SkillSource.Character);

 class World
 {
  public PoolManager Pools=new PoolManager();public CombatSpawner Spawner=new CombatSpawner();public Timers Timers=new Timers();
  public EffectManager Effects;public Search Search=new Search();public CharacterManager Characters=new CharacterManager();public LevelManager Levels=new LevelManager();
  public SkillFactory Factory;public SkillManager Manager;
  public World(){Effects=new EffectManager(new EffectRuntimePool(new ClassPool<SlowEffectRuntime>(),new ClassPool<DamageOverTimeEffectRuntime>()),Timers);Factory=new SkillFactory(Spawner,new ShootBehaviorFactory(),new ProjectileMoveFactory(),Pools,Effects);Manager=new SkillManager(Levels,new Clock(),Factory);}
  public CharacterView Character(CharacterTeam team,params SkillDataSO[] definitions)
  {
   var template=new CharacterDataSO{CharacterType=CharacterType.BlessedGladiator,Team=team,BaseStats=new BaseStats{maxHealth=1000,damage=20,moveSpeed=1,critDamageMultiplier=50},Skills=definitions};
   var character=new CharacterView{RuntimeData=new CharacterRuntimeData(template),Search=Search,Timers=Timers};
   character.EffectManager=Effects;character.FeedbackController=new FeedbackController();character.Skills=new CharacterSkills(character,definitions,Factory);
   Characters.Characters[character]=template;Search.Targets.Add(character);return character;
  }
 }

 class FlowPanel:UIPanelController
 {
  public bool Visible;public override void SetVisible(bool visible){Visible=visible;}
  public void Request(bool visible)=>RequestVisibility(visible);
 }

 static void ResetGameBus()=>typeof(GameUIEventBus).GetMethod("ResetListeners",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
 static void Routes(UIManager ui,params (GameState State,UIPanelController Panel)[] routes)
 {
  var type=typeof(UIManager).GetNestedType("StatePanelRoute",BindingFlags.NonPublic);var entries=Array.CreateInstance(type,routes.Length);
  for(int i=0;i<routes.Length;i++){var entry=Activator.CreateInstance(type,true);type.GetField("state").SetValue(entry,routes[i].State);type.GetField("panel").SetValue(entry,routes[i].Panel);entries.SetValue(entry,i);}Set(ui,"statePanels",entries);
 }

 // Kiểm tra ngắn luồng max/lượng đã mất và thông báo UI, không chạy toàn bộ harness.
 static void CheckVitals()
 {
  var template=new CharacterDataSO{BaseStats=new BaseStats{maxHealth=100,defense=100,damage=10,moveSpeed=1}};
  var runtime=new CharacterRuntimeData(template);int notifications=0;float observedHealth=0,observedShield=0,observedMaxHealth=0,observedMaxShield=0;
  runtime.Changed+=()=>{notifications++;observedHealth=runtime.CurrentHealth;observedShield=runtime.CurrentShield;observedMaxHealth=runtime.MaxHealth;observedMaxShield=runtime.MaxShield;};
  runtime.SetVitals(30,30);
  Equal(notifications,1,"damage notifies once");
  var buff=runtime.AddModifiers(new StatModifier(StatType.MaxHealth,StatOperation.Add,10),new StatModifier(StatType.Defense,StatOperation.Add,10));
  Equal(observedHealth,40,"UI observes health after max increase");Equal(observedShield,40,"UI observes shield after max increase");
  Equal(observedMaxHealth,110,"UI observes new health max");Equal(observedMaxShield,110,"UI observes new shield max");Equal(notifications,2,"bonus notifies once");
  buff.Update(new StatModifier(StatType.MaxHealth,StatOperation.Add,20),new StatModifier(StatType.Defense,StatOperation.Add,20));
  Equal(runtime.CurrentHealth,50,"buff update preserves health lost");Equal(runtime.CurrentShield,50,"buff update preserves shield lost");
  Equal(notifications,3,"atomic update notifies once");
  buff.Dispose();Equal(runtime.CurrentHealth,30,"removing bonus preserves health lost");Equal(runtime.CurrentShield,30,"removing bonus preserves shield lost");
  runtime.AddShield(10);Equal(runtime.MaxShield,110,"shield grant grows max");Equal(runtime.CurrentShield,40,"shield grant grows current");
  runtime.SetVitals(runtime.CurrentHealth+10,runtime.CurrentShield);Equal(runtime.MaxHealth,110,"health grant grows max");Equal(runtime.CurrentHealth,40,"health grant grows current");
  runtime.SetExternalBonus(new StatBonus{maxHealth=20,defense=20});
  Equal(runtime.MaxHealth,130,"external bonus retains direct health grant");Equal(runtime.CurrentHealth,60,"external bonus preserves health lost");
  Equal(runtime.MaxShield,130,"external bonus retains direct shield grant");Equal(runtime.CurrentShield,60,"external bonus preserves shield lost");
  int beforeSlow=notifications;runtime.AddModifier(new StatModifier(StatType.MoveSpeed,StatOperation.Percent,-30));Equal(notifications,beforeSlow+1,"slow notifies UI");
  runtime.SetVitals(0,runtime.CurrentShield);var afterDeath=runtime.AddModifier(new StatModifier(StatType.MaxHealth,StatOperation.Add,500));
  True(!runtime.IsAlive&&runtime.CurrentHealth==0,"stats cannot revive defeated character");afterDeath.Dispose();
 }

 // Kiểm tra tập trung công thức modifier chung, không chạy toàn bộ harness/UI.
 static void CheckStats()
 {
  var template=new CharacterDataSO{BaseStats=new BaseStats{maxHealth=100,damage=100,moveSpeed=10,critChance=30}};
  var runtime=new CharacterRuntimeData(template);int changed=0;runtime.Changed+=()=>changed++;
  var damage=runtime.AddModifier(new StatModifier(StatType.Damage,StatOperation.Percent,-20));
  Equal(runtime.FinalStats.damage,80,"damage percent debuff");
  var flat=runtime.AddModifier(new StatModifier(StatType.Damage,StatOperation.Add,20));
  Equal(runtime.FinalStats.damage,96,"flat bonuses precede percentages");
  damage.Update(new StatModifier(StatType.Damage,StatOperation.Percent,-50));
  Equal(runtime.FinalStats.damage,60,"update modifier in place");
  flat.Dispose();Equal(runtime.FinalStats.damage,50,"remove only flat source");
  damage.Dispose();Equal(runtime.FinalStats.damage,100,"remove damage debuff");
  var crit=runtime.AddModifier(new StatModifier(StatType.CritChance,StatOperation.Add,-10));
  Equal(runtime.FinalStats.critChance,20,"crit percentage points");
  crit.Update(new StatModifier(StatType.CritChance,StatOperation.Percent,-20));
  Equal(runtime.FinalStats.critChance,24,"crit relative percent");crit.Dispose();
  var speed=runtime.AddModifier(new StatModifier(StatType.MoveSpeed,StatOperation.Percent,-30));
  Equal(runtime.FinalStats.moveSpeed,7,"slow uses common modifier");speed.Dispose();
  Equal(runtime.FinalStats.moveSpeed,10,"remove speed source");
  var snapshot=new[]{new StatModifier(StatType.Damage,StatOperation.Percent,-20)};
  var source=runtime.AddModifiers(snapshot);snapshot[0].value=-90;runtime.SetExternalBonus(default);
  Equal(runtime.FinalStats.damage,80,"runtime snapshots mutable input array");
  source.Dispose();True(changed>0,"modifier changes notify UI");
  var world=new World();var owner=world.Character(CharacterTeam.Player);
  var weak=world.Effects.ApplyMaintained(new SlowEffect(.1f),new CharacterTarget(owner),CharacterTeam.Enemy);
  var strong=world.Effects.ApplyMaintained(new SlowEffect(.3f),new CharacterTarget(owner),CharacterTeam.Enemy);
  Equal(owner.RuntimeData.FinalStats.moveSpeed,.6f,"slow percentages add across runtimes");
  world.Effects.Remove(strong);Equal(owner.RuntimeData.FinalStats.moveSpeed,.9f,"remove only strong slow source");
  world.Effects.Remove(weak);Equal(owner.RuntimeData.FinalStats.moveSpeed,1,"clear last slow");
  world.Manager.Dispose();owner.Release();
 }

 static void CheckEffects()
 {
  // Bullet hết hạn độc lập với slow duy trì; cùng cấu hình aura vẫn có runtime riêng.
  {
   var w=new World();var victim=w.Character(CharacterTeam.Enemy);var target=new CharacterTarget(victim);
   var bullet=new SlowEffect(.3f);typeof(GameplayEffectData).GetField("duration",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(bullet,1f);
   True(w.Effects.Apply(bullet,target,CharacterTeam.Player),"bullet slow applied");Equal(victim.RuntimeData.FinalStats.moveSpeed,.7f,"bullet leaves 70 percent speed");
   var aura=new SlowEffect(.2f);var first=w.Effects.ApplyMaintained(aura,target,CharacterTeam.Player);
   Equal(victim.RuntimeData.FinalStats.moveSpeed,.5f,"bullet plus aura add to 50 percent");
   var second=w.Effects.ApplyMaintained(aura,target,CharacterTeam.Player);
   Equal(victim.RuntimeData.FinalStats.moveSpeed,.3f,"separate maintained sources using same configuration");
   w.Effects.Remove(second);Equal(victim.RuntimeData.FinalStats.moveSpeed,.5f,"remove exactly one maintained source");
   w.Timers.Advance(1f);Equal(victim.RuntimeData.FinalStats.moveSpeed,.8f,"bullet expires leaving aura");
   w.Timers.Advance(20f);True(w.Effects.IsActive(first),"maintained slow has no expiry");
   var root=w.Effects.ApplyMaintained(new SlowEffect(.9f),target,CharacterTeam.Player);Equal(victim.RuntimeData.FinalStats.moveSpeed,0,"over 100 percent slow stops movement");
   w.Effects.Remove(root);Equal(victim.RuntimeData.FinalStats.moveSpeed,.8f,"removing excess slow recovers exact speed");
   var replacement=w.Effects.ApplyMaintained(new SlowEffect(.1f),target,CharacterTeam.Player);
   True(ReferenceEquals(root.Runtime,replacement.Runtime),"test reuses pooled runtime");
   w.Effects.Remove(root);True(w.Effects.IsActive(replacement),"stale handle cannot remove reused runtime");Equal(victim.RuntimeData.FinalStats.moveSpeed,.7f,"stale handle leaves modifiers intact");
   victim.Release();True(!w.Effects.IsActive(first)&&!w.Effects.IsActive(replacement),"despawn clears maintained handles");Equal(victim.RuntimeData.FinalStats.moveSpeed,1,"despawn removes all slow modifiers");
   w.Effects.Remove(first);w.Effects.Remove(replacement);w.Manager.Dispose();w.Effects.Dispose();
  }
  // Skill vùng chỉ apply lúc vào, remove lúc ra/pause/nâng cấp/hủy.
  {
   var w=new World();var definition=Definition(new ISkill[]{new FrostAuraSkill()},new[]{
    new SkillBehaviourData[]{new FrostAuraData{visualPrefab=new SkillAreaVisual(),radius=2,slowFraction=.2f}},
    new SkillBehaviourData[]{new FrostAuraData{visualPrefab=new SkillAreaVisual(),radius=2,slowFraction=.4f}}
   });
   var owner=w.Character(CharacterTeam.Player);var victim=w.Character(CharacterTeam.Enemy);victim.transform.position=new Vector3(.5f,0);
   w.Manager.GrantOrUpgrade(definition,owner);w.Manager.TryGetRuntime(definition,owner,out var runtime);w.Manager.Tick();
   Equal(victim.RuntimeData.FinalStats.moveSpeed,.8f,"aura applies on enter");
   var bullet=new SlowEffect(.3f);typeof(GameplayEffectData).GetField("duration",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(bullet,1f);
   w.Effects.Apply(bullet,new CharacterTarget(victim),CharacterTeam.Player);Equal(victim.RuntimeData.FinalStats.moveSpeed,.5f,"real aura stacks with bullet");
   w.Manager.Tick();Equal(victim.RuntimeData.FinalStats.moveSpeed,.5f,"staying inside does not add slow each frame");
   victim.transform.position=new Vector3(10,0);w.Manager.Tick();Equal(victim.RuntimeData.FinalStats.moveSpeed,.7f,"leaving aura preserves bullet");
   victim.transform.position=new Vector3(.5f,0);w.Manager.Tick();w.Timers.Advance(1f);Equal(victim.RuntimeData.FinalStats.moveSpeed,.8f,"bullet expires while inside actual aura");
   owner.Disable();Equal(victim.RuntimeData.FinalStats.moveSpeed,1,"pausing aura releases slow");owner.Enable();w.Manager.Tick();Equal(victim.RuntimeData.FinalStats.moveSpeed,.8f,"resuming aura reapplies slow");
   runtime.TryUpgrade();w.Manager.Tick();Equal(victim.RuntimeData.FinalStats.moveSpeed,.6f,"upgrade replaces previous aura slow");
   owner.Release();Equal(victim.RuntimeData.FinalStats.moveSpeed,1,"disposing aura releases slow");w.Manager.Dispose();victim.Release();w.Effects.Dispose();
  }
  // Stat callback có thể dọn mục tiêu ngay trong Apply; không được rò modifier.
  {
   var w=new World();var victim=w.Character(CharacterTeam.Enemy);Action release=null;
   release=()=>{victim.RuntimeData.Changed-=release;victim.Release();};victim.RuntimeData.Changed+=release;
   var handle=w.Effects.ApplyMaintained(new SlowEffect(.3f),new CharacterTarget(victim),CharacterTeam.Player);
   True(!w.Effects.IsActive(handle),"cleanup inside Apply invalidates handle");Equal(victim.RuntimeData.FinalStats.moveSpeed,1,"cleanup inside Apply releases modifier");w.Manager.Dispose();w.Effects.Dispose();
  }
 }

 static void CheckLevelUpPresentation()
 {
  // Slow motion và pause dùng chung token, không khôi phục nhầm tốc độ khi hai nguồn chồng nhau.
  Time.timeScale=.8f;
  using(var pauses=new GameplayPauseService())
  {
   var slow=pauses.AcquireSlowMotion(.5f);Equal(Time.timeScale,.4f,"slow motion respects original time scale");
   var pause=pauses.Acquire();Equal(Time.timeScale,0,"pause overrides slow motion");
   slow.Dispose();Equal(Time.timeScale,0,"ending slow motion preserves another pause");
   pause.Dispose();Equal(Time.timeScale,.8f,"last token restores original time scale");
   slow=pauses.AcquireSlowMotion(.5f);pause=pauses.Acquire();pause.Dispose();
   Equal(Time.timeScale,.4f,"closing another pause preserves active slow motion");slow.Dispose();
  }
  Time.timeScale=1f;
  ResetGameBus();var w=new World();var pausesService=new GameplayPauseService();var players=new PlayerManager();
  var owner=w.Character(CharacterTeam.Player);players.Change(owner);
  var definition=Definition(new ISkill[]{new StatBonusSkill()},new[]{
   new SkillBehaviourData[]{new StatBonusData{stat=StatType.Damage,value=1}},
   new SkillBehaviourData[]{new StatBonusData{stat=StatType.Damage,value=2}}
  });
  var database=new SkillDatabaseSO();var entry=new SkillDatabaseSO.Entry();Set(entry,"skill",definition);Set(entry,"availableInRunChoices",true);Set(database,"skills",new List<SkillDatabaseSO.Entry>{entry});
  var view=new SkillChoiceUI();var controller=new SkillChoiceController();Set(controller,"levelUpPanel",view);
  controller.VisibilityRequested+=(panel,show)=>panel.SetVisible(show);controller.Construct(w.Levels,w.Manager,players,pausesService,database);
  var late=typeof(SkillChoiceController).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
  w.Levels.GainLevels(0,2);Equal(Time.timeScale,.65f,"level up starts mild slow motion");
  late.Invoke(controller,null);True(!view.Visible,"popup waits for slow motion beat");
  Time.unscaledTime+=.31f;late.Invoke(controller,null);True(view.Visible,"popup appears after real time delay");Equal(Time.timeScale,0,"popup pauses gameplay after slow motion");
  late.Invoke(controller,null); // SetVisible yêu cầu render lại ở frame kế tiếp trước khi cho chọn.
  view.Reroll();view.Reroll();view.Choose(0);Equal(controller.PendingSkillChoices,2,"reroll and same-frame select do not consume reward");
  late.Invoke(controller,null);late.Invoke(controller,null);True(view.Visible&&view.Offers.Count==1,"small catalog remains selectable after reroll");Equal(Time.timeScale,0,"reroll keeps popup pause");
  view.Choose(0);late.Invoke(controller,null);True(view.Visible&&controller.PendingSkillChoices==1,"queued level presents next choices");Equal(Time.timeScale,0,"queued reward does not restart slow motion");
  view.Choose(0);late.Invoke(controller,null);True(!view.Visible,"last reward closes popup");Equal(Time.timeScale,1,"closing popup restores normal speed");
  w.Levels.GainLevels(2,3);Equal(Time.timeScale,.65f,"later level starts a new slow motion beat");
  var manual=pausesService.Acquire();w.Levels.Reset();Equal(Time.timeScale,0,"reset cancels slow motion without lifting manual pause");manual.Dispose();Equal(Time.timeScale,1,"reset leaves no slow motion token");
  w.Levels.GainLevels(3,4);typeof(SkillChoiceController).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,null);Equal(Time.timeScale,1,"disable cancels pending slow motion");
  pausesService.Dispose();w.Manager.Dispose();owner.Release();ResetGameBus();
 }

 static void CheckEnemyFeedback()
 {
  Time.timeScale=1f;
  var w=new World();var enemy=w.Character(CharacterTeam.Enemy);var visual=new Transform{position=new Vector3(2,3,0)};
  var walkSprite=new Sprite();var sprites=new[]{new SpriteRenderer{color=new Color(.8f,.6f,.4f,.5f),sprite=walkSprite}};var animator=new Animator();
  var frames=new[]{new Sprite(),new Sprite(),new Sprite(),new Sprite(),new Sprite(),new Sprite()};
  var animation=new SpriteDeathAnimation();Set(animation,"target",sprites[0]);Set(animation,"frames",frames);
  var feedback=new EnemyCombatFeedback(enemy,visual,new[]{animator},animation);
  int completed=0;True(feedback.PlayDeath(()=>completed++),"visible enemy starts sprite death");True(!animator.enabled,"death disables walking animator");
  True(sprites[0].sprite==frames[0],"death starts at first sprite");True(!feedback.PlayDeath(()=>completed++),"death presentation cannot start twice");
  w.Timers.Advance(.1f);True(sprites[0].sprite==frames[1],"death advances sprite at frame rate");Equal(completed,0,"death waits for all frames");
  feedback.SetPaused(true);w.Timers.Advance(1);Equal(completed,0,"temporary disable pauses death animation");feedback.SetPaused(false);
  w.Timers.Advance(.41f);Equal(completed,1,"death animation completes once");True(sprites[0].sprite==frames[5],"death ends on last sprite");
  Equal(visual.position.x,2,"sprite death does not launch enemy");Equal(visual.localEulerAngles.z,0,"sprite death does not rotate enemy");
  feedback.Dispose();True(sprites[0].sprite==walkSprite,"pool cleanup restores walk sprite");True(animator.enabled,"pool cleanup restores animator");
  var cancelled=new EnemyCombatFeedback(enemy,visual,new[]{animator},animation);cancelled.PlayDeath(()=>completed++);cancelled.Dispose();
  w.Timers.Advance(1);PrimeTween.TweenHost.Advance(1);Equal(completed,1,"despawn cancels delayed death callback");Equal(sprites[0].color.g,.6f,"despawn restores flash tint");
  var stale=new EnemyCombatFeedback(enemy,visual,new[]{animator},animation);stale.PlayDeath(()=>completed++);
  enemy.RuntimeData.InitializeSpawn(new CharacterDataSO{Team=CharacterTeam.Enemy,BaseStats=BaseStats.Default},default);w.Timers.Advance(1);Equal(completed,1,"old spawn cannot complete death on reused runtime");stale.Dispose();
  enemy.IsVisualVisible=false;var hidden=new EnemyCombatFeedback(enemy,visual,new[]{animator},animation);True(!hidden.PlayDeath(()=>completed++),"hidden enemy skips death visual");hidden.Dispose();
  enemy.IsVisualVisible=true;var missing=new EnemyCombatFeedback(enemy,visual,new[]{animator});True(!missing.PlayDeath(()=>completed++),"missing sprite animation uses immediate cleanup fallback");missing.Dispose();
  w.Manager.Dispose();enemy.Release();
 }

 static void CheckWaveProgress()
 {
  var enemy=new CharacterDataSO{Team=CharacterTeam.Enemy};
  var first=new WaveConfigSO.Wave{duration=10,spawnCount=2,enemies=new[]{enemy}};
  var second=new WaveConfigSO.Wave{duration=20,spawnCount=3,enemies=new[]{enemy}};
  var config=new WaveConfigSO();Set(config,"waves",new[]{first,second});config.Validate();
  var progress=new WaveProgress();progress.Reset(100);
  Equal(progress.EnemyLevel,1,"first wave spawns level-one enemies");
  True(!progress.Advance(config,109,20),"timed wave waits for duration");
  True(progress.Advance(config,110,20)&&progress.WaveIndex==1,"timed wave advances despite living enemies");Equal(progress.EnemyLevel,2,"second wave increases enemy level");
  True(progress.Advance(config,130,20)&&progress.WaveIndex==1,"last timed wave repeats itself");Equal(progress.EnemyLevel,3,"repeating last wave increases enemy level");
  True(!progress.Advance(config,140,0),"last wave keeps its own duration rather than first wave duration");
  True(progress.Advance(config,150,0)&&progress.WaveIndex==1&&progress.EnemyLevel==4,"each final-wave duration increases enemy level");
  progress.Reset(0);True(progress.Advance(config,95,3)&&progress.WaveIndex==1&&progress.EnemyLevel==6,"long frame skips repeated last-wave durations and preserves remainder");
  True(!progress.Advance(config,109,3)&&progress.Advance(config,110,3)&&progress.WaveIndex==1&&progress.EnemyLevel==7,"last-wave remainder transitions at exact boundary");
  var single=new WaveConfigSO();Set(single,"waves",new[]{first});progress.Reset(0);progress.RecordSpawn();
  True(progress.Advance(single,10,20)&&progress.WaveIndex==0&&progress.EnemyLevel==2,"single timed template repeats as next level");Equal(progress.SpawnedInWave,0,"repeating same template resets spawn counter");
  progress.Reset(500);True(progress.EnemyLevel==1&&progress.WaveIndex==0&&!progress.Advance(single,509,0),"new run resets level, index and timer");
  Set(config,"advanceCondition",WaveAdvanceCondition.AfterEnemiesCleared);config.Validate();progress.Reset(0);
  True(!progress.Advance(config,1000,0),"clear mode ignores time and waits for successful spawn quota");
  progress.RecordSpawn();True(progress.CanSpawn(config)&&!progress.Advance(config,1000,0),"partial clear quota cannot advance");
  progress.RecordSpawn();True(!progress.CanSpawn(config)&&!progress.Advance(config,1000,1),"quota stops spawning while any enemy lives");
  True(progress.Advance(config,1000,0)&&progress.WaveIndex==1&&progress.EnemyLevel==2,"clear quota advances when no living enemies remain");
  Equal(progress.SpawnedInWave,0,"new wave starts a fresh quota");
  progress.RecordSpawn();progress.RecordSpawn();True(!progress.Advance(config,2000,0),"second template uses its own quota");progress.RecordSpawn();
  True(progress.Advance(config,2000,0)&&progress.WaveIndex==1&&progress.EnemyLevel==3&&progress.CanSpawn(config),"last clear wave repeats itself at higher level");
  progress.RecordSpawn();progress.RecordSpawn();True(progress.CanSpawn(config)&&!progress.Advance(config,3000,0),"repeated final wave keeps final quota rather than first quota");
  progress.RecordSpawn();True(!progress.CanSpawn(config)&&!progress.Advance(config,3000,1),"repeated final wave waits for survivors to die");
  True(progress.Advance(config,3000,0)&&progress.WaveIndex==1&&progress.EnemyLevel==4,"next cleared final batch increases level again");
  Set(single,"advanceCondition",WaveAdvanceCondition.AfterEnemiesCleared);progress.Reset(0);progress.RecordSpawn();progress.RecordSpawn();
  True(progress.Advance(single,50,0)&&progress.WaveIndex==0&&progress.EnemyLevel==2&&progress.CanSpawn(single),"single clear template also repeats at higher level");
  var enemyLevels=new CharacterLevelConfigSO();Set(enemyLevels,"characterType",CharacterType.ForagingMaggot);Set(enemyLevels,"maxLevel",3);Set(enemyLevels,"bonusPerLevel",new StatBonus{maxHealth=20});
  Equal(enemyLevels.GetTotalBonus(1).maxHealth,0,"enemy level one retains base HP");Equal(enemyLevels.GetTotalBonus(2).maxHealth,20,"wave two adds one level bonus");Equal(enemyLevels.GetTotalBonus(9).maxHealth,40,"enemy growth respects config level cap");
  var playerLevels=new CharacterLevelConfigSO();Set(playerLevels,"bonusPerLevel",new StatBonus{maxHealth=20,damage=2,defense=2});
  Equal(playerLevels.GetTotalBonus(3).defense,4,"shared config retains player permanent growth");True(playerLevels.TryGetUpgradeCost(1,out long cost)&&cost==100,"player upgrade cost remains available");
  Set(config,"advanceCondition",WaveAdvanceCondition.AfterDuration);first.duration=float.NaN;bool rejected=false;try{config.Validate();}catch(InvalidOperationException){rejected=true;}True(rejected,"timed wave rejects NaN duration");first.duration=10;
  Set(config,"advanceCondition",WaveAdvanceCondition.AfterEnemiesCleared);first.spawnCount=0;rejected=false;try{config.Validate();}catch(InvalidOperationException){rejected=true;}True(rejected,"clear mode rejects zero quota");first.spawnCount=2;
 }
 static void CheckCharacterFeedback()
 {
  foreach(CharacterTeam team in new[]{CharacterTeam.Player,CharacterTeam.Enemy})
  {
   var w=new World();var owner=w.Character(team);var source=CharacterSearch.OpponentOf(team);
   var events=new List<(CharacterFeedbackKind Kind,bool Active)>();
   ICharacterFeedbackSource feedback=owner.FeedbackController;feedback.FeedbackChanged+=(kind,active)=>events.Add((kind,active));
   int Count(CharacterFeedbackKind kind,bool active){int n=0;foreach(var e in events)if(e.Kind==kind&&e.Active==active)n++;return n;}
   var poison=new PoisonEffect();Set(poison,"duration",2f);var burn=new BurnEffect();Set(burn,"duration",3f);
   var slowA=new SlowEffect(.1f);Set(slowA,"duration",1f);var slowB=new SlowEffect(.2f);Set(slowB,"duration",3f);
   True(w.Effects.Apply(poison,new CharacterTarget(owner),source),"poison applies to either character team");
   True(w.Effects.Apply(poison,new CharacterTarget(owner),source),"poison refresh succeeds");Equal(Count(CharacterFeedbackKind.Poison,true),1,"refresh does not duplicate active feedback");
   w.Effects.Apply(burn,new CharacterTarget(owner),source);w.Effects.Apply(slowA,new CharacterTarget(owner),source);w.Effects.Apply(slowB,new CharacterTarget(owner),source);
   Equal(Count(CharacterFeedbackKind.Burn,true),1,"burn identified separately from poison shared runtime");Equal(Count(CharacterFeedbackKind.Slow,true),1,"stacked slow publishes one activation");
   w.Timers.Advance(1f);Equal(Count(CharacterFeedbackKind.Slow,false),0,"one expired slow cannot clear another active slow");
   w.Timers.Advance(1f);Equal(Count(CharacterFeedbackKind.Poison,false),1,"poison expiration publishes removal");Equal(Count(CharacterFeedbackKind.Burn,false),0,"poison expiration keeps burn active");
   w.Timers.Advance(1f);Equal(Count(CharacterFeedbackKind.Slow,false),1,"last slow removal restores state");Equal(Count(CharacterFeedbackKind.Burn,false),1,"burn expiration publishes removal");
   int hits=Count(CharacterFeedbackKind.Hit,true);
   owner.FeedbackController.GrantShield(owner,owner.RuntimeData,10);Equal(Count(CharacterFeedbackKind.Hit,true),hits,"shield gain does not flash as damage");
   var damage=owner.ReceiveDamage(new DamageInfo(2,source));True(damage.ShieldLost==2&&damage.HealthLost==0,"shield-only hit accepted");Equal(Count(CharacterFeedbackKind.Hit,true),hits+1,"shield-only hit emits feedback once");
   owner.ReceiveDamage(new DamageInfo(2,team));Equal(Count(CharacterFeedbackKind.Hit,true),hits+1,"friendly rejected hit emits no feedback");
   owner.FeedbackController.ChangeHealth(owner,owner.RuntimeData,-2);Equal(Count(CharacterFeedbackKind.Hit,true),hits+2,"direct health loss emits hit feedback");
   owner.FeedbackController.ChangeHealth(owner,owner.RuntimeData,2);Equal(Count(CharacterFeedbackKind.Hit,true),hits+2,"health gain emits no hit feedback");
   feedback.NotifyEffect(owner,owner.RuntimeData,owner.RuntimeData.Generation+1,CharacterFeedbackKind.Poison,true);Equal(Count(CharacterFeedbackKind.Poison,true),1,"stale spawn notification ignored");
   int logs=0;Debug.ExceptionSink=_=>logs++;Action<CharacterFeedbackKind,bool> failing=(kind,active)=>throw new Exception("injected feedback listener failure");feedback.FeedbackChanged+=failing;
   owner.ReceiveDamage(new DamageInfo(1,source));feedback.FeedbackChanged-=failing;Debug.ExceptionSink=null;Equal(logs,1,"feedback listener error is isolated");
   w.Effects.Apply(poison,new CharacterTarget(owner),source);w.Effects.ClearTarget(owner,owner.RuntimeData);Equal(Count(CharacterFeedbackKind.Poison,false),2,"explicit cleanup removes status feedback");
   w.Manager.Dispose();owner.Release();
  }
 }

 static void CheckRerollPool()
 {
  var world=new World();var owner=world.Character(CharacterTeam.Player);var catalog=new List<SkillDataSO>();
  for(int i=0;i<6;i++)catalog.Add(Definition(new ISkill[]{new StatBonusSkill()},new[]{new SkillBehaviourData[]{new StatBonusData{value=i+1}}}));
  var pool=new SkillChoicePool(new System.Random(42));var offers=new List<SkillChoiceOffer>();
  pool.Build(catalog,world.Manager,owner,offers);var previous=new HashSet<SkillDataSO>();foreach(var offer in offers)previous.Add(offer.Definition);
  pool.Build(catalog,world.Manager,owner,offers,previousOffers:offers);
  Equal(offers.Count,3,"reroll keeps three choices when available");foreach(var offer in offers)True(!previous.Contains(offer.Definition),"reroll prioritizes previously unseen skill");
  var small=catalog.GetRange(0,4);pool.Build(small,world.Manager,owner,offers);previous.Clear();foreach(var offer in offers)previous.Add(offer.Definition);
  pool.Build(small,world.Manager,owner,offers,previousOffers:offers);
  var unique=new HashSet<SkillDataSO>();bool fresh=false;foreach(var offer in offers){True(unique.Add(offer.Definition),"reroll never duplicates cards");fresh|=!previous.Contains(offer.Definition);}
  True(fresh&&offers.Count==3,"limited pool includes alternative and fills remaining slots");
  var blocked=new HashSet<SkillDataSO>{catalog[5]};world.Manager.GrantOrUpgrade(catalog[4],owner);
  pool.Build(catalog,world.Manager,owner,offers,excluded:blocked,previousOffers:offers);
  foreach(var offer in offers)True(offer.Definition!=catalog[4]&&offer.Definition!=catalog[5],"reroll excludes maxed and failed skills");
  world.Manager.Dispose();owner.Release();
 }

 static void CheckShooters()
 {
  var factory=new ShootBehaviorFactory();var straight=factory.CreateRuntime(new StraightShooter());
  for(int i=0;i<5;i++)
  {
   Vector2 direction=straight.GetDirection(Vector2.up,i,5);
   Equal(direction.x,0,"straight keeps aim x");Equal(direction.y,1,"straight keeps aim y");
  }
  var template=new FanShooter();var fan=factory.CreateRuntime(template);
  True(!ReferenceEquals(template,fan),"fan runtime is separate from template");
  for(int i=0;i<5;i++)
  {
   Vector2 direction=factory.ShootingHandle(fan,Vector2.right,i,5);
   float angle=(-30f+15f*i)*Mathf.Deg2Rad;
   Equal(direction.x,Mathf.Cos(angle),"fan spaces five projectiles evenly x");
   Equal(direction.y,Mathf.Sin(angle),"fan spaces five projectiles evenly y");
  }
  Vector2 single=fan.GetDirection(Vector2.up,0,1);Equal(single.x,0,"single fan projectile follows aim x");Equal(single.y,1,"single fan projectile follows aim y");
  Vector2 evenLeft=fan.GetDirection(Vector2.up,0,2),evenRight=fan.GetDirection(Vector2.up,1,2);
  Equal(evenLeft.x,.5f,"even fan rotates around current aim");Equal(evenRight.x,-.5f,"even fan is symmetric");
  foreach(float invalid in new[]{-1f,361f,float.NaN,float.PositiveInfinity})
  {
   Set(template,"spreadAngle",invalid);bool rejected=false;try{factory.CreateRuntime(template);}catch(InvalidOperationException){rejected=true;}
   True(rejected,"fan rejects invalid angle");
  }
  Set(template,"spreadAngle",0f);fan=factory.CreateRuntime(template);Equal(fan.GetDirection(Vector2.right,0,3).y,0,"zero angle fan follows aim");
  var shooting=new ShootingData{shooter=new FanShooter(),projectileCount=5};shooting.Validate();
 }

 static void Main(string[] args)
 {
  if(Array.IndexOf(args,"--effects-only")>=0){CheckEffects();Console.WriteLine("PASS: "+checks+" focused effect checks.");return;}
  if(Array.IndexOf(args,"--stats-only")>=0){CheckVitals();CheckStats();Console.WriteLine("PASS: "+checks+" focused stat/vitals checks.");return;}
  if(Array.IndexOf(args,"--vitals-only")>=0){CheckVitals();Console.WriteLine("PASS: "+checks+" focused vitals checks.");return;}
  CheckShooters();
  CheckRerollPool();
  CheckCharacterFeedback();
  CheckWaveProgress();
  CheckEffects();
  CheckLevelUpPresentation();
  CheckEnemyFeedback();
  // Data theo cấp cập nhật hành vi đang có và mở thêm loại mới.
  {
   var world=new World();var sawVisual=new SkillAreaVisual();var frostVisual=new SkillAreaVisual();
   var definition=Definition(new ISkill[]{new OrbitSawSkill(),new ShootingSkill(),new FrostAuraSkill()},new[]{
    new SkillBehaviourData[]{new OrbitSawData{visualPrefab=sawVisual,bladeCount=1}},
    new SkillBehaviourData[]{new OrbitSawData{visualPrefab=sawVisual,bladeCount=2},new ShootingData()},
    new SkillBehaviourData[]{new OrbitSawData{visualPrefab=sawVisual,bladeCount=3},new ShootingData{projectileCount=2},new FrostAuraData{visualPrefab=frostVisual,radius=2}}
   });
   var owner=world.Character(CharacterTeam.Player);var victim=world.Character(CharacterTeam.Enemy);victim.transform.position=new Vector3(.5f,0);
   True(world.Manager.GrantOrUpgrade(definition,owner),"grant gameplay skill");world.Manager.TryGetRuntime(definition,owner,out var runtime);
   Equal(runtime.BehaviourCount,1,"only saw at level one");world.Manager.Tick();Equal(world.Pools.Active.Count,1,"one saw visual");Equal(world.Spawner.Shots.Count,0,"shooting remains locked");
   True(runtime.TryUpgrade(),"upgrade level two");Equal(runtime.BehaviourCount,2,"shooting opens at level two");world.Manager.Tick();Equal(world.Spawner.Shots.Count,1,"shooting starts after unlock");
   True(runtime.TryUpgrade(),"upgrade level three");Equal(runtime.BehaviourCount,3,"aura opens at level three");world.Manager.Tick();Equal(victim.RuntimeData.FinalStats.moveSpeed,.7f,"unlocked aura slows");
   True(!runtime.TryUpgrade(),"level count defines cap");owner.Release();Equal(world.Pools.Active.Count,0,"all visuals returned");Equal(victim.RuntimeData.FinalStats.moveSpeed,1,"aura token removed");
   world.Manager.Dispose();victim.Release();
  }
  // Nhân vật giữ cấp cấu hình và không được nâng bằng lượt thưởng gameplay.
  {
   var world=new World();
   var characterSkill=Definition(new ISkill[]{new ShootingSkill()},new[]{new SkillBehaviourData[]{new ShootingData()},new SkillBehaviourData[]{new ShootingData{projectileCount=2}}},SkillSource.Character,2);
   var owner=world.Character(CharacterTeam.Player,characterSkill);owner.Skills.TryGetRuntime(characterSkill,out var runtime);
   Equal(runtime.RunLevel,2,"character configured level");True(!runtime.TryUpgrade()&&!world.Manager.GrantOrUpgrade(characterSkill,owner),"character rejects gameplay upgrade");
   True(!world.Manager.TryGetRuntime(characterSkill,owner,out _),"character runtime is not owned by gameplay manager");
   var slots=new List<SkillRuntime>();world.Manager.GetOwnedSkills(owner,slots);Equal(slots.Count,0,"innate skill does not create gameplay slot");
   world.Manager.Tick();Equal(world.Spawner.Shots.Count,0,"gameplay scheduler does not tick character skill");
   owner.Skills.Tick(0f);Equal(world.Spawner.Shots.Count,0,"paused game does not cast character skill");
   owner.Disable();owner.Skills.Tick(.1f);Equal(world.Spawner.Shots.Count,0,"disabled character does not cast");
   var victim=world.Character(CharacterTeam.Enemy);victim.transform.position=new Vector3(.5f,0);
   owner.Enable();owner.Skills.Tick(.1f);Equal(world.Spawner.Shots.Count,2,"character scheduler casts configured skill");
   var acquired=Definition(new ISkill[]{new StatBonusSkill()},new[]{new SkillBehaviourData[]{new StatBonusData{value=5}}});
   True(world.Manager.GrantOrUpgrade(acquired,owner),"acquire gameplay alongside character skill");
   world.Manager.GetOwnedSkills(owner,slots);True(slots.Count==1&&slots[0].Definition==acquired,"slots contain only acquired gameplay skill");
   world.Levels.Reset();True(runtime.IsActive&&owner.Skills.TryGetRuntime(characterSkill,out var retained)&&ReferenceEquals(runtime,retained),"run reset preserves character runtime and cooldown");
   Equal(owner.RuntimeData.FinalStats.defense,0,"reset removes only acquired modifier");
   var database=new SkillDatabaseSO();var entry=new SkillDatabaseSO.Entry();Set(entry,"skill",characterSkill);Set(entry,"availableInRunChoices",true);Set(database,"skills",new List<SkillDatabaseSO.Entry>{entry});
   bool rejected=false;try{database.Validate();}catch(InvalidOperationException){rejected=true;}True(rejected,"gameplay database rejects character skill");
   world.Manager.Dispose();True(runtime.IsActive,"disposing gameplay manager does not dispose character skill");
   owner.Release();True(!runtime.IsActive,"releasing character disposes its own runtime");victim.Release();
  }
  // Buff và visual ban đầu đi theo vòng đời nhân vật, không theo reset trận.
  {
   var world=new World();var visual=new SkillAreaVisual();
   var innate=Definition(new ISkill[]{new StatBonusSkill(),new OrbitSawSkill()},new[]{new SkillBehaviourData[]{new StatBonusData{stat=StatType.Damage,value=3},new OrbitSawData{visualPrefab=visual}}},SkillSource.Character);
   var owner=world.Character(CharacterTeam.Player,innate);owner.Skills.TryGetRuntime(innate,out var runtime);
   owner.Skills.Tick(.1f);Equal(world.Pools.Active.Count,1,"character owns saw visual");
   owner.RuntimeData.SetVitals(500,0);world.Levels.Reset();
   Equal(owner.RuntimeData.FinalStats.damage,23,"reset retains innate modifier");Equal(owner.RuntimeData.CurrentHealth,500,"reset does not refill living character");
   owner.ReceiveDamage(new DamageInfo(10000,CharacterTeam.Enemy));
   True(!runtime.IsActive,"death disposes character runtime");Equal(world.Pools.Active.Count,0,"death returns innate visual");Equal(owner.RuntimeData.FinalStats.damage,20,"death removes innate modifier");
   owner.Release();world.Manager.Dispose();
  }
  // Buff theo cấp thay giá trị cũ, không cộng chồng.
  {
   var world=new World();var owner=world.Character(CharacterTeam.Player);
   var definition=Definition(new ISkill[]{new StatBonusSkill()},new[]{new SkillBehaviourData[]{new StatBonusData{value=2}},new SkillBehaviourData[]{new StatBonusData{value=5}}});
   world.Manager.GrantOrUpgrade(definition,owner);Equal(owner.RuntimeData.FinalStats.defense,2,"initial defense buff");
   world.Manager.GrantOrUpgrade(definition,owner);Equal(owner.RuntimeData.FinalStats.defense,5,"replacement defense buff");
   world.Levels.Reset();Equal(owner.RuntimeData.FinalStats.defense,0,"reset removes acquired buff");world.Manager.Dispose();owner.Release();
  }

  {
   ResetGameBus();var w=new World();var resource=new PoolPrewarmService();var saves=new SaveManager();var factory=new CharacterFactory{Registry=w.Characters};
   var players=new PlayerManager{Factory=factory};players.SpawnFactory=()=>w.Character(CharacterTeam.Player,Def(new StatBonusSkill(),bonus:new StatBonus{damage=3}));
   var projectiles=new ProjectileManager();var pauses=new GameplayPauseService();
   var game=new GameManager(saves,resource,w.Levels,players,w.Characters,factory,projectiles,pauses,w.Manager);
   var gameplay=new FlowPanel();var result=new FlowPanel();var ui=new UIManager();Set(ui,"panels",new UIPanelController[]{gameplay,result});Routes(ui,(GameState.Playing,gameplay),(GameState.Lost,result));ui.Construct(game);
   True(game.State==GameState.Boot&&ui.ActivePanel==null,"boot has no implicit default UI");
   int resets=0;w.Levels.RunReset+=()=>resets++;bool reentrantStarted=true;
   GameUIEventBus.StateChanged+=(old,next)=>{if(next==GameState.PreparingRun)reentrantStarted=game.StartRun();};
   game.Initialize();
   True(game.State==GameState.Playing&&resource.Calls==1&&resource.IsReady&&players.SpawnCount==1,"game prepares resources and spawns before playing");
   True(resets==1&&!reentrantStarted&&!game.StartRun(),"one reset per run and duplicate/reentrant start rejected");
   True(ui.ActivePanel==gameplay&&gameplay.Visible,"state bus routes playing to gameplay UI");Equal(Time.timeScale,1,"playing releases only game flow pause");
   w.Character(CharacterTeam.Enemy);var oldPlayer=players.CurrentPlayer;
   True(game.WinRun()&&game.State==GameState.Won&&!game.LoseRun(),"win ends run once");True(w.Manager.RunCount==0,"game end clears acquired skill runtimes");Equal(Time.timeScale,0,"result pauses gameplay");
   True(ui.ActivePanel==null,"unmapped result state closes old UI without fallback");
   True(game.StartRun()&&players.CurrentPlayer!=oldPlayer&&w.Characters.Characters.Count==1,"restart removes old player enemies and spawns fresh player");
   True(resource.Calls==1&&resets==2&&projectiles.Clears==2,"restart reuses prepared resources and clears old projectiles");
   w.Characters.NotifyDied(players.CurrentPlayer);
   True(game.State==GameState.Lost&&ui.ActivePanel==result,"player death ends run and routes result UI");
   var manual=pauses.Acquire();True(game.StartRun(),"retry after loss succeeds");Equal(Time.timeScale,0,"starting run preserves another pause source");manual.Dispose();Equal(Time.timeScale,1,"last manual pause restores gameplay");
   True(game.ReturnHome()&&game.State==GameState.Home&&players.CurrentPlayer==null&&w.Characters.Characters.Count==0,"home cleans world and player");
   True(ui.ActivePanel==null&&resets==4,"home clears UI and resets progression once");Equal(Time.timeScale,0,"home remains paused");
   int logged=0,delivered=0;Action<GameState,GameState> bad=(_,__)=>throw new Exception("injected UI callback failure");Action<GameState,GameState> good=(_,__)=>delivered++;
   GameUIEventBus.StateChanged+=bad;GameUIEventBus.StateChanged+=good;Debug.ExceptionSink=_=>logged++;
   True(game.StartRun()&&game.State==GameState.Playing,"UI callback error cannot abort gameplay transition");
   Debug.ExceptionSink=null;GameUIEventBus.StateChanged-=bad;GameUIEventBus.StateChanged-=good;
   True(logged==2&&delivered==2,"bus logs listener failures and still notifies remaining UI");
   game.Dispose();True(!game.StartRun()&&!game.WinRun(),"disposed manager rejects commands");
   typeof(UIManager).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,null);
   players.RemovePlayer();pauses.Dispose();w.Manager.Dispose();ResetGameBus();
  }
  {
   ResetGameBus();var w=new World();var resource=new PoolPrewarmService{Fail=true};var factory=new CharacterFactory{Registry=w.Characters};var players=new PlayerManager{Factory=factory,SpawnFactory=()=>w.Character(CharacterTeam.Player)};var pauses=new GameplayPauseService();
   var game=new GameManager(new SaveManager(),resource,w.Levels,players,w.Characters,factory,new ProjectileManager(),pauses,w.Manager);
   int logs=0;Debug.ExceptionSink=_=>logs++;game.Initialize();Debug.ExceptionSink=null;
   True(game.State==GameState.Faulted&&game.LastError!=null&&players.SpawnCount==0&&logs==1,"resource failure never spawns player or enters playing");
   Equal(Time.timeScale,0,"resource failure holds flow pause");game.Dispose();Equal(Time.timeScale,1,"dispose releases failed startup pause");
   pauses.Dispose();w.Manager.Dispose();ResetGameBus();
  }
  {
   var w=new World();var factory=new CharacterFactory{Registry=w.Characters};var players=new PlayerManager{Factory=factory,SpawnFactory=()=>w.Character(CharacterTeam.Player)};var pauses=new GameplayPauseService();
   var game=new GameManager(new SaveManager(),new PoolPrewarmService(),w.Levels,players,w.Characters,factory,new ProjectileManager(),pauses,w.Manager,false);
   game.Initialize();True(game.State==GameState.Home&&players.SpawnCount==0,"auto start off waits at home without spawning");
   True(game.StartRun(),"explicit start enters run from home");game.Dispose();players.RemovePlayer();pauses.Dispose();w.Manager.Dispose();ResetGameBus();
  }
  {
   var gameplay=new GameObject();var first=new FlowPanel();var second=new FlowPanel();var manager=new UIManager();
   var gameplayController=new GameplayController();Set(gameplayController,"panel",gameplay);Set(manager,"panels",new UIPanelController[]{gameplayController,first,second});manager.SetVisible(true);manager.SetRootPanel(gameplayController);
   True(gameplay.Active&&manager.ActivePanel==gameplayController,"flow starts at gameplay");
   manager.Back();True(manager.ActivePanel==gameplayController&&gameplay.Active,"back at root keeps root visible");
   first.Request(true);True(first.Visible&&!gameplay.Active&&manager.ActivePanel==first,"panel request switches gameplay to modal");
   second.Request(true);True(second.Visible&&!first.Visible&&manager.ActivePanel==second,"new modal suspends previous panel");
   manager.Back();True(first.Visible&&!second.Visible&&!second.WantsVisible,"back restores previous panel and closes its request");
   manager.SetVisible(false);True(!first.Visible&&!gameplay.Active,"global hide suspends whole UI flow");
   manager.SetVisible(true);True(first.Visible&&manager.ActivePanel==first,"global show restores active flow");
   first.Request(false);True(gameplay.Active&&manager.ActivePanel==gameplayController,"closing last modal restores gameplay");
   manager.OpenPanel(first);manager.OpenPanel(second);manager.ClosePanel(first);
   True(second.Visible&&manager.ActivePanel==second,"closing suspended panel preserves active panel");
   manager.SetRootPanel(gameplayController);True(gameplay.Active&&!second.Visible&&!second.WantsVisible,"explicit gameplay route clears modal history");
   second.Request(true);True(manager.ActivePanel==second,"controller can reopen after manager navigation");
   typeof(UIManager).GetMethod("OnDisable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,null);
   True(!second.Visible,"manager disable hides active modal");
   typeof(UIManager).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(manager,null);
   True(second.Visible&&manager.ActivePanel==second,"manager enable restores pending request");
   manager.OpenPanel(gameplayController);
   True(manager.ActivePanel==gameplayController&&gameplay.Active&&!second.Visible,"opening root through shared API clears navigation");
  }
  {
   var home=new FlowPanel();var overlay=new FlowPanel();var manager=new UIManager();
   Set(manager,"panels",new UIPanelController[]{home,overlay});manager.SetVisible(true);True(manager.ActivePanel==null,"no panel opens without a root request");manager.SetRootPanel(home);
   True(home.Visible&&manager.ActivePanel==home,"any panel controller can be root without gameplay dependency");
   overlay.Request(true);manager.OpenPanel(home);
   True(home.Visible&&!overlay.Visible&&!overlay.WantsVisible,"root navigation clears old flow");
   var invalid=new UIManager();bool invalidRootRejected=false;
   try{invalid.SetRootPanel(home);}catch(ArgumentException){invalidRootRejected=true;}
   True(invalidRootRejected,"root must belong to registered array");
  }

  Console.WriteLine("PASS: "+checks+" checks; level data, composition, skill sources, UI and game flow.");
 }
}
