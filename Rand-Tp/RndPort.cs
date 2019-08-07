using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Localization;
using TShockAPI;
using TShockAPI.Hooks;
using TerrariaApi.Server;

namespace rnd_tp
{
    public class Plugin : TerrariaPlugin
    {
        private bool enabled = false;
        private int who = 255;
        private bool activated;
        private List<Vector2> position = new List<Vector2>();
        public override string Name
        {
            get { return "Random-TP"; }
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
            get { return "Adds chat command that enables a player to teleport anywhere on the given overworld (optional: for a certain price)"; }
        }
        public override void Initialize()
        {
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.GameInitialize.Register(this, OnInit);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInit);
            }
            base.Dispose(disposing);
        }
        public Plugin(Main game) : base(game)
        {
        }
        public void OnInit(EventArgs e)
        {
            Commands.ChatCommands.Add(new Command("randport.admin.toggle", RandOption, "random")
            {
                HelpText = "Permits players to teleport to a random location on the overworld"
            });
            int spawnY = Main.spawnTileY - 100;
            for (int i = 100; i < Main.maxTilesY; i++)
            for (int j = spawnY; j < spawnY + 200; j++)
            {
                if (Main.tile[i, j].wall == Terraria.ID.WallID.None && Main.tile[i, j + 1].active() && Main.tileSolid[Main.tile[i, j+ 1].type])
                {
                    position.Add(new Vector2(i * 16, j * 16));
                }
            }
        }
        public void OnChat(ServerChatEventArgs e)
        {
            if (e.Text.StartsWith("/random") && !activated)
            {
                activated = true;
                who = e.Who;
            }
        }
        public void OnGetData(GetDataEventArgs e)
        {
            if (!enabled || !activated)
                return;
            if (!e.Handled)
            {
                Vector2 moveTo = position[Main.rand.Next(position.Count - 1)];
                TShock.Players[who].Teleport(moveTo.X, moveTo.Y);
                activated = false;
                who = 255;
            }
        }
        private void RandOption(CommandArgs e)
        {
            enabled = !enabled;
            e.Player.SendSuccessMessage("RandPort has been " + (enabled ? "enabled" : "disabled") + ".");
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
    }
}
