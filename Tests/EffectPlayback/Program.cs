// Test doubles outside Assets; compile the production visual lifecycle without Unity native playback.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;

class Program
{
    static int checks;
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    sealed class Visual : IEffectVisual
    {
        public event Action Completed;
        public int Plays, Stops;
        public void Play() { Plays++; }
        public void StopAndReset() { Stops++; }
        public void Complete() => Completed?.Invoke();
        public Action Snapshot => Completed;
    }

    static void Main()
    {
        var animator = new Animator();
        var animation = new AnimationEffectVisual();
        animation.gameObject.Components.Add(animator);
        animation.SetVisual(new AnimationClip { length = .9f });
        int finished = 0; animation.Completed += () => finished++;
        animation.Play(); var firstGraph = PlayableGraph.Last;
        firstGraph.Time = .2; Call(animation, "LateUpdate"); Check(finished == 0, "animation survives old .2 second timeout");
        firstGraph.Time = .89; Call(animation, "LateUpdate"); Check(finished == 0, "animation waits until last frame interval ends");
        firstGraph.Time = .9; Call(animation, "LateUpdate"); Call(animation, "LateUpdate"); Check(finished == 1, "animation completes exactly once");
        animation.Play(); Check(!firstGraph.Valid && PlayableGraph.Last.Time == 0, "pool replay destroys old graph and starts at zero");
        Set(animation, "playbackSpeed", 2f); animation.Play(); Check(PlayableGraph.Last.Speed == 2, "playback speed applied to graph");
        animation.StopAndReset(); Check(!PlayableGraph.Last.Valid && finished == 1, "cancellation frees graph without completion");
        animation.Play(); Call(animation, "OnDisable"); Check(!PlayableGraph.Last.Valid && finished == 1, "disable cancels animation without completion");
        animation.SetVisual(new AnimationClip { length = 2 }); animation.Play();
        PlayableGraph.Last.Time = 1; Call(animation, "LateUpdate"); Check(finished == 1, "replacement clip uses its own duration");
        PlayableGraph.Last.Time = 2; Call(animation, "LateUpdate"); Check(finished == 2, "replacement clip completes"); animation.StopAndReset();

        var particles = new ParticleSystem(); var child = new ParticleSystem();
        var particleVisual = new ParticleEffectVisual();
        particleVisual.gameObject.Components.Add(particles); particleVisual.gameObject.Components.Add(child);
        int particleFinished = 0; particleVisual.Completed += () => particleFinished++;
        particleVisual.Play();
        Check(!particles.Settings.loop && !child.Settings.loop, "all particle systems are one-shot");
        Check(!particles.Settings.useUnscaledTime && !child.Settings.useUnscaledTime, "particles use gameplay time");
        Check(child.Settings.stopAction == ParticleSystemStopAction.None, "children cannot destroy pooled object");
        particles.Alive = true; Call(particleVisual, "LateUpdate"); Check(particleFinished == 0 && particles.CheckedChildren, "wait includes child particles");
        particles.Alive = false; Call(particleVisual, "LateUpdate"); Call(particleVisual, "LateUpdate"); Check(particleFinished == 1, "particles finish exactly once after final particle dies");
        particleVisual.Play(); particleVisual.StopAndReset(); Call(particleVisual, "LateUpdate"); Check(particleFinished == 1 && !particles.Alive, "particle cancellation clears particles without completion");
        particleVisual.Play(); Call(particleVisual, "OnDisable"); Check(particleFinished == 1 && !particles.Alive, "disable clears particles");

        var visual = new Visual(); var clone = new GameObject(); clone.Components.Add(visual);
        var pool = new PoolManager { Next = clone }; var shell = new EffectView(); shell.Construct(pool);
        var spawner = new CombatSpawner(); spawner.OnRelease = shell.Clear;
        shell.Initialize(spawner, new GameObject()); Call(shell, "OnEnable");
        Check(visual.Plays == 1 && spawner.Releases == 0, "shell starts visual without an expiry timer");
        Action stale = visual.Snapshot;
        visual.Complete(); Check(spawner.Releases == 1 && pool.Returns == 1 && visual.Stops == 1, "completion returns visual before shell release");
        visual.Complete(); Check(spawner.Releases == 1, "cleanup removes completion listener");
        shell.Initialize(spawner, new GameObject()); Call(shell, "OnEnable");
        stale(); Check(spawner.Releases == 1, "previous spawn callback cannot release reused shell");
        Call(shell, "OnDisable"); visual.Complete(); Check(spawner.Releases == 1 && pool.Returns == 1, "disable cancels without reparenting child");
        Call(shell, "OnEnable"); visual.Complete(); Check(spawner.Releases == 2 && pool.Returns == 2, "reenable subscribes once and can complete normally");
        Console.WriteLine("PASS: " + checks + " effect playback and pooling checks.");
    }
}

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(float value) { } }
    public class RequireComponent : Attribute { public RequireComponent(Type type) { } }
    public struct Vector3 { }
    public struct Quaternion { }
    public class Transform { public Vector3 position, localPosition, localScale; public Quaternion rotation, localRotation; }
    public class GameObject
    {
        public readonly Transform transform = new Transform();
        public readonly List<object> Components = new List<object>();
        public T GetComponent<T>() where T : class { foreach (var c in Components) if (c is T t) return t; return null; }
    }
    public class MonoBehaviour
    {
        public readonly GameObject gameObject = new GameObject();
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
        public T[] GetComponentsInChildren<T>(bool includeInactive) { var all = new List<T>(); foreach (var c in gameObject.Components) if (c is T t) all.Add(t); return all.ToArray(); }
    }
    public class Animator { }
    public class AnimationClip { public float length; }
    public enum ParticleSystemStopAction { None, Destroy }
    public enum ParticleSystemStopBehavior { StopEmittingAndClear }
    public class ParticleSystem
    {
        public class SettingsData { public bool playOnAwake = true, loop = true, useUnscaledTime = true; public ParticleSystemStopAction stopAction = ParticleSystemStopAction.Destroy; }
        public readonly SettingsData Settings = new SettingsData();
        public struct MainModule
        {
            public SettingsData Data;
            public bool playOnAwake { set => Data.playOnAwake = value; }
            public bool loop { set => Data.loop = value; }
            public bool useUnscaledTime { set => Data.useUnscaledTime = value; }
            public ParticleSystemStopAction stopAction { set => Data.stopAction = value; }
        }
        public MainModule main => new MainModule { Data = Settings };
        public bool Alive, CheckedChildren;
        public void Play(bool children) { Alive = true; }
        public void Stop(bool children, ParticleSystemStopBehavior behavior) { Alive = false; }
        public bool IsAlive(bool children) { CheckedChildren = children; return Alive; }
    }
}
namespace UnityEngine.Playables
{
    public enum DirectorUpdateMode { GameTime }
    public class GraphState { public bool Valid = true; public double Time, Speed = 1; }
    public struct PlayableGraph
    {
        public static GraphState Last;
        public GraphState State;
        public static PlayableGraph Create(string name) { Last = new GraphState(); return new PlayableGraph { State = Last }; }
        public void SetTimeUpdateMode(DirectorUpdateMode mode) { }
        public bool IsValid() => State != null && State.Valid;
        public void Play() { }
        public void Stop() { }
        public void Evaluate(float time) { }
        public void Destroy() { State.Valid = false; }
    }
}
namespace UnityEngine.Animations
{
    public struct AnimationClipPlayable
    {
        public GraphState State;
        public static AnimationClipPlayable Create(PlayableGraph graph, AnimationClip clip) => new AnimationClipPlayable { State = graph.State };
        public void SetApplyFootIK(bool value) { }
        public void SetDuration(double value) { }
        public void SetSpeed(double speed) { State.Speed = speed; }
        public double GetTime() => State.Time;
    }
    public struct AnimationPlayableOutput
    {
        public static AnimationPlayableOutput Create(PlayableGraph graph, string name, Animator animator) => default;
        public void SetSourcePlayable(AnimationClipPlayable playable) { }
    }
}
namespace Zenject { public class InjectAttribute : Attribute { } }
public enum PoolGroup { Other }
public class PoolManager
{
    public GameObject Next; public int Returns;
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, PoolGroup group, Transform parent) => Next;
    public void Despawn(GameObject instance) { Returns++; }
}
public class CombatSpawner
{
    public Action OnRelease; public int Releases;
    public void Release(GameObject instance) { Releases++; OnRelease?.Invoke(); }
}
