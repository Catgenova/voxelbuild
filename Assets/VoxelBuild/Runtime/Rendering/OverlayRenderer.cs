using System.Collections.Generic;
using UnityEngine;

namespace VoxelBuild.Rendering
{
    using VoxelBuild.Core;
    using VoxelBuild.Sim;

    /// <summary>Draws designations, stockpile tiles, the hover cell, the drag box and the selected object outline.</summary>
    public sealed class OverlayRenderer : MonoBehaviour
    {
        private GameContext ctx;
        private WorldRenderer worldRenderer;

        private OutlineMesh mineMesh, buildMesh, drainMesh, pourMesh, stockpileMesh, cursorMesh, selectionMesh;
        private bool ordersDirty = true;

        private static readonly Color MineColor = new Color(1f, 0.55f, 0.1f);
        private static readonly Color BuildColor = new Color(0.3f, 0.7f, 1f);
        private static readonly Color DrainColor = new Color(0.3f, 0.95f, 1f);
        private static readonly Color PourColor = new Color(0.35f, 0.5f, 1f);
        private static readonly Color StockpileColor = new Color(0.35f, 0.55f, 0.3f);
        private static readonly Color CursorColor = new Color(1f, 1f, 1f);
        private static readonly Color SelectionColor = new Color(0.4f, 1f, 0.5f);

        /// <summary>Set by the player tools each frame; null hides the cursor.</summary>
        public IntBox? Cursor;
        public Color CursorTint = Color.white;
        /// <summary>World-space bounds of the selected thing, or null.</summary>
        public Bounds? Selection;

        public void Init(GameContext ctx, WorldRenderer worldRenderer)
        {
            this.ctx = ctx;
            this.worldRenderer = worldRenderer;
            mineMesh = MakeLayer("Mine Orders", MineColor);
            buildMesh = MakeLayer("Build Orders", BuildColor);
            drainMesh = MakeLayer("Drain Orders", DrainColor);
            pourMesh = MakeLayer("Pour Orders", PourColor);
            stockpileMesh = MakeLayer("Stockpiles", StockpileColor);
            cursorMesh = MakeLayer("Cursor", CursorColor);
            selectionMesh = MakeLayer("Selection", SelectionColor);
            ctx.Jobs.Changed += () => ordersDirty = true;
            ctx.Stockpiles.Changed += () => ordersDirty = true;
            worldRenderer.SliceChanged += _ => ordersDirty = true;
        }

        private readonly Dictionary<OutlineMesh, MeshRenderer> renderers = new Dictionary<OutlineMesh, MeshRenderer>();

        private OutlineMesh MakeLayer(string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var om = new OutlineMesh(name);
            go.AddComponent<MeshFilter>().sharedMesh = om.Mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = BlockAtlas.CreateOverlayMaterial(name, color);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            renderers[om] = mr;
            return om;
        }

        private void LateUpdate()
        {
            if (ordersDirty)
            {
                ordersDirty = false;
                RebuildOrders();
            }
            RebuildCursor();
        }

        private void RebuildOrders()
        {
            float bs = worldRenderer.BlockSize;
            float t = Mathf.Min(0.015f, bs * 0.08f);
            int slice = worldRenderer.SliceY;
            mineMesh.Clear();
            buildMesh.Clear();
            drainMesh.Clear();
            pourMesh.Clear();
            foreach (var d in ctx.Jobs.Designations)
            {
                if (d.Cell.y > slice) continue;
                var min = worldRenderer.CellToWorld(d.Cell) + Vector3.one * t;
                var max = worldRenderer.CellToWorld(d.Cell) + Vector3.one * (bs - t);
                switch (d.Kind)
                {
                    case DesignationKind.Mine: mineMesh.AddBoxOutline(min, max, t); break;
                    case DesignationKind.Build: buildMesh.AddBoxOutline(min, max, t); break;
                    case DesignationKind.Drain: drainMesh.AddBoxOutline(min, max, t); break;
                    default: pourMesh.AddBoxOutline(min, max, t); break;
                }
            }
            mineMesh.Apply();
            buildMesh.Apply();
            drainMesh.Apply();
            pourMesh.Apply();

            stockpileMesh.Clear();
            foreach (var c in ctx.Stockpiles.Cells)
            {
                if (c.y > slice) continue;
                var min = worldRenderer.CellToWorld(c);
                float inset = bs * 0.06f;
                stockpileMesh.AddFloorTile(min + new Vector3(inset, 0.01f * bs, inset), min + new Vector3(bs - inset, 0f, bs - inset), bs * 0.03f);
            }
            stockpileMesh.Apply();
        }

        private void RebuildCursor()
        {
            float bs = worldRenderer.BlockSize;
            cursorMesh.Clear();
            if (Cursor.HasValue)
            {
                var box = Cursor.Value;
                var min = worldRenderer.CellToWorld(box.Min) - Vector3.one * (bs * 0.02f);
                var max = worldRenderer.CellToWorld(box.Max + Int3.One) + Vector3.one * (bs * 0.02f);
                cursorMesh.AddBoxOutline(min, max, Mathf.Min(0.02f, bs * 0.1f));
                renderers[cursorMesh].sharedMaterial.SetColor("_EmissiveColor", CursorTint);
            }
            cursorMesh.Apply();

            selectionMesh.Clear();
            if (Selection.HasValue)
            {
                var b = Selection.Value;
                selectionMesh.AddBoxOutline(b.min, b.max, Mathf.Min(0.02f, bs * 0.1f));
            }
            selectionMesh.Apply();
        }
    }
}
