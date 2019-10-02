using System;
using System.Collections;
using System.IO;
using System.Text;

namespace RUDD.Dotnet
{
    public class DataStore
    {
        public const string ext = ".dat";
        private string[] array = new string[50001];
        private string fileName;
        private string fileNoExt;
        internal Block[] block = new Block[501];
        public DataStore()
        {
        }
        public DataStore(string fileNoExt)
        {
            this.fileNoExt = fileNoExt;
            fileName = fileNoExt + ext;
            Initialize();
        }
         private void Log(object text)
        {
            Console.WriteLine(text);
        }
        private void Log(string[] array)
        {
            foreach (string t in array)
            {
                if (t != null && NotEmpty(t))
                   Console.WriteLine(t);
            }
        }
        private bool NotEmpty(string text)
        {
            return text != string.Empty && text != "\n" && text != "\r" && text != "\n\r" && text != "\r\n" && !string.IsNullOrWhiteSpace(text);
        }
        public void WriteToFile()
        {
            using (StreamWriter sw = new StreamWriter(fileName))
            {
                sw.NewLine = "\n";
                for (int j = 0; j < block.Length; j++)
                {
                    if (block[j].Data == null)
                        continue;
                    int count = 0;
                    foreach (string t in array)
                    {
                        if (t == block[j].Heading)
                        {
                            count++;
                        }
                    }
                    if (count < 2 || !BlockExists(block[j].Heading))
                    {
                        for (int i = 0; i < block[j].Data.Length; i++)
                        {
                            if (NotEmpty(block[j].Data[i]))
                                sw.WriteLine(block[j].Data[i]);
                        }
                    }
                }
            }
        }

        private void Initialize()
        {
            if (fileName.Contains("\\"))
            {
                if (!Directory.Exists(fileName.Remove(fileName.LastIndexOf("\\")))) 
                    Directory.CreateDirectory(fileName.Remove(fileName.LastIndexOf("\\")));
            }
            if (!File.Exists(fileName))
            {
                var f = File.Create(fileName);
                f.Close();
                f.Dispose();
                return;
            }
            using (StreamReader sr = new StreamReader(fileName))
            {
                array = sr.ReadToEnd().Split('\n');
            }
            Rewrite();
        }
        private void Rewrite()
        {
            int index = 0;
            int n = 0;
            for (int i = 1; i < array.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(array[i]))
                    continue;
                switch (0)
                {
                    case -1:
                        index++;
                        n = 0;
                        break;
                    case 0:
                        if (array[i].StartsWith("["))
                        {
                            block[index] = new Block() {
                                Heading = array[i - 1],
                                RawData = new string[1001],
                                active = true
                            };
                            block[index].RawData[0] = array[i - 1];
                            block[index].RawData[1] = "[";
                        }
                        else if (array[i].StartsWith("]"))
                        {
                            block[index].RawData[n + Block.Zero] = "]";
                            goto case -1;
                        }
                        if (array[i].Contains(":"))
                        {
                            block[index].RawData[n + Block.Zero] = array[i];
                        }
                        n++;
                        break;
                }
            }
        }
        private int NumBlocks()
        {
            int n = 0;
            for (int i = 0; i < block.Length; i++)
            {
                if (block[i] != null && block[i].Data != null)
                    n++;
            }
            return n;
        }
        public Block NewBlock(string[] array, string heading)
        {
            foreach (Block b in block)
            {
                if (b.Heading == heading)
                    return b;
            }
            for (int i = 0; i < array.Length; i++)
            {
                if (!array[i].Contains(":"))
                    array[i] += ":0";
            }
            int n = NumBlocks();
            ShiftDown(ref array, 2);
            array[0] = heading;
            array[1] = "[";
            array[array.Length - 1] = "]";
            block[n] = new Block()
            {
                Heading = heading,
                RawData = array,
                index = n,
                Root = this
            };
            return block[n];
        }
        private void ShiftDown(ref string[] input, int amount)
        {
            var clone = new string[input.Length + amount + 1];
            input.CopyTo(clone, amount);
            input = clone;
        }
        private bool IsClose(string item)
        {
            return item.StartsWith("]");
        }
        public Block GetBlock(string heading)
        {
            foreach (Block b in block)
            {
                if (b.Data != null)
                if (b.Heading == heading)
                    return b;
            }
            return block[0];
        }
        public bool BlockExists(string heading)
        {
            foreach (Block b in block)
            {
                if (b.Heading == heading)
                    return true;
            }
            return false;
        }
    }
    public struct Block : IEnumerable
    {
        private Block Instance
        {
            get { return this; }
        }
        public DataStore Root
        {
            get;
            internal set;
        }
        public string Heading
        {
            get;
            internal set;
        }
        internal string[] RawData
        {
            get;
            set;
        }
        internal string[] Data
        {
            get 
            {
                if (Root != null)
                    Root.block[index] = this;
                return RawData;
            }
        }
        public string[] Contents
        {
            get { return Trim(RawData); }
        }
        public int index;
        private const int Max = 500;
        public bool active;
        public IEnumerator GetEnumerator()
        {
            return Data.GetEnumerator();
        }
        public void Add(string item)
        {
            throw new NotImplementedException();
        }
        internal void UpdateRoot(DataStore data)
        {
            data.block[index] = this;
        }
        public string[] Trim(string[] array)
        {
            if (array == null) 
                return new string[] { "null" };
            var clone = new string[array.Length - 3];
            if (array.Length > 3)
            {
                for (int i = 0; i < clone.Length; i++)
                {
                    clone[i] = array[i + 2];
                }
            }
            return clone;
        }
        internal int offset 
        {
            get { throw new NotImplementedException(); }
        }
        internal int Length
        {
            get { return RawData.Length; } 
        }
        internal const int Zero = 2;
        public string Key(string item)
        {
            if (item.Contains(":"))
                return item.Substring(0, item.IndexOf(":"));
            else return string.Empty;
        }
        public string Key(int index)
        {
            index += 2;
            if (RawData == null || RawData[index] == null)
                return string.Empty;
            if (index >= RawData.Length - 2)
                return string.Empty;
            if (RawData[index].Contains(":"))
                return RawData[index].Substring(0, RawData[index].IndexOf(":"));
            else return string.Empty;
        }
        public string Value(string item)
        {
            if (item.Contains(":"))
                return item.Substring(item.IndexOf(":") + 1);
            else return string.Empty;
        }
        public string Value(int index)
        {
            index += 2;
            if (RawData == null || RawData[index] == null)
                return string.Empty;
            if (index >= RawData.Length - 2)
                return string.Empty;
            if (RawData[index].Contains(":"))
                return RawData[index].Substring(RawData[index].IndexOf(":") + 1);
            else return string.Empty;
        }
        public string[] Keys()
        {
            string[] keys = new string[RawData.Length];
            for (int i = 0; i < RawData.Length; i++)
            {
                if (RawData[i] != null && RawData[i].Contains(":"))
                    keys[i] = RawData[i].Substring(0, RawData[i].IndexOf(":"));
            }
            return keys;
        }
        public string[] Values()
        {
            string[] value = new string[RawData.Length];
            for (int i = 0; i < RawData.Length; i++)
            {
                if (RawData[i] != null && RawData[i].Contains(":"))
                    value[i] = RawData[i].Substring(RawData[i].IndexOf(":") + 1);
            }
            return value;
        }
        public string GetValue(string key)
        {
            var keys = Keys();
            for (int i = 0; i < keys.Length; i++) 
                if (key == keys[i])
                    return Value(RawData[i]);
            return string.Empty;
        }
        private void ShiftDown(ref string[] input, int amount)
        {
            var clone = new string[input.Length + amount];
            input.CopyTo(clone, 0);
            input = clone;
        }
        public void AddItem(string key, string value)
        {
            foreach (string s in Keys())
            {
                if (s == key)
                    return;
            }
            string[] clone = new string[Length + 1];
            RawData.CopyTo(clone, 0);
            RawData = new string[clone.Length];
            clone[clone.Length - 2] = string.Concat(key, ":", value);
            clone[clone.Length - 1] = "]";
            RawData = (string[])clone.Clone();
        }
        public void WriteValue(string key, string value)
        {
            if (RawData == null)
                return;
            int n = 0;
            for (int i = 0; i < RawData.Length; i++)
            {
                if (!string.IsNullOrEmpty(RawData[i])) 
                if (Key(RawData[i]) == key)
                {
                    n = i;
                    break;
                }
                if (i == RawData.Length - 1)
                    return;
            }
            RawData[n] = string.Concat(key, ":", value);
        }
        public int IncreaseValue(string key, int amount)
        {
            int n = 0;
            for (int i = 0; i < RawData.Length; i++)
            {
                if (!string.IsNullOrEmpty(RawData[i]))
                if (Key(RawData[i]) == key)
                {
                    n = i;
                    break;
                }
                if (i == RawData.Length - 1)
                    return amount;
            }
            int j = 0;
            string value = Value(RawData[n]);
            if (int.TryParse(value, out j))
            {
                j += amount;
                RawData[n] = string.Concat(key, ":", j);
            }
            return j + amount;
        }

        public override bool Equals(object obj)
        {
            return Instance == (Block)obj;
        }
        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
        public static bool operator !=(Block a, Block b)
        {
            return a.RawData == b.RawData;
        }
        public static bool operator ==(Block a, Block b)
        {
            return a.RawData == b.RawData;
        }
    }
    public class Ini
    {
        public string path;
        public const string ext = ".ini";
        public string[] setting;
        public Ini()
        {
        }
        private void MakeFile()
        {
            var file = File.Create(path);
            file.Close();
            file.Dispose();
        }
        public void WriteFile(object[] text)
        {
            if (text == null)
                return;
            for (int n = 0; n < setting.Length; n++)
                setting[n] += "=";
            if (!File.Exists(path))
                MakeFile();
            using (StreamWriter sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                for (int i = 0; i < setting.Length; i++)
                {
                    if (i < text.Length && i < setting.Length) 
                    {
                        sw.Write(setting[i]);
                        sw.WriteLine(text[i]);
                    }
                }
            }
        }
        public string[] ReadFile()
        {
            if (!File.Exists(path))
                MakeFile();
            string[] info = null;
            using (StreamReader sr = new StreamReader(path))
                info = sr.ReadToEnd().Split('\n');
            return info;
        }
        public static bool TryParse(string setting, out string output)
        {
            if (setting.Contains("="))
            {
                output = setting.Substring(setting.IndexOf('=') + 1);
                return true;
            }
            output = string.Empty;
            return false;
        }
    }
}
