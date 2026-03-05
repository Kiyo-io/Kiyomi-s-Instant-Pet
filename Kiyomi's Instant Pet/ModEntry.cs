using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Characters;
using StardewValley.GameData.Pets;
using StardewValley.Menus;

namespace Kiyomi_s_Instant_Pet
{
    internal class ModEntry : Mod
    {
        private static IMonitor ModMonitor = null!;
        private ModConfig Config = null!;
        private Rectangle petConfigButtonBounds;
        internal List<InstantPetData> PetBank = new();
        internal static ModEntry Instance = null!;


        public override void Entry(IModHelper helper)
        {
            ModMonitor = Monitor;

            Config = helper.ReadConfig<ModConfig>();

            var harmony = new Harmony(ModManifest.UniqueID);
            InstantPetPatches.ApplyPatches(harmony, Monitor);

            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.Saving += OnSaving;
            helper.Events.Display.RenderedHud += OnRenderedHud;
            helper.Events.Input.ButtonPressed += OnButtonPressed;

            Instance = this;

            // Initialize button bounds (top-left corner for visibility)
            petConfigButtonBounds = new Rectangle(10, 10, 64, 64);
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            Monitor.Log("Save loaded. Use the pet icon button to manually adopt pets.");


           PetBank = Helper.Data.ReadSaveData<List<InstantPetData>>("InstantPets")
                       ?? new List<InstantPetData>();

            SpawnAllInstantPets();
        }


        private void OnSaving(object sender, SavingEventArgs e)
        {
            Helper.Data.WriteSaveData("InstantPets", PetBank);
        }

        private void OnRenderedHud(object? sender, RenderedHudEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.activeClickableMenu != null)
                return;

            // Draw the pet icon
            e.SpriteBatch.Draw(
                Game1.mouseCursors,
                new Vector2(petConfigButtonBounds.X, petConfigButtonBounds.Y),
                new Rectangle(160, 208, 16, 16), // Dog icon
                Color.White,
                0f,
                Vector2.Zero,
                4f, // Scale to 64x64
                SpriteEffects.None,
                1f
            );

            // Draw hover effect
            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();
            if (petConfigButtonBounds.Contains(mouseX, mouseY))
            {
                // Draw subtle glow effect
                e.SpriteBatch.Draw(
                    Game1.fadeToBlackRect,
                    petConfigButtonBounds,
                    Color.White * 0.2f
                );

                IClickableMenu.drawHoverText(
                    e.SpriteBatch,
                    "Configure Pet",
                    Game1.smallFont
                );
            }
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.activeClickableMenu != null)
                return;

            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();

            // Left click to open menu
            if (e.Button == SButton.MouseLeft)
            {
                if (petConfigButtonBounds.Contains(mouseX, mouseY))
                {
                    Monitor.Log("Opening pet config menu!", LogLevel.Debug);
                    Game1.playSound("bigSelect");
                    OpenPetConfigMenu();
                    Helper.Input.Suppress(SButton.MouseLeft);
                }
            }
        }






        //Work together with OnSaveLoad() to store pet data in PetBank and then recall the Pets when SpawnInstantPet() is called with the SpawnAllInstantPets() in OnSaveLoaded()
        private void SpawnAllInstantPets()
        {
            foreach (var data in PetBank)
                SpawnInstantPet(data);
        }

        internal void SpawnInstantPet(InstantPetData data)
        {
            var farm = Game1.getFarm();

            // Prevent duplicate spawn (industry safety)
            if (farm.characters.OfType<Pet>()
                .Any(p => p.modData.ContainsKey("Kiyomi.InstantPetID")
                       && p.modData["Kiyomi.InstantPetID"] == data.InstantPetID))
                return;

            Pet pet = new Pet((int)data.TilePosition.X, (int)data.TilePosition.Y, "0", data.PetType);

            pet.Name = data.Name;

            // ⭐ CRITICAL — persistence marker
            pet.modData["Kiyomi.InstantPetID"] = data.InstantPetID;

            farm.addCharacter(pet);
        }
        //Work together with OnSaveLoad() to store pet data in PetBank and then recall the Pets when the method SpawnInstantPet is called with the Instantpet Data in OnSaveLoaded()







        private void OpenPetConfigMenu()
        {
            Game1.activeClickableMenu = new PetConfigMenu(Config, OnConfigSaved, OnSpawnPetRequested);
        }

        private void OnSpawnPetRequested()
        {

            Monitor.Log("Spawn Pet button clicked! Spawning pet...", LogLevel.Info);
            
            AddPetToFarm();
        }

        private void OnConfigSaved(ModConfig newConfig)
        {
            Config = newConfig;
            Helper.WriteConfig(Config);
            Monitor.Log($"Pet config saved: {Config.PetType} named {Config.PetName}", LogLevel.Info);

            // Always attempt to add/update pet when config is saved
            UpdateOrAddPet();
        }

        private void UpdateOrAddPet()
        {
            Pet? existingPet = null;

            // Find existing pet
            foreach (var character in Game1.getFarm().characters)
            {
                if (character is Pet pet &&
    pet.modData.ContainsKey("Kiyomi.InstantPetID"))
                {
                    existingPet = pet;
                    break;
                }
            }

            if (existingPet == null)
            {
                // No pet exists, spawn a new one
                Monitor.Log("No existing pet found. Spawning new pet...", LogLevel.Info);
                AddPetToFarm();
                return;
            }

            // Check if pet type changed
            string currentPetType = existingPet.petType.Value;
            bool typeChanged = !currentPetType.Equals(Config.PetType, StringComparison.OrdinalIgnoreCase);

            if (typeChanged)
            {
                Monitor.Log($"Pet type changed from {currentPetType} to {Config.PetType}. Replacing pet...", LogLevel.Info);

                // Remove old pet
                Game1.getFarm().characters.Remove(existingPet);

                // Spawn new pet
                AddPetToFarm();
            }
            else
            {
                // Just update the name
                existingPet.Name = Config.PetName;
                existingPet.displayName = Config.PetName;
                Monitor.Log($"Updated existing pet name to {Config.PetName}", LogLevel.Info);
            }
        }

        private void AddPetToFarm()
        {
            try
            {// Check if multiple pets are allowed
                if (!Config.AllowMultiplePets)
                {
                    // Count existing mod-spawned pets
                    int existingModPetsCount = Game1.getFarm().characters.Count(c =>
                        c is Pet pet && pet.modData.ContainsKey("Kiyomi.InstantPetID"));

                    if (existingModPetsCount > 0)
                    {
                        Monitor.Log($"Cannot spawn pet: 'Allow Multiple Pets' is disabled and {existingModPetsCount} mod pet(s) already exist.", LogLevel.Info);
                        Game1.addHUDMessage(new HUDMessage("Cannot adopt: Multiple pets not allowed (check config menu)", HUDMessage.error_type));
                        return;
                    }


                    string petType = Config.PetType.Trim();
                    string petName = Config.PetName ?? "Pet";


                    int spawnX, spawnY;



                    // Check if player is on the farm
                    if (Game1.player.currentLocation is Farm farm)
                    {
                        Monitor.Log("Player is on farm. Attempting to spawn pet near player...", LogLevel.Debug);

                        // Try to find valid tile near player
                        int playerTileX = (int)(Game1.player.Position.X / 64f);
                        int playerTileY = (int)(Game1.player.Position.Y / 64f);
                        Vector2? validTile = FindValidSpawnTile(farm, playerTileX, playerTileY);

                        if (validTile.HasValue)
                        {
                            spawnX = (int)validTile.Value.X;
                            spawnY = (int)validTile.Value.Y;
                            Monitor.Log($"Spawning pet near player at ({spawnX}, {spawnY})", LogLevel.Debug);
                        }
                        else
                        {
                            // No valid spot near player, spawn at farmhouse entrance
                            Monitor.Log("No valid spawn location near player, using farmhouse entrance", LogLevel.Debug);
                            Point farmhouseEntry = farm.GetMainFarmHouseEntry();
                            spawnX = farmhouseEntry.X;
                            spawnY = farmhouseEntry.Y + 1;
                        }
                    }
                    else
                    {
                        // Player is not on farm, spawn at farmhouse entrance
                        Monitor.Log("Player is not on farm. Spawning pet at farmhouse entrance.", LogLevel.Debug);
                        Farm farmLocation = Game1.getFarm();
                        Point farmhouseEntry = farmLocation.GetMainFarmHouseEntry();
                        spawnX = farmhouseEntry.X;
                        spawnY = farmhouseEntry.Y + 1;
                    }

                    Pet pet;


                    if (petType.Equals("Cat", StringComparison.OrdinalIgnoreCase))
                    {
                        pet = new Pet(spawnX, spawnY, "0", "Cat");
                        if (string.IsNullOrWhiteSpace(petName) || petName == "Max")
                        {
                            petName = "Ruby";
                        }
                        Monitor.Log("Adding cat...", LogLevel.Debug);
                    }
                    else if (petType.Equals("Dog", StringComparison.OrdinalIgnoreCase))
                    {
                        pet = new Pet(spawnX, spawnY, "0", "Dog");
                        if (string.IsNullOrWhiteSpace(petName) || petName == "Ruby")
                        {
                            petName = "Max";
                        }
                        Monitor.Log("Adding dog...", LogLevel.Debug);
                    }
                    else
                    {
                        Monitor.Log($"Invalid PetType '{petType}' in config. Defaulting to Dog.", LogLevel.Warn);
                        pet = new Pet(spawnX, spawnY, "0", "Dog");
                        if (string.IsNullOrWhiteSpace(petName))
                        {
                            petName = "Max";
                        }
                    }

                    pet.Name = petName;
                    pet.displayName = petName;
                    pet.Manners = 0;

                    pet.modData["Kiyomi.InstantPetID"] =
        Guid.NewGuid().ToString();

                    Monitor.Log(
                        $"Registered Instant Pet ID: {pet.modData["Kiyomi.InstantPetID"]}",
                        LogLevel.Debug
                    );


                    // Add pet to the farm
                    Game1.getFarm().characters.Add(pet);

                    Monitor.Log($"Successfully added {pet.displayName} ({pet.petType.Value}) to the farm at ({spawnX}, {spawnY})!", LogLevel.Info);
                    Game1.drawObjectDialogue($"Your pet {pet.displayName} has appeared on the farm!");
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error adding pet: {ex.Message}", LogLevel.Error);
            }
        }

        private Vector2? FindValidSpawnTile(GameLocation location, int centerX, int centerY)
        {
            // Check 3x3 area around the player (starting from center, spiraling out)
            for (int radius = 0; radius <= 1; radius++)
            {
                for (int xOffset = -radius; xOffset <= radius; xOffset++)
                {
                    for (int yOffset = -radius; yOffset <= radius; yOffset++)
                    {
                        int tileX = centerX + xOffset;
                        int tileY = centerY + yOffset;

                        if (IsTileValidForPetSpawn(location, tileX, tileY))
                        {
                            Monitor.Log($"Found valid spawn tile at ({tileX}, {tileY})", LogLevel.Debug);
                            return new Vector2(tileX, tileY);
                        }
                    }
                }
            }

            return null;
        }

        private bool IsTileValidForPetSpawn(GameLocation location, int tileX, int tileY)
        {
            try
            {
                Vector2 tile = new Vector2(tileX, tileY);

                // Check if tile is within map bounds
                if (tileX < 0 || tileY < 0 || tileX >= location.Map.Layers[0].LayerWidth || tileY >= location.Map.Layers[0].LayerHeight)
                {
                    return false;
                }

                // Check if tile is passable
                if (!location.isTilePassable(new xTile.Dimensions.Location(tileX, tileY), Game1.viewport))
                {
                    return false;
                }

                // Check if there's an object blocking the tile
                if (location.isObjectAtTile(tileX, tileY))
                {
                    return false;
                }

                // Check if there's a character already on this tile
                foreach (var character in location.characters)
                {
                    if (character.Tile == tile)
                    {
                        return false;
                    }
                }

                // Check if player is on this tile
                if (Game1.player.Tile == tile)
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error checking tile validity at ({tileX}, {tileY}): {ex.Message}", LogLevel.Error);
                return false;
            }
        }
    }
}
