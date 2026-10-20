using System.Collections.Generic;
using UnityEngine;

namespace Doom
{
    /// <summary>Wall cell with an exit switch on every face that borders an open cell.</summary>
    public class ExitSwitch : MonoBehaviour, IUsable
    {
        const string Off = "switch_exit";
        const string On = "switch_exit_on";

        bool used;
        MeshRenderer meshRenderer;

        public static ExitSwitch Create(Transform parent, Vector3 center, float size, float ceiling,
            List<Vector2Int> openDirs, float light)
        {
            var go = new GameObject("ExitSwitch");
            go.layer = Layers.World;
            go.transform.SetParent(parent, false);
            go.transform.position = center;

            float h = size * 0.5f;
            var mb = new LevelMeshBuilder();
            foreach (var d in openDirs)
            {
                // Grid +y is world -z. Edges ordered so the face points out of the cell toward the open neighbour.
                Vector3 a, b;
                if (d.x == 1) { a = new Vector3(h, 0, -h); b = new Vector3(h, 0, h); }
                else if (d.x == -1) { a = new Vector3(-h, 0, h); b = new Vector3(-h, 0, -h); }
                else if (d.y == 1) { a = new Vector3(-h, 0, -h); b = new Vector3(h, 0, -h); }
                else { a = new Vector3(h, 0, h); b = new Vector3(-h, 0, h); }
                mb.Wall("step", a, b, -1f, 0f, light);
                mb.Wall(Off, a, b, 0f, size, light);
                mb.Wall("wall_metal", a, b, size, ceiling + 3f, light);
            }

            var mesh = mb.Build("ExitSwitchMesh", out var mats);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0, ceiling * 0.5f, 0);
            box.size = new Vector3(size, ceiling + 2f, size);

            var sw = go.AddComponent<ExitSwitch>();
            sw.meshRenderer = mr;
            return sw;
        }

        public void Use(PlayerController player)
        {
            if (used) return;
            used = true;
            var mats = meshRenderer.sharedMaterials;
            var off = GameAssets.WorldMaterial(Off);
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] == off) mats[i] = GameAssets.WorldMaterial(On);
            meshRenderer.sharedMaterials = mats;
            AudioManager.I.PlayAt("switch", transform.position);
            GameManager.I.ExitLevel(1f);
        }
    }
}
