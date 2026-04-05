// ModEntry.cs
// Preexisting Relationship — start the game already married to an NPC.
// Originally by spacechase0 (MIT License). Updated for Stardew Valley 1.6+ by tbonehunter.
// SpaceShared dependency removed; all UI rebuilt on vanilla IClickableMenu.

using System;
using System.Collections.Generic;
using System.Linq;
using PreexistingRelationship.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Locations;

namespace PreexistingRelationship
{
    internal class ModEntry : Mod
    {
        /*──────────────────────────────────────────────────────────────
         *  Static / shared state
         *──────────────────────────────────────────────────────────────*/

        /// <summary>Singleton for helper access from menu classes.</summary>
        public static ModEntry Instance;

        /// <summary>Translation helper exposed for menu classes.</summary>
        public static ITranslationHelper I18n;

        /*──────────────────────────────────────────────────────────────
         *  Entry point
         *──────────────────────────────────────────────────────────────*/

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            I18n = helper.Translation;

            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Multiplayer.ModMessageReceived += this.OnMessageReceived;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;

            helper.ConsoleCommands.Add(
                "marry",
                "Opens the preexisting relationship spouse selection menu.",
                this.OnCommand);
        }
//// <summary>
        /// On each new day, strip any courtship tutorial mail that the
        /// game's overnight logic may have re-queued despite our flags.
        /// Only active while the player is married.
        /// </summary>
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            if (Game1.player.getSpouse() == null)
                return;

            string[] courtshipMailFlags = new[]
            {
                "Bouquet",
                "SeaAmulet",
                "abbySpiritBoard",
                "pennySpa",
                "samMessage",
                "joshMessage",
                "elliottBoat",
                "harveyBalloon",
                "EmilyClothingTherapy",
                "EmilyCamping",
                "haleyGarden"
            };

            foreach (string flag in courtshipMailFlags)
            {
                if (Game1.player.mailbox.Remove(flag))
                {
                    this.Monitor.Log(
                        $"Stripped re-queued mail '{flag}' from mailbox.", LogLevel.Trace);
                }

                if (!Game1.player.mailReceived.Contains(flag))
                    Game1.player.mailReceived.Add(flag);
            }
        }

        /*──────────────────────────────────────────────────────────────
         *  Event handlers
         *──────────────────────────────────────────────────────────────*/

        /// <summary>
        /// Each tick, check whether the player is free, has no menu open,
        /// hasn't already been offered the marriage prompt, has no spouse,
        /// and has finished character creation. If so, show the menu once.
        /// </summary>
        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsPlayerFree)
                return;
            if (Game1.activeClickableMenu != null)
                return;
            if (Game1.player.hasOrWillReceiveMail($"{this.ModManifest.UniqueID}/FreeMarriage"))
                return;
            if (Game1.player.getSpouse() != null)
                return;
            if (!Game1.player.isCustomized.Value)
                return;

            Game1.activeClickableMenu = new MarryMenu();
            Game1.player.mailReceived.Add($"{this.ModManifest.UniqueID}/FreeMarriage");
        }

        /// <summary>Handle multiplayer marriage messages from other players.</summary>
        private void OnMessageReceived(object sender, ModMessageReceivedEventArgs e)
        {
            if (e.Type != nameof(DoMarriageMessage))
                return;
            if (e.FromPlayerID == Game1.player.UniqueMultiplayerID)
                return;

            var msg = e.ReadAs<DoMarriageMessage>();
            var player = Game1.GetPlayer(e.FromPlayerID);

            if (player == null)
            {
                this.Monitor.Log(
                    $"Received marriage message from unknown player {e.FromPlayerID}",
                    LogLevel.Warn);
                return;
            }

            DoMarriage(player, msg.NpcName, false);
        }

        /// <summary>Console command handler — opens the marriage menu.</summary>
        private void OnCommand(string name, string[] args)
        {
            if (!Context.IsPlayerFree)
                return;

            if (Game1.player.getSpouse() != null)
            {
                this.Monitor.Log("You are already married.", LogLevel.Error);
                return;
            }

            Game1.activeClickableMenu = new MarryMenu();
        }

        /*──────────────────────────────────────────────────────────────
         *  Marriage logic
         *──────────────────────────────────────────────────────────────*/

        /// <summary>
        /// Perform the marriage between a player and an NPC.
        /// Upgrades the house, sets friendship data, marks courtship
        /// mail and heart events as already completed, and positions
        /// the spouse.
        /// </summary>
        /// <param name="player">The farmer getting married.</param>
        /// <param name="npcName">Internal name of the NPC to marry.</param>
        /// <param name="local">
        /// True if this is the local player initiating (sets friendship data
        /// and dialogue). False for remote multiplayer echoes.
        /// </param>
        internal static void DoMarriage(Farmer player, string npcName, bool local)
        {
            Instance.Monitor.Log(
                $"{player.Name} selected {npcName} (local={local})", LogLevel.Debug);

            // Guard: already taken by another farmer.
            foreach (var farmer in Game1.getAllFarmers())
            {
                if (farmer.spouse == npcName)
                    return;
            }

            // ── Upgrade house to level 1 so the spouse room exists ──
            var home = Utility.getHomeOfFarmer(player);
            if (local)
            {
                home.moveObjectsForHouseUpgrade(1);
                home.setMapForUpgradeLevel(1);
            }
            player.HouseUpgradeLevel = 1;
            home.RefreshFloorObjectNeighbors();

            // ── Set friendship / spouse data ──
            if (local)
            {
                if (!player.friendshipData.TryGetValue(npcName, out Friendship friendship))
                {
                    friendship = new Friendship();
                    player.friendshipData.Add(npcName, friendship);
                }

                // 2550 = just past the 10-heart (2500) wedding threshold.
                // Post-marriage milestones like the spouse Stardrop (12.5 hearts / 3125 pts)
                // will occur naturally through gameplay.
                friendship.Points = 2550;
                friendship.Status = FriendshipStatus.Married;
                friendship.WeddingDate = new WorldDate(Game1.Date);
                player.spouse = npcName;

                // ── Mark courtship mail as already received ──
                MarkCourtshipMailAsReceived(player);

                // ── Mark pre-marriage heart events as seen ──
                MarkHeartEventsAsSeen(player, npcName);
            }

            // ── Position the spouse NPC ──
            NPC spouse = Game1.getCharacterFromName(npcName);
            if (spouse == null)
            {
                Instance.Monitor.Log(
                    $"Could not find NPC '{npcName}' — marriage aborted.",
                    LogLevel.Error);
                return;
            }

            spouse.ClearSchedule();
            spouse.DefaultMap = player.homeLocation.Value;

            var spouseBedSpot = home.getSpouseBedSpot(npcName);
            spouse.DefaultPosition =
                Utility.PointToVector2(spouseBedSpot) * Game1.tileSize;
            spouse.DefaultFacingDirection = 2;

            spouse.ClearSchedule();
            spouse.ignoreScheduleToday = true;
            spouse.shouldPlaySpousePatioAnimation.Value = false;
            spouse.controller = null;
            spouse.temporaryController = null;

            if (local)
                spouse.Dialogue.Clear();
            spouse.currentMarriageDialogue.Clear();

            Game1.warpCharacter(
                spouse, "Farm",
                Utility.getHomeOfFarmer(player).getPorchStandingSpot());
            spouse.faceDirection(2);

            if (local)
            {
                // Try NPC-specific wedding dialogue, fall back to generic.
                string dialogueKey = $"Strings\\StringsFromCSFiles:{npcName}_AfterWedding";
                if (Game1.content.LoadStringReturnNullIfNotFound(dialogueKey) != null)
                {
                    spouse.addMarriageDialogue(
                        "Strings\\StringsFromCSFiles",
                        $"{npcName}_AfterWedding");
                }
                else
                {
                    spouse.addMarriageDialogue(
                        "Strings\\StringsFromCSFiles",
                        "Game1.cs.2782");
                }

                Game1.addHUDMessage(new HUDMessage(I18n.Get("married")));
            }
        }

             /*──────────────────────────────────────────────────────────────
         *  Courtship mail suppression
         *──────────────────────────────────────────────────────────────*/

        /// <summary>
        /// Mark all courtship-related tutorial and hint mail as already
        /// received so the player doesn't get bouquet/pendant letters
        /// after they're already married.
        /// </summary>
        private static void MarkCourtshipMailAsReceived(Farmer player)
        {
            // Vanilla mail keys for dating/marriage progression:
            //   "Bouquet"                — Pierre's letter about buying a bouquet
            //   "SeaAmulet"              — Lewis's letter about the Mermaid's Pendant
            //   Remaining entries        — NPC-specific heart event invitation mail
            //                              for all vanilla candidates that use mail
            string[] courtshipMailFlags = new[]
            {
                "Bouquet",
                "SeaAmulet",
                "abbySpiritBoard",
                "pennySpa",
                "samMessage",
                "joshMessage",
                "elliottBoat",
                "harveyBalloon",
                "EmilyClothingTherapy",
                "EmilyCamping",
                "haleyGarden"
            };

            foreach (string flag in courtshipMailFlags)
            {
                if (!player.mailReceived.Contains(flag))
                {
                    player.mailReceived.Add(flag);
                    Instance.Monitor.Log($"  Marked mail '{flag}' as received.", LogLevel.Trace);
                }
            }

            // Also remove any of these from the pending mailbox / tomorrow queue
            // in case they were already queued before the marriage ran.
            foreach (string flag in courtshipMailFlags)
            {
                player.mailbox.Remove(flag);
                player.mailForTomorrow.Remove(flag);
            }
        }
        /*──────────────────────────────────────────────────────────────
         *  Heart event suppression
         *──────────────────────────────────────────────────────────────*/

        /// <summary>
        /// Scan all Data/Events locations for events with heart-level
        /// or dating preconditions for the chosen spouse, and mark them
        /// as seen so they don't trigger after the player is already married.
        /// </summary>
        /// <summary>
        /// Scan all Data/Events locations for events related to the chosen
        /// spouse and mark them as seen. Does multiple passes to catch
        /// chain-triggered follow-up events (e.g. "sorry for acting weird"
        /// events that fire after a heart event is marked as seen).
        /// </summary>
        private static void MarkHeartEventsAsSeen(Farmer player, string npcName)
        {
            int totalEventsMarked = 0;

            // Collect all event data across all locations once.
            var allEvents = new List<(string locationName, string eventKey)>();
            foreach (string locationName in GetEventLocationNames())
            {
                Dictionary<string, string> eventData;
                try
                {
                    eventData = Game1.content.Load<Dictionary<string, string>>(
                        $"Data\\Events\\{locationName}");
                }
                catch
                {
                    continue;
                }

                foreach (var kvp in eventData)
                    allEvents.Add((locationName, kvp.Key));
            }

            // Multi-pass: keep scanning until no new events are marked.
            bool foundNew = true;
            while (foundNew)
            {
                foundNew = false;

                foreach (var (locationName, eventKey) in allEvents)
                {
                    string[] parts = eventKey.Split('/');
                    if (parts.Length < 2)
                        continue;

                    string eventId = parts[0];

                    // Skip if already marked.
                    if (player.eventsSeen.Contains(eventId))
                        continue;

                    bool isSpouseEvent = false;
                    for (int i = 1; i < parts.Length; i++)
                    {
                        string precondition = parts[i].Trim();

                        // Friendship check: "f {npcName} {hearts}"
                        if (precondition.StartsWith($"f {npcName} ",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            isSpouseEvent = true;
                            break;
                        }

                        // Dating check: "D {npcName}"
                        if (precondition.Equals($"D {npcName}",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            isSpouseEvent = true;
                            break;
                        }
                    }

                    // Second-pass logic: check for chain events.
                    // These have "e {seenEventId}" AND involve the spouse
                    // via "v {npcName}" (visible) or the event script
                    // mentioning the NPC in the actor setup.
                    if (!isSpouseEvent)
                    {
                        bool hasSeenPrecondition = false;
                        bool involvesSpouse = false;

                        for (int i = 1; i < parts.Length; i++)
                        {
                            string precondition = parts[i].Trim();

                            // "e {eventId}" — requires having seen a specific event
                            if (precondition.StartsWith("e ",
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                string[] eIds = precondition.Substring(2).Split(' ');
                                foreach (string eId in eIds)
                                {
                                    if (player.eventsSeen.Contains(eId.Trim()))
                                    {
                                        hasSeenPrecondition = true;
                                        break;
                                    }
                                }
                            }

                            // "v {npcName}" — NPC is visible/involved
                            if (precondition.Equals($"v {npcName}",
                                    StringComparison.OrdinalIgnoreCase))
                            {
                                involvesSpouse = true;
                            }
                        }

                        if (hasSeenPrecondition && involvesSpouse)
                            isSpouseEvent = true;
                    }

                    if (!isSpouseEvent)
                        continue;

                    if (player.eventsSeen.Add(eventId))
                    {
                        totalEventsMarked++;
                        foundNew = true;
                        Instance.Monitor.Log(
                            $"  Marked event {eventId} ({locationName}) as seen for spouse {npcName}.",
                            LogLevel.Trace);
                    }
                }
            }

            Instance.Monitor.Log(
                $"  Marked {totalEventsMarked} heart event(s) as seen for {npcName}.",
                LogLevel.Debug);
        }

        /// <summary>
        /// Get a list of location names that might have event data files.
        /// Includes all currently loaded game locations plus known vanilla
        /// event locations to ensure coverage on day 1 of a new save.
        /// </summary>
        private static IEnumerable<string> GetEventLocationNames()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // All currently loaded locations and their building interiors.
           
            // All currently loaded locations and their building interiors.
            foreach (var loc in Game1.locations)
            {
                names.Add(loc.Name);

                foreach (var building in loc.buildings)
                {
                    if (building.indoors.Value != null)
                        names.Add(building.indoors.Value.Name);
                }
            }
            // Known vanilla locations that commonly host events.
            // Ensures we catch events even if the location isn't loaded yet
            // on day 1 of a brand new save.
            string[] knownEventLocations = new[]
            {
                "Farm", "FarmHouse", "Town", "Beach", "Mountain",
                "Forest", "BusStop", "Railroad", "Mine", "Desert",
                "Woods", "SeedShop", "AnimalShop", "ScienceHouse",
                "ArchaeologyHouse", "WizardHouse", "AdventureGuild",
                "CommunityCenter", "JojaMart", "Saloon", "Hospital",
                "HaleyHouse", "SamHouse", "ElliottHouse", "Tent",
                "ManorHouse", "Backwoods", "Tunnel", "Trailer",
                "Trailer_Big", "Club", "Sewer", "BathHouse_Entry",
                "BathHouse_Pool", "JoshHouse", "IslandSouth",
                "IslandNorth", "IslandWest", "IslandEast",
                "IslandFarmHouse", "IslandShrine", "VolcanoDungeon0",
                "LeahHouse", "HarveyRoom", "Submarine",
                "MermaidHouse", "Sunroom", "MovieTheater",
                "AbandonedJojaMart", "Summit"
            };

            foreach (string loc in knownEventLocations)
                names.Add(loc);

            return names;
        }
    }
}
