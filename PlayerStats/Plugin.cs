using System;
using System.IO;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
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

namespace playerstats
{
    [ApiVersion(2,1)]
    public class Plugin : TerrariaPlugin
    {
        public override string Name
        {
            get { return "Player Stats"; }
        }
        public override string Author
        {
            get { return "Duze"; }
        }
        public override Version Version
        {
            get { return new Version(0, 1); }
        }
        public override string Description
        {
            get { return ""; }
        }
        public Plugin(Main game) : base(game)
        {
        }
        private bool[] isDead = new bool[256];
        private bool[] check = new bool[256];
        private DataStore data;
        private Command command;
        public override void Initialize()
        {
            data = new DataStore("config\\player_stats");
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerCommand.Register(this, OnCommand);
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
        }
        protected override void Dispose(bool disposing)
        {
            data.WriteToFile();
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerCommand.Deregister(this, OnCommand);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
            }
        }
        private void OnJoin(JoinEventArgs e)
        {
            string uuid = TShock.Players[e.Who].UUID;
            if (!data.BlockExists(uuid))
            {
                data.NewBlock(new string[] 
                {
                    "pveDeaths",
                    "pvpDeaths",
                    "Kills"
                }, uuid);
                Block id = data.NewBlock(new string[] 
                {
                    "UUID"
                }, TShock.Players[e.Who].Name.ToLower());
                id.WriteValue("UUID", uuid);
            }
        }
        private void OnGetData(GetDataEventArgs e)
        {
            if (!e.Handled)
            {
                if (e.MsgID == PacketTypes.PlayerDeathV2)
                {
                    using (BinaryReader br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                    {
                        byte who = br.ReadByte();
                        var reason = PlayerDeathReason.FromReader(br);
                        int damage = br.ReadInt16();
                        byte dir = br.ReadByte();
                        byte flag = br.ReadByte();
                        string d = TShock.Players[who].UUID;
                        switch (flag)
                        {
                            case 0:
                                block(d).IncreaseValue("pveDeaths", 1);
                                break;
                            case 1:
                                block(d).IncreaseValue("pvpDeaths", 1);
                                block(TShock.Players[reason.SourcePlayerIndex].UUID).IncreaseValue("Kills", 1);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }
        private void OnCommand(CommandEventArgs e)
        {
            if (command == null)
            {
                Commands.ChatCommands.Add(command = new Command("playerstats.view", ChatStats, "viewstats")
                {
                    HelpText = ""
                });
            }
        }
        private void ChatStats(CommandArgs e)
        {
            string cmd = e.Message.Substring(10);
            cmd = cmd.Substring(0, cmd.IndexOf(" "));
            string param = e.Message.Substring(10 + cmd.Length + 1);
            string uuid = data.GetBlock(param.ToLower()).GetValue("UUID");
            switch (cmd.ToLower())
            {
                case "pve-d":
                    if (data.BlockExists(uuid))
                    {
                        e.Player.SendInfoMessage(string.Concat(
                            param, 
                            "'s PvE death count: ", 
                            block(uuid).GetValue("pveDeaths"))
                        );
                        return;
                    }
                    break;
                case "pvp-d":
                    if (data.BlockExists(uuid))
                    {
                        e.Player.SendInfoMessage(string.Concat(
                            param, 
                            "'s PvP death count: ", 
                            block(uuid).GetValue("pvpDeaths"))
                        );
                        return;
                    }
                    break;
                case "k":
                    if (data.BlockExists(uuid))
                    {
                        e.Player.SendInfoMessage(string.Concat(
                            param, 
                            "'s PvP kill count: ", 
                            block(uuid).GetValue("Kills"))
                        );
                        return;
                    }
                    break;
                default:
                    break;
            }
            e.Player.SendErrorMessage("Syntax for player stats is: '/viewstats [pve-d | pvp-d | k] [player name]'.");
        }
        private Block block(string uuid)
        {
            return data.GetBlock(uuid);
        }
    }
}