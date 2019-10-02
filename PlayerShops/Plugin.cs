using System;
using System.IO;
using System.Collections.Generic;
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
using RUDD.Terraria;

namespace playershop
{
    [ApiVersion(2,1)]
    public class Plugin : TerrariaPlugin
    {
        
        private ShopChest[] shop = new ShopChest[500];
        private Command command;
        private ShopChest[] active = new ShopChest[256];
        private bool[] canSelect = new bool[256];
        private int[] priceFloor = new int[256];
        private bool[] invalidItem = new bool[256];
        private Item[][] oldInventory = new Item[256][];
        private bool[] justBought = new bool[256];
        private bool priceDouble;
        private DataStore data;
        private bool[] chestRefill = new bool[256];
        private bool[] resetContents = new bool[500];
        private List<CoinStorage> stored = new List<CoinStorage>();
        private bool[] soldItem = new bool[256];
        public override string Name
        {
            get { return "Player Shops"; }
        }
        public override string Author
        {
            get { return "Duze"; }
        }
        public override Version Version
        {
            get { return new Version(1, 0, 3); }
        }
        public override string Description
        {
            get { return "Makes designated chests into item shops with optional refilling"; }
        }
        public Plugin(Main game) : base(game)
        {
        }
        public override void Initialize()
        {
            data = new DataStore("config\\player_shop");
            for (int i = 0; i < Main.player.Length; i++)
                oldInventory[i] = new Item[NetItem.InventorySlots];
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerCommand.Register(this, OnCommand);
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInit);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        }
        protected override void Dispose(bool disposing)
        {
            data.WriteToFile();
            CoinStorage.data.WriteToFile();
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerCommand.Deregister(this, OnCommand);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInit);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            }
            base.Dispose(disposing);
        }
        
        private void OnGetData(GetDataEventArgs e)
        {
            if (!e.Handled)
            {
                if (e.MsgID == PacketTypes.PlayerSlot)
                {
                    using (BinaryReader br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                    {
                        byte id = br.ReadByte();
                        byte slot = br.ReadByte();
                        short stack = br.ReadInt16();
                        byte prefix = br.ReadByte();
                        short netID = br.ReadInt16();
                    }
                }
                if (e.MsgID == PacketTypes.ChestGetContents)
                {
                    using (BinaryReader br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                    {
                        short tileX = br.ReadInt16();
                        short tileY = br.ReadInt16();
                        foreach (ShopChest sc in shop)
                        {
                            if (sc != null)
                            {
                                var r = new RUDD.Region("", new Vector2(tileX - 1, tileY - 1), new Vector2(tileX + 1, tileY + 1));
                                if (r.Contains(sc.x, sc.y))
                                {
                                    for (int i = 0; i < sc.chest.item.Length; i++)
                                    {
                                        if (sc.contents[i] != null)
                                        {
                                            if (sc.refill || resetContents[sc.index2])
                                            {
                                                Main.chest[sc.index].item[i].netDefaults(sc.contents[i].type);
                                                Main.chest[sc.index].item[i].stack = sc.contents[i].stack;
                                                Main.chest[sc.index].item[i].prefix = sc.contents[i].prefix;
                                                TShock.Players[sc.owner].SendData(PacketTypes.ChestItem, "", sc.index, i, Main.chest[sc.index].item[i].stack, Main.chest[sc.index].item[i].prefix, Main.chest[sc.index].item[i].type);
                                            }
                                        }
                                    }
                                    resetContents[sc.index2] = false;
                                    break;
                                }
                            }
                        }
                    }
                }
                if (e.MsgID == PacketTypes.ChestItem)
                {
                    using (BinaryReader br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                    {
                        short cID = br.ReadInt16();
                        byte slot = br.ReadByte();
                        short stack = br.ReadInt16();
                        byte prefix = br.ReadByte();
                        short itemID = br.ReadInt16();
                        foreach (ShopChest sc in shop)
                        {
                            if (sc != null)
                            {   
                                if (sc.index == cID)
                                {
                                    Player player = null;
                                    foreach (Player p in Main.player)
                                    {
                                        if (p.chest == sc.index)
                                        {
                                            player = p;
                                            break;
                                        }
                                    }
                                    if (player != null)
                                    {
                                        if (itemID == 0)
                                        {
                                            if (sc.contents != null)
                                            {
                                                if (sc.contents[slot] != null)
                                                {
                                                    if (sc.contents[slot].type != 0)
                                                    {
                                                        int value = sc.contents[slot].value == 0 ? sc.chest.item[slot].value : sc.contents[slot].value;
                                                        value = Main.hardMode && priceDouble ? value * 2 : value;
                                                        if (!CoinPurse.ShopItem(player.whoAmI, value))
                                                        {
                                                            active[player.whoAmI] = new ShopChest();
                                                            active[player.whoAmI].invalid = new Item();
                                                            active[player.whoAmI].invalid.netDefaults(sc.contents[slot].type);
                                                            active[player.whoAmI].invalid.stack = sc.contents[slot].stack;
                                                            active[player.whoAmI].invalid.prefix = sc.contents[slot].prefix;
                                                            TShock.Players[player.whoAmI].SendInfoMessage(string.Concat("This item requires ", value, " copper."));
                                                            justBought[player.whoAmI] = true;
                                                            resetContents[sc.index2] = true;
                                                        }
                                                        else 
                                                        {
                                                            if (CoinStorage.data == null)
                                                                CoinStorage.data = new DataStore("config\\coin_storage");
                                                            var block = CoinStorage.data.GetBlock(TShock.Players[player.whoAmI].UUID);
                                                            block.WriteValue("Deliver", "True");
                                                            block.IncreaseValue("Coins", value);
                                                            soldItem[sc.owner] = true;
                                                            if (!sc.refill)
                                                                sc.contents[slot].type = 0;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                else if (e.MsgID == PacketTypes.ChestOpen)
                {
                    using (BinaryReader br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                    {
                        short id = br.ReadInt16();
                        short x = br.ReadInt16();
                        short y = br.ReadInt16();
                    }
                }
                else if (e.MsgID == PacketTypes.PlaceChest)
                {
                    using (BinaryReader br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                    {
                        byte id = br.ReadByte();
                        short tileX = br.ReadInt16();
                        short tileY = br.ReadInt16();
                    }
                }
                if (e.MsgID == PacketTypes.Tile)
                {
                    using (BinaryReader br = new BinaryReader(new MemoryStream(e.Msg.readBuffer, e.Index, e.Length)))
                    {
                        byte action = br.ReadByte();
                        short x = br.ReadInt16();
                        short y = br.ReadInt16();
                        if (action == 0)
                        {
                            foreach (Player player in Main.player)
                            {
                                if (player.active)
                                {
                                    if (canSelect[player.whoAmI])
                                    {
                                        if (Main.tile[x, y].type == TileID.Containers)
                                        {
                                            for (int i = 0; i < shop.Length; i++)
                                            {
                                                if (shop[i] == null || !shop[i].enabled)
                                                {
                                                    for (int j = 0; j < Main.chest.Length; j++)
                                                    {
                                                        if (new Microsoft.Xna.Framework.Rectangle(Main.chest[j].x * 16, Main.chest[j].y * 16, 32, 32).Contains(x * 16 + 8, y * 16 + 8))
                                                        {
                                                            shop[i] = new ShopChest();
                                                            shop[i].chest = Main.chest[j];
                                                            shop[i].contents = ShopItem.Convert(Main.chest[j].item, priceFloor[player.whoAmI]);
                                                            shop[i].index = j;
                                                            shop[i].index2 = i;
                                                            shop[i].x = Main.chest[j].x;
                                                            shop[i].y = Main.chest[j].y;
                                                            shop[i].enabled = true;
                                                            shop[i].active = true;
                                                            shop[i].owner = player.whoAmI;
                                                            shop[i].value = priceFloor[player.whoAmI];
                                                            shop[i].refill = chestRefill[player.whoAmI];
                                                            CoinStorage.WriteBlock(TShock.Players[player.whoAmI]);
                                                            if (data.BlockExists("Chest" + j))
                                                            {
                                                                var block = data.GetBlock("Chest" + j);
                                                                block.WriteValue("ChestX", x.ToString());
                                                                block.WriteValue("ChestY", y.ToString());
                                                                block.WriteValue("Index", j.ToString());
                                                                block.WriteValue("OwnerID", player.whoAmI.ToString());
                                                                block.WriteValue("Price", priceFloor[player.whoAmI].ToString());
                                                                block.WriteValue("Refill", chestRefill[player.whoAmI].ToString());
                                                                for (int m = 0; m < shop[i].contents.Length; m++)
                                                                {
                                                                    block.WriteValue("Slot" + m, shop[i].contents[m].type.ToString());
                                                                }
                                                            }
                                                            canSelect[player.whoAmI] = false;
                                                            priceFloor[player.whoAmI] = 0;
                                                            TShock.Players[player.whoAmI].SendSuccessMessage("The chest is now set as a shop.");
                                                            break;
                                                        }
                                                    }
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        private void OnCommand(CommandEventArgs e)
        {
            if (command == null)
            {
                Commands.ChatCommands.Add(command = new Command("playershop.place", PrePlaceChest, "shopstart")
                {
                    HelpText = "Precludes placing a shop chest"
                });
                Commands.ChatCommands.Add(new Command("playershop.tools.setup", SetAsShop, "setupshop")
                {
                    HelpText = "Designates chest contents"
                });
                Commands.ChatCommands.Add(new Command("playershop.tools.opt", PreChooseChest, "selectchest")
                {
                    HelpText = "Used before selecting a chest with a pickaxe"
                });
                Commands.ChatCommands.Add(new Command("playershop.tools.hm", PriceChange, "hmdouble")
                {
                    HelpText = "Changes whether or not shop prices double for hard mode"
                });
                Commands.ChatCommands.Add(new Command("playershop.tools.refill", RefillOpt, "refillopt")
                {
                    HelpText = "For setting the chest refill option"
                });
            }
        }
        private void OnUpdate(EventArgs e)
        {
            foreach (Player player in Main.player)
            {
                if (player.active && !player.dead)
                {
                    int id = player.whoAmI;
                    if (justBought[id])
                    {
                        if (active[id] != null)
                        {
                            if (active[id].invalid != null)
                            {
                                if (active[id].invalid.type != 0)
                                {
                                    ResetInventory(id);
                                    active[id].invalid.netDefaults(0);
                                    justBought[id] = false;
                                    break;
                                }
                            }
                        }
                    }
                    oldInventory[id] = (Item[])Main.player[id].inventory.Clone();
                    string uuid = TShock.Players[player.whoAmI].UUID;
                    if (CoinStorage.data != null && soldItem[player.whoAmI])
                    {
                        Console.WriteLine("Sold item?");
                        if (CoinStorage.data.BlockExists(uuid) && player.name == CoinStorage.data.GetBlock(uuid).GetValue("Name"))
                        {
                            Console.WriteLine("Block exists");
                            var block = CoinStorage.data.GetBlock(uuid);
                            bool deliver = false;
                            int value = 0;
                            if (bool.TryParse(block.GetValue("Deliver"), out deliver) && int.TryParse(block.GetValue("Coins"), out value))
                            {
                                Console.WriteLine("Deliver?");
                                if (deliver)
                                {
                                    TShock.Players[player.whoAmI].GiveItem(ItemID.CopperCoin, "", 32, 48, value);
                                    block.WriteValue("Coins", "0");
                                    block.WriteValue("Deliver", "False");
                                    soldItem[player.whoAmI] = false;
                                }
                            }
                        }
                    }
                }
            }
        }
        private void OnJoin(JoinEventArgs e)
        {
            oldInventory[e.Who] = (Item[])Main.player[e.Who].inventory.Clone();
            chestRefill[e.Who] = false;
        }
        private void OnLeave(LeaveEventArgs e)
        {
        }
        private void OnPostInit(EventArgs e)
        {
            string[] list = new string[46]; 
            list[0] = "ChestX";
            list[1] = "ChestY";
            list[2] = "Index";
            list[3] = "OwnerID";
            list[4] = "Price";
            list[5] = "Refill";
            for (int i = 6; i < 46; i++)
                list[i] += "Slot" + (i - 6);
            for (int m = 0; m < shop.Length; m++)
            {
                string heading = "Chest" + m;
                if (!data.BlockExists(heading))
                    data.NewBlock(list, heading);
                else
                {
                    var block = data.GetBlock(heading);
                    int Index = int.Parse(block.GetValue("Index"));
                    int value = int.Parse(block.GetValue("Price"));
                    bool Refill = block.GetValue("Refill").ToLower() != "true" ? false : true;
                    shop[m] = new ShopChest()
                    {
                        active = true,
                        enabled = Index != 0,
                        refill = Refill,
                        owner = int.Parse(block.GetValue("OwnerID")),
                        index = Index,
                        index2 = m
                    };
                    if (shop[m].enabled)
                    {
                        if (Main.chest[Index] != null)
                        {
                            shop[m].chest = Main.chest[Index];
                            shop[m].x = shop[m].chest.x;
                            shop[m].y = shop[m].chest.y;
                            for (int j = 0; j < shop[m].contents.Length; j++)
                            {
                                shop[m].contents[j] = new ShopItem()
                                {
                                    type = int.Parse(block.GetValue("Slot" + j)),
                                    value = int.Parse(block.GetValue("Price")),
                                    stack = 1,
                                    prefix = 0
                                };
                            }
                        }
                        else
                        {
                            shop[m].enabled = false;
                        }
                    }
                }
            }
        }

        private void PriceChange(CommandArgs e)
        {
            priceDouble = !priceDouble;
            e.Player.SendSuccessMessage(string.Concat("Prices are now doubled while in hard mode [", priceDouble ? "enabled" : "disabled", "]."));
        }
        private void PreChooseChest(CommandArgs e)
        {
            string opt = string.Empty;
            if (e.Message.Contains(" "))
            {
                int value = 0;
                opt = e.Message.Substring(e.Message.IndexOf(" ") + 1);
                if (int.TryParse(opt, out value))
                {
                    priceFloor[e.Player.Index] = value;
                }
                canSelect[e.Player.Index] = !canSelect[e.Player.Index];
                e.Player.SendInfoMessage((string.Concat("Mine a chest to select it, and its contents, to be a shop [", canSelect[e.Player.Index] ? "enabled" : "disabled", "].")));
            }
            else
            {
                e.Player.SendErrorMessage("The command requires a value input (/selectchest [value]).\nIf it is 0, the items will sell at their item value.\nOtherwise the items will sell at the value provoded (ex. 100 = 1 silver, 10000 = 1 gold)");
            }
        }
        private void SetAsShop(CommandArgs e)
        {
            string raw = string.Empty, cmd = string.Empty, opt = string.Empty;
            switch(0)
            {
                case 0:
                    raw = e.Message.Substring(10);
                    if (raw.Contains(" "))
                        cmd = raw.Substring(0, raw.IndexOf(" "));
                    else goto case 1;
                    try
                    {
                        opt = raw.Substring(raw.LastIndexOf(" ") + 1);
                    }
                    catch
                    {
                        goto case 1;
                    }
                    break;
                case 1:
                    e.Player.SendInfoMessage("Commands for shop chests:\naddcontent [item ID]\nremovecontent [item ID]\ncomplete [name]\nfloor [price]");
                    return;
            }
            ShopChest sc = null;
            foreach (ShopChest t in shop)
            {
                if (t != null)
                {
                    if ((!t.enabled && t.active && t.player == e.TPlayer) || t.name == opt)
                    {
                        sc = t;
                        break;
                    }
                }
            }
            if (sc == null)
                return;
            switch(cmd)
            {
                case "complete":
                    sc.enabled = true;
                    sc.name = opt;
                    sc.player = e.TPlayer;
                    sc.owner = e.Player.Index;
                    e.Player.SendInfoMessage("Mine a chest to complete the setup.");
                    break;
                case "addcontent":
                    for (int i = 0; i < sc.contents.Length; i++)
                    {
                        if (sc.contents[i] == null)
                        {
                            sc.contents[i] = new ShopItem();
                        }
                        int type = 0;
                        if (sc.contents[i].type == 0 && int.TryParse(opt, out type))
                        {
                            sc.contents[i].type = type;
                            e.Player.SendInfoMessage("Added item [" + type + "].");
                            break;
                        }
                    }
                    break;
                case "removecontent":
                    for (int i = 0; i < sc.contents.Length; i++)
                    {
                        int type = 0;
                        if ((sc.contents[i] != null || sc.contents[i].type != 0) && int.TryParse(opt, out type))
                        {
                            if (sc.contents[i].type == type)
                            {
                                sc.contents[i].type = 0;
                                e.Player.SendInfoMessage("Removed item [" + type + "].");
                                break;
                            }
                        }
                    }
                    break;
                case "list":
                    string list = "";
                    foreach (var item in sc.contents)
                    {
                        if (item != null)
                        if (item.type != 0)
                           list += string.Concat(item.type, " ");
                    }
                    e.Player.SendInfoMessage(list);
                    break;
                default:
                    break;
            }
        }
        private void PrePlaceChest(CommandArgs e)
        {
            for (int i = 0; i < shop.Length; i++)
            {
                if (shop[i] == null || !shop[i].enabled)
                {
                    shop[i] = new ShopChest()
                    {
                        active = true,
                        owner = e.Player.Index,
                        player = e.TPlayer,
                        index = i
                    };
                    e.Player.SendSuccessMessage(string.Concat("Shop with index ", i, " made available (hit or place a chest)."));
                    break;
                }
            }
        }
        private void RefillOpt(CommandArgs e)
        {
            chestRefill[e.Player.Index] = !chestRefill[e.Player.Index];
            e.Player.SendSuccessMessage(string.Concat("Selected chests [will", chestRefill[e.Player.Index] ? "" : " not", "] refill."));
        }

        private void RemoveItem(Item item, int who, int slot, int stack)
        {
            throw new NotImplementedException();
        }
        private void ResetInventory(int who)
        {
            if (Main.player[who].inventory != oldInventory[who])
            {
                var clone = (Item[])oldInventory[who].Clone();
                for (int i = 0; i < NetItem.InventorySlots; i++)
                {
                    Main.player[who].inventory[i] = clone[i];
                    UpdateItem(Main.player[who].inventory[i], i, who, false, clone[i].stack);
                }
            }
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
        private Item[][] MaxInventory(int who)
        {
            Player player = Main.player[who];
            return new Item[][]
            {
                player.inventory,
                player.armor,
                player.dye,
                player.miscEquips,
                player.miscDyes
            };
        }
    }
    internal class ShopChest
    {
        public bool active;
        public Chest chest;
        public int x;
        public int y;
        public int owner;
        public Player player;
        public bool enabled;
        public int index;
        public string name;
        public ShopItem[] contents = new ShopItem[40];
        public bool refill = true;
        public Item invalid;
        public int value;
        public int index2;
    }
    internal class ShopItem
    {
        public static ShopItem[] Convert(Item[] item, int price = 0)
        {
            var shop = new ShopItem[40];
            for (int i = 0; i < item.Length; i++)
            {
                if (item[i] != null)
                shop[i] = new ShopItem()
                {
                    type = item[i].type,
                    stack = item[i].stack,
                    prefix = item[i].prefix,
                    value = price > 0 ? price : item[i].value
                };
            }
            return shop;
        }
        public int type;
        public int stack;
        public byte prefix;
        public int value;
    }
    internal class CoinStorage
    {
        public static void WriteBlock(TSPlayer player)
        {
            if (data == null)
                data = new DataStore("config\\coin_storage");
            if (!data.BlockExists(player.UUID))
            {
                var block = data.NewBlock(new string[] 
                {
                    "Name",
                    "Deliver",
                    "Coins"
                }, player.UUID);
                block.WriteValue("Name", player.Name);
                block.WriteValue("Deliver", "False");
                block.WriteValue("Coins", "0");
            }
        }
        public static DataStore data;
    }
}
