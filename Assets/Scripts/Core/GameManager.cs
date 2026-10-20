using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Doom
{
    public enum GameState { Title, Playing, Intermission, Finale }

    public class LevelStats
    {
        public int Kills, Items, Secrets;
        public float Time;
    }

    public class LevelResult
    {
        public LevelDef Def;
        public LevelDef Next;
        public int Kills, TotalKills, Items, TotalItems, Secrets, TotalSecrets;
        public float Time;
    }

    /// <summary>Owns the game flow: title, levels, intermission, finale, pause, cheats and global effects.</summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager I { get; private set; }

        public GameState State { get; private set; } = GameState.Title;
        public int Skill { get; private set; } = 3;
        public int LevelIndex { get; private set; }
        public Level Level { get; private set; }
        public PlayerController Player { get; private set; }
        public LevelStats Stats { get; private set; } = new();
        public LevelResult LastResult { get; private set; }
        public Menu Menu { get; private set; }
        public GameUI UI { get; private set; }
        public bool AutomapOpen { get; set; }
        public string Message { get; private set; }
        public float MessageTimer { get; private set; }

        public Camera ViewCamera => Player != null ? Player.Cam : displayCamera;
        public bool IsGameplayActive => State == GameState.Playing && !Menu.IsOpen;

        static readonly int ExtraLightId = Shader.PropertyToID("_DoomExtraLight");

        Camera displayCamera;
        Transform listener;
        PlayerSnapshot carry;
        float flash, rumbleTimer, exitTimer = -1f;
        string cheatBuffer = "";

        void Awake()
        {
            if (I != null && I != this)
            {
                Destroy(gameObject);
                return;
            }
            I = this;
            DontDestroyOnLoad(gameObject);
            DoomInput.Init();
            QualitySettings.vSyncCount = 1;

            listener = new GameObject("Listener").transform;
            listener.SetParent(transform, false);
            listener.gameObject.AddComponent<AudioListener>();

            gameObject.AddComponent<AudioManager>();
            UI = gameObject.AddComponent<GameUI>();
            Menu = new Menu(this);

            var camGo = new GameObject("DisplayCamera");
            camGo.transform.SetParent(transform, false);
            displayCamera = camGo.AddComponent<Camera>();
            displayCamera.cullingMask = 0;
            displayCamera.clearFlags = CameraClearFlags.SolidColor;
            displayCamera.backgroundColor = Color.black;
            displayCamera.depth = -10;

            Shader.SetGlobalFloat("_DoomFalloff", 0.018f);
            Shader.SetGlobalFloat("_DoomMinLight", 0.06f);
            Shader.SetGlobalFloat(ExtraLightId, 0f);

            if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
        }

        void OnDestroy()
        {
            if (I != this) return;
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
            StopRumble();
            Time.timeScale = 1f;
        }

        void OnApplicationFocus(bool focus)
        {
            if (!focus && State == GameState.Playing && !Menu.IsOpen && !Application.isEditor) Menu.OpenPause();
        }

        void Start() => GoToTitle();

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Menu.Update();

            bool paused = State == GameState.Playing && Menu.IsOpen;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;

            switch (State)
            {
                case GameState.Title:
                    if (!Menu.IsOpen && DoomInput.QuickLoad.WasPressedThisFrame())
                        LoadGame(SaveSystem.QuickSlot);
                    else if (!Menu.IsOpen && Menu.LastCloseFrame != Time.frameCount &&
                        (DoomInput.AnyAdvance || DoomInput.Pause.WasPressedThisFrame()))
                        Menu.OpenMain();
                    break;
                case GameState.Playing:
                    if (!Menu.IsOpen)
                    {
                        if (DoomInput.Pause.WasPressedThisFrame() && Menu.LastCloseFrame != Time.frameCount) Menu.OpenPause();
                        else if (DoomInput.Automap.WasPressedThisFrame()) AutomapOpen = !AutomapOpen;
                        else if (DoomInput.QuickSave.WasPressedThisFrame()) SaveGame(SaveSystem.QuickSlot);
                        else if (DoomInput.QuickLoad.WasPressedThisFrame()) LoadGame(SaveSystem.QuickSlot);
                        if (Player != null && !Player.Status.IsDead) Stats.Time += Time.deltaTime;
                    }
                    if (exitTimer > 0f)
                    {
                        exitTimer -= Time.deltaTime;
                        if (exitTimer <= 0f) FinishLevel();
                    }
                    break;
                case GameState.Intermission:
                    if (!Menu.IsOpen) UI.UpdateIntermission(dt);
                    break;
                case GameState.Finale:
                    if (!Menu.IsOpen && UI.FinaleTime > 1f && DoomInput.AnyAdvance) GoToTitle();
                    break;
            }

            bool lockCursor = IsGameplayActive;
            Cursor.lockState = lockCursor ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !lockCursor;

            MessageTimer -= dt;
            flash = Mathf.MoveTowards(flash, 0f, dt * 3f);
            Shader.SetGlobalFloat(ExtraLightId, flash);

            if (rumbleTimer > 0f)
            {
                rumbleTimer -= dt;
                if (rumbleTimer <= 0f || paused) StopRumble();
            }
        }

        void LateUpdate()
        {
            var cam = ViewCamera;
            if (cam) listener.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
        }

        // ------------------------------------------------------------------ flow

        public void NewGame(int skill)
        {
            Skill = Mathf.Clamp(skill, 1, 5);
            carry = null;
            StartLevel(0);
        }

        public void StartLevel(int index)
        {
            ClearLevel();
            LevelIndex = index;
            var def = Levels.All[index];
            Level = Doom.Level.Build(def, transform);
            Player = PlayerController.Spawn(Level.transform, Level.PlayerStart, def.StartAngle);
            Player.Status.Load(carry);
            Stats = new LevelStats();
            State = GameState.Playing;
            Menu.CloseAll();
            AutomapOpen = false;
            exitTimer = -1f;
            Time.timeScale = 1f;
            ShowMessage($"{def.Id}: {def.Name}");
            AudioManager.I.PlayMusic(def.Music);
        }

        void ClearLevel()
        {
            if (Level != null) Destroy(Level.gameObject);
            Level = null;
            Player = null;
            flash = 0f;
            StopRumble();
        }

        /// <summary>After death the level restarts with a fresh pistol start, like the original.</summary>
        public void RestartLevel()
        {
            carry = null;
            StartLevel(LevelIndex);
        }

        public void ExitLevel(float delay)
        {
            if (exitTimer > 0f || State != GameState.Playing) return;
            exitTimer = Mathf.Max(0.01f, delay);
        }

        void FinishLevel()
        {
            exitTimer = -1f;
            carry = Player.Status.Save();
            LastResult = new LevelResult
            {
                Def = Level.Def,
                Next = LevelIndex + 1 < Levels.All.Length ? Levels.All[LevelIndex + 1] : null,
                Kills = Stats.Kills, TotalKills = Level.TotalKills,
                Items = Stats.Items, TotalItems = Level.TotalItems,
                Secrets = Stats.Secrets, TotalSecrets = Level.TotalSecrets,
                Time = Stats.Time
            };
            ClearLevel();
            State = GameState.Intermission;
            AutomapOpen = false;
            UI.BeginIntermission(LastResult);
            AudioManager.I.PlayMusic("intermission");
        }

        public void ContinueAfterIntermission()
        {
            if (LevelIndex + 1 < Levels.All.Length) StartLevel(LevelIndex + 1);
            else
            {
                State = GameState.Finale;
                UI.BeginFinale();
                AudioManager.I.PlayMusic("finale");
            }
        }

        public void GoToTitle()
        {
            ClearLevel();
            State = GameState.Title;
            Time.timeScale = 1f;
            Menu.OpenMain();
            AudioManager.I.PlayMusic("title");
        }

        // ------------------------------------------------------------------ save / load

        /// <summary>Why saving is not possible right now, or null when it is.</summary>
        public string SaveBlockedReason()
        {
            if (State != GameState.Playing || Player == null || Level == null) return "You can't save if you aren't playing!";
            if (Player.Status.IsDead) return "You can't save while dead!";
            if (exitTimer > 0f) return "You can't save while exiting the level!";
            return null;
        }

        public bool SaveGame(string slot)
        {
            var blocked = SaveBlockedReason();
            if (blocked != null)
            {
                ShowMessage(blocked);
                AudioManager.I.Play2D("noway");
                return false;
            }

            var def = Level.Def;
            var data = new SaveData
            {
                version = SaveSystem.Version,
                levelId = def.Id,
                levelName = def.Name,
                mapHash = SaveSystem.MapHash(def),
                savedAt = DateTime.Now.ToString("MM-dd HH:mm"),
                skill = Skill,
                kills = Stats.Kills,
                items = Stats.Items,
                secrets = Stats.Secrets,
                time = Stats.Time,
                player = new PlayerSave
                {
                    position = Player.transform.position,
                    yaw = Player.Yaw,
                    status = Player.Status.Save(),
                    keys = (bool[])Player.Status.Keys.Clone()
                }
            };
            Level.CaptureState(data);

            if (!SaveSystem.Write(slot, data, out var error))
            {
                ShowMessage(error);
                return false;
            }
            ShowMessage(slot == SaveSystem.QuickSlot ? "Quicksaved." : "Game saved.");
            return true;
        }

        public bool LoadGame(string slot)
        {
            var data = SaveSystem.Read(slot, out var error);
            int index = data == null ? -1 : Array.FindIndex(Levels.All, l => l.Id == data.levelId);
            if (data != null && index < 0) error = $"Level {data.levelId} no longer exists.";
            if (index < 0)
            {
                ShowMessage(error);
                AudioManager.I.Play2D("noway");
                return false;
            }
            if (data.mapHash != SaveSystem.MapHash(Levels.All[index]))
                Debug.LogWarning($"[Doom] {data.levelId} was edited after this save was made; things may be out of place.");

            Skill = Mathf.Clamp(data.skill, 1, 5);
            carry = null;
            StartLevel(index);

            Player.Status.Load(data.player.status);
            if (data.player.keys != null)
                for (int i = 0; i < Player.Status.Keys.Length && i < data.player.keys.Length; i++)
                    Player.Status.Keys[i] = data.player.keys[i];
            Player.Teleport(data.player.position, data.player.yaw);
            Level.ApplyState(data);
            Stats = new LevelStats { Kills = data.kills, Items = data.items, Secrets = data.secrets, Time = data.time };
            ShowMessage(slot == SaveSystem.QuickSlot ? "Quickloaded." : "Game loaded.");
            return true;
        }

        // ------------------------------------------------------------------ events

        public void ShowMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Message = text;
            MessageTimer = 4f;
        }

        public void OnKill() => Stats.Kills++;

        public void OnSecretFound()
        {
            Stats.Secrets++;
            ShowMessage("A secret is revealed!");
            AudioManager.I.Play2D("secret");
        }

        public void OnPickup(Pickup pickup, string message, string sound)
        {
            if (pickup.CountsAsItem) Stats.Items++;
            ShowMessage(message);
            AudioManager.I.Play2D(sound);
        }

        public void OnPlayerDied()
        {
            AutomapOpen = false;
            ShowMessage("You died. Press USE or FIRE to restart the level.");
            Rumble(1f, 1f, 0.6f);
        }

        public void MuzzleFlash(float amount) => flash = Mathf.Max(flash, amount);

        public void Rumble(float low, float high, float duration)
        {
            if (!Settings.Rumble) return;
            var pad = Gamepad.current;
            if (pad == null) return;
            pad.SetMotorSpeeds(low, high);
            rumbleTimer = Mathf.Max(rumbleTimer, duration);
        }

        void StopRumble()
        {
            rumbleTimer = 0f;
            Gamepad.current?.SetMotorSpeeds(0f, 0f);
        }

        // ------------------------------------------------------------------ cheats

        void OnTextInput(char c)
        {
            if (!IsGameplayActive || Player == null) return;
            cheatBuffer += char.ToLowerInvariant(c);
            if (cheatBuffer.Length > 16) cheatBuffer = cheatBuffer.Substring(cheatBuffer.Length - 16);

            var status = Player.Status;
            if (cheatBuffer.EndsWith("iddqd"))
            {
                status.God = !status.God;
                if (status.God) status.Health = 100;
                ShowMessage(status.God ? "Degreelessness Mode On" : "Degreelessness Mode Off");
            }
            else if (cheatBuffer.EndsWith("idkfa"))
            {
                status.GiveEverything(true);
                ShowMessage("Very Happy Ammo Added");
            }
            else if (cheatBuffer.EndsWith("idfa"))
            {
                status.GiveEverything(false);
                ShowMessage("Ammo (no keys) Added");
            }
            else if (cheatBuffer.EndsWith("idclip") || cheatBuffer.EndsWith("idspispopd"))
            {
                Player.Noclip = !Player.Noclip;
                ShowMessage(Player.Noclip ? "No Clipping Mode ON" : "No Clipping Mode OFF");
            }
            else if (cheatBuffer.EndsWith("iddt"))
            {
                Level.RevealAll();
                ShowMessage("Map revealed");
            }
            else return;

            cheatBuffer = "";
        }
    }
}
