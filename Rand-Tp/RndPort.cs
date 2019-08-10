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
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        private bool enabled = false;
        private bool update = true;
        private List<Vector2> position = new List<Vector2>();
        private List<User> users = new List<User>();
        private int maxCooldown = second * 30;
        private const int second = 60;
        public override string Name
        {
            get { return "Random-TP"; }
        }
        public override Version Version
        {
            get { return new Version(0, 2); }
        }
        public override string Author 
        {
            get { return "Duze"; }
        }
        public override string Description
        {
            get { return "Adds chat command that enables a player to teleport anywhere on the given overworld"; }
        }
        public override void Initialize()
        {
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.GameInitialize.Register(this, OnInit);
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInit);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            }
            base.Dispose(disposing);
        }
        public Plugin(Main game) : base(game)
        {
        }
        public void OnInit(EventArgs e)
        {
            Commands.ChatCommands.Add(new Command("randport.admin.toggle", RandOption, "randport")
            {
                HelpText = "Changes whether or not all users can teleport to a random location on the overworld"
            });
            Commands.ChatCommands.Add(new Command("randport.admin.settime", SetCooldown, "cooldown")
            {
                HelpText = "Permits user to change the maximum time for the cooldown (in seconds)"
            });
            Commands.ChatCommands.Add(new Command("randport.teleport", UserTp, "random")
            {
                HelpText = "Teleport to a random location on the overworld"
            });
        }
        public void OnChat(ServerChatEventArgs e)
        {
        }
        public void OnGetData(GetDataEventArgs e)
        {
        }
        private void UserTp(CommandArgs e)
        {
            User user = null;
            foreach (User u in users)
            {
                if (u.whoAmI == e.Player.Index && u.cooldown > 0)
                {
                    e.Player.SendMessage("Overworld teleport cooldown: " + (u.cooldown / 60) + " seconds", 150, 255, 150);
                    return;
                }
                else user = u;
            }
            if (enabled)
            {
                position.Clear();
                int spawnY = Main.spawnTileY - 100;
                for (int i = 100; i < Main.maxTilesY; i++)
                for (int j = spawnY; j < spawnY + 200; j++)
                {
                    if (Main.tile[i, j].wall == Terraria.ID.WallID.None && !Main.tile[i, j].active() && !Main.tile[i + 1, j].active() && Main.tile[i, j + 1].active() && Main.tileSolid[Main.tile[i, j+ 1].type])
                    {
                        position.Add(new Vector2(i * 16, j * 16));
                    }
                }
                Vector2 moveTo = position[Main.rand.Next(position.Count - 1)];
                e.Player.Teleport(moveTo.X, moveTo.Y - 32);
                user.cooldown = 60 * maxCooldown;
            }
        }
        private void OnJoin(JoinEventArgs e)
        {
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].whoAmI == e.Who)
                    return;
            }
            users.Add(new User(e.Who)
            {
                cooldown = 60 * maxCooldown
            });
        }
        private void OnLeave(LeaveEventArgs e)
        {
            update = false;
            for (int i = 0; i < users.Count; i++)
            {
                if (users[i].whoAmI == e.Who)
                    users.RemoveAt(i);
            }            
        }
        private void OnUpdate(EventArgs e)
        {
            if (update)
            {
                foreach (User u in users)
                    u.Update();
            }
            else update = true;
        }
        private void RandOption(CommandArgs e)
        {
            enabled = !enabled;
            e.Player.SendSuccessMessage("RandPort has been " + (enabled ? "enabled" : "disabled") + ".");
        }
        private void SetCooldown(CommandArgs e)
        {
            int cd = 0;
            if (int.TryParse(e.Message.Substring(e.Message.IndexOf(' ') + 1), out cd))
            {
                maxCooldown = cd;
                e.Player.SendSuccessMessage("Duration of cooldown has been set to " + cd + " seconds.");
            }
            else
            {
                e.Player.SendErrorMessage("The input for the total duration in seconds was in the incorrect format.");
            }
            if (!enabled)
                e.Player.SendErrorMessage("Reminder that the /random feature is disabled (use /randport to change it).");
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
    class User
    {
        public int cooldown
        {
            get; internal set;
        }
        public int whoAmI;
        public User(int who)
        {
            this.whoAmI = who;
        }
        public void Update()
        {
            if (cooldown > 0)
                cooldown--;
        }
    }
}
