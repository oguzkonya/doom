using System.Collections.Generic;
using UnityEngine;

namespace Doom
{
    /// <summary>
    /// Single entry point for every art/audio asset. Each lookup first tries a real asset in a
    /// Resources folder and falls back to a procedurally generated placeholder, so replacing art
    /// is just dropping a file with the right name into:
    ///   Assets/Resources/Art/Textures/&lt;name&gt;  (walls, floors, ceilings, sky, status bar)
    ///   Assets/Resources/Art/Sprites/&lt;name&gt;   (monsters, items, weapons, effects, HUD)
    ///   Assets/Resources/Audio/&lt;name&gt;         (sound effects)
    ///   Assets/Resources/Music/&lt;name&gt;         (music, optional; no placeholder)
    /// Use the "Doom/Export Placeholder Assets" menu to dump every placeholder with its expected name.
    /// </summary>
    public static class GameAssets
    {
        static readonly Dictionary<string, Texture2D> textures = new();
        static readonly Dictionary<string, Texture2D> sprites = new();
        static readonly Dictionary<string, Texture2D> optionalSprites = new();
        static readonly Dictionary<string, AudioClip> sounds = new();
        static readonly Dictionary<string, Material> worldMaterials = new();
        static Shader unlitShader, skyShader;
        static Material spriteMaterial;

        public static Shader UnlitShader => unlitShader ? unlitShader : (unlitShader = Shader.Find("Doom/Unlit"));
        public static Shader SkyShader => skyShader ? skyShader : (skyShader = Shader.Find("Doom/Sky"));

        public static Texture2D Texture(string name)
        {
            if (textures.TryGetValue(name, out var t) && t) return t;
            t = Resources.Load<Texture2D>("Art/Textures/" + name);
            if (t)
            {
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Repeat;
            }
            else t = PlaceholderArt.Texture(name);
            textures[name] = t;
            return t;
        }

        public static Texture2D Sprite(string name)
        {
            if (sprites.TryGetValue(name, out var t) && t) return t;
            t = Resources.Load<Texture2D>("Art/Sprites/" + name);
            if (t)
            {
                t.filterMode = FilterMode.Point;
                t.wrapMode = TextureWrapMode.Clamp;
            }
            else t = PlaceholderArt.Sprite(name);
            sprites[name] = t;
            return t;
        }

        /// <summary>Sprite that only exists if the user supplied it (e.g. a title picture). May be null.</summary>
        public static Texture2D OptionalSprite(string name)
        {
            if (optionalSprites.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>("Art/Sprites/" + name);
            if (t) t.filterMode = FilterMode.Point;
            optionalSprites[name] = t;
            return t;
        }

        /// <summary>
        /// Sound lookup with an optional fallback name: e.g. "imp_sight" falls back to the generic
        /// "monster_sight" so individual monsters can be given unique sounds later.
        /// </summary>
        public static AudioClip Sound(string name, string fallback = null)
        {
            string key = fallback == null ? name : name + "|" + fallback;
            if (sounds.TryGetValue(key, out var c) && c) return c;
            c = Resources.Load<AudioClip>("Audio/" + name);
            if (!c && fallback != null) c = Resources.Load<AudioClip>("Audio/" + fallback);
            if (!c) c = PlaceholderAudio.Make(fallback ?? name);
            sounds[key] = c;
            return c;
        }

        public static AudioClip Music(string name) =>
            string.IsNullOrEmpty(name) ? null : Resources.Load<AudioClip>("Music/" + name);

        public static Material WorldMaterial(string texture)
        {
            if (worldMaterials.TryGetValue(texture, out var m) && m) return m;
            m = new Material(UnlitShader) { name = "World_" + texture, mainTexture = Texture(texture) };
            m.SetFloat("_Cutoff", 0.01f);
            if (texture == "nukage") m.SetVector("_Scroll", new Vector4(0.05f, 0.03f, 0, 0));
            worldMaterials[texture] = m;
            return m;
        }

        /// <summary>Shared sprite material; per-sprite texture and light go through MaterialPropertyBlocks.</summary>
        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial) return spriteMaterial;
                spriteMaterial = new Material(UnlitShader) { name = "Sprite" };
                spriteMaterial.SetFloat("_Cutoff", 0.5f);
                return spriteMaterial;
            }
        }

        public static Material SkyMaterial(string texture) =>
            new Material(SkyShader) { name = "Sky", mainTexture = Texture(texture) };
    }
}
