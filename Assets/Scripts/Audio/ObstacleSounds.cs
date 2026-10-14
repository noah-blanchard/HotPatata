using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPatata
{
    /// <summary>
    /// Obstacle sounds with no wiring (ARCHITECTURE §26, docs/OBSTACLES.md): every obstacle system of the scene is found by
    /// its <see cref="IObstacleState"/> and heard through folders named after its component, e.g.
    /// SFX/Obstacles/PressurePlate/On, .../Off, SFX/Obstacles/MovingPlatform/Loop. On and Off play when the system switches
    /// (a plate pressed, a door opening, a platform turning back); Loop plays while it moves. Only systems within
    /// <see cref="GameTuning.obstacleAudibleDistance"/> of the listener sound. Reads the state, never changes it.
    /// Checkpoints and tubes are left out: the run and the bomb have their own sounds.
    /// </summary>
    public sealed class ObstacleSounds : MonoBehaviour
    {
        sealed class Tracked
        {
            public MonoBehaviour component;
            public IObstacleState state;
            public string on, off, loop;   // null when the folder is empty
            public bool known, active;
            public float progress;
            public LoopHandle sound;
        }

        const float LoopFadeIn = 0.15f, LoopFadeOut = 0.2f;   // seconds: a mover's hum starts and stops softly, never clicks

        AudioDirector director;
        readonly List<Tracked> tracked = new List<Tracked>();
        bool scanned;

        public void Init(AudioDirector owner)
        {
            director = owner;
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        void OnDestroy() => SceneManager.activeSceneChanged -= OnSceneChanged;

        void OnSceneChanged(Scene from, Scene to)
        {
            foreach (var t in tracked) t.sound.Stop();
            tracked.Clear();
            scanned = false;
        }

        void Scan()
        {
            scanned = true;
            var bank = director.Bank;
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!(mb is IObstacleState state) || mb is Checkpoint || mb is BombTransit) continue;
                string type = mb.GetType().Name;
                string on = CueKeys.Obstacle(type, CueKeys.ObstacleOn);
                string off = CueKeys.Obstacle(type, CueKeys.ObstacleOff);
                string loop = CueKeys.Obstacle(type, CueKeys.ObstacleLoop);
                if (!bank.Has(on) && !bank.Has(off) && !bank.Has(loop)) continue;   // nothing to hear: no cost
                tracked.Add(new Tracked
                {
                    component = mb, state = state,
                    on = bank.Has(on) ? on : null, off = bank.Has(off) ? off : null, loop = bank.Has(loop) ? loop : null,
                });
            }
        }

        void Update()
        {
            if (!scanned)
            {
                if (RunManager.Instance == null) return;   // menus have no obstacles
                Scan();
            }
            var listener = director.Listener;
            if (listener == null) return;
            var t = director.Tuning;
            float range = t.obstacleAudibleDistance;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                var o = tracked[i];
                if (o.component == null)
                {
                    o.sound.Stop();
                    tracked.RemoveAt(i);
                    continue;
                }
                bool active = o.state.Active;
                float progress = o.state.Progress;
                var where = o.component.transform;
                bool near = o.component.isActiveAndEnabled && (where.position - listener.position).sqrMagnitude <= range * range;

                if (!o.known)
                {
                    o.known = true;
                    o.active = active;
                    o.progress = progress;
                    continue;
                }

                if (active != o.active && near)
                {
                    var key = active ? o.on : o.off;
                    if (key != null) director.Play(key, where.position, 1f, where);
                }
                o.active = active;

                if (o.loop != null)
                {
                    float delta = Mathf.Abs(progress - o.progress);
                    if (delta > 0.5f) delta = 1f - delta;   // a turning obstacle wraps from 1 to 0
                    bool moving = delta / dt > t.obstacleLoopMinRate;
                    if (moving && near && !o.sound.IsPlaying) o.sound = director.Loop(o.loop, where, 1f, LoopFadeIn);
                    else if ((!moving || !near) && o.sound.IsPlaying) o.sound.Stop(LoopFadeOut);
                }
                o.progress = progress;
            }
        }
    }
}
