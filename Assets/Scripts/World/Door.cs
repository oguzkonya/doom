using UnityEngine;

namespace Doom
{
    public enum KeyColor { None = -1, Red = 0, Blue = 1, Yellow = 2 }

    public interface IUsable
    {
        void Use(PlayerController player);
    }

    /// <summary>A door occupies a whole cell and slides up into the ceiling, then closes after a delay.</summary>
    public class Door : MonoBehaviour, IUsable
    {
        enum State { Closed, Opening, Open, Closing }

        const float Speed = 4f;
        const float WaitTime = 4f;

        public KeyColor Key { get; private set; }
        public bool Secret { get; private set; }
        public bool IsOpen => offset >= travel * 0.6f;
        public bool MonstersCanOpen => Key == KeyColor.None && !Secret;

        State state;
        float travel, offset, waitTimer, height, size;
        Vector3 basePos;

        public static Door Create(Transform parent, Vector3 floorCenter, float size, float height, string faceTex,
            string sideTex, bool passageNS, KeyColor key, bool secret, float light)
        {
            var go = new GameObject(secret ? "SecretDoor" : "Door");
            go.layer = Layers.World;
            go.transform.SetParent(parent, false);
            go.transform.position = floorCenter;

            float h = size * 0.5f;
            string ns = passageNS ? faceTex : sideTex;
            string ew = passageNS ? sideTex : faceTex;
            var mb = new LevelMeshBuilder();
            mb.Wall(ew, new Vector3(h, 0, -h), new Vector3(h, 0, h), 0, height, light);
            mb.Wall(ew, new Vector3(-h, 0, h), new Vector3(-h, 0, -h), 0, height, light);
            mb.Wall(ns, new Vector3(h, 0, h), new Vector3(-h, 0, h), 0, height, light);
            mb.Wall(ns, new Vector3(-h, 0, -h), new Vector3(h, 0, -h), 0, height, light);
            mb.Floor(sideTex, -h, -h, h, h, 0, light, false);
            var mesh = mb.Build("DoorMesh", out var mats);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0, height * 0.5f, 0);
            box.size = new Vector3(size, height, size);

            var door = go.AddComponent<Door>();
            door.Key = key;
            door.Secret = secret;
            door.height = height;
            door.size = size;
            door.travel = height - 0.12f;
            door.basePos = floorCenter;
            return door;
        }

        public DoorSave Capture(Vector2Int cell) => new()
        {
            x = cell.x, y = cell.y, state = (int)state, offset = offset, wait = waitTimer
        };

        public void Restore(DoorSave s)
        {
            state = (State)s.state;
            offset = Mathf.Clamp(s.offset, 0f, travel);
            waitTimer = s.wait;
            transform.position = basePos + Vector3.up * offset;
        }

        public void Use(PlayerController player)
        {
            if (Key != KeyColor.None && !player.Status.HasKey(Key))
            {
                string color = Key.ToString().ToLowerInvariant();
                GameManager.I.ShowMessage($"You need a {color} key to open this door");
                AudioManager.I.Play2D("noway");
                return;
            }

            switch (state)
            {
                case State.Closed:
                case State.Closing:
                    Open();
                    break;
                case State.Open when !Secret:
                    state = State.Closing;
                    AudioManager.I.PlayAt("door_close", transform.position);
                    break;
            }
        }

        /// <summary>Monsters bumping into an unlocked door open it, like in the original.</summary>
        public void MonsterBump()
        {
            if (MonstersCanOpen && (state == State.Closed || state == State.Closing)) Open();
        }

        void Open()
        {
            if (state != State.Opening && state != State.Open)
                AudioManager.I.PlayAt("door_open", transform.position);
            state = State.Opening;
        }

        void Update()
        {
            switch (state)
            {
                case State.Opening:
                    offset = Mathf.MoveTowards(offset, travel, Speed * Time.deltaTime);
                    if (offset >= travel) { state = State.Open; waitTimer = WaitTime; }
                    break;
                case State.Open:
                    if (Secret) break;
                    waitTimer -= Time.deltaTime;
                    if (waitTimer <= 0f)
                    {
                        if (Blocked()) waitTimer = 1f;
                        else
                        {
                            state = State.Closing;
                            AudioManager.I.PlayAt("door_close", transform.position);
                        }
                    }
                    break;
                case State.Closing:
                    if (Blocked()) { Open(); break; }
                    offset = Mathf.MoveTowards(offset, 0f, Speed * Time.deltaTime);
                    if (offset <= 0f) state = State.Closed;
                    break;
            }
            transform.position = basePos + Vector3.up * offset;
        }

        static readonly Collider[] overlap = new Collider[8];

        bool Blocked()
        {
            int n = Physics.OverlapBoxNonAlloc(basePos + Vector3.up * height * 0.5f,
                new Vector3(size * 0.48f, height * 0.5f, size * 0.48f), overlap, Quaternion.identity, Layers.ActorMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (overlap[i] is CharacterController cc && cc.enabled) return true;
            return false;
        }
    }
}
