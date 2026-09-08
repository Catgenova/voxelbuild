using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelBuild.Rendering
{
    using VoxelBuild.Core;
    using VoxelBuild.Sim;
    using VoxelBuild.World;

    /// <summary>Shows felled trees toppling: a one-off mesh of the tree hinged at its base, rotating over the fall duration.</summary>
    public sealed class TreeFallRenderer : MonoBehaviour
    {
        private GameContext ctx;
        private WorldRenderer worldRenderer;
        private Material material;
        private readonly MeshData data = new MeshData();
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();

        public void Init(GameContext ctx, WorldRenderer worldRenderer, Material terrainMaterial)
        {
            this.ctx = ctx;
            this.worldRenderer = worldRenderer;
            material = terrainMaterial;
            ctx.Trees.Felled += Spawn;
        }

        private void Spawn(TreeFall fall)
        {
            float bs = worldRenderer.BlockSize;
            // Hinge on the base cell's bottom edge on the side the tree falls towards.
            var pivotCell = worldRenderer.CellToWorld(fall.Base);
            var dir = new Vector3(fall.Direction.x, 0f, fall.Direction.z);
            var pivot = pivotCell + new Vector3(0.5f, 0f, 0.5f) * bs + dir * (0.5f * bs);

            ChunkMesher.BuildCells(fall.Blocks, fall.Base, bs, data);
            if (data.VertexCount == 0) return;
            verts.Clear(); normals.Clear(); uvs.Clear();
            for (int i = 0; i < data.VertexCount; i++)
            {
                verts.Add(new Vector3(data.Positions[i * 3], data.Positions[i * 3 + 1], data.Positions[i * 3 + 2]) + pivotCell - pivot);
                normals.Add(new Vector3(data.Normals[i * 3], data.Normals[i * 3 + 1], data.Normals[i * 3 + 2]));
                uvs.Add(new Vector2(data.Uvs[i * 2], data.Uvs[i * 2 + 1]));
            }
            var mesh = new Mesh { name = "Falling tree", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(data.Triangles, 0, true);
            mesh.RecalculateTangents();

            var go = new GameObject("Falling tree");
            go.transform.SetParent(transform, false);
            go.transform.position = pivot;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.On;
            var effect = go.AddComponent<FallingTreeEffect>();
            effect.Init(ctx.Clock, Vector3.Cross(Vector3.up, dir), fall.Duration, mesh);
        }
    }

    /// <summary>Rotates a felled tree about its hinge with gravity-like acceleration, then removes itself.</summary>
    public sealed class FallingTreeEffect : MonoBehaviour
    {
        private GameClock clock;
        private Vector3 axis;
        private float duration;
        private float elapsed;
        private Mesh mesh;

        public void Init(GameClock clock, Vector3 axis, float duration, Mesh mesh)
        {
            this.clock = clock;
            this.axis = axis;
            this.duration = duration;
            this.mesh = mesh;
        }

        private void Update()
        {
            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.1f) * (clock != null ? clock.Speed : 1f);
            float t = Mathf.Clamp01(elapsed / duration);
            // Slow start, fast finish: angle grows with t^2, with a little bounce at the end.
            float angle = 90f * t * t;
            if (t >= 1f) angle = 90f;
            // Rotating about up x dir carries the trunk (up) towards dir.
            transform.rotation = Quaternion.AngleAxis(angle, axis);
            if (elapsed >= duration + 0.25f)
            {
                Destroy(mesh);
                Destroy(gameObject);
            }
        }
    }
}
