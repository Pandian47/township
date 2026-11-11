using System;
using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TownBuilder
{
    /// <summary>
    /// Seasons in the Town scene: the season follows the phone's local month (<see cref="SeasonCalendar"/>). Grass tiles
    /// are swapped for the season's grass (water, sand and roads stay), trees are tinted (golden in autumn, frosty in
    /// winter), and petals / leaves / snowflakes drift down over the view. Seasonal decorations are only sold in their
    /// season (<see cref="IsListedInShop"/>); ones already built stay.
    /// </summary>
    public sealed class SeasonController : MonoBehaviour
    {
        [SerializeField] private Tilemap groundTilemap;
        [Tooltip("Grass tiles per season, in Season order (Spring = the tile the map is painted with).")]
        [SerializeField] private TileBase[] grassTiles = new TileBase[4];
        [Tooltip("Falling sprites per season, in Season order (empty = none).")]
        [SerializeField] private Sprite[] fallingSprites = new Sprite[4];
        [SerializeField] private TownBuildingController buildings;
        [SerializeField] private PlacementSystem placement;
        [SerializeField] private int fallingCount = 32;
        [SerializeField] private float fallSpeed = 0.8f;
        [Tooltip("Building whose picture changes with the season.")]
        [SerializeField] private string seasonalTreeId = "tree";
        [Tooltip("That building's picture per season, in Season order (Spring = its normal sprite).")]
        [SerializeField] private Sprite[] treeSprites = new Sprite[4];

        private readonly List<(Transform t, float speed, float sway, float phase)> _falling = new List<(Transform, float, float, float)>();
        private Town _town;
        private IClock _clock;
        private Func<long> _utcOffsetMs;
        private Camera _camera;
        private Season? _forced;
        private bool _hasShown;
        private TileBase _shownGrass;
        private float _nextCheck;

        public event Action<string> Notification;
        public event Action Changed;

        public Season Current { get; private set; }
        public int FallingVisible => _falling.Count;
        public TileBase GrassFor(Season season) => grassTiles != null && (int)season < grassTiles.Length ? grassTiles[(int)season] : null;

        /// <param name="lastAnnounced">The season the player was last told about (-1 = never): a new season gets a toast.</param>
        public void Initialize(Town town, IClock clock, Func<long> utcOffsetMs, int lastAnnounced, GameContent content)
        {
            _town = town;
            _clock = clock;
            _utcOffsetMs = utcOffsetMs ?? (() => 0L);
            _camera = Camera.main;
            _shownGrass = GrassFor(Season.Spring);
            Refresh(force: true);
            if ((int)Current != lastAnnounced) Announce(content);
        }

        /// <summary>Seasonal decorations are sold only in their season; everything else always.</summary>
        public bool IsListedInShop(BuildingDefinition def) => def == null || !def.IsSeasonal || def.Season == Current;

        /// <summary>Developer mode: jump to the next season (until the app restarts).</summary>
        public void DevNextSeason(GameContent content)
        {
            _forced = SeasonCalendar.Next(Current);
            Refresh(force: true);
            Announce(content);
        }

        private void Announce(GameContent content)
        {
            string deco = null;
            if (content != null)
                foreach (var b in content.Buildings)
                    if (b != null && b.IsSeasonal && b.Season == Current) deco = b.DisplayName;
            Notification?.Invoke(deco != null
                ? $"{SeasonCalendar.Name(Current)} has come to town! {deco} is in Build → Decorations this season."
                : $"{SeasonCalendar.Name(Current)} has come to town!");
        }

        private void Update()
        {
            if (_clock == null) return;
            if (Time.unscaledTime >= _nextCheck)
            {
                _nextCheck = Time.unscaledTime + 2f;
                Refresh(force: false);
                TintTrees();
            }

            UpdateFalling(Time.deltaTime);
        }

        private void Refresh(bool force)
        {
            var season = _forced ?? SeasonCalendar.At(_clock.UtcNowUnixMs, _utcOffsetMs());
            if (!force && _hasShown && season == Current) return;
            bool changed = _hasShown && season != Current;
            Current = season;
            _hasShown = true;
            Apply(season);
            if (changed) Changed?.Invoke();
        }

        /// <summary>Shows a season right away (also used by the editor preview, with <paramref name="cam"/>).</summary>
        public void Apply(Season season, Camera cam = null)
        {
            Current = season;
            if (cam != null) _camera = cam;
            var target = GrassFor(season);
            if (_shownGrass == null) _shownGrass = GrassFor(Season.Spring);
            if (groundTilemap != null && target != null && _shownGrass != null && target != _shownGrass)
            {
                groundTilemap.SwapTile(_shownGrass, target);
                _shownGrass = target;
            }

            TintTrees();
            SpawnFalling(season);
        }

        /// <summary>Trees wear the season: golden leaves in autumn, snow in winter.</summary>
        private void TintTrees()
        {
            var sprite = TreeSpriteFor(Current);
            foreach (var view in FindObjectsByType<BuildingView>(FindObjectsInactive.Exclude))
            {
                if (view.Definition == null || view.Definition.Id != seasonalTreeId || view.SpriteRenderer == null) continue;
                var target = sprite != null ? sprite : view.Definition.Sprite;
                if (view.SpriteRenderer.sprite != target) view.SpriteRenderer.sprite = target;
            }
        }

        public Sprite TreeSpriteFor(Season season) => treeSprites != null && (int)season < treeSprites.Length ? treeSprites[(int)season] : null;

        private void SpawnFalling(Season season)
        {
            foreach (var f in _falling)
                if (f.t != null) Destroy(f.t.gameObject);
            _falling.Clear();
            var sprite = fallingSprites != null && (int)season < fallingSprites.Length ? fallingSprites[(int)season] : null;
            if (sprite == null || _camera == null) return;

            var random = new System.Random(17);
            for (int i = 0; i < fallingCount; i++)
            {
                var go = new GameObject("Falling");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingLayerName = SortingLayers.WorldUI;
                sr.sortingOrder = -50;
                sr.color = new Color(1f, 1f, 1f, 0.85f);
                var view = ViewRect();
                go.transform.position = new Vector3(Mathf.Lerp(view.xMin, view.xMax, (float)random.NextDouble()),
                    Mathf.Lerp(view.yMin, view.yMax, (float)random.NextDouble()), 0f);
                go.transform.rotation = Quaternion.Euler(0, 0, (float)random.NextDouble() * 360f);
                _falling.Add((go.transform, 0.6f + (float)random.NextDouble() * 0.8f, 0.2f + (float)random.NextDouble() * 0.5f,
                    (float)random.NextDouble() * 6.28f));
            }

            UpdateFalling(0f);
        }

        private Rect ViewRect()
        {
            float h = _camera.orthographicSize, w = h * _camera.aspect;
            var c = _camera.transform.position;
            return new Rect(c.x - w, c.y - h, 2 * w, 2 * h);
        }

        private void UpdateFalling(float dt)
        {
            if (_falling.Count == 0 || _camera == null) return;
            var view = ViewRect();
            float scale = _camera.orthographicSize / 5f;
            float time = Time.time;
            for (int i = 0; i < _falling.Count; i++)
            {
                var (t, speed, sway, phase) = _falling[i];
                if (t == null) continue;
                var p = t.position;
                p.y -= speed * fallSpeed * scale * dt;
                p.x += Mathf.Sin(time * 1.3f + phase) * sway * scale * dt;
                // Wrap around the visible area so the effect follows the camera.
                if (p.y < view.yMin) p.y += view.height;
                if (p.y > view.yMax) p.y -= view.height;
                if (p.x < view.xMin) p.x += view.width;
                if (p.x > view.xMax) p.x -= view.width;
                t.position = p;
                t.localScale = Vector3.one * scale;
                t.Rotate(0f, 0f, sway * 40f * dt);
            }
        }
    }
}
