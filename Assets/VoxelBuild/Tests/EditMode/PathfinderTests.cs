using System.Collections.Generic;
using NUnit.Framework;
using VoxelBuild.Core;
using VoxelBuild.Nav;
using VoxelBuild.World;

namespace VoxelBuild.Tests
{
    public class PathfinderTests
    {
        private const int Floor = 8;
        private static Int3 Feet(int x, int z, int extraY = 0) => new Int3(x, Floor + 1 + extraY, z);

        [Test]
        public void WalksAcrossFlatGround()
        {
            var world = TestWorld.Flat();
            var pf = new Pathfinder(world);
            var path = new List<Int3>();
            Assert.IsTrue(pf.FindPathTo(Feet(2, 2), Feet(10, 2), path));
            Assert.AreEqual(Feet(2, 2), path[0]);
            Assert.AreEqual(Feet(10, 2), path[path.Count - 1]);
            Assert.AreEqual(9, path.Count, "straight line uses one cell per step");
        }

        [Test]
        public void StepsUpToTheLimitButNotBeyond()
        {
            var world = TestWorld.Flat();
            var pf = new Pathfinder(world);
            var path = new List<Int3>();
            // A ledge across z at x=5, StepUp blocks high.
            for (int k = 1; k <= NavRules.StepUp; k++)
                for (int z = 0; z < world.SizeInBlocks.z; z++) world.SetBlockRaw(new Int3(5, Floor + k, z), BlockType.Stone);
            Assert.IsTrue(pf.FindPathTo(Feet(2, 2), Feet(8, 2), path));
            Assert.IsTrue(path.Contains(new Int3(5, Floor + 1 + NavRules.StepUp, 2)), "path goes over the ledge");

            // One block higher: impassable.
            for (int z = 0; z < world.SizeInBlocks.z; z++) world.SetBlockRaw(new Int3(5, Floor + 1 + NavRules.StepUp, z), BlockType.Stone);
            Assert.IsFalse(pf.FindPathTo(Feet(2, 2), Feet(8, 2), path));
        }

        [Test]
        public void DropsDownButRefusesLongFalls()
        {
            var world = TestWorld.Flat(2, 2, 8);
            var pf = new Pathfinder(world);
            var path = new List<Int3>();

            // A pit just too deep to climb out of, but fine to drop into.
            int depth = NavRules.StepUp + 1;
            DigPit(world, depth);
            var pitFeet = new Int3(10, Floor + 1 - depth, 2);
            Assert.IsTrue(pf.FindPathTo(Feet(2, 2), pitFeet, path), "can drop into the pit");
            Assert.IsFalse(pf.FindPathTo(pitFeet, Feet(2, 2), path), "cannot climb back out");

            // Deeper than the fall limit: refused.
            depth = NavRules.MaxFall + 1;
            DigPit(world, depth);
            Assert.IsFalse(pf.FindPathTo(Feet(2, 2), new Int3(10, Floor + 1 - depth, 2), path));
        }

        private static void DigPit(World.VoxelWorld world, int depth)
        {
            for (int x = 6; x < world.SizeInBlocks.x; x++)
                for (int z = 0; z < world.SizeInBlocks.z; z++)
                    for (int y = Floor + 1 - depth; y <= Floor; y++)
                        world.SetBlockRaw(new Int3(x, y, z), BlockType.Air);
        }

        [Test]
        public void NeedsHeadroom()
        {
            var world = TestWorld.Flat();
            // A ceiling two blocks above the floor across x=5.
            for (int z = 0; z < world.SizeInBlocks.z; z++)
                world.SetBlockRaw(new Int3(5, Floor + 3, z), BlockType.Stone);
            var pf = new Pathfinder(world);
            var path = new List<Int3>();
            Assert.IsFalse(pf.FindPathTo(Feet(2, 2), Feet(8, 2), path), "colonists are taller than two blocks");
        }

        [Test]
        public void ReachModeStopsNextToTheTargetBlock()
        {
            var world = TestWorld.Flat();
            var target = new Int3(10, Floor, 10); // a floor block to mine
            var pf = new Pathfinder(world);
            var path = new List<Int3>();
            Assert.IsTrue(pf.FindPathToReach(Feet(2, 2), target, path));
            var end = path[path.Count - 1];
            Assert.IsTrue(NavRules.CanReach(end, target));
            Assert.IsTrue(NavRules.IsStandable(world, end));
        }

        [Test]
        public void FollowerArrivesAndReportsBlockedPaths()
        {
            var world = TestWorld.Flat();
            var pf = new Pathfinder(world);
            var path = new List<Int3>();
            Assert.IsTrue(pf.FindPathTo(Feet(2, 2), Feet(6, 2), path));
            var follower = new PathFollower { SpeedCellsPerSecond = 4f };
            follower.Teleport(Feet(2, 2));
            follower.SetPath(path);
            float t = 0f;
            while (follower.HasPath && t < 10f)
            {
                Assert.IsTrue(follower.Tick(0.1f, world));
                t += 0.1f;
            }
            Assert.AreEqual(Feet(6, 2), follower.Cell);
            Assert.Less(t, 1.5f, "4 cells at 4 cells/s takes about a second");

            follower.Teleport(Feet(2, 2));
            follower.SetPath(path);
            world.SetBlock(Feet(4, 2), BlockType.Stone);
            bool ok = true;
            for (int i = 0; i < 20 && ok; i++) ok = follower.Tick(0.1f, world);
            Assert.IsFalse(ok, "a block placed on the path is reported");
        }
    }
}
