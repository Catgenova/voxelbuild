using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace VoxelBuild.Player
{
    using VoxelBuild.Core;
    using VoxelBuild.Rendering;
    using VoxelBuild.Sim;

    public enum Tool
    {
        Select,
        Mine,
        Build,
        Stockpile,
        Cancel,
    }

    /// <summary>Mouse and keyboard interaction with the world: designations, zones, selection and the view slice.</summary>
    public sealed class PlayerTools : MonoBehaviour
    {
        private GameContext ctx;
        private WorldRenderer worldRenderer;
        private OverlayRenderer overlay;
        private Camera cam;
        private Func<ColonistCore, ColonistView> findView;

        public Tool CurrentTool { get; private set; } = Tool.Select;
        public BlockType BuildType { get; private set; } = BlockType.Planks;
        public ColonistCore SelectedColonist { get; private set; }
        public Int3? SelectedCell { get; private set; }
        /// <summary>Hovered cell for the HUD's coordinate readout.</summary>
        public Int3? HoverCell { get; private set; }

        public event Action SelectionChanged;
        public event Action ToolChanged;

        private bool dragging;
        private Int3 dragAnchor;
        private Int3 lastHover;
        private const int TerrainMask = 1 << WorldRenderer.TerrainLayer;
        private const int ColonistMask = 1 << ColonistView.ColonistLayer;

        public void Init(GameContext ctx, WorldRenderer worldRenderer, OverlayRenderer overlay, Camera cam, Func<ColonistCore, ColonistView> findView)
        {
            this.ctx = ctx;
            this.worldRenderer = worldRenderer;
            this.overlay = overlay;
            this.cam = cam;
            this.findView = findView;
        }

        public static bool PointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public void SetTool(Tool tool)
        {
            if (CurrentTool == tool) return;
            CurrentTool = tool;
            dragging = false;
            ToolChanged?.Invoke();
        }

        public void SetBuildType(BlockType type)
        {
            BuildType = type;
            SetTool(Tool.Build);
            ToolChanged?.Invoke();
        }

        public void SelectColonist(ColonistCore c)
        {
            SelectedColonist = c;
            SelectedCell = null;
            SelectionChanged?.Invoke();
        }

        public void SelectCell(Int3? cell)
        {
            SelectedColonist = null;
            SelectedCell = cell;
            SelectionChanged?.Invoke();
        }

        public void ClearSelection()
        {
            if (SelectedColonist == null && SelectedCell == null) return;
            SelectedColonist = null;
            SelectedCell = null;
            SelectionChanged?.Invoke();
        }

        private void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null || cam == null) return;

            HandleHotkeys(kb, mouse);

            bool overUi = PointerOverUi();
            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            bool hitTerrain = Physics.Raycast(ray, out var hit, 4000f, TerrainMask);
            float bs = worldRenderer.BlockSize;
            Int3 blockCell = default, adjacentCell = default;
            if (hitTerrain)
            {
                blockCell = worldRenderer.WorldToCell(hit.point - hit.normal * (bs * 0.5f));
                adjacentCell = worldRenderer.WorldToCell(hit.point + hit.normal * (bs * 0.5f));
            }

            bool useAdjacent = CurrentTool == Tool.Build || CurrentTool == Tool.Stockpile;
            Int3 hover = useAdjacent ? adjacentCell : blockCell;
            if (hitTerrain) lastHover = hover;
            HoverCell = hitTerrain ? hover : (Int3?)null;

            if (!overUi && mouse.leftButton.wasPressedThisFrame)
            {
                if (CurrentTool == Tool.Select)
                {
                    if (Physics.Raycast(ray, out var chit, 4000f, ColonistMask))
                    {
                        var view = chit.collider.GetComponentInParent<ColonistView>();
                        if (view != null) SelectColonist(view.Core);
                    }
                    else if (hitTerrain) SelectCell(blockCell);
                    else ClearSelection();
                }
                else if (hitTerrain)
                {
                    dragging = true;
                    dragAnchor = hover;
                }
            }

            if (mouse.rightButton.wasPressedThisFrame && !(kb.leftAltKey.isPressed))
            {
                if (dragging) dragging = false;
                else if (CurrentTool != Tool.Select) SetTool(Tool.Select);
                else ClearSelection();
            }

            if (dragging)
            {
                var box = new IntBox(dragAnchor, lastHover);
                overlay.Cursor = box;
                overlay.CursorTint = TintFor(CurrentTool);
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    dragging = false;
                    Apply(box);
                }
            }
            else
            {
                overlay.Cursor = hitTerrain && !overUi ? new IntBox(hover, hover) : (IntBox?)null;
                overlay.CursorTint = TintFor(CurrentTool);
            }

            UpdateSelectionOutline();
        }

        private void HandleHotkeys(Keyboard kb, Mouse mouse)
        {
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (dragging) dragging = false;
                else if (CurrentTool != Tool.Select) SetTool(Tool.Select);
                else ClearSelection();
            }
            if (kb.digit1Key.wasPressedThisFrame) SetTool(Tool.Select);
            if (kb.digit2Key.wasPressedThisFrame) SetTool(Tool.Mine);
            if (kb.digit3Key.wasPressedThisFrame) SetTool(Tool.Build);
            if (kb.digit4Key.wasPressedThisFrame) SetTool(Tool.Stockpile);
            if (kb.digit5Key.wasPressedThisFrame) SetTool(Tool.Cancel);

            int sliceDelta = 0;
            if (kb.pageDownKey.wasPressedThisFrame || kb.leftBracketKey.wasPressedThisFrame) sliceDelta--;
            if (kb.pageUpKey.wasPressedThisFrame || kb.rightBracketKey.wasPressedThisFrame) sliceDelta++;
            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            float scroll = mouse.scroll.ReadValue().y;
            if (ctrl && Mathf.Abs(scroll) > 0.01f) sliceDelta += scroll > 0f ? 1 : -1;
            if (kb.homeKey.wasPressedThisFrame) worldRenderer.SliceY = worldRenderer.MaxSliceY;
            if (sliceDelta != 0) worldRenderer.SliceY += sliceDelta * (kb.leftShiftKey.isPressed ? 4 : 1);
        }

        private static Color TintFor(Tool t)
        {
            switch (t)
            {
                case Tool.Mine: return new Color(1f, 0.6f, 0.2f);
                case Tool.Build: return new Color(0.4f, 0.75f, 1f);
                case Tool.Stockpile: return new Color(0.5f, 0.9f, 0.5f);
                case Tool.Cancel: return new Color(1f, 0.3f, 0.3f);
                default: return Color.white;
            }
        }

        private void Apply(IntBox box)
        {
            switch (CurrentTool)
            {
                case Tool.Mine:
                {
                    int n = ctx.Jobs.AddMineBox(box);
                    if (n > 0) ctx.Log($"Designated {n} blocks for mining");
                    break;
                }
                case Tool.Build:
                {
                    int n = ctx.Jobs.AddBuildBox(box, BuildType);
                    var def = BlockRegistry.Get(BuildType);
                    if (n > 0) ctx.Log($"Planned {n} x {def.Name} (needs {def.BuildCost.Count * n} {ItemRegistry.Get(def.BuildCost.Type).Name})");
                    break;
                }
                case Tool.Stockpile:
                    ctx.Stockpiles.AddBox(box);
                    break;
                case Tool.Cancel:
                {
                    int n = ctx.Jobs.CancelBox(box);
                    ctx.Stockpiles.RemoveBox(box);
                    if (n > 0) ctx.Log($"Cancelled {n} orders");
                    break;
                }
            }
        }

        private void UpdateSelectionOutline()
        {
            if (SelectedColonist != null)
            {
                var view = findView(SelectedColonist);
                overlay.Selection = view != null ? view.Bounds : (Bounds?)null;
            }
            else if (SelectedCell.HasValue)
            {
                float bs = worldRenderer.BlockSize;
                overlay.Selection = new Bounds(worldRenderer.CellCenter(SelectedCell.Value), Vector3.one * bs * 1.04f);
            }
            else overlay.Selection = null;
        }
    }
}
