using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using TShockAPI;
using TShockAPI.DB;
using TerrariaApi.Server;
using RUDD.Dotnet;

namespace SafeRegions_TSAPI
{
    [ApiVersion(2,1)]
    public class Plugin : TerrariaPlugin
    {
        public override string Name => "Safe Regions TAPI";
        public override string Author => "Duze";
        public override string Description => "";
        public override Version Version => new Version(2, 0);
        public bool SetPoint
        {
            get;
            internal set;
        }
        public DataStore regionData;
        private const string Heading = "Regions";
        private const int Max = 256;
        private Dictionary<string,bool> pvpRules = new Dictionary<string, bool>(Max);
        public Plugin(Main game) : base(game)
        {
            regionData = new DataStore("config\\region_data");
            if (!regionData.BlockExists(Heading))
            {
                string[] array = new string[Max];
                for (int i = 0; i < array.Length; i++)
                    array[i] += (i + 1);
                regionData.NewBlock(array, Heading);
            }
        }
        public override void Initialize()
        {
            Action<Command> add = delegate(Command cmd) {
                Commands.ChatCommands.Add(cmd);
            };
            add(new Command("saferegion.admin.setup", Setup, new string[] { "region", "sr", "load" }));
            ServerApi.Hooks.GameUpdate.Register(this, OnGameUpdate);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                regionData.WriteToFile();
                ServerApi.Hooks.GameUpdate.Deregister(this, OnGameUpdate);
            }
        }
        public void OnGameUpdate(EventArgs e)
        {
            if ((int)Main.time % 20 != 0)
                return;
            foreach (TSPlayer player in TShock.Players)
            {
                if (player != null && player.Active && !player.Dead)
                {
                    Region region = HighestAxis(player.TPlayer, out int z);
                    if (region == null)
                        continue;
                    Console.WriteLine("In a region! " + z);
                    TogglePvp(player.Index, pvpRules[region.Name.ToLower()]);
                }
            }
        }
        public void Setup(CommandArgs e)
        {
            if (e.Message.StartsWith("load"))
            {
                int count = 0;
                var regions = TShock.Regions.Regions;
                for (int i = 0; i < Max; i++)
                {
                    if (i >= regions.Count)
                        break;
                    if (!pvpRules.Keys.Contains(regions[i].Name.ToLower()))
                    {
                        count++;
                        pvpRules.Add(regions[i].Name.ToLower(), false);
                    }
                }
                e.Player.SendSuccessMessage(count + " regions loaded into the database.");
            }
            if (e.Message.StartsWith("region") && e.Message.Contains("define"))
            {
                pvpRules.Add(e.Message.Substring(14).ToLower(), false);
                e.Player.SendSuccessMessage("Region " + e.Message.Substring(14) + " has been logged into the database.");
                return;
            }
            if (e.Message.StartsWith("sr") && e.Message.Contains(" "))
            {
                Block block = regionData.GetBlock(Heading);
                string sub = e.Message.Substring(e.Message.IndexOf(" ") + 1).ToLower();
                if (sub.Contains(" "))
                {
                    string arg = sub.Substring(sub.LastIndexOf(" ") + 1);
                    sub = sub.Substring(0, sub.IndexOf(" "));
                    var region = TShock.Regions.Regions.Where(t => t.Name.ToLower() == arg.ToLower()).ToArray();
                    if (region.Length > 0)
                    {
                        if (sub == "pvp")
                        {
                            pvpRules[arg] = !pvpRules[arg];
                            block.WriteValue(GetKey(block, arg), region[0].Name.ToLower(), pvpRules[arg]);
                            e.Player.SendSuccessMessage(pvpRules[arg] ? arg + " region is now PvP enabled." : arg + " region has PvP turned off.");
                            return;
                        }
                    }
                    e.Player.SendErrorMessage("There was an error in region name input.");
                    return;
                }
                if (sub.ToLower() == "help")
                {
                    e.Player.SendErrorMessage("Flips the PvP status of the region: [c/FFFF00:/sr pvp <region name>]");
                    return;
                }
                e.Player.SendErrorMessage("Flips the PvP status of the region: [c/FFFF00:/sr pvp <region name>]");
            }
            else
            {
                e.Player.SendErrorMessage("Flips the PvP status of the region: [c/FFFF00:/sr pvp <region name>]");
            }
        }
        public Region HighestAxis(Player player, out int z)
        {
            int index = -1;
            z = 0;
            var all = TShock.Regions.Regions;
            if (all.Count == 0)
                return null;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].InArea((int)player.Center.X / 16, (int)player.Center.Y / 16))
                {
                    if (z < all[i].Z)
                    {
                        z = all[i].Z;
                        index = i;
                        continue;
                    }
                }
            }
            return index == -1 ? null : all[index];
        }
        public string GetKey(Block block, string value)
        {
            for (int i = 0; i < block.RawData.Length; i++)
            {
                var c = block.RawData[i];
                if (c != null && !string.IsNullOrWhiteSpace(c) && !string.IsNullOrEmpty(c) && c.Contains(value))
                    return c.Substring(c.IndexOf(":"));
            }
            return string.Empty;
        }
        public string[] GetRegionNames(Block block)
        {
            string line = "";
            for (int i = 0; i < block.Contents.Length; i++)
            {
                string name;
                string value = (i + 1).ToString();
                if ((name = block.GetValue(value)) != "0")
                {
                    line += name + ";";
                }
            }
            return line.Split(';');
        }

        private void TogglePvp(int who, bool enabled)
        {
            Main.player[who].hostile = enabled;
            foreach (TSPlayer tsp in TShock.Players)
            {
                if (tsp != null) 
                if (tsp.TPlayer.active)
                    tsp.SendData(PacketTypes.TogglePvp, "", who);
            }
        }
    }
}
