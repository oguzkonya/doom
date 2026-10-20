using UnityEngine;

namespace Doom
{
    public enum PickupType
    {
        HealthBonus, Stimpack, Medikit, Soulsphere, ArmorBonus, GreenArmor, BlueArmor, Backpack,
        Clip, AmmoBox, Shells, ShellBox, Rocket, RocketBox, Cell, CellPack,
        Chainsaw, Shotgun, Chaingun, RocketLauncher, PlasmaGun, BFG,
        RedKey, BlueKey, YellowKey,
        Berserk, Invulnerability, PartialInvisibility, RadiationSuit, ComputerMap, LightAmp
    }

    public class Pickup : MonoBehaviour
    {
        const float TouchRadius = 1.1f;

        public PickupType Type { get; private set; }
        public bool Dropped { get; private set; }

        /// <summary>Like the original, only bonuses and powerups count toward the items percentage.</summary>
        public bool CountsAsItem => !Dropped && (Type == PickupType.HealthBonus || Type == PickupType.ArmorBonus ||
                                                 Type == PickupType.Soulsphere || IsPowerup);

        public bool IsKey => Type == PickupType.RedKey || Type == PickupType.BlueKey || Type == PickupType.YellowKey;
        public bool IsPowerup => Type >= PickupType.Berserk;

        Billboard sprite;
        float blink;

        public static string SpriteName(PickupType t) => "item_" + t.ToString().ToLowerInvariant();

        public static Pickup Spawn(PickupType type, Transform parent, Vector3 position, bool dropped)
        {
            var go = new GameObject("Pickup " + type);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            var p = go.AddComponent<Pickup>();
            p.Type = type;
            p.Dropped = dropped;
            bool bright = type == PickupType.Soulsphere || p.IsKey || p.IsPowerup;
            p.sprite = Billboard.Create(go.transform, false, bright);
            p.sprite.SetSprite(SpriteName(type));
            return p;
        }

        void Update()
        {
            if (IsKey)
            {
                blink += Time.deltaTime;
                sprite.SetVisible((int)(blink * 3f) % 2 == 0);
            }

            var player = GameManager.I.Player;
            if (player == null || player.Status.IsDead) return;
            var d = player.transform.position - transform.position;
            if (Mathf.Abs(d.y) > 1.2f) return;
            d.y = 0;
            if (d.sqrMagnitude > TouchRadius * TouchRadius) return;

            if (!player.Status.TryPickup(Type, Dropped, out var message, out var sound)) return;
            GameManager.I.OnPickup(this, message, sound);
            Destroy(gameObject);
        }
    }
}
