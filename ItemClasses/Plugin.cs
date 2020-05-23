using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;
using TerrariaApi;
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
            get { return new Version(0, 1); }
        }
        public override string Author
        {
            get { return "Duze"; }
        }
        public override string Description
        {
            get { return ""; }
        }
        private DataStore data;
        private Ini ini;
        private const int 
            None = 0,
            Ranged = 1,
            Melee = 2,
            Mage = 3,
            SSCReset = 4;
        class ClassID
        {
            public const string None = "None", Ranged = "Ranged", Melee = "Melee", Mage = "Mage";
            public static string[] Array
            {
                get { return new string[] { None, Ranged, Melee, Mage }; }
            }
        }
        private bool[] choseClass = new bool[256];
        private string[] itemSet = new string[4];
        private bool removeClass, canChoose;
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
                    e.Player.SendErrorMessage("There is no such class. Try '/chooseclass <none | ranged | melee | mage>' instead.");
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
                            if (int.TryParse(array[j], out type))
                            {
                                e.Player.GiveItem(type, 1);
                            }
                        }
                    }
                }
                data.GetBlock(userName).WriteValue("class", param);
                e.Player.SendSuccessMessage(ClassID.Array[ClassSet(param)] + " class chosen!");
                return;
            }
            e.Player.SendErrorMessage("Try '/chooseclass <none | ranged | melee | mage>' instead.");
        }
        
        private void Reload(CommandArgs e)
        {
            ini = new Ini()
            {
                setting = new string[] { ClassID.None, ClassID.Ranged, ClassID.Melee, ClassID.Mage, "SSCReset" },
                path = "config\\class_data" + Ini.ext
            };
            if (!File.Exists(ini.path))
            {
                ini.WriteFile(new string[] { "0", "0", "0", "0", "False" });            
            }
            string[] array = ini.ReadFile();
            for (int i = 0; i < itemSet.Length; i++)
            {
                Ini.TryParse(array[i], out itemSet[i]);
            }
            if (e.TPlayer.whoAmI == 255)
                    Console.WriteLine("[PlayerClasses] Successfully reloaded the INI.");
                else
                    e.Player.SendSuccessMessage("[c/FF0000:PlayerClasses] Successfully reloaded the INI.");
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
