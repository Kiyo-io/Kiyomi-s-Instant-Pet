using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Events;

namespace Kiyomi_s_Instant_Pet
{
    internal class InstantPetPatches
    {
        private static IMonitor Monitor;

        public static void ApplyPatches(Harmony harmony, IMonitor monitor)
        {
            Monitor = monitor;

            try
            {
                harmony.Patch(
                    original: AccessTools.Method(typeof(Event), nameof(Event.skipEvent)),
                    prefix: new HarmonyMethod(typeof(InstantPetPatches), nameof(SkipEvent_Prefix))
                );

                harmony.Patch(
                    original: AccessTools.Method(typeof(Event), nameof(Event.tryEventCommand)),
                    prefix: new HarmonyMethod(typeof(InstantPetPatches), nameof(TryEventCommand_Prefix))
                );

                Monitor.Log("Harmony patches applied successfully!", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error applying Harmony patches: {ex.Message}", LogLevel.Error);
            }
        }

        private static bool SkipEvent_Prefix(Event __instance)
        {
            try
            {
                if (__instance?.id != null && IsPetAdoptionEvent(__instance.id))
                {
                    Monitor.Log($"Skipping pet adoption event: {__instance.id}", LogLevel.Debug);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error in SkipEvent_Prefix: {ex.Message}", LogLevel.Error);
            }

            return true;
        }

        private static bool TryEventCommand_Prefix(Event __instance, GameLocation location, GameTime time, string[] args)
        {
            try
            {
                if (__instance?.id != null && IsPetAdoptionEvent(__instance.id))
                {
                    if (Game1.player.hasPet())
                    {
                        Monitor.Log("Player already has a pet. Ending pet adoption event.", LogLevel.Debug);
                        __instance.skipEvent();
                        Game1.exitActiveMenu();
                        Game1.dialogueUp = false;
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error in TryEventCommand_Prefix: {ex.Message}", LogLevel.Error);
            }

            return true;
        }

        private static bool IsPetAdoptionEvent(string eventId)
        {
            return eventId.Contains("pet") || eventId.Contains("Pet") || eventId == "917";
        }
    }
}
