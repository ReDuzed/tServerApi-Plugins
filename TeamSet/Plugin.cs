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
            get { return new string[] { "None", "Red Team", "Green Team", "Blue Team", "Yellow Team", "Purple Team" }; }
        }
        private const string Empty = "0";
        private bool kickOnSwitch;
        private DataStore data;
        private Command command;
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
        public override void Initialize()
        {
            Ini ini = new Ini()
            {
                setting = new string[] { "playersperteam", "kickonswitch" },
                path = "config\\team_data" + Ini.ext
            };
            int total = 0;
            if (!File.Exists(ini.path))
                ini.WriteFile(new object[] { 4, false });
            
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
            }
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
                Commands.ChatCommands.Add(command = new Command("teamset.admin.set", PlaceTeam, new string[] { "placeteam", "removeteam" })
                {
                    HelpText = "For placing or removing players from teams."
                });
                Commands.ChatCommands.Add(command = new Command("teamset.join", JoinTeam, new string[] { "jointeam" })
                {
                    HelpText = "Allows players to join a team if they aren't on one already."
                });
            }
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
            foreach (string t in Teams)
            {
                cmd = e.Message.Substring(9);
                int.TryParse(cmd, out index);
                if (GetPlayerTeam(e.Player.Name) == 0)
                {
                    if (cmd.ToLower() == t.ToLower())
                        success = SetPlayerTeam(e.Player.Name, GetTeamIndex(cmd));
                    else if (index != 0 && index == GetTeamIndex(t))
                    {
                        success = SetPlayerTeam(e.Player.Name, index);
                    }
                    if (success)
                    {
                        e.Player.SendSuccessMessage(string.Concat("Joining ", t, " has succeeded."));
                    }
                    else
                    {
                        e.Player.SendErrorMessage(string.Concat(t, " might be full."));
                    }
                }
            }
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
