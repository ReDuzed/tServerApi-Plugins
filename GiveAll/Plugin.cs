using System;
using System.IO;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using System.Net;
using System.Net.Sockets;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;
using TerrariaApi.Server;
using RUDD;
using RUDD.Dotnet;
using RUDD.Terraria;

namespace GiveItems
{
    [ApiVersion(2,1)]
    public class Plugin : TerrariaPlugin
    {
        public override string Name
        {
            get { return "GiveAll"; }
        }
        public override string Author
        {
            get { return "Duze"; }
        }
        public override Version Version
        {
            get { return new Version(0, 1, 0, 0); }
        }
        public override string Description
        {
            get { return "Gives players all of a set of predefined items"; }
        }
        public Plugin(Main game) : base(game)
        {
        }
        private DataStore data;
        public override void Initialize()
        {
            data = new DataStore("giver_data");
            Commands.ChatCommands.Add(new Command("giver.mod.give", GiveAll, "giveall")
            {
                HelpText = "/giveall [item id] [item2 id] [item3 id] [etc.]"
            });
            Commands.ChatCommands.Add(new Command("giver.mod.give", GiveAllStack, "giveallstack")
            {
                HelpText = "/giveallstack [item id] [stack]"
            });
            Commands.ChatCommands.Add(new Command("giver.mod.give", GiveAllRecord, "giveallrecord")
            {
                HelpText = "/giveallrecord"
            });
            Commands.ChatCommands.Add(new Command("giver.admin.record", SaveRecorded, "saverecord")
            {
                HelpText = "/saverecord [item id] [item2 id] [item3 id] [etc.]"
            });
            Commands.ChatCommands.Add(new Command("giver.admin.record", ClearRecorded, "clearrecord")
            {
                HelpText = "/clearrecord (clears all recorded)"
            });
        }
        protected override void Dispose(bool disposing)
        {
            data.WriteToFile();
            base.Dispose(disposing);
        }
        private void GiveAllRecord(CommandArgs e)
        {
            Block block = new Block();
            if (!data.BlockExists("items"))
            {
                return;
            }
            else block = data.GetBlock("items");
            foreach (TSPlayer plr in TShock.Players)
            {
                for (int i = 0 ; i < 40; i++)
                {
                    int n;
                    if (int.TryParse(block.GetValue(i.ToString()), out n))
                    {
                        if (n > 0)
                        {                        
                            plr.GiveItem(n, "", plr.TPlayer.width, plr.TPlayer.height, 1, 0);
                        }
                    }
                }
            }
        }
        private void SaveRecorded(CommandArgs e)
        {
            Block block = new Block();
            if (!data.BlockExists("items"))
            {
                var list = new string[40];
                for (int i = 0; i < list.Length; i++)
                    list[i] = i.ToString();
                block = data.NewBlock(list, "items");
            }
            else block = data.GetBlock("items");
            string[] item = e.Message.Substring(e.Message.IndexOf(' ') + 1).Split(' ');
            for (int i = 0; i < item.Length; i++)
            {
                block.WriteValue(i.ToString(), item[i]);
            }
            data.WriteToFile();
        }
        private void ClearRecorded(CommandArgs e)
        {
            Block block = new Block();
            if (!data.BlockExists("items"))
            {
                var list = new string[40];
                for (int i = 0; i < list.Length; i++)
                    list[i] = i.ToString();
                block = data.NewBlock(list, "items");
                return;
            }
            else block = data.GetBlock("items");
            for (int i = 0; i < 40; i++)
            {
                block.WriteValue(i.ToString(), "0");
            }
        }
        private void GiveAll(CommandArgs e)
        {
            string msg = e.Message.Substring(e.Message.IndexOf(" ") + 1);
            string[] item = msg.Split(' ');
            foreach (var plr in TShock.Players)
            {
                for (int i = 0; i < item.Length; i++)
                {
                    plr.GiveItem(int.Parse(item[i]), "", plr.TPlayer.width, plr.TPlayer.height, 1, 0);
                }
            }
        }
        private void GiveAllStack(CommandArgs e)
        {
            string item = e.Message.Substring(e.Message.IndexOf(" ") + 1);
            string stack = item.Substring(item.IndexOf(' ') + 1);
            int i, s;
            int.TryParse(item.Substring(0, stack.Length), out i);
            int.TryParse(stack, out s);
            foreach (var plr in TShock.Players)
            {
                plr.GiveItem(i, "", plr.TPlayer.width, plr.TPlayer.height, s, 0);
            }
        }
    }
}
