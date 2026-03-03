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
        private static IMonitor ModMonitor;
        private ModConfig Config;
        private Rectangle petConfigButtonBounds;

        public override void Entry(IModHelper helper)
        {
            ModMonitor = Monitor;

            Config = helper.ReadConfig<ModConfig>();

            var harmony = new Harmony(ModManifest.UniqueID);
            InstantPetPatches.ApplyPatches(harmony, Monitor);

            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.Display.RenderedHud += OnRenderedHud;
            helper.Events.Input.ButtonPressed += OnButtonPressed;

            // Initialize button bounds (top-left corner for visibility)
            petConfigButtonBounds = new Rectangle(10, 10, 64, 64);
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (!Game1.player.hasPet())
            {
                Monitor.Log("No pet detected. Adding pet instantly...", LogLevel.Info);
                AddPetToFarm();
            }
            else
            {
                Monitor.Log("Player already has a pet. No action needed.", LogLevel.Info);
            }
        }

        private void OnRenderedHud(object sender, RenderedHudEventArgs e)
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

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
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

            // Check if pet type or name changed
            UpdateOrReplacePet();
        }

        private void UpdateOrReplacePet()
        {
            Pet existingPet = null;

            // Find existing pet
            foreach (var character in Game1.getFarm().characters)
            {
                if (character is Pet pet)
                {
                    existingPet = pet;
                    break;
                }
            }

            if (existingPet == null)
            {
                Monitor.Log("No existing pet found to update.", LogLevel.Debug);
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

                // Spawn new pet at player's location
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
            {
                string petType = Config.PetType.Trim();
                string petName = Config.PetName ?? "Pet";

                // Get or create pet bowl
                var petBowl = GetOrCreatePetBowl();
                if (petBowl == null)
                {
                    Monitor.Log("Failed to create or find pet bowl!", LogLevel.Error);
                    Game1.drawObjectDialogue("Failed to set up pet bowl!");
                    return;
                }

                int spawnX, spawnY;

                // Check if player is on the farm
                if (Game1.player.currentLocation is Farm)
                {
                    Monitor.Log("Player is on farm. Attempting to spawn pet near player...", LogLevel.Debug);

                    // Try to find valid tile near player
                    int playerTileX = (int)(Game1.player.Position.X / 64f);
                    int playerTileY = (int)(Game1.player.Position.Y / 64f);
                    Vector2? validTile = FindValidSpawnTile(Game1.getFarm(), playerTileX, playerTileY);

                    if (validTile.HasValue)
                    {
                        spawnX = (int)validTile.Value.X;
                        spawnY = (int)validTile.Value.Y;
                        Monitor.Log($"Spawning pet near player at ({spawnX}, {spawnY})", LogLevel.Debug);
                    }
                    else
                    {
                        // Fallback to bowl location if no valid spot near player
                        Monitor.Log("No valid spawn location near player, using bowl location", LogLevel.Debug);
                        Point bowlPoint = petBowl.GetPetSpot();
                        spawnX = bowlPoint.X;
                        spawnY = bowlPoint.Y;
                    }
                }
                else
                {
                    // Player is not on farm, spawn at bowl
                    Monitor.Log("Player is not on farm. Spawning pet at bowl location.", LogLevel.Debug);
                    Point bowlPoint = petBowl.GetPetSpot();
                    spawnX = bowlPoint.X;
                    spawnY = bowlPoint.Y;
                }

                Pet pet;

                if (petType.Equals("Cat", StringComparison.OrdinalIgnoreCase))
                {
                    pet = new Pet(spawnX, spawnY, "0", "Cat");
                    Game1.player.whichPetType = "Cat";
                    if (string.IsNullOrWhiteSpace(petName) || petName == "Max")
                    {
                        petName = "Ruby";
                    }
                    Monitor.Log("Adding cat...", LogLevel.Debug);
                }
                else if (petType.Equals("Dog", StringComparison.OrdinalIgnoreCase))
                {
                    pet = new Pet(spawnX, spawnY, "0", "Dog");
                    Game1.player.whichPetType = "Dog";
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
                    Game1.player.whichPetType = "Dog";
                    if (string.IsNullOrWhiteSpace(petName))
                    {
                        petName = "Max";
                    }
                }

                pet.Name = petName;
                pet.displayName = petName;
                pet.Manners = 0;

                // Link pet to bowl
                petBowl.AssignPet(pet);

                // Add pet to the farm
                Game1.getFarm().characters.Add(pet);

                Monitor.Log($"Successfully added {pet.displayName} ({Game1.player.whichPetType}) to the farm at ({spawnX}, {spawnY})!", LogLevel.Info);
                Game1.drawObjectDialogue($"Your pet {pet.displayName} has appeared on the farm!");
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error adding pet: {ex.Message}", LogLevel.Error);
            }
        }

        private StardewValley.Buildings.PetBowl GetOrCreatePetBowl()
        {
            try
            {
                Farm farm = Game1.getFarm();

                // Check if a pet bowl already exists
                foreach (var building in farm.buildings)
                {
                    if (building is StardewValley.Buildings.PetBowl bowl)
                    {
                        Monitor.Log($"Found existing pet bowl at ({bowl.tileX.Value}, {bowl.tileY.Value})", LogLevel.Debug);
                        return bowl;
                    }
                }

                // No bowl exists, create one at default location
                Monitor.Log("No pet bowl found. Creating new bowl at default location...", LogLevel.Info);

                // Default pet bowl location (same as vanilla - near farmhouse)
                int bowlX = 54;
                int bowlY = 7;

                // Create the pet bowl building
                var petBowl = new StardewValley.Buildings.PetBowl(new Vector2(bowlX, bowlY));

                // Add to farm
                farm.buildings.Add(petBowl);

                Monitor.Log($"Created pet bowl at ({bowlX}, {bowlY})", LogLevel.Info);
                return petBowl;
            }
            catch (Exception ex)
            {
                Monitor.Log($"Error in GetOrCreatePetBowl: {ex.Message}", LogLevel.Error);
                return null;
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
