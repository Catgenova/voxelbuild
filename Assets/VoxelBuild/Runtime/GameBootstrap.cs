using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VoxelBuild
{
    using VoxelBuild.Core;
    using VoxelBuild.Nav;
    using VoxelBuild.Player;
    using VoxelBuild.Rendering;
    using VoxelBuild.Sim;
    using VoxelBuild.UI;
    using VoxelBuild.World;

    /// <summary>
    /// Scene entry point. Generates the world, creates the simulation, and wires up rendering, camera, input and HUD.
    /// Everything is created from code so the scene only needs this component, a camera, a sun and the HDRP volume.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("World")]
        public int Seed = 1337;
        [Tooltip("World size in 16-block chunks. Blocks are 0.25 m (see Core/Scale.cs), so 12 chunks = 48 m.")]
        public int ChunksX = 12;
        public int ChunksY = 6;
        public int ChunksZ = 12;
        public bool Caves = true;

        /// <summary>Metres per block, fixed by <see cref="Scale"/>.</summary>
        public float BlockSize => Scale.BlockSize;

        [Header("Look")]
        [Tooltip("Parallax occlusion mapping on block faces (HDRP pixel displacement).")]
        public bool ParallaxOcclusion = true;
        [Tooltip("Apparent depth of the surface relief in centimetres.")]
        [Range(0.2f, 5f)] public float ReliefDepthCm = 1.5f;
        [Tooltip("Ray-march samples for parallax. Higher is smoother at grazing angles but costs GPU time.")]
        [Range(8, 64)] public int ParallaxMaxSamples = 24;

        [Header("Colony")]
        public int ColonistCount = 3;
        public int DayLengthSeconds = 600;

        [Header("Scene references (found automatically when empty)")]
        public Camera MainCamera;
        public Light Sun;

        public static GameBootstrap Instance { get; private set; }

        public GameContext Ctx { get; private set; }
        public WorldRenderer WorldRenderer { get; private set; }
        public ItemRenderer ItemRenderer { get; private set; }
        public OverlayRenderer Overlay { get; private set; }
        public PlayerTools Tools { get; private set; }
        public IsometricCameraController CameraController { get; private set; }
        public Hud Hud { get; private set; }

        private readonly List<ColonistView> views = new List<ColonistView>();
        private float lastSpeed = 1f;

        private static readonly string[] Names =
        {
            "Ada", "Bram", "Cass", "Dov", "Esme", "Finn", "Greta", "Hal", "Ines", "Juno", "Kit", "Lior",
        };

        private static readonly ColorRgb[] Colors =
        {
            ColorRgb.Bytes(70, 130, 200), ColorRgb.Bytes(200, 90, 70), ColorRgb.Bytes(90, 170, 90),
            ColorRgb.Bytes(200, 170, 60), ColorRgb.Bytes(160, 90, 190), ColorRgb.Bytes(80, 180, 180),
        };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("VoxelBuild: a second GameBootstrap was found and disabled.", this);
                enabled = false;
                return;
            }
            Instance = this;
            Debug.Log("VoxelBuild: bootstrap starting");
            try
            {
                Build();
                Debug.Log("VoxelBuild: bootstrap finished");
            }
            catch (System.Exception e)
            {
                Debug.LogError("VoxelBuild: startup failed. " + e, this);
                enabled = false;
            }
        }

        /// <summary>
        /// Safety net: if the scene has no (working) GameBootstrap, for example a stale scene or a missing-script
        /// reference, create one so pressing Play always starts the game.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (Instance != null) return;
            if (FindFirstObjectByType<GameBootstrap>() != null) return;
            Debug.LogWarning("VoxelBuild: no GameBootstrap found in the scene; creating one automatically.");
            new GameObject("Game (auto)").AddComponent<GameBootstrap>();
        }

        private void Build()
        {
            var atlas = BlockAtlas.Create(new BlockAtlas.Options
            {
                ParallaxOcclusion = ParallaxOcclusion,
                ReliefDepth = ReliefDepthCm * 0.01f,
                MinSamples = 6,
                MaxSamples = ParallaxMaxSamples,
            });

            var world = new VoxelWorld(new Int3(Mathf.Max(1, ChunksX), Mathf.Max(1, ChunksY), Mathf.Max(1, ChunksZ)));
            new WorldGenerator { Seed = Seed, Caves = Caves }.Generate(world);

            Ctx = new GameContext(world, Seed);
            Ctx.Clock.DayLengthSeconds = Mathf.Max(30, DayLengthSeconds);

            WorldRenderer = new GameObject("World").AddComponent<WorldRenderer>();
            WorldRenderer.transform.SetParent(transform, false);
            WorldRenderer.Init(world, atlas, Ctx.Fluids, BlockSize);

            ItemRenderer = new GameObject("Items").AddComponent<ItemRenderer>();
            ItemRenderer.transform.SetParent(transform, false);
            ItemRenderer.Init(Ctx.Items, WorldRenderer);

            Overlay = new GameObject("Overlay").AddComponent<OverlayRenderer>();
            Overlay.transform.SetParent(transform, false);
            Overlay.Init(Ctx, WorldRenderer);

            var spawn = WorldGenerator.FindSpawn(world);
            var font = LoadFont();

            SpawnColonists(spawn, font);
            SpawnStartingSupplies(spawn);

            var cam = MainCamera != null ? MainCamera : Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                cam.tag = "MainCamera";
            }
            CameraController = cam.gameObject.GetComponent<IsometricCameraController>();
            if (CameraController == null) CameraController = cam.gameObject.AddComponent<IsometricCameraController>();
            CameraController.Init(cam, WorldRenderer.WorldBounds, WorldRenderer.CellFloorCenter(spawn));

            var sun = Sun != null ? Sun : FindSun();
            var dayNight = gameObject.AddComponent<DayNightController>();
            dayNight.Init(Ctx.Clock, sun);

            Tools = gameObject.AddComponent<PlayerTools>();
            Tools.Init(Ctx, WorldRenderer, Overlay, cam, FindView);

            Hud = new GameObject("HUD").AddComponent<Hud>();
            Hud.transform.SetParent(transform, false);
            Hud.Init(Ctx, Tools, WorldRenderer, CameraController, font);

            Ctx.Log($"World {world.SizeInBlocks.x}x{world.SizeInBlocks.y}x{world.SizeInBlocks.z} blocks, seed {Seed}. {Ctx.Colonists.Count} colonists landed.");
        }

        private void SpawnColonists(Int3 spawn, Font font)
        {
            int count = Mathf.Clamp(ColonistCount, 1, Names.Length);
            var rnd = new System.Random(Seed);
            var used = new HashSet<Int3>();
            for (int i = 0; i < count; i++)
            {
                var core = new ColonistCore(Ctx, i, Names[(i + rnd.Next(0, 3)) % Names.Length]) { Color = Colors[i % Colors.Length] };
                // Give the crew a spread of specialities.
                switch (i % 3)
                {
                    case 0: core.Work.Set(WorkType.Mine, 1); break;
                    case 1: core.Work.Set(WorkType.Build, 1); core.Work.Set(WorkType.Craft, 2); break;
                    default: core.Work.Set(WorkType.Haul, 1); core.Work.Set(WorkType.Craft, 1); break;
                }
                core.Needs.Food = 0.7f + 0.2f * (float)rnd.NextDouble();
                core.Needs.Rest = 0.8f + 0.2f * (float)rnd.NextDouble();
                core.Inventory.Add(ItemType.Berries, 2);

                var cell = FindStandableNear(spawn, used, Scale.Metres(3f));
                used.Add(cell);
                core.Spawn(cell);
                Ctx.Colonists.Add(core);

                var view = new GameObject().AddComponent<ColonistView>();
                view.transform.SetParent(transform, false);
                view.Init(core, WorldRenderer, font);
                views.Add(view);
            }
        }

        private void SpawnStartingSupplies(Int3 spawn)
        {
            var used = new HashSet<Int3>();
            foreach (var c in Ctx.Colonists) used.Add(c.Cell);
            Drop(ItemType.Berries, 10);
            Drop(ItemType.Log, 8);
            Drop(ItemType.Stone, 6);
            Drop(ItemType.Planks, 4);

            void Drop(ItemType type, int count)
            {
                var cell = FindStandableNear(spawn + new Int3(Scale.Metres(1f), 0, Scale.Metres(1f)), used, Scale.Metres(3f));
                used.Add(cell);
                Ctx.Items.AddNear(cell, type, count);
            }
        }

        private Int3 FindStandableNear(Int3 origin, HashSet<Int3> exclude, int radius)
        {
            for (int r = 0; r <= radius; r++)
                for (int dz = -r; dz <= r; dz++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                        for (int dy = NavRules.StepUp * 2; dy >= -NavRules.MaxFall; dy--)
                        {
                            var p = origin + new Int3(dx, dy, dz);
                            if (exclude.Contains(p)) continue;
                            if (NavRules.IsStandable(Ctx.World, p)) return p;
                        }
                    }
            return origin;
        }

        private ColonistView FindView(ColonistCore core)
        {
            foreach (var v in views)
                if (v.Core == core) return v;
            return null;
        }

        private static Light FindSun()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) return l;
            return null;
        }

        private static Font LoadFont()
        {
            Font font = null;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { /* fall through */ }
            if (font == null) font = Font.CreateDynamicFontFromOSFont("Arial", 14);
            return font;
        }

        private void Update()
        {
            if (Ctx == null) return;
            var kb = Keyboard.current;
            if (kb != null && kb.spaceKey.wasPressedThisFrame)
            {
                if (Ctx.Clock.IsPaused) Ctx.Clock.Speed = lastSpeed;
                else
                {
                    lastSpeed = Ctx.Clock.Speed;
                    Ctx.Clock.Speed = 0f;
                }
            }

            // Simulation runs on unscaled time so pausing never stalls the camera or UI.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f) * Ctx.Clock.Speed;
            Ctx.Tick(dt);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
