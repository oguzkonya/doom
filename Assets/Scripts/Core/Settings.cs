using UnityEngine;

namespace Doom
{
    /// <summary>Persistent user options, backed by PlayerPrefs.</summary>
    public static class Settings
    {
        public static float MouseSensitivity
        {
            get => PlayerPrefs.GetFloat("mouseSens", 1f);
            set => PlayerPrefs.SetFloat("mouseSens", Mathf.Clamp(value, 0.1f, 4f));
        }

        public static float StickSensitivity
        {
            get => PlayerPrefs.GetFloat("stickSens", 1f);
            set => PlayerPrefs.SetFloat("stickSens", Mathf.Clamp(value, 0.1f, 4f));
        }

        public static bool InvertY
        {
            get => PlayerPrefs.GetInt("invertY", 0) == 1;
            set => PlayerPrefs.SetInt("invertY", value ? 1 : 0);
        }

        /// <summary>Vertical mouse/stick look. Off by default, like the original.</summary>
        public static bool Freelook
        {
            get => PlayerPrefs.GetInt("freelook", 0) == 1;
            set => PlayerPrefs.SetInt("freelook", value ? 1 : 0);
        }

        public static bool AlwaysRun
        {
            get => PlayerPrefs.GetInt("alwaysRun", 1) == 1;
            set => PlayerPrefs.SetInt("alwaysRun", value ? 1 : 0);
        }

        public static bool Autoaim
        {
            get => PlayerPrefs.GetInt("autoaim", 1) == 1;
            set => PlayerPrefs.SetInt("autoaim", value ? 1 : 0);
        }

        public static bool Crosshair
        {
            get => PlayerPrefs.GetInt("crosshair", 0) == 1;
            set => PlayerPrefs.SetInt("crosshair", value ? 1 : 0);
        }

        /// <summary>0 = classic 200p, 1 = 400p, 2 = native resolution.</summary>
        public static int RenderMode
        {
            get => PlayerPrefs.GetInt("renderMode", 1);
            set => PlayerPrefs.SetInt("renderMode", (value % 3 + 3) % 3);
        }

        /// <summary>0 = status bar, 1 = fullscreen minimal HUD.</summary>
        public static int HudStyle
        {
            get => PlayerPrefs.GetInt("hudStyle", 0);
            set => PlayerPrefs.SetInt("hudStyle", (value % 2 + 2) % 2);
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat("sfxVol", 0.8f);
            set => PlayerPrefs.SetFloat("sfxVol", Mathf.Clamp01(value));
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat("musicVol", 0.6f);
            set => PlayerPrefs.SetFloat("musicVol", Mathf.Clamp01(value));
        }

        public static bool Rumble
        {
            get => PlayerPrefs.GetInt("rumble", 1) == 1;
            set => PlayerPrefs.SetInt("rumble", value ? 1 : 0);
        }

        public static bool Fullscreen
        {
            get => Screen.fullScreenMode != FullScreenMode.Windowed;
            set
            {
                if (value)
                    Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
                else
                    Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            }
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
