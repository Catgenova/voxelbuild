using VoxelBuild.Core;
using VoxelBuild.Sim;
using VoxelBuild.World;

namespace VoxelBuild.Tests
{
    /// <summary>Helpers for building small deterministic worlds.</summary>
    internal static class TestWorld
    {
        /// <summary>A flat world: bedrock at y=0, stone up to and including <paramref name="floorY"/>, air above.</summary>
        public static VoxelWorld Flat(int chunksXZ = 2, int chunksY = 2, int floorY = 8)
        {
            var world = new VoxelWorld(new Int3(chunksXZ, chunksY, chunksXZ));
            var size = world.SizeInBlocks;
            for (int z = 0; z < size.z; z++)
                for (int x = 0; x < size.x; x++)
                    for (int y = 0; y <= floorY; y++)
                        world.SetBlockRaw(new Int3(x, y, z), y == 0 ? BlockType.Bedrock : BlockType.Stone);
            return world;
        }

        public static GameContext Context(VoxelWorld world, int seed = 1) => new GameContext(world, seed);

        public static ColonistCore Colonist(GameContext ctx, Int3 feet, string name = "Tester")
        {
            var c = new ColonistCore(ctx, ctx.Colonists.Count, name);
            c.Spawn(feet);
            ctx.Colonists.Add(c);
            return c;
        }

        /// <summary>Runs the simulation in fixed steps until the condition holds or the time budget runs out.</summary>
        public static bool RunUntil(GameContext ctx, System.Func<bool> condition, float maxSeconds = 120f, float step = 0.05f)
        {
            float t = 0f;
            while (t < maxSeconds)
            {
                if (condition()) return true;
                ctx.Tick(step);
                t += step;
            }
            return condition();
        }
    }
}
