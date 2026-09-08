using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace VoxelBuild.Player
{
    using VoxelBuild.Sim;

    /// <summary>Rotates the sun (and a dim moon) with the game clock and switches the HDRP exposure to automatic.</summary>
    public sealed class DayNightController : MonoBehaviour
    {
        public float SunYaw = 30f;
        public float MoonIntensityFraction = 0.0004f;

        private GameClock clock;
        private Transform sun;
        private Transform moon;

        public void Init(GameClock clock, Light sunLight)
        {
            this.clock = clock;
            if (sunLight == null) return;
            sun = sunLight.transform;

            // Clone the template sun so the moon inherits its HDRP light data and units.
            var moonGo = Instantiate(sunLight.gameObject, sunLight.transform.parent);
            moonGo.name = "Moon";
            var moonLight = moonGo.GetComponent<Light>();
            moonLight.intensity = sunLight.intensity * MoonIntensityFraction;
            moonLight.color = new Color(0.55f, 0.65f, 1f);
            moonLight.shadows = LightShadows.None;
            moon = moonGo.transform;

            ConfigureExposure();
            Apply();
        }

        private static void ConfigureExposure()
        {
            var volume = FindFirstObjectByType<Volume>();
            if (volume == null || volume.sharedProfile == null) return;
            // volume.profile clones the asset so edits never touch the on-disk profile.
            var profile = volume.profile;
            if (profile.TryGet<Exposure>(out var exposure))
            {
                exposure.mode.Override(ExposureMode.Automatic);
                exposure.limitMin.Override(-1f);
                exposure.limitMax.Override(15f);
                exposure.adaptationSpeedDarkToLight.Override(3f);
                exposure.adaptationSpeedLightToDark.Override(3f);
            }
        }

        private void Update()
        {
            if (clock != null) Apply();
        }

        private void Apply()
        {
            if (sun == null) return;
            float elevation = (clock.TimeOfDay - 0.25f) * 360f;
            sun.rotation = Quaternion.Euler(elevation, SunYaw, 0f);
            if (moon != null) moon.rotation = Quaternion.Euler(elevation + 180f, SunYaw + 40f, 0f);
        }
    }
}
