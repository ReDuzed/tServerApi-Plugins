using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.Localization;
using TShockAPI;
using TShockAPI.Hooks;
using TerrariaApi.Server;
using RUDD;
using RUDD.Dotnet;

namespace saferegion
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        private Vector2 
            leftPoint = Vector2.Zero, 
            rightPoint = Vector2.Zero;
        private IList<Region> region = new List<Region>();
        private bool await1, await2;
        private int who = 255;
        private List<SHPlayer> shp = new List<SHPlayer>();
        private int oldCount, regionCount;
        private Command regions = null;
        private const string config = "config\\region_config_";
        private bool pvpRegions = true;
        private const bool checkBoss = true;
        private DataStore data;
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
                data.WriteToFile();
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
                    
                    int z = 0;
                    bool pvp = false;
                    bool heal = false;
                    Block block;
                    if (data.BlockExists(r.name))
                    {
                        block = data.GetBlock(r.name);
                        z = int.Parse(block.GetValue("z"));
                        pvp = bool.Parse(block.GetValue("pvp"));
                        heal = bool.Parse(block.GetValue("heal"));
                    }
                    else 
                    {
                        continue;
                    }
                    Player player = Main.player[p.who];
                    bool contains = r.Contains(player.position.X, player.position.Y);
                    if (contains && !bossActive && heal)
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
                    if (pvpRegions)
                    {
                        if (!contains)
                            continue; 
                        if (z > 0)
                        {
                            TogglePvp(p.who, false);
                            break;
                        }
                        else if (z == 0 && pvp)
                        {
                            TogglePvp(p.who, true);
                            break;
                        }
                    }
                }
            }
            oldCount = shp.Count;
        }
        private void OnCommand(CommandEventArgs e)
        {
            if (!Commands.ChatCommands.Contains(regions))
            {
                Commands.ChatCommands.Add(new Command("saferegion.admin.remove", ShRemove, "shremove")
                {
                    HelpText = "Causes removal of safe region by name or index"
                });
                Commands.ChatCommands.Add(regions = new Command("saferegion.admin.regions", SafeRegion, new string[] { "sr" }) 
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
            if (e.Message.Length < 5)
            {
                e.Player.SendInfoMessage("[SafeRegion] Commands:\n   set1   set2   make <help> | <name> <z>\n   reset   list   pvpopt\n   pvp <name> <true|false>   z <name> <z>");
                return;
            }
            string command = e.Message.Contains(" ") ? e.Message.Substring(3) : e.Message;
            if (command.Contains("set1"))
            {
                if (e.Message.Contains("set1") && !e.Message.Contains("2"))
                {
                    e.Player.SendInfoMessage("Mine at a tile to set the first corner" + (await2 ? " (reset set2)" : "."));
                    if (await2)
                        await2 = false;
                    await1 = true;
                    who = e.Player.Index;
                    return;
                }
            }
            else if (command.Contains("set2"))
            {
                if (e.Message.Contains("set2") && !e.Message.Contains("1"))
                {
                    e.Player.SendInfoMessage("Mine at a tile to set the second corner" + (await1 ? " (reset set1)" : "."));
                    if (await1)
                        await1 = false;
                    await2 = true;
                    who = e.Player.Index;
                }
            }
            else if (command.Contains("make"))
            {
                if (e.Message.Contains("help"))
                {
                    e.Player.SendErrorMessage("Please provide parameters for the safe region:\n    shmake <name> <z>");
                    return;
                }
                if (leftPoint.X == 0 || rightPoint.X == 0)
                {
                    e.Player.SendErrorMessage("Region points not set correctly.");
                    return;
                }
                string name = e.Message.Substring(10);
                if (name.Contains(" "))
                {
                    name = name.Substring(0, name.IndexOf(" "));
                }
                string coord = e.Message.Substring(e.Message.LastIndexOf(" ") + 1);
                int z = 0;
                int.TryParse(coord, out z);
                e.Player.SendSuccessMessage("[SafeRegion] " + name + " has been made at " + z + " z axis.");
                region.Add(new Region(name, leftPoint, rightPoint, false, z));
                WriteRegion(leftPoint, rightPoint, name.ToLower(), z, false, false);
                leftPoint = Vector2.Zero;
                rightPoint = Vector2.Zero;
                e.Player.SendInfoMessage("Points 1 and 2 have been unset.");
            }
            else if (command.Contains("pvp"))
            {
                for (int i = 0; i < region.Count; i++)
                {
                    Region r = region[i];
                    if (command.ToLower().Contains(r.name.ToLower()))
                    {
                        string cmd = command.Substring(command.LastIndexOf(" ") + 1);
                        bool status;
                        bool.TryParse(cmd, out status);
                        r.nonPvP = !status;
                        Block block = data.GetBlock(r.name.ToLower());
                        block.WriteValue("pvp", status.ToString());
                        e.Player.SendSuccessMessage(string.Concat("Region ", r.name, " set to PvP: ", status, "."));
                        return;
                    }
                }
                e.Player.SendErrorMessage("No region exists with this name.");
            }
            else if (command.Contains("heal"))
            {
                for (int i = 0; i < region.Count; i++)
                {
                    Region r = region[i];
                    if (command.ToLower().Contains(r.name.ToLower()))
                    {
                        string cmd = command.Substring(command.LastIndexOf(" ") + 1);
                        bool status;
                        bool.TryParse(cmd, out status);
                        r.reserved = status;
                        Block block = data.GetBlock(r.name.ToLower());
                        block.WriteValue("heal", status.ToString());
                        e.Player.SendSuccessMessage(string.Concat("Region ", r.name, " set to heal: ", status, "."));
                        return;
                    }
                }
                e.Player.SendErrorMessage("No region exists with this name.");
            }
            else if (command.Contains("z"))
            {
                string Z = e.Message.Substring(e.Message.LastIndexOf(" ") + 1);
                int z = 0;
                int.TryParse(Z, out z);
                for (int i = 0; i < region.Count; i++)
                {
                    Region r = region[i];
                    if (command.ToLower().Contains(r.name.ToLower()))
                    {
                        Block block = data.GetBlock(r.name.ToLower());
                        block.WriteValue("z", z.ToString());
                        e.Player.SendSuccessMessage(string.Concat("Region ", r.name, " set to z: ", z, "."));
                        return;
                    }
                }
                e.Player.SendErrorMessage("No region exists with this name.");
            }
            else if (command.Contains("reset"))
            {
                leftPoint = Vector2.Zero;
                rightPoint = Vector2.Zero;
                e.Player.SendInfoMessage("Points 1 and 2 have been unset.");
            }
            else if (command.Contains("list"))
            {
                string list = string.Empty;
                if (region.Count > 0)
                {
                    foreach (Region r in region)
                        list += r.name + " ";
                    e.Player.SendSuccessMessage("[Safe Regions] " + list);
                }
            }
            else if (command.Contains("pvpopt"))
            {
                pvpRegions = !pvpRegions;
                e.Player.SendSuccessMessage("Regions disabling PvP has been " + (pvpRegions ? "enabled" : "disabled") + ".");                    
            }
        }
        private void OnPostInit(EventArgs e)
        {
            data = new DataStore("config\\safe_regions_" + Main.worldName);
            if (!data.BlockExists("Header"))
            {
                var array = new string[64];
                for (int i = 0; i < 64; i++)
                    array[i] = i.ToString();
                data.NewBlock(array, "Header");
            }
            ReadRegions();
        }
        private void ReadRegions()
        {
            if (data.BlockExists("Header"))
            {
                Block block = data.GetBlock("Header");
                for (int i = 0; i < 64; i++)
                {
                    string name = block.GetValue(i.ToString()).ToLower();
                    if (data.BlockExists(name))
                    {
                        Block content = data.GetBlock(name.ToLower());
                        float   x1 = float.Parse(content.GetValue("pos1").Split(';')[0]),
                                y1 = float.Parse(content.GetValue("pos1").Split(';')[1]), 
                                x2 = float.Parse(content.GetValue("pos2").Split(';')[0]), 
                                y2 = float.Parse(content.GetValue("pos2").Split(';')[1]);
                        int     zAxis = int.Parse(content.GetValue("z"));
                        bool    pvp = bool.Parse(content.GetValue("pvp")),
                                heal = bool.Parse(content.GetValue("heal"));
                        region.Add(new Region()
                        {
                            name = name,
                            point1 = new Vector2(x1, y1),
                            point2 = new Vector2(x2, y2),
                            z = zAxis,
                            nonPvP = pvp,
                            reserved = heal
                        });
                    }
                }
            }
        }
        private void WriteRegion(Vector2 vec1, Vector2 vec2, string name, int z, bool pvp, bool heal)
        {
            Block block;
            if (!data.BlockExists(name.ToLower()))
            {
                Block header = data.GetBlock("Header");
                for (int i = 0; i < header.Contents.Length; i++)
                {
                    if (header.GetValue(i.ToString()) == "0")
                    {
                        header.WriteValue(i.ToString(), name.ToLower());
                        break;
                    }
                }
                block = data.NewBlock(new string[] { "pos1", "pos2", "z", "pvp", "heal" }, name.ToLower());
                block.WriteValue("pos1", string.Concat(vec1.X,";",vec1.Y));
                block.WriteValue("pos2", string.Concat(vec2.X,";",vec2.Y));
                block.WriteValue("z", z.ToString());
                block.WriteValue("pvp", pvp.ToString());
                block.WriteValue("heal", heal.ToString());
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