﻿using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Localization;
using TShockAPI;
using TShockAPI.Hooks;
using TerrariaApi.Server;
using RUDD;

namespace safehouse
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
        private int limit;
        private Command remove = null;
        private Command regions = null;
        public override string Name
        {
            get { return "Safe Regions"; }
        }
        public override Version Version
        {
            get { return new Version(0, 1); }
        }
        public override string Author 
        {
            get { return "Duze"; }
        }
        public override string Description
        {
            get { return "This plugin features setting regions where player damage is completely nullified."; }
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
            foreach (Region r in region)
            {
                if (r.Equals(null))
                    continue;
                foreach (var p in shp)
                {
                    if (p == null)
                        continue;
                    Player player = Main.player[p.who];
                    if (player.statLife != p.oldLife)
                    {
                        if (r.Contains(player.position.X, player.position.Y))
                        {
                            if (!p.healed)
                            {
                                var tsp = TShock.Players[p.who];
                                tsp.Heal();
                                p.healed = true;
                                tsp.SendData(PacketTypes.PlayerHp, "", p.who);
                                continue;
                            }
                            p.oldLife = player.statLife;
                        }
                    }
                    else p.healed = false;
                }
            }
            oldCount = shp.Count;
        }
        private void OnCommand(CommandEventArgs e)
        {
            if (!Commands.ChatCommands.Contains(remove))
            Commands.ChatCommands.Add(remove = new Command("safehouse.admin.remove", ShRemove, "shremove")
            {
                HelpText = "Causes removal of safe region by name or index"
            });
            if (!Commands.ChatCommands.Contains(regions))
            Commands.ChatCommands.Add(regions = new Command("safehouse.admin.regions", SafeRegion, new string[] { "shset1", "shset2", "shmake", "shreset", "shlist" }) 
            {
                HelpText = Name + ": Modifies the safe region parameters"
            });
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
                        region.Remove(region[index]);
                }
            }
            else
            {
                foreach (Region r in region)
                {
                    if (input == r.name)
                    {
                        region.Remove(r);
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
                        e.Player.SendErrorMessage("Please provide a name for the safe house.");
                        return;
                    }
                    if (leftPoint.Equals(Vector2.Zero) || rightPoint.Equals(Vector2.Zero))
                        return;
                    string name = e.Message.Substring(7);
                    region.Add(new Region(name, leftPoint, rightPoint));
                    e.Player.SendSuccessMessage("Safe house " + name + " has been made.");
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
                default:
                    break;                    
            }
        }
    }
}