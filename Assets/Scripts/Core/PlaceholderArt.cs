using System.Collections.Generic;
using UnityEngine;

namespace Doom
{
    /// <summary>Tiny software canvas used to paint placeholder art. Origin is bottom-left.</summary>
    public class PixelCanvas
    {
        public readonly int W, H;
        public readonly Color32[] Px;

        public PixelCanvas(int w, int h, Color32 fill)
        {
            W = w; H = h;
            Px = new Color32[w * h];
            for (int i = 0; i < Px.Length; i++) Px[i] = fill;
        }

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            Px[y * W + x] = c;
        }

        public Color32 Get(int x, int y) => Px[Mathf.Clamp(y, 0, H - 1) * W + Mathf.Clamp(x, 0, W - 1)];

        public void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int j = y; j < y + h; j++)
                for (int i = x; i < x + w; i++)
                    Set(i, j, c);
        }

        public void Frame(int x, int y, int w, int h, Color32 c)
        {
            Rect(x, y, w, 1, c); Rect(x, y + h - 1, w, 1, c);
            Rect(x, y, 1, h, c); Rect(x + w - 1, y, 1, h, c);
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            if (rx <= 0 || ry <= 0) return;
            for (int y = Mathf.FloorToInt(cy - ry); y <= Mathf.CeilToInt(cy + ry); y++)
                for (int x = Mathf.FloorToInt(cx - rx); x <= Mathf.CeilToInt(cx + rx); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }

        public void Line(int x0, int y0, int x1, int y1, Color32 c)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            while (true)
            {
                Set(x0, y0, c);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        public void Noise(System.Random r, int amount)
        {
            for (int i = 0; i < Px.Length; i++)
            {
                var p = Px[i];
                if (p.a == 0) continue;
                int d = r.Next(-amount, amount + 1);
                Px[i] = new Color32((byte)Mathf.Clamp(p.r + d, 0, 255), (byte)Mathf.Clamp(p.g + d, 0, 255),
                    (byte)Mathf.Clamp(p.b + d, 0, 255), p.a);
            }
        }

        public void Tint(Color32 c, float t)
        {
            for (int i = 0; i < Px.Length; i++)
                if (Px[i].a > 0) Px[i] = Color32.Lerp(Px[i], c, t);
        }

        public void Outline(Color32 c)
        {
            var src = (Color32[])Px.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (src[y * W + x].a > 0) continue;
                    bool edge = (x > 0 && src[y * W + x - 1].a > 0) || (x < W - 1 && src[y * W + x + 1].a > 0) ||
                                (y > 0 && src[(y - 1) * W + x].a > 0) || (y < H - 1 && src[(y + 1) * W + x].a > 0);
                    if (edge) Px[y * W + x] = c;
                }
        }

        public Texture2D ToTexture(string name, bool repeat)
        {
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp
            };
            t.SetPixels32(Px);
            t.Apply(false, false);
            return t;
        }
    }

    /// <summary>
    /// Procedural placeholder art. Every name returned by AllTextureNames / AllSpriteNames is an asset
    /// slot that can be replaced by a real file (see GameAssets).
    /// </summary>
    public static class PlaceholderArt
    {
        static readonly Color32 Clear = new(0, 0, 0, 0);
        static readonly Color32 Ink = new(16, 12, 10, 255);
        static readonly Color32 Blood = new(150, 0, 0, 255);
        static readonly Color32 Skin = new(220, 160, 120, 255);
        static readonly Color32 Gun = new(70, 70, 75, 255);
        static readonly Color32 GunLight = new(120, 120, 128, 255);
        static readonly Color32 Flash = new(255, 230, 120, 255);

        static Color32 C(int r, int g, int b, int a = 255) => new((byte)r, (byte)g, (byte)b, (byte)a);

        static System.Random Rng(string name)
        {
            int h = 17;
            foreach (char ch in name) h = h * 31 + ch;
            return new System.Random(h & 0x7fffffff);
        }

        // ------------------------------------------------------------------ names

        public static readonly string[] TextureNames =
        {
            "wall_brick", "wall_stone", "wall_tech", "wall_metal", "door", "door_red", "door_blue", "door_yellow",
            "door_track", "switch_exit", "switch_exit_on", "floor_tile", "floor_stone", "floor_dirt", "ceil_tile",
            "ceil_light", "nukage", "step", "sky", "stbar", "stbar_side", "lift_top", "lift_side", "teleport"
        };

        public static readonly string[] MonsterFrames = { "walk0", "walk1", "attack0", "attack1", "pain", "death0", "death1", "death2" };
        public static readonly string[] WeaponFrames = { "idle", "fire0", "fire1" };
        public static readonly string[] ProjectileFrames = { "fly0", "fly1", "hit0", "hit1", "hit2" };

        public static readonly string[] ItemNames =
        {
            "stimpack", "medikit", "healthbonus", "armorbonus", "greenarmor", "bluearmor", "soulsphere", "backpack",
            "clip", "ammobox", "shells", "shellbox", "rocket", "rocketbox", "cell", "cellpack", "chainsaw", "shotgun",
            "chaingun", "rocketlauncher", "plasmagun", "bfg", "redkey", "bluekey", "yellowkey"
        };

        public static readonly string[] WeaponIds = { "fist", "chainsaw", "pistol", "shotgun", "chaingun", "rocketlauncher", "plasmagun", "bfg" };
        public static readonly string[] ProjectileIds = { "imp_ball", "caco_ball", "baron_ball", "rocket", "plasma", "bfg" };
        public static readonly string[] EffectIds = { "puff", "blood", "explosion", "bfgspray", "tfog" };

        public static IEnumerable<string> AllTextureNames => TextureNames;

        public static IEnumerable<string> AllSpriteNames
        {
            get
            {
                foreach (var m in monsterStyles.Keys)
                    foreach (var f in MonsterFrames) yield return m + "_" + f;
                foreach (var i in ItemNames) yield return "item_" + i;
                foreach (var w in WeaponIds)
                    foreach (var f in WeaponFrames) yield return "weapon_" + w + "_" + f;
                foreach (var p in ProjectileIds)
                    foreach (var f in ProjectileFrames) yield return "proj_" + p + "_" + f;
                foreach (var e in EffectIds)
                    for (int i = 0; i < 3; i++) yield return "fx_" + e + "_" + i;
                yield return "barrel_idle0";
                yield return "barrel_idle1";
                for (int t = 0; t < 5; t++)
                    foreach (var d in new[] { "c", "l", "r" }) yield return $"hud_face_{t}_{d}";
                foreach (var f in new[] { "ouch", "dead", "god", "grin" }) yield return "hud_face_" + f;
                yield return "hud_skull0";
                yield return "hud_skull1";
            }
        }

        // ------------------------------------------------------------------ textures

        public static Texture2D Texture(string name)
        {
            var r = Rng(name);
            PixelCanvas c;
            switch (name)
            {
                case "wall_brick": c = Bricks(r, C(128, 70, 46), C(66, 54, 46), 16, 8, 10); break;
                case "wall_stone": c = Bricks(r, C(104, 104, 98), C(52, 52, 50), 32, 16, 18); break;
                case "wall_tech": c = Tech(r); break;
                case "wall_metal": c = Metal(r, C(84, 90, 98)); break;
                case "door": c = Door(r, null); break;
                case "door_red": c = Door(r, C(210, 30, 30)); break;
                case "door_blue": c = Door(r, C(40, 70, 230)); break;
                case "door_yellow": c = Door(r, C(230, 200, 30)); break;
                case "door_track":
                    c = new PixelCanvas(64, 64, C(40, 40, 40));
                    for (int y = 0; y < 64; y++)
                        for (int x = 0; x < 64; x++)
                            if (((x + y) / 8) % 2 == 0) c.Set(x, y, C(170, 140, 30));
                    c.Noise(r, 8);
                    break;
                case "switch_exit":
                case "switch_exit_on":
                    c = Metal(r, C(84, 90, 98));
                    c.Rect(18, 14, 28, 36, C(30, 30, 32));
                    c.Frame(18, 14, 28, 36, C(150, 150, 150));
                    bool on = name.EndsWith("_on");
                    c.Rect(22, 40, 20, 6, on ? C(40, 220, 40) : C(220, 30, 30));
                    c.Rect(29, on ? 30 : 18, 6, 12, C(200, 200, 200));
                    break;
                case "floor_tile":
                    c = new PixelCanvas(64, 64, C(92, 92, 92));
                    for (int y = 0; y < 64; y++)
                        for (int x = 0; x < 64; x++)
                            if (((x / 16) + (y / 16)) % 2 == 0) c.Set(x, y, C(72, 72, 78));
                    for (int i = 0; i < 64; i += 16) { c.Rect(i, 0, 1, 64, C(50, 50, 50)); c.Rect(0, i, 64, 1, C(50, 50, 50)); }
                    c.Noise(r, 8);
                    break;
                case "floor_stone":
                    c = Bricks(r, C(90, 84, 76), C(50, 46, 42), 32, 32, 14);
                    break;
                case "floor_dirt":
                    c = new PixelCanvas(64, 64, C(96, 76, 50));
                    for (int i = 0; i < 40; i++) c.Ellipse(r.Next(64), r.Next(64), 2 + r.Next(4), 1 + r.Next(3), C(74, 58, 38));
                    c.Noise(r, 16);
                    break;
                case "ceil_tile":
                case "ceil_light":
                    c = new PixelCanvas(64, 64, C(110, 110, 106));
                    for (int i = 0; i < 64; i += 32) { c.Rect(i, 0, 2, 64, C(60, 60, 60)); c.Rect(0, i, 64, 2, C(60, 60, 60)); }
                    if (name == "ceil_light") { c.Rect(14, 14, 36, 36, C(250, 250, 230)); c.Frame(12, 12, 40, 40, C(70, 70, 70)); }
                    c.Noise(r, 6);
                    break;
                case "nukage":
                    c = new PixelCanvas(64, 64, C(40, 150, 40));
                    for (int i = 0; i < 30; i++) c.Ellipse(r.Next(64), r.Next(64), 3 + r.Next(6), 2 + r.Next(4), C(70, 200, 60));
                    for (int i = 0; i < 20; i++) c.Ellipse(r.Next(64), r.Next(64), 1 + r.Next(3), 1 + r.Next(2), C(160, 240, 120));
                    c.Noise(r, 10);
                    break;
                case "step":
                    c = new PixelCanvas(64, 64, C(70, 70, 70));
                    for (int y = 0; y < 64; y += 8) c.Rect(0, y, 64, 2, C(110, 100, 40));
                    c.Noise(r, 10);
                    break;
                case "lift_top":
                    c = new PixelCanvas(64, 64, C(96, 92, 80));
                    for (int y = 4; y < 64; y += 8)
                        for (int x = 4; x < 64; x += 8)
                            c.Rect(x - 1, y - 1, 3, 3, C(130, 124, 108));
                    c.Frame(0, 0, 64, 64, C(170, 140, 30));
                    c.Frame(1, 1, 62, 62, C(170, 140, 30));
                    c.Noise(r, 8);
                    break;
                case "lift_side":
                    c = new PixelCanvas(64, 64, C(60, 58, 52));
                    for (int y = 0; y < 64; y++)
                        for (int x = 0; x < 64; x++)
                            if (y >= 52 && ((x + y) / 6) % 2 == 0) c.Set(x, y, C(200, 170, 30));
                    for (int x = 0; x < 64; x += 16) c.Rect(x, 0, 2, 52, C(40, 38, 34));
                    c.Noise(r, 8);
                    break;
                case "teleport":
                    c = new PixelCanvas(64, 64, C(20, 60, 25));
                    for (int i = 0; i < 4; i++)
                    {
                        float rad = 30 - i * 7;
                        c.Ellipse(32, 32, rad, rad, i % 2 == 0 ? C(60, 220, 90) : C(20, 90, 35));
                    }
                    c.Ellipse(32, 32, 4, 4, C(220, 255, 220));
                    c.Noise(r, 10);
                    break;
                case "sky":
                    c = new PixelCanvas(256, 128, Clear);
                    for (int y = 0; y < 128; y++)
                        c.Rect(0, y, 256, 1, Color32.Lerp(C(200, 90, 40), C(40, 20, 30), y / 127f));
                    for (int x = 0; x < 256; x++)
                    {
                        float m = 30 + 16 * Mathf.Sin(x * 0.049f) + 9 * Mathf.Sin(x * 0.123f + 1.3f) + 4 * Mathf.Sin(x * 0.37f);
                        c.Rect(x, 0, 1, (int)m, C(50, 30, 28));
                    }
                    c.Noise(r, 5);
                    break;
                case "stbar":
                    c = new PixelCanvas(320, 32, C(90, 90, 90));
                    c.Noise(r, 14);
                    foreach (var (x, w) in new[] { (0, 48), (48, 56), (104, 38), (142, 36), (178, 58), (236, 14), (250, 70) })
                    {
                        c.Frame(x + 1, 1, w - 2, 30, C(50, 50, 50));
                        c.Frame(x + 2, 2, w - 4, 28, C(130, 130, 130));
                    }
                    c.Rect(143, 1, 34, 30, C(40, 40, 40));
                    break;
                case "stbar_side":
                    c = Bricks(r, C(80, 76, 70), C(46, 44, 42), 16, 8, 10);
                    c = Crop(c, 32, 32);
                    break;
                default:
                    Debug.LogWarning($"[Doom] No texture or placeholder named '{name}'");
                    c = Missing(64, 64);
                    break;
            }

            return c.ToTexture(name, true);
        }

        static PixelCanvas Crop(PixelCanvas src, int w, int h)
        {
            var c = new PixelCanvas(w, h, Clear);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    c.Set(x, y, src.Get(x, y));
            return c;
        }

        static PixelCanvas Missing(int w, int h)
        {
            var c = new PixelCanvas(w, h, C(255, 0, 255));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (((x / 8) + (y / 8)) % 2 == 0) c.Set(x, y, C(0, 0, 0));
            return c;
        }

        static PixelCanvas Bricks(System.Random r, Color32 brick, Color32 mortar, int bw, int bh, int noise)
        {
            var c = new PixelCanvas(64, 64, mortar);
            for (int row = 0; row < 64 / bh; row++)
            {
                int off = row % 2 == 0 ? 0 : bw / 2;
                for (int col = -1; col <= 64 / bw; col++)
                {
                    int shade = r.Next(-14, 15);
                    var b = C(Mathf.Clamp(brick.r + shade, 0, 255), Mathf.Clamp(brick.g + shade, 0, 255), Mathf.Clamp(brick.b + shade, 0, 255));
                    c.Rect(col * bw + off + 1, row * bh + 1, bw - 1, bh - 1, b);
                }
            }
            c.Noise(r, noise);
            return c;
        }

        static PixelCanvas Tech(System.Random r)
        {
            var c = new PixelCanvas(64, 64, C(112, 112, 118));
            for (int py = 0; py < 64; py += 32)
                for (int px = 0; px < 64; px += 32)
                {
                    c.Frame(px, py, 32, 32, C(60, 60, 66));
                    c.Frame(px + 1, py + 1, 30, 30, C(150, 150, 156));
                    c.Rect(px + 6, py + 8, 20, 4, C(40, 40, 44));
                    for (int i = 0; i < 4; i++)
                        c.Rect(px + 7 + i * 5, py + 9, 2, 2, r.Next(3) == 0 ? C(220, 40, 40) : C(40, 220, 60));
                    c.Rect(px + 6, py + 18, 20, 6, C(80, 80, 86));
                }
            c.Noise(r, 6);
            return c;
        }

        static PixelCanvas Metal(System.Random r, Color32 baseColor)
        {
            var c = new PixelCanvas(64, 64, baseColor);
            for (int x = 0; x < 64; x += 16) c.Rect(x, 0, 1, 64, C(50, 52, 58));
            for (int x = 4; x < 64; x += 16)
                for (int y = 4; y < 64; y += 14)
                    c.Rect(x, y, 2, 2, C(150, 150, 160));
            c.Noise(r, 9);
            return c;
        }

        static PixelCanvas Door(System.Random r, Color32? stripe)
        {
            var c = new PixelCanvas(64, 64, C(132, 126, 112));
            for (int y = 0; y < 64; y += 8) c.Rect(0, y, 64, 2, C(92, 88, 78));
            c.Rect(31, 0, 2, 64, C(60, 58, 52));
            c.Rect(26, 26, 12, 12, C(80, 80, 80));
            if (stripe.HasValue)
            {
                c.Rect(0, 44, 64, 8, stripe.Value);
                c.Rect(0, 12, 64, 8, stripe.Value);
            }
            c.Noise(r, 8);
            return c;
        }

        // ------------------------------------------------------------------ sprites

        class MonsterStyle
        {
            public string Shape;
            public int W, H;
            public Color32 Body, Legs, Head, Eyes;
            public bool Spikes, Horns, Gun;
        }

        static readonly Dictionary<string, MonsterStyle> monsterStyles = new()
        {
            { "zombieman",  new MonsterStyle { Shape = "human", W = 40, H = 56, Body = C(90, 110, 60), Legs = C(70, 70, 60), Head = C(200, 150, 120), Eyes = C(30, 30, 30), Gun = true } },
            { "shotgunguy", new MonsterStyle { Shape = "human", W = 40, H = 56, Body = C(50, 50, 55), Legs = C(80, 40, 40), Head = C(190, 140, 110), Eyes = C(30, 30, 30), Gun = true } },
            { "imp",        new MonsterStyle { Shape = "human", W = 44, H = 56, Body = C(130, 90, 60), Legs = C(110, 75, 50), Head = C(140, 95, 60), Eyes = C(255, 60, 20), Spikes = true } },
            { "baron",      new MonsterStyle { Shape = "human", W = 48, H = 72, Body = C(200, 150, 140), Legs = C(110, 70, 50), Head = C(205, 150, 140), Eyes = C(40, 200, 40), Horns = true } },
            { "demon",      new MonsterStyle { Shape = "beast", W = 60, H = 56, Body = C(215, 120, 140), Legs = C(170, 90, 105), Head = C(215, 120, 140), Eyes = C(20, 20, 20) } },
            { "cacodemon",  new MonsterStyle { Shape = "blob",  W = 64, H = 64, Body = C(200, 40, 40), Legs = C(150, 30, 30), Head = C(200, 40, 40), Eyes = C(60, 230, 60) } },
            { "lostsoul",   new MonsterStyle { Shape = "skull", W = 36, H = 44, Body = C(225, 220, 200), Legs = C(255, 150, 30), Head = C(225, 220, 200), Eyes = C(10, 10, 10) } },
        };

        public static IEnumerable<string> MonsterIds => monsterStyles.Keys;

        public static Texture2D Sprite(string name)
        {
            var r = Rng(name);
            PixelCanvas c = null;
            int us = name.LastIndexOf('_');
            string head = us > 0 ? name.Substring(0, us) : name;
            string tail = us > 0 ? name.Substring(us + 1) : "";

            if (monsterStyles.TryGetValue(head, out var style)) c = Monster(style, tail, r);
            else if (name.StartsWith("item_")) c = Item(name.Substring(5), r);
            else if (name.StartsWith("weapon_")) c = Weapon(head.Substring(7), tail, r);
            else if (name.StartsWith("proj_")) c = Projectile(head.Substring(5), tail);
            else if (name.StartsWith("fx_")) c = Effect(head.Substring(3), tail);
            else if (name.StartsWith("barrel_")) c = Barrel(tail);
            else if (name.StartsWith("hud_face_")) c = Face(name.Substring(9));
            else if (name.StartsWith("hud_skull")) c = Skull(name.EndsWith("1"));

            if (c == null)
            {
                Debug.LogWarning($"[Doom] No sprite or placeholder named '{name}'");
                c = Missing(32, 32);
            }

            return c.ToTexture(name, false);
        }

        static PixelCanvas Monster(MonsterStyle s, string frame, System.Random r)
        {
            var c = new PixelCanvas(s.W, s.H, Clear);
            switch (s.Shape)
            {
                case "human": Humanoid(c, s, frame, r); break;
                case "beast": Beast(c, s, frame, r); break;
                case "blob": Blob(c, s, frame, r); break;
                case "skull": SkullMonster(c, s, frame, r); break;
            }
            if (frame == "pain") c.Tint(C(255, 40, 40), 0.45f);
            c.Outline(Ink);
            c.Noise(r, 8);
            return c;
        }

        static void Gore(PixelCanvas c, System.Random r, int count, int maxY)
        {
            for (int i = 0; i < count; i++) c.Ellipse(r.Next(c.W), r.Next(Mathf.Max(1, maxY)), 1 + r.Next(3), 1 + r.Next(2), Blood);
        }

        static void Corpse(PixelCanvas c, MonsterStyle s, System.Random r)
        {
            int cx = c.W / 2;
            c.Ellipse(cx, 3, c.W * 0.45f, 3, Blood);
            c.Ellipse(cx - 2, 6, c.W * 0.32f, 4, s.Body);
            c.Ellipse(cx + c.W * 0.28f, 6, 4, 3.5f, s.Head);
            Gore(c, r, 6, 8);
        }

        static void Humanoid(PixelCanvas c, MonsterStyle s, string frame, System.Random r)
        {
            if (frame == "death2") { Corpse(c, s, r); return; }
            float squash = frame == "death0" ? 0.72f : frame == "death1" ? 0.42f : 1f;
            int w = c.W, h = c.H, cx = w / 2;
            int legH = (int)(h * 0.38f * squash), torsoH = (int)(h * 0.34f * squash);
            float headR = w * 0.15f * Mathf.Max(squash, 0.7f);
            int legW = Mathf.Max(3, w / 7);
            int liftL = frame == "walk1" ? 3 : 0, liftR = frame == "walk0" ? 3 : 0;

            c.Rect(cx - legW - 1, liftL, legW, legH - liftL, s.Legs);
            c.Rect(cx + 1, liftR, legW, legH - liftR, s.Legs);
            int ty = legH;
            c.Rect(cx - w / 4, ty, w / 2, torsoH, s.Body);

            bool attack = frame.StartsWith("attack");
            if (attack)
            {
                c.Rect(cx - w / 4 - 2, ty + torsoH - 9, w / 2 + 4, 5, s.Body);
                if (s.Gun) c.Rect(cx - 2, ty + torsoH - 12, 5, 10, Gun);
            }
            else
            {
                c.Rect(cx - w / 4 - legW, ty + 3, legW, torsoH - 3, s.Body);
                c.Rect(cx + w / 4, ty + 3, legW, torsoH - 3, s.Body);
                if (s.Gun) c.Rect(cx + w / 4, ty, 3, 12, Gun);
            }

            float hy = ty + torsoH + headR;
            c.Ellipse(cx, hy, headR, headR * 1.1f, s.Head);
            c.Set(cx - 2, (int)hy + 1, s.Eyes); c.Set(cx + 2, (int)hy + 1, s.Eyes);
            c.Set(cx - 3, (int)hy + 1, s.Eyes); c.Set(cx + 3, (int)hy + 1, s.Eyes);

            if (s.Spikes)
            {
                for (int i = 0; i < 3; i++)
                {
                    c.Line(cx - w / 4 + i * 2, ty + torsoH, cx - w / 4 - 2 + i * 2, ty + torsoH + 4, C(230, 220, 200));
                    c.Line(cx + w / 4 - i * 2, ty + torsoH, cx + w / 4 + 2 - i * 2, ty + torsoH + 4, C(230, 220, 200));
                }
            }
            if (s.Horns)
            {
                c.Line(cx - (int)headR, (int)(hy + headR * 0.6f), cx - (int)headR - 3, (int)(hy + headR + 4), C(240, 230, 210));
                c.Line(cx + (int)headR, (int)(hy + headR * 0.6f), cx + (int)headR + 3, (int)(hy + headR + 4), C(240, 230, 210));
            }

            if (frame == "attack1")
            {
                if (s.Gun) c.Ellipse(cx, ty + torsoH, 5, 5, Flash);
                else c.Ellipse(cx + w / 4, ty + torsoH + 2, 6, 6, s.Eyes.g > s.Eyes.r ? C(80, 255, 80) : C(255, 140, 40));
            }

            if (squash < 1f) Gore(c, r, frame == "death1" ? 10 : 5, legH + torsoH);
        }

        static void Beast(PixelCanvas c, MonsterStyle s, string frame, System.Random r)
        {
            if (frame == "death2") { Corpse(c, s, r); return; }
            float squash = frame == "death0" ? 0.7f : frame == "death1" ? 0.4f : 1f;
            int w = c.W, cx = w / 2;
            int lift0 = frame == "walk0" ? 3 : 0, lift1 = frame == "walk1" ? 3 : 0;
            int legH = (int)(14 * squash);
            c.Rect(cx - 18, lift0, 7, legH, s.Legs);
            c.Rect(cx + 11, lift1, 7, legH, s.Legs);
            c.Rect(cx - 8, lift1, 6, legH - 2, s.Legs);
            c.Rect(cx + 2, lift0, 6, legH - 2, s.Legs);
            float by = legH + 14 * squash;
            c.Ellipse(cx, by, 26, 16 * squash, s.Body);
            bool open = frame.StartsWith("attack");
            float mouthRy = (frame == "attack1" ? 10 : open ? 7 : 4) * squash;
            c.Ellipse(cx, by - 2, 14, mouthRy, C(90, 10, 20));
            for (int x = cx - 12; x <= cx + 12; x += 3)
            {
                c.Set(x, (int)(by - 2 + mouthRy - 1), C(250, 250, 240));
                c.Set(x, (int)(by - 2 - mouthRy + 1), C(250, 250, 240));
            }
            c.Ellipse(cx - 7, by + 9 * squash, 2, 2, s.Eyes);
            c.Ellipse(cx + 7, by + 9 * squash, 2, 2, s.Eyes);
            if (squash < 1f) Gore(c, r, 10, (int)by);
        }

        static void Blob(PixelCanvas c, MonsterStyle s, string frame, System.Random r)
        {
            int w = c.W, cx = w / 2;
            if (frame == "death2") { Corpse(c, s, r); return; }
            float sag = frame == "death0" ? 8 : frame == "death1" ? 18 : 0;
            float cy = 36 - sag, ry = 25 - sag * 0.6f;
            c.Ellipse(cx, cy, 26, ry, s.Body);
            for (int i = 0; i < 6; i++) c.Ellipse(cx - 20 + i * 8, cy + ry - 2, 2, 3, C(240, 220, 200));
            if (frame != "pain" && sag == 0)
            {
                c.Ellipse(cx, cy + 6, 8, 7, C(240, 240, 220));
                c.Ellipse(cx, cy + 6, 5, 5, s.Eyes);
                c.Ellipse(cx, cy + 6, 2, 2, Ink);
            }
            else c.Rect(cx - 7, (int)cy + 5, 14, 2, Ink);
            float mouth = frame == "attack1" ? 10 : frame == "attack0" ? 7 : 4;
            c.Ellipse(cx, cy - 10, 15, mouth * (ry / 25f), C(80, 10, 50));
            if (frame == "attack1") c.Ellipse(cx, cy - 10, 6, 4, C(255, 80, 220));
            if (sag > 0) Gore(c, r, 12, (int)cy);
        }

        static void SkullMonster(PixelCanvas c, MonsterStyle s, string frame, System.Random r)
        {
            int cx = c.W / 2;
            if (frame.StartsWith("death"))
            {
                int k = frame[5] - '0';
                c.Ellipse(cx, 20, 10 + k * 5, 10 + k * 5, k == 2 ? C(120, 40, 10) : C(255, 160, 40));
                c.Ellipse(cx, 20, 6 + k * 3, 6 + k * 3, C(255, 240, 160));
                return;
            }
            bool attack = frame.StartsWith("attack");
            int flicker = frame.EndsWith("1") ? 3 : 0;
            c.Ellipse(cx, 18 + flicker, attack ? 17 : 14, attack ? 21 : 17, s.Legs);
            c.Ellipse(cx, 16, attack ? 12 : 10, 13, C(255, 220, 80));
            c.Ellipse(cx, 18, 11, 12, s.Body);
            c.Rect(cx - 6, 8, 12, 5, s.Body);
            c.Ellipse(cx - 4, 20, 3, 3.5f, s.Eyes);
            c.Ellipse(cx + 4, 20, 3, 3.5f, s.Eyes);
            for (int x = cx - 5; x <= cx + 5; x += 2) c.Set(x, 9, Ink);
        }

        static PixelCanvas Barrel(string frame)
        {
            var c = new PixelCanvas(24, 32, Clear);
            c.Ellipse(12, 16, 11, 15, C(70, 110, 70));
            c.Rect(1, 6, 22, 20, C(70, 110, 70));
            c.Rect(1, 8, 22, 2, C(40, 60, 40));
            c.Rect(1, 22, 22, 2, C(40, 60, 40));
            c.Ellipse(12, 29, 9, 2.5f, frame.EndsWith("1") ? C(120, 255, 90) : C(80, 220, 60));
            c.Outline(Ink);
            return c;
        }

        static PixelCanvas Item(string id, System.Random r)
        {
            PixelCanvas c;
            switch (id)
            {
                case "stimpack":
                    c = new PixelCanvas(14, 16, Clear);
                    c.Rect(1, 1, 12, 12, C(235, 235, 235)); c.Rect(6, 3, 2, 8, C(220, 20, 20)); c.Rect(3, 6, 8, 2, C(220, 20, 20));
                    break;
                case "medikit":
                    c = new PixelCanvas(28, 20, Clear);
                    c.Rect(1, 1, 26, 16, C(235, 235, 235)); c.Rect(12, 3, 4, 12, C(220, 20, 20)); c.Rect(8, 7, 12, 4, C(220, 20, 20));
                    c.Rect(10, 17, 8, 2, C(160, 160, 160));
                    break;
                case "healthbonus":
                    c = new PixelCanvas(14, 18, Clear);
                    c.Ellipse(7, 6, 6, 6, C(60, 100, 255)); c.Rect(5, 10, 4, 6, C(60, 100, 255)); c.Rect(4, 16, 6, 2, C(200, 200, 200));
                    break;
                case "armorbonus":
                    c = new PixelCanvas(16, 14, Clear);
                    c.Ellipse(8, 4, 7, 9, C(60, 180, 60)); c.Rect(0, 0, 16, 3, Clear); c.Rect(3, 4, 10, 2, C(30, 90, 30));
                    break;
                case "greenarmor":
                case "bluearmor":
                    c = new PixelCanvas(32, 18, Clear);
                    var col = id == "greenarmor" ? C(50, 170, 50) : C(50, 90, 230);
                    c.Rect(4, 1, 24, 14, col); c.Rect(0, 9, 32, 6, col); c.Rect(12, 11, 8, 6, Clear);
                    c.Rect(6, 3, 20, 2, C(30, 30, 30));
                    break;
                case "soulsphere":
                    c = new PixelCanvas(26, 26, Clear);
                    c.Ellipse(13, 13, 12, 12, C(40, 80, 255)); c.Ellipse(13, 13, 8, 9, C(120, 170, 255));
                    c.Ellipse(9, 15, 2, 2, Ink); c.Ellipse(17, 15, 2, 2, Ink); c.Rect(10, 8, 6, 2, Ink);
                    break;
                case "backpack":
                    c = new PixelCanvas(22, 22, Clear);
                    c.Rect(2, 1, 18, 17, C(120, 90, 50)); c.Rect(4, 10, 14, 6, C(100, 72, 40)); c.Rect(8, 18, 6, 3, C(80, 60, 30));
                    break;
                case "clip":
                    c = new PixelCanvas(10, 14, Clear);
                    c.Rect(2, 0, 6, 12, C(70, 70, 70)); c.Rect(3, 12, 4, 2, C(220, 180, 60));
                    break;
                case "ammobox":
                    c = new PixelCanvas(28, 16, Clear);
                    c.Rect(1, 0, 26, 12, C(70, 90, 50)); for (int x = 3; x < 26; x += 3) c.Rect(x, 12, 2, 3, C(220, 180, 60));
                    break;
                case "shells":
                    c = new PixelCanvas(16, 8, Clear);
                    for (int i = 0; i < 4; i++) { c.Rect(i * 4, 0, 3, 6, C(200, 30, 30)); c.Rect(i * 4, 0, 3, 2, C(220, 180, 60)); }
                    break;
                case "shellbox":
                    c = new PixelCanvas(32, 14, Clear);
                    c.Rect(1, 0, 30, 12, C(170, 40, 30)); c.Rect(4, 4, 24, 4, C(230, 200, 60));
                    break;
                case "rocket":
                    c = new PixelCanvas(8, 28, Clear);
                    c.Rect(2, 0, 4, 22, C(130, 130, 120)); c.Ellipse(4, 23, 2.5f, 4, C(200, 60, 40)); c.Rect(0, 0, 8, 4, C(90, 90, 90));
                    break;
                case "rocketbox":
                    c = new PixelCanvas(30, 20, Clear);
                    c.Rect(1, 0, 28, 14, C(110, 80, 50)); for (int x = 4; x < 28; x += 6) { c.Rect(x, 14, 4, 4, C(130, 130, 120)); c.Rect(x, 18, 4, 2, C(200, 60, 40)); }
                    break;
                case "cell":
                    c = new PixelCanvas(16, 12, Clear);
                    c.Rect(1, 0, 14, 10, C(60, 60, 70)); c.Rect(3, 3, 10, 4, C(80, 230, 80));
                    break;
                case "cellpack":
                    c = new PixelCanvas(30, 20, Clear);
                    c.Rect(1, 0, 28, 18, C(60, 60, 70)); c.Rect(4, 4, 22, 10, C(80, 230, 80)); c.Rect(10, 18, 10, 2, C(90, 90, 90));
                    break;
                case "chainsaw":
                    c = new PixelCanvas(60, 20, Clear);
                    c.Rect(0, 4, 20, 14, C(200, 60, 30)); c.Rect(20, 7, 38, 6, C(180, 180, 180));
                    for (int x = 20; x < 58; x += 3) c.Set(x, 13, Ink);
                    break;
                case "shotgun":
                    c = new PixelCanvas(62, 12, Clear);
                    c.Rect(0, 2, 20, 8, C(120, 80, 40)); c.Rect(20, 5, 42, 4, Gun); c.Rect(28, 2, 14, 3, C(120, 80, 40));
                    break;
                case "chaingun":
                    c = new PixelCanvas(54, 16, Clear);
                    c.Rect(0, 3, 22, 11, Gun); c.Rect(22, 3, 32, 3, GunLight); c.Rect(22, 7, 32, 3, GunLight); c.Rect(22, 11, 32, 3, GunLight);
                    break;
                case "rocketlauncher":
                    c = new PixelCanvas(62, 16, Clear);
                    c.Rect(0, 2, 62, 12, C(80, 100, 80)); c.Rect(54, 4, 8, 8, Ink); c.Rect(14, 0, 10, 4, Gun);
                    break;
                case "plasmagun":
                    c = new PixelCanvas(54, 16, Clear);
                    c.Rect(0, 2, 54, 12, C(80, 90, 120)); for (int x = 10; x < 50; x += 8) c.Rect(x, 4, 4, 8, C(100, 160, 255));
                    break;
                case "bfg":
                    c = new PixelCanvas(62, 24, Clear);
                    c.Rect(0, 2, 62, 20, C(70, 110, 70)); c.Ellipse(50, 12, 8, 8, C(140, 255, 140)); c.Rect(6, 6, 30, 3, Gun);
                    break;
                case "redkey":
                case "bluekey":
                case "yellowkey":
                    c = new PixelCanvas(14, 16, Clear);
                    var kc = id == "redkey" ? C(230, 30, 30) : id == "bluekey" ? C(40, 80, 240) : C(240, 210, 30);
                    c.Rect(1, 0, 12, 16, kc); c.Rect(1, 10, 12, 2, C(240, 240, 240)); c.Rect(4, 2, 6, 5, C(40, 40, 40));
                    break;
                default:
                    return null;
            }
            c.Outline(Ink);
            c.Noise(r, 6);
            return c;
        }

        static PixelCanvas Weapon(string id, string frame, System.Random r)
        {
            var c = new PixelCanvas(120, 80, Clear);
            bool f0 = frame == "fire0", f1 = frame == "fire1";
            int cx = 60;
            switch (id)
            {
                case "fist":
                    if (f0) { c.Ellipse(cx, 40, 16, 14, Skin); c.Rect(cx - 10, 0, 20, 30, Skin); }
                    else if (f1) { c.Ellipse(cx, 52, 11, 10, Skin); c.Rect(cx - 7, 0, 14, 44, Skin); }
                    else { c.Ellipse(cx + 34, 20, 16, 14, Skin); c.Rect(cx + 26, 0, 18, 12, Skin); c.Ellipse(cx - 34, 16, 14, 12, Skin); }
                    break;
                case "chainsaw":
                    int jitter = f0 ? 2 : f1 ? -2 : 0;
                    c.Rect(cx - 16, 0, 32, 26, C(200, 60, 30));
                    c.Rect(cx - 6 + jitter, 26, 12, 50, C(180, 180, 180));
                    for (int y = 26; y < 76; y += 4) { c.Set(cx - 7 + jitter, y, Ink); c.Set(cx + 6 + jitter, y + 2, Ink); }
                    if (f0 || f1) for (int i = 0; i < 8; i++) c.Set(cx + r.Next(-12, 12), 70 + r.Next(8), Flash);
                    c.Rect(cx - 22, 0, 10, 14, Skin); c.Rect(cx + 12, 0, 10, 14, Skin);
                    break;
                case "pistol":
                    if (f0) c.Ellipse(cx, 62, 9, 9, Flash);
                    if (f1) c.Ellipse(cx, 60, 5, 5, Flash);
                    c.Rect(cx - 5, 18, 10, 38, Gun); c.Rect(cx - 3, 50, 6, 6, GunLight);
                    c.Rect(cx - 9, 0, 18, 22, Skin);
                    break;
                case "shotgun":
                    if (f0) c.Ellipse(cx, 70, 14, 9, Flash);
                    int pump = f1 ? -8 : 0;
                    c.Rect(cx - 6, 20 + pump, 12, 46, Gun); c.Rect(cx - 4, 22 + pump, 3, 44, GunLight);
                    c.Rect(cx - 9, 20 + pump, 18, 14, C(120, 80, 40));
                    c.Rect(cx - 12, 0, 24, 22, Skin);
                    break;
                case "chaingun":
                    if (f0 || f1) c.Ellipse(cx + (f0 ? -4 : 4), 70, 10, 8, Flash);
                    for (int i = -2; i <= 2; i++) c.Rect(cx + i * 5 - 2, 24, 4, 42, i % 2 == 0 ? GunLight : Gun);
                    c.Rect(cx - 16, 0, 32, 28, Gun);
                    c.Rect(cx - 22, 0, 8, 16, Skin); c.Rect(cx + 14, 0, 8, 16, Skin);
                    break;
                case "rocketlauncher":
                    if (f0) c.Ellipse(cx, 72, 16, 8, C(255, 160, 40));
                    c.Rect(cx - 14, 10, 28, 60, C(80, 100, 80)); c.Ellipse(cx, 66, 9, 6, Ink);
                    c.Rect(cx - 20, 0, 40, 14, Gun);
                    break;
                case "plasmagun":
                    if (f0 || f1) c.Ellipse(cx, 70, 12, 9, f0 ? C(120, 180, 255) : C(200, 230, 255));
                    c.Rect(cx - 12, 14, 24, 52, C(80, 90, 120));
                    for (int y = 20; y < 62; y += 9) c.Rect(cx - 9, y, 18, 4, C(100, 160, 255));
                    c.Rect(cx - 18, 0, 36, 18, Gun);
                    break;
                case "bfg":
                    if (f0 || f1) c.Ellipse(cx, 68, f0 ? 18 : 24, 10, C(140, 255, 140));
                    c.Rect(cx - 22, 8, 44, 56, C(70, 110, 70)); c.Ellipse(cx, 58, 12, 8, C(30, 60, 30));
                    c.Rect(cx - 26, 0, 52, 14, Gun);
                    break;
                default:
                    return null;
            }
            c.Outline(Ink);
            c.Noise(r, 6);
            return c;
        }

        static PixelCanvas Projectile(string id, string frame)
        {
            Color32 col = id switch
            {
                "imp_ball" => C(255, 130, 30),
                "caco_ball" => C(255, 60, 210),
                "baron_ball" => C(70, 255, 70),
                "rocket" => C(255, 170, 60),
                "plasma" => C(90, 150, 255),
                _ => C(120, 255, 120)
            };
            int size = id == "bfg" ? 40 : 18;
            var c = new PixelCanvas(size, size, Clear);
            float m = size / 2f;
            if (frame.StartsWith("hit"))
            {
                int k = frame[3] - '0';
                float rad = m * (0.45f + 0.27f * k);
                c.Ellipse(m, m, rad, rad, Color32.Lerp(col, C(60, 20, 10), k * 0.3f));
                c.Ellipse(m, m, rad * 0.55f, rad * 0.55f, Color32.Lerp(C(255, 255, 220), col, k * 0.4f));
                if (k == 2) c.Ellipse(m, m, rad * 0.35f, rad * 0.35f, Clear);
                return c;
            }
            bool alt = frame == "fly1";
            if (id == "rocket")
            {
                c.Ellipse(m, m, 5, 5, C(150, 150, 140));
                c.Ellipse(m, m, alt ? 3 : 2, alt ? 3 : 2, col);
                return c;
            }
            float outer = m * (alt ? 0.9f : 0.75f);
            c.Ellipse(m, m, outer, outer, col);
            c.Ellipse(m, m, outer * 0.5f, outer * 0.5f, C(255, 255, 230));
            return c;
        }

        static PixelCanvas Effect(string id, string frame)
        {
            int k = Mathf.Clamp(frame.Length > 0 ? frame[0] - '0' : 0, 0, 2);
            PixelCanvas c;
            switch (id)
            {
                case "puff":
                    c = new PixelCanvas(12, 12, Clear);
                    c.Ellipse(6, 6, 5 - k, 5 - k, k == 0 ? C(255, 240, 200) : C(170, 170, 170));
                    break;
                case "blood":
                    c = new PixelCanvas(12, 12, Clear);
                    c.Ellipse(6, 6 - k, 4 - k, 4 - k, Blood);
                    c.Set(3, 8 - k * 2, Blood); c.Set(9, 7 - k * 2, Blood);
                    break;
                case "explosion":
                    c = new PixelCanvas(48, 48, Clear);
                    float rad = 12 + k * 7;
                    c.Ellipse(24, 24, rad, rad, k == 2 ? C(120, 40, 10) : C(255, 110, 20));
                    c.Ellipse(24, 24, rad * 0.6f, rad * 0.6f, k == 2 ? C(200, 90, 20) : C(255, 230, 120));
                    break;
                case "tfog":
                    c = new PixelCanvas(40, 56, Clear);
                    for (int i = 0; i < 12; i++)
                    {
                        float t = i / 11f;
                        c.Ellipse(20 + Mathf.Sin(t * 9f + k) * (8 + k * 3), 6 + t * 46, 3 + k, 3 + k,
                            Color32.Lerp(C(120, 255, 150), C(240, 255, 240), t));
                    }
                    break;
                case "bfgspray":
                    c = new PixelCanvas(24, 24, Clear);
                    c.Ellipse(12, 12, 6 + k * 3, 6 + k * 3, C(120, 255, 120));
                    c.Ellipse(12, 12, 3 + k, 3 + k, C(230, 255, 230));
                    break;
                default:
                    return null;
            }
            return c;
        }

        static PixelCanvas Face(string state)
        {
            var c = new PixelCanvas(24, 30, Clear);
            int tier = 0; char look = 'c';
            if (state.Length >= 3 && char.IsDigit(state[0])) { tier = state[0] - '0'; look = state[2]; }
            bool dead = state == "dead", ouch = state == "ouch", god = state == "god", grin = state == "grin";

            var skin = dead ? C(150, 130, 120) : Skin;
            c.Ellipse(12, 14, 10, 13, skin);
            c.Rect(3, 22, 18, 6, C(90, 60, 30));
            int eyeShift = look == 'l' ? -2 : look == 'r' ? 2 : 0;
            var eye = god ? C(255, 220, 40) : C(40, 40, 40);
            if (dead)
            {
                c.Line(6, 18, 9, 15, Ink); c.Line(6, 15, 9, 18, Ink);
                c.Line(15, 18, 18, 15, Ink); c.Line(15, 15, 18, 18, Ink);
            }
            else
            {
                c.Rect(6 + eyeShift, 15, 4, 3, C(255, 255, 255)); c.Rect(14 + eyeShift, 15, 4, 3, C(255, 255, 255));
                c.Rect(7 + eyeShift * 3 / 2, 15, 2, 2, eye); c.Rect(15 + eyeShift * 3 / 2, 15, 2, 2, eye);
            }
            if (ouch) c.Ellipse(12, 7, 4, 4, C(80, 10, 10));
            else if (grin) { c.Rect(7, 7, 10, 3, C(250, 250, 250)); c.Rect(7, 6, 10, 1, Ink); }
            else c.Rect(8, 7, 8, 2, C(120, 50, 40));
            var rng = new System.Random(tier * 7 + 3);
            int bloodSpots = dead ? 14 : tier * 3;
            for (int i = 0; i < bloodSpots; i++) c.Ellipse(4 + rng.Next(16), 3 + rng.Next(20), 1 + rng.Next(2), 1, Blood);
            c.Outline(Ink);
            return c;
        }

        static PixelCanvas Skull(bool alt)
        {
            var c = new PixelCanvas(16, 18, Clear);
            c.Ellipse(8, 10, 7, 7, C(230, 225, 205));
            c.Rect(4, 2, 8, 5, C(230, 225, 205));
            var eye = alt ? C(255, 40, 20) : Ink;
            c.Ellipse(5, 10, 2, 2.2f, eye); c.Ellipse(11, 10, 2, 2.2f, eye);
            for (int x = 4; x < 12; x += 2) c.Set(x, 3, Ink);
            c.Outline(Ink);
            return c;
        }
    }
}
