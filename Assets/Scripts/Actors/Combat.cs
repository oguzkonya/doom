using System.Collections.Generic;
using UnityEngine;

namespace Doom
{
    public interface IDamageable
    {
        bool IsDead { get; }
        bool Bleeds { get; }
        /// <param name="direction">Direction the damage travelled (used for knockback / face direction).</param>
        /// <param name="source">The actor responsible (player, monster), used for infighting.</param>
        void Damage(int amount, Vector3 direction, GameObject source);
    }

    public static class Combat
    {
        static readonly RaycastHit[] hits = new RaycastHit[32];
        static readonly Collider[] overlap = new Collider[64];

        /// <summary>Doom-style damage roll: (1..sides) * multiplier.</summary>
        public static int Roll(int sides, int multiplier) => Random.Range(1, sides + 1) * multiplier;

        /// <summary>Triangular random spread in degrees, like (P_Random() - P_Random()).</summary>
        public static float Spread(float maxDegrees) => (Random.value - Random.value) * maxDegrees;

        public static bool IsPartOf(Component c, GameObject root) =>
            root != null && (c.gameObject == root || c.transform.IsChildOf(root.transform));

        /// <summary>Nearest non-trigger hit that does not belong to <paramref name="ignore"/>.</summary>
        public static bool Trace(Vector3 origin, Vector3 dir, float range, GameObject ignore, int mask, out RaycastHit hit)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, hits, range, mask, QueryTriggerInteraction.Ignore);
            return Nearest(n, ignore, out hit);
        }

        public static bool SphereTrace(Vector3 origin, float radius, Vector3 dir, float range, GameObject ignore, int mask, out RaycastHit hit)
        {
            int n = Physics.SphereCastNonAlloc(origin, radius, dir, hits, range, mask, QueryTriggerInteraction.Ignore);
            return Nearest(n, ignore, out hit);
        }

        static bool Nearest(int n, GameObject ignore, out RaycastHit hit)
        {
            hit = default;
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (IsPartOf(h.collider, ignore)) continue;
                // Sphere casts that start overlapping report distance 0 and no point; skip those for walls.
                if (h.distance <= 0f && h.collider.gameObject.layer == Layers.World) continue;
                if (h.distance < best) { best = h.distance; hit = h; found = true; }
            }
            return found;
        }

        public static bool LineOfSight(Vector3 from, Vector3 to) =>
            !Physics.Linecast(from, to, Layers.WorldMask, QueryTriggerInteraction.Ignore);

        /// <summary>Hitscan bullet: damages the first thing hit and spawns a puff or blood.</summary>
        public static void FireBullet(Vector3 origin, Vector3 dir, float range, int damage, GameObject source)
        {
            if (!Trace(origin, dir, range, source, Layers.ShotMask, out var hit)) return;
            var target = hit.collider.GetComponentInParent<IDamageable>();
            var fxPos = hit.point - dir * 0.1f;
            if (target != null && !target.IsDead)
            {
                target.Damage(damage, dir, source);
                if (target.Bleeds) Effect.Spawn("fx_blood_", 3, 0.1f, fxPos, false, -0.5f);
                else Effect.Spawn("fx_puff_", 3, 0.08f, fxPos, true, 0.5f);
            }
            else Effect.Spawn("fx_puff_", 3, 0.08f, fxPos, true, 0.5f);
        }

        /// <summary>Melee / chainsaw: short trace, returns whether something damageable was hit.</summary>
        public static bool Melee(Vector3 origin, Vector3 dir, float range, int damage, GameObject source)
        {
            if (!Trace(origin, dir, range, source, Layers.ShotMask, out var hit)) return false;
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead)
            {
                Effect.Spawn("fx_puff_", 3, 0.08f, hit.point - dir * 0.1f, true, 0.5f);
                return false;
            }
            target.Damage(damage, dir, source);
            if (target.Bleeds) Effect.Spawn("fx_blood_", 3, 0.1f, hit.point - dir * 0.1f, false, -0.5f);
            return true;
        }

        /// <summary>Splash damage falling off linearly with distance, blocked by walls.</summary>
        public static void RadiusDamage(Vector3 center, float radius, int maxDamage, GameObject source)
        {
            int n = Physics.OverlapSphereNonAlloc(center, radius, overlap, Layers.ActorMask, QueryTriggerInteraction.Ignore);
            var done = new HashSet<IDamageable>();
            for (int i = 0; i < n; i++)
            {
                var col = overlap[i];
                var target = col.GetComponentInParent<IDamageable>();
                if (target == null || target.IsDead || !done.Add(target)) continue;
                var closest = col.ClosestPoint(center);
                float dist = Vector3.Distance(center, closest);
                int dmg = Mathf.RoundToInt(maxDamage * (1f - dist / radius));
                if (dmg <= 0) continue;
                var targetCenter = col.bounds.center;
                if (!LineOfSight(center, targetCenter)) continue;
                var dir = targetCenter - center;
                dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.up;
                target.Damage(dmg, dir, source);
            }
        }
    }
}
