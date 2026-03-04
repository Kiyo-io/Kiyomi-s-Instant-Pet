using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;
using StardewValley.BellsAndWhistles;

namespace Kiyomi_s_Instant_Pet
{
    internal class PetConfigMenu : IClickableMenu
    {
        private ModConfig Config;
        private Action<ModConfig> OnSave;
        private Action SpawnPetAction;

        private TextBox nameTextBox;
        private ClickableComponent nameTextBoxCC;

        private ClickableTextureComponent dogButton;
        private ClickableTextureComponent catButton;
        private ClickableTextureComponent okButton;
        private ClickableTextureComponent spawnPetButton;
        private ClickableTextureComponent allowMultiplePetsCheckbox;

        private string selectedPetType;
        private bool isNameBoxSelected = false;
        private bool allowMultiplePets;

        public PetConfigMenu(ModConfig config, Action<ModConfig> onSave, Action spawnPetAction) : base(
            (Game1.uiViewport.Width - 640) / 2,
            (Game1.uiViewport.Height - 540) / 2,
            640,
            540)
        {
            Config = config;
            OnSave = onSave;
            SpawnPetAction = spawnPetAction;
            selectedPetType = config.PetType;
            allowMultiplePets = config.AllowMultiplePets;

            UpdateComponentPositions();
            nameTextBox.Text = config.PetName;
        }

        private void UpdateComponentPositions()
        {
            // Name textbox
            nameTextBox = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
            {
                X = xPositionOnScreen + 200,
                Y = yPositionOnScreen + 120,
                Width = 300
            };

            nameTextBoxCC = new ClickableComponent(
                new Rectangle(nameTextBox.X, nameTextBox.Y, nameTextBox.Width, 48),
                "nameTextBox"
            );

            // Dog button (centered layout, 68 pixels left of center + button width, moved 10px right)
            dogButton = new ClickableTextureComponent(
                new Rectangle(xPositionOnScreen + (width / 2) - 122, yPositionOnScreen + 200, 64, 64),
                Game1.mouseCursors,
                new Rectangle(160, 208, 16, 16),
                4f)
            {
                name = "Dog"
            };

            // Cat button (centered layout, 68 pixels right of center)
            catButton = new ClickableTextureComponent(
                new Rectangle(xPositionOnScreen + (width / 2) + 68, yPositionOnScreen + 200, 64, 64),
                Game1.mouseCursors,
                new Rectangle(192, 208, 16, 16),
                4f)
            {
                name = "Cat"
            };

            // OK button
            okButton = new ClickableTextureComponent(
                new Rectangle(xPositionOnScreen + width - 100, yPositionOnScreen + height - 80, 64, 64),
                Game1.mouseCursors,
                Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46),
                1f)
            {
                name = "OK"
            };

            // Spawn Pet button (+ icon)
            spawnPetButton = new ClickableTextureComponent(
                new Rectangle(xPositionOnScreen + (width / 2) - 40, yPositionOnScreen + 290, 80, 80),
                Game1.mouseCursors,
                new Rectangle(0, 410, 16, 16), // Plus sign icon
                5f)
            {
                name = "SpawnPet"
            };

            // Allow Multiple Pets checkbox (centered)
            string allowMultipleLabel = "Allow Multiple Pets";
            Vector2 allowMultipleLabelSize = Game1.smallFont.MeasureString(allowMultipleLabel);
            int totalCheckboxWidth = 36 + 10 + (int)allowMultipleLabelSize.X;
            int centeredCheckboxX = xPositionOnScreen + (width / 2) - (totalCheckboxWidth / 2);
            
            allowMultiplePetsCheckbox = new ClickableTextureComponent(
                new Rectangle(centeredCheckboxX, yPositionOnScreen + 427, 36, 36),
                Game1.mouseCursors,
                allowMultiplePets ? new Rectangle(236, 425, 9, 9) : new Rectangle(227, 425, 9, 9),
                4f)
            {
                name = "AllowMultiplePetsCheckbox"
            };
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            // Check name textbox click
            if (nameTextBoxCC.containsPoint(x, y))
            {
                nameTextBox.Selected = true;
                isNameBoxSelected = true;
            }
            else
            {
                nameTextBox.Selected = false;
                isNameBoxSelected = false;
            }

            // Check dog button click
            if (dogButton.containsPoint(x, y))
            {
                selectedPetType = "Dog";
                if (string.IsNullOrWhiteSpace(nameTextBox.Text) || nameTextBox.Text == "Ruby")
                {
                    nameTextBox.Text = "Max";
                }
                Game1.playSound("dogWhining");
            }

            // Check cat button click
            if (catButton.containsPoint(x, y))
            {
                selectedPetType = "Cat";
                if (string.IsNullOrWhiteSpace(nameTextBox.Text) || nameTextBox.Text == "Max")
                {
                    nameTextBox.Text = "Ruby";
                }
                Game1.playSound("cat");
            }

            // Check Spawn Pet button click
            if (spawnPetButton.containsPoint(x, y))
            {
                // Save current config first
                Config.PetType = selectedPetType;
                Config.PetName = string.IsNullOrWhiteSpace(nameTextBox.Text) ? (selectedPetType == "Cat" ? "Ruby" : "Max") : nameTextBox.Text;
                Config.AllowMultiplePets = allowMultiplePets;

                // Call spawn action
                SpawnPetAction?.Invoke();

                Game1.playSound("coin");
                Game1.addHUDMessage(new HUDMessage($"Spawning {Config.PetName} the {Config.PetType}!", 2));
                return;
            }

            // Check Allow Multiple Pets checkbox
            if (allowMultiplePetsCheckbox.containsPoint(x, y))
            {
                allowMultiplePets = !allowMultiplePets;
                // Update checkbox appearance
                allowMultiplePetsCheckbox.sourceRect = allowMultiplePets ? 
                    new Rectangle(236, 425, 9, 9) : new Rectangle(227, 425, 9, 9);
                Game1.playSound("drumkit6");
            }

            // Check OK button click
            if (okButton.containsPoint(x, y))
            {
                SaveAndClose();
            }
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Escape)
            {
                exitThisMenu();
                return;
            }

            if (isNameBoxSelected)
            {
                nameTextBox.RecieveCommandInput('\0');
            }

        }

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);

            dogButton.tryHover(x, y, 0.25f);
            catButton.tryHover(x, y, 0.25f);
            okButton.tryHover(x, y, 0.25f);
            spawnPetButton.tryHover(x, y, 0.25f);
            allowMultiplePetsCheckbox.tryHover(x, y, 0.25f);
        }

        private void SaveAndClose()
        {
            Config.PetType = selectedPetType;
            Config.PetName = string.IsNullOrWhiteSpace(nameTextBox.Text) ? "Pet" : nameTextBox.Text;
            Config.AllowMultiplePets = allowMultiplePets;

            OnSave?.Invoke(Config);

            Game1.playSound("coin");
            exitThisMenu();
        }

        public override void draw(SpriteBatch b)
        {
            // Draw background fade
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);

            // Draw menu box
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, false, true);

            // Draw title
            string title = "Configure Your Pet";
            SpriteText.drawStringHorizontallyCenteredAt(b, title, xPositionOnScreen + width / 2, yPositionOnScreen + 32);

            // Draw "Name:" label
            b.DrawString(Game1.smallFont, "Pet Name:", new Vector2(xPositionOnScreen + 80, yPositionOnScreen + 130), Game1.textColor);

            // Draw name textbox
            nameTextBox.Draw(b);

            // Draw "Type:" label
            b.DrawString(Game1.smallFont, "Pet Type:", new Vector2(xPositionOnScreen + 80, yPositionOnScreen + 210), Game1.textColor);

            // Draw pet type buttons
            dogButton.draw(b);
            catButton.draw(b);

            // Draw selection indicator
            if (selectedPetType == "Dog")
            {
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(375, 357, 3, 3),
                    dogButton.bounds.X - 8, dogButton.bounds.Y - 8,
                    dogButton.bounds.Width + 16, dogButton.bounds.Height + 16,
                    Color.White, 4f);
            }
            else if (selectedPetType == "Cat")
            {
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(375, 357, 3, 3),
                    catButton.bounds.X - 8, catButton.bounds.Y - 8,
                    catButton.bounds.Width + 16, catButton.bounds.Height + 16,
                    Color.White, 4f);
            }

            // Draw Spawn Pet button
            spawnPetButton.draw(b);

            // Draw label for spawn button
            string spawnLabel = "Spawn New Pet";
            Vector2 spawnLabelSize = Game1.smallFont.MeasureString(spawnLabel);
            b.DrawString(Game1.smallFont, spawnLabel, 
                new Vector2(xPositionOnScreen + (width / 2) - (spawnLabelSize.X / 2), spawnPetButton.bounds.Y + 85), 
                Game1.textColor);

            // Draw hover tooltip for spawn button
            if (spawnPetButton.containsPoint(Game1.getMouseX(), Game1.getMouseY()))
            {
                IClickableMenu.drawHoverText(b, "Spawn a new pet instantly\n(Use if pet was removed)", Game1.smallFont);
            }

            // Draw Allow Multiple Pets checkbox (centered)
            string allowMultipleLabel = "Allow Multiple Pets";
            Vector2 allowMultipleLabelSize = Game1.smallFont.MeasureString(allowMultipleLabel);
            int totalCheckboxWidth = 36 + 10 + (int)allowMultipleLabelSize.X; // checkbox + spacing + text
            int centeredCheckboxX = xPositionOnScreen + (width / 2) - (totalCheckboxWidth / 2);
            
            allowMultiplePetsCheckbox.draw(b);
            b.DrawString(Game1.smallFont, allowMultipleLabel, 
                new Vector2(centeredCheckboxX + 36 + 10, allowMultiplePetsCheckbox.bounds.Y + 8), 
                Game1.textColor);

            // Draw OK button
            okButton.draw(b);

            // Draw mouse cursor
            drawMouse(b);
        }
    }
}
