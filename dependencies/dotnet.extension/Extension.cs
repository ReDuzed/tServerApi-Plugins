using System;
using System.IO;
using TShockAPI;

namespace RUDD.Dotnet
{
    public class Util
    {
        public static TSPlayer FindPlayer(string Name)
        {
            int[] index = new int[256];
            for (int i = 0; i < index.Length; i++)
            {
                index[i] = 0;
            }
            foreach(TSPlayer p in TShock.Players)
            {
                if (p != null && p.Active)
                {
                    index[p.Index] = NameMatch(p.Name.ToLower(), Name.ToLower());
                }
            }
            int closestMatch = -1;
            for (int i = 0; i < index.Length -1; i++)
            {
                if (index[i] > index[i + 1])
                    closestMatch = i;
            }
            return closestMatch == -1 ? null : TShock.Players[closestMatch];
        }
        private static int NameMatch(string input, string search)
        {
            int count = 0;
            if (input.StartsWith(search.Substring(0, 1)))
            {
                count++;
                for (int i = 1; i < input.Length; i++)
                {
                    if (i < search.Length)
                    {
                        if (input.Substring(i, 1) == search.Substring(i, 1))
                        {
                            count++;
                        }
                    }
                }
            }
            return count;  
        }
    }
}