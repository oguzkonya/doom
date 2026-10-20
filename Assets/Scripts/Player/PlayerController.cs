using UnityEngine;

namespace Doom
{
    /// <summary>
    /// First-person movement with Doom-like momentum, view bob and step smoothing.
    /// Horizontal mouse / stick turning; vertical look is optional (Freelook setting) and
    /// autoaim handles vertical targeting like the original.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public const float Radius = 0.5f;
        public const float Height = 1.75f;
        public const float EyeHeight = 1.28f;
        const float WalkSpeed = 7f;
        const float RunSpeed = 12f;
        const float Gravity = 22f;

        public Camera Cam { get; private set; }
        public PlayerStatus Status { get; private set; }
        public PlayerWeapons Weapons { get; private set; }
        public bool Noclip { get; set; }
        public float BobAmount { get; private set; }
        public float BobPhase { get; private set; }
        public float Yaw => yaw;

        public Vector3 Center => transform.position + Vector3.up * Height * 0.5f;
        public Vector3 EyePosition => eye.position;

        CharacterController cc;
        Transform eye;
        float yaw, pitch, vy, stepOffset, deathTime, nukageTimer;
        Vector3 velocity, knock;
        float lastY;
        Vector2Int teleportCell;

        public static PlayerController Spawn(Transform parent, Vector3 position, float yaw)
        {
            var go = new GameObject("Player") { layer = Layers.Actor };
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var cc = go.AddComponent<CharacterController>();
            cc.radius = Radius;
            cc.height = Height;
            cc.center = new Vector3(0, Height * 0.5f, 0);
            cc.stepOffset = 0.6f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;
            cc.minMoveDistance = 0f;

            var eye = new GameObject("Eye").transform;
            eye.SetParent(go.transform, false);
            eye.localPosition = new Vector3(0, EyeHeight, 0);
            var cam = eye.gameObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 250f;
            cam.fieldOfView = 74f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.allowMSAA = false;
            cam.allowHDR = false;

            var pc = go.AddComponent<PlayerController>();
            pc.cc = cc;
            pc.eye = eye;
            pc.Cam = cam;
            pc.Status = go.AddComponent<PlayerStatus>();
            pc.Weapons = go.AddComponent<PlayerWeapons>();
            pc.yaw = yaw;
            pc.lastY = position.y;
            var level = parent.GetComponent<Level>();
            if (level != null) pc.teleportCell = level.WorldToCell(position);
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            return pc;
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || !gm.IsGameplayActive) return;
            float dt = Time.deltaTime;

            if (Status.IsDead)
            {
                UpdateDead(dt);
                return;
            }

            Look(dt);
            Move(dt);
            if (!Noclip && GameManager.I.Level != null)
                GameManager.I.Level.TryTeleport(gameObject, transform.position, ref teleportCell);
            if (DoomInput.Use.WasPressedThisFrame()) TryUse();
            CheckFloor(dt);
        }

        void Look(float dt)
        {
            var mouse = DoomInput.MouseLook.ReadValue<Vector2>();
            float mouseSens = Settings.MouseSensitivity * 0.12f;
            var stick = DoomInput.StickLook.ReadValue<Vector2>();
            float stickSens = Settings.StickSensitivity;
            bool running = Settings.AlwaysRun ^ DoomInput.Run.IsPressed();

            yaw += mouse.x * mouseSens;
            yaw += stick.x * Mathf.Abs(stick.x) * 220f * stickSens * dt;
            yaw += DoomInput.KeyTurn.ReadValue<float>() * (running ? 200f : 130f) * dt;

            if (Settings.Freelook)
            {
                float invert = Settings.InvertY ? 1f : -1f;
                pitch += mouse.y * mouseSens * invert;
                pitch += stick.y * Mathf.Abs(stick.y) * 150f * stickSens * dt * invert;
                pitch = Mathf.Clamp(pitch, -70f, 70f);
            }
            else pitch = Mathf.MoveTowards(pitch, 0f, 200f * dt);

            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        void Move(float dt)
        {
            var input = DoomInput.Move.ReadValue<Vector2>();
            if (input.sqrMagnitude > 1f) input.Normalize();
            bool analog = DoomInput.MoveIsAnalog;
            bool run = analog || (Settings.AlwaysRun ^ DoomInput.Run.IsPressed());
            float speed = run ? RunSpeed : WalkSpeed;

            if (Noclip)
            {
                cc.enabled = false;
                var fly = eye.forward * input.y + transform.right * input.x;
                transform.position += fly * speed * 1.5f * dt;
                UpdateEye(dt, 0f);
                return;
            }
            if (!cc.enabled) cc.enabled = true;

            var wish = (transform.forward * input.y + transform.right * input.x) * speed;
            float k = wish.sqrMagnitude > 0.01f ? 10f : 7f;
            velocity = Vector3.Lerp(velocity, wish, 1f - Mathf.Exp(-k * dt));
            knock *= Mathf.Exp(-5f * dt);

            if (cc.isGrounded && vy < 0f) vy = -2f;
            vy -= Gravity * dt;
            cc.Move((velocity + knock + Vector3.up * vy) * dt);

            // Smooth the camera over steps instead of snapping.
            float dy = transform.position.y - lastY;
            if (cc.isGrounded && dy > 0.05f && dy < 0.7f) stepOffset -= dy;
            lastY = transform.position.y;

            var hv = cc.velocity;
            hv.y = 0;
            BobAmount = cc.isGrounded ? Mathf.Clamp01(hv.magnitude / RunSpeed) : BobAmount * 0.9f;
            BobPhase += dt * 11f;
            UpdateEye(dt, Mathf.Sin(BobPhase * 2f) * 0.06f * BobAmount);
        }

        void UpdateEye(float dt, float bob)
        {
            stepOffset = Mathf.MoveTowards(stepOffset, 0f, 3f * dt);
            eye.localPosition = new Vector3(0, EyeHeight + bob + stepOffset, 0);
            eye.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        void UpdateDead(float dt)
        {
            deathTime += dt;
            var p = eye.localPosition;
            eye.localPosition = new Vector3(0, Mathf.MoveTowards(p.y, 0.25f, 2.5f * dt), 0);
            eye.localRotation = Quaternion.Slerp(eye.localRotation, Quaternion.identity, 5f * dt);
            if (cc.enabled)
            {
                if (cc.isGrounded && vy < 0f) vy = -2f;
                vy -= Gravity * dt;
                velocity *= Mathf.Exp(-4f * dt);
                cc.Move((velocity + Vector3.up * vy) * dt);
            }
            if (deathTime > 1f && (DoomInput.Use.WasPressedThisFrame() || DoomInput.Fire.WasPressedThisFrame()))
                GameManager.I.RestartLevel();
        }

        void TryUse()
        {
            if (Combat.Trace(EyePosition, transform.forward, 2.2f, gameObject, Layers.WorldMask, out var hit))
            {
                var usable = hit.collider.GetComponentInParent<IUsable>();
                if (usable != null) usable.Use(this);
                else AudioManager.I.Play2D("noway");
            }
        }

        void CheckFloor(float dt)
        {
            var level = GameManager.I.Level;
            if (level == null || !cc.isGrounded || Noclip) return;
            var cell = level.CellAt(transform.position);
            if (!cell.Nukage) { nukageTimer = 0.5f; return; }
            nukageTimer -= dt;
            if (nukageTimer > 0f) return;
            nukageTimer = 1f;
            Status.Damage(5, Vector3.zero, null);
        }

        /// <summary>Instantly moves the player (used when loading a save).</summary>
        public void Teleport(Vector3 position, float newYaw)
        {
            cc.enabled = false;
            transform.position = position;
            cc.enabled = !Noclip;
            yaw = newYaw;
            pitch = 0f;
            vy = 0f;
            velocity = Vector3.zero;
            knock = Vector3.zero;
            stepOffset = 0f;
            lastY = position.y;
            var level = GameManager.I != null ? GameManager.I.Level : null;
            if (level != null) teleportCell = level.WorldToCell(position);
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        /// <summary>Carried by a moving lift.</summary>
        public void Ride(float dy)
        {
            if (!cc.enabled) return;
            float before = transform.position.y;
            cc.Move(Vector3.up * dy);
            lastY += transform.position.y - before; // no step smoothing while riding
        }

        public void AddKnockback(Vector3 direction, int damage)
        {
            var push = new Vector3(direction.x, 0, direction.z);
            if (push.sqrMagnitude < 0.0001f) return;
            knock += push.normalized * Mathf.Min(damage * 12f / 100f * 1.1f, 12f);
        }

        // ------------------------------------------------------------------ aiming

        /// <summary>Nearest-angle living monster within a horizontal cone that is in line of sight.</summary>
        public Enemy FindAutoaimTarget(Vector3 direction, float range, float maxAngle)
        {
            var level = GameManager.I.Level;
            if (level == null) return null;
            var flatDir = new Vector3(direction.x, 0, direction.z);
            Enemy best = null;
            float bestAngle = maxAngle;
            foreach (var e in level.Enemies)
            {
                if (e == null || e.IsDead) continue;
                var to = e.Center - EyePosition;
                if (to.magnitude > range) continue;
                float angle = Vector3.Angle(flatDir, new Vector3(to.x, 0, to.z));
                if (angle >= bestAngle) continue;
                if (!Combat.LineOfSight(EyePosition, e.Center)) continue;
                best = e;
                bestAngle = angle;
            }
            return best;
        }

        /// <summary>Shot direction: autoaim adjusts the vertical slope toward a monster in front of you.</summary>
        public Vector3 AimDirection()
        {
            var forward = transform.forward;
            if (Settings.Autoaim)
            {
                var target = FindAutoaimTarget(forward, 64f, Settings.Freelook ? 3f : 6f);
                if (target != null)
                {
                    var to = target.Center - EyePosition;
                    float horizontal = new Vector2(to.x, to.z).magnitude;
                    return (forward + Vector3.up * (to.y / Mathf.Max(horizontal, 0.01f))).normalized;
                }
            }
            return Settings.Freelook ? eye.forward : forward;
        }
    }
}
