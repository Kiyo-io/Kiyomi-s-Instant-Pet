using HarmonyLib;
using Microsoft.Xna.Framework;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Characters;


namespace Kiyomi_s_Instant_Pet
{
    internal class InstantPetPatches
    {
        private static IMonitor Monitor;

        // Track pets spawned by this mod
        public static HashSet<string> ModSpawnedPets = new HashSet<string>();

        // Track if we're currently in Marnie's pet adoption event
        private static bool inPetAdoptionEvent = false;












        public static void ApplyPatches(Harmony harmony, IMonitor monitor)
        {
            Monitor = monitor;

            try
            {
                // Patch hasPet() to ignore mod-spawned pets
                var hasPetMethod = AccessTools.Method(typeof(Farmer), nameof(Farmer.hasPet));
                if (hasPetMethod != null)
                {
                    harmony.Patch(
                        original: hasPetMethod,
                        postfix: new HarmonyMethod(typeof(InstantPetPatches), nameof(HasPet_Postfix))
                    );
                    monitor.Log("Successfully patched Farmer.hasPet", LogLevel.Debug);
                }
                else
                {
                    monitor.Log("Could not find Farmer.hasPet method - patch skipped", LogLevel.Warn);
                }

                // Patch Event.namePet to track when a vanilla pet is named/added
                var namePetMethod = AccessTools.Method(typeof(StardewValley.Event), "namePet");
                if (namePetMethod != null)
                {
                    harmony.Patch(
                        original: namePetMethod,
                        postfix: new HarmonyMethod(typeof(InstantPetPatches), nameof(NamePet_Postfix))
                    );
                    monitor.Log("Successfully patched Event.namePet", LogLevel.Debug);
                }
                else
                {
                    monitor.Log("Could not find Event.namePet method", LogLevel.Warn);
                }

                // Track when event starts (constructor with eventID parameter)
                var eventConstructor = AccessTools.Constructor(typeof(StardewValley.Event), new Type[] { typeof(string), typeof(string), typeof(string), typeof(Farmer) });
                if (eventConstructor != null)
                {
                    harmony.Patch(
                        original: eventConstructor,
                        postfix: new HarmonyMethod(typeof(InstantPetPatches), nameof(EventConstructor_Postfix))
                    );
                    monitor.Log("Successfully patched Event constructor", LogLevel.Debug);
                }
                else
                {
                    monitor.Log("Could not find Event constructor - event tracking disabled", LogLevel.Warn);
                }

                // Track when event ends
                var exitEventMethod = AccessTools.Method(typeof(StardewValley.Event), nameof(StardewValley.Event.exitEvent));
                if (exitEventMethod != null)
                {
                    harmony.Patch(
                        original: exitEventMethod,
                        prefix: new HarmonyMethod(typeof(InstantPetPatches), nameof(ExitEvent_Prefix))
                    );
                    monitor.Log("Successfully patched Event.exitEvent", LogLevel.Debug);
                }
                else
                {
                    monitor.Log("Could not find Event.exitEvent method - patch skipped", LogLevel.Warn);
                }

                // Patch Event.endBehaviors to ensure pet spawns after event
                var endBehaviorsMethod = AccessTools.Method(typeof(StardewValley.Event), "endBehaviors", new Type[] { typeof(GameLocation) });
                if (endBehaviorsMethod != null)
                {
                    harmony.Patch(
                        original: endBehaviorsMethod,
                        postfix: new HarmonyMethod(typeof(InstantPetPatches), nameof(EndBehaviors_Postfix))
                    );
                    monitor.Log("Successfully patched Event.endBehaviors", LogLevel.Debug);
                }
                else
                {
                    monitor.Log("Could not find Event.endBehaviors method", LogLevel.Warn);
                }

                monitor.Log("Harmony patch application completed", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                monitor.Log($"Error applying Harmony patches: {ex.Message}", LogLevel.Error);
            }
        }














        // Postfix for Event.namePet to ensure pet is spawned
        private static void NamePet_Postfix(StardewValley.Event __instance, string name)
        {
            Monitor.Log($"=== Pet named: '{name}' during event ===", LogLevel.Info);

            // Access the private gotPet field
            var gotPetField = AccessTools.Field(typeof(StardewValley.Event), "gotPet");
            if (gotPetField != null)
            {
                bool gotPet = (bool)gotPetField.GetValue(__instance);
                Monitor.Log($"Event.gotPet flag: {gotPet}", LogLevel.Debug);

                if (gotPet && inPetAdoptionEvent)
                {
                    // The vanilla namePet() may have failed to spawn the pet due to hasPet() returning true
                    // Let's check if a pet was actually added to the farm
                    var farm = Game1.getFarm();
                    bool petAlreadyExists = farm.characters.Any(c => c is Pet p && p.Name == name);

                    if (!petAlreadyExists)
                    {
                        Monitor.Log($"Pet '{name}' was not spawned by vanilla logic - manually spawning it now", LogLevel.Warn);

                        // Get the pet type from Game1.player.whichPetType
                        string petType = Game1.player.whichPetType;
                        Monitor.Log($"Pet type from player: {petType}", LogLevel.Debug);

                        // Find the pet bowl on the farm to spawn nearby
                        int spawnX = 54;  // Default fallback
                        int spawnY = 8;

                        // Try to find pet bowl location
                        foreach (var building in farm.buildings)
                        {
                            if (building is StardewValley.Buildings.PetBowl petBowl)
                            {
                                spawnX = (int)building.tileX.Value + 1;  // Spawn one tile to the right of bowl
                                spawnY = (int)building.tileY.Value;
                                Monitor.Log($"Found pet bowl at ({building.tileX.Value}, {building.tileY.Value}), spawning pet nearby", LogLevel.Debug);
                                break;
                            }
                        }

                        // If no pet bowl building found, check for objects that might be pet bowls
                        if (spawnX == 54 && farm.objects != null)
                        {
                            foreach (var kvp in farm.objects.Pairs)
                            {
                                if (kvp.Value?.Name?.Contains("Bowl", StringComparison.OrdinalIgnoreCase) == true ||
                                    kvp.Value?.QualifiedItemId == "(BC)PetBowl")
                                {
                                    spawnX = (int)kvp.Key.X + 1;
                                    spawnY = (int)kvp.Key.Y;
                                    Monitor.Log($"Found pet bowl object at ({kvp.Key.X}, {kvp.Key.Y}), spawning pet nearby", LogLevel.Debug);
                                    break;
                                }
                            }
                        }

                        // Create the pet with the correct constructor
                        Pet newPet = new Pet(spawnX * 64, spawnY * 64, petType, name);
                        newPet.Manners = 0;

                        // CRITICAL: Set location BEFORE adding to characters
                        newPet.currentLocation = farm;

                        // Set the position explicitly to ensure it's on the farm
                        newPet.Position = new Vector2(spawnX * 64, spawnY * 64);

                        // Initialize the pet's sprite so it renders properly
                        try
                        {
                            newPet.reloadSprite();
                            Monitor.Log("Pet sprite reloaded successfully", LogLevel.Debug);
                        }
                        catch (Exception spriteEx)
                        {
                            Monitor.Log($"Warning: Could not reload pet sprite: {spriteEx.Message}", LogLevel.Warn);
                        }

                        // Add to farm - this must happen AFTER setting currentLocation
                        if (!farm.characters.Contains(newPet))
                        {
                            farm.characters.Add(newPet);
                            Monitor.Log($"Successfully spawned vanilla pet '{name}' ({petType}) at ({spawnX}, {spawnY})", LogLevel.Info);








                            Monitor.Log($"Pet location AFTER SPAWN: {newPet.currentLocation.Name}",LogLevel.Warn);
                        }







                        else
                        {
                            Monitor.Log($"Pet already in farm.characters, skipping add", LogLevel.Warn);
                        }
                    }
                    else
                    {
                        Monitor.Log($"Pet '{name}' already exists on farm - vanilla spawn succeeded", LogLevel.Debug);
                    }
                }
            }
        }












        // Postfix for Event.endBehaviors to ensure pet is spawned after event
        private static void EndBehaviors_Postfix(StardewValley.Event __instance, GameLocation location)
        {
            // Check if this was a pet adoption event and gotPet is true
            var gotPetField = AccessTools.Field(typeof(StardewValley.Event), "gotPet");
            if (gotPetField != null && inPetAdoptionEvent)
            {
                bool gotPet = (bool)gotPetField.GetValue(__instance);
                Monitor.Log($"=== Event.endBehaviors called - gotPet: {gotPet} ===", LogLevel.Info);

                if (gotPet)
                {
                    // Check if a pet actor exists in the event
                    var petActor = __instance.getActorByName("PetActor");
                    if (petActor is Pet pet)
                    {
                        Monitor.Log($"Found PetActor in event: {pet.Name} at ({pet.Position.X}, {pet.Position.Y})", LogLevel.Debug);

                        // Check if this pet is already on the farm
                        var farm = Game1.getFarm();
                        bool petAlreadyOnFarm = farm.characters.Contains(pet);
                        Monitor.Log($"Pet already on farm: {petAlreadyOnFarm}", LogLevel.Debug);

                        if (!petAlreadyOnFarm)
                        {
                            Monitor.Log($"Pet not on farm - this is the issue!", LogLevel.Warn);
                        }
                    }
                    else
                    {
                        Monitor.Log("No PetActor found in event", LogLevel.Warn);
                    }
                }
            }
        }











        // Track when event is created
        private static void EventConstructor_Postfix(StardewValley.Event __instance)
        {
            // Log ALL events to help identify the pet adoption event
            if (__instance?.id != null)
            {
                bool isPetEvent = __instance.eventCommands != null && 
                                  __instance.eventCommands.Any(cmd => cmd.Contains("pet", StringComparison.OrdinalIgnoreCase) && 
                                                                       (cmd.Contains("Marnie", StringComparison.OrdinalIgnoreCase) ||
                                                                        cmd.Contains("PetActor", StringComparison.OrdinalIgnoreCase)));

                if (isPetEvent)
                {
                    Monitor.Log($"=== POTENTIAL PET EVENT - ID: '{__instance.id}', Location: {Game1.currentLocation?.Name ?? "unknown"} ===", LogLevel.Info);
                    Monitor.Log($"  All event commands:", LogLevel.Debug);
                    for (int i = 0; i < __instance.eventCommands.Length; i++)
                    {
                        Monitor.Log($"    [{i}] {__instance.eventCommands[i]}", LogLevel.Debug);
                    }

                    inPetAdoptionEvent = true;
                    Monitor.Log($"=== PET ADOPTION EVENT DETECTED ===", LogLevel.Info);
                }
            }
        }

        // Track when we exit any event (simpler approach)
        private static void ExitEvent_Prefix()
        {
            if (inPetAdoptionEvent)
            {
                inPetAdoptionEvent = false;
                Monitor.Log("Pet adoption event ended - checking for newly adopted pet", LogLevel.Debug);

                // Give the game a frame to complete the event logic
                StardewValley.DelayedAction.functionAfterDelay(delegate
                {






                    CheckForNewlyAdoptedPet();
                }, 100);
            }
        }












        // Check if a pet was just adopted and ensure it's properly spawned
        private static void CheckForNewlyAdoptedPet()
        {
            try
            {
                var farm = Game1.getFarm();
                if (farm == null)
                {
                    Monitor.Log("Farm not found when checking for adopted pet", LogLevel.Warn);
                    return;
                }

                // Check if there's now a vanilla pet on the farm (not in our mod list)
                Pet vanillaPet = null!;
                foreach (var character in farm.characters)
                {
                    if (character is Pet pet)
                    {
                        string petId = $"{pet.Name}_{pet.GetType().Name}";
                        if (!ModSpawnedPets.Contains(petId))
                        {
                            vanillaPet = pet;
                            Monitor.Log($"Found vanilla pet: {pet.Name} ({pet.GetType().Name})", LogLevel.Debug);
                            break;
                        }
                    }
                }

                if (vanillaPet != null)
                {
                    Monitor.Log($"Vanilla pet '{vanillaPet.Name}' successfully spawned from adoption event", LogLevel.Info);
                }
                else
                {
                    Monitor.Log("No vanilla pet found after adoption event - the event may have failed to spawn the pet", LogLevel.Warn);
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error checking for adopted pet: {ex.Message}", LogLevel.Error);
            }
        }






        







        private static bool IsPetAdoptionEvent(string eventId)
        {
            // Common pet adoption event IDs from Stardew Valley
            // Try multiple possibilities since we're not sure which one is correct
            return eventId == "13" || 
                   eventId.Contains("pet", StringComparison.OrdinalIgnoreCase) ||
                   eventId.Contains("adopt", StringComparison.OrdinalIgnoreCase) ||
                   eventId.Contains("Marnie", StringComparison.OrdinalIgnoreCase);
        }

        // Override hasPet() to ignore mod-spawned pets (always)
        private static void HasPet_Postfix(Farmer __instance, ref bool __result)
        {
            try
            {
                bool originalResult = __result;

                if (!__result)
                    return; // Already false, nothing to do

                // Check if ALL pets on farm are mod-spawned
                bool hasVanillaPet = false;
                bool foundAnyPets = false;

                foreach (var character in Game1.getFarm().characters)
                {
                    if (character is Pet pet)
                    {
                        foundAnyPets = true;
                        string petId = $"{pet.Name}_{pet.GetType().Name}";
                        if (!ModSpawnedPets.Contains(petId))
                        {
                            hasVanillaPet = true;
                            break;
                        }
                    }
                }

                // Return false if only mod-spawned pets exist (including during adoption event)
                // This ensures the event can spawn a vanilla pet
                if (foundAnyPets && !hasVanillaPet)
                {
                    __result = false;
                    if (inPetAdoptionEvent)
                    {
                        Monitor.Log($"During adoption event - hasPet() returning false to allow pet spawn", LogLevel.Debug);
                    }
                    else
                    {
                        Monitor.Log($"hasPet() changed: {originalResult} -> {__result} (only mod pets exist)", LogLevel.Debug);
                    }
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error in HasPet_Postfix: {ex.Message}", LogLevel.Error);
            }
        }

      
    }
}
