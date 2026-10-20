using UnityEngine;

namespace Doom
{
    public enum AmmoType { None = -1, Bullets = 0, Shells = 1, Rockets = 2, Cells = 3 }

    /// <summary>What carries over between levels.</summary>
    public class PlayerSnapshot
    {
        public int Health = 100, Armor, ArmorClass;
        public int Weapon = (int)WeaponId.Pistol;
        public bool[] Weapons = { true, false, true, false, false, false, false, false };
        public int[] Ammo = { 50, 0, 0, 0 };
        public bool Backpack;
    }

    /// <summary>Health, armor, inventory and pickup rules.</summary>
    public class PlayerStatus : MonoBehaviour, IDamageable
    {
        public static readonly int[] BaseMaxAmmo = { 200, 50, 50, 300 };

        public int Health = 100;
        public int Armor;
        public int ArmorClass; // 0 none, 1 green (1/3 absorbed), 2 blue (1/2 absorbed)
        public readonly bool[] Weapons = new bool[8];
        public readonly int[] Ammo = new int[4];
        public readonly bool[] Keys = new bool[3];
        public bool Backpack;
        public bool God;

        // HUD feedback
        public float DamageFlash;   // 0..1
        public float BonusFlash;    // 0..1
        public float OuchTimer, GrinTimer, LookTimer;
        public int LookDirection;   // -1 left, 0 center, 1 right

        public bool IsDead => Health <= 0;
        public bool Bleeds => true;

        PlayerController controllerCache;
        PlayerWeapons weaponsCache;
        PlayerController controller => controllerCache ? controllerCache : controllerCache = GetComponent<PlayerController>();
        PlayerWeapons weapons => weaponsCache ? weaponsCache : weaponsCache = GetComponent<PlayerWeapons>();

        public int MaxAmmo(AmmoType t) => BaseMaxAmmo[(int)t] * (Backpack ? 2 : 1);
        public bool HasKey(KeyColor k) => k == KeyColor.None || Keys[(int)k];

        public void Load(PlayerSnapshot s)
        {
            s ??= new PlayerSnapshot();
            Health = s.Health;
            Armor = s.Armor;
            ArmorClass = s.ArmorClass;
            Backpack = s.Backpack;
            for (int i = 0; i < Weapons.Length; i++) Weapons[i] = s.Weapons[i];
            for (int i = 0; i < Ammo.Length; i++) Ammo[i] = s.Ammo[i];
            for (int i = 0; i < Keys.Length; i++) Keys[i] = false;
            weapons.SetImmediate(s.Weapon);
        }

        public PlayerSnapshot Save() => new()
        {
            Health = Health,
            Armor = Armor,
            ArmorClass = ArmorClass,
            Backpack = Backpack,
            Weapon = weapons.Current,
            Weapons = (bool[])Weapons.Clone(),
            Ammo = (int[])Ammo.Clone()
        };

        void Update()
        {
            float dt = Time.deltaTime;
            DamageFlash = Mathf.MoveTowards(DamageFlash, 0f, dt * 1.2f);
            BonusFlash = Mathf.MoveTowards(BonusFlash, 0f, dt * 2.5f);
            OuchTimer -= dt;
            GrinTimer -= dt;
            LookTimer -= dt;
            if (LookTimer <= 0f)
            {
                LookTimer = Random.Range(0.6f, 1.6f);
                LookDirection = Random.Range(-1, 2);
            }
        }

        public void Damage(int amount, Vector3 direction, GameObject source)
        {
            if (IsDead || God || amount <= 0) return;
            if (GameManager.I.Skill == 1) amount = Mathf.Max(1, amount / 2);

            if (ArmorClass > 0)
            {
                int saved = ArmorClass == 1 ? amount / 3 : amount / 2;
                if (Armor <= saved)
                {
                    saved = Armor;
                    ArmorClass = 0;
                }
                Armor -= saved;
                amount -= saved;
            }

            Health = Mathf.Max(0, Health - amount);
            DamageFlash = Mathf.Min(1f, DamageFlash + amount / 40f);
            if (amount >= 20) OuchTimer = 1f;

            if (source != null && source != gameObject)
            {
                var local = transform.InverseTransformPoint(source.transform.position);
                LookDirection = local.x < -0.5f ? -1 : local.x > 0.5f ? 1 : 0;
                LookTimer = 1f;
            }

            if (direction != Vector3.zero) controller.AddKnockback(direction, amount);
            GameManager.I.Rumble(Mathf.Clamp01(amount / 30f), Mathf.Clamp01(amount / 50f), 0.25f);

            if (Health <= 0)
            {
                AudioManager.I.Play2D("player_death");
                GameManager.I.OnPlayerDied();
            }
            else AudioManager.I.Play2D("player_pain");
        }

        // ------------------------------------------------------------------ pickups

        bool GiveAmmo(AmmoType type, int amount, bool dropped)
        {
            int i = (int)type;
            if (Ammo[i] >= MaxAmmo(type)) return false;
            if (dropped) amount = Mathf.Max(1, amount / 2);
            int skill = GameManager.I.Skill;
            if (skill == 1 || skill == 5) amount *= 2;
            bool wasEmpty = Ammo[i] == 0;
            Ammo[i] = Mathf.Min(MaxAmmo(type), Ammo[i] + amount);
            if (wasEmpty) weapons.OnAmmoFromEmpty(type);
            return true;
        }

        bool GiveWeapon(WeaponId w, AmmoType ammo, int amount, bool dropped)
        {
            bool gotAmmo = ammo != AmmoType.None && GiveAmmo(ammo, amount, dropped);
            if (Weapons[(int)w]) return gotAmmo;
            Weapons[(int)w] = true;
            weapons.Select((int)w);
            GrinTimer = 2f;
            return true;
        }

        bool GiveHealth(int amount, int max)
        {
            if (Health >= max) return false;
            Health = Mathf.Min(max, Health + amount);
            return true;
        }

        bool GiveArmor(int cls)
        {
            int amount = cls * 100;
            if (Armor >= amount) return false;
            ArmorClass = cls;
            Armor = amount;
            return true;
        }

        public bool TryPickup(PickupType type, bool dropped, out string message, out string sound)
        {
            sound = "item_pickup";
            message = null;
            bool ok;
            switch (type)
            {
                case PickupType.HealthBonus:
                    Health = Mathf.Min(200, Health + 1); ok = true; message = "Picked up a health bonus.";
                    break;
                case PickupType.Stimpack:
                    ok = GiveHealth(10, 100); message = "Picked up a stimpack.";
                    break;
                case PickupType.Medikit:
                    bool needed = Health < 25;
                    ok = GiveHealth(25, 100);
                    message = needed ? "Picked up a medikit that you REALLY need!" : "Picked up a medikit.";
                    break;
                case PickupType.Soulsphere:
                    Health = Mathf.Min(200, Health + 100); ok = true; message = "Supercharge!"; sound = "powerup";
                    break;
                case PickupType.ArmorBonus:
                    Armor = Mathf.Min(200, Armor + 1);
                    if (ArmorClass == 0) ArmorClass = 1;
                    ok = true; message = "Picked up an armor bonus.";
                    break;
                case PickupType.GreenArmor:
                    ok = GiveArmor(1); message = "Picked up the armor.";
                    break;
                case PickupType.BlueArmor:
                    ok = GiveArmor(2); message = "Picked up the MegaArmor!";
                    break;
                case PickupType.Backpack:
                    if (!Backpack) Backpack = true;
                    GiveAmmo(AmmoType.Bullets, 10, false);
                    GiveAmmo(AmmoType.Shells, 4, false);
                    GiveAmmo(AmmoType.Rockets, 1, false);
                    GiveAmmo(AmmoType.Cells, 20, false);
                    ok = true; message = "Picked up a backpack full of ammo!";
                    break;
                case PickupType.Clip:
                    ok = GiveAmmo(AmmoType.Bullets, 10, dropped); message = "Picked up a clip.";
                    break;
                case PickupType.AmmoBox:
                    ok = GiveAmmo(AmmoType.Bullets, 50, dropped); message = "Picked up a box of bullets.";
                    break;
                case PickupType.Shells:
                    ok = GiveAmmo(AmmoType.Shells, 4, dropped); message = "Picked up 4 shotgun shells.";
                    break;
                case PickupType.ShellBox:
                    ok = GiveAmmo(AmmoType.Shells, 20, dropped); message = "Picked up a box of shotgun shells.";
                    break;
                case PickupType.Rocket:
                    ok = GiveAmmo(AmmoType.Rockets, 1, dropped); message = "Picked up a rocket.";
                    break;
                case PickupType.RocketBox:
                    ok = GiveAmmo(AmmoType.Rockets, 5, dropped); message = "Picked up a box of rockets.";
                    break;
                case PickupType.Cell:
                    ok = GiveAmmo(AmmoType.Cells, 20, dropped); message = "Picked up an energy cell.";
                    break;
                case PickupType.CellPack:
                    ok = GiveAmmo(AmmoType.Cells, 100, dropped); message = "Picked up an energy cell pack.";
                    break;
                case PickupType.Chainsaw:
                    ok = GiveWeapon(WeaponId.Chainsaw, AmmoType.None, 0, dropped); message = "A chainsaw! Find some meat!"; sound = "weapon_pickup";
                    break;
                case PickupType.Shotgun:
                    ok = GiveWeapon(WeaponId.Shotgun, AmmoType.Shells, 8, dropped); message = "You got the shotgun!"; sound = "weapon_pickup";
                    break;
                case PickupType.Chaingun:
                    ok = GiveWeapon(WeaponId.Chaingun, AmmoType.Bullets, 20, dropped); message = "You got the chaingun!"; sound = "weapon_pickup";
                    break;
                case PickupType.RocketLauncher:
                    ok = GiveWeapon(WeaponId.RocketLauncher, AmmoType.Rockets, 2, dropped); message = "You got the rocket launcher!"; sound = "weapon_pickup";
                    break;
                case PickupType.PlasmaGun:
                    ok = GiveWeapon(WeaponId.PlasmaGun, AmmoType.Cells, 40, dropped); message = "You got the plasma gun!"; sound = "weapon_pickup";
                    break;
                case PickupType.BFG:
                    ok = GiveWeapon(WeaponId.BFG, AmmoType.Cells, 40, dropped); message = "You got the BFG9000! Oh, yes."; sound = "weapon_pickup";
                    break;
                case PickupType.RedKey:
                case PickupType.BlueKey:
                case PickupType.YellowKey:
                    int k = type - PickupType.RedKey;
                    ok = !Keys[k];
                    Keys[k] = true;
                    message = $"Picked up a {((KeyColor)k).ToString().ToLowerInvariant()} keycard.";
                    break;
                default:
                    ok = false;
                    break;
            }
            if (ok) BonusFlash = Mathf.Min(1f, BonusFlash + 0.5f);
            return ok;
        }

        // ------------------------------------------------------------------ cheats

        public void GiveEverything(bool keys)
        {
            for (int i = 0; i < Weapons.Length; i++) Weapons[i] = true;
            Backpack = true;
            for (int i = 0; i < Ammo.Length; i++) Ammo[i] = MaxAmmo((AmmoType)i);
            Armor = 200;
            ArmorClass = 2;
            if (keys) for (int i = 0; i < Keys.Length; i++) Keys[i] = true;
        }
    }
}
