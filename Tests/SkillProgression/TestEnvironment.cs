// Test doubles only. This folder is outside Assets and is never compiled into the Unity game.
using System;
using System.Collections.Generic;
using Unity.Mathematics;
namespace UnityEngine {
 public static class Time { public static float timeScale = 1f; }
 [AttributeUsage(AttributeTargets.All)] public class SerializeField:Attribute {}
 [AttributeUsage(AttributeTargets.All)] public class SerializeReference:Attribute {}
 [AttributeUsage(AttributeTargets.All)] public class TextAreaAttribute:Attribute {}
 [AttributeUsage(AttributeTargets.All)] public class HeaderAttribute:Attribute {public HeaderAttribute(string s){} }
 [AttributeUsage(AttributeTargets.All)] public class MinAttribute:Attribute {public MinAttribute(float f){} }
 [AttributeUsage(AttributeTargets.All)] public class RangeAttribute:Attribute {public RangeAttribute(float a,float b){} }
 [AttributeUsage(AttributeTargets.All)] public class InspectorNameAttribute:Attribute {public InspectorNameAttribute(string s){} }
 [AttributeUsage(AttributeTargets.All)] public class CreateAssetMenuAttribute:Attribute {public string menuName;}
 public class ScriptableObject {public string name;}
 public class Sprite {}
 public static class Debug {public static void LogException(Exception e,object context=null)=>throw new Exception("Unexpected log",e);}
 public static class Random {public static float value=>0.5f;}
 public static class Mathf {
  public const float PI=(float)Math.PI,Deg2Rad=PI/180f;
  public static float Min(float a,float b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);
  public static float Clamp(float n,float a,float b)=>Math.Clamp(n,a,b);public static int Clamp(int n,int a,int b)=>Math.Clamp(n,a,b);
  public static float Repeat(float v,float n)=>v-(float)Math.Floor(v/n)*n;
  public static float Cos(float v)=>(float)Math.Cos(v);public static float Sin(float v)=>(float)Math.Sin(v);
  public static int CeilToInt(float v)=>(int)Math.Ceiling(v);
 }
 public struct Vector2 {
  public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
  public float sqrMagnitude=>x*x+y*y;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
  public Vector2 normalized=>magnitude>0?this*(1/magnitude):zero;
  public static Vector2 right=>new Vector2(1,0);public static Vector2 zero=>new Vector2();
  public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
  public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
  public static Vector2 operator *(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
  public static float Distance(Vector2 a,Vector2 b)=>(a-b).magnitude;
  public static Vector2 Lerp(Vector2 a,Vector2 b,float t)=>a+(b-a)*t;
 }
 public struct Vector3 {
  public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}
  public static implicit operator Vector2(Vector3 v)=>new Vector2(v.x,v.y);
  public static implicit operator Vector3(Vector2 v)=>new Vector3(v.x,v.y);
 }
 public struct Quaternion {public static Quaternion identity=>new Quaternion();}
 public class Transform {public Vector3 position;}
 public class GameObject {
  public SkillAreaVisual Visual;public bool Active;
  public T GetComponent<T>() where T:class=>Visual as T;
 }
}
namespace UnityEngine.Serialization {public class FormerlySerializedAsAttribute:Attribute {public FormerlySerializedAsAttribute(string s){} } }
namespace Zenject {public interface ITickable {void Tick();}}
public struct SpatialEntity {public float2 Center,HalfSize,AxisX,AxisY;}
public enum BulletType {Default}
public enum PoolGroup {Other}
public class SkillAreaVisual {
 public readonly UnityEngine.GameObject gameObject;
 public float Radius;public UnityEngine.Vector2 Offset;public bool Visible;
 public SkillAreaVisual(){gameObject=new UnityEngine.GameObject{Visual=this};}
 public void Initialize(CharacterView owner,float radius){Radius=radius;Visible=true;}
 public void SetRadius(float r)=>Radius=r;public void SetPose(UnityEngine.Vector2 p,float a)=>Offset=p;
 public void SetVisible(bool v)=>Visible=v;
}
public class PoolManager {
 public readonly HashSet<UnityEngine.GameObject> Active=new HashSet<UnityEngine.GameObject>();
 public UnityEngine.GameObject Spawn(UnityEngine.GameObject prefab,UnityEngine.Vector3 p,UnityEngine.Quaternion q,
  PoolGroup group=PoolGroup.Other,UnityEngine.Transform parent=null,Action<UnityEngine.GameObject> beforeActivate=null){
  var obj=new SkillAreaVisual().gameObject;beforeActivate?.Invoke(obj);obj.Active=true;Active.Add(obj);return obj;
 }
 public void Despawn(UnityEngine.GameObject obj){obj.Active=false;Active.Remove(obj);}
}
public interface IGameplayEffect {void Validate();}
public class SlowEffect:IGameplayEffect {public void Validate(){} }
public struct ProjectileSpawnData {
 public DamageInfo Damage;public IProjectileMove Movement;public IReadOnlyList<IGameplayEffect> Effects;
 public ProjectileSpawnData(BulletType b,DamageInfo d,float s,float l,float r,IProjectileMove m,IReadOnlyList<IGameplayEffect> e,float h){Damage=d;Movement=m;Effects=e;}
}
public class CombatSpawner {
 public readonly List<ProjectileSpawnData> Shots=new List<ProjectileSpawnData>();public Action OnSpawn;
 public bool SpawnBullet(ProjectileSpawnData d,UnityEngine.Vector2 p,UnityEngine.Vector2 dir,CharacterTarget t){Shots.Add(d);OnSpawn?.Invoke();return true;}
}
public class NormalAttackSkill:ISkill,ISkillData {public NormalAttackSkill(){}public NormalAttackSkill(CharacterSkills o,float r){}public bool TryExecute()=>false;}
public class RegenerationSkill:ISkill,ISkillData {public float HealAmount=>1;public RegenerationSkill(){}public RegenerationSkill(CharacterSkills o,float h){}public bool TryExecute()=>false;}
public class EmergencyShieldSkill:ISkill,ISkillData {public float ShieldAmount=>1;public float HealthThreshold=>20;public EmergencyShieldSkill(){}public EmergencyShieldSkill(CharacterSkills o,float t,float s){}public bool TryExecute()=>false;}
public class CharacterDataSO {
 public CharacterType CharacterType;public CharacterTeam Team;public string DisplayName;public BaseStats BaseStats;
 public SkillDataSO[] Skills=Array.Empty<SkillDataSO>();public void ValidateStats()=>BaseStats.Validate();
}
public class CharacterView {
 public CharacterRuntimeData RuntimeData;public CharacterSkills Skills;public CharacterEffectHandler Effects;
 public readonly UnityEngine.Transform transform=new UnityEngine.Transform();public UnityEngine.Vector3 HitPosition=>transform.position;
 public bool isActiveAndEnabled=true;public Search Search;public ITimerManager Timers;
 public event Action<CharacterView> Died,Released;public event Action Deactivated;
 public DamageResult ReceiveDamage(DamageInfo d){
  if(!RuntimeData.IsAlive||d.SourceTeam==RuntimeData.Team)return default;
  float shield=Math.Min(RuntimeData.CurrentShield,d.Amount);float health=Math.Min(RuntimeData.CurrentHealth,d.Amount-shield);
  RuntimeData.SetVitals(RuntimeData.CurrentHealth-health,RuntimeData.CurrentShield-shield);
  if(!RuntimeData.IsAlive){Died?.Invoke(this);Skills?.Dispose();Effects?.Dispose();}
  return new DamageResult(health,shield,!RuntimeData.IsAlive);
 }
 public SpatialEntity GetSpatialEntity()=>new SpatialEntity{Center=new float2(HitPosition.x,HitPosition.y),HalfSize=new float2(.05f,.05f),AxisX=new float2(1,0),AxisY=new float2(0,1)};
 public void Disable(){isActiveAndEnabled=false;Skills?.SetPaused(true);Deactivated?.Invoke();}
 public void Enable(){isActiveAndEnabled=true;Skills?.SetPaused(false);}
 public void Release(){Released?.Invoke(this);Skills?.Dispose();Effects?.Dispose();}
}
public class Search {
 public readonly List<CharacterView> Targets=new List<CharacterView>();
 public int FindAll(UnityEngine.Vector2 c,float r,CharacterTeam team,ref CharacterTarget[] buffer,CharacterView exclude=null){
  if(buffer.Length<Targets.Count)buffer=new CharacterTarget[Targets.Count];int n=0;
  foreach(var t in Targets)if(t!=exclude&&t.isActiveAndEnabled&&t.RuntimeData.IsAlive&&t.RuntimeData.Team==team&&SpatialGeometry.CircleOverlapsBox(new float2(c.x,c.y),r,t.GetSpatialEntity()))buffer[n++]=new CharacterTarget(t);
  return n;
 }
 public CharacterTarget FindNearest(UnityEngine.Vector2 c,float r,CharacterTeam team,CharacterView exclude=null){CharacterTarget[] b=Array.Empty<CharacterTarget>();return FindAll(c,r,team,ref b,exclude)>0?b[0]:default;}
}
public static class CharacterSearch {public static CharacterTeam OpponentOf(CharacterTeam t)=>t==CharacterTeam.Player?CharacterTeam.Enemy:CharacterTeam.Player;}
public class EffectManager {internal int CountFor(CharacterView o,CharacterRuntimeData i)=>0;internal void ClearTarget(CharacterView o,CharacterRuntimeData i){} }
public struct TimerHandle {public int Id;}
public struct TimerResult {}
public interface ITimerListener {void OnTimer(TimerHandle h,TimerResult r);}
public interface ITimerManager {
 float DeltaTime{get;}TimerHandle Register(float d,ITimerListener l,float interval=0,bool paused=false);
 float GetRemaining(TimerHandle h);bool Restart(TimerHandle h,float d,bool resetInterval=false);bool SetPaused(TimerHandle h,bool p);bool Cancel(TimerHandle h);
}
public interface ITimeSource {float DeltaTime{get;}}
public class Clock:ITimeSource {public float DeltaTime=>.1f;}
public class Timers:ITimerManager {
 int next;public float DeltaTime=>.1f;public readonly HashSet<int> Live=new HashSet<int>();public readonly Dictionary<int,float> Remaining=new Dictionary<int,float>();
 public TimerHandle Register(float d,ITimerListener l,float interval=0,bool paused=false){Live.Add(++next);Remaining[next]=d;return new TimerHandle{Id=next};}
 public float GetRemaining(TimerHandle h)=>Remaining.TryGetValue(h.Id,out float r)?r:0;
 public bool Restart(TimerHandle h,float d,bool resetInterval=false){Remaining[h.Id]=d;return true;}
 public bool SetPaused(TimerHandle h,bool p)=>true;public bool Cancel(TimerHandle h){Remaining.Remove(h.Id);return Live.Remove(h.Id);}
}
public class LevelManager {public event Action RunReset;public void Reset()=>RunReset?.Invoke();}
public class CharacterManager {public readonly Dictionary<CharacterView,CharacterDataSO> Characters=new Dictionary<CharacterView,CharacterDataSO>();}
