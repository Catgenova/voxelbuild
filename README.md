# VoxelBuild

A single-player colony sim that crosses **RimWorld** with **Minecraft**: a 2.5D isometric world made of
tiny blocks that your colonists dig out, carry around, craft with and build from. You never control a
colonist directly; you give orders (mine this, build that, store things here, craft these) and set each
person's needs and work priorities, and an AI-pathed crew carries them out.

Built with **Unity 6 (6000.4)** on the **High Definition Render Pipeline** so the scene is lit in HDR: a
physically based sky, a sun that tracks the in-game clock, a moon for the night shift, and emissive
crystals and torches underground.

## Playing

Open the project in Unity 6, load `Assets/OutdoorsScene.unity` and press Play. Everything (terrain,
materials, colonists, HUD) is generated from code at startup; the scene only holds the `Game`
object, the camera, the sun and the HDRP volume. The default world is 48 m × 24 m × 48 m, which at
0.25 m blocks is 192 × 96 × 192 cells (864 chunks); expect a couple of seconds of generation.

| Input | Action |
| --- | --- |
| `W A S D` / arrows, middle-mouse drag | Pan the camera |
| `Q` / `E` | Rotate the view in 90° steps |
| Mouse wheel | Zoom |
| `PgUp` / `PgDn`, `[` / `]`, `Ctrl` + wheel | Move the view level (cut the world open to see underground) |
| `Home` | View level back to the top |
| `1`–`5` | Select / Mine / Build / Stockpile / Cancel tool |
| Left-drag | Apply the current tool to a box of blocks |
| Right-click / `Esc` | Back to the Select tool, then clear selection |
| `Space` | Pause / resume |

Typical first minutes: drag the **Mine** tool over a hillside to gather stone and dirt, order a
**Stockpile** zone so hauled goods land in one place, craft **planks by hand** from the starting logs,
then build a **Workbench** and queue beds and torches on it. Colonists eat berries from piles or forage
wild bushes when hungry, and sleep in beds (or on the ground) when tired.

## Architecture

Everything lives in `Assets/VoxelBuild`. The simulation is plain C# with no `UnityEngine` dependency
so it can be unit-tested and reasoned about without a scene; a thin Unity layer renders it.

```
Runtime/
  Core/        Int3, Direction, ColorRgb, block / item / recipe registries
  World/       Chunk (16³ blocks), VoxelWorld (finite chunk grid + change events),
               Noise + WorldGenerator (terrain, ores, caves, trees, bushes),
               AtlasLayout + ChunkMesher (culled cube meshing with a Y-slice cap)
  Nav/         NavRules (clearance, step-up, fall limits), A* Pathfinder with goal
               predicates, PathFollower (smooth cell-to-cell movement)
  Sim/         GameClock, Needs, Inventory, GroundItems, Stockpiles, JobBoard
               (designations + craft bills), GameContext, ColonistCore (the brain)
  Sim/Jobs/    Mine, Build, Craft, Haul/Deposit, Eat, Sleep, Idle
  Rendering/   BlockAtlas (procedural texture atlas + HDRP materials), ChunkView,
               WorldRenderer (remesh queue, slice), ItemRenderer, OverlayRenderer
               (order outlines, stockpile tiles, cursor), ColonistView
  Player/      IsometricCameraController, PlayerTools (orders, selection, slice),
               DayNightController (sun + moon + auto exposure)
  UI/          UiFactory (runtime uGUI helpers), Hud
  GameBootstrap.cs   Scene entry point that wires it all together
Tests/EditMode/      NUnit tests for the engine-free layers
```

### Key design choices

- **Tiny blocks.** A block is 0.25 m and a colonist is ~7 blocks tall. All block-measured rules live
  in `Core/Scale.cs`: seven cells of clearance, step up two blocks (0.5 m), drop up to six (1.5 m),
  work reach of one metre. The generator is tuned in metres, so the look survives scale changes.
- **View slice.** The mesher takes a maximum visible Y, so cutting the world open is a remesh of the
  affected chunks, not a shader trick. Colliders follow the cut, so clicks always land on what you see.
- **Orders, not units.** The `JobBoard` holds player intent (mine/build designations, stockpile cells,
  crafting bills). Each colonist's `ColonistCore` picks the next job from needs first, then work
  priorities (0 = never, 1 = first), reserving orders so two people don't take the same one. Failed
  orders back off and retry when the world changes.
- **Items are physical.** Mined blocks go into the miner's pack; packs are emptied into stockpiles;
  building and crafting fetch materials from piles. Piles push up when built over and fall when
  undermined.
- **HDR without hassle.** Materials are cloned from the pipeline's default Lit material so shaders are
  always included in builds. Overlays use HDRP emissive with exposure weight 0 so they read the same at
  noon and midnight.

## Tests

Open **Window ▸ General ▸ Test Runner ▸ EditMode** and run `VoxelBuild.Tests.EditMode`. The suite
covers chunk storage and change events, the generator, meshing and slicing, pathfinding (steps, drops,
headroom, reach), the job board, and end-to-end colonist behaviour (mine, fetch-and-build, craft and
haul, eat, sleep) driven purely through `GameContext.Tick`.

## Roadmap

- Save / load (chunk RLE + colonist state)
- Water and falling blocks (sand, gravel)
- More needs (recreation, warmth), moods with consequences, skills that speed up work
- Farming, cooking, and a proper food chain
- Threats: wildlife, weather, raids
- Room detection and furniture that affects mood
- Greedy meshing and a job-scheduler thread once worlds grow
