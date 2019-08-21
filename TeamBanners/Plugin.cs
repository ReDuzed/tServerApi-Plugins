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
            "None", "Red Team", "Green Team", "Blue Team", "Yellow Team", "Purple Team"
        };
        private DataStore data;
        private bool enabled = true;
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
        private int minBanners = 1;
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
        private bool Event
        {
            get;
            set;
        }
        private int count;
        private const string region = "access";
        private bool[] tally = new bool[256];
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
        private void StartData()
        {
            data = new DataStore("config\\banner_data");
            string[] ids = new string[Main.npcTexture.Length + 1];
            ids[0] = "score";
            for (int i = 0; i < ids.Length - 1; i++)
                ids[i + 1] += i;
            foreach (string t in teams) 
            {
                if (!data.BlockExists(t))
                    data.NewBlock(ids, t);
            }
        }
        public override void Initialize()
        {
            StartData();
            for (int i = 0; i < Main.player.Length; i++)
                Stored.splr[i] = new Stored();
            ServerApi.Hooks.NpcKilled.Register(this, OnNpcKilled);
            ServerApi.Hooks.ServerCommand.Register(this, OnCommand);
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                data.WriteToFile();
                ServerApi.Hooks.NpcKilled.Deregister(this, OnNpcKilled);
                ServerApi.Hooks.ServerCommand.Deregister(this, OnCommand);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
            }
            base.Dispose(disposing);
        }
        private void OnNpcKilled(NpcKilledEventArgs e)
        {
            if (!enabled)
                return;
            Player closest = Main.player[e.npc.FindClosestPlayer()];
            var info = data.GetBlock(teams[closest.team]);
            int val = info.IncreaseValue(e.npc.type.ToString(), 1);
            if (val % 50 == 0 && val != 0)
            {
                TShock.Players[closest.whoAmI].GiveItem(e.npc.BannerID(), "", closest.width, closest.height, 1);
                MessageAll(string.Concat(teams[closest.team], " has defeated ", val, " ", e.npc.FullName, "!"));
            }
        }
        private void OnCommand(CommandEventArgs e)
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
                Commands.ChatCommands.Add(command = new Command("banner.tally", BannerTally, new string[] { "bannergive" })
                {
                    HelpText = "Grants permission for players to turn in banners"
                });
            }
        }
        private void OnUpdate(EventArgs e)
        {
            if (!Event)
                return;
            int total = 0;
            foreach (TSPlayer tsp in TShock.Players)
            {
                if (tsp == null || !tsp.TPlayer.active)
                    continue;
                if (tally[tsp.Index] && !tsp.TPlayer.dead)
                {
                    if (tsp.CurrentRegion == null)
                        continue;
                    if (tsp.CurrentRegion.Name == region)
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
                        MessageTeam("Total banner point value: " + total + "!", tsp.Team);
                        tally[tsp.Index] = false;
                    }
                }
            }
        }
        private void Recycle(CommandArgs e)
        {
            if (e.Message.Length > 8)
            {
                string cmd = e.Message.Substring(8).ToLower();
                if (cmd == "type")
                {
                    Stored.splr[e.Player.Index].flag = false;
                    Item item = Stored.splr[e.Player.Index].getItem = Main.item[Item.NewItem(0, 0, 1, 1, TypeValue(e.Player.Index))];
                    e.Player.SendInfoMessage(string.Concat("Result: ", item.Name, " (use '/recycle accept' to complete action)."));
                }
                else if (cmd == "value")
                {
                    Stored.splr[e.Player.Index].flag = true;
                    Item item = Stored.splr[e.Player.Index].getItem = Main.item[Item.NewItem(0, 0, 1, 1, CopperValue(e.Player.Index))];
                    e.Player.SendInfoMessage(string.Concat("Result: ", item.Name, " (use '/recycle accept' to complete action)."));
                }
                else if (cmd == "accept" && Stored.splr[e.Player.Index].getItem != null)
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
            }
            else if (cmd.Contains("minbanner") && cmd.Length > 12)
            {
                int n = 0;
                int.TryParse(cmd.Substring(cmd.LastIndexOf(' ') + 1), out n);
                minBanners = n;
                e.Player.SendSuccessMessage(string.Concat("Minimum banners set to [", minBanners, "], meaning goal is [", PointGoal, "] points, and esimated finish time is [", Math.Round(EstMaxTime / 24f, 2) * 4f, " days]."));
            }
            else
            {
                enabled = !enabled;
                e.Player.SendSuccessMessage("Banners dropping for team's monster counts is [" + (enabled ? "enabled" : "disabled") + "].");
            }
        }
        private void BannerTally(CommandArgs e)
        {
            if (!tally[e.Player.Index])
            {
                tally[e.Player.Index] = true;
                e.Player.SendSuccessMessage("Enabled.");
            }
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
            return (int)Math.Min((total / items) * weight, ItemID.MusicBoxSandstorm);
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
            return (int)Math.Min((total / items) * weight / (Item.gold / 5), ceiling);
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
                    return block.IncreaseValue(k, value);
                }
            }
            return value;
        }
    }
}
