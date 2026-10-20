using UnityEngine;

namespace Doom
{
    /// <summary>
    /// Spins up the game in whatever scene is loaded. Everything (level geometry, actors, UI)
    /// is generated at runtime, so the scene itself can be empty.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindAnyObjectByType<GameManager>() != null) return;

            // Template scenes ship with their own camera / listener; the game manages its own.
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                Object.Destroy(cam.gameObject);

            new GameObject("DOOM").AddComponent<GameManager>();
        }
    }

    public static class Layers
    {
        public const int World = 8;
        public const int Actor = 9;
        public const int WorldMask = 1 << World;
        public const int ActorMask = 1 << Actor;
        public const int ShotMask = WorldMask | ActorMask;
    }
}
