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
using RUDD.Terraria;

namespace chestshop
{
    [ApiVersion(2,1)]
    public class Plugin : TerrariaPlugin
    {
        
        private ShopChest[] shop = new ShopChest[501];
        private Command command;
        private ShopChest[] active = new ShopChest[256];
        private bool[] canSelect = new bool[256];
        private int[] priceFloor = new int[256];
        private bool[] invalidItem = new bool[256];
        private Item[][] oldInventory = new Item[256][];
        private bool[] justBought = new bool[256];
        private bool priceDouble;
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
            get { return new Version(0, 3); }
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
            for (int i = 0; i < Main.player.Length; i++)
                oldInventory[i] = new Item[NetItem.InventorySlots];
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerCommand.Register(this, OnCommand);
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerCommand.Deregister(this, OnCommand);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
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
                                            if (sc.refill)
                                            {
                                                Main.chest[sc.index].item[i].netDefaults(sc.contents[i].type);
                                                Main.chest[sc.index].item[i].stack = sc.contents[i].stack;
                                                Main.chest[sc.index].item[i].prefix = sc.contents[i].prefix;
                                                TShock.Players[sc.owner].SendData(PacketTypes.ChestItem, "", sc.index, i, Main.chest[sc.index].item[i].stack, Main.chest[sc.index].item[i].prefix, Main.chest[sc.index].item[i].type);
                                            }
                                        }
                                    }
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
                                            if (sc.contents[slot].type != 0)
                                            {
                                                int value = Main.hardMode && priceDouble ? sc.contents[slot].value * 2 : sc.contents[slot].value;
                                                if (!CoinPurse.ShopItem(player.whoAmI, value))
                                                {
                                                    active[player.whoAmI] = new ShopChest();
                                                    active[player.whoAmI].invalid = new Item();
                                                    active[player.whoAmI].invalid.netDefaults(sc.contents[slot].type);
                                                    active[player.whoAmI].invalid.stack = sc.contents[slot].stack;
                                                    active[player.whoAmI].invalid.prefix = sc.contents[slot].prefix;
                                                    TShock.Players[player.whoAmI].SendInfoMessage(string.Concat("This item requires ", value, " copper."));
                                                    justBought[player.whoAmI] = true;
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
                                                if (shop[i] == null)
                                                {
                                                    for (int j = 0; j < Main.chest.Length; j++)
                                                    {
                                                        if (new Microsoft.Xna.Framework.Rectangle(Main.chest[j].x * 16, Main.chest[j].y * 16, 32, 32).Contains(x * 16 + 8, y * 16 + 8))
                                                        {
                                                            shop[i] = new ShopChest();
                                                            shop[i].chest = Main.chest[j];
                                                            shop[i].contents = ShopItem.Convert(Main.chest[j].item, priceFloor[player.whoAmI]);
                                                            shop[i].index = j;
                                                            shop[i].x = x;
                                                            shop[i].y = y;
                                                            shop[i].enabled = true;
                                                            shop[i].active = true;
                                                            shop[i].owner = player.whoAmI;
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
                Commands.ChatCommands.Add(new Command("playershop.place", PrePlaceChest, "shopstart")
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
                Commands.ChatCommands.Add(command = new Command("playershop.tools.hm", PriceChange, "hmdouble")
                {
                    HelpText = "Changes whether or not shop prices double for hard mode"
                });
            }
        }
        private void PriceChange(CommandArgs e)
        {
            priceDouble = !priceDouble;
            e.Player.SendSuccessMessage(string.Concat("Prices are now doubled while in hard mode [", priceDouble ? "enabled" : "disabled", "]."));
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
                }
            }
        }
        private void OnJoin(JoinEventArgs e)
        {
            oldInventory[e.Who] = (Item[])Main.player[e.Who].inventory.Clone();
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

        private void RemoveItem(Item item, int who, int slot, int stack)
        {
            throw new NotImplementedException();
        }
        private void ResetInventory(int who)
        {
            if (Main.player[who].inventory != oldInventory[who])
            {
                Console.WriteLine("Attempting reset.");
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
}
