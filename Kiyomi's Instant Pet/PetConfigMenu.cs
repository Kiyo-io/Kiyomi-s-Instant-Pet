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

        private TextBox nameTextBox;
        private ClickableComponent nameTextBoxCC;

        private ClickableTextureComponent dogButton;
        private ClickableTextureComponent catButton;
        private ClickableTextureComponent okButton;

        private string selectedPetType;
        private bool isNameBoxSelected = false;

        // Drag support
        private bool isDragging = false;
        private Point dragOffset;
        private Rectangle titleBarBounds;

        public PetConfigMenu(ModConfig config, Action<ModConfig> onSave) : base(
            (Game1.uiViewport.Width - 600) / 2,
            (Game1.uiViewport.Height - 400) / 2,
            600,
            400)
        {
            Config = config;
            OnSave = onSave;
            selectedPetType = config.PetType;

            // Define title bar for dragging (top 64 pixels of the menu)
            titleBarBounds = new Rectangle(xPositionOnScreen, yPositionOnScreen, width, 64);

            UpdateComponentPositions();
            nameTextBox.Text = config.PetName;
        }

        private void UpdateComponentPositions()
        {
            // Update title bar
            titleBarBounds = new Rectangle(xPositionOnScreen, yPositionOnScreen, width, 64);

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

            // Dog button
            dogButton = new ClickableTextureComponent(
                new Rectangle(xPositionOnScreen + 150, yPositionOnScreen + 200, 64, 64),
                Game1.mouseCursors,
                new Rectangle(160, 208, 16, 16),
                4f)
            {
                name = "Dog"
            };

            // Cat button
            catButton = new ClickableTextureComponent(
                new Rectangle(xPositionOnScreen + 350, yPositionOnScreen + 200, 64, 64),
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
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            // Check if clicking on title bar to start dragging
            if (titleBarBounds.Contains(x, y))
            {
                isDragging = true;
                dragOffset = new Point(x - xPositionOnScreen, y - yPositionOnScreen);
                return;
            }

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

            // Check OK button click
            if (okButton.containsPoint(x, y))
            {
                SaveAndClose();
            }
        }

        public override void leftClickHeld(int x, int y)
        {
            base.leftClickHeld(x, y);

            if (isDragging)
            {
                // Update menu position
                xPositionOnScreen = x - dragOffset.X;
                yPositionOnScreen = y - dragOffset.Y;

                // Keep menu within screen bounds
                xPositionOnScreen = Math.Max(0, Math.Min(xPositionOnScreen, Game1.uiViewport.Width - width));
                yPositionOnScreen = Math.Max(0, Math.Min(yPositionOnScreen, Game1.uiViewport.Height - height));

                // Update all component positions
                UpdateComponentPositions();
            }
        }

        public override void releaseLeftClick(int x, int y)
        {
            base.releaseLeftClick(x, y);
            isDragging = false;
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

            // Change cursor when hovering over title bar
            if (titleBarBounds.Contains(x, y) && !isDragging)
            {
                Game1.mouseCursor = 6; // Hand cursor
            }
        }

        private void SaveAndClose()
        {
            Config.PetType = selectedPetType;
            Config.PetName = string.IsNullOrWhiteSpace(nameTextBox.Text) ? "Pet" : nameTextBox.Text;
            
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

            // Highlight title bar if hovering (to show it's draggable)
            int mouseX = Game1.getMouseX();
            int mouseY = Game1.getMouseY();
            if (titleBarBounds.Contains(mouseX, mouseY) || isDragging)
            {
                b.Draw(Game1.fadeToBlackRect, titleBarBounds, Color.White * 0.1f);
            }

            // Draw title
            string title = "Configure Your Pet";
            SpriteText.drawStringHorizontallyCenteredAt(b, title, xPositionOnScreen + width / 2, yPositionOnScreen + 32);

            // Draw drag hint
            if (titleBarBounds.Contains(mouseX, mouseY) && !isDragging)
            {
                b.DrawString(Game1.tinyFont, "(Drag to move)", 
                    new Vector2(xPositionOnScreen + 10, yPositionOnScreen + 10), 
                    Color.Gray * 0.8f);
            }

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
                b.DrawString(Game1.smallFont, "Dog", new Vector2(dogButton.bounds.X + 80, dogButton.bounds.Y + 20), Game1.textColor);
            }
            else if (selectedPetType == "Cat")
            {
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(375, 357, 3, 3),
                    catButton.bounds.X - 8, catButton.bounds.Y - 8,
                    catButton.bounds.Width + 16, catButton.bounds.Height + 16,
                    Color.White, 4f);
                b.DrawString(Game1.smallFont, "Cat", new Vector2(catButton.bounds.X + 80, catButton.bounds.Y + 20), Game1.textColor);
            }

            // Draw OK button
            okButton.draw(b);

            // Draw mouse cursor
            drawMouse(b);
        }
    }
}
