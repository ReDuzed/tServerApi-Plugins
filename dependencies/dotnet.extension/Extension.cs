using System;
using System.IO;
using TShockAPI;

namespace RUDD.Dotnet
{
    public class Util
    {
        public static DataStore data;
        public static string[] players = new string[256];
        public const string ROSTER = "ROSTER", KEY = "PLAYERS";
        private static bool once;
        protected static void Initialize()
        {
            data = new DataStore("config\\util_player_roster");
            if (!data.BlockExists(ROSTER))
            {
                data.NewBlock(new string[] {KEY}, ROSTER);
            }
        }
        public static void AddPlayer(string Name)
        {
            if (!once)
            {
                Initialize();
                once = true;
            }
            Block roster = data.GetBlock(ROSTER);
            string list = roster.GetValue(KEY);
            string userName = Name;
            if (!list.Contains(";"))
            {
                roster.WriteValue(KEY, userName + ";");
                return;
            }
            for (int i = 0; i < list.Length; i++)
            {
                if (list.Substring(i).StartsWith(userName))
                    return;
            }
            roster.AddValue(KEY, ';', userName);
        }
        public static TSPlayer FindPlayer(string Name)
        {
            if (!once)   
            {
                Initialize();
                once = true;
            }
            Block roster = data.GetBlock(ROSTER);
            string list = roster.GetValue(KEY).Replace(';', ' ') + " ..";
            Console.WriteLine(list);
            foreach(TSPlayer p in TShock.Players)
            {
                if (p != null && p.Active)
                {
                    for (int i = 0; i < list.Length - 3; i++)
                    {
                        string sub;
                        string sub2;
                        if ((sub = list.Substring(i, 2).ToLower()) == (sub2 = p.Name.ToLower().Substring(0, 2)))
                        {
                            Console.WriteLine(list.Substring(i, 2).ToLower());
                            Console.WriteLine(p.Name.ToLower().Substring(0, 2));
                            if (sub == sub2)
                            {
                                if (list.Substring(i).ToLower().StartsWith(p.Name.ToLower()))
                                Console.WriteLine("success");
                                return p;
                            }
                        }
                    }
                }
            }
            return null;
        }
        public static void WriteFile()
        {
            data.WriteToFile();
        }
    }
}
