using System;

namespace VoxelBuild.Sim
{
    /// <summary>Tracks in-game time. Speed is applied by the host (Unity sets Time.timeScale from it).</summary>
    public sealed class GameClock
    {
        /// <summary>Real seconds for one full day at 1x speed.</summary>
        public float DayLengthSeconds = 600f;

        /// <summary>0 = midnight, 0.5 = noon.</summary>
        public float TimeOfDay { get; private set; } = 0.3f;
        public int Day { get; private set; } = 1;
        /// <summary>Total simulated seconds since start.</summary>
        public float Elapsed { get; private set; }

        private float speed = 1f;
        public float Speed
        {
            get => speed;
            set
            {
                speed = Math.Max(0f, value);
                SpeedChanged?.Invoke(speed);
            }
        }

        public bool IsPaused => speed <= 0f;
        public event Action<float> SpeedChanged;
        public event Action<int> DayChanged;

        public float Hour => TimeOfDay * 24f;
        public bool IsNight => Hour < 6f || Hour >= 22f;

        /// <summary>Advance by already-scaled simulation seconds.</summary>
        public void Advance(float simDt)
        {
            if (simDt <= 0f) return;
            Elapsed += simDt;
            TimeOfDay += simDt / DayLengthSeconds;
            while (TimeOfDay >= 1f)
            {
                TimeOfDay -= 1f;
                Day++;
                DayChanged?.Invoke(Day);
            }
        }

        public string ClockText()
        {
            int h = (int)Hour;
            int m = (int)((Hour - h) * 60f);
            return $"Day {Day}  {h:00}:{m:00}";
        }
    }
}
