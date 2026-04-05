// Framework/MarryMenu.cs
// Marriage selection menu using vanilla SDV IClickableMenu components.
// Replaces the SpaceShared UI dependency from the original mod.

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace PreexistingRelationship.Framework
{
    internal class MarryMenu : IClickableMenu
    {
        /*──────────────────────────────────────────────────────────────
         *  Constants
         *──────────────────────────────────────────────────────────────*/

        private const int MenuWidth = 800;
        private const int MenuHeight = 700;
        private const int PortraitScale = 2;
        private const int PortraitSize = 64;      // source pixels
        private const int CellWidth = 230;
        private const int CellHeight = 200;
        private const int Columns = 3;
        private const int GridLeft = 50;
        private const int GridTop = 225;
        private const int GridAreaHeight = 420;    // visible scrollable area

        /*──────────────────────────────────────────────────────────────
         *  Fields
         *──────────────────────────────────────────────────────────────*/

        private readonly List<NPC> validNpcs;
        private int selectedIndex = -1;
        private int scrollOffset;              // row offset for scrolling
        private int totalRows;

        private ClickableTextureComponent upArrow;
        private ClickableTextureComponent downArrow;
        private ClickableComponent acceptButton;
        private ClickableComponent cancelButton;

        /*──────────────────────────────────────────────────────────────
         *  Constructor
         *──────────────────────────────────────────────────────────────*/

        public MarryMenu()
            : base(
                (Game1.uiViewport.Width - MenuWidth) / 2,
                (Game1.uiViewport.Height - MenuHeight) / 2,
                MenuWidth,
                MenuHeight)
        {
            // Gather datable, unmarried NPCs.
            this.validNpcs = new List<NPC>();
            foreach (var npc in Utility.getAllCharacters())
            {
                if (npc.datable.Value && npc.getSpouse() == null)
                    this.validNpcs.Add(npc);
            }

            this.validNpcs.Sort((a, b) =>
                string.Compare(a.Name, b.Name, StringComparison.Ordinal));

            this.totalRows = (this.validNpcs.Count + Columns - 1) / Columns;

            this.SetUpPositions();
        }

        /// <summary>Create button components positioned relative to the menu.</summary>
        private void SetUpPositions()
        {
            // Scroll arrows
            this.upArrow = new ClickableTextureComponent(
                new Rectangle(
                    this.xPositionOnScreen + this.width - 64,
                    this.yPositionOnScreen + GridTop,
                    44, 48),
                Game1.mouseCursors,
                new Rectangle(421, 459, 11, 12),
                4f);

            this.downArrow = new ClickableTextureComponent(
                new Rectangle(
                    this.xPositionOnScreen + this.width - 64,
                    this.yPositionOnScreen + GridTop + GridAreaHeight - 48,
                    44, 48),
                Game1.mouseCursors,
                new Rectangle(421, 472, 11, 12),
                4f);

            // Accept / Cancel labels as clickable components
            this.cancelButton = new ClickableComponent(
                new Rectangle(
                    this.xPositionOnScreen + 100,
                    this.yPositionOnScreen + this.height - 60,
                    200, 48),
                "cancel");

            this.acceptButton = new ClickableComponent(
                new Rectangle(
                    this.xPositionOnScreen + this.width - 300,
                    this.yPositionOnScreen + this.height - 60,
                    200, 48),
                "accept");
        }

        /*──────────────────────────────────────────────────────────────
         *  Input handling
         *──────────────────────────────────────────────────────────────*/

        /// <inheritdoc />
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            // Scroll arrows
            if (this.upArrow.containsPoint(x, y) && this.scrollOffset > 0)
            {
                this.scrollOffset--;
                Game1.playSound("shwip");
                return;
            }

            if (this.downArrow.containsPoint(x, y) &&
                this.scrollOffset < this.totalRows - this.VisibleRows())
            {
                this.scrollOffset++;
                Game1.playSound("shwip");
                return;
            }

            // Cancel
            if (this.cancelButton.containsPoint(x, y))
            {
                Game1.exitActiveMenu();
                Game1.playSound("bigDeSelect");
                return;
            }

            // Accept
            if (this.acceptButton.containsPoint(x, y))
            {
                this.DoMarriage();
                return;
            }

            // NPC portrait grid click
            int clicked = this.GetCellIndexAt(x, y);
            if (clicked >= 0 && clicked < this.validNpcs.Count)
            {
                this.selectedIndex = clicked;
                Game1.playSound("smallSelect");
            }
        }

        /// <inheritdoc />
        public override void receiveScrollWheelAction(int direction)
        {
            int maxScroll = Math.Max(0, this.totalRows - this.VisibleRows());
            if (direction > 0 && this.scrollOffset > 0)
                this.scrollOffset--;
            else if (direction < 0 && this.scrollOffset < maxScroll)
                this.scrollOffset++;
        }

        /// <inheritdoc />
        public override bool overrideSnappyMenuCursorMovementBan()
        {
            return true;
        }

        /*──────────────────────────────────────────────────────────────
         *  Drawing
         *──────────────────────────────────────────────────────────────*/

        /// <inheritdoc />
        public override void draw(SpriteBatch b)
        {
            // Dimmed background
            b.Draw(Game1.fadeToBlackRect,
                Game1.graphics.GraphicsDevice.Viewport.Bounds,
                Color.Black * 0.75f);

            // Menu box
            IClickableMenu.drawTextureBox(b,
                this.xPositionOnScreen, this.yPositionOnScreen,
                this.width, this.height, Color.White);

            // Title
            string title = ModEntry.I18n.Get("menu.title");
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            b.DrawString(Game1.dialogueFont, title,
                new Vector2(
                    this.xPositionOnScreen + (this.width - titleSize.X) / 2,
                    this.yPositionOnScreen + 16),
                Game1.textColor);

            // Subtitle / flavour text
            string subtitle = ModEntry.I18n.Get("menu.text");
            b.DrawString(Game1.smallFont, subtitle,
                new Vector2(
                    this.xPositionOnScreen + 50,
                    this.yPositionOnScreen + 80),
                Game1.textColor);

            // NPC portrait grid — clipped to grid area
            int visibleRows = this.VisibleRows();
            int gridAbsTop = this.yPositionOnScreen + GridTop;

            for (int row = this.scrollOffset;
                 row < Math.Min(this.scrollOffset + visibleRows, this.totalRows);
                 row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    int idx = row * Columns + col;
                    if (idx >= this.validNpcs.Count)
                        continue;

                    int drawRow = row - this.scrollOffset;
                    int cellX = this.xPositionOnScreen + GridLeft + col * CellWidth;
                    int cellY = gridAbsTop + drawRow * CellHeight;

                    // Selection highlight
                    if (idx == this.selectedIndex)
                    {
                        IClickableMenu.drawTextureBox(b,
                            Game1.mouseCursors,
                            new Rectangle(375, 357, 3, 3),
                            cellX - 4, cellY - 4,
                            PortraitSize * PortraitScale + 8 + 96,
                            CellHeight - 10,
                            Color.Green, 4f, false);
                    }

                    // Portrait frame (from mouseCursors)
                    b.Draw(Game1.mouseCursors,
                        new Vector2(cellX, cellY),
                        new Rectangle(583, 411, 115, 97),
                        Color.White, 0f, Vector2.Zero,
                        PortraitScale, SpriteEffects.None, 0.88f);

                    // NPC portrait
                    NPC npc = this.validNpcs[idx];
                    b.Draw(npc.Portrait,
                        new Vector2(cellX + 50, cellY + 16),
                        new Rectangle(0, 128, 64, 64),
                        Color.White, 0f, Vector2.Zero,
                        PortraitScale, SpriteEffects.None, 0.88f);

                    // NPC display name
                    string name = npc.displayName;
                    Vector2 nameSize = Game1.smallFont.MeasureString(name);
                    b.DrawString(Game1.smallFont, name,
                        new Vector2(
                            cellX + 115 - nameSize.X / 2,
                            cellY + 160),
                        Game1.textColor);
                }
            }

            // Scroll arrows (only when needed)
            if (this.scrollOffset > 0)
                this.upArrow.draw(b);
            if (this.scrollOffset < this.totalRows - visibleRows)
                this.downArrow.draw(b);

            // Cancel button
            string cancelText = ModEntry.I18n.Get("menu.button.cancel");
            Utility.drawTextWithShadow(b, cancelText, Game1.dialogueFont,
                new Vector2(this.cancelButton.bounds.X, this.cancelButton.bounds.Y),
                Game1.textColor);

            // Accept button
            string acceptText = ModEntry.I18n.Get("menu.button.accept");
            Utility.drawTextWithShadow(b, acceptText, Game1.dialogueFont,
                new Vector2(this.acceptButton.bounds.X, this.acceptButton.bounds.Y),
                Game1.textColor);

            // Mouse cursor on top
            this.drawMouse(b);
        }

        /*──────────────────────────────────────────────────────────────
         *  Helpers
         *──────────────────────────────────────────────────────────────*/

        /// <summary>How many full rows fit in the visible grid area.</summary>
        private int VisibleRows()
        {
            return GridAreaHeight / CellHeight;
        }

        /// <summary>Map a screen coordinate to a cell index, or -1 if none.</summary>
        private int GetCellIndexAt(int x, int y)
        {
            int gridAbsLeft = this.xPositionOnScreen + GridLeft;
            int gridAbsTop = this.yPositionOnScreen + GridTop;

            int relX = x - gridAbsLeft;
            int relY = y - gridAbsTop;

            if (relX < 0 || relY < 0)
                return -1;

            int col = relX / CellWidth;
            int row = relY / CellHeight + this.scrollOffset;

            if (col >= Columns || row >= this.totalRows)
                return -1;

            int idx = row * Columns + col;
            return idx < this.validNpcs.Count ? idx : -1;
        }

        /// <summary>Execute the marriage for the selected NPC.</summary>
        private void DoMarriage()
        {
            if (this.selectedIndex < 0 || this.selectedIndex >= this.validNpcs.Count)
                return;

            string npcName = this.validNpcs[this.selectedIndex].Name;

            ModEntry.Instance.Monitor.Log(
                $"Marrying {npcName}", StardewModdingAPI.LogLevel.Debug);

            // Check if another player already married this NPC.
            foreach (var player in Game1.getAllFarmers())
            {
                if (player.spouse == npcName)
                {
                    Game1.addHUDMessage(
                        new HUDMessage(ModEntry.I18n.Get("spouse-taken")));
                    this.selectedIndex = -1;
                    return;
                }
            }

            // In multiplayer, notify the host.
            if (!Game1.IsMasterGame)
            {
                ModEntry.Instance.Helper.Multiplayer.SendMessage(
                    new DoMarriageMessage { NpcName = npcName },
                    nameof(DoMarriageMessage),
                    new[] { ModEntry.Instance.ModManifest.UniqueID });
            }

            ModEntry.DoMarriage(Game1.player, npcName, true);

            this.selectedIndex = -1;
            Game1.exitActiveMenu();
        }
    }
}
