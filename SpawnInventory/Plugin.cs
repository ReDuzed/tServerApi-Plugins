using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using TShockAPI;
using TShockAPI.Hooks;
using TerrariaApi.Server;
using RUDD;
using RUDD.Dotnet;

namespace inv_start
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        private IList<int> item = new List<int>();
        private IList<InvPlayer> invp = new List<InvPlayer>();
        private bool enabled = true;
        private Command opt;
        private Command set;
        private readonly string config = ".\\tshock\\inv_config.ini";
        private bool dropOpt;
        private bool shopOpt = true;
        private bool[] buyBack = new bool[256];
        public static Plugin Instance;
        public override string Name
        {
            get { return "Civic Core"; }
        }
        public override string Author
        {
            get { return "Duze"; }
        }
        public override string Description
        {   
            get { return "When a player joins or respawns, their inventory is given a [set of] particular items, if missing. Drops on death, also, are only partial."; }
        }
        public override Version Version
        {
            get { return new Version(1, 0); }
        }
        public Plugin(Main game) : base(game)
        {
            Instance = this;
        }
        public override void Initialize()
        {
            InvPlayer.Initialize();
            if (!File.Exists(config))
            {
                var str = File.Create(".\\" + config);
                str.Close();
                str.Dispose();
            }
            using (StreamReader sr = new StreamReader(config))
            {
                string[] ids = sr.ReadToEnd().Split(' ');
                int i = 0;
                foreach (string s in ids)
                {
                    if (s.Length > 0)
                    if (int.TryParse(s, out i))
                        item.Add(i);
                }
            }
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerCommand.Register(this, OnCommand);
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        }
        protected override void Dispose(bool disposed)
        {
            if (disposed)
            {
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);  
                ServerApi.Hooks.ServerCommand.Deregister(this, OnCommand);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
                var i = new Ini()
                {
                    setting = InvPlayer.setting,
                    path = InvPlayer.path
                };
                i.WriteFile(new object[] { (int)InvPlayer.dropType, InvPlayer.value });
            }
            base.Dispose(disposed);
        }
        private void OnCommand(CommandEventArgs e)
        {
            if (!Commands.ChatCommands.Contains(opt))
            {
                Commands.ChatCommands.Add(opt = new Command("invset.admin.opt", InvOpt, new string[] { "invopt", "civiccore" })
                {
                    HelpText = "Changes whether or not " + Name + " settings enabled."
                });
                Commands.ChatCommands.Add(new Command("invset.buyback", AetherShop, new string[] { "aethershop", "aeshop" })
                {
                    HelpText = "A time limited shop where player's lost gear goes upon death."
                });
                Commands.ChatCommands.Add(set = new Command("invset.admin.setitems", ItemPool, new string[] { "additem", "removeitem", "listitems", "clearitems"})
                {
                    HelpText = Name + ": Modifies the item pool based on item IDs."
                });
            }
        }
        private void OnJoin(JoinEventArgs e)
        {
            invp.Add(new InvPlayer()
            {
                who = e.Who,
                death = false,
                justJoined = true
            });
        }
        private void OnLeave(LeaveEventArgs e)
        {
            foreach (var i in invp)
            {
                if (i.who == e.Who)
                {
                    invp.Remove(i);
                    break;
                }
            }
        }
        private void OnGetData(GetDataEventArgs e)
        {
            if (enabled && !e.Handled)
            {
                byte who = 255;
                if (e.MsgID == PacketTypes.PlayerDeathV2)
                {
                    using (BinaryReader br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                    {
                        who = br.ReadByte();
                        invp[who].death = true;
                    }
                    InvPlayer plr = null;
                    foreach (var i in invp)
                    {
                        if (i.who == (int)who)
                            plr = i;
                    }
                    if (plr == null)
                        return;
                    invp[who].aether.Clear();
                    plr.DropRand(who, dropOpt);
                }
                else if (who != 255 && e.MsgID == PacketTypes.ItemDrop)
                {
                    foreach (Item i in invp[who].keep)
                    foreach (Item j in Main.item)
                    {
                        if (j.IsTheSameAs(i))
                        {
                            j.active = false;
                            j.SetDefaults(0);
                            break;
                        }
                    }
                }
            }
        }
        private void OnUpdate(EventArgs e)
        {
            if (!enabled || invp.Count == 0)
                return;
            foreach (var i in invp)
            {
                if (i.death && !Main.player[i.who].dead)
                {
                    foreach (Item d in i.drop)
                    foreach (Item m in Main.item)
                    {
                        foreach (int n in this.item)
                        {
                            if (d.IsTheSameAs(m) && d.type == n && !IsGem(n))
                            {
                                m.active = false;
                                m.SetDefaults(0);
                                break;
                            }
                        }
                        break;
                    }
                    InvSet(i.who, item.ToArray());
                    i.death = false;
                    Revived(i);
                }
                int team = TShock.Players[i.who].Team;
                if (i.justJoined && team != 0)
                {
                    if (!HasGem(i.who))
                        InvSet(i.who, new int[] { GemType(team) });   
                    i.justJoined = false;
                }
            }
        }
        
        private void InvOpt(CommandArgs e)
        {
            if (e.Message.Contains("invopt"))
            {
                enabled = !enabled;
                e.Player.SendSuccessMessage("Players getting the item pool on joining or respawning has been " + (enabled ? "enabled" : "disabled" ) + ".");
            }
            if (e.Message.Contains("civiccore"))
            {
                dropOpt = !dropOpt;
                e.Player.SendSuccessMessage("Players dropping a portion of their gear on death " + (dropOpt ? "enabled" : "disabled" ) + ".");
            }
        }
        private void AetherShop(CommandArgs e)
        {
            if (!dropOpt || !shopOpt)
                return;
            string cmd = e.Message.Substring(e.Message.IndexOf(' ') + 1);
            string opt = string.Empty;
            if (cmd.Contains(' '))
                opt = cmd.Substring(cmd.IndexOf(' ') + 1);
            string text = string.Empty;
            int who = e.Player.Index;
            foreach (Item inv in Main.player[who].inventory)
            foreach (var ai in invp[who].aether)
            if (ai.item.IsTheSameAs(inv))
                ai.item.owner = who;
            switch (cmd.Contains(" ") ? cmd.Substring(0, cmd.IndexOf(" ")) : cmd)
            {
                case "list":
                    foreach (var ai in invp[who].aether)
                    {
                        if (ai.newOwner == who || !NotCoin(ai.item) || ai.newOwner != 255)
                        {
                            ai.unbuyable = true;
                            continue;
                        }
                        if (!string.IsNullOrEmpty(ai.item.Name))
                            text += string.Concat(ai.item.Name, " (", ai.item.value, " copper) ");
                    }
                    e.Player.SendInfoMessage(text == string.Empty ? "The items have expired or been reacquired." : text);
                    break;
                case "buy":
                    foreach (var ai in invp[who].aether)
                    {
                        if (ai.unbuyable || ai.newOwner == who || ai.newOwner != 255 || !NotCoin(ai.item))
                            continue;
                        invp[who].coinBank = CoinPurse.CoinInit(who);
                        buyBack[who] = true;
                        int value = ai.item.shopCustomPrice.HasValue ? ai.item.shopCustomPrice.Value : ai.item.value;
                        if (opt.ToLower().Replace(" ", "") == ai.item.Name.ToLower().Replace(" ", ""))
                        {
                            if (CoinPurse.ShopItem(who, value))
                            {
                                Item clone = ai.item.Clone();
                                foreach (Item i in Main.item)
                                {
                                    if (i.IsTheSameAs(ai.item))
                                    {
                                        i.active = false;
                                        i.SetDefaults(0);
                                        break;
                                    }
                                }
                                e.Player.GiveItem(clone.type, clone.Name, clone.width, clone.height, clone.stack, clone.prefix);
                                e.Player.SendSuccessMessage(string.Concat("Reacquiring ", ai.item.Name, " complete."));
                                invp[who].aether.Remove(ai);
                                break;
                            }
                            else 
                            {
                                e.Player.SendInfoMessage("The cost necessary for this item has not been acquired.");
                            }
                        }
                    }
                    break;
                default:
                    break;
            }
        }
        private void ItemPool(CommandArgs e)
        {
            if (!enabled)
                return;
            string command = e.Message.Contains(" ") ? e.Message.Substring(0, e.Message.IndexOf(' ')) : e.Message;
            int type = 0;
            switch (command)
            {
                case "additem":
                    type = 0;
                    if (int.TryParse(e.Message.Substring(e.Message.IndexOf(" ") + 1), out type))
                    {
                        item.Add(type);
                        e.Player.SendSuccessMessage("Item number " + type + " added to the pool.");
                        ConfigWrite(type.ToString());
                    }
                    else e.Player.SendErrorMessage("Could not add item " + type + ".");
                    break;
                case "removeitem":
                    type = 0;
                    if (int.TryParse(e.Message.Substring(e.Message.IndexOf(" ") + 1), out type))
                    {
                        if (!item.Contains(type))
                        {
                            e.Player.SendInfoMessage("Item " + type + " is already not in the pool.");
                            return;
                        }
                        item.Remove(type);
                        e.Player.SendSuccessMessage("Item number " + type + " removed from the pool.");
                        ConfigWrite(type.ToString(), true);
                    }
                    else e.Player.SendErrorMessage("Could not remove item " + type + ".");
                    break;
                case "listitems":
                    string text = string.Empty;
                    foreach (int i in item)
                        text += i + " ";
                    e.Player.SendSuccessMessage(text);
                    break;
                case "clearitems":
                    item.Clear();
                    e.Player.SendSuccessMessage("All items have been cleared from the pool.");
                    ConfigWrite(type.ToString(), false, true);
                    break;
            }
        }
        private void Revived(InvPlayer i)
        {
            if (dropOpt)
            {
                if (TShock.Players[i.who].Difficulty != 0)
                {
                    foreach (Item m in i.keep)
                        TShock.Players[i.who].GiveItem(m.type, m.Name, m.width, m.height, m.stack, m.prefix);
                }
                i.Clear();
            }
        }
        private void ConfigWrite(string text, bool remove = false, bool clear = false, bool append = true)
        {
            if (clear)
            {
                using (StreamWriter sw = new StreamWriter(config))
                    sw.Write("");
                return;
            }
            if (remove)
            {
                string t = string.Empty;
                using (StreamReader sr = new StreamReader(config))
                    t = sr.ReadToEnd(); 
                t = t.Replace(text + " ", "");
                using (StreamWriter sw = new StreamWriter(config))
                    sw.Write(t);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(text))
                using (StreamWriter sw = new StreamWriter(config, append))
                    sw.Write(text + " ");
            }
        }
        private int GemType(int team)
        {
            int type = 0;
            switch (team)
            {
                case (int)TeamID.Red:
                    type = ItemID.LargeRuby;
                    break;
                case (int)TeamID.Green:
                    type = ItemID.LargeEmerald;
                    break;
                case (int)TeamID.Blue:
                    type = ItemID.LargeSapphire;
                    break;
                case (int)TeamID.Yellow:
                    type = ItemID.LargeTopaz;
                    break;
                case (int)TeamID.Purple:
                    type = ItemID.LargeAmethyst;
                    break;
                default:
                    break;
            }
            return type;
        }
        private bool IsGem(int type)
        {
            return type == ItemID.LargeRuby || type == ItemID.LargeEmerald || type == ItemID.LargeSapphire || type == ItemID.LargeTopaz || type == ItemID.LargeAmethyst;
        }
        public static bool NotCoin(Item item)
        {
            return item.type != ItemID.CopperCoin && item.type != ItemID.SilverCoin && item.type != ItemID.GoldCoin && item.type != ItemID.PlatinumCoin;
        }
        public static bool NotCoin(int type)
        {
            return type != ItemID.CopperCoin && type != ItemID.SilverCoin && type != ItemID.GoldCoin && type != ItemID.PlatinumCoin;
        }
        private bool HasGem(int who)
        {
            int gemType = GemType(TShock.Players[who].Team);
            Player player = Main.player[who];
            for (int i = 0; i < player.inventory.Length; i++)
            {
                if (player.inventory[i].type == gemType)
                {
                    return true;
                }
            }
            return false;
        }
        private void InvSet(int who, int[] types)
        {
            bool haveGem = HasGem(who);
            Player player = Main.player[who];
            int team = TShock.Players[who].Team;
            foreach (int i in types)
            {
                if (i == GemType(team) && !haveGem)
                {
                    TShock.Players[who].GiveItem(i, "", player.width, player.height, 1);
                    haveGem = true;
                }
                if (!IsGem(i) && !HasItem(i, who))
                {
                    TShock.Players[who].GiveItem(i, "", player.width, player.height, 1);
                }
            }
        }
        private bool HasItem(int type, int who)
        {
            foreach (Item item in Main.player[who].inventory)
            if (item.type == type)
            {
                return true;
            }
            foreach (Item item in Main.player[who].armor)
            if (item.type == type)
            {
                return true;
            }
            foreach (Item item in Main.player[who].miscEquips)
            if (item.type == type)
            {
                return true;
            }
            foreach (Item item in Main.player[who].dye)
            if (item.type == type)
            {
                return true;
            }
            foreach (Item item in Main.player[who].miscDyes)
            if (item.type == type)
            {
                return true;
            }
            return false;
        }

        internal class InvPlayer : SHPlayer
        {
            public bool death;
            public bool justJoined;
            public List<Item> keep = new List<Item>();
            public List<Item> drop = new List<Item>();
            public List<AetherItem> aether = new List<AetherItem>();
            public Stash coinBank;
            public static DropType dropType = DropType.Random;
            public static int value = 10000;
            public static string[] setting = new string[]
            {
                "droptype",
                "valuefloor"
            };
            public static string path = ".\\tshock\\inv_player" + Ini.ext;
            public static void Initialize()
            {
                var ini = new Ini()
                {
                    path = InvPlayer.path,
                    setting = InvPlayer.setting
                };
                string[] data = ini.ReadFile();
                string vf = string.Empty, str = string.Empty;
                int k = 10000;
                DropType dt = DropType.Random;
                if (data.Length == 2)
                {
                    Ini.TryParse(data[0], out str);
                    Ini.TryParse(data[1], out vf);
                    Enum.TryParse(str, out dt);
                    int.TryParse(vf, out k);
                }
                dropType = dt;
                value = k == 0 ? 10000 : k;
            }
            public void Clear()
            {
                keep.Clear();
                drop.Clear();
            }
            public void DropRand(int who, bool dropOpt)
            {
                if (!dropOpt)
                    return;
                int difficulty = TShock.Players[who].Difficulty;
                Player plr = TShock.Players[who].TPlayer;
                #region all carried items
                AllocateItems(ref plr.inventory, 0, difficulty, who);
                AllocateItems(ref plr.armor, NetItem.InventorySlots, difficulty, who);
                AllocateItems(ref plr.dye, NetItem.InventorySlots + NetItem.ArmorSlots, difficulty, who);
                AllocateItems(ref plr.miscEquips, NetItem.InventorySlots + NetItem.ArmorSlots + NetItem.DyeSlots, difficulty, who);
                AllocateItems(ref plr.miscDyes, NetItem.InventorySlots + NetItem.ArmorSlots + NetItem.DyeSlots + NetItem.MiscEquipSlots, difficulty, who);
                #endregion
                if (difficulty == 0)
                {
                    for (int n = 0; n < drop.Count; n++)
                    {
                        Item i = drop[n];
                        int id = Item.NewItem(plr.position, plr.width, plr.height, i.type, i.stack, false, i.prefix);
                        Main.item[id].whoAmI = id;
                        var ae1 = new AetherItem()
                        {
                            item = i,
                            oldOwner = who,
                            whoAmI = Main.item[id].whoAmI
                        };
                        aether.Add(ae1);
                    }
                }
                else
                {
                    for (int k = 0; k < keep.Count; k++)
                    {
                        foreach (Item i in Main.item)
                        {
                            if (i.Distance(plr.position) < 200f && i.IsTheSameAs(keep[k]))
                            {
                                i.active = false;
                                i.SetDefaults(0);
                                break;
                            }
                        }
                    }
                }
            }
            private bool MakeDrop(DropType t, Item i, int value)
            {
                switch (t)
                {
                    case DropType.Random:
                        return Main.rand.Next(2) == 0;
                    case DropType.Value:
                        return i.value >= value;
                    case DropType.Both:
                        return Main.rand.Next(2) == 0 && i.value >= value;
                    default:
                        break;
                }
                return false;
            }
            private void AllocateItems(ref Item[] inv, int slot, int diff = 1, int who = 255)
            {
                for (int i = 0; i < inv.Length; i++)
                {
                    if (inv[i].type != 0 && Plugin.NotCoin(inv[i]))
                    {
                        if (!MakeDrop(dropType, inv[i], value))
                            keep.Add(inv[i].Clone());
                        else 
                        {
                            Item clone = inv[i].Clone();
                            drop.Add(clone);
                            if (diff != 0)
                            {
                                aether.Add(new AetherItem()
                                {
                                    item = clone,
                                    oldOwner = who,
                                    invSlot = i
                                });
                            }
                            else
                            {
                                UpdateItem(ref inv[i], slot + i, who, true);
                            }
                        }
                    }
                }
            }
            public static void UpdateItem(ref Item item, int slot, int who, bool remove = false, int stack = 1, byte prefix = 0 )
            {
                if (remove)
                {
                    item.active = false;
                    item.type = 0;
                    item.SetDefaults();
                }
                item.stack = stack;
                item.prefix = prefix;
                NetMessage.SendData((int)PacketTypes.PlayerSlot, -1, -1, NetworkText.FromLiteral(item.Name), who, slot, item.prefix);
                NetMessage.SendData((int)PacketTypes.PlayerSlot, who, -1, NetworkText.FromLiteral(item.Name), who, slot, item.prefix);
                TShock.Players[who].SendData(PacketTypes.PlayerSlot, item.Name, who, slot, item.prefix);
            }
            public enum DropType
            {
                Random = 0,
                Value = 1,
                Both = 2
            }
        }
        internal class AetherItem
        {
            public Item item;
            public int newOwner
            {
                get { return item.owner; }
            }
            public int oldOwner;
            public bool unbuyable;
            public int whoAmI;
            public int invSlot;
        }
        public class CoinPurse
        {
            private struct CoinItem
            {
                public int slot;
                public int type;
                public Item[] storage;
                public Chest chest;
                public CoinItem(int type, int slot, Item[] storage, Chest chest = null)
                {
                    this.type = type;
                    this.slot = slot;
                    this.storage = storage;
                    this.chest = chest;
                }
            }
            private static List<CoinItem> WhereCoins(int who)
            {
                var coin = new List<CoinItem>();
                var temp = new List<CoinItem>();
                Player plr = Main.player[who];
                Item[][] bank = new Item[][]
                {
                    plr.inventory,
                    plr.bank.item,
                    plr.bank2.item,
                    plr.bank3.item
                };
                Chest[] chest = new Chest[]
                {
                    null,
                    plr.bank,
                    plr.bank2,
                    plr.bank3
                };
                for (int i = 0; i < bank.Length; i++)
                {
                    for (int j = 0; j < bank[i].Length; j++)
                    {
                        Item n = bank[i][j];
                        if (!Plugin.NotCoin(n))
                        {
                            temp.Add(new CoinItem(n.type, j, bank[i], chest[i]));
                        }
                    }
                }
                int[] types = new int[]
                {
                    ItemID.CopperCoin,
                    ItemID.SilverCoin,
                    ItemID.GoldCoin,
                    ItemID.PlatinumCoin
                };
                while (coin.Count < temp.Count)
                {
                    for (int i = 0; i < types.Length; i++)
                    {
                        foreach (var item in temp)
                        {
                            if (item.type == types[i])
                                coin.Add(item);
                        }
                    }
                }
                return coin;
            }
            public static Stash CoinInit(int who)
            {
                int platinum = 0;
                int gold = 0;
                int silver = 0;
                uint copper = 0;
                var bank = new Item[][]
                {
                    Main.player[who].inventory
                };
                for (int i = 0; i < bank.Length; i++)
                {
                    for (int j = 0; j < bank[i].Length; j++)
                    {
                        Item n = bank[i][j];
                        if (!Plugin.NotCoin(n))
                        {
                            switch (n.type)
                            {
                                case ItemID.PlatinumCoin:
                                    platinum += n.stack;
                                    break;
                                case ItemID.GoldCoin:
                                    gold += n.stack;
                                    break;
                                case ItemID.SilverCoin:
                                    silver += n.stack;
                                    break;
                                case ItemID.CopperCoin:
                                    copper += (uint)n.stack;
                                    break;
                            }
                        }
                    }
                }
                return Stash.DoConverge(new Stash(platinum, gold, silver, copper));
            }
            public static Item[][] TotalStorage(int who)
            {
                return new Item[][]
                {
                    Main.player[who].inventory,
                    Main.player[who].bank.item,
                    Main.player[who].bank2.item,
                    Main.player[who].bank3.item
                };
            }
            public static bool ShopItem(int who, int value)
            {
                var coinTypes = new int[] { ItemID.CopperCoin, ItemID.SilverCoin, ItemID.GoldCoin, ItemID.PlatinumCoin };
                Stash.Initialize(coinTypes);
                Stash a = null;
                if ((a = CoinInit(who)) >= value)
                {
                    a -= value;
                    var inv = Main.player[who].inventory;
                    for (int i = 0; i < inv.Length; i++)
                    {
                        foreach (int ct in coinTypes)
                            if (inv[i].type == ct)
                                InvPlayer.UpdateItem(ref inv[i], i, who, true); 
                    }
                    var tsp = TShock.Players[who];
                    foreach (int ct in coinTypes)
                        tsp.GiveItem(ct, "", 32, 48, (int)a.GetCurrency(ct));
                    return true;
                }
                return false;
            }
        }
    }
}