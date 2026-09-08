using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelBuild.Rendering
{
    /// <summary>Builds a single mesh of box outlines (thin edge bars) and flat floor quads for overlays.</summary>
    public sealed class OutlineMesh
    {
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<int> tris = new List<int>();
        private readonly Mesh mesh;

        public Mesh Mesh => mesh;

        public OutlineMesh(string name)
        {
            mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
        }

        public void Clear()
        {
            verts.Clear();
            tris.Clear();
        }

        /// <summary>Adds the 12 edges of a box as bars of the given thickness.</summary>
        public void AddBoxOutline(Vector3 min, Vector3 max, float thickness)
        {
            float t = thickness;
            // Edges along X
            for (int i = 0; i < 4; i++)
            {
                float y = (i & 1) == 0 ? min.y : max.y;
                float z = (i & 2) == 0 ? min.z : max.z;
                AddBar(new Vector3(min.x, y - t, z - t), new Vector3(max.x, y + t, z + t));
            }
            // Edges along Y
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? min.x : max.x;
                float z = (i & 2) == 0 ? min.z : max.z;
                AddBar(new Vector3(x - t, min.y, z - t), new Vector3(x + t, max.y, z + t));
            }
            // Edges along Z
            for (int i = 0; i < 4; i++)
            {
                float x = (i & 1) == 0 ? min.x : max.x;
                float y = (i & 2) == 0 ? min.y : max.y;
                AddBar(new Vector3(x - t, y - t, min.z), new Vector3(x + t, y + t, max.z));
            }
        }

        /// <summary>Adds a solid box (used for bars and for thin floor tiles).</summary>
        public void AddBar(Vector3 min, Vector3 max)
        {
            int b = verts.Count;
            verts.Add(new Vector3(min.x, min.y, min.z)); // 0
            verts.Add(new Vector3(max.x, min.y, min.z)); // 1
            verts.Add(new Vector3(max.x, max.y, min.z)); // 2
            verts.Add(new Vector3(min.x, max.y, min.z)); // 3
            verts.Add(new Vector3(min.x, min.y, max.z)); // 4
            verts.Add(new Vector3(max.x, min.y, max.z)); // 5
            verts.Add(new Vector3(max.x, max.y, max.z)); // 6
            verts.Add(new Vector3(min.x, max.y, max.z)); // 7

            Quad(b, 0, 3, 2, 1); // -Z
            Quad(b, 4, 5, 6, 7); // +Z
            Quad(b, 0, 4, 7, 3); // -X
            Quad(b, 1, 2, 6, 5); // +X
            Quad(b, 3, 7, 6, 2); // +Y
            Quad(b, 0, 1, 5, 4); // -Y
        }

        /// <summary>Adds a flat tile lying on the floor (a very thin bar).</summary>
        public void AddFloorTile(Vector3 min, Vector3 max, float height)
        {
            AddBar(min, new Vector3(max.x, min.y + height, max.z));
        }

        private void Quad(int b, int a, int c, int d, int e)
        {
            tris.Add(b + a); tris.Add(b + c); tris.Add(b + d);
            tris.Add(b + a); tris.Add(b + d); tris.Add(b + e);
        }

        public void Apply()
        {
            mesh.Clear();
            if (verts.Count == 0) return;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0, true);
            mesh.RecalculateNormals();
        }
    }
}
