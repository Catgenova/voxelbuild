namespace VoxelBuild.Core
{
    /// <summary>
    /// The one place that defines how big a block is. Everything measured in blocks (movement rules, reach,
    /// generator features) derives from these so the block size can change without hunting through the code.
    /// </summary>
    public static class Scale
    {
        /// <summary>Blocks along one metre.</summary>
        public const int BlocksPerMetre = 4;
        /// <summary>Metres along one block.</summary>
        public const float BlockSize = 1f / BlocksPerMetre;

        /// <summary>Vertical cells a colonist occupies (~1.75 m).</summary>
        public const int ColonistClearance = 7;
        /// <summary>Cells a colonist can step up in one move (0.5 m).</summary>
        public const int StepUp = 2;
        /// <summary>Cells a colonist will drop without refusing the path (1.5 m).</summary>
        public const int MaxFall = 6;
        /// <summary>Horizontal work reach in cells (1 m).</summary>
        public const int Reach = 4;
        public const int ReachDown = 4;
        public const int ReachUp = 10;

        /// <summary>Colonist walking speed.</summary>
        public const float WalkSpeedMetresPerSecond = 1.8f;
        public const float WalkSpeedCells = WalkSpeedMetresPerSecond * BlocksPerMetre;

        /// <summary>Mining/building times in the registries are per 0.5 m block; small blocks take proportionally less.</summary>
        public const float WorkTimeFactor = 0.25f;

        public static int Metres(float metres) => (int)System.Math.Round(metres * BlocksPerMetre);
    }
}
