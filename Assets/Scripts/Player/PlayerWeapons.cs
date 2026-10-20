using UnityEngine;

namespace Doom
{
    public enum WeaponId { Fist, Chainsaw, Pistol, Shotgun, Chaingun, RocketLauncher, PlasmaGun, BFG }

    public class WeaponDef
    {
        public WeaponId Id;
        public string Sprite;
        public AmmoType Ammo;
        public int AmmoPerShot;
        public float FireDelay;  // wind-up before the shot happens
        public float Cycle;      // time until the next shot is possible
        public string Sound;
        public float Flash;      // muzzle flash light boost

        // Timings converted from the original's tic-based state tables.
        public static readonly WeaponDef[] All =
        {
            new() { Id = WeaponId.Fist, Sprite = "fist", Ammo = AmmoType.None, FireDelay = 0.11f, Cycle = 0.63f, Sound = null },
            new() { Id = WeaponId.Chainsaw, Sprite = "chainsaw", Ammo = AmmoType.None, FireDelay = 0f, Cycle = 0.12f, Sound = "saw" },
            new() { Id = WeaponId.Pistol, Sprite = "pistol", Ammo = AmmoType.Bullets, AmmoPerShot = 1, FireDelay = 0.11f, Cycle = 0.4f, Sound = "pistol", Flash = 0.2f },
            new() { Id = WeaponId.Shotgun, Sprite = "shotgun", Ammo = AmmoType.Shells, AmmoPerShot = 1, FireDelay = 0.09f, Cycle = 1.06f, Sound = "shotgun", Flash = 0.3f },
            new() { Id = WeaponId.Chaingun, Sprite = "chaingun", Ammo = AmmoType.Bullets, AmmoPerShot = 1, FireDelay = 0f, Cycle = 0.115f, Sound = "pistol", Flash = 0.2f },
            new() { Id = WeaponId.RocketLauncher, Sprite = "rocketlauncher", Ammo = AmmoType.Rockets, AmmoPerShot = 1, FireDelay = 0.23f, Cycle = 0.57f, Sound = "rocket_fire", Flash = 0.3f },
            new() { Id = WeaponId.PlasmaGun, Sprite = "plasmagun", Ammo = AmmoType.Cells, AmmoPerShot = 1, FireDelay = 0f, Cycle = 0.086f, Sound = "plasma_fire", Flash = 0.15f },
            new() { Id = WeaponId.BFG, Sprite = "bfg", Ammo = AmmoType.Cells, AmmoPerShot = 40, FireDelay = 0.57f, Cycle = 1.2f, Sound = "bfg_fire", Flash = 0.4f },
        };

        public static readonly int[] SwitchPriority =
        {
            (int)WeaponId.PlasmaGun, (int)WeaponId.Chaingun, (int)WeaponId.Shotgun, (int)WeaponId.Pistol,
            (int)WeaponId.Chainsaw, (int)WeaponId.RocketLauncher, (int)WeaponId.BFG, (int)WeaponId.Fist
        };
    }

    public class PlayerWeapons : MonoBehaviour
    {
        const float SwitchTime = 0.18f;

        public int Current { get; private set; } = (int)WeaponId.Pistol;
        public WeaponDef CurrentDef => WeaponDef.All[Current];
        /// <summary>0 = fully raised, 1 = lowered off screen.</summary>
        public float Lower { get; private set; } = 1f;

        int pending = -1;
        float cooldown, delayTimer, sinceShot = 99f;
        bool shotPending, refire;
        PlayerController pc;
        PlayerStatus status;

        void Awake()
        {
            pc = GetComponent<PlayerController>();
            status = GetComponent<PlayerStatus>();
        }

        public void SetImmediate(int weapon)
        {
            Current = weapon;
            pending = -1;
            Lower = 1f;
        }

        public void Select(int weapon)
        {
            if (status == null) status = GetComponent<PlayerStatus>();
            if (!status.Weapons[weapon]) return;
            if (weapon == Current) { pending = -1; return; }
            pending = weapon;
        }

        public bool HasAmmo(int weapon)
        {
            var def = WeaponDef.All[weapon];
            return def.AmmoPerShot == 0 || status.Ammo[(int)def.Ammo] >= def.AmmoPerShot;
        }

        /// <summary>Like the original: picking up ammo while holding an empty fist/pistol switches up.</summary>
        public void OnAmmoFromEmpty(AmmoType type)
        {
            if (Current != (int)WeaponId.Fist && Current != (int)WeaponId.Pistol) return;
            int w = type switch
            {
                AmmoType.Bullets => status.Weapons[(int)WeaponId.Chaingun] ? (int)WeaponId.Chaingun : (int)WeaponId.Pistol,
                AmmoType.Shells => (int)WeaponId.Shotgun,
                AmmoType.Cells => (int)WeaponId.PlasmaGun,
                _ => -1
            };
            if (w >= 0 && w != Current && status.Weapons[w] && Current == (int)WeaponId.Fist) Select(w);
        }

        void SwitchToBest()
        {
            foreach (var w in WeaponDef.SwitchPriority)
                if (status.Weapons[w] && HasAmmo(w)) { Select(w); return; }
        }

        void Cycle(int dir)
        {
            for (int i = 1; i <= WeaponDef.All.Length; i++)
            {
                int w = ((pending >= 0 ? pending : Current) + dir * i + WeaponDef.All.Length * 2) % WeaponDef.All.Length;
                if (status.Weapons[w]) { Select(w); return; }
            }
        }

        void HandleSwitchInput()
        {
            if (DoomInput.Slots[0].WasPressedThisFrame())
            {
                bool saw = status.Weapons[(int)WeaponId.Chainsaw];
                int target = saw && Current != (int)WeaponId.Chainsaw ? (int)WeaponId.Chainsaw : (int)WeaponId.Fist;
                Select(target);
            }
            for (int i = 1; i < DoomInput.Slots.Length; i++)
                if (DoomInput.Slots[i].WasPressedThisFrame()) Select(i + 1);

            float wheel = DoomInput.MouseWheel;
            if (DoomInput.NextWeapon.WasPressedThisFrame() || wheel > 0.1f) Cycle(1);
            if (DoomInput.PrevWeapon.WasPressedThisFrame() || wheel < -0.1f) Cycle(-1);
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || !gm.IsGameplayActive) return;
            float dt = Time.deltaTime;

            if (status.IsDead)
            {
                Lower = Mathf.MoveTowards(Lower, 1f, dt * 2f);
                return;
            }

            HandleSwitchInput();
            cooldown -= dt;
            sinceShot += dt;

            if (shotPending)
            {
                delayTimer -= dt;
                if (delayTimer <= 0f)
                {
                    shotPending = false;
                    Fire();
                }
                return;
            }

            if (pending >= 0 && cooldown <= 0f)
            {
                Lower += dt / SwitchTime;
                if (Lower >= 1f)
                {
                    Lower = 1f;
                    Current = pending;
                    pending = -1;
                    if (Current == (int)WeaponId.Chainsaw) AudioManager.I.Play2D("saw", 0.6f);
                }
                return;
            }
            if (Lower > 0f) { Lower = Mathf.Max(0f, Lower - dt / SwitchTime); return; }

            bool held = DoomInput.Fire.IsPressed();
            if (!held) refire = false;
            if (!held || cooldown > 0f) return;

            if (!HasAmmo(Current))
            {
                SwitchToBest();
                return;
            }

            cooldown = CurrentDef.Cycle;
            sinceShot = -CurrentDef.FireDelay;
            delayTimer = CurrentDef.FireDelay;
            if (delayTimer <= 0f) Fire();
            else shotPending = true;
        }

        void Fire()
        {
            var def = CurrentDef;
            if (def.AmmoPerShot > 0)
            {
                ref int ammo = ref status.Ammo[(int)def.Ammo];
                if (ammo < def.AmmoPerShot) return;
                ammo -= def.AmmoPerShot;
            }

            var gm = GameManager.I;
            var origin = pc.EyePosition;
            var dir = pc.AimDirection();
            sinceShot = 0f;
            if (def.Sound != null) AudioManager.I.Play2D(def.Sound);
            if (def.Flash > 0f) gm.MuzzleFlash(def.Flash);
            if (gm.Level != null) gm.Level.NoiseAlert(gameObject);

            switch (def.Id)
            {
                case WeaponId.Fist:
                    int punch = Combat.Roll(10, 2) * (status.Berserk ? 10 : 1);
                    if (Combat.Melee(origin, dir, 2.0f, punch, gameObject)) AudioManager.I.Play2D("punch");
                    break;
                case WeaponId.Chainsaw:
                    Combat.Melee(origin, dir, 2.1f, Combat.Roll(10, 2), gameObject);
                    gm.Rumble(0.3f, 0.1f, 0.12f);
                    break;
                case WeaponId.Pistol:
                case WeaponId.Chaingun:
                    // First shot is perfectly accurate; holding the trigger adds spread.
                    float spread = refire ? Combat.Spread(5.6f) : 0f;
                    Combat.FireBullet(origin, Quaternion.AngleAxis(spread, Vector3.up) * dir, 64f, Combat.Roll(3, 5), gameObject);
                    gm.Rumble(0.1f, 0.25f, 0.08f);
                    break;
                case WeaponId.Shotgun:
                    for (int i = 0; i < 7; i++)
                        Combat.FireBullet(origin, Quaternion.AngleAxis(Combat.Spread(5.6f), Vector3.up) * dir, 64f, Combat.Roll(3, 5), gameObject);
                    gm.Rumble(0.5f, 0.6f, 0.18f);
                    break;
                case WeaponId.RocketLauncher:
                    Projectile.Spawn("rocket", origin - Vector3.up * 0.15f, dir, gameObject);
                    gm.Rumble(0.6f, 0.3f, 0.2f);
                    break;
                case WeaponId.PlasmaGun:
                    Projectile.Spawn("plasma", origin - Vector3.up * 0.15f, dir, gameObject);
                    gm.Rumble(0.05f, 0.2f, 0.06f);
                    break;
                case WeaponId.BFG:
                    Projectile.Spawn("bfg", origin - Vector3.up * 0.15f, dir, gameObject);
                    gm.Rumble(1f, 0.8f, 0.4f);
                    break;
            }
            refire = true;
        }

        /// <summary>Sprite to draw for the first-person weapon.</summary>
        public string SpriteName
        {
            get
            {
                var def = CurrentDef;
                string frame = "idle";
                if (sinceShot >= 0f)
                {
                    if (sinceShot < 0.07f) frame = "fire0";
                    else if (sinceShot < Mathf.Max(0.12f, def.Cycle * 0.55f)) frame = "fire1";
                }
                else if (def.Id == WeaponId.Fist || def.Id == WeaponId.BFG) frame = "fire1";
                return "weapon_" + def.Sprite + "_" + frame;
            }
        }

        public bool IsFiring => sinceShot < 0.07f;
    }
}
