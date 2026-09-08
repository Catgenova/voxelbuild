using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace VoxelBuild.UI
{
    using VoxelBuild.Core;
    using VoxelBuild.Player;
    using VoxelBuild.Rendering;
    using VoxelBuild.Sim;

    /// <summary>The in-game HUD: clock and speed, tools, colonist roster, context panel, resources and log.</summary>
    public sealed class Hud : MonoBehaviour
    {
        private GameContext ctx;
        private PlayerTools tools;
        private WorldRenderer worldRenderer;
        private IsometricCameraController cameraController;

        private Text clockText, sliceText, resourceText, hoverText, hintText;
        private readonly List<Text> logLines = new List<Text>();
        private readonly List<string> logHistory = new List<string>();
        private readonly Button[] speedButtons = new Button[4];
        private readonly Dictionary<Tool, Button> toolButtons = new Dictionary<Tool, Button>();
        private readonly Dictionary<BlockType, Button> blockButtons = new Dictionary<BlockType, Button>();
        private RectTransform blockList;
        private RectTransform colonistBar;
        private RectTransform contextPanel;
        private RectTransform contextBody;
        private Text contextTitle;

        private readonly List<ColonistCard> cards = new List<ColonistCard>();
        private bool contextDirty = true;
        private bool handCrafting;
        private float refreshTimer;

        private sealed class ColonistCard
        {
            public ColonistCore Core;
            public Button Root;
            public Text Activity;
            public RectTransform Food, Rest;
        }

        public void Init(GameContext ctx, PlayerTools tools, WorldRenderer worldRenderer, IsometricCameraController cameraController, Font font)
        {
            this.ctx = ctx;
            this.tools = tools;
            this.worldRenderer = worldRenderer;
            this.cameraController = cameraController;
            UiFactory.Font = font;

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            var canvas = UiFactory.CreateCanvas("HUD");
            canvas.transform.SetParent(transform, false);
            var root = (RectTransform)canvas.transform;

            BuildTopBar(root);
            BuildToolBar(root);
            BuildResourcePanel(root);
            BuildColonistBar(root);
            BuildContextPanel(root);
            BuildLog(root);

            ctx.Log = Log;
            ctx.Jobs.Changed += () => contextDirty = true;
            tools.SelectionChanged += () => { handCrafting = false; contextDirty = true; };
            tools.ToolChanged += RefreshToolButtons;
            ctx.Clock.SpeedChanged += _ => RefreshSpeedButtons();
            worldRenderer.SliceChanged += _ => RefreshSlice();
            foreach (var c in ctx.Colonists) c.JobChanged += changed => { if (tools.SelectedColonist == changed) contextDirty = true; };

            RefreshToolButtons();
            RefreshSpeedButtons();
            RefreshSlice();
            Log("Welcome. Drag with the Mine tool to dig, build a Workbench from planks, and keep your colonists fed.");
        }

        // ------------------------------------------------------------------ Layout

        private void BuildTopBar(RectTransform root)
        {
            var bar = UiFactory.Panel(root, "TopBar", UiFactory.PanelColor);
            UiFactory.Anchor(bar, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(760f, 40f));
            UiFactory.Horizontal(bar, 8f, 6);

            clockText = UiFactory.Label(bar, "Day 1 00:00", 16);
            clockText.GetComponent<LayoutElement>().preferredWidth = 150f;
            clockText.GetComponent<LayoutElement>().flexibleWidth = 0f;

            string[] names = { "||", "1x", "2x", "3x" };
            for (int i = 0; i < 4; i++)
            {
                int speed = i;
                speedButtons[i] = UiFactory.Button(bar, names[i], () => SetSpeed(speed), 40f);
            }

            var spacer = UiFactory.Group(bar, "Spacer");
            spacer.gameObject.AddComponent<LayoutElement>().preferredWidth = 20f;

            UiFactory.Button(bar, "Level -", () => worldRenderer.SliceY -= 1, 70f);
            sliceText = UiFactory.Label(bar, "Level", 15, TextAnchor.MiddleCenter);
            sliceText.GetComponent<LayoutElement>().preferredWidth = 90f;
            sliceText.GetComponent<LayoutElement>().flexibleWidth = 0f;
            UiFactory.Button(bar, "Level +", () => worldRenderer.SliceY += 1, 70f);
            UiFactory.Button(bar, "Top", () => worldRenderer.SliceY = worldRenderer.MaxSliceY, 50f);

            hoverText = UiFactory.Label(bar, "", 13, TextAnchor.MiddleRight, UiFactory.DimTextColor);
        }

        private void BuildToolBar(RectTransform root)
        {
            var panel = UiFactory.Panel(root, "ToolBar", UiFactory.PanelColor);
            UiFactory.Anchor(panel, new Vector2(0f, 1f), new Vector2(8f, -60f), new Vector2(190f, 0f));
            UiFactory.Vertical(panel, 4f, 6);

            UiFactory.Label(panel, "Orders", 15, TextAnchor.MiddleLeft, UiFactory.DimTextColor);
            AddToolButton(panel, Tool.Select, "Select  [1]");
            AddToolButton(panel, Tool.Mine, "Mine  [2]");
            AddToolButton(panel, Tool.Build, "Build  [3]");
            AddToolButton(panel, Tool.Stockpile, "Stockpile  [4]");
            AddToolButton(panel, Tool.Cancel, "Cancel  [5]");
            UiFactory.Button(panel, "Craft by hand", () => { handCrafting = true; tools.ClearSelection(); contextDirty = true; });

            UiFactory.Label(panel, "Build with", 15, TextAnchor.MiddleLeft, UiFactory.DimTextColor);
            blockList = UiFactory.Group(panel, "Blocks");
            UiFactory.Vertical(blockList, 2f, 0);
            foreach (var def in BlockRegistry.Buildable())
            {
                var type = def.Type;
                var b = UiFactory.Button(blockList, def.Name, () => tools.SetBuildType(type), 0f, 24f, 13);
                blockButtons[type] = b;
            }

            hintText = UiFactory.Label(panel,
                "WASD pan  ·  Q/E rotate  ·  Scroll zoom\nPgUp/PgDn or Ctrl+Scroll: view level\nRight-click: back to Select  ·  Space: pause",
                11, TextAnchor.UpperLeft, UiFactory.DimTextColor);
        }

        private void AddToolButton(RectTransform panel, Tool tool, string label)
        {
            toolButtons[tool] = UiFactory.Button(panel, label, () => tools.SetTool(tool));
        }

        private void BuildResourcePanel(RectTransform root)
        {
            var panel = UiFactory.Panel(root, "Resources", UiFactory.PanelColor);
            UiFactory.Anchor(panel, new Vector2(1f, 1f), new Vector2(-8f, -60f), new Vector2(200f, 0f));
            UiFactory.Vertical(panel, 2f, 6);
            UiFactory.Label(panel, "Resources", 15, TextAnchor.MiddleLeft, UiFactory.DimTextColor);
            resourceText = UiFactory.Label(panel, "", 13, TextAnchor.UpperLeft);
        }

        private void BuildColonistBar(RectTransform root)
        {
            colonistBar = UiFactory.Group(root, "Colonists");
            UiFactory.Anchor(colonistBar, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(0f, 74f));
            UiFactory.Horizontal(colonistBar, 6f, 0, true);
            foreach (var c in ctx.Colonists) AddColonistCard(c);
        }

        private void AddColonistCard(ColonistCore core)
        {
            var btn = UiFactory.Button(colonistBar, "", () => OnColonistCardClicked(core), 170f, 74f);
            btn.GetComponent<Image>().color = UiFactory.PanelColor;
            var text = btn.GetComponentInChildren<Text>();
            if (text != null) Destroy(text.gameObject);
            var rt = (RectTransform)btn.transform;
            UiFactory.Vertical(rt, 2f, 6, false);

            var name = UiFactory.Label(rt, core.Name, 14);
            name.color = BlockAtlas.ToColor(core.Color) * 0.6f + Color.white * 0.4f;
            var activity = UiFactory.Label(rt, "Idle", 12, TextAnchor.MiddleLeft, UiFactory.DimTextColor);
            var food = UiFactory.Bar(rt, new Color(0.9f, 0.6f, 0.2f), 7f);
            var rest = UiFactory.Bar(rt, new Color(0.4f, 0.55f, 1f), 7f);
            cards.Add(new ColonistCard { Core = core, Root = btn, Activity = activity, Food = food, Rest = rest });
        }

        private void OnColonistCardClicked(ColonistCore core)
        {
            if (tools.SelectedColonist == core)
                cameraController.FocusOn(new Vector3(core.Mover.PosX, core.Mover.PosY, core.Mover.PosZ) * worldRenderer.BlockSize);
            tools.SelectColonist(core);
        }

        private void BuildContextPanel(RectTransform root)
        {
            contextPanel = UiFactory.Panel(root, "Context", UiFactory.PanelColor);
            UiFactory.Anchor(contextPanel, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(300f, 0f));
            UiFactory.Vertical(contextPanel, 4f, 8);
            contextTitle = UiFactory.Label(contextPanel, "", 16);
            contextBody = UiFactory.Group(contextPanel, "Body");
            UiFactory.Vertical(contextBody, 3f, 0);
            contextPanel.gameObject.SetActive(false);
        }

        private void BuildLog(RectTransform root)
        {
            var panel = UiFactory.Group(root, "Log");
            UiFactory.Anchor(panel, new Vector2(0f, 0f), new Vector2(8f, 8f), new Vector2(520f, 0f));
            UiFactory.Vertical(panel, 0f, 4);
            for (int i = 0; i < 4; i++)
                logLines.Add(UiFactory.Label(panel, "", 12, TextAnchor.LowerLeft, UiFactory.DimTextColor));
        }

        // ------------------------------------------------------------------ Behaviour

        private void SetSpeed(int speed)
        {
            ctx.Clock.Speed = speed;
        }

        public void Log(string message)
        {
            logHistory.Add(message);
            if (logHistory.Count > 40) logHistory.RemoveAt(0);
            for (int i = 0; i < logLines.Count; i++)
            {
                int idx = logHistory.Count - logLines.Count + i;
                logLines[i].text = idx >= 0 ? logHistory[idx] : "";
                float age = logLines.Count - 1 - i;
                logLines[i].color = Color.Lerp(UiFactory.TextColor, UiFactory.DimTextColor, age / logLines.Count);
            }
        }

        private void RefreshToolButtons()
        {
            foreach (var kv in toolButtons) UiFactory.SetActive(kv.Value, kv.Key == tools.CurrentTool);
            foreach (var kv in blockButtons) UiFactory.SetActive(kv.Value, tools.CurrentTool == Tool.Build && kv.Key == tools.BuildType);
        }

        private void RefreshSpeedButtons()
        {
            int s = Mathf.RoundToInt(ctx.Clock.Speed);
            for (int i = 0; i < speedButtons.Length; i++) UiFactory.SetActive(speedButtons[i], i == s);
        }

        private void RefreshSlice()
        {
            sliceText.text = worldRenderer.SliceY >= worldRenderer.MaxSliceY ? "Level: top" : $"Level {worldRenderer.SliceY}";
        }

        private void Update()
        {
            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 0.2f;
                RefreshPeriodic();
            }
            if (contextDirty)
            {
                contextDirty = false;
                RebuildContext();
            }
        }

        private readonly StringBuilder sb = new StringBuilder();

        private void RefreshPeriodic()
        {
            clockText.text = ctx.Clock.ClockText() + (ctx.Clock.IsNight ? "  (night)" : "");
            hoverText.text = tools.HoverCell.HasValue
                ? $"{BlockRegistry.Get(ctx.World.GetBlock(tools.HoverCell.Value)).Name} {tools.HoverCell.Value}"
                : "";

            sb.Clear();
            foreach (var def in ItemRegistry.All())
            {
                int n = ctx.TotalItems(def.Type);
                if (n > 0) sb.Append(def.Name).Append(": ").Append(n).Append('\n');
            }
            if (ctx.Jobs.DesignationCount > 0) sb.Append('\n').Append("Orders: ").Append(ctx.Jobs.DesignationCount);
            resourceText.text = sb.Length > 0 ? sb.ToString() : "Nothing yet";

            foreach (var card in cards)
            {
                card.Activity.text = card.Core.Activity;
                UiFactory.SetBar(card.Food, card.Core.Needs.Food);
                UiFactory.SetBar(card.Rest, card.Core.Needs.Rest);
                card.Root.GetComponent<Image>().color = tools.SelectedColonist == card.Core
                    ? new Color(0.25f, 0.3f, 0.2f, 0.95f)
                    : UiFactory.PanelColor;
            }

            if (tools.SelectedColonist != null && contextPanel.gameObject.activeSelf) RefreshColonistContextValues();
        }

        // ------------------------------------------------------------------ Context panel

        private readonly List<Text> dynamicTexts = new List<Text>();
        private readonly List<RectTransform> dynamicBars = new List<RectTransform>();

        private void RebuildContext()
        {
            UiFactory.Clear(contextBody);
            dynamicTexts.Clear();
            dynamicBars.Clear();

            if (tools.SelectedColonist != null)
            {
                BuildColonistContext(tools.SelectedColonist);
            }
            else if (handCrafting)
            {
                contextTitle.text = "Craft by hand";
                UiFactory.Label(contextBody, "Colonists with Craft enabled will work these anywhere.", 12, TextAnchor.UpperLeft, UiFactory.DimTextColor);
                BuildBillList(null, BlockType.Air);
            }
            else if (tools.SelectedCell.HasValue)
            {
                BuildCellContext(tools.SelectedCell.Value);
            }
            else
            {
                contextPanel.gameObject.SetActive(false);
                return;
            }
            contextPanel.gameObject.SetActive(true);
        }

        private void BuildColonistContext(ColonistCore c)
        {
            contextTitle.text = c.Name;
            dynamicTexts.Add(UiFactory.Label(contextBody, "", 13));
            UiFactory.Label(contextBody, "Food", 12, TextAnchor.MiddleLeft, UiFactory.DimTextColor);
            dynamicBars.Add(UiFactory.Bar(contextBody, new Color(0.9f, 0.6f, 0.2f)));
            UiFactory.Label(contextBody, "Rest", 12, TextAnchor.MiddleLeft, UiFactory.DimTextColor);
            dynamicBars.Add(UiFactory.Bar(contextBody, new Color(0.4f, 0.55f, 1f)));
            UiFactory.Label(contextBody, "Mood", 12, TextAnchor.MiddleLeft, UiFactory.DimTextColor);
            dynamicBars.Add(UiFactory.Bar(contextBody, new Color(0.5f, 0.85f, 0.4f)));

            UiFactory.Label(contextBody, "Carrying", 13, TextAnchor.MiddleLeft, UiFactory.DimTextColor);
            dynamicTexts.Add(UiFactory.Label(contextBody, "", 12));

            UiFactory.Label(contextBody, "Work priorities (click to cycle, 0 = never)", 13, TextAnchor.MiddleLeft, UiFactory.DimTextColor);
            for (int i = 0; i < (int)WorkType.Count; i++)
            {
                var w = (WorkType)i;
                var row = UiFactory.Row(contextBody, w.ToString());
                UiFactory.Label(row, w.ToString(), 13);
                Button b = null;
                b = UiFactory.Button(row, c.Work.Get(w).ToString(), () =>
                {
                    c.Work.Cycle(w);
                    UiFactory.SetText(b, c.Work.Get(w).ToString());
                    UiFactory.SetActive(b, c.Work.Get(w) > 0);
                }, 44f, 24f);
                UiFactory.SetActive(b, c.Work.Get(w) > 0);
            }

            var actions = UiFactory.Row(contextBody, "Actions");
            UiFactory.Button(actions, "Focus camera", () =>
                cameraController.FocusOn(new Vector3(c.Mover.PosX, c.Mover.PosY, c.Mover.PosZ) * worldRenderer.BlockSize), 120f, 26f);
            UiFactory.Button(actions, "Drop job", () => c.Interrupt(), 90f, 26f);
            RefreshColonistContextValues();
        }

        private void RefreshColonistContextValues()
        {
            var c = tools.SelectedColonist;
            if (c == null || dynamicTexts.Count < 2 || dynamicBars.Count < 3) return;
            dynamicTexts[0].text = $"{c.Activity}\nJob: {(c.CurrentJob != null ? c.CurrentJob.Label : "none")}   at {c.Cell}";
            UiFactory.SetBar(dynamicBars[0], c.Needs.Food);
            UiFactory.SetBar(dynamicBars[1], c.Needs.Rest);
            UiFactory.SetBar(dynamicBars[2], c.Needs.Mood);
            sb.Clear();
            foreach (var s in c.Inventory.Stacks()) sb.Append(s.ToString()).Append("  ");
            dynamicTexts[1].text = sb.Length > 0 ? sb.ToString() : "nothing";
        }

        private void BuildCellContext(Int3 cell)
        {
            var block = ctx.World.GetBlock(cell);
            var def = BlockRegistry.Get(block);
            contextTitle.text = def.Name;
            UiFactory.Label(contextBody, $"Position {cell}", 12, TextAnchor.MiddleLeft, UiFactory.DimTextColor);

            var pile = ctx.Items.HasItems(cell) ? cell : cell + Int3.Up;
            if (ctx.Items.HasItems(pile))
            {
                sb.Clear();
                foreach (var s in ctx.Items.StacksIn(pile)) sb.Append(s.ToString()).Append('\n');
                UiFactory.Label(contextBody, "Items here:\n" + sb, 12);
            }

            var order = ctx.Jobs.Get(cell);
            if (order != null)
            {
                UiFactory.Label(contextBody, order.Kind == DesignationKind.Mine ? "Ordered: mine" : "Ordered: build " + BlockRegistry.Get(order.BuildType).Name, 12);
                UiFactory.Button(contextBody, "Cancel order", () => ctx.Jobs.Cancel(cell), 0f, 26f);
            }
            else if (def.IsMinable)
            {
                UiFactory.Button(contextBody, "Mine this block", () => ctx.Jobs.AddMine(cell), 0f, 26f);
            }

            if (block == BlockType.Workbench)
            {
                UiFactory.Label(contextBody, "Bills", 14, TextAnchor.MiddleLeft, UiFactory.DimTextColor);
                BuildBillList(cell, BlockType.Workbench);
            }
        }

        private void BuildBillList(Int3? station, BlockType stationType)
        {
            foreach (var bill in ctx.Jobs.BillsAt(station))
            {
                var b = bill;
                var row = UiFactory.Row(contextBody, "Bill");
                UiFactory.Label(row, $"{b.Recipe.Name}  x{b.Remaining}", 13);
                UiFactory.Button(row, "x", () => ctx.Jobs.RemoveBill(b), 28f, 22f);
            }
            UiFactory.Label(contextBody, "Add bill", 13, TextAnchor.MiddleLeft, UiFactory.DimTextColor);
            foreach (var recipe in RecipeRegistry.ForStation(stationType))
            {
                var r = recipe;
                var row = UiFactory.Row(contextBody, r.Id);
                sb.Clear();
                foreach (var input in r.Inputs) sb.Append(input.ToString()).Append(", ");
                if (sb.Length > 2) sb.Length -= 2;
                UiFactory.Label(row, $"{r.Output}\n<size=11><color=#aaaaaa>{sb}</color></size>", 13);
                UiFactory.Button(row, "+1", () => ctx.Jobs.AddBill(r, station, 1), 34f, 22f);
                UiFactory.Button(row, "+5", () => ctx.Jobs.AddBill(r, station, 5), 34f, 22f);
            }
        }
    }
}
