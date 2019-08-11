using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Localization;
using TShockAPI;
using TShockAPI.Hooks;
using TerrariaApi.Server;

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
        public override string Name
        {
            get { return "Safe Houses"; }
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
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerCommand.Deregister(this, OnCommand);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
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
        private void SafeHouse()
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
                    if (player.statLife < player.statLifeMax)
                    {
                        if (r.Contains(player.position.X, player.position.Y))
                        {
                            if (!p.healed)
                            {
                                p.tsp.Heal();
                                p.healed = true;
                                p.tsp.SendData(PacketTypes.PlayerHp, "", p.who);
                                continue;
                            }
                        }
                    }
                    else p.healed = false;
                }
            }
            oldCount = shp.Count;
        }
        private void OnCommand(CommandEventArgs e)
        {
            Commands.ChatCommands.Add(new Command("safehouse.admin.regions", ShRemove, "shremove")
            {
                HelpText = "Causes removal of safe region by name or index"
            });
            Commands.ChatCommands.Add(new Command("safehouse.admin.regions", SetRegion, new string[] { "shset1", "shset2" }) 
            {
                HelpText = "Set1 sets up the upper left point while set2 sets the lower right point"
            });
            Commands.ChatCommands.Add(new Command("safehouse.admin.regions", MakeRegion, "shmake") 
            {
                HelpText = "After region points are set, this makes the zone and removes the temporary points"
            });
            Commands.ChatCommands.Add(new Command("safehouse.admin.regions", Reset, "shreset") 
            {
                HelpText = "Removes the points that are being used for designating a region"
            });
            Commands.ChatCommands.Add(new Command("safehouse.admin.regions", List, "shlist") 
            {
                HelpText = "Lists all the made safe regions"
            });
        }
        private void OnGetData(GetDataEventArgs e)
        {
            if (!e.Handled)
            {
                if (e.MsgID != PacketTypes.Tile && e.MsgID != PacketTypes.PlayerHurtV2)
                    return;
                using (BinaryReader br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                {
                    if (e.MsgID == PacketTypes.Tile)
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
                    if (e.MsgID == PacketTypes.PlayerHurtV2)
                    {  
                        SafeHouse();
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
        private void SetRegion(CommandArgs e)
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
            if (e.Message.Contains("set2") && !e.Message.Contains("1"))
            {
                e.Player.SendInfoMessage("Mine at a tile to set the second corner" + (await1 ? " (reset set1)" : "."));
                if (await1)
                    await1 = false;
                await2 = true;
                who = e.Player.Index;
            }
        }
        private void MakeRegion(CommandArgs e)
        {
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
            Reset(e);
        }
        private void Reset(CommandArgs e)
        {
            leftPoint = Vector2.Zero;
            rightPoint = Vector2.Zero;
            e.Player.SendInfoMessage("Points 1 and 2 have been unset.");
        }
        private void List(CommandArgs e)
        {
            string list = string.Empty;
            if (region.Count > 0)
            {
                foreach (Region r in region)
                    list += r.name + " ";
                e.Player.SendSuccessMessage(list);
            }
        }
    }
    struct Vector2
    {
        public float X;
        public float Y;
        public Vector2(float x, float y)
        {
            this.X = x;
            this.Y = y;
        }
        public static Vector2 Zero
        {
            get { return new Vector2(0f, 0f); }
        }
    }
    struct Region
    {
        public string name;
        public Vector2 point1;
        public Vector2 point2;
        public Region(string name, Vector2 tl, Vector2 br)
        {
            if (br.X < tl.X || tl.Y > br.Y)
            {
                this.point1 = br;
                this.point2 = tl;
            }
            else
            {
                this.point1 = tl;
                this.point2 = br;
            }
            this.name = name;
        }
        public bool Contains(float X, float Y)
        {
            return X >= point1.X && X <= point2.X && Y >= point1.Y && Y <= point2.Y;
        }
    }
    internal class SHPlayer
    {
        public int who;
        public bool healed;
        public TSPlayer tsp
        {
            get { return TShock.Players[who]; }
        }
    }
}
