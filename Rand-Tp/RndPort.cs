using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Localization;
using TShockAPI;
using TShockAPI.Hooks;
using TerrariaApi.Server;
using RUDD.Dotnet;
using RUDD.Terraria;

namespace rnd_tp
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        private bool enabled = false;
        private bool update = true;
        private List<Vector2> position = new List<Vector2>();
        private List<User> users = new List<User>();
        private int maxCooldown = 30;
        private const int second = 60;
        private int copperPrice = 10000;
        private Ini ini;
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
            ini = new Ini()
            {
                setting = new string[]
                {
                    "enabled",
                    "cooldown",
                    "goldcost"
                },
                path = "config\\rndtp_config" + Ini.ext
            };
            if (!File.Exists(ini.path))
            {
                Directory.CreateDirectory("config");
                ini.WriteFile(new string[] { true.ToString(), 60.ToString(), 1.ToString() });
            }
            else
            {
                ReadFile(ini);
            }
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
            Commands.ChatCommands.Add(new Command("randport.admin.price", AdminPrice, "modifyprice")
            {
                HelpText = "Modifies price to use teleport"
            });
            Commands.ChatCommands.Add(new Command("randport.admin.reload", AdminReload, "reload")
            {
                HelpText = "Reloads from INI file"
            });
        }
        private void ReadFile(Ini ini)
        {
            string e = string.Empty, m = string.Empty, c = string.Empty;
            var i = ini.ReadFile();
            Ini.TryParse(i[0], out e);
            Ini.TryParse(i[1], out m);
            Ini.TryParse(i[2], out c);
            bool.TryParse(e, out enabled);
            int.TryParse(m, out maxCooldown);
            int.TryParse(c, out copperPrice);
            copperPrice *= 10000;
        }
        private void AdminReload(CommandArgs e)
        {
            ReadFile(ini);
            e.Player.SendSuccessMessage("[RandTp] Ini settings reloaded.");
        }
        public void OnChat(ServerChatEventArgs e)
        {
        }
        public void OnGetData(GetDataEventArgs e)
        {
        }
        private void AdminPrice(CommandArgs e)
        {
            if (e.Message.Contains(" "))
            {
                string copper = e.Message.Substring(e.Message.IndexOf(" ") + 1);
                int.TryParse(copper, out copperPrice);
                copperPrice = Math.Max(10000, copperPrice);
                e.Player.SendSuccessMessage("Gold cost for random-tp set to: " + copperPrice / 10000);
            }
            else
            {
                e.Player.SendErrorMessage("Command: '/modifyprice [copper]' (total copper in one gold = 10000)");
            }
        }
        private void UserTp(CommandArgs e)
        {
            User user = null;
            foreach (User u in users)
            {
                if (u.whoAmI == e.Player.Index && u.cooldown > 0)
                {
                    e.Player.SendMessage("Overworld teleport cooldown: " + (u.cooldown / 60) + " seconds", 100, 255, 100);
                    return;
                }
                else user = u;
            }
            if (enabled) 
            {
                if (CoinPurse.ShopItem(e.TPlayer.whoAmI, copperPrice))
                {
                    position.Clear();
                    int spawnY = Main.spawnTileY - 100;
                    for (int i = 120; i < Main.maxTilesX - 100; i++)
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
                else
                {
                    e.Player.SendInfoMessage("It requires " + copperPrice / 10000 + " gold to teleport onto a random location the overworld.");
                }
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
                cooldown = 60 * 15
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
