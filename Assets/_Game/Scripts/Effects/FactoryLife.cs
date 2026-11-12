using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TownBuilder
{
    /// <summary>
    /// Makes production buildings come alive (v0.37.0), using each building's <see cref="WorkingAnimation"/>:
    /// * Idle (nothing being made): sleepy "Z" letters drift up.
    /// * Working: a stream of grains falls from the chute, dust puffs rise, a worker carries sacks between two points,
    ///   another works on the spot, and the building gently squashes and stretches.
    /// Purely cosmetic: everything is pooled sprites, driven by <see cref="Step"/> (Update, or the editor preview).
    /// </summary>
    public sealed class FactoryLife : MonoBehaviour
    {
        public enum State
        {
            Idle,
            Working,
        }

        [SerializeField] private TownBuildingController buildings;
        [SerializeField] private ProductionController production;
        [SerializeField] private Sprite sleepSprite;
        [SerializeField] private Sprite grainSprite;
        [SerializeField] private Sprite dustSprite;
        [SerializeField] private Sprite sackSprite;
        [SerializeField] private Sprite[] workerSprites = new Sprite[0];
        [Tooltip("True if the worker pictures face left (they are flipped when walking right).")]
        [SerializeField] private bool workersFaceLeft = true;
        [SerializeField] private float walkSpeed = 0.32f;
        [Tooltip("Workers are drawn a little smaller than townsfolk so they fit next to a building.")]
        [SerializeField] private float workerScale = 0.75f;

        private sealed class Particle
        {
            public SpriteRenderer Renderer;
            public Vector3 Origin;
            public Vector2 Velocity;
            public float Gravity;
            public float Age;
            public float Lifetime;
            public float Scale0, Scale1;
            public float Wobble;
            public Color Color;
        }

        private sealed class Life
        {
            public BuildingView View;
            public State State;
            public float Clock;
            public float NextGrain, NextDust, NextSleep;
            public int SleepLeft; // Z letters left in the current burst
            public SpriteRenderer Carrier, Sack, Worker;
            public float WalkT;       // 0..1 along the walk
            public int WalkDir = 1;   // +1 from → to (carrying), -1 back
            public float Pause;
        }

        private const int SleepBurst = 3;

        private readonly Dictionary<string, Life> _lives = new Dictionary<string, Life>();
        private readonly List<Particle> _particles = new List<Particle>();
        private readonly Stack<Particle> _pool = new Stack<Particle>();
        private readonly List<string> _gone = new List<string>();
        private float _nextScan;
        private int _workerPick;

        public int ParticleCount => _particles.Count;

        /// <summary>The state a building is shown in (tests), or null if it has no working animation.</summary>
        public State? StateOf(string instanceId) => _lives.TryGetValue(instanceId, out var l) ? l.State : (State?)null;

        public bool WorkersVisible(string instanceId) =>
            _lives.TryGetValue(instanceId, out var l) && ((l.Carrier != null && l.Carrier.enabled) || (l.Worker != null && l.Worker.enabled));

        private void Update()
        {
            if (Time.unscaledTime >= _nextScan) Scan();
            Step(Time.unscaledDeltaTime);
        }

        /// <summary>Re-reads which factories are working (every half second).</summary>
        public void Scan()
        {
            _nextScan = Time.unscaledTime + 0.5f;
            if (production == null || production.Factories == null || buildings == null) return;
            _gone.Clear();
            _gone.AddRange(_lives.Keys);
            foreach (var id in production.BuiltFactoryIds())
            {
                if (!buildings.TryGetView(id, out var view)) continue;
                _gone.Remove(id);
                SetState(view, production.IsWorking(id) ? State.Working : State.Idle);
            }

            foreach (var id in _gone) Remove(id);
        }

        /// <summary>Shows a building as idle or working (Scan, and the editor preview).</summary>
        public void SetState(BuildingView view, State state)
        {
            if (view == null || view.Definition == null || view.Definition.Working == null || !view.Definition.Working.enabled) return;
            if (!_lives.TryGetValue(view.InstanceId ?? view.name, out var life))
            {
                life = new Life { View = view, State = (State)(-1) };
                _lives[view.InstanceId ?? view.name] = life;
            }

            if (life.State == state) return;
            life.State = state;
            if (state == State.Idle)
            {
                // First burst after a short random wait, so idle buildings don't snore in step.
                life.NextSleep = life.Clock + Random.Range(0.5f, 4f);
                life.SleepLeft = SleepBurst;
            }
            bool working = state == State.Working;
            var spec = view.Definition.Working;
            if (working && WorkingAnimation.IsSet(spec.walkFrom) && life.Carrier == null) life.Carrier = CreateWorker(view, "Carrier", out life.Sack);
            if (working && WorkingAnimation.IsSet(spec.workerSpot) && life.Worker == null) life.Worker = CreateWorker(view, "Worker", out _);
            if (life.Carrier != null) { life.Carrier.enabled = working; life.Sack.enabled = false; }
            if (life.Worker != null) life.Worker.enabled = working;
            if (!working) ResetScale(view);
        }

        /// <summary>Advances every animation by <paramref name="dt"/> seconds.</summary>
        public void Step(float dt)
        {
            foreach (var life in _lives.Values)
            {
                if (life.View == null) continue;
                life.Clock += dt;
                if (life.State == State.Working) StepWorking(life, dt);
                else StepIdle(life);
            }

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.Age += dt;
                if (p.Age >= p.Lifetime) { Release(i); continue; }
                Layout(p);
            }
        }

        // ------------------------------------------------------------------ States

        private void StepIdle(Life life)
        {
            var spec = life.View.Definition.Working;
            if (!WorkingAnimation.IsSet(spec.sleepPoint) || sleepSprite == null || life.Clock < life.NextSleep) return;
            // Bursts: three Z letters half a second apart, then a 5-10 s rest.
            if (--life.SleepLeft > 0) life.NextSleep = life.Clock + 0.55f;
            else
            {
                life.SleepLeft = SleepBurst;
                life.NextSleep = life.Clock + Random.Range(5f, 10f);
            }

            Spawn(sleepSprite, PointWorld(life.View, spec.sleepPoint), new Vector2(0.12f, 0.32f), 0f, 2.2f, 0.35f, 0.75f,
                Color.white, wobble: 1f);
        }

        private void StepWorking(Life life, float dt)
        {
            var view = life.View;
            var spec = view.Definition.Working;

            if (WorkingAnimation.IsSet(spec.streamPoint) && grainSprite != null)
            {
                while (life.NextGrain <= life.Clock)
                {
                    life.NextGrain += 0.045f;
                    // Falls streamFall in 0.32 s from a small push down: gravity = 2(d - v0 t) / t².
                    const float t = 0.32f, v0 = 0.12f;
                    float g = 2f * (spec.streamFall - v0 * t) / (t * t);
                    var start = PointWorld(view, spec.streamPoint) + new Vector3(Random.Range(-0.012f, 0.012f), 0f, 0f);
                    var c = spec.streamColor * Random.Range(0.85f, 1.1f);
                    c.a = 1f;
                    Spawn(grainSprite, start, new Vector2(Random.Range(-0.03f, 0.03f), -v0), -g, t, 0.9f, 0.9f, c);
                }
            }

            if (WorkingAnimation.IsSet(spec.dustPoint) && dustSprite != null && life.Clock >= life.NextDust)
            {
                life.NextDust = life.Clock + Random.Range(0.45f, 0.8f);
                Spawn(dustSprite, PointWorld(view, spec.dustPoint) + new Vector3(Random.Range(-0.04f, 0.04f), 0f, 0f),
                    new Vector2(Random.Range(-0.06f, 0.06f), 0.16f), 0f, 1.1f, 0.12f, 0.32f, spec.dustColor);
            }

            if (life.Carrier != null) StepCarrier(life, spec, dt);
            if (life.Worker != null) StepWorker(life, spec);

            // Rumble: squash and stretch around the base (the pivot), unless a "pop" is playing.
            if (spec.rumble > 0f)
            {
                var pop = view.GetComponent<PopAnimator>();
                if (pop == null || !pop.IsPlaying)
                {
                    float w = Mathf.Sin(life.Clock * Mathf.PI * 2f * 1.7f) * spec.rumble;
                    view.transform.localScale = new Vector3(1f - w * 0.5f, 1f + w, 1f);
                }
            }
        }

        private void StepCarrier(Life life, WorkingAnimation spec, float dt)
        {
            var from = PointWorld(life.View, spec.walkFrom);
            var to = PointWorld(life.View, spec.walkTo);
            float length = Mathf.Max(0.05f, Vector3.Distance(from, to));
            if (life.Pause > 0f) life.Pause -= dt;
            else
            {
                life.WalkT += life.WalkDir * walkSpeed * dt / length;
                if (life.WalkT >= 1f || life.WalkT <= 0f)
                {
                    life.WalkT = Mathf.Clamp01(life.WalkT);
                    life.WalkDir = -life.WalkDir;
                    life.Pause = 0.5f; // drops off / picks up a sack
                }
            }

            bool walking = life.Pause <= 0f;
            float bob = walking ? Mathf.Abs(Mathf.Sin(life.Clock * Mathf.PI * 2.4f)) * 0.022f : 0f;
            var pos = Vector3.Lerp(from, to, life.WalkT);
            life.Carrier.transform.position = pos + new Vector3(0f, bob, 0f);
            float screenDx = (to.x - from.x) * life.WalkDir;
            if (Mathf.Abs(screenDx) > 0.001f) life.Carrier.flipX = workersFaceLeft ? screenDx > 0f : screenDx < 0f;

            // Carries a sack on the way from → to (out of the mill to the pile).
            bool carrying = life.WalkDir > 0 ? !(life.Pause > 0f && life.WalkT <= 0f) : life.Pause > 0f && life.WalkT >= 1f;
            life.Sack.enabled = carrying && sackSprite != null;
            if (life.Sack.enabled)
            {
                var top = life.Carrier.bounds.max.y;
                life.Sack.transform.position = new Vector3(pos.x, top - 0.02f + bob, 0f);
                life.Sack.sortingOrder = life.Carrier.sortingOrder + 1;
            }
        }

        private void StepWorker(Life life, WorkingAnimation spec)
        {
            // Busy on the spot: a quick shovel-like dip and stretch, facing the building.
            var pos = PointWorld(life.View, spec.workerSpot);
            float k = Mathf.Sin(life.Clock * Mathf.PI * 2f * 1.25f);
            life.Worker.transform.position = pos;
            life.Worker.transform.localScale = new Vector3(workerScale * (1f + k * 0.04f), workerScale * (1f - k * 0.07f), 1f);
            life.Worker.flipX = !workersFaceLeft;
        }

        // ------------------------------------------------------------------ Helpers

        /// <summary>World position of a 0-1 point on the building's sprite (same maths as the chimney point).</summary>
        public static Vector3 PointWorld(BuildingView view, Vector2 point)
        {
            // Unscaled: the rumble must not move the effects. Follows the rotated facing.
            return view.transform.position + view.PointLocal(point);
        }

        /// <summary>
        /// Workers belong to their building: they are its children and the building gets a <see cref="SortingGroup"/>, so
        /// they are drawn in front of their own building but still behind buildings standing in front of it.
        /// </summary>
        private SpriteRenderer CreateWorker(BuildingView view, string name, out SpriteRenderer sack)
        {
            if (view.GetComponent<SortingGroup>() == null)
            {
                var group = view.gameObject.AddComponent<SortingGroup>();
                group.sortingLayerName = SortingLayers.Objects;
            }

            var go = new GameObject(name);
            go.transform.SetParent(view.transform, false);
            go.transform.localScale = Vector3.one * workerScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = workerSprites.Length > 0 ? workerSprites[_workerPick++ % workerSprites.Length] : null;
            sr.sortingLayerName = SortingLayers.Objects;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            sr.sortingOrder = 2; // in front of its own building (inside the building's sorting group)
            var sackGo = new GameObject("Sack");
            sackGo.transform.SetParent(go.transform, false);
            sack = sackGo.AddComponent<SpriteRenderer>();
            sack.sprite = sackSprite;
            sack.sortingLayerName = SortingLayers.Objects;
            sack.spriteSortPoint = SpriteSortPoint.Pivot;
            sack.sortingOrder = 3;
            sack.enabled = false;
            return sr;
        }

        private void Spawn(Sprite sprite, Vector3 origin, Vector2 velocity, float gravity, float lifetime, float scale0, float scale1,
            Color color, float wobble = 0f)
        {
            var p = _pool.Count > 0 ? _pool.Pop() : CreateParticle();
            p.Renderer.sprite = sprite;
            p.Origin = origin;
            p.Velocity = velocity;
            p.Gravity = gravity;
            p.Age = 0f;
            p.Lifetime = lifetime;
            p.Scale0 = scale0;
            p.Scale1 = scale1;
            p.Color = color;
            p.Wobble = wobble;
            p.Renderer.gameObject.SetActive(true);
            _particles.Add(p);
            Layout(p);
        }

        private static void Layout(Particle p)
        {
            float t = p.Age, k = Mathf.Clamp01(p.Age / p.Lifetime);
            var offset = new Vector3(p.Velocity.x * t + Mathf.Sin(t * 3.2f) * 0.05f * p.Wobble, p.Velocity.y * t + 0.5f * p.Gravity * t * t, 0f);
            p.Renderer.transform.position = p.Origin + offset;
            p.Renderer.transform.localScale = Vector3.one * Mathf.Lerp(p.Scale0, p.Scale1, k);
            float alpha = k < 0.12f ? k / 0.12f : k > 0.7f ? 1f - (k - 0.7f) / 0.3f : 1f;
            p.Renderer.color = new Color(p.Color.r, p.Color.g, p.Color.b, p.Color.a * alpha);
        }

        private Particle CreateParticle()
        {
            var go = new GameObject("LifeParticle");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = SortingLayers.Effects;
            return new Particle { Renderer = sr };
        }

        private void Release(int index)
        {
            var p = _particles[index];
            _particles.RemoveAt(index);
            p.Renderer.gameObject.SetActive(false);
            _pool.Push(p);
        }

        private void Remove(string id)
        {
            var life = _lives[id];
            if (life.View != null) ResetScale(life.View);
            if (life.Carrier != null) Destroy(life.Carrier.gameObject);
            if (life.Worker != null) Destroy(life.Worker.gameObject);
            _lives.Remove(id);
        }

        private static void ResetScale(BuildingView view)
        {
            var pop = view.GetComponent<PopAnimator>();
            if (pop == null || !pop.IsPlaying) view.transform.localScale = Vector3.one;
        }
    }
}
