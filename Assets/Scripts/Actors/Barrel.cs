using UnityEngine;

namespace Doom
{
    public class Barrel : MonoBehaviour, IDamageable
    {
        int health = 20;
        bool exploding;
        float fuse, anim;
        GameObject killer;
        Billboard sprite;
        Collider body;

        public bool IsDead => exploding;
        public int Health
        {
            get => health;
            set => health = Mathf.Max(1, value);
        }
        public bool Bleeds => false;

        public static Barrel Spawn(Transform parent, Vector3 position)
        {
            var go = new GameObject("Barrel") { layer = Layers.Actor };
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.33f;
            col.height = 1.3f;
            col.center = new Vector3(0, 0.65f, 0);
            var b = go.AddComponent<Barrel>();
            b.body = col;
            b.sprite = Billboard.Create(go.transform, false);
            b.sprite.SetSprite("barrel_idle0");
            return b;
        }

        public void Damage(int amount, Vector3 direction, GameObject source)
        {
            if (exploding) return;
            health -= amount;
            if (health > 0) return;
            exploding = true;
            killer = source;
            fuse = 0.15f;
        }

        void Update()
        {
            anim += Time.deltaTime;
            sprite.SetSprite((int)(anim * 3f) % 2 == 0 ? "barrel_idle0" : "barrel_idle1");
            if (!exploding) return;
            fuse -= Time.deltaTime;
            if (fuse > 0f) return;

            var center = transform.position + Vector3.up * 0.6f;
            body.enabled = false;
            Effect.Spawn("fx_explosion_", 3, 0.12f, center);
            AudioManager.I.PlayAt("explosion", center);
            Combat.RadiusDamage(center, 4f, 128, killer);
            Destroy(gameObject);
        }
    }
}
