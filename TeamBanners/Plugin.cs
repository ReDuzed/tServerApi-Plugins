using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;
using TerrariaApi.Server;
using RUDD;
using RUDD.Dotnet;

namespace banner
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        private string[] teams = new string[] 
        {
            "None", "Red Team", "Green Team", "Blue Team", "Yellow Team", "Pink Team"
        };
        private DataStore data;
        private bool enabled = false;
        private Command command;
        private float MinMon
        {
            get { return minBanners * npcPerBanner; }
            set { minBanners = (int)value / npcPerBanner; }
        }
        private float MaxBannerValue
        {
            get { return MinMon * TotalNpcs / minBanners; }
        }
        private float minBanners = 1f;
        private float MaxBanners
        {
            get { return (PointGoal * (TotalNpcs / MaxBannerValue)) / npcPerBanner; }
        }
        private float PointGoal
        {
            get { return MinMon * TotalNpcs; }
        }
        private const int npcPerBanner = 50;
        private int TotalNpcs
        {
            get { return Main.npcTexture.Length; }
        }
        private float AverageBanners
        {
            get { return (MaxBanners + minBanners) / 2; }
        }
        private float hoursPerWeek = 192f;
        private float HoursPerBanner
        {
            get { return hoursPerWeek / 600f; }
        }
        private double EstMaxTime
        {
            get { return AverageBanners * HoursPerBanner; }
        }
        private bool Event;
        private string region = "access";
        private bool[] tally = new bool[256];
        private bool autoTurnIn;
        public override string Name
        {
            get { return "Team Banners"; }
        }
        public override Version Version
        {
            get { return new Version(0, 1, 7); }
        }
        public override string Author
        {
            get { return "Duze"; }
        }
        public override string Description
        {
            get { return "Two options are added: 1) a function for monster defeat counts per team, 2) recycling inventory tile items with banners adding weight in surplus"; }
        }
        public Plugin(Main game) : base(game)
        {
        }
        private int PointsPerBanner(int b)
        {
            float n = Item.BannerToNPC(b);
            return (int)((MaxBannerValue - n) * (n / TotalNpcs));
        }
        private Block setting;
        private string winningTeam;
        private bool teamHasWon;
        private bool once;
        private int bannerQuest; 
        private string questName;
        private bool quest = false;
        private void StartData()
        {
            data = new DataStore(string.Concat("config\\", "banner_data_", Main.worldName));
            string[] ids = new string[Main.npcTexture.Length + 1];
            ids[0] = "score";
            for (int i = 0; i < ids.Length - 1; i++)
                ids[i + 1] += i;
            foreach (string t in teams) 
            {
                if (!data.BlockExists(t))
                    data.NewBlock(ids, t);
            }
            if (data.BlockExists("settings"))
            {
                setting = data.GetBlock("settings");
                region = setting.GetValue("region");
                bool.TryParse(setting.GetValue("auto"), out autoTurnIn);
                bool.TryParse(setting.GetValue("event"), out Event);
                float.TryParse(setting.GetValue("minimum"), out minBanners);
                winningTeam = setting.GetValue("winner");
                bool.TryParse(setting.GetValue("teamdrops"), out enabled);
                bool.TryParse(setting.GetValue("quest"), out quest);
            }
            else
            {
                setting = data.NewBlock(new string[] 
                {
                    "region", "auto", "event", "minimum", "winner", "first", "teamdrops", "quest"
                }, "settings");
            }
        }
        public override void Initialize()
        {
            for (int i = 0; i < Main.player.Length; i++)
                Stored.splr[i] = new Stored();
            ServerApi.Hooks.NpcKilled.Register(this, OnNpcKilled);
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            ServerApi.Hooks.ServerCommand.Register(this, CommandPopulate);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                data.WriteToFile();
                ServerApi.Hooks.NpcKilled.Deregister(this, OnNpcKilled);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
                ServerApi.Hooks.ServerCommand.Deregister(this, CommandPopulate);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
            }
            base.Dispose(disposing);
        }
        private void OnGetData(GetDataEventArgs e)
        {
            if (!e.Handled)
            {
                using (BinaryReader br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    if (e.MsgID == PacketTypes.NpcKillCount)
                    {
                        short type = br.ReadInt16();
                        int count = br.ReadInt32(); 
                    }
                    if (e.MsgID == PacketTypes.WorldInfo)
                    {
                        //const byte  day = 1,
                        //            bloodMoon = 2, 
                        //            eclipse = 4;
                        int time = br.ReadInt32();
                        byte dayInfo = br.ReadByte();
                    }
                }
            }
        }
        private void OnNpcKilled(NpcKilledEventArgs e)
        {
            Player closest = Main.player[e.npc.FindClosestPlayer()];
            int banner = 0;
            if (quest && bannerQuest != 0 && bannerQuest == e.npc.type)
            {
                banner = Item.BannerToItem(Item.NPCtoBanner(e.npc.BannerID()));
                if (banner != 1614 && Main.rand.NextDouble() >= 0.90f)
                {
                    TShock.Players[closest.whoAmI].GiveItem(banner, "", closest.width, closest.height, 1);
                    TShock.Players[closest.whoAmI].SendInfoMessage(string.Concat("Extra ", questName, " banner has dropped."));
                }
            }
            if (!enabled)
                return;
            var info = data.GetBlock(teams[closest.team]);
            int val = info.IncreaseValue(e.npc.type.ToString(), 1);
            if (val % 50 == 0 && val != 0)
            {
                banner = Item.BannerToItem(Item.NPCtoBanner(e.npc.BannerID()));
                if (banner != 1614)
                {
                    TShock.Players[closest.whoAmI].GiveItem(banner, "", closest.width, closest.height, 1);
                    MessageAll(string.Concat(teams[closest.team], " has defeated ", val, " ", e.npc.FullName, "!"));
                }
            }
        }
        private void CommandPopulate(EventArgs e)
        {
            if (!Commands.ChatCommands.Contains(command))
            {
                Commands.ChatCommands.Add(new Command("banner.recycle", Recycle, new string[] { "recycle" })
                {
                    HelpText = "Allows players to recycle their inventory's tile items for a new item based on the recycled types"
                });
                Commands.ChatCommands.Add(new Command("banner.admin.opt", TeamDropOpt, new string[] { "banneropt" })
                {
                    HelpText = "Opts to allow whether or not banner drops are per team's monster-defeat count"
                });
                Commands.ChatCommands.Add(new Command("banner.admin.region", BannerRegion, new string[] { "regbanner" })
                {
                    HelpText = "Sets region for turning in banners"
                });
                Commands.ChatCommands.Add(new Command("banner.tally", BannerTally, new string[] { "bannerscore" })
                {
                    HelpText = "Grants permission for players to turn check a team's score"
                });
                Commands.ChatCommands.Add(new Command("banner.tally", BannerGoal, new string[] { "bannergoal" })
                {
                    HelpText = "Grants permission for players to check point goal"
                });
                Commands.ChatCommands.Add(new Command("banner.tally", BannerQuest, new string[] { "bannerquest" })
                {
                    HelpText = "Checks which NPC is part of the time cycle's quest"
                });
                Commands.ChatCommands.Add(new Command("banner.admin.quest", BannerQuestToggle, new string[] { "togglequest" })
                {
                    HelpText = "Toggles quest banners function"
                });
                Commands.ChatCommands.Add(command = new Command("banner.tally", BannerTally, new string[] { "bannergive" })
                {
                    HelpText = "Grants permission for players to turn in banners"
                });  
            }
        }
        private void BannerQuestToggle(CommandArgs e)
        {
            e.Player.SendSuccessMessage(string.Concat("[Quest] ", quest = !quest, " NPC quest banners."));
            setting.WriteValue("quest", quest.ToString());
        }
        private void BannerQuest(CommandArgs e)
        {
            if (quest && questName != null && questName.Length > 2)
            {
                e.Player.SendSuccessMessage(string.Concat("[Quest] ", questName, " NPC has a rare chance to drop its banner upon death."));
            }
            else 
            {
                e.Player.SendSuccessMessage("[Quest] No NPC banner chosen for today.");
            }
        }
        private void BannerGoal(CommandArgs e)
        {
            /*string msg = e.Message.Substring(12);
            if (e.Message.Length <= 10)
            {
                e.Player.SendInfoMessage("Append [goal | team | winning] to this command.");
            }
            else if (msg.Contains("goal"))
            {*/
                e.Player.SendSuccessMessage(string.Concat("Total point goal is: ", PointGoal, "."));
            /*}
            else if (msg.Contains("team"))
            {
                
            }*/
        }
        private void BannerRegion(CommandArgs e)
        {
            region = e.Message.Substring(10).Trim(' ');
            e.Player.SendSuccessMessage("Region for banner turn-in set to " + region + ".");
            setting.WriteValue("region", region);
        }
        private void OnUpdate(EventArgs e)
        {
            if (quest && (int)Main.time % 39600 == 0)
            {
                bannerQuest = Main.rand.Next(1, Main.npcTexture.Length);
                int npc = NPC.NewNPC(0, 0, bannerQuest);
                questName = Main.npc[npc].TypeName;
                bannerQuest = Main.npc[npc].type;
                TSPlayer.All.SendInfoMessage(string.Concat("[Quest] ", questName, " NPC has a rare chance to drop its banner upon death."));
            }
            if (!once)
            {
                StartData();
                once = true;
            }
            if (!Event)
                return;
            int total = 0;
            foreach (TSPlayer tsp in TShock.Players)
            {
                if (tsp == null || !tsp.TPlayer.active || tsp.TPlayer.dead)
                    continue;
                if (tsp.CurrentRegion == null)
                {
                    tally[tsp.Index] = true;
                    continue;
                }
                if (tsp.CurrentRegion.Name == region)
                {
                    if (tally[tsp.Index])
                    {
                        for (int i = 0; i < tsp.TPlayer.inventory.Length; i++)
                        {
                            var item = tsp.TPlayer.inventory[i];
                            if (item.Name.ToLower().Contains("banner"))
                            {
                                total = TeamPoints(tsp.Team, BannerValue(item.type, item.stack));
                                InvData(ref item, tsp.Index, i);
                            }
                            else continue;
                        }
                        if (total > 0)
                        {
                            if (teamHasWon)
                            {
                                string winner;
                                MessageTeam((winner = setting.GetValue("winner")) + " with " + data.GetBlock(winner).GetValue("score") + " points has already achieved victory.", tsp.Team);
                                tally[tsp.Index] = false;
                                return;
                            }
                            MessageTeam("Total banner point value: " + total + "!", tsp.Team);
                            if (total >= PointGoal && setting.GetValue("winner") == "0")
                            {
                                setting.WriteValue("winner", teams[tsp.Team]);
                                setting.WriteValue("first", teams[tsp.Team] + ";" + minBanners);
                                MessageAll(string.Concat(teams[tsp.Team], " has achieved victory!"));
                                teamHasWon = true;
                            }
                        }
                        tally[tsp.Index] = false;
                    }
                }
                else tally[tsp.Index] = true;
            }
        }
        private void Recycle(CommandArgs e)
        {
            if (e.Message.Length > 8)
            {
                string cmd = e.Message.Substring(8).ToLower();
                if (cmd.Contains("type"))
                {
                    Stored.splr[e.Player.Index].flag = false;
                    Item item = Stored.splr[e.Player.Index].getItem = Main.item[Item.NewItem(0, 0, 1, 1, TypeValue(e.Player.Index))];
                    e.Player.SendInfoMessage(string.Concat("Result: ", item.Name, " (use '/recycle accept' to complete action)."));
                }
                else if (cmd.Contains("value"))
                {
                    Stored.splr[e.Player.Index].flag = true;
                    Item item = Stored.splr[e.Player.Index].getItem = Main.item[Item.NewItem(0, 0, 1, 1, CopperValue(e.Player.Index))];
                    e.Player.SendInfoMessage(string.Concat("Result: ", item.Name, " (use '/recycle accept' to complete action)."));
                }
                else if (cmd.Contains("accept") && Stored.splr[e.Player.Index].getItem != null)
                {
                    bool flag = Stored.splr[e.Player.Index].flag;
                    int min = 10,
                        max = 40;
                    for (int i = min; i < max; i++)
                    {
                        var item = Main.player[e.Player.Index].inventory[i];
                        if (item.type >= 0 && (item.createTile > 0 && !flag || item.createTile > 0 && item.value > 0 && flag))
                        {
                            if (Stored.splr[e.Player.Index].getItem != null)
                            {
                                Item stored = Stored.splr[e.Player.Index].getItem;
                                e.Player.GiveItem(stored.type, stored.Name, 32, 48, Math.Max(stored.maxStack / 4, 1), stored.prefix); 
                                Stored.splr[e.Player.Index].getItem = null;
                                InvData(ref item, e.Player.Index, i);
                            }
                        }
                    }
                }
            }
            else
            {
                e.Player.SendInfoMessage("The recycle sub commands are 'type', 'value', and 'accept'.");
            }
        }
        private void TeamDropOpt(CommandArgs e)
        {
            string cmd = e.Message.Substring(9);
            if (cmd.Contains("event"))
            {
                Event = !Event;
                e.Player.SendSuccessMessage("The banner mode for events and point tallying has been [" + (Event ? "enabled" : "disabled") + "].");
                setting.WriteValue("event", Event.ToString());
                return;
            }
            else if (cmd.Contains("minbanner") && cmd.Length > 10)
            {
                float n = 0;
                float.TryParse(cmd.Substring(cmd.LastIndexOf(' ') + 1), out n);
                minBanners = n;
                e.Player.SendSuccessMessage(string.Concat("Minimum banners set to [", minBanners, "], meaning goal is [", PointGoal, "] points, and esimated finish time is [", Math.Round(EstMaxTime / 24f, 2) * 4f, " days]."));
                setting.WriteValue("minimum", minBanners.ToString());
                string winner;
                if ((winner = setting.GetValue("winner")) != "0")
                {
                    teamHasWon = false;
                    setting.WriteValue("winner", "0");
                    e.Player.SendSuccessMessage(string.Concat(winner, " victory has been remoked until next banner turn in."));
                }
                return;
            }
            else if (cmd.Contains("toggle"))
            {
                enabled = !enabled;
                e.Player.SendSuccessMessage("Banners dropping for team's monster counts is [" + (enabled ? "enabled" : "disabled") + "].");
                setting.WriteValue("teamdrops", enabled.ToString());
                return;
            }
            e.Player.SendInfoMessage("Commands are: [event | minbanner | toggle].");
        }
        private void BannerTally(CommandArgs e)
        {
            if (e.Message.StartsWith("bannerscore"))
            {
                if (e.Message.Length <= 12)
                {
                     e.Player.SendInfoMessage("/bannerscore [Red | Green | Blue | Yellow | Pink] Team");
                     return;
                }
                string team = e.Message.Substring(12);
                Block block;
                if (data.BlockExists(team))
                {
                    block = data.GetBlock(team);
                    e.Player.SendInfoMessage(string.Concat(team, " points total is ", block.GetValue("score"), "."));
                }
                else
                {
                    e.Player.SendErrorMessage("Try again with the color of the team.");
                }
                return;
            }
            if (!tally[e.Player.Index])
            {
                if (autoTurnIn)
                {
                    e.Player.SendErrorMessage("The process is automatic meaning using this command is unnecessary.");
                    return;
                }
                tally[e.Player.Index] = true;
                e.Player.SendSuccessMessage("Enabled. Find your way to the banner location to tally points.");
                return;
            }
            e.Player.SendInfoMessage("Commands: /bannerscore [Red | Green | Blue | Yellow | Pink] Team" + (autoTurnIn ? "\n/bannergive" : "."));
        }

        private void InvData(ref Item item, int who, int slot)
        {
            item.type = 0;
            item.SetDefaults();
            NetMessage.SendData((int)PacketTypes.PlayerSlot, -1, -1, NetworkText.FromLiteral(item.Name), who, slot, item.prefix);
            NetMessage.SendData((int)PacketTypes.PlayerSlot, who, -1, NetworkText.FromLiteral(item.Name), who, slot, item.prefix);
            TShock.Players[who].SendData(PacketTypes.PlayerSlot, item.Name, who, slot, item.prefix);
        }
        private int TypeValue(int who)
        {
            int min = 10,
                max = 40,
                items = 0,
                total = 0;
            float weight = 1f;
            for (int i = min; i < max; i++)
            {
                var item = Main.player[who].inventory[i];
                if (item.type != 0 && item.createTile > 0)
                {
                    if (item.Name.ToLower().Contains("banner"))
                        weight *= ItemWeight(item.type) + 1;
                    else
                    {
                        total += item.type;
                        items++;
                    }
                }
            }
            TShock.Players[who].SendInfoMessage("type: " + (int)Math.Min((total / Math.Max(1, items)) * weight, ItemID.MusicBoxSandstorm));
            return (int)Math.Min((total / Math.Max(1, items)) * weight, ItemID.MusicBoxSandstorm);
        }
        private int CopperValue(int who, int ceiling = ItemID.MusicBoxSandstorm)
        {
            int min = 10,
                max = 40,
                items = 0,
                total = 0;
            float weight = 1f;
            for (int i = min; i < max; i++)
            {
                var item = Main.player[who].inventory[i];
                if (item.type != 0 && item.GetStoreValue() > 0 && item.createTile > 0)
                {
                    if (item.Name.ToLower().Contains("banner"))
                        weight *= ItemWeight(item.type) + 1;
                    else
                    {
                        total += item.GetStoreValue();
                        items++;
                    }
                }
            }
            return (int)Math.Min((total / Math.Max(1, items)) * weight / Math.Max(1, (Item.gold / 5)), ceiling);
        }
        private float ItemWeight(int type, int itemFloor = ItemID.AnkhBanner, int ceiling = ItemID.MusicBoxSandstorm)
        {
            return (type - itemFloor) / (ceiling - itemFloor);
        }
        private int BannerValue(int type, int stack = 1, float floor = ItemID.AnglerFishBanner, float ceiling = ItemID.TumbleweedBanner)
        {
            return (int)(Math.Max((type + 1f - floor) / (ceiling - floor), 0.01f) * 100f / 0.90f) * stack;
        }
        private void MessageAll(string message)
        {
            foreach (var tsp in TShock.Players) 
            {
                if (tsp != null && tsp.TPlayer.active)
                    tsp.SendMessage(message, 100, 255, 100);
            }
        }
        private void MessageTeam(string message, int team)
        {
            foreach (var tsp in TShock.Players) 
            {
                if (tsp != null && tsp.TPlayer.active && tsp.Team == team)
                    tsp.SendMessage(message, 100, 255, 100);
            }
        }

        private class Stored : SHPlayer
        {
            public bool flag = false;
            public Item getItem;
            public static Stored[] splr = new Stored[256];
        }
        private int TeamPoints(int team, int value = 1)
        {
            var block = data.GetBlock(teams[team]);
            foreach (string k in block.Keys())
            {
                if (k == "score")
                {
                    return block.IncreaseValue(k, value) - value;
                }
            }
            return value;
        }
    }
}
