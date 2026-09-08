using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelBuild.Rendering
{
    using VoxelBuild.Core;
    using VoxelBuild.World;

    /// <summary>Renders and collides one chunk. Rebuilt on demand by <see cref="WorldRenderer"/>.</summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public sealed class ChunkView : MonoBehaviour
    {
        public Int3 Coord { get; private set; }

        private Mesh mesh;
        private MeshFilter filter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private readonly MeshData data = new MeshData();
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();

        public void Init(Int3 coord, Material material, float blockSize, int layer)
        {
            Coord = coord;
            filter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();
            mesh = new Mesh { name = $"Chunk {coord}", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;
            gameObject.layer = layer;
            transform.position = new Vector3(coord.x, coord.y, coord.z) * (Chunk.Size * blockSize);
        }

        public void Rebuild(IBlockQuery world, int sliceY, float blockSize)
        {
            ChunkMesher.Build(world, Coord, sliceY, blockSize, data);
            if (data.VertexCount == 0)
            {
                mesh.Clear();
                meshRenderer.enabled = false;
                meshCollider.enabled = false;
                return;
            }

            verts.Clear(); normals.Clear(); uvs.Clear();
            var p = data.Positions;
            var n = data.Normals;
            var u = data.Uvs;
            int count = data.VertexCount;
            for (int i = 0; i < count; i++)
            {
                verts.Add(new Vector3(p[i * 3], p[i * 3 + 1], p[i * 3 + 2]));
                normals.Add(new Vector3(n[i * 3], n[i * 3 + 1], n[i * 3 + 2]));
                uvs.Add(new Vector2(u[i * 2], u[i * 2 + 1]));
            }

            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(data.Triangles, 0, true);
            mesh.RecalculateBounds();

            meshRenderer.enabled = true;
            meshCollider.enabled = true;
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
