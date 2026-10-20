using UnityEngine;

namespace Doom
{
    /// <summary>
    /// Everything 2D, drawn with IMGUI in a virtual 200-pixel-tall coordinate space (S = screen pixels
    /// per virtual pixel): the low-res 3D view, weapon sprite, status bar, automap, intermission, finale.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public float S => Screen.height / 200f;
        public float FinaleTime { get; private set; }

        static readonly Color DoomRed = new(0.85f, 0.1f, 0.05f);
        static readonly Color DoomYellow = new(1f, 0.85f, 0.2f);
        static readonly Color DoomGrey = new(0.75f, 0.75f, 0.75f);

        GUIStyle style;
        RenderTexture viewTexture;

        LevelResult result;
        float interTime, tickTimer;
        readonly float[] shown = new float[4];
        bool interDone;

        const string FinaleText =
            "With the last of the hellspawn torn apart,\n" +
            "the refinery falls silent.\n\n" +
            "But the gates beyond still burn, and\n" +
            "something far larger is stirring in the dark.\n\n" +
            "Rest while you can, marine.\n" +
            "This was only the beginning.\n\n\n" +
            "THANKS FOR PLAYING THIS PLACEHOLDER BUILD";

        // ------------------------------------------------------------------ camera setup

        void LateUpdate()
        {
            var gm = GameManager.I;
            var player = gm != null ? gm.Player : null;
            if (player == null) return;
            var cam = player.Cam;

            var view = ViewRect01();
            int mode = Settings.RenderMode;
            if (mode == 2)
            {
                cam.targetTexture = null;
                cam.rect = view;
                return;
            }

            int baseHeight = mode == 0 ? 200 : 400;
            int h = Mathf.Max(16, Mathf.RoundToInt(baseHeight * view.height));
            float aspect = Screen.width * view.width / Mathf.Max(1f, Screen.height * view.height);
            int w = Mathf.Max(16, Mathf.RoundToInt(h * aspect));
            if (viewTexture == null || viewTexture.width != w || viewTexture.height != h)
            {
                if (viewTexture != null)
                {
                    cam.targetTexture = null;
                    viewTexture.Release();
                    Destroy(viewTexture);
                }
                viewTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, name = "DoomView" };
                viewTexture.Create();
            }
            cam.rect = new Rect(0, 0, 1, 1);
            cam.targetTexture = viewTexture;
        }

        /// <summary>The 3D view area in normalized viewport coordinates (above the status bar).</summary>
        static Rect ViewRect01() => Settings.HudStyle == 0 ? new Rect(0, 32f / 200f, 1, 168f / 200f) : new Rect(0, 0, 1, 1);

        Rect ViewRectGui() => Settings.HudStyle == 0
            ? new Rect(0, 0, Screen.width, Screen.height - 32f * S)
            : new Rect(0, 0, Screen.width, Screen.height);

        // ------------------------------------------------------------------ drawing helpers

        public void FillRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public void DrawText(Rect r, string text, float size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft,
            bool bold = false, bool shadow = true, bool wrap = false)
        {
            style.fontSize = Mathf.Max(1, Mathf.RoundToInt(size));
            style.alignment = anchor;
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            style.wordWrap = wrap;
            if (shadow)
            {
                float o = Mathf.Max(1f, size * 0.08f);
                style.normal.textColor = new Color(0, 0, 0, color.a * 0.85f);
                GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, style);
            }
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        public void DrawSprite(Rect r, Texture tex, Color tint)
        {
            if (tex == null) return;
            var old = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        /// <summary>Rect in virtual status-bar space (320x200 centered horizontally).</summary>
        Rect V(float x, float y, float w, float h)
        {
            float s = S;
            float x0 = (Screen.width - 320f * s) * 0.5f;
            return new Rect(x0 + x * s, y * s, w * s, h * s);
        }

        // ------------------------------------------------------------------ OnGUI

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            style ??= new GUIStyle();
            var gm = GameManager.I;
            if (gm == null) return;

            switch (gm.State)
            {
                case GameState.Title: DrawTitle(gm); break;
                case GameState.Playing: DrawGame(gm); break;
                case GameState.Intermission: DrawIntermission(); break;
                case GameState.Finale: DrawFinale(); break;
            }

            if (gm.Menu.IsOpen) gm.Menu.Draw(this);
        }

        void DrawTitle(GameManager gm)
        {
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            var pic = GameAssets.OptionalSprite("hud_titlepic");
            if (pic != null)
            {
                FillRect(screen, Color.black);
                GUI.DrawTexture(screen, pic, ScaleMode.ScaleToFit);
            }
            else
            {
                for (int i = 0; i < 20; i++)
                {
                    float t = i / 19f;
                    FillRect(new Rect(0, Screen.height * t, Screen.width, Screen.height / 19f + 1),
                        Color.Lerp(new Color(0.25f, 0.02f, 0.01f), new Color(0.04f, 0.0f, 0.0f), t));
                }
                if (!gm.Menu.IsOpen)
                {
                    DrawText(new Rect(0, 40 * S, Screen.width, 70 * S), "DOOM", 64 * S, DoomRed, TextAnchor.MiddleCenter, true);
                    DrawText(new Rect(0, 105 * S, Screen.width, 12 * S), "UNITY EDITION  -  PLACEHOLDER ART", 7 * S, DoomGrey, TextAnchor.MiddleCenter);
                }
            }
            if (!gm.Menu.IsOpen && (int)(Time.unscaledTime * 2f) % 2 == 0)
                DrawText(new Rect(0, 170 * S, Screen.width, 12 * S), "PRESS ENTER / START", 8 * S, DoomYellow, TextAnchor.MiddleCenter, true);
        }

        void DrawGame(GameManager gm)
        {
            var player = gm.Player;
            if (player == null) return;
            var view = ViewRectGui();

            if (Settings.RenderMode != 2 && viewTexture != null)
                GUI.DrawTexture(view, viewTexture, ScaleMode.StretchToFill, false);

            if (gm.AutomapOpen) DrawAutomap(gm, view);
            else
            {
                DrawWeapon(gm, player, view);
                if (Settings.Crosshair && !player.Status.IsDead)
                {
                    float c = 1.5f * S;
                    FillRect(new Rect(view.center.x - c * 0.5f, view.center.y - c * 0.5f, c, c), new Color(1f, 0.8f, 0.2f, 0.8f));
                }
            }

            var st = player.Status;
            if (st.DamageFlash > 0f) FillRect(view, new Color(1f, 0f, 0f, Mathf.Clamp01(st.DamageFlash) * 0.5f));
            if (st.BonusFlash > 0f) FillRect(view, new Color(1f, 0.85f, 0.3f, st.BonusFlash * 0.25f));
            if (st.IsDead) FillRect(view, new Color(0.5f, 0f, 0f, 0.35f));

            if (gm.MessageTimer > 0f)
                DrawText(new Rect(4 * S, 2 * S, Screen.width - 8 * S, 10 * S), gm.Message, 7 * S,
                    new Color(1f, 0.75f, 0.6f), TextAnchor.UpperLeft);

            if (Settings.HudStyle == 0) DrawStatusBar(player);
            else DrawFullscreenHud(player);

            if (player.Noclip) DrawText(new Rect(0, 12 * S, Screen.width, 8 * S), "NOCLIP", 6 * S, DoomGrey, TextAnchor.UpperCenter);
        }

        void DrawWeapon(GameManager gm, PlayerController player, Rect view)
        {
            var weapons = player.Weapons;
            var tex = GameAssets.Sprite(weapons.SpriteName);
            if (tex == null) return;
            float s = S;
            float w = tex.width * s, h = tex.height * s;
            float bob = weapons.IsFiring ? 0f : player.BobAmount;
            float bx = Mathf.Cos(player.BobPhase) * bob * 12f * s;
            float by = Mathf.Abs(Mathf.Sin(player.BobPhase)) * bob * 8f * s;
            float lower = weapons.Lower * 90f * s;
            var r = new Rect(view.center.x - w * 0.5f + bx, view.yMax - h + by + lower + 2f * s, w, h);

            float light = gm.Level != null ? gm.Level.LightAt(player.transform.position) : 1f;
            light = weapons.IsFiring ? 1f : Mathf.Clamp(light + 0.1f, 0.25f, 1f);
            DrawSprite(r, tex, new Color(light, light, light, 1f));
        }

        // ------------------------------------------------------------------ status bar

        void DrawStatusBar(PlayerController player)
        {
            float s = S;
            float barH = 32f * s;
            float y0 = Screen.height - barH;
            var side = GameAssets.Texture("stbar_side");
            for (float x = 0; x < Screen.width; x += 32f * s)
                GUI.DrawTexture(new Rect(x, y0, 32f * s, barH), side);
            GUI.DrawTexture(V(0, 168, 320, 32), GameAssets.Texture("stbar"));

            var st = player.Status;
            var weapons = player.Weapons;
            var def = weapons.CurrentDef;

            // Ammo for current weapon
            string ammo = def.Ammo == AmmoType.None ? "" : st.Ammo[(int)def.Ammo].ToString();
            DrawText(V(2, 170, 44, 20), ammo, 17 * s, DoomRed, TextAnchor.MiddleRight, true);
            DrawText(V(2, 190, 44, 9), "AMMO", 5.5f * s, DoomGrey, TextAnchor.MiddleCenter);

            DrawText(V(48, 170, 54, 20), st.Health + "%", 17 * s, DoomRed, TextAnchor.MiddleRight, true);
            DrawText(V(48, 190, 56, 9), "HEALTH", 5.5f * s, DoomGrey, TextAnchor.MiddleCenter);

            // Arms 2-7
            for (int i = 0; i < 6; i++)
            {
                int weapon = i + 2;
                bool owned = st.Weapons[weapon];
                var col = owned ? DoomYellow : new Color(0.45f, 0.45f, 0.45f);
                DrawText(V(108 + (i % 3) * 11, 171 + (i / 3) * 9, 10, 9), (i + 2).ToString(), 7 * s, col, TextAnchor.MiddleCenter, true);
            }
            DrawText(V(104, 190, 38, 9), "ARMS", 5.5f * s, DoomGrey, TextAnchor.MiddleCenter);

            GUI.DrawTexture(V(148, 169, 24, 30), GameAssets.Sprite(FaceSprite(st)));

            DrawText(V(178, 170, 56, 20), st.Armor + "%", 17 * s, DoomRed, TextAnchor.MiddleRight, true);
            DrawText(V(178, 190, 58, 9), "ARMOR", 5.5f * s, DoomGrey, TextAnchor.MiddleCenter);

            // Keys
            string[] keySprites = { "item_redkey", "item_bluekey", "item_yellowkey" };
            for (int k = 0; k < 3; k++)
                if (st.Keys[k]) GUI.DrawTexture(V(239, 171 + k * 9, 7, 8), GameAssets.Sprite(keySprites[k]));

            // Ammo table
            string[] names = { "BULL", "SHEL", "RCKT", "CELL" };
            for (int a = 0; a < 4; a++)
            {
                float y = 171 + a * 7;
                DrawText(V(252, y, 22, 7), names[a], 5f * s, DoomGrey, TextAnchor.MiddleLeft, false, false);
                DrawText(V(272, y, 46, 7), $"{st.Ammo[a]} / {st.MaxAmmo((AmmoType)a)}", 5f * s, DoomYellow, TextAnchor.MiddleRight, false, false);
            }
        }

        static string FaceSprite(PlayerStatus st)
        {
            if (st.IsDead) return "hud_face_dead";
            if (st.God) return "hud_face_god";
            if (st.OuchTimer > 0f) return "hud_face_ouch";
            if (st.GrinTimer > 0f) return "hud_face_grin";
            int tier = st.Health >= 80 ? 0 : st.Health >= 60 ? 1 : st.Health >= 40 ? 2 : st.Health >= 20 ? 3 : 4;
            char look = st.LookDirection < 0 ? 'l' : st.LookDirection > 0 ? 'r' : 'c';
            return $"hud_face_{tier}_{look}";
        }

        void DrawFullscreenHud(PlayerController player)
        {
            float s = S;
            var st = player.Status;
            var def = player.Weapons.CurrentDef;
            DrawText(new Rect(6 * s, Screen.height - 22 * s, 80 * s, 20 * s), st.Health + "%", 15 * s, DoomRed, TextAnchor.LowerLeft, true);
            DrawText(new Rect(6 * s, Screen.height - 30 * s, 80 * s, 8 * s), "HEALTH", 5 * s, DoomGrey, TextAnchor.LowerLeft);
            DrawText(new Rect(60 * s, Screen.height - 22 * s, 80 * s, 20 * s), st.Armor + "%", 15 * s, DoomRed, TextAnchor.LowerLeft, true);
            DrawText(new Rect(60 * s, Screen.height - 30 * s, 80 * s, 8 * s), "ARMOR", 5 * s, DoomGrey, TextAnchor.LowerLeft);
            if (def.Ammo != AmmoType.None)
            {
                DrawText(new Rect(Screen.width - 86 * s, Screen.height - 22 * s, 80 * s, 20 * s), st.Ammo[(int)def.Ammo].ToString(), 15 * s, DoomRed, TextAnchor.LowerRight, true);
                DrawText(new Rect(Screen.width - 86 * s, Screen.height - 30 * s, 80 * s, 8 * s), "AMMO", 5 * s, DoomGrey, TextAnchor.LowerRight);
            }
            string[] keySprites = { "item_redkey", "item_bluekey", "item_yellowkey" };
            for (int k = 0; k < 3; k++)
                if (st.Keys[k]) GUI.DrawTexture(new Rect(Screen.width - 16 * s, 6 * s + k * 10 * s, 7 * s, 8 * s), GameAssets.Sprite(keySprites[k]));
        }

        // ------------------------------------------------------------------ automap

        void DrawAutomap(GameManager gm, Rect view)
        {
            var level = gm.Level;
            var player = gm.Player;
            FillRect(view, Color.black);
            if (level == null) return;

            float ppm = 3.5f * S; // pixels per meter
            var pp = player.transform.position;
            var center = view.center;
            float cs = Level.CellSize * ppm;

            GUI.BeginGroup(view);
            var c0 = center - view.position;
            for (int y = 0; y < level.Height; y++)
                for (int x = 0; x < level.Width; x++)
                {
                    if (!level.Seen[x, y]) continue;
                    var cell = level.Cells[x, y];
                    float wx = x * Level.CellSize, wz = (level.Height - 1 - y) * Level.CellSize;
                    var r = new Rect(c0.x + (wx - pp.x) * ppm, c0.y - (wz + Level.CellSize - pp.z) * ppm, cs + 0.5f, cs + 0.5f);
                    if (r.xMax < 0 || r.yMax < 0 || r.x > view.width || r.y > view.height) continue;

                    Color col;
                    switch (cell.Kind)
                    {
                        case CellKind.Floor:
                            col = cell.Pad != default ? new Color(0.3f, 1f, 0.4f)
                                : cell.Nukage ? new Color(0.05f, 0.25f, 0.05f) : new Color(0.12f, 0.1f, 0.08f);
                            break;
                        case CellKind.Lift:
                            col = new Color(0.55f, 0.35f, 0.1f);
                            break;
                        case CellKind.Door:
                            col = cell.Char switch
                            {
                                'R' => new Color(0.9f, 0.1f, 0.1f),
                                'U' => new Color(0.2f, 0.3f, 1f),
                                'Y' => new Color(1f, 0.9f, 0.1f),
                                _ => new Color(0.75f, 0.65f, 0.2f)
                            };
                            break;
                        case CellKind.Exit:
                            col = new Color(0.2f, 0.9f, 0.3f);
                            break;
                        case CellKind.SecretDoor:
                        case CellKind.Wall:
                            col = new Color(0.7f, 0.12f, 0.08f);
                            break;
                        default:
                            continue;
                    }
                    FillRect(r, col);
                }

            // Player arrow
            var pr = new Vector2(c0.x, c0.y);
            var oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(player.Yaw, pr);
            FillRect(new Rect(pr.x - 0.75f * S, pr.y - 5f * S, 1.5f * S, 8f * S), Color.white);
            FillRect(new Rect(pr.x - 2.5f * S, pr.y - 3f * S, 5f * S, 1.5f * S), Color.white);
            GUI.matrix = oldMatrix;
            GUI.EndGroup();

            DrawText(new Rect(view.x + 4 * S, view.yMax - 12 * S, view.width, 10 * S),
                $"{level.Def.Id}: {level.Def.Name}", 7 * S, DoomRed, TextAnchor.MiddleLeft, true);
            DrawText(new Rect(view.x, view.yMax - 12 * S, view.width - 4 * S, 10 * S),
                $"K {gm.Stats.Kills}/{level.TotalKills}   I {gm.Stats.Items}/{level.TotalItems}   S {gm.Stats.Secrets}/{level.TotalSecrets}",
                6 * S, DoomGrey, TextAnchor.MiddleRight);
        }

        // ------------------------------------------------------------------ intermission

        public void BeginIntermission(LevelResult r)
        {
            result = r;
            interTime = 0f;
            interDone = false;
            for (int i = 0; i < shown.Length; i++) shown[i] = -1f;
        }

        static float Percent(int got, int total) => total <= 0 ? 100f : got * 100f / total;

        float[] Targets() => new[]
        {
            Percent(result.Kills, result.TotalKills),
            Percent(result.Items, result.TotalItems),
            Percent(result.Secrets, result.TotalSecrets),
            result.Time
        };

        public void UpdateIntermission(float dt)
        {
            if (result == null) return;
            interTime += dt;
            var targets = Targets();

            if (DoomInput.AnyAdvance && interTime > 0.3f)
            {
                if (!interDone)
                {
                    for (int i = 0; i < shown.Length; i++) shown[i] = targets[i];
                    interDone = true;
                    AudioManager.I.Play2D("explosion", 0.5f);
                }
                else GameManager.I.ContinueAfterIntermission();
                return;
            }

            if (interDone || interTime < 0.6f) return;
            for (int i = 0; i < shown.Length; i++)
            {
                if (shown[i] >= targets[i]) continue;
                float speed = i == 3 ? Mathf.Max(30f, targets[i] * 0.8f) : 90f;
                shown[i] = Mathf.Min(targets[i], Mathf.Max(0f, shown[i]) + speed * dt);
                tickTimer -= dt;
                if (tickTimer <= 0f)
                {
                    tickTimer = 0.06f;
                    AudioManager.I.Play2D("tick", 0.6f);
                }
                if (shown[i] >= targets[i]) AudioManager.I.Play2D("explosion", 0.4f);
                return;
            }
            interDone = true;
        }

        static string FormatTime(float seconds)
        {
            int t = Mathf.FloorToInt(seconds);
            return $"{t / 60}:{t % 60:00}";
        }

        void DrawIntermission()
        {
            if (result == null) return;
            float s = S;
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            var pic = GameAssets.OptionalSprite("hud_interpic");
            if (pic != null) GUI.DrawTexture(screen, pic, ScaleMode.ScaleAndCrop);
            else FillRect(screen, new Color(0.08f, 0.05f, 0.04f));

            DrawText(new Rect(0, 12 * s, Screen.width, 16 * s), $"{result.Def.Id}: {result.Def.Name.ToUpperInvariant()}", 14 * s, DoomRed, TextAnchor.MiddleCenter, true);
            DrawText(new Rect(0, 28 * s, Screen.width, 10 * s), "FINISHED", 10 * s, DoomRed, TextAnchor.MiddleCenter, true);

            string[] labels = { "KILLS", "ITEMS", "SECRET" };
            float left = Screen.width * 0.5f - 90 * s;
            for (int i = 0; i < 3; i++)
            {
                float y = (56 + i * 20) * s;
                DrawText(new Rect(left, y, 100 * s, 16 * s), labels[i], 13 * s, DoomRed, TextAnchor.MiddleLeft, true);
                if (shown[i] >= 0f)
                    DrawText(new Rect(left, y, 180 * s, 16 * s), Mathf.FloorToInt(shown[i]) + "%", 13 * s, DoomRed, TextAnchor.MiddleRight, true);
            }

            float ty = 122 * s;
            DrawText(new Rect(left, ty, 100 * s, 16 * s), "TIME", 13 * s, DoomRed, TextAnchor.MiddleLeft, true);
            if (shown[3] >= 0f) DrawText(new Rect(left, ty, 180 * s, 16 * s), FormatTime(shown[3]), 13 * s, DoomRed, TextAnchor.MiddleRight, true);
            DrawText(new Rect(left, ty + 18 * s, 100 * s, 16 * s), "PAR", 13 * s, DoomRed, TextAnchor.MiddleLeft, true);
            DrawText(new Rect(left, ty + 18 * s, 180 * s, 16 * s), FormatTime(result.Def.ParSeconds), 13 * s, DoomRed, TextAnchor.MiddleRight, true);

            if (interDone)
            {
                if (result.Next != null)
                    DrawText(new Rect(0, 166 * s, Screen.width, 10 * s), $"ENTERING {result.Next.Id}: {result.Next.Name.ToUpperInvariant()}", 8 * s, DoomYellow, TextAnchor.MiddleCenter, true);
                if ((int)(Time.unscaledTime * 2f) % 2 == 0)
                    DrawText(new Rect(0, 182 * s, Screen.width, 10 * s), "PRESS FIRE / USE TO CONTINUE", 6 * s, DoomGrey, TextAnchor.MiddleCenter);
            }
        }

        // ------------------------------------------------------------------ finale

        public void BeginFinale() => FinaleTime = 0f;

        void DrawFinale()
        {
            FinaleTime += Time.unscaledDeltaTime;
            float s = S;
            FillRect(new Rect(0, 0, Screen.width, Screen.height), new Color(0.12f, 0.03f, 0.02f));
            int chars = Mathf.Min(FinaleText.Length, (int)(FinaleTime * 25f));
            DrawText(new Rect(Screen.width * 0.5f - 140 * s, 20 * s, 280 * s, 160 * s), FinaleText.Substring(0, chars), 8 * s,
                new Color(0.95f, 0.9f, 0.8f), TextAnchor.UpperLeft, false, true, true);
            if (chars >= FinaleText.Length && (int)(Time.unscaledTime * 2f) % 2 == 0)
                DrawText(new Rect(0, 184 * s, Screen.width, 10 * s), "PRESS FIRE / USE", 6 * s, DoomGrey, TextAnchor.MiddleCenter);
        }

        void OnDestroy()
        {
            if (viewTexture != null) viewTexture.Release();
        }
    }
}
