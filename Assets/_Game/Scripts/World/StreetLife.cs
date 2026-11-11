using System.Collections.Generic;
using TownBuilder.Domain;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TownBuilder
{
    /// <summary>
    /// Townspeople and cars moving along the roads. Purely cosmetic: they walk a graph built from the road tilemap
    /// (cell to neighbouring road cell, avoiding U-turns unless at a dead end), people on the side of the road and cars
    /// in the right-hand lane. More cottages = more people (up to a limit).
    /// </summary>
    public sealed class StreetLife : MonoBehaviour
    {
        [SerializeField] private IsoGrid grid;
        [SerializeField] private Tilemap roadTilemap;
        [Tooltip("Townsfolk sprites (front view, pivot at the feet). One is picked per person; flipped for direction.")]
        [SerializeField] private Sprite[] peopleSprites = new Sprite[0];
        [Tooltip("True when the townsfolk art faces/walks to the left (it's mirrored when they walk right).")]
        [SerializeField] private bool peopleFaceLeft = true;
        [Tooltip("Car seen from the front / back, along the grid X axis (pivot at the bottom corner).")]
        [SerializeField] private Sprite carFront;
        [SerializeField] private Sprite carBack;
        [SerializeField] private int basePeople = 3;
        [SerializeField] private int maxPeople = 10;
        [SerializeField] private int cars = 2;
        [SerializeField] private float walkSpeed = 0.55f;  // cells per second
        [SerializeField] private float driveSpeed = 1.9f;
        [Tooltip("Sideways offset from the road centre, in cells: people on the edge, cars in the right-hand lane.")]
        [SerializeField] private float sidewalkOffset = 0.36f;
        [SerializeField] private float laneOffset = 0.17f;

        // Car footprint centre relative to its pivot (bottom corner), in cells: 1.3 long (X) × 0.7 wide (Y).
        private const float CarCenterAlong = 0.65f, CarCenterAcross = 0.35f;

        private sealed class Agent
        {
            public bool IsCar;
            public SpriteRenderer Renderer;
            public GridCoord From, To;
            public float T;
            public float Pause;
            public float Side; // +1 / -1: which side of the road a person walks on
        }

        private readonly List<GridCoord> _roadCells = new List<GridCoord>();
        private readonly HashSet<GridCoord> _road = new HashSet<GridCoord>();
        private readonly List<Agent> _agents = new List<Agent>();
        private Town _town;
        private GameContent _content;
        private System.Random _random = new System.Random();
        private float _nextCensus;

        public int People => _agents.FindAll(a => !a.IsCar).Count;
        public int Cars => _agents.FindAll(a => a.IsCar).Count;
        public int RoadCellCount => _roadCells.Count;

        /// <summary>World positions of all agents (tests).</summary>
        public IEnumerable<Vector3> Positions()
        {
            foreach (var a in _agents) yield return a.Renderer.transform.parent.position;
        }

        public void Initialize(Town town, GameContent content, int? seed = null)
        {
            _town = town;
            _content = content;
            if (seed.HasValue) _random = new System.Random(seed.Value);
            BuildGraph();
            for (int i = 0; i < cars; i++) Spawn(true);
            Census();
        }

        /// <summary>Road cells around <paramref name="cell"/> that a walker can step to.</summary>
        public List<GridCoord> Neighbours(GridCoord cell)
        {
            var list = new List<GridCoord>(4);
            foreach (var n in new[] { new GridCoord(cell.X + 1, cell.Y), new GridCoord(cell.X - 1, cell.Y), new GridCoord(cell.X, cell.Y + 1), new GridCoord(cell.X, cell.Y - 1) })
                if (_road.Contains(n)) list.Add(n);
            return list;
        }

        public bool IsRoad(GridCoord cell) => _road.Contains(cell);

        /// <summary>After landscape painting: re-reads the roads and moves anyone left off-road onto a road.</summary>
        public void RefreshRoads()
        {
            BuildGraph();
            foreach (var a in _agents)
            {
                bool lost = !_road.Contains(a.From) || !_road.Contains(a.To);
                a.Renderer.enabled = _roadCells.Count > 0;
                if (!lost || _roadCells.Count == 0) continue;
                var c = _roadCells[_random.Next(_roadCells.Count)];
                a.From = a.To = c;
                a.T = 0f;
            }
        }

        private void BuildGraph()
        {
            _road.Clear();
            _roadCells.Clear();
            if (roadTilemap == null) return;
            var b = roadTilemap.cellBounds;
            for (int x = b.xMin; x < b.xMax; x++)
            for (int y = b.yMin; y < b.yMax; y++)
            {
                if (!roadTilemap.HasTile(new Vector3Int(x, y, 0))) continue;
                var c = new GridCoord(x, y);
                _road.Add(c);
                _roadCells.Add(c);
            }
        }

        private void Census()
        {
            _nextCensus = Time.unscaledTime + 2f;
            int homes = 0;
            if (_town != null && _content != null)
                foreach (var b in _town.Buildings)
                    if (_content.TryGetBuilding(b.DefinitionId, out var def) && def.Category == BuildingCategory.Residential) homes++;
            int wanted = Mathf.Min(maxPeople, basePeople + homes);
            while (People < wanted && Spawn(false)) { }
        }

        private bool Spawn(bool car)
        {
            if (_roadCells.Count == 0) return false;
            var sprite = car ? carFront : peopleSprites.Length > 0 ? peopleSprites[_random.Next(peopleSprites.Length)] : null;
            if (sprite == null) return false;

            var start = _roadCells[_random.Next(_roadCells.Count)];
            var next = Neighbours(start);
            if (next.Count == 0) return false;

            var holder = new GameObject(car ? "Car" : "Townsperson");
            holder.transform.SetParent(transform, false);
            var art = new GameObject("Sprite");
            art.transform.SetParent(holder.transform, false);
            var sr = art.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = SortingLayers.Objects;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;

            var agent = new Agent
            {
                IsCar = car, Renderer = sr, From = start, To = next[_random.Next(next.Count)],
                T = (float)_random.NextDouble(), Side = _random.Next(2) == 0 ? 1f : -1f,
            };
            _agents.Add(agent);
            Place(agent);
            return true;
        }

        private void Update()
        {
            if (_agents.Count == 0 && _roadCells.Count == 0) return;
            if (Time.unscaledTime >= _nextCensus) Census();

            float dt = Time.unscaledDeltaTime;
            foreach (var a in _agents)
            {
                if (a.Pause > 0f) { a.Pause -= dt; continue; }
                a.T += dt * (a.IsCar ? driveSpeed : walkSpeed);
                while (a.T >= 1f)
                {
                    a.T -= 1f;
                    var options = Neighbours(a.To);
                    if (options.Count > 1) options.Remove(a.From); // no U-turn unless it's a dead end
                    var next = options.Count > 0 ? options[_random.Next(options.Count)] : a.From;
                    a.From = a.To;
                    a.To = next;
                    if (!a.IsCar && _random.NextDouble() < 0.08) a.Pause = 1f + (float)_random.NextDouble() * 2f; // stop for a chat
                }

                Place(a);
            }
        }

        private void Place(Agent a)
        {
            Vector2 from = grid.CellCenterWorld(a.From), to = grid.CellCenterWorld(a.To);
            int dx = a.To.X - a.From.X, dy = a.To.Y - a.From.Y;
            // Right-hand side of the direction of travel (in grid space), turned into a world offset.
            Vector2 right = (dx != 0 ? grid.AxisY * -dx : grid.AxisX * dy);
            float offset = a.IsCar ? laneOffset : sidewalkOffset * a.Side;
            Vector2 p = Vector2.Lerp(from, to, a.T) + right * offset;
            a.Renderer.transform.parent.position = new Vector3(p.x, p.y, 0f);

            if (a.IsCar)
            {
                // Along X: as drawn; along Y: mirrored. Towards the viewer (−X / −Y): front; away: back.
                bool alongY = dx == 0;
                bool towardViewer = dx < 0 || dy < 0;
                a.Renderer.sprite = towardViewer || carBack == null ? carFront : carBack;
                a.Renderer.flipX = alongY;
                // Centre the car's footprint on the lane point (the sprite's pivot is its bottom corner).
                Vector2 centre = grid.AxisX * CarCenterAlong + grid.AxisY * CarCenterAcross;
                if (alongY) centre = new Vector2(-centre.x, centre.y);
                a.Renderer.transform.localPosition = new Vector3(-centre.x, -centre.y, 0f);
            }
            else
            {
                // Face the direction of travel: screen-left (−X or +Y moves left on screen) flips the sprite.
                float screenDx = (to - from).x;
                if (Mathf.Abs(screenDx) > 0.001f) a.Renderer.flipX = peopleFaceLeft ? screenDx > 0f : screenDx < 0f;
            }
        }

        /// <summary>Editor previews: a few people and a car spread along the roads.</summary>
        public void PreviewPopulate(int people, int previewCars, int seed = 3)
        {
            _random = new System.Random(seed);
            BuildGraph();
            for (int i = 0; i < previewCars; i++) Spawn(true);
            for (int i = 0; i < people; i++) Spawn(false);
        }
    }
}
