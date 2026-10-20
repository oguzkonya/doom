using System.IO;
using UnityEditor;
using UnityEngine;

namespace Doom.EditorTools
{
    /// <summary>
    /// Writes every procedural placeholder out as a real file, using the exact name the game looks up.
    /// The files land in a staging folder; edit/replace them and move them into the matching
    /// Assets/Resources/... folder (or export straight into Resources to override everything at once).
    /// </summary>
    public static class PlaceholderExporter
    {
        const string StagingRoot = "Assets/PlaceholderExport";

        [MenuItem("Doom/Export Placeholder Assets (staging folder)")]
        public static void ExportToStaging() => Export(StagingRoot + "/Art/Textures", StagingRoot + "/Art/Sprites", StagingRoot + "/Audio");

        [MenuItem("Doom/Export Placeholder Assets (into Resources, overrides)")]
        public static void ExportToResources()
        {
            if (!EditorUtility.DisplayDialog("Export into Resources",
                    "This writes every placeholder into Assets/Resources so they can be edited in place. Existing files with the same name are skipped. Continue?",
                    "Export", "Cancel"))
                return;
            Export("Assets/Resources/Art/Textures", "Assets/Resources/Art/Sprites", "Assets/Resources/Audio");
        }

        static void Export(string texDir, string spriteDir, string audioDir)
        {
            Directory.CreateDirectory(texDir);
            Directory.CreateDirectory(spriteDir);
            Directory.CreateDirectory(audioDir);
            int count = 0;

            foreach (var name in PlaceholderArt.AllTextureNames)
                count += WritePng(Path.Combine(texDir, name + ".png"), PlaceholderArt.Texture(name));
            foreach (var name in PlaceholderArt.AllSpriteNames)
                count += WritePng(Path.Combine(spriteDir, name + ".png"), PlaceholderArt.Sprite(name));
            foreach (var name in PlaceholderAudio.AllNames)
            {
                var path = Path.Combine(audioDir, name + ".wav");
                if (File.Exists(path)) continue;
                WriteWav(path, PlaceholderAudio.Make(name));
                count++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[Doom] Exported {count} placeholder assets.");
        }

        static int WritePng(string path, Texture2D tex)
        {
            if (File.Exists(path)) return 0;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return 1;
        }

        static void WriteWav(string path, AudioClip clip)
        {
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);
            using var fs = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(fs);
            int byteCount = samples.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + byteCount);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)clip.channels);
            w.Write(clip.frequency);
            w.Write(clip.frequency * clip.channels * 2);
            w.Write((short)(clip.channels * 2));
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(byteCount);
            foreach (var s in samples) w.Write((short)Mathf.Clamp(s * 32767f, short.MinValue, short.MaxValue));
        }
    }

    /// <summary>Retro-friendly import settings for anything placed under a Resources/Art folder.</summary>
    public class DoomArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Art/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.wrapMode = assetPath.Contains("/Textures/") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        }
    }
}
