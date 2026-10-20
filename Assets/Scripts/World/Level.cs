using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Doom
{
    public enum CellKind : byte { Void, Wall, Floor, Door, SecretDoor, Exit, Lift }

    public struct Cell
    {
        public CellKind Kind;
        public char Char;
        public float Floor;
        public float Light;
        public bool Outdoor, Nukage, Secret;
        public char Pad;   // teleporter pad channel ('Q', 'W', 'E'), or 0
        public bool IsSolid => Kind == CellKind.Void || Kind == CellKind.Wall || Kind == CellKind.Exit;
    }

    /// <summary>
    /// Runtime level: owns the cell grid, builds geometry from a LevelDef, spawns things, and
    /// provides grid queries (flow field pathing for monsters, automap visibility, secrets).
    /// </summary>
    public class Level : MonoBehaviour
    {
        public const float CellSize = 2f;

        public LevelDef Def { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public Cell[,] Cells { get; private set; }
        public bool[,] Seen { get; private set; }
        public Vector3 PlayerStart { get; private set; }
        public int TotalKills, TotalItems, TotalSecrets;
        public readonly List<Enemy> Enemies = new();

        readonly Dictionary<Vector2Int, Door> doors = new();
        readonly HashSet<Vector2Int> foundSecrets = new();
        readonly List<Lift> lifts = new();
        readonly Dictionary<Vector2Int, Lift> liftCells = new();
        readonly Dictionary<Vector2Int, float> liftLow = new();
        readonly Dictionary<char, Vector2Int> teleportDestinations = new();
        readonly Dictionary<char, float> teleportYaw = new();
        readonly Dictionary<char, List<Vector2Int>> teleportPads = new();
        int[,] flow;
        Vector2Int flowOrigin = new(-1, -1);
        float flowTimer, seenTimer;

        static readonly Vector2Int[] Dirs4 = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        static readonly Vector2Int[] Dirs8 =
            { new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(1, -1), new(-1, 1), new(-1, -1) };

        // ------------------------------------------------------------------ coordinates

        public Vector3 CellCenter(int x, int y) =>
            new(x * CellSize + CellSize * 0.5f, Cells[x, y].Floor, (Height - 1 - y) * CellSize + CellSize * 0.5f);

        public Vector2Int WorldToCell(Vector3 p) =>
            new(Mathf.FloorToInt(p.x / CellSize), Height - 1 - Mathf.FloorToInt(p.z / CellSize));

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

        public Cell CellAt(Vector3 p)
        {
            var c = WorldToCell(p);
            return InBounds(c) ? Cells[c.x, c.y] : default;
        }

        public float LightAt(Vector3 p)
        {
            var c = WorldToCell(p);
            return InBounds(c) ? Cells[c.x, c.y].Light : Def.Light;
        }

        // ------------------------------------------------------------------ building

        public static Level Build(LevelDef def, Transform parent)
        {
            var go = new GameObject("Level " + def.Id);
            go.transform.SetParent(parent, false);
            var level = go.AddComponent<Level>();
            level.Construct(def);
            return level;
        }

        void Construct(LevelDef def)
        {
            Def = def;
            Height = def.Map.Length;
            Width = 0;
            foreach (var row in def.Map) Width = Mathf.Max(Width, row.Length);
            Cells = new Cell[Width, Height];
            Seen = new bool[Width, Height];

            var things = new List<(char ch, int x, int y)>();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    char ch = CharAt(def.Map, x, y, ' ');
                    var cell = new Cell { Char = ch };
                    switch (ch)
                    {
                        case ' ': cell.Kind = CellKind.Void; break;
                        case '#': case 'B': case 'T': case 'M': case 'K': cell.Kind = CellKind.Wall; break;
                        case 'X': cell.Kind = CellKind.Exit; break;
                        case 'H': cell.Kind = CellKind.SecretDoor; break;
                        case 'D': case 'R': case 'U': case 'Y': cell.Kind = CellKind.Door; break;
                        case '.': cell.Kind = CellKind.Floor; break;
                        case ',': cell.Kind = CellKind.Floor; cell.Outdoor = true; break;
                        case '~': cell.Kind = CellKind.Floor; cell.Nukage = true; break;
                        case 'S': cell.Kind = CellKind.Floor; cell.Secret = true; break;
                        case 'L': cell.Kind = CellKind.Lift; break;
                        case 'Q': case 'W': case 'E':
                            cell.Kind = CellKind.Floor;
                            cell.Pad = ch;
                            if (!teleportPads.TryGetValue(ch, out var pads)) teleportPads[ch] = pads = new List<Vector2Int>();
                            pads.Add(new Vector2Int(x, y));
                            break;
                        default: cell.Kind = CellKind.Floor; things.Add((ch, x, y)); break;
                    }
                    char hc = def.Heights != null ? CharAt(def.Heights, x, y, '0') : '0';
                    cell.Floor = char.IsDigit(hc) ? (hc - '0') * 0.25f : 0f;
                    if (cell.Nukage) cell.Floor -= 0.25f;
                    Cells[x, y] = cell;
                }

            // Things sitting in outdoor areas inherit the sky.
            foreach (var (_, x, y) in things)
            {
                int outdoor = 0;
                foreach (var d in Dirs4)
                {
                    var n = new Vector2Int(x + d.x, y + d.y);
                    if (InBounds(n) && Cells[n.x, n.y].Outdoor) outdoor++;
                }
                if (outdoor >= 2) Cells[x, y].Outdoor = true;
            }

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    ref var c = ref Cells[x, y];
                    c.Light = c.Nukage || c.Pad != default ? 1f : c.Outdoor ? Mathf.Min(1f, def.Light + 0.15f) : def.Light;
                    if (c.Secret) TotalSecrets++;
                }

            FindLiftGroups(out var liftGroups);
            BuildGeometry();
            BuildDoorsAndSwitches();
            BuildLifts(liftGroups);
            if (HasOutdoor()) Sky.Create(transform);

            foreach (var (ch, x, y) in things) SpawnThing(ch, x, y);
            foreach (var pad in teleportPads.Keys)
                if (!teleportDestinations.ContainsKey(char.ToLowerInvariant(pad)))
                    Debug.LogWarning($"[Doom] {def.Id}: teleporter '{pad}' has no destination '{char.ToLowerInvariant(pad)}'");
        }

        // ------------------------------------------------------------------ lifts

        /// <summary>Groups connected lift cells; each group's low position is the lowest adjacent floor.</summary>
        void FindLiftGroups(out List<List<Vector2Int>> groups)
        {
            groups = new List<List<Vector2Int>>();
            var visited = new HashSet<Vector2Int>();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var start = new Vector2Int(x, y);
                    if (Cells[x, y].Kind != CellKind.Lift || !visited.Add(start)) continue;
                    var group = new List<Vector2Int>();
                    var q = new Queue<Vector2Int>();
                    q.Enqueue(start);
                    float low = float.MaxValue;
                    while (q.Count > 0)
                    {
                        var c = q.Dequeue();
                        group.Add(c);
                        foreach (var d in Dirs4)
                        {
                            var n = c + d;
                            if (!InBounds(n)) continue;
                            var nc = Cells[n.x, n.y];
                            if (nc.Kind == CellKind.Lift) { if (visited.Add(n)) q.Enqueue(n); }
                            else if (nc.Kind == CellKind.Floor || nc.Kind == CellKind.Door) low = Mathf.Min(low, nc.Floor);
                        }
                    }
                    float high = Cells[x, y].Floor;
                    if (low == float.MaxValue || low >= high)
                    {
                        Debug.LogWarning($"[Doom] {Def.Id}: lift at {x},{y} needs a Heights value above its neighbouring floor");
                        low = high - 1f;
                    }
                    foreach (var c in group) liftLow[c] = low;
                    groups.Add(group);
                }
        }

        void BuildLifts(List<List<Vector2Int>> groups)
        {
            foreach (var group in groups)
            {
                var centers = new List<Vector3>();
                foreach (var c in group) centers.Add(CellCenter(c.x, c.y));
                var first = group[0];
                var lift = Lift.Create(transform, centers, CellSize, liftLow[first], Cells[first.x, first.y].Floor, Cells[first.x, first.y].Light);
                lifts.Add(lift);
                foreach (var c in group) liftCells[c] = lift;
            }
        }

        public Lift LiftAt(int x, int y) => liftCells.TryGetValue(new Vector2Int(x, y), out var l) ? l : null;

        // ------------------------------------------------------------------ teleporters

        /// <summary>
        /// Call after an actor moves. Teleports it when it has just entered a pad cell. The player
        /// telefrags anything at the destination; monsters only teleport when it is clear.
        /// </summary>
        public bool TryTeleport(GameObject actor, Vector3 feet, ref Vector2Int lastCell)
        {
            var c = WorldToCell(feet);
            if (c == lastCell) return false;
            lastCell = c;
            if (!InBounds(c)) return false;
            char pad = Cells[c.x, c.y].Pad;
            if (pad == default) return false;
            char channel = char.ToLowerInvariant(pad);
            if (!teleportDestinations.TryGetValue(channel, out var dest)) return false;

            var to = CellCenter(dest.x, dest.y);
            bool isPlayer = actor.TryGetComponent<PlayerController>(out var pc);
            var hits = Physics.OverlapCapsule(to + Vector3.up * 0.6f, to + Vector3.up * 1.3f, 0.6f, Layers.ActorMask, QueryTriggerInteraction.Ignore);
            var victims = new HashSet<IDamageable>();
            foreach (var h in hits)
            {
                if (Combat.IsPartOf(h, actor)) continue;
                var d = h.GetComponentInParent<IDamageable>();
                if (d != null && !d.IsDead) victims.Add(d);
            }
            if (!isPlayer && victims.Count > 0) return false;
            foreach (var v in victims) v.Damage(10000, Vector3.up, actor); // telefrag

            Effect.Spawn("fx_tfog_", 3, 0.12f, feet + Vector3.up * 0.9f);
            Effect.Spawn("fx_tfog_", 3, 0.12f, to + Vector3.up * 0.9f);
            AudioManager.I.PlayAt("teleport", feet);
            float yaw = teleportYaw[channel];
            if (isPlayer)
            {
                AudioManager.I.Play2D("teleport");
                pc.Teleport(to, yaw);
            }
            else if (actor.TryGetComponent<Enemy>(out var e)) e.Warp(to, yaw);
            lastCell = dest;
            return true;
        }

        /// <summary>Destinations face the direction with the longest open run of cells.</summary>
        float OpenFacing(int x, int y)
        {
            float bestYaw = 0f;
            int best = -1;
            foreach (var d in Dirs4)
            {
                int run = 0;
                while (IsOpen(x + d.x * (run + 1), y + d.y * (run + 1)) && run < 32) run++;
                if (run <= best) continue;
                best = run;
                bestYaw = d.x == 1 ? 90f : d.x == -1 ? 270f : d.y == 1 ? 180f : 0f;
            }
            return bestYaw;
        }

        static char CharAt(string[] rows, int x, int y, char fallback)
        {
            if (y < 0 || y >= rows.Length) return fallback;
            var row = rows[y];
            return x < row.Length ? row[x] : fallback;
        }

        bool HasOutdoor()
        {
            foreach (var c in Cells) if (c.Outdoor) return true;
            return false;
        }

        string WallTextureFor(char ch) => ch switch
        {
            'B' => "wall_brick",
            'T' => "wall_tech",
            'M' => "wall_metal",
            'K' => "wall_stone",
            _ => Def.WallTexture
        };

        bool IsOpen(int x, int y) =>
            x >= 0 && y >= 0 && x < Width && y < Height &&
            (Cells[x, y].Kind == CellKind.Floor || Cells[x, y].Kind == CellKind.Door ||
             Cells[x, y].Kind == CellKind.SecretDoor || Cells[x, y].Kind == CellKind.Lift);

        void BuildGeometry()
        {
            var mb = new LevelMeshBuilder();
            float ceil = Def.CeilingHeight;
            const float bottom = -1f;
            const float skyWall = 3f;

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var cell = Cells[x, y];
                    if (!IsOpen(x, y)) continue;
                    float x0 = x * CellSize, x1 = x0 + CellSize;
                    float z1 = (Height - y) * CellSize, z0 = z1 - CellSize;
                    var light = cell.Light;

                    bool isLift = cell.Kind == CellKind.Lift;
                    string floorTex = cell.Pad != default ? "teleport" : cell.Nukage ? "nukage" : cell.Outdoor ? Def.OutdoorFloorTexture : Def.FloorTexture;
                    if (!isLift) mb.Floor(floorTex, x0, z0, x1, z1, cell.Floor, light, true);
                    if (!cell.Outdoor)
                    {
                        bool lamp = x % 4 == 1 && y % 4 == 1 && cell.Kind == CellKind.Floor;
                        mb.Floor(lamp ? "ceil_light" : Def.CeilingTexture, x0, z0, x1, z1, ceil, light, false);
                    }

                    foreach (var d in Dirs4)
                    {
                        int nx = x + d.x, ny = y + d.y;
                        bool inside = nx >= 0 && ny >= 0 && nx < Width && ny < Height;
                        var n = inside ? Cells[nx, ny] : new Cell { Kind = CellKind.Void };

                        // Edge between this cell and the neighbour, wall facing back into this cell.
                        Vector3 a, b;
                        EdgeToward(x0, z0, x1, z1, d, out a, out b);

                        if (n.Kind == CellKind.Void || n.Kind == CellKind.Wall)
                        {
                            string tex = cell.Kind == CellKind.Door ? "door_track"
                                : WallTextureFor(n.Kind == CellKind.Void ? '#' : n.Char);
                            float top = cell.Outdoor ? ceil + skyWall : ceil;
                            mb.Wall(tex, a, b, bottom, top, light);
                        }
                        else if (n.Kind == CellKind.Floor || n.Kind == CellKind.Door || n.Kind == CellKind.Lift)
                        {
                            // Lift sides are drawn by the lift itself; seen from a lowered lift, higher
                            // neighbours need a riser down to the lift's low position.
                            float from = isLift ? liftLow[new Vector2Int(x, y)] : cell.Floor;
                            if (n.Kind != CellKind.Lift && n.Floor > from + 0.001f)
                                mb.Wall("step", a, b, from, n.Floor, light);
                            if (cell.Outdoor && !n.Outdoor)
                                mb.Wall(Def.WallTexture, a, b, ceil, ceil + skyWall, light);
                        }
                    }
                }

            var mesh = mb.Build("LevelMesh", out var materials);
            var geo = new GameObject("Geometry");
            geo.layer = Layers.World;
            geo.transform.SetParent(transform, false);
            geo.AddComponent<MeshFilter>().sharedMesh = mesh;
            geo.AddComponent<MeshRenderer>().sharedMaterials = materials;
            geo.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>Returns the shared edge between a cell and its neighbour in direction d (grid y grows southwards).</summary>
        static void EdgeToward(float x0, float z0, float x1, float z1, Vector2Int d, out Vector3 a, out Vector3 b)
        {
            // a->b is ordered so that the wall's front face points back into the cell
            // (LevelMeshBuilder.Wall's normal is (e.z, 0, -e.x) for edge e = b - a).
            if (d.x == 1) { a = new Vector3(x1, 0, z1); b = new Vector3(x1, 0, z0); }
            else if (d.x == -1) { a = new Vector3(x0, 0, z0); b = new Vector3(x0, 0, z1); }
            else if (d.y == 1) { a = new Vector3(x1, 0, z0); b = new Vector3(x0, 0, z0); }
            else { a = new Vector3(x0, 0, z1); b = new Vector3(x1, 0, z1); }
        }

        void BuildDoorsAndSwitches()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var cell = Cells[x, y];
                    var center = CellCenter(x, y);
                    if (cell.Kind == CellKind.Door || cell.Kind == CellKind.SecretDoor)
                    {
                        KeyColor key = cell.Char switch
                        {
                            'R' => KeyColor.Red,
                            'U' => KeyColor.Blue,
                            'Y' => KeyColor.Yellow,
                            _ => KeyColor.None
                        };
                        bool secret = cell.Kind == CellKind.SecretDoor;
                        bool passageNS = !IsOpen(x - 1, y) && !IsOpen(x + 1, y);
                        string face = secret ? WallTextureFor(NeighbourWallChar(x, y)) : key switch
                        {
                            KeyColor.Red => "door_red",
                            KeyColor.Blue => "door_blue",
                            KeyColor.Yellow => "door_yellow",
                            _ => "door"
                        };
                        string side = secret ? face : "door_track";
                        var door = Door.Create(transform, center, CellSize, Def.CeilingHeight - cell.Floor, face, side,
                            passageNS, key, secret, cell.Light);
                        doors[new Vector2Int(x, y)] = door;
                    }
                    else if (cell.Kind == CellKind.Exit)
                    {
                        var faces = new List<Vector2Int>();
                        foreach (var d in Dirs4)
                            if (IsOpen(x + d.x, y + d.y)) faces.Add(d);
                        ExitSwitch.Create(transform, new Vector3(center.x, 0, center.z), CellSize, Def.CeilingHeight, faces, Def.Light);
                    }
                }
        }

        char NeighbourWallChar(int x, int y)
        {
            foreach (var d in Dirs4)
            {
                int nx = x + d.x, ny = y + d.y;
                if (nx >= 0 && ny >= 0 && nx < Width && ny < Height && Cells[nx, ny].Kind == CellKind.Wall)
                    return Cells[nx, ny].Char;
            }
            return '#';
        }

        void SpawnThing(char ch, int x, int y)
        {
            var pos = CellCenter(x, y);
            switch (ch)
            {
                case 'P': PlayerStart = pos; return;
                case 'q': case 'w': case 'e':
                    teleportDestinations[ch] = new Vector2Int(x, y);
                    teleportYaw[ch] = OpenFacing(x, y);
                    return;
                case 'o': Barrel.Spawn(transform, pos); return;
            }

            var monster = ch switch
            {
                'z' => "zombieman",
                'g' => "shotgunguy",
                'i' => "imp",
                'd' => "demon",
                'c' => "cacodemon",
                'l' => "lostsoul",
                'b' => "baron",
                _ => null
            };
            if (monster != null)
            {
                var e = Enemy.Spawn(EnemyDefs.Get(monster), transform, pos, Random.Range(0, 4) * 90f);
                Enemies.Add(e);
                TotalKills++;
                return;
            }

            PickupType? item = ch switch
            {
                'h' => PickupType.HealthBonus,
                '+' => PickupType.Stimpack,
                'm' => PickupType.Medikit,
                '*' => PickupType.Soulsphere,
                'v' => PickupType.ArmorBonus,
                'a' => PickupType.GreenArmor,
                'A' => PickupType.BlueArmor,
                'p' => PickupType.Backpack,
                '1' => PickupType.Clip,
                '2' => PickupType.AmmoBox,
                '3' => PickupType.Shells,
                '4' => PickupType.ShellBox,
                '5' => PickupType.Rocket,
                '6' => PickupType.RocketBox,
                '7' => PickupType.Cell,
                '8' => PickupType.CellPack,
                '^' => PickupType.Chainsaw,
                '$' => PickupType.Shotgun,
                '%' => PickupType.Chaingun,
                '&' => PickupType.RocketLauncher,
                '=' => PickupType.PlasmaGun,
                '@' => PickupType.BFG,
                'r' => PickupType.RedKey,
                'u' => PickupType.BlueKey,
                'y' => PickupType.YellowKey,
                _ => null
            };
            if (item.HasValue)
            {
                var p = Pickup.Spawn(item.Value, transform, pos, false);
                if (p.CountsAsItem) TotalItems++;
                return;
            }

            Debug.LogWarning($"[Doom] Unknown map character '{ch}' at {x},{y} in {Def.Id}");
        }

        // ------------------------------------------------------------------ runtime

        void Update()
        {
            var player = GameManager.I != null ? GameManager.I.Player : null;
            if (player == null) return;
            var pc = WorldToCell(player.transform.position);

            flowTimer -= Time.deltaTime;
            if (flowTimer <= 0f || pc != flowOrigin)
            {
                flowTimer = 0.3f;
                if (InBounds(pc)) ComputeFlow(pc);
            }

            seenTimer -= Time.deltaTime;
            if (seenTimer <= 0f)
            {
                seenTimer = 0.2f;
                MarkSeen(pc, 12);
            }

            if (InBounds(pc) && Cells[pc.x, pc.y].Secret && foundSecrets.Add(pc))
                GameManager.I.OnSecretFound();
        }

        public bool IsPassable(int x, int y, bool forMonster)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return false;
            var c = Cells[x, y];
            if (c.Kind == CellKind.Floor || c.Kind == CellKind.Lift) return true;
            if (c.Kind == CellKind.Door || c.Kind == CellKind.SecretDoor)
            {
                var door = doors[new Vector2Int(x, y)];
                return door.IsOpen || (forMonster && door.MonstersCanOpen);
            }
            return false;
        }

        void ComputeFlow(Vector2Int origin)
        {
            flowOrigin = origin;
            flow ??= new int[Width, Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    flow[x, y] = int.MaxValue;

            var q = new Queue<Vector2Int>();
            flow[origin.x, origin.y] = 0;
            q.Enqueue(origin);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                int dist = flow[c.x, c.y];
                foreach (var d in Dirs4)
                {
                    int nx = c.x + d.x, ny = c.y + d.y;
                    if (!IsPassable(nx, ny, true) || flow[nx, ny] != int.MaxValue) continue;
                    // Monsters walk from n to c, so they must be able to step up from n.
                    if (Cells[c.x, c.y].Floor - Cells[nx, ny].Floor > 0.6f) continue;
                    flow[nx, ny] = dist + 1;
                    q.Enqueue(new Vector2Int(nx, ny));
                }

                // A monster standing on a pad arrives at its destination, so pads lead here.
                foreach (var kv in teleportDestinations)
                {
                    if (kv.Value != c || !teleportPads.TryGetValue(char.ToUpperInvariant(kv.Key), out var pads)) continue;
                    foreach (var p in pads)
                    {
                        if (flow[p.x, p.y] != int.MaxValue) continue;
                        flow[p.x, p.y] = dist + 1;
                        q.Enqueue(p);
                    }
                }
            }
        }

        /// <summary>Number of grid steps from the player, or int.MaxValue if unreachable.</summary>
        public int FlowDistance(Vector3 p)
        {
            var c = WorldToCell(p);
            return flow != null && InBounds(c) ? flow[c.x, c.y] : int.MaxValue;
        }

        /// <summary>Direction toward the player following the flow field, or zero when there is no route.</summary>
        public Vector3 FlowDirection(Vector3 p)
        {
            var c = WorldToCell(p);
            if (flow == null || !InBounds(c)) return Vector3.zero;
            int best = flow[c.x, c.y];
            Vector2Int bestCell = c;
            foreach (var d in Dirs8)
            {
                int nx = c.x + d.x, ny = c.y + d.y;
                if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                // Diagonals only when both orthogonal neighbours are clear, to avoid corner snagging.
                if (d.x != 0 && d.y != 0 && (!IsPassable(c.x + d.x, c.y, true) || !IsPassable(c.x, c.y + d.y, true))) continue;
                int v = flow[nx, ny];
                if (v < best) { best = v; bestCell = new Vector2Int(nx, ny); }
            }
            if (bestCell == c) return Vector3.zero;
            var target = CellCenter(bestCell.x, bestCell.y);
            var dir = target - p;
            dir.y = 0;
            return dir.normalized;
        }

        void MarkSeen(Vector2Int origin, int radius)
        {
            if (!InBounds(origin)) return;
            for (int y = origin.y - radius; y <= origin.y + radius; y++)
                for (int x = origin.x - radius; x <= origin.x + radius; x++)
                {
                    if (x < 0 || y < 0 || x >= Width || y >= Height || Seen[x, y]) continue;
                    if (GridLineOfSight(origin, new Vector2Int(x, y))) Seen[x, y] = true;
                }
        }

        public void RevealAll()
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    Seen[x, y] = true;
        }

        bool GridLineOfSight(Vector2Int a, Vector2Int b)
        {
            int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                if (x0 == x1 && y0 == y1) return true;
                if (!(x0 == a.x && y0 == a.y))
                {
                    var c = Cells[x0, y0];
                    if (c.IsSolid) return false;
                    if ((c.Kind == CellKind.Door || c.Kind == CellKind.SecretDoor) && !doors[new Vector2Int(x0, y0)].IsOpen) return false;
                }
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public Door DoorAt(int x, int y) => doors.TryGetValue(new Vector2Int(x, y), out var d) ? d : null;

        // ------------------------------------------------------------------ save / load

        public void CaptureState(SaveData data)
        {
            foreach (var e in Enemies)
                if (e != null) data.enemies.Add(e.Capture());
            foreach (var p in GetComponentsInChildren<Pickup>())
                data.pickups.Add(new PickupSave { type = p.Type, position = p.transform.position, dropped = p.Dropped });
            foreach (var b in GetComponentsInChildren<Barrel>())
                if (!b.IsDead) data.barrels.Add(new BarrelSave { position = b.transform.position, health = b.Health });
            foreach (var kv in doors)
                data.doors.Add(kv.Value.Capture(kv.Key));
            for (int i = 0; i < lifts.Count; i++)
                data.lifts.Add(lifts[i].Capture(i));
            data.secretsFound.AddRange(foundSecrets);
            data.seen = new bool[Width * Height];
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    data.seen[y * Width + x] = Seen[x, y];
        }

        /// <summary>Replaces the freshly built level's things with the saved ones.</summary>
        public void ApplyState(SaveData data)
        {
            // Deactivate before destroying so nothing (e.g. pickups) runs another Update this frame.
            void Remove(Component c)
            {
                c.gameObject.SetActive(false);
                Destroy(c.gameObject);
            }
            foreach (var e in GetComponentsInChildren<Enemy>(true)) Remove(e);
            foreach (var p in GetComponentsInChildren<Pickup>(true)) Remove(p);
            foreach (var b in GetComponentsInChildren<Barrel>(true)) Remove(b);
            foreach (var p in GetComponentsInChildren<Projectile>(true)) Remove(p);
            Enemies.Clear();

            foreach (var es in data.enemies)
            {
                var e = Enemy.Restore(es, transform);
                if (e != null) Enemies.Add(e);
            }
            foreach (var ps in data.pickups) Pickup.Spawn(ps.type, transform, ps.position, ps.dropped);
            foreach (var bs in data.barrels) Barrel.Spawn(transform, bs.position).Health = bs.health;
            foreach (var ds in data.doors) DoorAt(ds.x, ds.y)?.Restore(ds);
            foreach (var ls in data.lifts)
                if (ls.index >= 0 && ls.index < lifts.Count) lifts[ls.index].Restore(ls);

            foundSecrets.Clear();
            foreach (var s in data.secretsFound) foundSecrets.Add(s);
            if (data.seen != null && data.seen.Length == Width * Height)
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                        Seen[x, y] = data.seen[y * Width + x];
            flowOrigin = new Vector2Int(-1, -1);
        }

        /// <summary>Wakes monsters that can "hear" a noise at the player's position (sound travels through open space).</summary>
        public void NoiseAlert(GameObject source)
        {
            foreach (var e in Enemies)
                if (e != null && !e.IsDead && FlowDistance(e.transform.position) <= 18)
                    e.Alert(source);
        }
    }

    /// <summary>Accumulates quads per texture into a single multi-material mesh.</summary>
    public class LevelMeshBuilder
    {
        class Part
        {
            public readonly List<Vector3> V = new();
            public readonly List<Vector2> UV = new();
            public readonly List<Color> Col = new();
            public readonly List<int> Tri = new();
        }

        readonly Dictionary<string, Part> parts = new();

        Part Get(string tex)
        {
            if (!parts.TryGetValue(tex, out var p)) parts[tex] = p = new Part();
            return p;
        }

        void Quad(string tex, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, float light)
        {
            // a b c d in clockwise order as seen from the visible side.
            var p = Get(tex);
            int i = p.V.Count;
            p.V.Add(a); p.V.Add(b); p.V.Add(c); p.V.Add(d);
            p.UV.Add(ua); p.UV.Add(ub); p.UV.Add(uc); p.UV.Add(ud);
            var col = new Color(light, light, light, 1f);
            p.Col.Add(col); p.Col.Add(col); p.Col.Add(col); p.Col.Add(col);
            p.Tri.Add(i); p.Tri.Add(i + 1); p.Tri.Add(i + 2);
            p.Tri.Add(i); p.Tri.Add(i + 2); p.Tri.Add(i + 3);
        }

        public void Floor(string tex, float x0, float z0, float x1, float z1, float y, float light, bool faceUp)
        {
            const float s = 0.5f;
            var a = new Vector3(x0, y, z0); var b = new Vector3(x0, y, z1);
            var c = new Vector3(x1, y, z1); var d = new Vector3(x1, y, z0);
            Vector2 ua = new(x0 * s, z0 * s), ub = new(x0 * s, z1 * s), uc = new(x1 * s, z1 * s), ud = new(x1 * s, z0 * s);
            if (faceUp) Quad(tex, a, b, c, d, ua, ub, uc, ud, light);
            else Quad(tex, a, d, c, b, ua, ud, uc, ub, light);
        }

        /// <summary>Vertical quad along edge a->b; its front face normal is (e.z, 0, -e.x) for e = b - a.</summary>
        public void Wall(string tex, Vector3 a, Vector3 b, float y0, float y1, float light)
        {
            const float s = 0.5f;
            float len = Vector3.Distance(a, b);
            float u0 = (a.x + a.z) * s;
            float u1 = u0 + len * s;
            var p0 = new Vector3(a.x, y0, a.z); var p1 = new Vector3(a.x, y1, a.z);
            var p2 = new Vector3(b.x, y1, b.z); var p3 = new Vector3(b.x, y0, b.z);
            Quad(tex, p0, p1, p2, p3, new Vector2(u0, y0 * s), new Vector2(u0, y1 * s), new Vector2(u1, y1 * s), new Vector2(u1, y0 * s), light);
        }

        public Mesh Build(string name, out Material[] materials)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var cols = new List<Color>();
            var mats = new List<Material>();
            var subTris = new List<int[]>();
            foreach (var kv in parts)
            {
                int offset = verts.Count;
                verts.AddRange(kv.Value.V);
                uvs.AddRange(kv.Value.UV);
                cols.AddRange(kv.Value.Col);
                var tris = kv.Value.Tri.ToArray();
                for (int i = 0; i < tris.Length; i++) tris[i] += offset;
                subTris.Add(tris);
                mats.Add(GameAssets.WorldMaterial(kv.Key));
            }
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(cols);
            mesh.subMeshCount = subTris.Count;
            for (int i = 0; i < subTris.Count; i++) mesh.SetTriangles(subTris[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            materials = mats.ToArray();
            return mesh;
        }
    }
}
