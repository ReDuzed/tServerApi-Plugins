﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using TShockAPI;
using TerrariaApi.Server;
using RUDD.Dotnet;

namespace ItemClasses
{
    [ApiVersion(2,1)]
    public class Plugin : TerrariaPlugin
    {
        public override string Name
        {
            get { return "Item Classes"; }
        }
        public override Version Version
        {
            get { return new Version(1, 0, 1); }
        }
        public override string Author
        {
            get { return "Duze"; }
        }
        public override string Description
        {
            get { return "Players can choose between any number of predefined classes."; }
        }
        private DataStore data;
        private Ini ini;
        private const int 
            None = 0,
            Ranged = 1,
            Melee = 2,
            Mage = 3,
            SSCReset = 4;
        partial class ClassID
        {
            public const string None = "None", Ranged = "Ranged", Melee = "Melee", Mage = "Mage";
            public static string[] Array = new string[4];
        }
        private bool[] choseClass = new bool[256];
        private string[] itemSet = new string[4];
        private bool removeClass, canChoose = true;
        private const string Roster = "Roster", Key = "names";
        public Plugin(Main game) : base(game)
        {
            Action<Command> add = delegate(Command cmd)
            {
                Commands.ChatCommands.Add(cmd);
            };
            add(new Command("classes.user.choose", ChooseClass, "chooseclass") { HelpText = "" });
            add(new Command("classes.admin.reset", ResetAll, "resetall") { HelpText = "" });
            add(new Command("classes.admin.reset.opt", ResetOption, "resetopt") { HelpText = "" });
            add(new Command("classes.admin.reload", Reload, "reload") { HelpText = "" });
            add(new Command("classes.admin.reset.logout", delegate(CommandArgs e)
            {
                removeClass = !removeClass;
                e.Player.SendSuccessMessage("Player that log out have their class type removed: [" + removeClass + "]");
            }, "classlogout") { HelpText = "" });
            add(new Command("classes.admin.opt", delegate(CommandArgs e)
            {
                canChoose = !canChoose;
                e.Player.SendSuccessMessage("Players able to choose classes: [" + canChoose + "]");
            }, "canchoose") { HelpText = "" });
        }
        public override void Initialize()
        {
            data = new DataStore("config\\player_class_data");
            ini = new Ini()
            {
                setting = new string[] { "SSCReset" },
                path = "config\\class_data" + Ini.ext
            };
            Reload(new CommandArgs("", TSPlayer.All, null));
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        }
         protected override void Dispose(bool disposing)
        {
            data.WriteToFile();
            if (disposing)
            {
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            }
            base.Dispose();
        }
        private void ChooseClass(CommandArgs e)
        {
            if (!canChoose)
            {
                e.Player.SendErrorMessage("Class selection has currently been disabled.");
                return;
            }
            if (!TShockAPI.TShock.ServerSideCharacterConfig.Enabled)
            {
                e.Player.SendErrorMessage("SSC is not enabled, therefore class choosing is also not enabled.");
                return;
            }
            string classes = "";
            for (int i = 0; i < ClassID.Array.Length; i++)
            {
                classes += ClassID.Array[i] + " ";
            }
            if (e.Message.Contains(" "))
            {
                string userName = e.TPlayer.name;
                string param = e.Message.Substring(e.Message.IndexOf(" ") + 1).ToLower();
                if (data.GetBlock(userName).GetValue("class") != "0")
                {
                    e.Player.SendErrorMessage("The character class designation has already occurred.");
                    return;
                }
                if (ClassSet(param) == -1)
                {
                    e.Player.SendErrorMessage("There is no such class. Try '/chooseclass [c/FFFF00:'" + classes.TrimEnd(' ') + "'] instead.");
                    return;
                }
                for (int i = 0; i < NetItem.InventorySlots; i++)
                {
                    UpdateItem(e.TPlayer.inventory[i], i, e.Player.Index, false, 0);
                }
                if (itemSet[ClassSet(param)].Length > 0)
                {
                    int index;
                    if ((index = ClassSet(param)) >= 0)
                    {
                        string[] array = itemSet[index].Trim(' ').Split(',');
                        for (int j = 0; j < array.Length; j++)
                        {
                            int type;
                            #region Works | good formatting
                            /*
                            for (int n = 0; n < array[j].Length; n++)
                            {
                                if (array[j].Substring(n, 1) == "s")
                                {
                                    int.TryParse(array[j].Substring(n + 1), out type);
                                    int.TryParse(array[j].Substring(0, n), out stack);
                                    e.Player.GiveItem(type, stack);
                                    continue;
                                }
                                else if (array[j].Substring(n, 1) == "p")
                                {
                                    int.TryParse(array[j].Substring(n + 1), out type);
                                    int.TryParse(array[j].Substring(0, n), out prefix);
                                    e.Player.GiveItem(type, 1, prefix);
                                    continue;
                                }
                            }
                            int.TryParse(array[j], out type);
                            e.Player.GiveItem(type, 1);*/
                            #endregion
                            #region Tried & works | bad formatting
                            if (int.TryParse(array[j], out type))
                            {
                                int stack = j + 1;
                                if (stack < array.Length)
                                {
                                    if (array[stack].StartsWith("s"))
                                    {
                                        j++;
                                        if (int.TryParse(array[stack].Substring(1), out stack))
                                        {
                                            e.Player.GiveItem(type, stack);
                                            continue;
                                        }
                                        else
                                        {
                                            e.Player.GiveItem(type, 1);
                                            continue;
                                        }
                                    }
                                }
                                int prefix = j + 1;
                                if (prefix < array.Length)
                                {
                                    if (array[prefix].StartsWith("p"))
                                    {
                                        j++;
                                        if (int.TryParse(array[prefix].Substring(1), out prefix))
                                        {
                                            e.Player.GiveItem(type, 1, prefix);
                                            continue;
                                        }
                                        else
                                        {   
                                            e.Player.GiveItem(type, 1);
                                            continue;
                                        }
                                    }
                                }
                                e.Player.GiveItem(type, 1);
                            }
                            #endregion
                        }
                    }
                }
                data.GetBlock(userName).WriteValue("class", param);
                e.Player.SendSuccessMessage(ClassID.Array[ClassSet(param)] + " class chosen!");
                return;
            }
            e.Player.SendErrorMessage("Try '/chooseclass [c/FFFF00:'" + classes.TrimEnd(' ') + "'] instead.");
        }
        
        private void Reload(CommandArgs e)
        {
            if (!File.Exists(ini.path))
            {
                ini.WriteFile(new string[] { "False" });            
            }
            if (e.Message.Contains(" "))
            {
                string cmd = e.Message.Substring(e.Message.IndexOf(" "));
                if (cmd.Contains("add"))
                {
                    string sub = e.Message.Substring(e.Message.LastIndexOf(" ") + 1);
                    ini.AddSetting(sub);
                    e.Player.SendSuccessMessage(sub + " added to the class listing.");
                }
            }
            string[] array = ini.ReadFile();
            
            //string choose = "";
            //Ini.TryParse(array[0], out choose);
            //bool.TryParse(choose, out canChoose);

            if (array.Length <= 1)
                return;
            itemSet = new string[array.Length];
            ClassID.Array = new string[itemSet.Length];
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i].Contains('='))
                {
                    Ini.TryParse(array[i], out itemSet[i - 1]);
                    ClassID.Array[i - 1] = array[i].Substring(0, array[i].IndexOf('='));
                }
            }
            try
            {
                if (e.TPlayer.whoAmI == 255)
                        Console.WriteLine("[PlayerClasses] Successfully reloaded the INI.");
                    else
                        e.Player.SendSuccessMessage("[c/FF0000:PlayerClasses] Successfully reloaded the INI.");
            }
            catch
            {
                return;
            }
        }
        private void ResetOption(CommandArgs e)
        {
            if (e.Message.Contains(" "))
            {
                string userName = e.Message.Substring(e.Message.IndexOf(" ") + 1);
                string[] array = data.GetBlock(Roster).GetValue(Key).Split(';');
                for (int i = 0; i < array.Length; i++)
                {
                    if (data.BlockExists(array[i]) && userName.ToLower() == array[i].ToLower())
                    {
                        data.GetBlock(array[i]).WriteValue("class", "0");
                        e.Player.SendSuccessMessage(array[i] + " has had their class removed.");
                        return;
                    }
                }
            }
            e.Player.SendErrorMessage("Try '/resetopt <user name>' instead.");
        }
        private void OnJoin(JoinEventArgs e)
        {
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
            
            Block user;
            if (!data.BlockExists(userName))
            {
                user = data.NewBlock(new string[] 
                { 
                    "class"
                }, userName);    
            }
        }
        private void ResetAll(CommandArgs e)
        {
            Block roster = data.GetBlock(Roster);
            string[] array = roster.GetValue(Key).Split(';');
            string list = " ";
            if (array.Length > 0)
            {
                for (int i = 1; i < array.Length; i++)
                {
                    if (data.BlockExists(array[i]))
                    {
                        data.GetBlock(array[i]).WriteValue("class", "0");
                        list += array[i] + " ";
                    }
                }
            }
            e.Player.SendSuccessMessage("The users:" + list + "have had their classes removed.");
        }
        private void OnLeave(LeaveEventArgs e)
        {
            if (removeClass)
            {
                Block user = data.GetBlock(TShock.Players[e.Who].Name);
                user.WriteValue("class", "0");
            }
        }
        private string Class(int index)
        {
            return ClassID.Array[index].ToLower();
        }
        private int ClassSet(string param)
        {
            if (ClassID.Array == null || ClassID.Array.Length == 0)
                return -1;
            for (int i = 0; i < ClassID.Array.Length; i++)
            {
                if (param.ToLower() == ClassID.Array[i].ToLower())
                    return i;
            }
            return -1;
        }
        private int IndexTotal()
        {
            return NetItem.InventorySlots + NetItem.MiscDyeSlots + NetItem.MiscEquipSlots + NetItem.ArmorSlots + NetItem.DyeSlots + NetItem.TrashSlots;
        }
        private void UpdateItem(Item item, int slot, int who, bool remove = false, int stack = 1)
        {
            if (remove)
            {
                item.active = false;
                item.type = 0;
                item.netDefaults(0);
            }
            item.stack = stack;
            NetMessage.SendData((int)PacketTypes.PlayerSlot, -1, -1, NetworkText.FromLiteral(item.Name), who, slot, item.prefix);
            NetMessage.SendData((int)PacketTypes.PlayerSlot, who, -1, NetworkText.FromLiteral(item.Name), who, slot, item.prefix);
        }
    }
}
