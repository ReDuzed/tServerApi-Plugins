namespace RUDD
{
    public struct Vector2
    {
        public float X;
        public float Y;
        public Vector2(float x, float y)
        {
            this.X = x;
            this.Y = y;
        }
        public static Vector2 Zero
        {
            get { return new Vector2(0f, 0f); }
        }
    }
    public struct Region
    {
        public string name;
        public Vector2 point1;
        public Vector2 point2;
        public bool nonPvP;
        public Region(string name, Vector2 tl, Vector2 br, bool nonPvP = false)
        {
            if (br.X < tl.X || tl.Y > br.Y)
            {
                this.point1 = br;
                this.point2 = tl;
            }
            else
            {
                this.point1 = tl;
                this.point2 = br;
            }
            this.name = name;
            this.nonPvP = nonPvP;
        }
        public bool Contains(float X, float Y)
        {
            return X >= point1.X && X <= point2.X && Y >= point1.Y && Y <= point2.Y;
        }
    }
    public class User
    {
        public int cooldown
        {
            get; internal set;
        }
        public int whoAmI;
        public User(int who)
        {
            this.whoAmI = who;
        }
        public void Update()
        {
            if (cooldown > 0)
                cooldown--;
        }
    }
    public class SHPlayer
    {
        public int who;
        public bool healed;
        public int oldLife;
        public bool reserved;
    }
    public enum TeamID
    {
        White = 0,
        Red = 1,
        Green = 2,
        Blue = 3,
        Yellow = 4,
        Purple = 5
    }
    public class Stash
    {
        public int platinum;
        public int gold;
        public int silver;
        public uint copper;
        public static int CopperCoin, SilverCoin, GoldCoin, PlatinumCoin;
        public Stash()
        {
        }
        public Stash(int platinum, int gold, int silver, uint copper)
        {
            this.copper = copper;
            this.silver = silver;
            this.gold = gold;
            this.platinum = platinum;
            DoConverge(this);
        }
        public static void Initialize(int[] type = null)
        {
            if (type != null && type.Length == 4)
            {
                CopperCoin = type[0];
                SilverCoin = type[1];
                GoldCoin = type[2];
                PlatinumCoin = type[3];
            }
        }
        public long GetCurrency(int type)
        {
            if (type == CopperCoin)
                return copper;
            else if (type == SilverCoin)
                return silver;
            else if (type == GoldCoin)
                return gold;
            else if (type == PlatinumCoin)
                return platinum;
            return 0;
        }
        public static Stash DoConverge(Stash a)
        {
            while (a.copper >= 100)
            {
                a.copper -= 100;
                a.silver++;
            }
            while (a.silver >= 100)
            {
                a.silver -= 100;
                a.gold++;
            }
            while (a.gold >= 100)
            {
                a.gold -= 100;
                a.platinum++;
            }
            return a;
        }
        public static Stash DoConvert(uint copper)
        {
            var a = new Stash();
            a.copper = copper;
            while (a.copper >= 100)
            {
                a.copper -= 100;
                a.silver++;
            }
            while (a.silver >= 100)
            {
                a.silver -= 100;
                a.gold++;
            }
            while (a.gold >= 100)
            {
                a.gold -= 100;
                a.platinum++;
            }
            return a;
        }
        public uint TotalCopper()
        {
            return (uint)(platinum * 1000000 + gold * 10000 + silver * 100 + copper);
        }
        public static uint TotalCopper(Stash a)
        {
            return (uint)(a.platinum * 1000000 + a.gold * 10000 + a.silver * 100 + a.copper);
        }
        public bool Compare(Stash a, uint copper)
        {
            return TotalCopper() > copper;
        }
        public static Stash operator -(Stash a, Stash b)
        {
            uint at = TotalCopper(a);
            uint bt = TotalCopper(b);
            Stash c = DoConvert(at - bt);
            return c;
        }
        public static Stash operator -(Stash a, uint i)
        {
            uint at = TotalCopper(a);
            Stash c = DoConvert(at - i);
            return c;
        }
        public static Stash operator -(Stash a, int i)
        {
            uint at = TotalCopper(a);
            Stash c = DoConvert(at - (uint)i);
            return c;
        }
        public static bool operator >=(Stash a, int i)
        {
            return TotalCopper(a) > i;
        }
        public static bool operator <=(Stash a, int i)
        {
            return TotalCopper(a) < i;
        }
    } 
}
