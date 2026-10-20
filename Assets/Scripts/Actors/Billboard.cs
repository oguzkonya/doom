using UnityEngine;

namespace Doom
{
    /// <summary>
    /// Camera-facing (Y axis only) sprite quad. 1 texture pixel = 1/32 m, matching the original's
    /// map-unit scale, so real Doom-sized sprites drop in at the right size.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Billboard : MonoBehaviour
    {
        public const float PixelsToMeters = 1f / 32f;

        static Mesh bottomQuad, centerQuad;
        static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        static readonly int LightId = Shader.PropertyToID("_Light");
        static readonly int FullbrightId = Shader.PropertyToID("_Fullbright");
        static readonly int FuzzId = Shader.PropertyToID("_Fuzz");

        public bool Fullbright;
        public bool Fuzz;
        public float Scale = 1f;

        MeshRenderer meshRenderer;
        MaterialPropertyBlock block;
        Texture2D current;
        float lightTimer;

        public static Billboard Create(Transform parent, bool centered, bool fullbright = false)
        {
            var go = new GameObject("Sprite");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = centered ? CenterQuad : BottomQuad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GameAssets.SpriteMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var bb = go.AddComponent<Billboard>();
            bb.meshRenderer = mr;
            bb.block = new MaterialPropertyBlock();
            bb.Fullbright = fullbright;
            return bb;
        }

        static Mesh BottomQuad => bottomQuad ? bottomQuad : (bottomQuad = MakeQuad(0f));
        static Mesh CenterQuad => centerQuad ? centerQuad : (centerQuad = MakeQuad(-0.5f));

        static Mesh MakeQuad(float yOffset)
        {
            var m = new Mesh { name = "SpriteQuad" };
            m.vertices = new[]
            {
                new Vector3(-0.5f, yOffset, 0), new Vector3(0.5f, yOffset, 0),
                new Vector3(-0.5f, yOffset + 1f, 0), new Vector3(0.5f, yOffset + 1f, 0)
            };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public void SetSprite(string name) => SetSprite(GameAssets.Sprite(name));

        public void SetSprite(Texture2D tex)
        {
            if (tex == current || tex == null) return;
            current = tex;
            transform.localScale = new Vector3(tex.width * PixelsToMeters * Scale, tex.height * PixelsToMeters * Scale, 1f);
            block.SetTexture(MainTexId, tex);
            Apply();
        }

        public void SetVisible(bool visible) => meshRenderer.enabled = visible;

        void Apply()
        {
            block.SetFloat(FullbrightId, Fullbright ? 1f : 0f);
            block.SetFloat(FuzzId, Fuzz ? 1f : 0f);
            var level = GameManager.I != null ? GameManager.I.Level : null;
            block.SetFloat(LightId, level != null ? level.LightAt(transform.position) : 1f);
            meshRenderer.SetPropertyBlock(block);
        }

        void LateUpdate()
        {
            var cam = GameManager.I != null ? GameManager.I.ViewCamera : null;
            if (cam)
            {
                var d = transform.position - cam.transform.position;
                d.y = 0;
                if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
            }

            lightTimer -= Time.deltaTime;
            if (lightTimer <= 0f)
            {
                lightTimer = 0.25f;
                Apply();
            }
        }
    }

    /// <summary>Short-lived animated sprite (puffs, blood, explosions).</summary>
    public class Effect : MonoBehaviour
    {
        string baseName;
        int frames;
        float frameTime, t, rise;
        Billboard sprite;

        public static void Spawn(string baseName, int frames, float frameTime, Vector3 position, bool fullbright = true, float rise = 0f)
        {
            var go = new GameObject("Fx " + baseName);
            go.transform.position = position;
            var parent = GameManager.I != null && GameManager.I.Level != null ? GameManager.I.Level.transform : null;
            if (parent) go.transform.SetParent(parent, true);
            var fx = go.AddComponent<Effect>();
            fx.baseName = baseName;
            fx.frames = frames;
            fx.frameTime = frameTime;
            fx.rise = rise;
            fx.sprite = Billboard.Create(go.transform, true, fullbright);
            fx.sprite.SetSprite(baseName + "0");
        }

        void Update()
        {
            t += Time.deltaTime;
            transform.position += Vector3.up * rise * Time.deltaTime;
            int idx = (int)(t / frameTime);
            if (idx >= frames) { Destroy(gameObject); return; }
            sprite.SetSprite(baseName + idx);
        }
    }
}
