using System;
using System.IO;
using System.Linq;
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


namespace teamsetqueue
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
        partial class TeamID
        {
            public const int None = 0, Red = 1, Green = 2, Blue = 3, Yellow = 4, Pink = 5;
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
        private Block spawn;
        private bool freeJoin;
        private int total;
        private bool kickOnLeave;
        private bool teamSpawn;
        const string Roster = "Roster";
        const string Key = "names";
        public override string Name
        {
            get { return "Team Set Queue"; }
        }
        public override string Author
        {
            get { return "Duze"; }
        }
        public override Version Version
        {
            get { return new Version(2, 1, 0, 0); }
        }
        public override string Description
        {
            get { return "Places players into teams on server join after they have been set to a team by an admin, queue edition."; }
        }
        public Plugin(Main game) : base(game)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[Team Set] Be sure to set the database using the chat command: '/database init' before proper use.");
        }
        private void Reload()
        {
            if (!Directory.Exists("config"))
                Directory.CreateDirectory("config");
            
            Ini ini = new Ini()
            {
                setting = new string[] { "playersperteam", "kickonswitch", "teamfreejoin", "kickonleave", "enableteamspawn" },
                path = "config\\team_data" + Ini.ext
            };
            total = 0;
            if (!File.Exists(ini.path))
                ini.WriteFile(new object[] { 4, false, false, false, false });
            
            string t = string.Empty;
            string kick = string.Empty;
            string free = string.Empty;
            string leave = string.Empty;
            string tspawn = string.Empty;
            var file = ini.ReadFile();
            if (file.Length > 0)
            {
                Ini.TryParse(file[0], out t);
                Ini.TryParse(file[1], out kick);
                Ini.TryParse(file[2], out free);
                Ini.TryParse(file[3], out leave);
                Ini.TryParse(file[4], out tspawn);
            }
            bool.TryParse(kick, out kickOnSwitch);
            int.TryParse(t, out total);
            bool.TryParse(free, out freeJoin);
            bool.TryParse(leave, out kickOnLeave);
            bool.TryParse(tspawn, out teamSpawn);
        }
        
        public override void Initialize()
        {   
            data = new DataStore("config\\team_data");
            Reload();
            OnInit();
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                data.WriteToFile();
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            }
            base.Dispose(disposing);
        }
        private void OnJoin(JoinEventArgs e)
        {
            SetTeam(e.Who, GetPlayerTeam(Main.player[e.Who].name));

            Block roster;
            if (!data.BlockExists(Roster))
            {
                roster = data.NewBlock(new string[] { Key }, Roster);
            }
            else
            {
                roster = data.GetBlock(Roster);
            }
            string userName = TShock.Players[e.Who].Name;
            string list = roster.GetValue(Key);
            if (!list.Contains(userName))
                list += (";" + userName);
            roster.WriteValue(Key, list);
        }
        private void OnLeave(LeaveEventArgs e)
        {
            if (kickOnLeave)
            {
                RemoveFromTeam(TShock.Players[e.Who].Name);
            }
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
        private void OnInit()
        {
            if (command == null)
            {
                Commands.ChatCommands.Add(new Command("teamset.admin.set", PlaceTeam, new string[] { "placeteam", "removeteam" })
                {
                    HelpText = "For placing or removing players from teams.",
                    AllowServer = false
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
                    HelpText = "Modifies the associated group for the specified team color."
                });
                Commands.ChatCommands.Add(new Command("teamset.join", JoinTeam, new string[] { "jointeam" })
                {
                    HelpText = "Allows players to join a team if they aren't on one already.",
                    AllowServer = false
                });
                Commands.ChatCommands.Add(new Command("teamset.tp", TeamSpawn, new string[] { "tspawn" })
                {
                    HelpText = "Spawns player to their team-designated spawn point",
                    AllowServer = false
                });
                Commands.ChatCommands.Add(new Command("teamset.admin.tp", SetSpawn, new string[] { "settspawn" })
                {
                    HelpText = "For admins to set team spawns",
                    AllowServer = false
                });
                Commands.ChatCommands.Add(new Command("teamset.admin", delegate(CommandArgs a)
                {
                    teamSpawn = !teamSpawn;
                    a.Player.SendSuccessMessage("Teams being permitted to use /tspawn is [" + teamSpawn + "].");
                }, "teamspawn")
                {
                    HelpText = "Toggles whether players can use /tspawn to go to team spawn locations."
                });
                Commands.ChatCommands.Add(new Command("teamset.help", delegate(CommandArgs a)
                {
                    a.Player.SendInfoMessage(string.Format("{0} <index | color>, {1} <name>\n{2}\n{3} automates team group creation parented to group default\n{4} <team color> <group>\n{5} <color | index>\n{6} team spawn switch\n{7} <color> places spawn at your current position\n{8} teleports to team spawn\n{9} switches player on leave being removed from team\n{10} <<1-5> <1-5> [<1-5>]...> use 2 or more team indices to autosort into said teams",
                                            "/placeteam", "/removeteam", "/reload", "/teamgroups", "/teamset", "/jointeam", "/tspawn", "/settspawn", "/teamspawn", "/teamleavekick", "/autosort"));
                }, "teamsethelp")
                {
                    HelpText = "Toggles whether players can use /tspawn to go to team spawn locations."
                });
                //  Kicking a player via /kick removed them from their team with this flag set.
                Commands.ChatCommands.Add(command = new Command("teamset.admin", delegate(CommandArgs a)
                {
                    kickOnLeave = !kickOnLeave;
                    a.Player.SendSuccessMessage("Players that leave are removed from their designated team [" + kickOnLeave + "].");
                }, "teamleavekick")
                {
                    HelpText = "Toggles whether players leaving should kick them off their teams."
                });
                Commands.ChatCommands.Add(new Command("teamset.superadmin.db", MakeDataBase, "database")
                {
                    HelpText = "Makes the database with which to store maximum player per team only to be used after the INI file is manually set up"
                });
                Commands.ChatCommands.Add(new Command("teamset.admin.sort", AutoSort, "autosort")
                {
                    HelpText = "Begins automatically sorting players into the teams that have the least players through use of team indices."
                });
            }
        }
        private void AutoSort(CommandArgs e)
        {
            Action error = delegate(){
                e.Player.SendErrorMessage(string.Format("Use as many team indices as you want teams to be sorted into, 1:{0}, 2:{1}, 3:{2}, 4:{3}, 5:{4}.", Teams[1], Teams[2], Teams[3], Teams[4], Teams[5]));
            };
            if (e.Message.Contains(" "))
            {
                string sub = e.Message.Substring(e.Message.IndexOf(" ") + 1);
                if (sub.Contains(" "))
                {
                    int[] num = new int[6];
                    bool[] valid = new bool[6];
                    string[] array = sub.Split(' ');
                    for (int i = 0; i < array.Length; i++)
                    {
                        int index = 0;
                        int count = 0;
                        if (int.TryParse(array[i], out index))
                        {
                            valid[index] = true;
                            if (!TeamFull(index, out count))
                            {
                                num[index] = count;
                            }
                        }
                        else
                        {
                            error();
                        }
                    }
                    Block roster = data.GetBlock(Roster);
                    string[] list = roster.GetValue(Key).Split(';');
                    for (int i = 1; i < list.Length; i++)
                    {
                        int teamIndex = 0;
                        int previous = total;
                        for (int j = 0; j < num.Length; j++)
                        {
                            if (num[j] < previous && valid[j])
                            {
                                previous = num[j];
                                teamIndex = j;
                            }
                        }
                        foreach (TSPlayer p in TShock.Players)
                        {
                            if (p != null && p.Active && p.Name == list[i])
                            {
                                JoinTeam(new CommandArgs("jointeam " + informal[teamIndex], p, null)); 
                                num[teamIndex]++;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    error();
                }
            }
            else
            {
                error();
            }
        }
        private bool TeamFull(int index, out int count)
        {
            string[] array = new string[total];
            Block block = data.GetBlock(Teams[index]);
            for (int i = 0; i < array.Length; i++)
            {
                if (block.GetValue("players" + i + 1) == "0")
                {
                    count = i;
                    return false;
                }
            }
            count = 0;
            return true;
        }
        private void MakeDataBase(CommandArgs e)
        {
            if (e.Message.Contains(" "))
            {
                string sub = e.Message.Substring(e.Message.IndexOf(" ") + 1);
                if (sub.StartsWith("reset"))
                {
                    data.Dispose(true);
                    e.Player.SendSuccessMessage("The database has been cleared. Please run [c/FF0000:/database init <max # per team>.]");
                    return;
                }
                if (sub.StartsWith("init"))
                {
                    string[] Slots = new string[] {};
                    Action<int> num = delegate(int count)
                    {
                        total = Math.Max(count, 2);
                        Slots = new string[total];
                        for (int i = 0; i < total; i++)
                            Slots[i] = "players" + (i + 1);
                        e.Player.SendSuccessMessage("Max spots per team has been set to: [/cFFFF00: " + total + "].");
                    };
                    int t;
                    if (!sub.Contains(" "))
                    {
                        num(total);
                    }
                    else if (int.TryParse(sub.Substring(sub.IndexOf(" ") + 1), out t))
                    {
                        num(t);
                    }
                    else
                    {
                        e.Player.SendErrorMessage("Specify total max players per team: [c/FFFF00:/database init <#>], or leave the # out which defaults to config data.");
                        return;
                    }
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
                    if (!data.BlockExists("spawns"))
                    {
                        spawn = data.NewBlock(keys, "spawns");
                    }
                    else 
                    {
                        spawn = data.GetBlock("spawns");
                    }
                    e.Player.SendSuccessMessage("Database initializing complete.");
                }
            }
        }
        private void TeamSpawn(CommandArgs e)
        {
            if (teamSpawn)
            {
                TeamTeleport(e.Player.Name, e.Player.Index);
            }
            else e.Player.SendInfoMessage("Team spawn points are disabled.");
        }
        private void TeamTeleport(string name, int whoAmI)
        {
            string team = informal[GetPlayerTeam(name)];
            string[] s = spawn.GetValue(team).Split('x');
            string sX = s[0];
            string sY = s[1];
            float x, y;
            float.TryParse(sX, out x);
            float.TryParse(sY, out y);
            TShock.Players[whoAmI].Teleport(x, y);
            TShock.Players[whoAmI].SendSuccessMessage(string.Format("You have been sent to {0}'s spawn at {1}:{2}.", team, x, y));
        }
        private void SetSpawn(CommandArgs e)
        {
            Vector2 v2 = new Vector2((float)Math.Round(e.TPlayer.position.X, 0), (float)Math.Round(e.TPlayer.position.Y, 0));
            if (e.Message.Contains(" "))
            {
                string team = e.Message.Substring(e.Message.IndexOf(" ") + 1).ToLower();
                for (int i = 0; i < informal.Length; i++)
                {
                    if (informal[i] == team)
                    {
                        spawn.WriteValue(team, string.Concat(v2.X, "x", v2.Y));
                        break;
                    }
                    if (i == informal.Length - 1)
                    {
                        e.Player.SendErrorMessage(string.Concat(team, " is not an existing team. Only the name of the color of the team is required."));
                        return;
                    }
                }
                e.Player.SendSuccessMessage(string.Format("{0} team spawn set at {1}X {2}Y.", team, v2.X, v2.Y));
            }
            else
            {
                e.Player.SendErrorMessage("The command format is /settspawn <team color>.");
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
                if (GetPlayerTeam(e.Player.Name) == 0 || freeJoin)
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
            //TShock.Players[who].SetTeam(team);
            NetMessage.SendData((int)PacketTypes.PlayerTeam, -1, -1, null, who, team);
            NetMessage.SendData((int)PacketTypes.PlayerTeam, who, -1, null, who, team);
            //TShock.Players[who].SendData(PacketTypes.PlayerTeam, "", who, team);
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
