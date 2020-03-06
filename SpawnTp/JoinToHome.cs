using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Localization;
using TShockAPI;
using TShockAPI.Hooks;
using TerrariaApi.Server;

namespace join_home_tp
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        private bool enabled = true;
        public override string Name
        {
            get { return "OnJoin-TP"; }
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
            get { return "On player join, it sends them to their most recent set spawn"; }
        }
        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInit);
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInit);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
            }
            base.Dispose(disposing);
        }
        public Plugin(Main game) : base(game)
        {
        }
        private void OnJoin(JoinEventArgs e)
        {
            if (enabled)
            {
                TSPlayer player = TShock.Players[e.Who];
                Player p = Main.player[e.Who];
                player.Teleport(p.SpawnX, p.SpawnY);
            }
        }
        private void OnInit(EventArgs e)
        {
            Commands.ChatCommands.Add(new Command("join2base.admin.toggle", JoinOption, "join2base")
            {
                HelpText = "Permits player, on join, to spawn at their most recent set spawn"
            });
        }
        private void JoinOption(CommandArgs e)
        {
            enabled = !enabled;
            e.Player.SendSuccessMessage("Join2Base has been " + (enabled ? "enabled" : "disabled") + ".");
        }
    }
}
