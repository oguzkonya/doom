using System;
using System.Collections.Generic;
using UnityEngine;

namespace Doom
{
    /// <summary>Doom-style menu stack navigable with keyboard or gamepad (no mouse needed).</summary>
    public class Menu
    {
        class Item
        {
            public string Label;
            public Func<string> Value;
            public Action Activate;
            public Action<int> Adjust;
        }

        class Page
        {
            public string Title;
            public string Body;
            public readonly List<Item> Items = new();
            public int Index;
        }

        static readonly string[] SkillNames =
        {
            "I'm too young to die.", "Hey, not too rough.", "Hurt me plenty.", "Ultra-Violence.", "Nightmare!"
        };

        static readonly string[] QuitMessages =
        {
            "Please don't leave, there's more demons to toast!",
            "You're trying to say you like Windows better than me, right?",
            "Don't leave yet -- there's a demon around that corner!",
            "Go ahead and leave. See if I care.",
            "Are you sure you want to quit this great game?",
        };

        const string ControlsText =
            "KEYBOARD / MOUSE\n" +
            "Move: WASD or arrows    Turn: mouse or Left/Right\n" +
            "Fire: left mouse / Ctrl    Use / open: E or Space\n" +
            "Run: Shift (toggles vs. Always Run)\n" +
            "Weapons: 1-7, mouse wheel, Q / [ ]\n" +
            "Automap: Tab    Menu: Esc\n\n" +
            "GAMEPAD\n" +
            "Move: left stick    Turn / look: right stick\n" +
            "Fire: RT    Use / open: A (Cross)\n" +
            "Weapons: LB / RB, Y (Triangle)\n" +
            "Automap: View / Select    Menu: Start";

        readonly Stack<Page> stack = new();
        readonly GameManager gm;
        int openedFrame;

        public bool IsOpen => stack.Count > 0;
        public int LastCloseFrame { get; private set; } = -1;

        public Menu(GameManager gm) => this.gm = gm;

        public void CloseAll()
        {
            if (stack.Count > 0) LastCloseFrame = Time.frameCount;
            stack.Clear();
            Settings.Save();
        }

        void Push(Page p)
        {
            stack.Push(p);
            openedFrame = Time.frameCount;
        }

        void Pop()
        {
            stack.Pop();
            if (stack.Count == 0) LastCloseFrame = Time.frameCount;
            Settings.Save();
        }

        public void OpenMain()
        {
            stack.Clear();
            Push(MainPage());
        }

        public void OpenPause()
        {
            stack.Clear();
            Push(MainPage());
            AudioManager.I.Play2D("menu_back");
        }

        // ------------------------------------------------------------------ pages

        Page MainPage()
        {
            bool inGame = gm.State == GameState.Playing;
            var p = new Page();
            if (inGame) p.Items.Add(new Item { Label = "Resume", Activate = CloseAll });
            p.Items.Add(new Item { Label = "New Game", Activate = () => Push(SkillPage()) });
            p.Items.Add(new Item { Label = "Options", Activate = () => Push(OptionsPage()) });
            p.Items.Add(new Item { Label = "Controls", Activate = () => Push(InfoPage("CONTROLS", ControlsText)) });
            if (inGame)
            {
                p.Items.Add(new Item { Label = "Restart Level", Activate = () => Push(ConfirmPage("Restart this level from a pistol start?", gm.RestartLevel)) });
                p.Items.Add(new Item { Label = "End Game", Activate = () => Push(ConfirmPage("Are you sure you want to end the game?", gm.GoToTitle)) });
            }
            p.Items.Add(new Item
            {
                Label = "Quit Game",
                Activate = () => Push(ConfirmPage(QuitMessages[UnityEngine.Random.Range(0, QuitMessages.Length)], QuitApplication))
            });
            return p;
        }

        Page SkillPage()
        {
            var p = new Page { Title = "CHOOSE SKILL LEVEL", Index = 2 };
            for (int i = 0; i < SkillNames.Length; i++)
            {
                int skill = i + 1;
                p.Items.Add(new Item { Label = SkillNames[i], Activate = () => gm.NewGame(skill) });
            }
            return p;
        }

        static string Bar(float value, float min, float max)
        {
            int n = Mathf.RoundToInt(Mathf.InverseLerp(min, max, value) * 10f);
            return "[" + new string('#', n) + new string('-', 10 - n) + "]";
        }

        static string OnOff(bool v) => v ? "ON" : "OFF";

        Item Toggle(string label, Func<bool> get, Action<bool> set) => new()
        {
            Label = label,
            Value = () => OnOff(get()),
            Activate = () => set(!get()),
            Adjust = _ => set(!get())
        };

        Item Slider(string label, Func<float> get, Action<float> set, float min, float max, float step) => new()
        {
            Label = label,
            Value = () => Bar(get(), min, max),
            Adjust = d => set(Mathf.Clamp(get() + d * step, min, max))
        };

        Page OptionsPage()
        {
            var p = new Page { Title = "OPTIONS" };
            string[] renderNames = { "CLASSIC 200p", "DOUBLE 400p", "NATIVE" };
            string[] hudNames = { "STATUS BAR", "FULLSCREEN" };
            p.Items.Add(Slider("Mouse sensitivity", () => Settings.MouseSensitivity, v => Settings.MouseSensitivity = v, 0.1f, 4f, 0.1f));
            p.Items.Add(Slider("Gamepad look speed", () => Settings.StickSensitivity, v => Settings.StickSensitivity = v, 0.1f, 4f, 0.1f));
            p.Items.Add(Toggle("Freelook (vertical aim)", () => Settings.Freelook, v => Settings.Freelook = v));
            p.Items.Add(Toggle("Invert Y", () => Settings.InvertY, v => Settings.InvertY = v));
            p.Items.Add(Toggle("Autoaim", () => Settings.Autoaim, v => Settings.Autoaim = v));
            p.Items.Add(Toggle("Always run", () => Settings.AlwaysRun, v => Settings.AlwaysRun = v));
            p.Items.Add(new Item
            {
                Label = "Rendering",
                Value = () => renderNames[Settings.RenderMode],
                Activate = () => Settings.RenderMode++,
                Adjust = d => Settings.RenderMode += d
            });
            p.Items.Add(new Item
            {
                Label = "HUD",
                Value = () => hudNames[Settings.HudStyle],
                Activate = () => Settings.HudStyle++,
                Adjust = d => Settings.HudStyle += d
            });
            p.Items.Add(Toggle("Crosshair", () => Settings.Crosshair, v => Settings.Crosshair = v));
            p.Items.Add(Toggle("Controller rumble", () => Settings.Rumble, v => Settings.Rumble = v));
            p.Items.Add(Slider("Sound volume", () => Settings.SfxVolume, v => Settings.SfxVolume = v, 0f, 1f, 0.1f));
            p.Items.Add(Slider("Music volume", () => Settings.MusicVolume, v => Settings.MusicVolume = v, 0f, 1f, 0.1f));
            p.Items.Add(Toggle("Fullscreen", () => Settings.Fullscreen, v => Settings.Fullscreen = v));
            return p;
        }

        Page InfoPage(string title, string body)
        {
            var p = new Page { Title = title, Body = body };
            p.Items.Add(new Item { Label = "Back", Activate = Pop });
            return p;
        }

        Page ConfirmPage(string question, Action onYes)
        {
            var p = new Page { Body = question, Index = 1 };
            p.Items.Add(new Item { Label = "Yes", Activate = onYes });
            p.Items.Add(new Item { Label = "No", Activate = Pop });
            return p;
        }

        static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ input

        public void Update()
        {
            if (!IsOpen || Time.frameCount == openedFrame) return;
            var page = stack.Peek();
            var audio = AudioManager.I;

            if (gm.State == GameState.Playing && DoomInput.Pause.WasPressedThisFrame() && !DoomInput.MenuBack.WasPressedThisFrame())
            {
                CloseAll();
                return;
            }

            if (DoomInput.MenuBack.WasPressedThisFrame())
            {
                audio.Play2D("menu_back");
                if (stack.Count > 1) Pop();
                else CloseAll();
                return;
            }

            if (page.Items.Count == 0) return;
            if (DoomInput.MenuUp.WasPressedThisFrame())
            {
                page.Index = (page.Index - 1 + page.Items.Count) % page.Items.Count;
                audio.Play2D("menu_move");
            }
            if (DoomInput.MenuDown.WasPressedThisFrame())
            {
                page.Index = (page.Index + 1) % page.Items.Count;
                audio.Play2D("menu_move");
            }

            var item = page.Items[page.Index];
            if (item.Adjust != null)
            {
                if (DoomInput.MenuLeft.WasPressedThisFrame()) { item.Adjust(-1); audio.Play2D("menu_move"); }
                if (DoomInput.MenuRight.WasPressedThisFrame()) { item.Adjust(1); audio.Play2D("menu_move"); }
            }

            if (DoomInput.MenuConfirm.WasPressedThisFrame())
            {
                if (item.Activate != null)
                {
                    audio.Play2D("menu_select");
                    item.Activate();
                }
                else if (item.Adjust != null)
                {
                    item.Adjust(1);
                    audio.Play2D("menu_move");
                }
            }
        }

        // ------------------------------------------------------------------ drawing

        public void Draw(GameUI ui)
        {
            if (!IsOpen) return;
            var page = stack.Peek();
            float s = ui.S;
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            ui.FillRect(screen, new Color(0, 0, 0, gm.State == GameState.Title ? 0.25f : 0.6f));
            var red = new Color(0.85f, 0.1f, 0.05f);
            var hot = new Color(1f, 0.55f, 0.2f);

            float y = 14 * s;
            if (page.Title == null && page.Body == null)
            {
                ui.DrawText(new Rect(0, y, Screen.width, 40 * s), "DOOM", 40 * s, red, TextAnchor.MiddleCenter, true);
                y += 50 * s;
            }
            else if (page.Title != null)
            {
                ui.DrawText(new Rect(0, y, Screen.width, 16 * s), page.Title, 13 * s, red, TextAnchor.MiddleCenter, true);
                y += 22 * s;
            }

            if (page.Body != null)
            {
                bool isConfirm = page.Title == null;
                if (isConfirm) y = 60 * s;
                float bodyH = isConfirm ? 30 * s : 110 * s;
                ui.DrawText(new Rect(Screen.width * 0.5f - 150 * s, y, 300 * s, bodyH), page.Body, (isConfirm ? 8 : 6.2f) * s,
                    new Color(0.95f, 0.9f, 0.8f), isConfirm ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft, false, true, true);
                y += bodyH + 6 * s;
            }

            int count = page.Items.Count;
            float lineH = count > 8 ? 9.5f * s : 15 * s;
            float fontSize = count > 8 ? 7 * s : 11 * s;
            float left = Screen.width * 0.5f - (count > 8 ? 110 : 75) * s;
            for (int i = 0; i < count; i++)
            {
                var item = page.Items[i];
                bool selected = i == page.Index;
                var r = new Rect(left, y + i * lineH, 240 * s, lineH);
                ui.DrawText(r, item.Label, fontSize, selected ? hot : red, TextAnchor.MiddleLeft, true);
                if (item.Value != null)
                    ui.DrawText(new Rect(left + 125 * s, r.y, 120 * s, lineH), item.Value(), fontSize * 0.85f,
                        selected ? hot : new Color(0.9f, 0.8f, 0.6f), TextAnchor.MiddleLeft, true);
                if (selected)
                {
                    var skull = GameAssets.Sprite((int)(Time.unscaledTime * 4f) % 2 == 0 ? "hud_skull0" : "hud_skull1");
                    float sh = Mathf.Min(lineH, 16 * s), sw = sh * skull.width / skull.height;
                    GUI.DrawTexture(new Rect(left - sw - 4 * s, r.y + (lineH - sh) * 0.5f, sw, sh), skull);
                }
            }

            ui.DrawText(new Rect(0, 188 * s, Screen.width, 10 * s),
                "UP/DOWN select   LEFT/RIGHT adjust   ENTER / A confirm   ESC / B back", 5 * s,
                new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleCenter);
        }
    }
}
