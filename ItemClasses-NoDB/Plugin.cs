using System;
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
        private Ini ini;
        private const int 
            None = 0,
            Ranged = 1,
            Melee = 2,
            Mage = 3,
            SSCReset = 4;
        private const string none = "None", red = "Red", green = "Green", blue = "Blue", yellow = "Yellow", pink = "Pink";
        partial class ClassID
        {
            public const string None = "None", Ranged = "Ranged", Melee = "Melee", Mage = "Mage";
            public static string[] Array = new string[4];
        }
        private bool[] choseClass = new bool[256];
        private string[] itemSet = new string[4];
        private bool removeClass, canChoose = true;
        private bool[] hasChosenClass = new bool[256];
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
            add(new Command("classes.admin.start", Start, "match"));
        }
        public override void Initialize()
        {
            ini = new Ini()
            {
                path = "config\\class_data" + Ini.ext
            };
            Reload(new CommandArgs(string.Empty, TSPlayer.Server, null));
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
        }
         protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
            }
            base.Dispose();
        }
        private int ticks;
        private bool preMatchChoose;
        private void Start(CommandArgs e)
        {
            Action error = delegate()
            {
                e.Player.SendErrorMessage("Try using [c/FFFF00:/match class <# of seconds>] to set a countdown for players to choose a class.");
            };
            if (e.Message.Contains(" "))
            {
                string sub = e.Message.Substring(e.Message.IndexOf(" ") + 1);
                if (sub.StartsWith("class") && sub.Contains(" "))
                {
                    int.TryParse(sub.Substring(sub.IndexOf(" ") + 1), out ticks);
                    ticks = Math.Max(ticks, 60);
                    preMatchChoose = true;
                    TShockAPI.TSPlayer.All.SendInfoMessage("You have [c/FF00FF:" + ticks + " seconds] to choose a class before one is auto-assigned to you");
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
        private void OnUpdate(EventArgs e)
        {
            if (preMatchChoose)
            {
                if ((int)Main.time % 60 == 0)
                    ticks--;
                if (ticks < 0)
                {
                    foreach (TSPlayer p in TShock.Players)
                    {
                        if (p != null && p.Active && !p.Dead)
                        {
                            ChooseClass(new CommandArgs("chooseclass " + ClassID.Array[Main.rand.Next(0, ClassID.Array.Length - 1)], p, null));
                        }
                    }
                    preMatchChoose = false;
                }
            }
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
                string param = e.Message.Substring(e.Message.IndexOf(" ") + 1).ToLower().Trim(' ');
                if (/*data.GetBlock(userName).GetValue("class") != "0"*/ hasChosenClass[e.Player.Index])
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
                hasChosenClass[e.Player.Index] = true;
                e.Player.SendSuccessMessage(ClassID.Array[ClassSet(param)] + " class chosen!");
                return;
            }
            e.Player.SendErrorMessage("Try '/chooseclass [c/FFFF00:'" + classes.TrimEnd(' ') + "'] instead.");
        }
        private void Reload(CommandArgs e)
        {
            if (!File.Exists(ini.path))
            {
                ini.WriteFile(null);            
            }
            string[] array = ini.ReadFile();
            
            //string choose = "";
            //Ini.TryParse(array[0], out choose);
            //bool.TryParse(choose, out canChoose);
            
            if (array.Length == 0)
                return;
            itemSet = new string[array.Length];
            ClassID.Array = new string[itemSet.Length];
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].Contains('='))
                {
                    Ini.TryParse(array[i], out itemSet[i]);
                    ClassID.Array[i] = array[i].Substring(0, array[i].IndexOf('='));
                }
            }
            if (e.TPlayer == TShockAPI.TSPlayer.Server.TPlayer)
                Console.WriteLine("[PlayerClasses] Successfully reloaded the INI.");
            else e.Player.SendSuccessMessage("[c/FF0000:PlayerClasses] Successfully reloaded the INI.");
        }
        private void ResetOption(CommandArgs e)
        {
            if (e.Message.Contains(" "))
            {
                string userName = e.Message.Substring(e.Message.IndexOf(" ") + 1);
                TSPlayer player = Util.FindPlayer(userName);
                hasChosenClass[Util.FindPlayer(userName).Index] = false;
                e.Player.SendSuccessMessage(player.Name + " has had their class removed.");
            }
            e.Player.SendErrorMessage("Try '/resetopt <user name>' instead.");
        }
        private void OnJoin(JoinEventArgs e)
        {
            hasChosenClass[e.Who] = false;
        }
        private void OnLeave(LeaveEventArgs e)
        {
            hasChosenClass[e.Who] = false;
        }
        private void ResetAll(CommandArgs e)
        {
            string list = " ";
            for (int i = 0; i < hasChosenClass.Length; i++)
            {
                hasChosenClass[i] = false;
            }
            foreach (TSPlayer p in TShock.Players)
            {
                if (p != null & p.Active)
                    list += p.Name + " ";
            }
            e.Player.SendSuccessMessage("The users:" + list + "have had their classes removed.");
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
