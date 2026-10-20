using System.Collections.Generic;
using UnityEngine;

namespace Doom
{
    public class ProjectileDef
    {
        public string Id;
        public float Speed;
        public float Radius = 0.25f;
        public int DamageMultiplier;    // damage = (1..8) * multiplier
        public float SplashRadius;
        public int SplashDamage;
        public string HitFx;            // sprite base for the impact animation
        public string HitSound = "fireball_hit";
        public bool BfgSpray;

        public static readonly Dictionary<string, ProjectileDef> All = new()
        {
            // Speeds/damage converted from the original (1 map unit = 1/32 m, 35 tics/s).
            { "imp_ball",   new ProjectileDef { Id = "imp_ball", Speed = 10.9f, DamageMultiplier = 3, HitFx = "proj_imp_ball_hit" } },
            { "caco_ball",  new ProjectileDef { Id = "caco_ball", Speed = 10.9f, DamageMultiplier = 5, HitFx = "proj_caco_ball_hit" } },
            { "baron_ball", new ProjectileDef { Id = "baron_ball", Speed = 16.4f, DamageMultiplier = 8, HitFx = "proj_baron_ball_hit" } },
            { "rocket",     new ProjectileDef { Id = "rocket", Speed = 21.9f, Radius = 0.2f, DamageMultiplier = 20, SplashRadius = 4f, SplashDamage = 128, HitFx = "fx_explosion_", HitSound = "explosion" } },
            { "plasma",     new ProjectileDef { Id = "plasma", Speed = 27.3f, Radius = 0.2f, DamageMultiplier = 5, HitFx = "proj_plasma_hit", HitSound = "fireball_hit" } },
            { "bfg",        new ProjectileDef { Id = "bfg", Speed = 27.3f, Radius = 0.4f, DamageMultiplier = 100, HitFx = "proj_bfg_hit", HitSound = "bfg_explode", BfgSpray = true } },
        };
    }

    public class Projectile : MonoBehaviour
    {
        ProjectileDef def;
        GameObject owner;
        Vector3 velocity;
        float age;
        Billboard sprite;

        public static Projectile Spawn(string id, Vector3 position, Vector3 direction, GameObject owner, float speedScale = 1f)
        {
            var def = ProjectileDef.All[id];
            var go = new GameObject("Proj " + id);
            go.transform.position = position;
            var level = GameManager.I.Level;
            if (level) go.transform.SetParent(level.transform, true);
            var p = go.AddComponent<Projectile>();
            p.def = def;
            p.owner = owner;
            p.velocity = direction.normalized * def.Speed * speedScale;
            p.sprite = Billboard.Create(go.transform, true, true);
            p.sprite.SetSprite($"proj_{id}_fly0");
            return p;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            age += dt;
            if (age > 8f) { Destroy(gameObject); return; }

            sprite.SetSprite($"proj_{def.Id}_fly{(int)(age * 8f) % 2}");

            float dist = velocity.magnitude * dt;
            var dir = velocity.normalized;
            if (Combat.SphereTrace(transform.position, def.Radius, dir, dist, owner, Layers.ShotMask, out var hit))
            {
                Explode(transform.position + dir * hit.distance, hit.collider);
                return;
            }
            transform.position += velocity * dt;
        }

        void Explode(Vector3 point, Collider hitCollider)
        {
            var target = hitCollider.GetComponentInParent<IDamageable>();
            if (target != null && !target.IsDead && !SameSpecies(hitCollider))
                target.Damage(Combat.Roll(8, def.DamageMultiplier), velocity.normalized, owner);

            if (def.SplashRadius > 0f) Combat.RadiusDamage(point, def.SplashRadius, def.SplashDamage, owner);
            if (def.BfgSpray) BfgSpray();

            Effect.Spawn(def.HitFx, 3, 0.1f, point);
            AudioManager.I.PlayAt(def.HitSound, point);
            Destroy(gameObject);
        }

        /// <summary>Monsters' projectiles never hurt their own kind (imps can't hurt imps).</summary>
        bool SameSpecies(Collider hitCollider)
        {
            if (owner == null) return false;
            var mine = owner.GetComponent<Enemy>();
            var theirs = hitCollider.GetComponentInParent<Enemy>();
            return mine != null && theirs != null && mine.Def.Id == theirs.Def.Id;
        }

        /// <summary>40 tracers fanned over 90 degrees from the shooter's current view, as in the original BFG.</summary>
        void BfgSpray()
        {
            if (owner == null) return;
            var pc = owner.GetComponent<PlayerController>();
            if (pc == null) return;
            var origin = pc.EyePosition;
            var forward = pc.transform.forward;
            for (int i = 0; i < 40; i++)
            {
                float yaw = -45f + 90f / 40f * i;
                var dir = Quaternion.AngleAxis(yaw, Vector3.up) * forward;
                var target = pc.FindAutoaimTarget(dir, 32f, 3f);
                if (target == null) continue;
                int dmg = 0;
                for (int k = 0; k < 15; k++) dmg += Random.Range(1, 9);
                var tc = target.Center;
                target.Damage(dmg, (tc - origin).normalized, owner);
                Effect.Spawn("fx_bfgspray_", 3, 0.1f, tc);
            }
        }
    }
}
