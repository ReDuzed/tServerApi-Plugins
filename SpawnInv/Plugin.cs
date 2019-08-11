using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using TShockAPI;
using TShockAPI.Hooks;
using TerrariaApi.Server;
using RUDD;

namespace inv_start
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        private IList<int> item = new List<int>();
        private IList<InvPlayer> invp = new List<InvPlayer>();
        private bool enabled = true;
        private Command opt;
        private Command set;
        private readonly string config = ".\\tshock\\inv_config.ini";
        public override string Name
        {
            get { return "Respawn Items"; }
        }
        public override string Author
        {
            get { return "Duze"; }
        }
        public override string Description
        {   
            get { return "When a player joins or respawns, their inventory is given a [set of] particular items if missing"; }
        }
        public Plugin(Main game) : base(game)
        {
        }
        public override void Initialize()
        {
            if (!File.Exists(config))
            {
                var str = File.Create(".\\" + config);
                str.Close();
                str.Dispose();
            }
            using (StreamReader sr = new StreamReader(config))
            {
                string[] ids = sr.ReadToEnd().Split(' ');
                int i = 0;
                foreach (string s in ids)
                {
                    if (s.Length > 0)
                    if (int.TryParse(s, out i))
                        item.Add(i);
                }
            }
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerCommand.Register(this, OnCommand);
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        }
        protected override void Dispose(bool disposed)
        {
            if (disposed)
            {
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);  
                ServerApi.Hooks.ServerCommand.Deregister(this, OnCommand);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
            }
            base.Dispose(disposed);
        }
        private void OnCommand(CommandEventArgs e)
        {
            if (!Commands.ChatCommands.Contains(opt))
            Commands.ChatCommands.Add(opt = new Command("invset.admin.opt", InvOpt, "invopt")
            {
                HelpText = $"Changes where or not {Name} is enabled."
            });
            if (!Commands.ChatCommands.Contains(set))
            Commands.ChatCommands.Add(set = new Command("invset.admin.setitems", ItemPool, new string[] { "additem", "removeitem", "listitems", "clearitems"})
            {
                HelpText = $"{Name}: Modifies the item pool based on item IDs."
            });
        }
        private void OnJoin(JoinEventArgs e)
        {
            invp.Add(new InvPlayer()
            {
                who = e.Who,
                death = false,
                justJoined = true
            });
        }
        private void OnLeave(LeaveEventArgs e)
        {
            foreach (var i in invp)
            {
                if (i.who == e.Who)
                {
                    invp.Remove(i);
                    break;
                }
            }
        }
        private void OnGetData(GetDataEventArgs e)
        {
            if (enabled && !e.Handled)
            {
                if (e.MsgID == PacketTypes.PlayerDeathV2)
                {
                    byte who = 0;
                    using (BinaryReader br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                    {
                        who = br.ReadByte();
                        invp[who].death = true;
                    }
                }
            }
        }
        private void OnUpdate(EventArgs e)
        {
            if (!enabled || invp.Count == 0)
                return;
            foreach (var i in invp)
            {
                if (i.death && !Main.player[i.who].dead)
                {
                    InvSet(i.who, item.ToArray());
                    i.death = false;
                }
                int team = TShock.Players[i.who].Team;
                if (i.justJoined && team != 0)
                {
                    if (!HasGem(i.who))
                        InvSet(i.who, new int[] { GemType(team) });   
                    i.justJoined = false;
                }
            }
        }
        private void InvOpt(CommandArgs e)
        {
            enabled = !enabled;
            e.Player.SendSuccessMessage($"Players getting the item pool on joining or respawning has been {(enabled ? "enabled" : "disabled" )}.");
        }
        private void ItemPool(CommandArgs e)
        {
            if (!enabled)
                return;
            string command = e.Message.Contains(" ") ? e.Message.Substring(0, e.Message.IndexOf(' ')) : e.Message;
            int type = 0;
            switch (command)
            {
                case "additem":
                    type = 0;
                    if (int.TryParse(e.Message.Substring(e.Message.IndexOf(" ") + 1), out type))
                    {
                        item.Add(type);
                        e.Player.SendSuccessMessage($"Item number {type} added to the pool.");
                        ConfigWrite(type.ToString());
                    }
                    else e.Player.SendErrorMessage($"Could not add item {type}.");
                    break;
                case "removeitem":
                    type = 0;
                    if (int.TryParse(e.Message.Substring(e.Message.IndexOf(" ") + 1), out type))
                    {
                        if (!item.Contains(type))
                        {
                            e.Player.SendInfoMessage($"Item {type} is already not in the pool.");
                            return;
                        }
                        item.Remove(type);
                        e.Player.SendSuccessMessage($"Item number {type} removed from the pool.");
                        ConfigWrite(type.ToString(), true);
                    }
                    else e.Player.SendErrorMessage($"Could not remove item {type}.");
                    break;
                case "listitems":
                    string text = string.Empty;
                    foreach (int i in item)
                        text += i + " ";
                    e.Player.SendSuccessMessage(text);
                    break;
                case "clearitems":
                    item.Clear();
                    e.Player.SendSuccessMessage("All items have been cleared from the pool.");
                    ConfigWrite(type.ToString(), false, true);
                    break;
            }
        }
        private void ConfigWrite(string text, bool remove = false, bool clear = false, bool append = true)
        {
            if (clear)
            {
                using (StreamWriter sw = new StreamWriter(config))
                    sw.Write("");
                return;
            }
            if (remove)
            {
                string t = string.Empty;
                using (StreamReader sr = new StreamReader(config))
                    t = sr.ReadToEnd(); 
                t = t.Replace(text + " ", "");
                using (StreamWriter sw = new StreamWriter(config))
                    sw.Write(t);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(text))
                using (StreamWriter sw = new StreamWriter(config, append))
                    sw.Write(text + " ");
            }
        }
        private int GemType(int team)
        {
            int type = 0;
            switch (team)
            {
                case (int)TeamID.Red:
                    type = ItemID.LargeRuby;
                    break;
                case (int)TeamID.Green:
                    type = ItemID.LargeEmerald;
                    break;
                case (int)TeamID.Blue:
                    type = ItemID.LargeSapphire;
                    break;
                case (int)TeamID.Yellow:
                    type = ItemID.LargeTopaz;
                    break;
                case (int)TeamID.Purple:
                    type = ItemID.LargeAmethyst;
                    break;
                default:
                    break;
            }
            return type;
        }
        private bool IsGem(int type)
        {
            return type == ItemID.LargeRuby || type == ItemID.LargeEmerald || type == ItemID.LargeSapphire || type == ItemID.LargeTopaz || type == ItemID.LargeAmethyst;
        }
        private bool HasGem(int who)
        {
            int gemType = GemType(TShock.Players[who].Team);
            Player player = Main.player[who];
            for (int i = 0; i < player.inventory.Length; i++)
            {
                if (player.inventory[i].type == gemType)
                {
                    return true;
                }
            }
            return false;
        }
        private void InvSet(int who, int[] types)
        {
            bool haveGem = HasGem(who);
            Player player = Main.player[who];
            int team = TShock.Players[who].Team;
            foreach (int i in types)
            {
                if (i == GemType(team) && !haveGem)
                {
                    TShock.Players[who].GiveItem(i, "", player.width, player.height, 1);
                    haveGem = true;
                }
                if (!IsGem(i))
                    TShock.Players[who].GiveItem(i, "", player.width, player.height, 1);
            }
        }
        internal class InvPlayer : SHPlayer
        {
            public bool death;
            public bool justJoined;
        }
    }
}
