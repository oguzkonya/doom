using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Doom.Tests
{
    public class GameplayTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (GameManager.I == null) new GameObject("DOOM").AddComponent<GameManager>();
            yield return null;
            GameManager.I.GoToTitle();
            yield return null;
        }

        static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        /// <summary>Saves what the player camera sees when DOOM_SHOT_DIR is set (for eyeballing placeholder art).</summary>
        static void Capture(Camera cam, string name)
        {
            var dir = System.Environment.GetEnvironmentVariable("DOOM_SHOT_DIR");
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            var rt = new RenderTexture(640, 400, 24);
            var old = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = old;
            RenderTexture.active = rt;
            var tex = new Texture2D(640, 400, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 640, 400), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }

        [UnityTest]
        public IEnumerator PlaysThroughAllLevels()
        {
            var gm = GameManager.I;
            Assert.AreEqual(GameState.Title, gm.State);
            gm.NewGame(3);

            for (int i = 0; i < Levels.All.Length; i++)
            {
                Assert.AreEqual(GameState.Playing, gm.State);
                Assert.AreEqual(i, gm.LevelIndex);
                yield return Wait(1.5f);

                var level = gm.Level;
                var player = gm.Player;
                Assert.NotNull(level);
                Assert.NotNull(player);
                player.Status.God = true;
                Assert.Less(Vector3.Distance(player.transform.position, level.PlayerStart), 3f, "player should stay near the start");
                Assert.Greater(player.transform.position.y, level.PlayerStart.y - 0.2f, "player fell through the floor");
                Assert.Greater(level.Enemies.Count, 0);
                Capture(player.Cam, $"{level.Def.Id}_start.png");

                Projectile.Spawn("rocket", player.EyePosition, player.transform.forward, player.gameObject);
                yield return Wait(0.6f);

                foreach (var e in level.Enemies.ToArray())
                    if (!e.IsDead) e.Damage(100000, Vector3.forward, player.gameObject);
                yield return Wait(0.6f);
                Assert.AreEqual(level.TotalKills, gm.Stats.Kills, "all kills counted");

                player.Status.GiveEverything(true);
                var doors = level.GetComponentsInChildren<Door>();
                foreach (var d in doors) d.Use(player);
                yield return Wait(1.5f);
                foreach (var d in doors) Assert.IsTrue(d.IsOpen, $"door {d.name} did not open");

                var exit = level.GetComponentInChildren<ExitSwitch>();
                Assert.NotNull(exit, "level has an exit");
                exit.Use(player);
                yield return Wait(1.5f);
                Assert.AreEqual(GameState.Intermission, gm.State);
                gm.ContinueAfterIntermission();
                yield return null;
            }

            Assert.AreEqual(GameState.Finale, gm.State);
            gm.GoToTitle();
        }

        [UnityTest]
        public IEnumerator SaveAndLoadRestoresLevelState()
        {
            const string slot = "test_roundtrip";
            var gm = GameManager.I;
            gm.NewGame(4);
            yield return Wait(0.3f);
            var level = gm.Level;
            var player = gm.Player;
            try
            {
                player.Status.God = true;
                foreach (var e in level.Enemies.Take(3)) e.Damage(100000, Vector3.forward, player.gameObject);
                player.Status.Health = 57;
                player.Status.Ammo[(int)AmmoType.Bullets] = 33;
                player.Status.Keys[(int)KeyColor.Blue] = true;
                player.Status.Weapons[(int)WeaponId.Shotgun] = true;
                level.GetComponentsInChildren<Door>().First().Use(player);
                Object.Destroy(level.GetComponentsInChildren<Pickup>().First().gameObject);
                player.Teleport(level.CellCenter(6, 10), 90f);
                yield return Wait(1.5f);

                Assert.IsTrue(gm.SaveGame(slot), "save failed");
                var pos = player.transform.position;
                int kills = gm.Stats.Kills;
                int dead = level.Enemies.Count(e => e.IsDead);
                int alive = level.Enemies.Count(e => !e.IsDead);
                int pickups = level.GetComponentsInChildren<Pickup>().Length;
                bool[] openDoors = level.GetComponentsInChildren<Door>().Select(d => d.IsOpen).ToArray();
                Assert.AreEqual(3, kills);

                gm.RestartLevel();
                yield return null;
                Assert.AreEqual(0, gm.Stats.Kills);

                // Load from the title screen, as a player would after relaunching.
                gm.GoToTitle();
                yield return null;
                Assert.IsTrue(gm.LoadGame(slot), "load failed");
                yield return null;
                yield return null;

                level = gm.Level;
                player = gm.Player;
                Assert.AreEqual(GameState.Playing, gm.State);
                Assert.AreEqual(4, gm.Skill);
                Assert.Less(Vector3.Distance(player.transform.position, pos), 0.3f, "player position");
                Assert.AreEqual(57, player.Status.Health);
                Assert.AreEqual(33, player.Status.Ammo[(int)AmmoType.Bullets]);
                Assert.IsTrue(player.Status.Keys[(int)KeyColor.Blue]);
                Assert.IsTrue(player.Status.Weapons[(int)WeaponId.Shotgun]);
                Assert.AreEqual(kills, gm.Stats.Kills);
                Assert.AreEqual(dead, level.Enemies.Count(e => e.IsDead), "corpses");
                Assert.AreEqual(alive, level.Enemies.Count(e => !e.IsDead), "living monsters");
                Assert.AreEqual(pickups, level.GetComponentsInChildren<Pickup>().Length, "pickups");
                CollectionAssert.AreEqual(openDoors, level.GetComponentsInChildren<Door>().Select(d => d.IsOpen).ToArray(), "doors");

                player.Status.God = false;
                player.Status.Damage(1000, Vector3.forward, null);
                Assert.IsFalse(gm.SaveGame(slot + "_dead"), "saving while dead should be refused");
            }
            finally
            {
                SaveSystem.Delete(slot);
                SaveSystem.Delete(slot + "_dead");
            }
        }

        [UnityTest]
        public IEnumerator LiftCarriesPlayerUp()
        {
            var gm = GameManager.I;
            gm.NewGame(3);
            gm.StartLevel(1); // E1M2: lift up to the exit room
            yield return Wait(0.3f);
            var level = gm.Level;
            var player = gm.Player;
            player.Status.God = true;

            var lift = level.LiftAt(21, 5);
            Assert.NotNull(lift, "E1M2 should have a lift");
            Assert.AreEqual(0f, lift.Low, 0.01f);
            Assert.AreEqual(1.5f, lift.High, 0.01f);

            // Standing below and facing north, the use trace should hit the lift's side.
            player.Teleport(level.CellCenter(21, 6), 0f);
            yield return null;
            Assert.IsTrue(Combat.Trace(player.EyePosition, player.transform.forward, 2.2f, player.gameObject, Layers.WorldMask, out var hit));
            Capture(player.Cam, "lift_raised.png");
            var used = hit.collider.GetComponentInParent<IUsable>();
            Assert.AreSame(lift, used, "use trace should hit the lift");
            used.Use(player);
            yield return Wait(0.6f);
            Assert.IsTrue(lift.IsDown, "lift should have lowered");
            Capture(player.Cam, "lift_lowered.png");

            var c = level.CellCenter(21, 5);
            player.Teleport(new Vector3(c.x, lift.Height + 0.05f, c.z), 0f);
            yield return Wait(4.5f);
            Assert.AreEqual(1.5f, lift.Height, 0.01f, "lift should be back up");
            Assert.Greater(player.transform.position.y, 1.4f, "player should ride the lift up");
        }

        [UnityTest]
        public IEnumerator TeleporterMovesPlayerAndMonsters()
        {
            var gm = GameManager.I;
            gm.NewGame(3);
            gm.StartLevel(2); // E1M3: pad in the red key room -> courtyard by the red door
            yield return Wait(0.3f);
            var level = gm.Level;
            var player = gm.Player;
            player.Status.God = true;
            foreach (var e in level.Enemies.ToArray()) e.Damage(100000, Vector3.forward, player.gameObject);

            var destination = level.CellCenter(32, 17);
            var blocker = Enemy.Spawn(EnemyDefs.Get("zombieman"), level.transform, destination, 0f);
            level.Enemies.Add(blocker);

            // Walk east onto the pad with the gamepad.
            player.Teleport(level.CellCenter(28, 5), 90f);
            yield return null;
            Capture(player.Cam, "teleport_pad.png");
            var pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0, 1) });
                yield return Wait(0.35f);
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return Wait(0.3f);
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
            }
            Assert.Less(Vector3.Distance(Flat(player.transform.position), Flat(destination)), 2f, "player should arrive at the destination");
            Assert.IsTrue(blocker.IsDead, "player should telefrag whatever is on the destination");

            // A monster in the red key room follows through the pad once the destination is clear.
            player.Teleport(level.CellCenter(25, 17), 90f);
            var demon = Enemy.Spawn(EnemyDefs.Get("demon"), level.transform, level.CellCenter(24, 4), 90f);
            level.Enemies.Add(demon);
            demon.Alert(player.gameObject);
            float end = Time.time + 8f;
            while (Time.time < end && level.WorldToCell(demon.transform.position).y < 12) yield return null;
            Assert.GreaterOrEqual(level.WorldToCell(demon.transform.position).y, 12, "demon should path through the teleporter");
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);

        [UnityTest]
        public IEnumerator LockedDoorNeedsKey()
        {
            var gm = GameManager.I;
            gm.NewGame(3);
            gm.StartLevel(1);
            yield return Wait(0.3f);
            var door = gm.Level.GetComponentsInChildren<Door>().First(d => d.Key == KeyColor.Blue);
            door.Use(gm.Player);
            yield return Wait(1.5f);
            Assert.IsFalse(door.IsOpen, "locked door opened without key");
            gm.Player.Status.Keys[(int)KeyColor.Blue] = true;
            door.Use(gm.Player);
            yield return Wait(1.5f);
            Assert.IsTrue(door.IsOpen);
        }

        [UnityTest]
        public IEnumerator MonsterChasesAndHurtsPlayer()
        {
            var gm = GameManager.I;
            gm.NewGame(4);
            yield return Wait(0.3f);
            var level = gm.Level;
            var player = gm.Player;

            // E1M1: the player starts facing south down a corridor.
            var imp = Enemy.Spawn(EnemyDefs.Get("imp"), level.transform, level.CellCenter(6, 12), 0f);
            level.Enemies.Add(imp);
            imp.Alert(player.gameObject);
            int health = player.Status.Health;
            yield return Wait(8f);
            Capture(player.Cam, "imp_attack.png");
            Assert.Less(player.Status.Health, health, "imp never hurt the player");
        }

        [UnityTest]
        public IEnumerator GamepadMovesAndShoots()
        {
            var gm = GameManager.I;
            gm.NewGame(3);
            yield return Wait(0.5f);
            var level = gm.Level;
            var player = gm.Player;
            player.Status.God = true;

            var pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                // Walk forward with the left stick.
                var start = player.transform.position;
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0, 1) });
                yield return Wait(0.5f);
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return Wait(0.3f);
                Assert.Greater(Vector3.Distance(start, player.transform.position), 1f, "left stick did not move the player");

                // Face a zombie and hold the trigger.
                var zombiePos = player.transform.position + player.transform.forward * 5f;
                var zombie = Enemy.Spawn(EnemyDefs.Get("zombieman"), level.transform, zombiePos, 0f);
                level.Enemies.Add(zombie);
                int ammo = player.Status.Ammo[(int)AmmoType.Bullets];
                InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f });
                yield return Wait(2.5f);
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
                Assert.Less(player.Status.Ammo[(int)AmmoType.Bullets], ammo, "trigger did not fire");
                Assert.IsTrue(zombie.IsDead, "pistol + autoaim should kill a zombie at 5m");

                // Turn with the right stick.
                float yaw = player.Yaw;
                InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(1, 0) });
                yield return Wait(0.3f);
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
                Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(yaw, player.Yaw)), 20f, "right stick did not turn the player");
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
            }
        }

        [UnityTest]
        public IEnumerator KeyboardMovesAndOpensMenu()
        {
            var gm = GameManager.I;
            gm.NewGame(3);
            yield return Wait(0.5f);
            var player = gm.Player;

            // Batch mode has no focused Game view; by default the Editor drops keyboard input then.
            var focusBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            var backgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var kb = InputSystem.AddDevice<Keyboard>();
            try
            {
                var start = player.transform.position;
                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.W));
                yield return Wait(0.5f);
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return Wait(0.3f);
                Assert.Greater(Vector3.Distance(start, player.transform.position), 1f, "W did not move the player");

                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return null;
                Assert.IsTrue(gm.Menu.IsOpen, "Escape should open the pause menu");
                Assert.AreEqual(0f, Time.timeScale, "game should be paused");

                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return null;
                Assert.IsFalse(gm.Menu.IsOpen, "Escape should close the pause menu");
            }
            finally
            {
                InputSystem.RemoveDevice(kb);
                InputSystem.settings.editorInputBehaviorInPlayMode = focusBehavior;
                InputSystem.settings.backgroundBehavior = backgroundBehavior;
            }
        }
    }
}
