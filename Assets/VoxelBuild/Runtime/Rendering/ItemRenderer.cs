using System.Collections.Generic;
using UnityEngine;

namespace VoxelBuild.Rendering
{
    using VoxelBuild.Core;
    using VoxelBuild.Sim;

    /// <summary>Shows ground item piles as small cubes coloured by their dominant item.</summary>
    public sealed class ItemRenderer : MonoBehaviour
    {
        public const int ItemLayer = 8;

        private GroundItems items;
        private WorldRenderer worldRenderer;
        private readonly Dictionary<Int3, GameObject> views = new Dictionary<Int3, GameObject>();
        private readonly Dictionary<ItemType, Material> materials = new Dictionary<ItemType, Material>();
        private readonly HashSet<Int3> pending = new HashSet<Int3>();

        public void Init(GroundItems items, WorldRenderer worldRenderer)
        {
            this.items = items;
            this.worldRenderer = worldRenderer;
            items.CellChanged += c => pending.Add(c);
            worldRenderer.SliceChanged += _ => RefreshVisibility();
            foreach (var c in items.Cells) pending.Add(c);
        }

        private void LateUpdate()
        {
            if (pending.Count == 0) return;
            foreach (var c in pending) Refresh(c);
            pending.Clear();
        }

        private Material MaterialFor(ItemType type)
        {
            if (!materials.TryGetValue(type, out var m))
            {
                m = BlockAtlas.CreateSolidMaterial("Item " + type, BlockAtlas.ToColor(ItemRegistry.Get(type).Color), 0.35f);
                materials[type] = m;
            }
            return m;
        }

        private void Refresh(Int3 cell)
        {
            int total = items.TotalInCell(cell);
            if (total <= 0)
            {
                if (views.TryGetValue(cell, out var old))
                {
                    Destroy(old);
                    views.Remove(cell);
                }
                return;
            }

            ItemType dominant = ItemType.None;
            int best = 0;
            foreach (var s in items.StacksIn(cell))
                if (s.Count > best) { best = s.Count; dominant = s.Type; }

            if (!views.TryGetValue(cell, out var go))
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Pile {cell}";
                go.layer = ItemLayer;
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                views[cell] = go;
            }

            // Pile size in metres, so piles stay readable whatever the block size.
            float size = 0.16f + 0.24f * Mathf.Clamp01(total / (float)GroundItems.MaxPerCell);
            go.transform.localScale = new Vector3(size, size * 0.7f, size);
            go.transform.position = worldRenderer.CellFloorCenter(cell) + new Vector3(0f, size * 0.35f, 0f);
            go.GetComponent<MeshRenderer>().sharedMaterial = MaterialFor(dominant);
            go.SetActive(cell.y <= worldRenderer.SliceY);
        }

        private void RefreshVisibility()
        {
            foreach (var kv in views) kv.Value.SetActive(kv.Key.y <= worldRenderer.SliceY);
        }
    }
}
