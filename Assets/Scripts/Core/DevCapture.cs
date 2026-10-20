using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Doom
{
    /// <summary>
    /// Developer aid: launch the player with "-doomcapture &lt;folder&gt;" to walk through the title,
    /// gameplay, automap, pause menu and intermission, saving a screenshot of each, then quit.
    /// Inactive unless the flag is present.
    /// </summary>
    public class DevCapture : MonoBehaviour
    {
        string folder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-doomcapture");
            if (i < 0 || i + 1 >= args.Length) return;
            new GameObject("DevCapture").AddComponent<DevCapture>().folder = args[i + 1];
        }

        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Directory.CreateDirectory(folder);
            yield return Wait(1.5f);
            Shot("01_title_menu");

            var gm = GameManager.I;
            gm.NewGame(3);
            yield return Wait(2f);
            Shot("02_e1m1");

            gm.Player.Status.GiveEverything(true);
            gm.Player.Weapons.Select((int)WeaponId.Shotgun);
            gm.Player.Status.Damage(30, Vector3.forward, null);
            yield return Wait(0.8f);
            Shot("03_shotgun_hurt");

            gm.AutomapOpen = true;
            yield return Wait(0.5f);
            Shot("04_automap");
            gm.AutomapOpen = false;

            gm.Menu.OpenPause();
            yield return Wait(0.5f);
            Shot("05_pause_menu");
            gm.Menu.CloseAll();

            gm.StartLevel(2);
            yield return Wait(2f);
            Shot("06_e1m3");

            gm.ExitLevel(0.01f);
            yield return Wait(3f);
            Shot("07_intermission");

            yield return Wait(0.5f);
            Application.Quit();
        }

        static IEnumerator Wait(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        void Shot(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
    }
}
