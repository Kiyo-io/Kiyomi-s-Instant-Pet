using HarmonyLib;
using StardewModdingAPI;


namespace Kiyomi_s_Instant_Pet
{
    internal class InstantPetPatches
    {
        private static IMonitor Monitor = null!;


        public static void ApplyPatches(Harmony harmony, IMonitor monitor)
        {
            Monitor = monitor;
            monitor.Log(
                "Instant Pet running with no vanilla overrides.",
                LogLevel.Debug
            );
        }
    }
}

///log statement for SMAPI