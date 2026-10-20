using UnityEngine;

namespace Doom
{
    /// <summary>
    /// Sky cylinder that follows the camera's position (not rotation), drawn behind all geometry.
    /// Replace the "sky" texture to change it.
    /// </summary>
    public class Sky : MonoBehaviour
    {
        const int Segments = 32;
        const float Radius = 60f, Bottom = -25f, Top = 55f, Wraps = 4f;

        public static Sky Create(Transform parent)
        {
            var go = new GameObject("Sky");
            go.transform.SetParent(parent, false);

            var verts = new Vector3[(Segments + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var tris = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                float ang = t * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang)) * Radius;
                verts[i * 2] = dir + Vector3.up * Bottom;
                verts[i * 2 + 1] = dir + Vector3.up * Top;
                uvs[i * 2] = new Vector2(t * Wraps, 0);
                uvs[i * 2 + 1] = new Vector2(t * Wraps, 1);
                if (i == Segments) break;
                int k = i * 6, v = i * 2;
                tris[k] = v; tris[k + 1] = v + 1; tris[k + 2] = v + 2;
                tris[k + 3] = v + 2; tris[k + 4] = v + 1; tris[k + 5] = v + 3;
            }

            var mesh = new Mesh { name = "SkyMesh", vertices = verts, uv = uvs, triangles = tris };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1000f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GameAssets.SkyMaterial("sky");
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go.AddComponent<Sky>();
        }

        void LateUpdate()
        {
            var cam = GameManager.I != null ? GameManager.I.ViewCamera : null;
            if (cam) transform.position = cam.transform.position;
        }
    }
}
