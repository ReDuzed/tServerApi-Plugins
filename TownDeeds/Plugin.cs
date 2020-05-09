using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.World.Generation;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;
using TerrariaApi;
using TerrariaApi.Server;
using RUDD;
using RUDD.Dotnet;
using RUDD.Terraria;

namespace banner
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        private DataStore data;
        private Block user; 
        private const int tileSize = 16;
        public override string Name
        {
            get { return "Town Deeds"; }
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
            get { return "Used for protecting player plots of land by use of buffs and chat alerts."; }
        }
        public Plugin(Main game) : base(game)
        {
        }
        private void StartData()
        {
            data = new DataStore(string.Concat("config\\", "town_deeds_data"));
            if (!data.BlockExists("users"))
            {
                user = data.NewBlock(new string[] { "names" }, "users");
            }
            else 
            {
                user = data.GetBlock("users");
            }
        }
        public override void Initialize()
        {
            StartData();
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            ServerApi.Hooks.GameInitialize.Register(this, OnInit);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                data.WriteToFile();
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInit);
            }
            base.Dispose(disposing);
        }
        private void OnInit(EventArgs e)
        {
            Commands.ChatCommands.Add(new Command("towndeed.user", StartTown, "plantdeed")
            {
                AllowServer = false,
                HelpText = "Attempts to place a town upon where the player is standing."
            });
            Commands.ChatCommands.Add(new Command("towndeed.user", DisbandTown, "disband")
            {
                AllowServer = false,
                HelpText = "Attempts to disband a town upon where the player is standing."
            });
            Commands.ChatCommands.Add(new Command("towndeed.user", DeedInfo, "mydeed")
            {
                AllowServer = false,
                HelpText = "Grants data regarding the player's deed."
            });
        } 
        private void DeedInfo(CommandArgs e)
        {
            const int tenGold = 100000;
            string lower = e.Message.ToLower();
            if (e.Message.Contains(" "))
            {
                Block block = data.GetBlock(GetName(e.Player.IP));
                if (lower.Contains("name"))
                {
                    e.Player.SendSuccessMessage(string.Concat("The deed you hold is named: ", block.GetValue("name")));
                    return;
                }
                else if (lower.Contains("whitelist"))
                {
                    if (lower.Substring(lower.LastIndexOf(" ") + 1) != "list")
                    {
                        if (lower.Contains("add"))
                        {
                            string name = e.Message.Substring(e.Message.LastIndexOf(" ") + 1);
                            if (!name.Contains("whitelist"))
                            {
                                block.WriteValue("whitelist", block.GetValue("whitelist") + ";" + name);
                                e.Player.SendSuccessMessage(string.Concat(name, " added to the deed security whitelist."));
                            }
                            else
                            {
                                e.Player.SendErrorMessage("There was an error in the list.");
                            }
                        }
                        else if (lower.Contains("remove"))
                        {
                            string name = e.Message.Substring(e.Message.LastIndexOf(" ") + 1);
                            if (!name.Contains("whitelist"))
                            {
                                string[] list = block.GetValue("whitelist").Split(';');
                                string names = string.Empty;
                                for (int i = 1; i < list.Length; i++)
                                {
                                    if (list[i].ToLower() != name.ToLower())
                                    {
                                        names += ";" + list[i];
                                    }
                                }
                                block.WriteValue("whitelist", names);
                                e.Player.SendSuccessMessage(string.Concat(name, " removed from the deed security whitelist."));
                            }
                            else
                            {
                                e.Player.SendErrorMessage("There was an error in the list.");
                            }
                        }
                    }
                    else
                    {
                        string[] array = block.GetValue("whitelist").Split(';');
                        string list = string.Empty;
                        for (int i = 1; i < array.Length; i++)
                        {
                            list += " " + array[i];
                        }
                        e.Player.SendSuccessMessage(string.Concat("Player deed whitelist:\n", list.TrimStart(' ')));
                    }
                    return;
                }
                else if (lower.Contains("size"))
                {
                    int size = 10, difference;
                    int oldSize = int.Parse(block.GetValue("size"));
                    if (int.TryParse(e.Message.Substring(e.Message.LastIndexOf(" ") + 1), out size))
                    {
                        size = Math.Min(Math.Max(size, 10), 25);
                        difference = size - oldSize;
                        if (difference > 0)
                        {
                            if (CoinPurse.ShopItem(e.Player.Index, 10000 * difference))
                            {
                                block.WriteValue("size", size.ToString());
                                e.Player.SendSuccessMessage(string.Concat("The deed radius has been increased to ", size, " tiles!"));
                            }
                            else
                            {
                                e.Player.SendErrorMessage(string.Concat(difference, " gold in total is required to upgrade the deed size."));
                            }
                        }
                        else
                        {
                            e.Player.SendErrorMessage("The deed size is already at ", block.GetValue("size"), ". The maximum size is a 25 tile radius.");
                        }
                    }
                    else
                    {
                        e.Player.SendErrorMessage("There has been an error.");
                    }
                    return;
                }
                else if (lower.Contains("guard"))
                {
                    e.Player.SendErrorMessage("This command is obsolete.");
                    return;
                    /*
                    if (CoinPurse.ShopItem(e.Player.Index, tenGold))
                    {
                        bool guard = false;
                        bool.TryParse(block.GetValue("guard"), out guard);
                        guard = true;
                        block.WriteValue("guard", guard.ToString());
                        e.Player.SendSuccessMessage(string.Concat("The town guard is set to protect the premises."));
                    }*/
                }
                else if (lower.Contains("alarm"))
                {
                    bool alarm = false;
                    bool.TryParse(block.GetValue("alarm"), out alarm);
                    if (!alarm && CoinPurse.ShopItem(e.Player.Index, tenGold))
                    {
                        alarm = true;
                        block.WriteValue("alarm", alarm.ToString());
                        e.Player.SendSuccessMessage(string.Concat("The town alarm is set to sound on invasions."));
                    }
                    else
                    {
                        e.Player.SendErrorMessage("A deed alarm " + (alarm ? "is already enabled." : "costs 10 gold."));
                    }
                }
                else if (lower.Contains("honey"))
                { 
                    bool honey = false;
                    bool.TryParse(block.GetValue("honey"), out honey);    
                    if (!honey && CoinPurse.ShopItem(e.Player.Index, tenGold))
                    {
                        honey = true;
                        block.WriteValue("honey", honey.ToString());
                        e.Player.SendSuccessMessage(string.Concat("For those in the town roster and proximity gain the honey debuff: ", honey, "."));
                    }
                    else
                    {
                        e.Player.SendErrorMessage("This buff " + (honey ? "is already enabled." : "costs 10 gold."));
                    }
                }
                else if (lower.Contains("poison"))
                {
                    bool poison = false;
                    bool.TryParse(block.GetValue("poison"), out poison);    
                    if (!poison && CoinPurse.ShopItem(e.Player.Index, tenGold))
                    {
                        poison = true;
                        block.WriteValue("poison", poison.ToString());
                        e.Player.SendSuccessMessage(string.Concat("Invaders now are given the poisoned debuff when in proximity."));
                    }
                    else
                    {
                        e.Player.SendErrorMessage("This debuff, applied to invaders " + (poison ? "is already enabled." : "costs 10 gold."));
                    }
                }
                else if (lower.Contains("fire"))
                {
                    bool fire = false;
                    bool.TryParse(block.GetValue("fire"), out fire);
                    if (!fire && CoinPurse.ShopItem(e.Player.Index, tenGold))
                    {
                        fire = true;
                        block.WriteValue("fire", fire.ToString());
                        e.Player.SendSuccessMessage(string.Concat("Invaders now are given the on fire debuff when in proximity."));
                    }
                    else
                    {
                        e.Player.SendErrorMessage("This debuff, applied to invaders " + (fire ? "is already enabled." : "costs 10 gold."));
                    }
                }
                else if (lower.Contains("ice"))
                {
                    bool ice = false;
                    bool.TryParse(block.GetValue("ice"), out ice);    
                    if (!ice && CoinPurse.ShopItem(e.Player.Index, tenGold))
                    {
                        ice = true;
                        block.WriteValue("ice", ice.ToString());
                        e.Player.SendSuccessMessage(string.Concat("Invaders now are given the frost burn debuff when in proximity."));
                    }
                    else
                    {
                        e.Player.SendErrorMessage("This debuff, applied to invaders " + (ice ? "is already enabled." : "costs 10 gold."));
                    }
                }
                else if (lower.Contains("venom"))
                {
                    bool venom = false;
                    bool.TryParse(block.GetValue("venom"), out venom);    
                    if (!venom && CoinPurse.ShopItem(e.Player.Index, tenGold))
                    {
                        venom = true;
                        block.WriteValue("venom", venom.ToString());
                        e.Player.SendSuccessMessage(string.Concat("Invaders now are given the venom debuff when in proximity."));
                    }
                    else
                    {
                        e.Player.SendErrorMessage("This debuff, applied to invaders " + (venom ? "is already enabled." : "costs 10 gold."));
                    }
                }
                else if (lower.Contains("upkeep"))
                {
                    int upkeep = int.Parse(block.GetValue("upkeep"));
                    int difference = (tenGold - upkeep) / 100;
                    int spent = 0;
                    if (lower.Contains("info"))
                    {
                        e.Player.SendInfoMessage(string.Concat("Upkeep is at ", upkeep / 100, " silver with the missing margin being ", difference, " silver which can be repaid."));
                        return;
                    }
                    if (lower.Contains(" ") && lower.Contains("add"))
                    {
                        int parse;
                        if (int.TryParse(lower.Substring(lower.LastIndexOf(" ") + 1), out parse))
                        {
                            while (parse > difference)
                            {
                                parse -= 100;
                            }
                            if (parse > 0)
                            {
                                for (int i = 0; i < parse; i++)
                                {
                                    if (CoinPurse.ShopItem(e.Player.Index, 100))
                                    {
                                        spent += 1;
                                    }
                                }
                                e.Player.SendSuccessMessage(string.Concat(parse, " silver in total spent on increasing upkeep."));
                            }
                        }
                        else
                        {
                            e.Player.SendSuccessMessage(string.Concat("The upkeep is missing ", difference, " silver. Use '/mydeed upkeep add <#>'."));
                        }
                    }
                    else
                    {
                        e.Player.SendErrorMessage("Try: '/mydeed upkeep add <# of silver>' or '/mydeed upkeep info'");
                    }
                }
            }
            else
            {
                e.Player.SendErrorMessage("The list of the subcommands is:\nname, whitelist <add | remove | list> <name>, size <##>, upkeep <add <#> | info>, alarm, honey, poison, fire, ice, venom");
            }
        }
        private void DisbandTown(CommandArgs e)
        {
            if (e.Message.Contains(" "))
            {
                string name = e.Message.Substring(e.Message.IndexOf(" ") + 1);
                string user = GetName(e.Player.IP);
                Block block;
                if (data.BlockExists(user))
                {
                    block = data.GetBlock(user);
                    if (block.GetValue("name").Substring(user.Length + 3) != name)
                    {
                        e.Player.SendErrorMessage("The deed's suffix needs to match the name input.");
                        return;
                    }
                    string[] s = block.GetValue("token").Split(';');
                    Vector2 token = new Vector2(float.Parse(s[0]), float.Parse(s[1]));
                    if (bool.Parse(block.GetValue("deed")))
                    {
                        if (e.TPlayer.Distance(new Microsoft.Xna.Framework.Vector2(token.X * tileSize, token.Y * tileSize)) < int.Parse(block.GetValue("size")) * 16)
                        {
                            int upkeep = int.Parse(block.GetValue("upkeep"));
                            for (int i = 0; i < upkeep / 100; i++)
                            {
                                e.Player.GiveItem(ItemID.SilverCoin, "Silver Coin", 32, 48, 1);
                            }
                            block.WriteValue("deed", false.ToString());
                            block.WriteValue("name", "0");
                            block.WriteValue("size", "0");
                            block.WriteValue("token", "0");
                            block.WriteValue("upkeep", "0");
                            TSPlayer.All.SendSuccessMessage(string.Concat(user, " has just disbanded the deed: ", name));
                        }
                        else
                        {
                            e.Player.SendErrorMessage("To disband the deed, being within its proximity is necessary.");
                        }
                    }
                    else
                    {
                        e.Player.SendErrorMessage("There is no deed for you to disband.");
                    }
                }
            }
            else 
            {
                e.Player.SendErrorMessage("Use: '/disband <name>' instead. Note that being in the deed's proximity is necessary.");
            }
        }
        private void StartTown(CommandArgs e)
        {
            if (e.Message.Contains(" "))
            {
                string name = e.Message.Substring(e.Message.IndexOf(" ") + 1);
                string user = GetName(e.Player.IP);
                bool deed = false;
                Block block = data.GetBlock(user);
                if (bool.TryParse(block.GetValue("deed"), out deed))
                {
                    if (!deed)
                    {
                        Vector2[] region;
                        if ((region = StatueProximity(e.TPlayer)) != null)
                        {
                            if (CoinPurse.ShopItem(e.Player.Index, 100000))
                            {
                                string title = string.Concat(user, "'s ", name);
                                TShock.Regions.AddRegion((int)region[0].X / 16 - 4, (int)region[0].Y / 16 - 4, 6, 8, title, user, Main.worldID.ToString(), 4);
                                deed = true;
                                block.WriteValue("deed", true.ToString());
                                block.WriteValue("name", title);
                                block.WriteValue("size", 10.ToString());
                                block.WriteValue("upkeep", 100000.ToString());
                                block.WriteValue("token", string.Concat(region[0].X / tileSize, ";", region[0].Y / tileSize));
                                for (int i = 0; i < 2; i++)
                                {
                                    for (int j = 0; j < 3; j++)
                                    {
                                        WorldGen.PlaceWire((int)region[0].X / 16 + i, (int)region[0].Y / 16 + j);
                                    }
                                }
                                e.Player.SendSuccessMessage(string.Concat("Deed token designated at tile coordinates ", region[0].X / tileSize, " x ", region[0].Y / tileSize, " y."));
                            }
                            else
                            {
                                e.Player.SendErrorMessage(string.Concat("To start a deed 10 gold is required therefore make sure 10 gold is in your inventory."));
                            }
                        }
                        else
                        {
                            e.Player.SendErrorMessage("Planting a deed requires proximity to a statue of any kind.");
                        }
                    }
                    else
                    {
                        e.Player.SendErrorMessage("A deed is already owned by you therefore you cannot designate a new one.");
                    }
                }
                else
                {
                    e.Player.SendErrorMessage("Parsing error in checking for the deed value for user " + user + ". It may mean the .dat file was manually modified.");
                }
            }
            else
            {
                e.Player.SendErrorMessage("Use: '/plantdeed <name>' instead.");
            }
        }
        private void OnJoin(JoinEventArgs e)
        {
            Block block;
            Block users;
            string[] array;
            string ip = TShock.Players[e.Who].IP;
            string name = TShock.Players[e.Who].Name;
            if (!data.BlockExists(name))
            {
                block = data.NewBlock(new string[] 
                {
                    "IP", "deed", "name", "token", "whitelist", "size", "upkeep", "guard", "alarm", "honey", "poison", "fire", "ice", "venom"
                }, name);
                block.WriteValue("IP", ip);
                block.WriteValue("deed", false.ToString());
            }
            else
            {
                List<string> IP = new List<string>();
                users = data.GetBlock("users");
                array = users.GetValue("names").Split(';');
                for (int i = 1; i < array.Length; i++)
                {
                    if (i < array.Length)
                    {
                        if (data.BlockExists(array[i]))
                        {
                            block = data.GetBlock(array[i]);
                            IP.Add(block.GetValue("IP"));
                        }
                    }
                }
                if (IP.FindAll(t => t == ip).Count == 0)
                {
                    block = data.NewBlock(new string[] 
                    {
                        "IP", "deed", "name", "token", "whitelist", "size", "upkeep", "guard", "alarm", "honey", "poison", "fire", "ice", "venom"
                    }, name);
                    block.WriteValue("IP", ip);
                    block.WriteValue("deed", false.ToString());
                }
            }
            array = user.GetValue("names").Split(';');
            string list = string.Empty;
            for (int i = 0; i < array.Length; i++)
            {
                list += array[i] + ";";
                if (array[i] == name)
                {
                    return;
                }
                if (i == array.Length - 1)
                {
                    list += name;
                    break;
                }
            }
            user.WriteValue("names", list);
        }
        private void OnUpdate(EventArgs e)
        {
            if ((int)Main.time % 300 == 0)
            {
                for (int i = 0; i < Main.player.Length; i++)
                {
                    Player player = Main.player[i];
                    if (player == null || !player.active || player.dead)
                        continue;
                    string[] name = user.GetValue("names").Split(';');
                    for (int j = 1; j < name.Length; j++)
                    {
                        Block block = data.GetBlock(name[j]);
                        bool deed;
                        int m = 0;
                        bool.TryParse(block.GetValue("deed"), out deed);
                        int.TryParse(block.GetValue("upkeep"), out m);
                        if (deed && m > 0)
                        {
                            int size = int.Parse(block.GetValue("size"));
                            string[] s = block.GetValue("token").Split(';');
                            Vector2 token = new Vector2(float.Parse(s[0]), float.Parse(s[1]));
                            if (player.Distance(new Microsoft.Xna.Framework.Vector2(token.X * tileSize, token.Y * tileSize)) < size * 16)
                            {
                                bool guard, alarm, poison, fire, ice, venom;
                                bool.TryParse(block.GetValue("alarm"), out alarm);
                                bool.TryParse(block.GetValue("guard"), out guard);
                                bool.TryParse(block.GetValue("poison"), out poison);
                                bool.TryParse(block.GetValue("fire"), out fire);
                                bool.TryParse(block.GetValue("ice"), out ice);
                                bool.TryParse(block.GetValue("venom"), out venom);
                                if (block.GetValue("name").Contains(player.name) || block.GetValue("whitelist").Contains(TShock.Players[i].Name))
                                {
                                    if (block.GetValue("honey").ToLower() == "true")
                                    {
                                        TShock.Players[i].SetBuff(BuffID.Honey, 1800, true);
                                    }
                                    continue;
                                }
                                if (alarm)
                                {
                                    for (int n = 0; n < Main.player.Length; n++)
                                    {
                                        Player p = Main.player[n];
                                        if (p == null || !p.active || p.dead || !block.GetValue("name").Contains(p.name))   // continue if 'deed name' does not contain player name
                                            continue;
                                            // TShock.Players[i].Name: name of unique player in proximity
                                            // TShock.Players[p.whoAmI]: deed owner
                                        TShock.Players[p.whoAmI].SendMessage(string.Concat(TShock.Players[i].Name, " has invaded your deed's parameter!"), Microsoft.Xna.Framework.Color.Firebrick);
                                        break;
                                    }
                                }
                                if (guard)
                                {
                                    Wiring.TripWire((int)token.X / 16 - 1, (int)token.Y / 16 - 2, 1, 2);
                                }
                                if (poison)
                                {
                                    TShock.Players[i].SetBuff(BuffID.Poisoned, 900, true);
                                }
                                if (fire)
                                {
                                    TShock.Players[i].SetBuff(BuffID.OnFire, 900, true);
                                }
                                if (ice)
                                {
                                    TShock.Players[i].SetBuff(BuffID.Frostburn, 900, true);
                                }
                                if (venom)
                                {
                                    TShock.Players[i].SetBuff(BuffID.Venom, 900, true);
                                }
                                int whoAmI = 255;
                                for (int n = 0; n < Main.player.Length; n++)
                                {
                                    Player p = Main.player[n];
                                    if (p == null || !p.active || p.dead || !block.GetValue("name").Contains(p.name))   // continue if 'deed name' does not contain player name
                                        continue;
                                        // TShock.Players[i].Name: name of unique player in proximity
                                        // TShock.Players[p.whoAmI]: deed owner
                                    whoAmI = p.whoAmI;
                                    break;
                                }   
                                if (TShock.Players[i].TPlayer.Distance(new Microsoft.Xna.Framework.Vector2(token.X * tileSize, token.Y * tileSize)) < (size / 5) * 16)
                                {
                                    int upkeep = 99;
                                    if (int.TryParse(block.GetValue("upkeep"), out upkeep))
                                    {
                                        if (i != whoAmI)
                                        {
                                            upkeep = Math.Max(block.IncreaseValue("upkeep", -500) - 500, 0);
                                        }
                                    }
                                    if (upkeep <= 0)
                                    {
                                        string plr = string.Empty;;
                                        block.WriteValue("deed", false.ToString());
                                        plr = Main.player[whoAmI].name;
                                        TShock.Players[whoAmI].SendMessage(string.Concat(TShock.Players[i].Name, " has reduced your deed's upkeep to zero therefore disbanding it."), Microsoft.Xna.Framework.Color.Firebrick);
                                        TSPlayer.All.SendInfoMessage(string.Concat(TShock.Players[i].Name, " has just disbanded ", plr, "'s deed."));
                                    }
                                    if (i != whoAmI && upkeep % 2000 == 0)
                                    {
                                        TShock.Players[i].SendSuccessMessage(string.Concat("The deed's upkeep has been drained to ", upkeep, "."));
                                    }
                                }
                            }
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
            }
        }
        private Vector2[] StatueProximity(Player player, int size = 10)
        {
            const int tileSize = 16;
            for (int i = -size; i < size; i++)
            {
                for (int j = -size; j < size; j++)
                {
                    int x = (int)(player.position.X - (player.position.X % tileSize)) + i * tileSize;
                    int y = (int)(player.position.Y - (player.position.Y % tileSize)) + j * tileSize;
                    if (Main.tile[x / tileSize, y / tileSize].active() && Main.tile[x / tileSize, y / tileSize].type == TileID.Statues)
                    {
                        return new Vector2[] { new Vector2(x, y), new Vector2(x + 1 * tileSize, y + 5 * tileSize) };
                    }
                }
            }
            return null;
        }
        private string GetName(string IP)
        {
            string[] array = user.GetValue("names").Split(';');
            for (int i = 0; i < array.Length; i++)
            {
                if (data.GetBlock(array[i]).GetValue("IP") == IP)
                {
                    return array[i];
                }
            }
            return string.Empty;
        }
    }
}
