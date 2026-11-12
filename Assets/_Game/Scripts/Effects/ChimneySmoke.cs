using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;

namespace TownBuilder
{
    /// <summary>
    /// Smoke puffs rising from the chimney of every factory that is producing right now (an active job in its queue).
    /// The chimney spot comes from <see cref="BuildingDefinition.ChimneyPoint"/> (a point on the building's sprite), so it
    /// follows the art. Puffs are pooled sprites on the Effects layer; purely cosmetic.
    /// </summary>
    public sealed class ChimneySmoke : MonoBehaviour
    {
        [SerializeField] private TownBuildingController buildings;
        [SerializeField] private ProductionController production;
        [SerializeField] private Sprite puffSprite;
        [SerializeField] private Color color = new Color(0.94f, 0.94f, 0.96f, 0.75f);
        [SerializeField] private float puffInterval = 0.85f;
        [SerializeField] private float lifetime = 2.4f;
        [SerializeField] private float rise = 0.9f;
        [SerializeField] private float drift = 0.3f;
        [SerializeField] private float startScale = 0.45f;
        [SerializeField] private float endScale = 1.3f;

        private sealed class Puff
        {
            public SpriteRenderer Renderer;
            public Vector3 Origin;
            public float Age;
            public float Side;
        }

        private readonly List<Puff> _active = new List<Puff>();
        private readonly Stack<Puff> _pool = new Stack<Puff>();
        private readonly Dictionary<string, float> _nextPuff = new Dictionary<string, float>();
        private readonly List<string> _working = new List<string>();
        private float _nextScan;

        public int ActivePuffs => _active.Count;

        /// <summary>Factories that smoked at the last scan (tests).</summary>
        public IReadOnlyList<string> WorkingFactories => _working;

        private void Update()
        {
            if (Time.unscaledTime >= _nextScan) Scan();

            foreach (var id in _working)
            {
                if (!_nextPuff.TryGetValue(id, out var next)) next = 0f;
                if (Time.unscaledTime < next) continue;
                _nextPuff[id] = Time.unscaledTime + puffInterval * Random.Range(0.85f, 1.15f);
                if (buildings.TryGetView(id, out var view)) Emit(view, 0f);
            }

            float dt = Time.unscaledDeltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var p = _active[i];
                p.Age += dt;
                if (p.Age >= lifetime) { Release(i); continue; }
                Layout(p);
            }
        }

        /// <summary>Spawns a puff at the building's chimney, optionally already aged (previews show a full plume).</summary>
        public bool Emit(BuildingView view, float age)
        {
            if (puffSprite == null || view == null || view.Definition == null || !view.Definition.HasChimney) return false;
            var p = _pool.Count > 0 ? _pool.Pop() : CreatePuff();
            p.Origin = ChimneyWorld(view);
            p.Age = age;
            p.Side = Random.Range(-0.4f, 1f);
            p.Renderer.gameObject.SetActive(true);
            _active.Add(p);
            Layout(p);
            return true;
        }

        /// <summary>World position of a building's chimney top (sprite-relative point → world).</summary>
        public static Vector3 ChimneyWorld(BuildingView view)
        {
            if (view.SpriteRenderer.sprite == null) return view.transform.position;
            return view.transform.TransformPoint(view.PointLocal(view.Definition.ChimneyPoint));
        }

        private void Scan()
        {
            _nextScan = Time.unscaledTime + 0.5f;
            _working.Clear();
            if (production == null || production.Factories == null) return;
            long now = production.NowUnixMs;
            foreach (var pair in production.Factories.Queues)
                if (pair.Value.ActiveJob(now) != null) _working.Add(pair.Key);
        }

        private void Layout(Puff p)
        {
            float k = Mathf.Clamp01(p.Age / lifetime);
            float eased = 1f - (1f - k) * (1f - k);
            p.Renderer.transform.position = p.Origin + new Vector3(drift * p.Side * eased, rise * eased, 0f);
            p.Renderer.transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, eased);
            float alpha = k < 0.15f ? k / 0.15f : 1f - (k - 0.15f) / 0.85f;
            p.Renderer.color = new Color(color.r, color.g, color.b, color.a * alpha);
        }

        private Puff CreatePuff()
        {
            var go = new GameObject("SmokePuff");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = puffSprite;
            sr.sortingLayerName = SortingLayers.Effects;
            return new Puff { Renderer = sr };
        }

        private void Release(int index)
        {
            var p = _active[index];
            _active.RemoveAt(index);
            p.Renderer.gameObject.SetActive(false);
            _pool.Push(p);
        }
    }
}
