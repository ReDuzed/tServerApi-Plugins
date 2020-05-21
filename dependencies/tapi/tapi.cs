using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using TShockAPI;
using RUDD;

namespace RUDD.Terraria
{
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
                    if (!NotCoin(n))
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
                    if (!NotCoin(n))
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
                            UpdateItem(ref inv[i], i, who, true); 
                }
                var tsp = TShock.Players[who];
                foreach (int ct in coinTypes)
                    tsp.GiveItem(ct, (int)a.GetCurrency(ct));
                return true;
            }
            return false;
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
        public static bool NotCoin(Item item)
        {
            return item.type != ItemID.CopperCoin && item.type != ItemID.SilverCoin && item.type != ItemID.GoldCoin && item.type != ItemID.PlatinumCoin;
        }
        public static bool NotCoin(int type)
        {
            return type != ItemID.CopperCoin && type != ItemID.SilverCoin && type != ItemID.GoldCoin && type != ItemID.PlatinumCoin;
        }
    }
}