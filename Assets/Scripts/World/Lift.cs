using System.Collections.Generic;
using UnityEngine;

namespace Doom
{
    /// <summary>
    /// A platform made of one or more connected 'L' cells. It rests at its raised height, lowers to
    /// the surrounding floor when used (or when the player steps onto it from above), waits, then
    /// rises again carrying anyone standing on it.
    /// </summary>
    public class Lift : MonoBehaviour, IUsable
    {
        enum State { Up, Lowering, Down, Raising }

        const float Speed = 4.4f;   // 4 map units per tic
        const float WaitTime = 3f;

        public float Low { get; private set; }
        public float High { get; private set; }
        public float Height => height;
        public bool IsDown => state == State.Down;

        State state;
        float height, waitTimer, half;
        bool playerWasOn;
        readonly List<Vector3> cells = new();
        readonly HashSet<CharacterController> riders = new();
        static readonly Collider[] overlap = new Collider[16];

        public static Lift Create(Transform parent, List<Vector3> cellCenters, float cellSize, float low, float high, float light)
        {
            var go = new GameObject("Lift") { layer = Layers.World };
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0, high, 0);

            float h = cellSize * 0.5f;
            float depth = high - low + 1f;
            var mb = new LevelMeshBuilder();
            foreach (var c in cellCenters)
            {
                float x0 = c.x - h, x1 = c.x + h, z0 = c.z - h, z1 = c.z + h;
                mb.Floor("lift_top", x0, z0, x1, z1, 0f, light, true);
                mb.Wall("lift_side", new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), -depth, 0f, light);
                mb.Wall("lift_side", new Vector3(x0, 0, z1), new Vector3(x0, 0, z0), -depth, 0f, light);
                mb.Wall("lift_side", new Vector3(x1, 0, z1), new Vector3(x0, 0, z1), -depth, 0f, light);
                mb.Wall("lift_side", new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), -depth, 0f, light);
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(c.x, -depth * 0.5f, c.z);
                box.size = new Vector3(cellSize, depth, cellSize);
            }
            var mesh = mb.Build("LiftMesh", out var mats);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;

            var lift = go.AddComponent<Lift>();
            lift.cells.AddRange(cellCenters);
            lift.half = h;
            lift.Low = low;
            lift.High = high;
            lift.height = high;
            return lift;
        }

        public void Use(PlayerController player) => Activate();

        public void Activate()
        {
            if (state != State.Up) return;
            state = State.Lowering;
            AudioManager.I.PlayAt("lift_start", Center);
        }

        Vector3 Center
        {
            get
            {
                var sum = Vector3.zero;
                foreach (var c in cells) sum += c;
                return sum / cells.Count + Vector3.up * height;
            }
        }

        void CollectRiders()
        {
            riders.Clear();
            foreach (var c in cells)
            {
                int n = Physics.OverlapBoxNonAlloc(new Vector3(c.x, height + 0.15f, c.z), new Vector3(half * 0.98f, 0.2f, half * 0.98f),
                    overlap, Quaternion.identity, Layers.ActorMask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                    if (overlap[i] is CharacterController cc && cc.enabled) riders.Add(cc);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            CollectRiders();
            var player = GameManager.I != null ? GameManager.I.Player : null;
            bool playerOn = false;
            if (player != null)
                foreach (var r in riders)
                    if (r.gameObject == player.gameObject) playerOn = true;

            // Stepping onto the raised lift sends it down (like a walk-over trigger).
            if (state == State.Up && playerOn && !playerWasOn) Activate();
            playerWasOn = playerOn;

            switch (state)
            {
                case State.Lowering:
                    MoveTo(Low, dt);
                    if (height <= Low)
                    {
                        state = State.Down;
                        waitTimer = WaitTime;
                        AudioManager.I.PlayAt("lift_stop", Center);
                    }
                    break;
                case State.Down:
                    waitTimer -= dt;
                    if (waitTimer <= 0f)
                    {
                        state = State.Raising;
                        AudioManager.I.PlayAt("lift_start", Center);
                    }
                    break;
                case State.Raising:
                    MoveTo(High, dt);
                    if (height >= High)
                    {
                        state = State.Up;
                        AudioManager.I.PlayAt("lift_stop", Center);
                    }
                    break;
            }
        }

        void MoveTo(float target, float dt)
        {
            float next = Mathf.MoveTowards(height, target, Speed * dt);
            float delta = next - height;
            if (delta == 0f) return;

            // Going up: lift riders first so the platform doesn't move into them.
            // Going down: move the platform first so riders have somewhere to go.
            if (delta > 0f) MoveRiders(delta);
            height = next;
            transform.position = new Vector3(0, height, 0);
            if (delta < 0f) MoveRiders(delta);
        }

        void MoveRiders(float delta)
        {
            foreach (var cc in riders)
            {
                if (cc == null || !cc.enabled) continue;
                if (cc.TryGetComponent<PlayerController>(out var pc)) pc.Ride(delta);
                else if (cc.TryGetComponent<Enemy>(out var e)) e.Ride(delta);
            }
        }

        public LiftSave Capture(int index) => new() { index = index, state = (int)state, height = height, wait = waitTimer };

        public void Restore(LiftSave s)
        {
            state = (State)s.state;
            height = Mathf.Clamp(s.height, Low, High);
            waitTimer = s.wait;
            transform.position = new Vector3(0, height, 0);
            playerWasOn = true; // don't retrigger just because the player was loaded standing on it
        }
    }
}
