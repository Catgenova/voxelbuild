# VoxelBuild — notes for coding agents

Unity 6 (6000.4) HDRP project. RimWorld-meets-Minecraft colony sim on a tiny-block voxel world.
See README.md for the design and the module map.

## Layout rules

- All game code lives under `Assets/VoxelBuild`. Runtime assembly: `VoxelBuild.Runtime`
  (references Input System, HDRP, uGUI). Tests: `VoxelBuild.Tests.EditMode`.
- `Core/`, `World/`, `Nav/`, `Sim/` must stay free of `UnityEngine` references. They are the
  simulation and are exercised by NUnit tests without a scene. Unity-facing code goes in
  `Rendering/`, `Player/`, `UI/` or `GameBootstrap.cs`.
- Coordinates: `Int3` block cells; a colonist's cell is where its feet are. World units = cells ×
  `BlockSize` (0.5 m). Use `WorldRenderer.CellToWorld` / `WorldToCell` for conversions.
- Every block/item/recipe is registered in code (`Core/Blocks.cs`, `Core/Items.cs`,
  `Core/Recipes.cs`). Adding a block: add the enum value, a `BlockDefinition`, and (if craftable)
  an `ItemType` + recipe. The atlas and UI pick it up automatically.
- The scene is generated at runtime by `GameBootstrap`. Do not hand-author prefabs; extend the
  bootstrap or the relevant renderer.

## Unity asset hygiene

- Every file and folder under `Assets/` needs a `.meta` with a stable GUID. New scripts referenced
  from a scene must keep their GUID (`GameBootstrap.cs.meta` is referenced by
  `OutdoorsScene.unity`).
- Input uses the new Input System only (`activeInputHandler: 1`); never call legacy `Input.*`.
- Materials must come from `BlockAtlas.CreateLitMaterial` (clone of the HDRP default) so the shader
  ships in builds. Overlays use `CreateOverlayMaterial` (emissive, exposure weight 0).

## Verifying without an editor

No Unity or .NET compiler is available in the remote sandbox. A tree-sitter based syntax check was
used during development; semantic verification happens in the editor via the EditMode tests
(Window ▸ General ▸ Test Runner). When touching simulation code, add or extend a test in
`Tests/EditMode` that drives `GameContext.Tick` directly.

## Git

- Push all work directly to `main` (the owner's standing instruction). Feature branches are
  optional scratch space; `main` must always carry the latest state.
