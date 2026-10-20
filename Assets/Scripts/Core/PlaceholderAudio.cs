using System.Collections.Generic;
using UnityEngine;

namespace Doom
{
    /// <summary>Synthesizes simple placeholder sound effects (tone sweeps + filtered noise).</summary>
    public static class PlaceholderAudio
    {
        const int Rate = 22050;

        struct Recipe
        {
            public float Duration, Noise, F0, F1, Decay, Square, Lowpass;
            public Recipe(float duration, float noise, float f0, float f1, float decay, float square = 0f, float lowpass = 1f)
            {
                Duration = duration; Noise = noise; F0 = f0; F1 = f1; Decay = decay; Square = square; Lowpass = lowpass;
            }
        }

        static readonly Dictionary<string, Recipe> recipes = new()
        {
            // weapons
            { "pistol",          new Recipe(0.30f, 0.85f, 420, 90, 14f, 0f, 0.5f) },
            { "shotgun",         new Recipe(0.65f, 0.9f, 260, 50, 6f, 0f, 0.35f) },
            { "punch",           new Recipe(0.15f, 0.7f, 140, 60, 25f, 0f, 0.25f) },
            { "saw",             new Recipe(0.25f, 0.3f, 110, 130, 2f, 1f, 0.6f) },
            { "rocket_fire",     new Recipe(0.50f, 0.8f, 200, 80, 4f, 0f, 0.2f) },
            { "explosion",       new Recipe(1.10f, 1f, 80, 30, 3.5f, 0f, 0.12f) },
            { "plasma_fire",     new Recipe(0.20f, 0.1f, 1400, 500, 10f, 0.5f) },
            { "bfg_fire",        new Recipe(0.90f, 0.2f, 200, 1600, 1.5f, 0.6f) },
            { "bfg_explode",     new Recipe(1.20f, 0.6f, 900, 60, 2.5f, 0.3f, 0.3f) },
            { "fireball_hit",    new Recipe(0.45f, 0.9f, 150, 50, 6f, 0f, 0.2f) },
            { "fireball_launch", new Recipe(0.35f, 0.7f, 300, 120, 6f, 0f, 0.3f) },
            // world
            { "door_open",       new Recipe(0.90f, 0.5f, 70, 90, 1.5f, 1f, 0.15f) },
            { "door_close",      new Recipe(0.90f, 0.5f, 90, 60, 1.5f, 1f, 0.15f) },
            { "switch",          new Recipe(0.20f, 0.3f, 900, 400, 18f, 1f) },
            { "noway",           new Recipe(0.25f, 0.2f, 180, 120, 9f, 0.4f, 0.5f) },
            { "secret",          new Recipe(0.60f, 0f, 520, 1040, 3f, 0.3f) },
            // items
            { "item_pickup",     new Recipe(0.12f, 0f, 1200, 1500, 12f, 0.5f) },
            { "weapon_pickup",   new Recipe(0.35f, 0.1f, 500, 900, 5f, 1f) },
            { "powerup",         new Recipe(0.80f, 0f, 300, 1200, 2f, 0.3f) },
            // player
            { "player_pain",     new Recipe(0.35f, 0.3f, 300, 150, 6f, 0.7f, 0.5f) },
            { "player_death",    new Recipe(1.00f, 0.3f, 280, 70, 2.5f, 0.7f, 0.4f) },
            { "player_land",     new Recipe(0.15f, 0.6f, 100, 60, 20f, 0f, 0.2f) },
            // monsters (pitched per monster at play time)
            { "monster_sight",   new Recipe(0.60f, 0.35f, 160, 260, 3f, 0.8f, 0.5f) },
            { "monster_active",  new Recipe(0.50f, 0.4f, 140, 120, 3f, 0.8f, 0.5f) },
            { "monster_pain",    new Recipe(0.35f, 0.4f, 260, 140, 6f, 0.8f, 0.5f) },
            { "monster_death",   new Recipe(0.90f, 0.4f, 220, 50, 2.5f, 0.8f, 0.4f) },
            { "monster_attack",  new Recipe(0.30f, 0.6f, 200, 100, 7f, 0.5f, 0.4f) },
            { "claw",            new Recipe(0.20f, 0.9f, 500, 200, 14f, 0f, 0.6f) },
            { "bite",            new Recipe(0.30f, 0.7f, 180, 80, 9f, 0.6f, 0.4f) },
            // ui
            { "menu_move",       new Recipe(0.06f, 0.5f, 400, 300, 30f, 0f, 0.4f) },
            { "menu_select",     new Recipe(0.25f, 0.85f, 300, 90, 12f, 0f, 0.4f) },
            { "menu_back",       new Recipe(0.12f, 0.3f, 600, 300, 18f, 1f) },
            { "tick",            new Recipe(0.04f, 0.1f, 1500, 1500, 40f, 1f) },
        };

        public static IEnumerable<string> AllNames => recipes.Keys;

        public static AudioClip Make(string name)
        {
            if (!recipes.TryGetValue(name, out var r))
            {
                Debug.LogWarning($"[Doom] No sound or placeholder named '{name}'");
                r = new Recipe(0.1f, 0f, 1000, 1000, 20f);
            }

            int n = Mathf.Max(1, (int)(r.Duration * Rate));
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode() & 0x7fffffff);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float f = Mathf.Lerp(r.F0, r.F1, t / r.Duration);
                phase += 2f * Mathf.PI * f / Rate;
                float sine = Mathf.Sin(phase);
                float tone = Mathf.Lerp(sine, Mathf.Sign(sine), r.Square);
                float white = (float)rng.NextDouble() * 2f - 1f;
                lp += (white - lp) * r.Lowpass;
                float env = Mathf.Exp(-r.Decay * t) * Mathf.Clamp01(t / 0.004f);
                float tail = Mathf.Clamp01((r.Duration - t) / 0.02f);
                data[i] = (tone * (1f - r.Noise) + lp * r.Noise * 1.6f) * env * tail * 0.55f;
            }

            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
