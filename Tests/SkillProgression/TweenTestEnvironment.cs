// Host double: chỉ kiểm tra ownership/callback/pause của code feedback thật, không mô phỏng render/easing Unity.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrimeTween
{
 public enum Ease {Linear,OutQuad}
 internal sealed class TweenState
 {
  internal bool Alive=true,Paused,Grouped;
  internal float Remaining;
  internal Action Finish,Completed;
  internal readonly List<TweenState> Children=new List<TweenState>();
  internal void Stop(){Alive=false;Completed=null;foreach(var child in Children)child.Stop();}
 }
 public static class TweenHost
 {
  static readonly List<TweenState> active=new List<TweenState>();
  internal static TweenState Create(float duration,Action finish=null)
  {var state=new TweenState{Remaining=duration,Finish=finish};active.Add(state);return state;}
  public static void Advance(float delta)
  {
   foreach(var state in active.ToArray())
   {
    if(!state.Alive||state.Paused||state.Grouped)continue;
    state.Remaining-=delta*Time.timeScale;if(state.Remaining>0)continue;
    state.Alive=false;state.Finish?.Invoke();
    foreach(var child in state.Children){child.Alive=false;child.Finish?.Invoke();}
    var callback=state.Completed;state.Completed=null;callback?.Invoke();
   }
   active.RemoveAll(state=>!state.Alive);
  }
 }
 public struct Tween
 {
  internal TweenState State;
  public void Stop()=>State?.Stop();
  public static Tween Delay<T>(T target,float duration,Action<T> onComplete) where T:class
  {var state=TweenHost.Create(duration);state.Completed=()=>onComplete(target);return new Tween{State=state};}
  public static Tween Position(Transform target,Vector3 from,Vector3 to,float duration,Ease ease)
   =>new Tween{State=TweenHost.Create(duration,()=>target.position=to)};
  public static Tween LocalEulerAngles(Transform target,Vector3 from,Vector3 to,float duration,Ease ease)
   =>new Tween{State=TweenHost.Create(duration,()=>target.localEulerAngles=to)};
 }
 public struct Sequence
 {
  TweenState state;
  public bool isAlive=>state!=null&&state.Alive;
  public bool isPaused{get=>state!=null&&state.Paused;set{if(state!=null)state.Paused=value;}}
  public static Sequence Create()=>new Sequence{state=TweenHost.Create(0)};
  public Sequence Group(Tween tween){tween.State.Grouped=true;state.Children.Add(tween.State);state.Remaining=Math.Max(state.Remaining,tween.State.Remaining);return this;}
  public Sequence OnComplete(Action callback){state.Completed=callback;return this;}
  public Sequence OnComplete<T>(T target,Action<T> callback) where T:class{state.Completed=()=>callback(target);return this;}
  public void Stop()=>state?.Stop();
 }
}
