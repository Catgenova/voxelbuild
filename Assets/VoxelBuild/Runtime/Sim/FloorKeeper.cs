using System.Collections.Generic;

namespace VoxelBuild.Sim
{
    using VoxelBuild.Core;
    using VoxelBuild.World;

    /// <summary>Collapses floor tiles that lose their structural support when blocks are mined away beneath them.</summary>
    public sealed class FloorKeeper
    {
        private readonly GameContext ctx;
        private readonly List<Int3> toCheck = new List<Int3>();

        public int Collapsed { get; private set; }

        public FloorKeeper(GameContext ctx)
        {
            this.ctx = ctx;
            ctx.World.BlockChanged += OnBlockChanged;
        }

        private void OnBlockChanged(Int3 pos, BlockType oldType, BlockType newType)
        {
            if (!BlockRegistry.IsSolid(oldType) || BlockRegistry.IsSolid(newType)) return;
            var world = ctx.World;
            int r = FloorRules.SupportRadius;
            toCheck.Clear();
            for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    var cell = new Int3(pos.x + dx, pos.y + 1, pos.z + dz);
                    if (world.HasFloor(cell) && !FloorRules.IsSupported(world, cell)) toCheck.Add(cell);
                }
            foreach (var cell in toCheck)
            {
                var floor = world.GetFloor(cell);
                world.SetFloor(cell, BlockType.Air);
                var drop = BlockRegistry.Get(floor).Drop;
                if (!drop.IsEmpty) ctx.Items.AddNear(cell, drop.Type, drop.Count);
                Collapsed++;
            }
            if (toCheck.Count > 0) ctx.Log($"{toCheck.Count} floor tile(s) collapsed for lack of support");
        }
    }
}
