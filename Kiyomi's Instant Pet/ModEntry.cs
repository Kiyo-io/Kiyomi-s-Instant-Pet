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
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.Display.RenderedHud += OnRenderedHud;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
            helper.Events.Input.ButtonReleased += OnButtonReleased;

            // Initialize button bounds (top-right corner) - will be updated when game loads
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

            // Calculate opacity based on dragging state
            float opacity = isDraggingButton ? 0.5f : 1f;

            // Draw the pet icon with adjusted opacity
            e.SpriteBatch.Draw(
                Game1.mouseCursors,
                new Vector2(petConfigButtonBounds.X, petConfigButtonBounds.Y),
                new Rectangle(160, 208, 16, 16), // Dog icon
                Color.White * opacity,
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
                IClickableMenu.drawHoverText(
                    e.SpriteBatch,
                    isDraggingButton ? "Move Pet Button" : "Configure Pet (Right-click to move)",
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
            Game1.activeClickableMenu = new PetConfigMenu(Config, OnConfigSaved);
        }

        private void OnConfigSaved(ModConfig newConfig)
        {
            Config = newConfig;
            Helper.WriteConfig(Config);
            Monitor.Log($"Pet config saved: {Config.PetType} named {Config.PetName}", LogLevel.Info);

            // Update existing pet if one exists
            UpdateExistingPet();
        }

        private void UpdateExistingPet()
        {
            foreach (var character in Game1.getFarm().characters)
            {
                if (character is Pet pet)
                {
                    pet.Name = Config.PetName;
                    pet.displayName = Config.PetName;
                    Monitor.Log($"Updated existing pet name to {Config.PetName}", LogLevel.Info);
                    return;
                }
            }
        }

        private void AddPetToFarm()
        {
            try
            {
                string petType = Config.PetType.Trim();
                string petName = Config.PetName ?? "Pet";

                // Get player's current location and tile position
                GameLocation currentLocation = Game1.player.currentLocation;
                int playerTileX = (int)(Game1.player.Position.X / 64f);
                int playerTileY = (int)(Game1.player.Position.Y / 64f);

                // Find a valid spawn location within 3x3 around the player
                Vector2? validTile = FindValidSpawnTile(currentLocation, playerTileX, playerTileY);

                if (!validTile.HasValue)
                {
                    Monitor.Log("No valid spawn location found within 3x3 area around player.", LogLevel.Warn);
                    Game1.drawObjectDialogue("There's not enough room to spawn your pet here!");
                    return;
                }

                int spawnX = (int)validTile.Value.X;
                int spawnY = (int)validTile.Value.Y;

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

                // Add pet to current location (not just farm)
                currentLocation.characters.Add(pet);

                Monitor.Log($"Successfully added {pet.displayName} ({Game1.player.whichPetType}) at ({spawnX}, {spawnY}) in {currentLocation.Name}!", LogLevel.Info);
                Game1.drawObjectDialogue($"Your pet {pet.displayName} has appeared!");
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
