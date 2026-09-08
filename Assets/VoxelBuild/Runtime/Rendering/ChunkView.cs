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
        private Mesh waterMesh;
        private MeshFilter filter;
        private MeshRenderer meshRenderer;
        private MeshRenderer waterRenderer;
        private MeshCollider meshCollider;
        private readonly MeshData data = new MeshData();
        private readonly MeshData translucent = new MeshData();
        private readonly MeshData water = new MeshData();
        private readonly List<Vector3> verts = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<int> tris = new List<int>();
        private Material[] solidMaterials;

        public void Init(Int3 coord, Material terrain, Material crystal, Material waterMaterial, float blockSize, int layer)
        {
            Coord = coord;
            filter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();
            mesh = new Mesh { name = $"Chunk {coord}", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            solidMaterials = new[] { terrain, crystal };
            meshRenderer.sharedMaterials = solidMaterials;
            meshRenderer.shadowCastingMode = ShadowCastingMode.On;
            meshRenderer.receiveShadows = true;
            gameObject.layer = layer;
            transform.position = new Vector3(coord.x, coord.y, coord.z) * (Chunk.Size * blockSize);

            var waterGo = new GameObject("Water");
            waterGo.transform.SetParent(transform, false);
            waterGo.layer = layer;
            waterMesh = new Mesh { name = $"Water {coord}", indexFormat = IndexFormat.UInt32 };
            waterMesh.MarkDynamic();
            waterGo.AddComponent<MeshFilter>().sharedMesh = waterMesh;
            waterRenderer = waterGo.AddComponent<MeshRenderer>();
            waterRenderer.sharedMaterial = waterMaterial;
            waterRenderer.shadowCastingMode = ShadowCastingMode.Off;
            waterRenderer.receiveShadows = true;
            waterRenderer.enabled = false;
        }

        public void Rebuild(IBlockQuery world, IFluidLevels fluids, int sliceY, float blockSize)
        {
            ChunkMesher.Build(world, Coord, sliceY, blockSize, fluids, data, translucent, water);

            // Solid mesh: submesh 0 opaque terrain, submesh 1 refractive crystal. The collider covers both.
            int solidVerts = data.VertexCount + translucent.VertexCount;
            if (solidVerts == 0)
            {
                mesh.Clear();
                meshRenderer.enabled = false;
                meshCollider.enabled = false;
            }
            else
            {
                verts.Clear(); normals.Clear(); uvs.Clear();
                Append(data);
                Append(translucent);
                mesh.Clear();
                mesh.subMeshCount = 2;
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(data.Triangles, 0, false);
                tris.Clear();
                int offset = data.VertexCount;
                foreach (var i in translucent.Triangles) tris.Add(i + offset);
                mesh.SetTriangles(tris, 1, false);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                meshRenderer.enabled = true;
                meshCollider.enabled = true;
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = mesh;
            }

            if (water.VertexCount == 0)
            {
                waterMesh.Clear();
                waterRenderer.enabled = false;
            }
            else
            {
                verts.Clear(); normals.Clear(); uvs.Clear();
                Append(water);
                waterMesh.Clear();
                waterMesh.SetVertices(verts);
                waterMesh.SetNormals(normals);
                waterMesh.SetUVs(0, uvs);
                waterMesh.SetTriangles(water.Triangles, 0, false);
                waterMesh.RecalculateTangents();
                waterMesh.RecalculateBounds();
                waterRenderer.enabled = true;
            }
        }

        private void Append(MeshData src)
        {
            var p = src.Positions;
            var n = src.Normals;
            var u = src.Uvs;
            int count = src.VertexCount;
            for (int i = 0; i < count; i++)
            {
                verts.Add(new Vector3(p[i * 3], p[i * 3 + 1], p[i * 3 + 2]));
                normals.Add(new Vector3(n[i * 3], n[i * 3 + 1], n[i * 3 + 2]));
                uvs.Add(new Vector2(u[i * 2], u[i * 2 + 1]));
            }
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (waterMesh != null) Destroy(waterMesh);
        }
    }
}
