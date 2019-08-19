using System;
using System.IO;
using Terraria;
using Terraria.ID;
using TShockAPI;
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
            "None", "RedTeam", "GreenTeam", "BlueTeam", "YellowTeam", " PurpleTeam"
        };
        private DataStore data;
        private bool enabled = true;
        private Command command;
        public override string Name
        {
            get { return "Team Banners"; }
        }
        public override Version Version
        {
            get { return new Version(0, 1, 5); }
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
        private void StartData()
        {
            data = new DataStore("config\\banner_data");
            string[] ids = new string[Main.npcTexture.Length];
            for (int i = 0; i < ids.Length; i++)
                ids[i] += i;
            foreach (string t in teams) 
            {
                if (!data.BlockExists(t))
                    data.AddBlock(ids, t);
            }
        }
        public override void Initialize()
        {
            StartData();
            for (int i = 0; i < Main.player.Length; i++)
                Stored.splr[i] = new Stored();
            ServerApi.Hooks.NpcKilled.Register(this, OnNpcKilled);
            ServerApi.Hooks.ServerCommand.Register(this, OnCommand);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                data.WriteToFile();
                ServerApi.Hooks.NpcKilled.Deregister(this, OnNpcKilled);
                ServerApi.Hooks.ServerCommand.Deregister(this, OnCommand);
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
                MessageAll(string.Concat(TeamName(closest.team), " has defeated ", val, " ", e.npc.FullName, "!"));
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
                Commands.ChatCommands.Add(command = new Command("banner.admin.opt", TeamDropOpt, new string[] { "teamdrop" })
                {
                    HelpText = "Opts to allow whether or not banner drops are per team's monster-defeat count"
                });
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
                                item.type = 0;
                                item.SetDefaults();
                                e.Player.SendData(PacketTypes.PlayerSlot, item.Name, e.Player.Index, i, item.prefix);
                                Stored.splr[e.Player.Index].getItem = null;
                            }
                            else
                            {
                                item.type = 0;
                                item.SetDefaults();
                                e.Player.SendData(PacketTypes.PlayerSlot, item.Name, e.Player.Index, i, item.prefix);
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
            enabled = !enabled;
            e.Player.SendSuccessMessage("Banners dropping for team's monster counts is [" + (enabled ? "enabled" : "disabled") + "]");
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
        private void MessageAll(string message)
        {
            foreach (var tsp in TShock.Players) 
            {
                if (tsp != null && tsp.TPlayer.active)
                    tsp.SendInfoMessage(message, 100, 255, 100);
            }
        }

        private class Stored : SHPlayer
        {
            public bool flag = false; 
            public Item getItem;
            public static Stored[] splr = new Stored[256];
            public Stored()
            {
            }
        }
        private string TeamName(int team)
        {
            if (team == 0)
                return "None";
            if (team == 1)
                return "Red Team";
            if (team == 2)
                return "Green Team";
            if (team == 3)
                return "Blue Team";
            if (team == 3)
                return "Yellow Team";
            if (team == 4)
                return "Purple Team";
            return string.Empty;
        }
    }
}
