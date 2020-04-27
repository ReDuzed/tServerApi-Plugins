using System;
using System.IO;
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


namespace teamset
{
    [ApiVersion(2,1)]
    public class Plugin : TerrariaPlugin
    {
        private string[] Teams
        {
            get { return new string[] { "None", "Red Team", "Green Team", "Blue Team", "Yellow Team", "Pink Team" }; }
        }
        private string redTeam = "red", greenTeam = "green", blueTeam = "blue", yellowTeam = "yellow", pinkTeam = "pink";
        private string[] Groups
        {
            get { return new string[] { "none", redTeam, greenTeam, blueTeam, yellowTeam, pinkTeam }; }
        }
        private string[] informal
        {
            get { return new string[] { "none", "red", "green", "blue", "yellow", "pink" }; }
        }
        private const string Empty = "0";
        private bool kickOnSwitch;
        private DataStore data;
        private Command command;
        private Block setting;
        public override string Name
        {
            get { return "Team Set"; }
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
            get { return "Places players into teams on server join after they have been set to a team by an admin"; }
        }
        public Plugin(Main game) : base(game)
        {
        }
        private void Reload()
        {
            if (!Directory.Exists("config"))
                Directory.CreateDirectory("config");
            
            Ini ini = new Ini()
            {
                setting = new string[] { "playersperteam", "kickonswitch" },
                path = "config\\team_data" + Ini.ext
            };
            int total = 0;
            if (!File.Exists(ini.path))
                ini.WriteFile(new object[] { 8, false });
            
            string t = string.Empty;
            string kick = string.Empty;
            var file = ini.ReadFile();
            if (file.Length > 0)
            {
                Ini.TryParse(file[0], out t);
                Ini.TryParse(file[1], out kick);
            }
            bool.TryParse(kick, out kickOnSwitch);
            total = int.Parse(t);

            total = Math.Max(total, 2);
            string[] Slots = new string[total];
            for (int i = 0; i < total; i++)
                Slots[i] = "players" + (i + 1);

            data = new DataStore("config\\team_data");
            foreach (string team in Teams)
            {
                if (!data.BlockExists(team))
                    data.NewBlock(Slots, team);
                else
                {
                    Block block;
                    if ((block = data.GetBlock(team)).Contents.Length < total)
                    {
                        for (int i = 0; i < total; i++)
                        {
                            if (!block.Keys()[i].Contains(i.ToString()))
                                block.AddItem("players" + i, "0");
                        }
                    }
                }
            }
            string[] keys = informal;
            if (!data.BlockExists("groups"))
            {
                setting = data.NewBlock(keys, "groups");
                for (int i = 0; i < Groups.Length; i++)
                {
                    setting.WriteValue(keys[i], Groups[i]);
                }
            }
            else
            {
                setting = data.GetBlock("groups");
                for (int i = 0; i < Groups.Length; i++)
                {
                    setting.WriteValue(keys[i], Groups[i]);
                }
            }
        }
        public override void Initialize()
        {   
            Reload();
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerCommand.Register(this, OnCommand);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                data.WriteToFile();
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerCommand.Deregister(this, OnCommand);
            }
            base.Dispose(disposing);
        }
        private void OnJoin(JoinEventArgs e)
        {
            SetTeam(e.Who, GetPlayerTeam(Main.player[e.Who].name));
        }
        private void OnGetData(GetDataEventArgs e)
        {
            if (!e.Handled)
            {
                if (e.MsgID == PacketTypes.PlayerTeam)
                {
                    using (BinaryReader br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                    {
                        byte who = br.ReadByte();
                        byte team = br.ReadByte();
                        int check = GetPlayerTeam(Main.player[who].name);
                        SetTeam(who, check);
                        if (kickOnSwitch && team != check && team != 0)
                        {
                            TShock.Players[who].Disconnect("Kicked for switching teams.");
                        }
                    }
                }
            }
        }
        private void OnCommand(CommandEventArgs e)
        {
            if (command == null)
            {
                Commands.ChatCommands.Add(new Command("teamset.admin.set", PlaceTeam, new string[] { "placeteam", "removeteam" })
                {
                    HelpText = "For placing or removing players from teams."
                });
                Commands.ChatCommands.Add(new Command("teamset.admin", Reload, new string[] { "reload" })
                {
                    HelpText = "Reloads settings."
                });
                Commands.ChatCommands.Add(new Command("teamset.admin.group", MakeGroups, new string[] { "teamgroups" })
                {
                    HelpText = "Makes general groups for each team color."
                });
                Commands.ChatCommands.Add(new Command("teamset.admin.group", MakeGroups, new string[] { "teamset" })
                {
                    HelpText = "Makes general groups for each team color."
                });
                Commands.ChatCommands.Add(command = new Command("teamset.join", JoinTeam, new string[] { "jointeam" })
                {
                    HelpText = "Allows players to join a team if they aren't on one already."
                });
            }
        }
        private void Reload(CommandArgs e)
        {
            Reload();
            e.Player.SendSuccessMessage("[TeamSet] settings reloaded.");
        }
        private void MakeGroups(CommandArgs e)
        {
            if (e.Message.ToLower().Contains("teamset"))
            {
                string cmd = e.Message.Substring(7);
                if (cmd.Contains("red"))
                {
                    redTeam = cmd.Substring(cmd.LastIndexOf(" ") + 1);
                    e.Player.SendSuccessMessage("Red's group: " + redTeam);
                }
                else if (cmd.Contains("green"))
                {
                    greenTeam = cmd.Substring(cmd.LastIndexOf(" ") + 1);
                    e.Player.SendSuccessMessage("Green's group: " + greenTeam);
                }
                else if (cmd.Contains("blue"))
                {
                    blueTeam = cmd.Substring(cmd.LastIndexOf(" ") + 1);
                    e.Player.SendSuccessMessage("Blue's group: " + blueTeam);
                }
                else if (cmd.Contains("yellow"))
                {
                    yellowTeam = cmd.Substring(cmd.LastIndexOf(" ") + 1);
                    e.Player.SendSuccessMessage("Yellow's group: " + yellowTeam);
                }
                else if (cmd.Contains("pink"))
                {
                    pinkTeam = cmd.Substring(cmd.LastIndexOf(" ") + 1);
                    e.Player.SendSuccessMessage("Pink's group: " + pinkTeam);
                }
                else
                {
                    e.Player.SendInfoMessage("/teamset [team color] [group name]");
                }
                for (int i = 1; i < Groups.Length; i++)
                    setting.WriteValue(informal[i], Groups[i]);
                return;
            }
            var manage = TShock.Groups;
            if (manage.GroupExists("default"))
            {
                manage.GetGroupByName("default").SetPermission(new System.Collections.Generic.List<string>() { "teamset.join" });
                manage.GetGroupByName("default").ChatColor = "200,200,200";
                manage.GetGroupByName("default").Prefix = "[i:1] ";
            }
            if (!manage.GroupExists("team"))
            {
                manage.AddGroup("team", "default", "", "255,255,255");
                Console.WriteLine("The group 'team' has been made.");
            }
            for (int i = 1; i < Teams.Length; i++)
            {
                if (!TShock.Groups.GroupExists(Groups[i]))
                {
                    TShock.Groups.AddGroup(Groups[i], "team", "", "255,255,255");
                    switch (i)
                    {
                        case 1:
                            manage.GetGroupByName(Groups[i]).Prefix = "[i:1526] ";
                            manage.GetGroupByName(Groups[i]).ChatColor = "200,000,000";
                            break;
                        case 2:
                            manage.GetGroupByName(Groups[i]).Prefix = "[i:1525] ";
                            manage.GetGroupByName(Groups[i]).ChatColor = "000,200,050";
                            break;
                        case 3:
                            manage.GetGroupByName(Groups[i]).Prefix = "[i:1524] ";
                            manage.GetGroupByName(Groups[i]).ChatColor = "100,100,200";
                            break;
                        case 4:
                            manage.GetGroupByName(Groups[i]).Prefix = "[i:1523] ";
                            manage.GetGroupByName(Groups[i]).ChatColor = "200,150,000";
                            break;
                        case 5:
                            manage.GetGroupByName(Groups[i]).Prefix = "[i:1522] ";
                            manage.GetGroupByName(Groups[i]).ChatColor = "200,000,150";
                            break;
                    }
                    Console.WriteLine("The group '", Groups[i], "' has been made.");
                }
            }
            string msg;
            Console.WriteLine(msg = "The permissions, group colors, and chat prefixes have not been completely set up and will need to be done manually, though each team group has been parented to group 'team'.");
            e.Player.SendSuccessMessage(msg);
        }
        private void PlaceTeam(CommandArgs e)
        {
            string cmd = string.Empty;
            if (e.Message.ToLower().Contains("placeteam"))
            {
                if ((cmd = e.Message.ToLower()).Length > 9 && e.Message.Contains(" "))
                {
                    for (int i = 0; i < Main.player.Length; i++)
                    {
                        var player = Main.player[i];
                        if (player.active)
                        {
                            string name = player.name.ToLower();
                            if (cmd.ToLower().Contains(name))
                            {
                                string preserveCase = cmd.Substring(cmd.IndexOf(" ") + 1, name.Length);
                                string sub = cmd.Substring(cmd.IndexOf(" ") + 1, name.Length).ToLower();
                                if (sub == name)
                                {
                                    string team = cmd.Substring(cmd.LastIndexOf(" ") + 1).ToLower();
                                    int t = GetTeamIndex(team);
                                    if (t > 0 || int.TryParse(team, out t))
                                    {
                                        int get = 0;
                                        if ((get = GetPlayerTeam(name)) == 0)
                                        {
                                            if (SetPlayerTeam(name, t))
                                            {
                                                e.Player.SendSuccessMessage(string.Concat(preserveCase, " is now on team ", Teams[t], "."));
                                                string set = Groups[t].ToLower();
                                                if (TShock.Groups.GroupExists(set))
                                                {
                                                    e.Player.Group = TShock.Groups.GetGroupByName(set);
                                                    Console.WriteLine(string.Concat(e.Player.Name, " has been set to group ", set, "!"));
                                                }
                                            }
                                            else
                                            {
                                                e.Player.SendErrorMessage(string.Concat(Teams[t], " might be already full."));
                                            }
                                        }
                                        else 
                                        {
                                            e.Player.SendErrorMessage(string.Concat(preserveCase, " is already on ", Teams[get], ". Using /removeteam [name] will remove the player from their team."));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else if (e.Message.Contains("removeteam"))
            {
                if ((cmd = e.Message).Length > 10 && e.Message.Contains(" "))
                {
                    string name = string.Empty;
                    if (RemoveFromTeam(name = cmd.Substring(cmd.IndexOf(" ") + 1)))
                    {
                        e.Player.SendSuccessMessage(string.Concat(name, " has been removed from their team."));
                        string set = "default";
                        if (TShock.Groups.GroupExists(set))
                        {
                            e.Player.Group = TShock.Groups.GetGroupByName(set);
                            Console.WriteLine(string.Concat(e.Player.Name, " has been set to group ", set, "!"));
                        }
                    }
                    else
                    {
                        e.Player.SendErrorMessage(string.Concat(name, " might not be on a team, or their is no player by this name."));
                    }
                }
                else
                {
                    e.Player.SendErrorMessage(string.Concat("Try /removeteam [name]."));
                }
            }
        }
        private void JoinTeam(CommandArgs e)
        {
            string cmd = string.Empty;
            int index = 0;
            bool success = false;
            for (int i = 0; i < Teams.Length; i++)
            {
                string t = Teams[i];
                cmd = e.Message.Substring(9);
                int.TryParse(cmd, out index);
                if (GetPlayerTeam(e.Player.Name) == 0)
                {
                    if (t.ToLower().Contains(cmd.ToLower()))
                        success = SetPlayerTeam(e.Player.Name, GetTeamIndex(cmd));
                    else if (index != 0 && index == GetTeamIndex(t))
                    {
                        success = SetPlayerTeam(e.Player.Name, index);
                    }
                    if (success)
                    {
                        e.Player.SendSuccessMessage(string.Concat("Joining ", t, " has succeeded."));
                        string set = Groups[i];
                        if (TShock.Groups.GroupExists(set))
                        {
                            e.Player.Group = TShock.Groups.GetGroupByName(set);
                            Console.WriteLine(string.Concat(e.Player.Name, " has been set to group ", set, "!"));
                        }
                        return;
                    }
                }
            }
            e.Player.SendErrorMessage(string.Concat("Chances are you are already on a team or this team's roster is full."));
        }
        private int GetTeamIndex(string team)
        {
            for (int j = 0; j < Teams.Length; j++)
            {
                if (Teams[j].ToLower().Contains(team.ToLower()))
                    return j;
            }
            return 0;
        }
        private void SetTeam(int who, int team)
        {
            Main.player[who].team = team;
            TShock.Players[who].SetTeam(team);
            NetMessage.SendData((int)PacketTypes.PlayerTeam, -1, -1, null, who, team);
            NetMessage.SendData((int)PacketTypes.PlayerTeam, who, -1, null, who, team);
            TShock.Players[who].SendData(PacketTypes.PlayerTeam, "", who, team);
        }
        private int GetPlayerTeam(string name)
        {
            for (int i = 0; i < Teams.Length; i++)
            {
                var block = data.GetBlock(Teams[i]);
                foreach (string t in block.Contents)
                {
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        if (block.Value(t).ToLower() == name.ToLower())
                        {
                            return i;
                        }
                    }
                }
            }
            return 0;
        }
        private bool SetPlayerTeam(string name, int team)
        {
            var block = data.GetBlock(Teams[team]);
            foreach (string t in block.Contents)
            {
                if (!string.IsNullOrWhiteSpace(t))
                {
                    if (block.Value(t) == Empty)
                    {
                        RemoveFromTeam(name);
                        block.WriteValue(block.Key(t), name);
                        SetTeam(FromName(name).whoAmI, team);
                        return true;
                    }
                }
            }
            return false;
        }
        private bool RemoveFromTeam(string name)
        {
            for (int i = 0; i < Teams.Length; i++)
            {
                var block = data.GetBlock(Teams[i]);
                foreach (string t in block.Contents)
                {
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        if (block.Value(t).ToLower() == name.ToLower())
                        {
                            block.WriteValue(block.Key(t), Empty);
                            SetTeam(FromName(name).whoAmI, 0);
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        private Player FromName(string name)
        {
            for (int i = 0; i < Main.player.Length; i++)
            {
                if (Main.player[i].name.ToLower() == name.ToLower())
                    return Main.player[i];
            }
            return Main.player[255];
        }
    }
}
