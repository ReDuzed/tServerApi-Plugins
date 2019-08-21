using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Localization;
using TShockAPI;
using TShockAPI.Hooks;
using TerrariaApi.Server;
using RUDD;

namespace saferegion
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        private Vector2 
            leftPoint = Vector2.Zero, 
            rightPoint = Vector2.Zero;
        private List<Region> region = new List<Region>();
        private bool await1, await2;
        private int who = 255;
        private List<SHPlayer> shp = new List<SHPlayer>();
        private int oldCount, regionCount;
        private Command remove = null;
        private Command regions = null;
        private const string config = "config\\region_config_";
        private string realConfig = config;
        private bool pvpRegions;
        private const bool checkBoss = true;
        public override string Name
        {
            get { return "Safe Regions"; }
        }
        public override Version Version
        {
            get { return new Version(0, 3); }
        }
        public override string Author 
        {
            get { return "Duze"; }
        }
        public override string Description
        {
            get { return "This plugin features setting regions where player PvP status is disabled, and damage is completely nullified."; }
        }
        
        public Plugin(Main game) : base(game)
        {
        }
        public override void Initialize()
        {
            ServerApi.Hooks.ServerCommand.Register(this, OnCommand);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.GameUpdate.Register(this, SafeRegion);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInit);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerCommand.Deregister(this, OnCommand);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.GameUpdate.Deregister(this, SafeRegion);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInit);
            }
            base.Dispose(disposing);
        }

        private void OnJoin(JoinEventArgs e)
        {
            shp.Add(new SHPlayer()
            {
                who = e.Who
            });
        }
        private void OnLeave(LeaveEventArgs e)
        {
            foreach (var sh in shp)
            {
                if (sh.who == e.Who)
                {
                    shp.Remove(sh);
                    break;
                }
            }
        }
        private void SafeRegion(EventArgs e)
        {
            if (oldCount != shp.Count)
            {
                oldCount = shp.Count;
                return;
            }
            if (regionCount != region.Count)
            {
                regionCount = region.Count;
                return;
            }
            bool bossActive = false;
            NPC boss = null;
            foreach (NPC n in Main.npc)
            {
                if (n.active && n.boss && Main.player[n.FindClosestPlayer()].Distance(n.position) < 100 * 16)
                {
                    boss = n;
                    bossActive = true;
                    break;
                }
            }
            foreach (var p in shp)
            {
                if (p == null)
                    continue;
                foreach (Region r in region)
                {
                    if (r.Equals(null))
                        continue;
                    Player player = Main.player[p.who];
                    bool contains = r.Contains(player.position.X, player.position.Y);
                    if (pvpRegions)
                    {
                        if (contains) 
                        { 
                            if (!p.reserved)
                            {
                                TogglePvp(p.who, false);
                                p.reserved = true;
                            }
                            break;
                        }
                        else
                        {
                            TogglePvp(p.who, true);
                            p.reserved = false;
                        }
                    }
                    if (contains && !bossActive)
                    {
                        if (player.statLife != p.oldLife)
                        {
                            if (!p.healed)
                            {
                                var tsp = TShock.Players[p.who];
                                tsp.Heal(player.statLifeMax - player.statLife);
                                p.healed = true;
                                tsp.SendData(PacketTypes.EffectHeal, "", p.who, player.statLifeMax - player.statLife);
                                continue;
                            }
                            p.oldLife = player.statLife;
                        }
                        else p.healed = false;
                    }
                }
            }
            oldCount = shp.Count;
        }
        private void OnCommand(CommandEventArgs e)
        {
            if (!Commands.ChatCommands.Contains(regions))
            {
                Commands.ChatCommands.Add(remove = new Command("saferegion.admin.remove", ShRemove, "shremove")
                {
                    HelpText = "Causes removal of safe region by name or index"
                });
                Commands.ChatCommands.Add(regions = new Command("saferegion.admin.regions", SafeRegion, new string[] { "shset1", "shset2", "shmake", "shreset", "shlist", "pvpregion" }) 
                {
                    HelpText = Name + ": Modifies the safe region parameters"
                });
            }
        }
        private void OnGetData(GetDataEventArgs e)
        {
            if (!e.Handled)
            {
                if (e.MsgID == PacketTypes.Tile)
                {
                    using (BinaryReader br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                    {
                        byte action = br.ReadByte();
                        short x = br.ReadInt16();
                        short y = br.ReadInt16();
                        if (await1)
                        {
                            leftPoint = new Vector2(x * 16, y * 16);
                            await1 = false;
                            TShock.Players[who].SendInfoMessage("Point 1 set.");
                            who = 0;
                            return;
                        }
                        else if (await2)
                        {
                            rightPoint = new Vector2(x * 16, y * 16);
                            await2 = false;
                            TShock.Players[who].SendInfoMessage("Point 2 set.");
                            who = 0;
                        }
                    }
                }
            }
        }
        private void ShRemove(CommandArgs e)
        {
            int index = 0;
            string input = e.Message.Substring(e.Message.IndexOf(' ') + 1);
            if (int.TryParse(input, out index))
            {
                if (region.Count - 1 >= index)
                {
                    if (!region[index].Equals(null))
                    {
                        region.Remove(region[index]);
                        e.Player.SendSuccessMessage(string.Concat(region[index].name, " region removed."));
                    }
                }
            }
            else
            {
                foreach (Region r in region)
                {
                    if (input == r.name)
                    {
                        region.Remove(r);
                        e.Player.SendSuccessMessage(string.Concat(r.name, " region removed."));
                        break;
                    }
                }
            }
        }
        private void SafeRegion(CommandArgs e)
        {
            string command = e.Message.Contains(" ") ? e.Message.Substring(0, e.Message.IndexOf(" ")) : e.Message;
            switch (command)
            {
                case "shset1":
                    if (e.Message.Contains("set1") && !e.Message.Contains("2"))
                    {
                        e.Player.SendInfoMessage("Mine at a tile to set the first corner" + (await2 ? " (reset set2)" : "."));
                        if (await2)
                            await2 = false;
                        await1 = true;
                        who = e.Player.Index;
                        return;
                    }
                    break;
                case "shset2":
                    if (e.Message.Contains("set2") && !e.Message.Contains("1"))
                    {
                        e.Player.SendInfoMessage("Mine at a tile to set the second corner" + (await1 ? " (reset set1)" : "."));
                        if (await1)
                            await1 = false;
                        await2 = true;
                        who = e.Player.Index;
                    }
                    break;
                case "shmake":
                    if (e.Message.Length <= 7)
                    {
                        e.Player.SendErrorMessage("Please provide a name for the safe region.");
                        return;
                    }
                    if (leftPoint.Equals(Vector2.Zero) || rightPoint.Equals(Vector2.Zero))
                        return;
                    string name = e.Message.Substring(7);
                    foreach (Region r in region)
                    {
                        if (name == r.name)
                        {
                            e.Player.SendErrorMessage("Safe region " + name + " already exists.");
                            return;
                        }
                    }
                    region.Add(new Region(name, leftPoint, rightPoint, true));
                    e.Player.SendSuccessMessage("Safe region " + name + " has been made.");
                    WriteRegion(leftPoint, rightPoint, name);
                    goto case "shreset";
                case "shreset":
                    leftPoint = Vector2.Zero;
                    rightPoint = Vector2.Zero;
                    e.Player.SendInfoMessage("Points 1 and 2 have been unset.");
                    break;
                case "shlist":
                    string list = string.Empty;
                    if (region.Count > 0)
                    {
                        foreach (Region r in region)
                            list += r.name + " ";
                        e.Player.SendSuccessMessage(list);
                    }
                    break;
                case "pvpregion":
                    pvpRegions = !pvpRegions;
                    e.Player.SendSuccessMessage("Regions disabling PvP has been " + (pvpRegions ? "enabled" : "disabled") + ".");
                    break;
                default:
                    break;                    
            }
        }
        private void OnPostInit(EventArgs e)
        {
            realConfig = config;
            realConfig += string.Concat(Main.worldName, ".ini").Replace(' ', '_');
            ReadRegions();
        }
        private void ReadRegions()
        {
            if (!File.Exists(realConfig))
            {
                var file = File.Create(realConfig);
                file.Close();
                file.Dispose();
            }
            string[] lines = null;
            using (StreamReader sr = new StreamReader(realConfig))
                lines = sr.ReadToEnd().Split('\n');
            if (lines.Length > 1)
            {
                float   x1 = 0f,
                        y1 = 0f, 
                        x2 = 0f, 
                        y2 = 0f;
                for (int i = 0; i < lines.Length - 2; i += 3)
                {
                    float.TryParse(lines[i + 1].Substring(0, lines[i + 1].IndexOf(' ')), out x1);
                    float.TryParse(lines[i + 1].Substring(lines[i + 1].IndexOf(' ') + 1), out y1);
                    float.TryParse(lines[i + 2].Substring(0, lines[i + 2].IndexOf(' ')), out x2);
                    float.TryParse(lines[i + 2].Substring(lines[i + 2].IndexOf(' ') + 1), out y2);
                    region.Add(new Region()
                    {
                        name = lines[i],
                        point1 = new Vector2(x1, y1),
                        point2 = new Vector2(x2, y2)
                    });
                }
            }
        }
        private void WriteRegion(Vector2 vec1, Vector2 vec2, string name)
        {
            using (StreamWriter sw = new StreamWriter(realConfig, true))
            {
                sw.NewLine = "\n";
                sw.WriteLine(name);
                sw.WriteLine(vec1.X + " " + vec1.Y);
                sw.WriteLine(vec2.X + " " + vec2.Y);
            }
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