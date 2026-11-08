using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
class Program {
 static int checks;
 static void Equal(float a,float b,string label){if(Math.Abs(a-b)>.001f)throw new Exception(label+": "+a+" != "+b);checks++;}
 static void True(bool c,string label){if(!c)throw new Exception(label);checks++;}
 static void Set(object o,string field,object value)=>o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
 static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
 static SkillDataSO Def(ISkillData data,int max=5,int permanent=1,StatBonus bonus=default){
  var d=new SkillDataSO();Set(d,"behavior",data);Set(d,"maxLevel",max);Set(d,"level",permanent);Set(d,"statBonus",bonus);d.Validate();return d;
 }
 class World {
  public PoolManager Pools=new PoolManager();public CombatSpawner Spawner=new CombatSpawner();public Timers Timers=new Timers();
  public Search Search=new Search();public CharacterManager Characters=new CharacterManager();public LevelManager Levels=new LevelManager();
  public SkillFactory Factory;public SkillManager Manager;
  public World(){Factory=new SkillFactory(Spawner,new ShootBehaviorFactory(),new ProjectileMoveFactory(),Pools);Manager=new SkillManager(Levels,Timers,new Clock(),Factory,Characters);}
  public CharacterView Character(CharacterTeam team,params SkillDataSO[] defs){
   var template=new CharacterDataSO{CharacterType=CharacterType.BlessedGladiator,Team=team,BaseStats=new BaseStats{maxHealth=1000,damage=20,moveSpeed=1,critDamageMultiplier=50},Skills=defs};
   var c=new CharacterView{RuntimeData=new CharacterRuntimeData(template),Search=Search,Timers=Timers};c.Effects=new CharacterEffectHandler(c,new EffectManager());
   c.Skills=new CharacterSkills(c,defs,Factory);Characters.Characters[c]=template;Search.Targets.Add(c);return c;
  }
 }
 static OrbitSawSkill Saw(){var d=new OrbitSawSkill();Set(d,"visualPrefab",new SkillAreaVisual());return d;}
 static FrostAuraSkill Frost(){var d=new FrostAuraSkill();Set(d,"visualPrefab",new SkillAreaVisual());return d;}
 static void Main(){
  var saw=Saw();var frost=Frost();
  for(int i=1;i<=5;i++){Equal(saw.BladeCountAt(i),i,"saw count");Equal(frost.RadiusAt(i),i,"frost radius");}
  bool rejected=false;try{saw.BladeCountAt(6);}catch(ArgumentOutOfRangeException){rejected=true;}True(rejected,"saw rejects six");
  rejected=false;try{Def(frost,6);}catch(InvalidOperationException){rejected=true;}True(rejected,"frost max must be five");
  foreach(var team in new[]{CharacterTeam.Player,CharacterTeam.Enemy}){
   var w=new World();var sawData=Saw();Set(sawData,"bladeRadius",.6f);var sawDef=Def(sawData,5,5,new StatBonus{damage=10});
   var c=w.Character(team,sawDef);var victim=w.Character(CharacterSearch.OpponentOf(team));
   True(c.Skills.TryGetRuntime(sawDef,out var runtime),"innate runtime exists");Equal(runtime.RunLevel,1,"run starts one despite permanent five");Equal(runtime.PermanentLevel,5,"permanent separate");
   c.Skills.Tick(.1f);Equal(w.Pools.Active.Count,1,"one saw visual");Equal(victim.RuntimeData.CurrentHealth,970,"saw final damage");
   c.Skills.Tick(.1f);Equal(victim.RuntimeData.CurrentHealth,970,"per blade hit cooldown");
   True(w.Manager.GrantOrUpgrade(sawDef,c),"upgrade existing innate");Equal(runtime.RunLevel,2,"upgraded innate level");Equal(w.Manager.RunCount,0,"no duplicate run registration");Equal(c.RuntimeData.FinalStats.damage,30,"no duplicate stat bonus");
   c.Skills.Tick(.1f);Equal(w.Pools.Active.Count,2,"two blade visuals");Equal(victim.RuntimeData.CurrentHealth,940,"new blade has independent hit schedule");
   while(runtime.RunLevel<5)True(w.Manager.GrantOrUpgrade(sawDef,c),"upgrade to five");True(!w.Manager.GrantOrUpgrade(sawDef,c),"cap five");c.Skills.Tick(.1f);Equal(w.Pools.Active.Count,5,"five blades");
   w.Levels.Reset();Equal(runtime.RunLevel,1,"reset innate run level");Equal(runtime.PermanentLevel,5,"reset preserves permanent");Equal(c.RuntimeData.FinalStats.damage,30,"reset preserves innate bonus");Equal(w.Pools.Active.Count,1,"reset returns extra blades");
   c.Release();Equal(w.Pools.Active.Count,0,"despawn returns all saw visuals");
   w=new World();var frostDef=Def(Frost());c=w.Character(team);victim=w.Character(CharacterSearch.OpponentOf(team));victim.transform.position=new Vector3(.5f,0);
   True(w.Manager.GrantOrUpgrade(frostDef,c),"grant run frost");w.Manager.Tick();Equal(victim.RuntimeData.FinalStats.moveSpeed,.7f,"frost slows opponent");Equal(victim.RuntimeData.CurrentHealth,1000,"frost no damage");
   var friend=w.Character(team);w.Manager.Tick();Equal(friend.RuntimeData.FinalStats.moveSpeed,1,"frost ignores ally");
   victim.transform.position=new Vector3(1.5f,0);w.Manager.Tick();Equal(victim.RuntimeData.FinalStats.moveSpeed,1,"leaving removes slow immediately");
   True(w.Manager.GrantOrUpgrade(frostDef,c),"upgrade run frost");w.Manager.Tick();Equal(victim.RuntimeData.FinalStats.moveSpeed,.7f,"larger radius catches enemy");Equal(w.Manager.RunCount,1,"one run runtime");
   c.Disable();Equal(victim.RuntimeData.FinalStats.moveSpeed,1,"disable clears aura slow");c.Enable();w.Manager.Tick();Equal(victim.RuntimeData.FinalStats.moveSpeed,.7f,"enable reapplies aura");
   var c2=w.Character(team,frostDef);c2.Skills.Tick(.1f);victim.transform.position=new Vector3(.5f,0);c2.Skills.Tick(.1f);Equal(victim.RuntimeData.FinalStats.moveSpeed,.7f,"overlap uses strongest not multiplied");
   w.Levels.Reset();Equal(victim.RuntimeData.FinalStats.moveSpeed,1,"reset clears tokens until innate aura next tick");Equal(w.Manager.RunCount,0,"reset removes run frost");
   c2.Skills.Tick(.1f);Equal(victim.RuntimeData.FinalStats.moveSpeed,.7f,"innate frost survives run reset");c2.Release();Equal(victim.RuntimeData.FinalStats.moveSpeed,1,"release removes only owned aura tokens");
   w.Manager.Dispose();
  }
  // Reset invoked by a stats callback must cancel an in-flight level change and release all tokens.
  {
   var w=new World();var d=Def(Frost());var c=w.Character(CharacterTeam.Player,d);var victim=w.Character(CharacterTeam.Enemy);
   c.Skills.Tick(.1f);True(w.Manager.TryGetRuntime(d,c,out var runtime),"lookup innate frost");
   Action reset=null;reset=()=>{victim.RuntimeData.Changed-=reset;w.Levels.Reset();};victim.RuntimeData.Changed+=reset;
   True(!w.Manager.GrantOrUpgrade(d,c),"reset during upgrade cancels upgrade");Equal(runtime.RunLevel,1,"no stale level commit");Equal(victim.RuntimeData.FinalStats.moveSpeed,1,"reset removes aura token");
   c.Skills.Tick(.1f);Equal(victim.RuntimeData.FinalStats.moveSpeed,.7f,"aura can reapply after reset");
   w.Manager.Dispose();c.Release();
  }
  // Sparse, unordered dictionaries: nearest shooting milestone and cumulative effect milestones.
  var straight=new StraightShooter();var spiral=new SpiralShooter();var slow=new SlowEffect();
  var table=new ShootingLevelTable();
  Set(table,"shootingLevels",new List<ShootingLevelEntry>{
   new ShootingLevelEntry{level=6,shooter=new CircularShooter(),projectileMove=new HomingProjectileMove(),projectileCount=5},
   new ShootingLevelEntry{level=5,shooter=spiral,projectileMove=new HomingProjectileMove(),projectileCount=5},
   new ShootingLevelEntry{level=2,shooter=spiral,projectileMove=new StraightProjectileMove(),projectileCount=2}});
  Set(table,"effectLevels",new List<ShootingEffectLevelEntry>{new ShootingEffectLevelEntry{level=6,effects=new List<IGameplayEffect>{slow}},new ShootingEffectLevelEntry{level=3,effects=new List<IGameplayEffect>{slow}}});
  var lookup=table.CreateLookup(6);Equal(lookup.ResolveShooting(4,straight,new StraightProjectileMove(),1).Count,2,"sparse nearest milestone");Equal(lookup.ResolveEffects(6,Array.Empty<IGameplayEffect>()).Count,1,"effect references deduplicated");
  var templateShot=new ShootingSkill();Set(templateShot,"upgrades",table);
  var shooters=new ShootBehaviorFactory();var shot=templateShot.CreateRuntime(shooters,6);var other=templateShot.CreateRuntime(shooters,6);
  shot.ApplyRunLevel(2,shooters);other.ApplyRunLevel(2,shooters);
  var shotWorld=new World();var shooter=shotWorld.Character(CharacterTeam.Player);var moves=new ProjectileMoveFactory();var emitter=new CombatSpawner();
  shot.ExecuteSkill(emitter,shooters,moves,shooter.RuntimeData,new Vector2(),Vector2.right);Equal(emitter.Shots.Count,2,"level two count");
  var instance=Get<IShooter>(shot,"liveShooter");shot.ApplyRunLevel(5,shooters);True(ReferenceEquals(instance,Get<IShooter>(shot,"liveShooter")),"same template preserves spiral state");
  True(!ReferenceEquals(instance,Get<IShooter>(other,"liveShooter")),"shooter runtime isolated");True(shot.UsesHoming,"level five homing");
  var oldBullet=emitter.Shots[0];shot.ApplyRunLevel(6,shooters);True(Get<IShooter>(shot,"liveShooter") is CircularShooter,"six circular");Equal(oldBullet.Effects.Count,0,"old bullet effects unchanged");True(oldBullet.Movement is StraightProjectileMove,"old bullet movement unchanged");
  emitter.Shots.Clear();shot.ExecuteSkill(emitter,shooters,moves,shooter.RuntimeData,new Vector2(),Vector2.right);Equal(emitter.Shots.Count,5,"level six count");True(emitter.Shots[0].Movement is HomingProjectileMove,"new bullet homing");Equal(emitter.Shots[0].Effects.Count,1,"new bullet effect");
  shot.ApplyRunLevel(1,shooters);Equal(Get<int>(shot,"liveCount"),1,"reset shooting base count");True(!shot.UsesHoming,"reset shooting movement");Equal(Get<IReadOnlyList<IGameplayEffect>>(shot,"liveEffects").Count,0,"reset shooting effects");
  // A callback upgrade must not mix configurations inside an already-started volley.
  shot.ApplyRunLevel(2,shooters);emitter.Shots.Clear();emitter.OnSpawn=()=>shot.ApplyRunLevel(6,shooters);
  shot.ExecuteSkill(emitter,shooters,moves,shooter.RuntimeData,new Vector2(),Vector2.right);Equal(emitter.Shots.Count,2,"volley snapshots count");Equal(emitter.Shots[1].Effects.Count,0,"volley snapshots effects");
  rejected=false;Set(table,"shootingLevels",new List<ShootingLevelEntry>{new ShootingLevelEntry{level=2},new ShootingLevelEntry{level=2}});try{table.Validate(6);}catch(InvalidOperationException){rejected=true;}True(rejected,"duplicate levels rejected");
  // Choice UI policy: only current + 1, a separate five-star cap, and repeatable appearances across rolls.
  {
   var w=new World();var sawDef=Def(Saw());var frostDef=Def(Frost());var single=Def(new StatBonusSkill());
   var shotDef=Def(new ShootingSkill(),6);var owner=w.Character(CharacterTeam.Player,sawDef);
   var catalog=new[]{sawDef,frostDef,single,shotDef,sawDef};var offers=new List<SkillChoiceOffer>();var pool=new SkillChoicePool(new System.Random(19));
   pool.Build(catalog,w.Manager,owner,offers);Equal(offers.Count,3,"three choices");
   var seen=new HashSet<SkillDataSO>();foreach(var offer in offers){True(seen.Add(offer.Definition),"unique skills in one roll");True(offer.CurrentLevel==0?offer.OfferedLevel==1:offer.OfferedLevel==offer.CurrentLevel+1,"only next level offered");}
   for(int i=0;i<20;i++){pool.Build(new[]{sawDef},w.Manager,owner,offers);Equal(offers[0].OfferedLevel,2,"owned level one never offers three");}
   var stale=offers[0];True(stale.TryApply(w.Manager,owner),"apply offered next level");True(!stale.TryApply(w.Manager,owner),"stale double click rejected");
   pool.Build(new[]{sawDef},w.Manager,owner,offers);Equal(offers[0].OfferedLevel,3,"same skill may reappear at next level");
   while(w.Manager.GrantOrUpgrade(sawDef,owner)){}
   pool.Build(new[]{sawDef},w.Manager,owner,offers);Equal(offers.Count,0,"max level excluded");
   pool.Build(new[]{single},w.Manager,owner,offers);Equal(offers[0].MaxLevel,1,"single level has one star");True(offers[0].TryApply(w.Manager,owner),"grant single level skill");
   pool.Build(new[]{single},w.Manager,owner,offers);Equal(offers.Count,0,"owned single level is not a no-op reward");
   for(int i=0;i<5;i++)w.Manager.GrantOrUpgrade(shotDef,owner);
   pool.Build(new[]{shotDef},w.Manager,owner,offers);Equal(offers.Count,0,"UI does not offer sixth shooting level yet");
   var queue=new SkillChoiceProgress();queue.AddLevels(1,4);Equal(queue.Pending,3,"multi level grants three choices");True(queue.Consume(),"consume one choice");Equal(queue.Pending,2,"two choices remain");queue.AddLevels(4,5);Equal(queue.Pending,3,"new levels append to queue");queue.Clear();True(!queue.Consume(),"reset queue cannot underflow");
   w.Levels.Reset();pool.Build(new[]{sawDef},w.Manager,owner,offers);Equal(offers[0].OfferedLevel,2,"reset uses current innate level one");
   pool.Build(Array.Empty<SkillDataSO>(),w.Manager,owner,offers);Equal(offers.Count,0,"empty catalog safe");
   w.Manager.Dispose();owner.Release();
  }
  {
   Time.timeScale=.75f;var pauses=new GameplayPauseService();var manual=pauses.Acquire();var modal=pauses.Acquire();Equal(Time.timeScale,0,"pause acquired");
   modal.Dispose();Equal(Time.timeScale,0,"closing choice keeps manual pause");manual.Dispose();Equal(Time.timeScale,.75f,"last lease restores actual time scale");manual.Dispose();Equal(Time.timeScale,.75f,"release is idempotent");
   var first=pauses.Acquire();var second=pauses.Acquire();first.Dispose();Equal(Time.timeScale,0,"out of order release keeps modal pause");second.Dispose();Equal(Time.timeScale,.75f,"second order restore");
   var cleanup=pauses.Acquire();pauses.Dispose();Equal(Time.timeScale,.75f,"dispose restores pause");cleanup.Dispose();Equal(Time.timeScale,.75f,"old token cannot pause again");Time.timeScale=1;
  }
  Console.WriteLine("PASS: "+checks+" checks; production runtime, progression, saw damage, frost tokens and shooting strategies.");
 }
}
