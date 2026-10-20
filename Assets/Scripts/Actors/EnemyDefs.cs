using System.Collections.Generic;

namespace Doom
{
    public enum AttackKind { None, Hitscan, Projectile, Charge }

    /// <summary>Monster stats, converted from the original (1 map unit = 1/32 m, 35 tics/s).</summary>
    public class EnemyDef
    {
        public string Id;
        public int Health;
        public float Speed;           // m/s
        public float Radius, Height;  // m
        public int PainChance;        // out of 256
        public float PainTime = 0.2f;
        public float Mass = 100f;
        public bool Flying, Fullbright;

        public AttackKind Ranged;
        public int Pellets = 1;
        public string Projectile;
        public string AttackSound = "monster_attack";

        public bool Melee;
        public int MeleeSides = 8, MeleeMultiplier = 3;
        public string MeleeSound = "claw";

        public float Windup = 0.35f;   // time showing the attack frame before firing
        public float Cooldown = 1.2f;  // average time between attacks
        public float SoundPitch = 1f;  // pitch applied to the generic monster sounds
        public PickupType? Drop;
    }

    public static class EnemyDefs
    {
        static readonly Dictionary<string, EnemyDef> defs = new()
        {
            {
                "zombieman", new EnemyDef
                {
                    Id = "zombieman", Health = 20, Speed = 2.2f, Radius = 0.5f, Height = 1.75f, PainChance = 200,
                    Ranged = AttackKind.Hitscan, Pellets = 1, AttackSound = "pistol", Windup = 0.3f, Cooldown = 1.3f,
                    SoundPitch = 1.15f, Drop = PickupType.Clip
                }
            },
            {
                "shotgunguy", new EnemyDef
                {
                    Id = "shotgunguy", Health = 30, Speed = 2.2f, Radius = 0.5f, Height = 1.75f, PainChance = 170,
                    Ranged = AttackKind.Hitscan, Pellets = 3, AttackSound = "shotgun", Windup = 0.3f, Cooldown = 1.5f,
                    SoundPitch = 1f, Drop = PickupType.Shotgun
                }
            },
            {
                "imp", new EnemyDef
                {
                    Id = "imp", Health = 60, Speed = 2.2f, Radius = 0.5f, Height = 1.75f, PainChance = 200,
                    Ranged = AttackKind.Projectile, Projectile = "imp_ball", AttackSound = "fireball_launch",
                    Melee = true, MeleeSides = 8, MeleeMultiplier = 3, Windup = 0.4f, Cooldown = 1.4f, SoundPitch = 0.9f
                }
            },
            {
                "demon", new EnemyDef
                {
                    Id = "demon", Health = 150, Speed = 5.0f, Radius = 0.9f, Height = 1.75f, PainChance = 180, Mass = 400f,
                    Melee = true, MeleeSides = 10, MeleeMultiplier = 4, MeleeSound = "bite", Windup = 0.25f, Cooldown = 0.6f,
                    SoundPitch = 0.7f
                }
            },
            {
                "cacodemon", new EnemyDef
                {
                    Id = "cacodemon", Health = 400, Speed = 2.9f, Radius = 0.95f, Height = 1.75f, PainChance = 128, Mass = 400f,
                    Flying = true, Ranged = AttackKind.Projectile, Projectile = "caco_ball", AttackSound = "fireball_launch",
                    Melee = true, MeleeSides = 6, MeleeMultiplier = 10, MeleeSound = "bite", Windup = 0.4f, Cooldown = 1.6f,
                    SoundPitch = 0.6f
                }
            },
            {
                "lostsoul", new EnemyDef
                {
                    Id = "lostsoul", Health = 100, Speed = 1.6f, Radius = 0.5f, Height = 1.4f, PainChance = 256, Mass = 50f,
                    Flying = true, Fullbright = true, Ranged = AttackKind.Charge, Windup = 0.35f, Cooldown = 1.8f,
                    MeleeSides = 8, MeleeMultiplier = 3, SoundPitch = 1.5f
                }
            },
            {
                "baron", new EnemyDef
                {
                    Id = "baron", Health = 1000, Speed = 2.9f, Radius = 0.75f, Height = 2.0f, PainChance = 50, Mass = 1000f,
                    Ranged = AttackKind.Projectile, Projectile = "baron_ball", AttackSound = "fireball_launch",
                    Melee = true, MeleeSides = 8, MeleeMultiplier = 10, Windup = 0.45f, Cooldown = 1.5f, SoundPitch = 0.5f
                }
            },
        };

        public static EnemyDef Get(string id) => defs[id];
        public static bool TryGet(string id, out EnemyDef def) => defs.TryGetValue(id, out def);
        public static IEnumerable<EnemyDef> All => defs.Values;
    }
}
