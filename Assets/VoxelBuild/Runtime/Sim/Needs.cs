using System;

namespace VoxelBuild.Sim
{
    /// <summary>Colonist needs, all in 0..1 where 1 is fully satisfied.</summary>
    public sealed class Needs
    {
        public float Food = 0.85f;
        public float Rest = 0.9f;

        /// <summary>How much Food drains per in-game day.</summary>
        public float FoodDecayPerDay = 1.6f;
        public float RestDecayPerDay = 1.1f;
        public float RestRegainPerDayInBed = 7f;
        public float RestRegainPerDayOnGround = 3.5f;

        public const float HungryThreshold = 0.45f;
        public const float StarvingThreshold = 0.2f;
        public const float TiredThreshold = 0.3f;
        public const float ExhaustedThreshold = 0.12f;

        public bool IsHungry => Food < HungryThreshold;
        public bool IsStarving => Food < StarvingThreshold;
        public bool IsTired => Rest < TiredThreshold;
        public bool IsExhausted => Rest < ExhaustedThreshold;

        /// <summary>Simple aggregate mood for the UI and speed modifiers.</summary>
        public float Mood
        {
            get
            {
                float m = 0.5f + (Food - 0.5f) * 0.5f + (Rest - 0.5f) * 0.5f;
                return Clamp01(m);
            }
        }

        /// <summary>Work and walk speed multiplier derived from needs.</summary>
        public float Efficiency
        {
            get
            {
                float e = 1f;
                if (IsStarving) e *= 0.6f;
                else if (IsHungry) e *= 0.85f;
                if (IsExhausted) e *= 0.6f;
                else if (IsTired) e *= 0.85f;
                return e;
            }
        }

        public void Tick(float simDt, float dayLengthSeconds, bool sleeping, bool inBed)
        {
            float days = simDt / dayLengthSeconds;
            Food = Clamp01(Food - FoodDecayPerDay * days * (sleeping ? 0.5f : 1f));
            if (sleeping)
                Rest = Clamp01(Rest + (inBed ? RestRegainPerDayInBed : RestRegainPerDayOnGround) * days);
            else
                Rest = Clamp01(Rest - RestDecayPerDay * days);
        }

        public void Eat(float nutrition) => Food = Clamp01(Food + nutrition);

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
