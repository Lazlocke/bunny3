using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bunny_black_3_exe_text_extract
{
    class Program
    {
        static uint SwapEndian(uint number)
        {
            return (number << 24) | (number & 0x0000FF00) << 8 | (number & 0x00FF0000) >> 8 | (number >> 24);
        }

        static ushort SwapEndian(ushort number)
        {
            return (ushort)(((number & 0x00FF) << 8) | ((number & 0xFF00) >> 8));
        }

        static void PrintPrettyHex(StreamWriter txt, byte hex)
        {
            txt.Write("<$" + (hex & 0xFF).ToString("X2") + ">");
        }

        static void PrintPrettyHex(StreamWriter txt, ushort hex)
        {
            txt.Write("<$" + ((hex & 0xFF00) >> 8).ToString("X2") + ">");
            txt.Write("<$" + ((hex & 0xFF)).ToString("X2") + ">");
        }

        static string PrintPrettyHex(ushort hex)
        {
            string pretty = "<$" + ((hex & 0xFF)).ToString("X2") + ">";
            pretty += "<$" + ((hex & 0xFF00) >> 8).ToString("X2") + ">";
            return pretty;
        }

        static string PrintPrettyHex(byte hex)
        {
            string pretty = "<$" + hex.ToString("X2") + ">";
            return pretty;
        }

        class Ptr
        {
            public uint ptr;
            public uint orig;
            public List<int> pos = new List<int>();
        }

        static string GetEncodedLine(byte[] theLine, Dictionary<string, string> table)
        {
            int pos = 0;
            bool found = false;
            bool isLetter = false;
            string myLine = "";

            bool doDupe = true;
            string dupe = "";

            int maxTbleValLen = table.Keys.OrderByDescending(x => x.ToString().Length).First().Length / 2;

            while (pos < theLine.Length)
            {
                found = false;
                // Get largest letter...
                for (int i = maxTbleValLen; i >= 1; i--)
                {
                    if (pos + i < theLine.Length)
                    {
                        string key = BitConverter.ToString(theLine, pos, i).Replace("-", "");
                        if (table.ContainsKey(key))
                        {
                            if (!isLetter)
                            {
                                if (pos != 0)
                                {
                                    myLine += "\n";
                                    //dupe += "\n";
                                }
                                myLine += "//";
                            }

                            myLine += table[key];
                            dupe += table[key];
                            pos += i;
                            found = true;
                            isLetter = true;
                            break;
                        }
                    }

                    found = false;
                }

                if (!found)
                {
                    if (isLetter)
                    {
                        myLine += "\n";
                        dupe += "\n";
                    }

                    if (doDupe)
                        myLine += dupe;

                    dupe = "";
                    myLine += "<$" + theLine[pos].ToString("X2") + ">";
                    pos++;

                    isLetter = false;
                }
            }

            return myLine;
        }

        static string GetPrettyLine(string myLine, string newLine)
        {
            string prettyLine = "";
            foreach (string portion in myLine.Split(new string[] { newLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (portion[0] != '<')
                {
                    prettyLine += "//";
                }

                for (int i = 0; i < portion.Length; i++)
                {
                    if (portion[i] == '<')
                    {
                        if (i - 1 > 0 && portion[i - 1] != '>')
                        {
                            prettyLine += "\n//";
                        }
                    }

                    prettyLine += portion[i];

                    if (portion[i] == '>')
                    {
                        if (i + 1 < portion.Length && portion[i + 1] != '<')
                        {
                            prettyLine += "\n//";
                        }
                    }
                }


                prettyLine += newLine + "\n";
            }

            prettyLine = prettyLine.Trim();
            if (prettyLine.Length > 0)
            {
                prettyLine = prettyLine.Substring(0, prettyLine.Length - 3);
            }

            return prettyLine;
        }
        static List<Ptr> FindPointers(BinaryReader block, uint start, uint end, uint textStart, uint textEnd, uint offset)
        {
            List<Ptr> ptrs = new List<Ptr>();

            block.BaseStream.Seek(start, SeekOrigin.Begin);
            while (block.BaseStream.Position + 4 < end)
            {
                if (block.BaseStream.Position == 0x00029EDC)
                {
                    int boopme = 0;
                }
                uint ptr = block.ReadUInt32();

                if ((ptr & 0xFFF00000) == 0x00400000 && (ptr - offset) < textEnd && (ptr - offset) >= textStart && ptr % 2 == 0)
                {
                    long curPos = block.BaseStream.Position;
                    block.BaseStream.Seek((ptr - offset) - 2, SeekOrigin.Begin);
                    ushort isZero = block.ReadUInt16();
                    block.BaseStream.Seek(curPos, SeekOrigin.Begin);

                    if (isZero == 0)
                    {
                        Ptr thisPtr = ptrs.Where(p => p.ptr == ptr - offset).SingleOrDefault();
                        if (thisPtr != null)
                        {
                            thisPtr.pos.Add((int)(block.BaseStream.Position - 4));
                        }
                        else
                        {
                            Ptr newPtr = new Ptr();
                            newPtr.ptr = ptr - offset;
                            newPtr.orig = ptr;
                            newPtr.pos.Add((int)(block.BaseStream.Position - 4));
                            ptrs.Add(newPtr);
                        }
                    }
                }
                else
                {
                    block.BaseStream.Seek(-3, SeekOrigin.Current);
                }
            }

            return ptrs;
        }

        static void WriteStrings(List<Ptr> ptrs, StreamWriter script_txt, BinaryReader script_bin, Dictionary<string, string> table, uint scr_end)
        {

            if (ptrs.Count > 0)
            {
                //ptrs = ptrs.OrderBy(x => x.ptr).ToList();

                bool has_text = false;

                List<string> found = new List<string>();

                script_txt.Write("#VAR(Table, TABLE)\n");
                script_txt.Write("#ADDTBL(\"sjis.tbl\", Table)\n");
                script_txt.Write("#ACTIVETBL(Table)\n");
                script_txt.Write("#VAR(PTR, CUSTOMPOINTER)\n");
                script_txt.Write("#CREATEPTR(PTR, \"LINEAR\", -" + 0x400000 + ", 32)\n");
                //script_txt.Write("#JMP($" + ptr.ptr.ToString("X8") + ")\n\n");

                for (int i = 0; i < ptrs.Count; i++)
                {

                    if (ptrs[i].orig == 0x4B6E20)
                    {
                        int boopme = 0;
                    }
                    uint length = (i + 1 < ptrs.Count) ? ptrs[i + 1].ptr - ptrs[i].ptr : scr_end - ptrs[i].ptr;

                   

                    script_bin.BaseStream.Seek(ptrs[i].ptr, SeekOrigin.Begin);




                    List<byte> letters = new List<byte>();
                    while (true)
                    {
                        if (script_bin.BaseStream.Position >= script_bin.BaseStream.Length)
                            break;

                        ushort letter = script_bin.ReadUInt16();
                        if (letter == 0x00)
                            break;

                        letters.AddRange(BitConverter.GetBytes(letter));
                    }

                    long resetPos = script_bin.BaseStream.Position;

                    //find warn position...
                    while (true)
                    {
                        if (script_bin.BaseStream.Position >= script_bin.BaseStream.Length)
                            break;

                        ushort isNotZero = script_bin.ReadUInt16();
                        if (isNotZero != 0)
                            break;
                    }

                    long warnPos = script_bin.BaseStream.Position - 3; // We still need to leave 1 zero left...

                    script_bin.BaseStream.Seek(resetPos, SeekOrigin.Begin);

                    //string myLine = GetEncodedLine(letters.ToArray(), table);

                    //letters.RemoveAt(letters.Count - 1); // Remove the 0

                    if (letters.Count > 0)
                    {
                        string myLine = Encoding.Unicode.GetString(letters.ToArray());
                        bool hasJapanese = false;
                        foreach(char c in myLine)
                        {
                            if (c >= 0x7F)
                            {
                                hasJapanese = true;
                                break;
                            }
                        }

                        if (hasJapanese)
                        {
                            if (myLine.IndexOf("@") != 0 && !myLine.Contains(".wav") && !myLine.Contains(".agf") && !myLine.Contains(".bmp") && !myLine.Contains(".aog"))
                            {
                                //script_txt.Write("#JMP($" + ptrs[i].ptr.ToString("X8") + ")\n\n");

                                // Write text peice
                                //script_txt.Write("// Ptr: " + (ptrs[i].text_ptr - ptrs[i].offset).ToString("X8") + "\n");
                                //script_txt.Write("// Ptr: " + ptrs[i].ptr.ToString("X8") + "\n");

                                script_txt.Write("//PTR:");
                                foreach (int pos in ptrs[i].pos)
                                {
                                    //script_txt.Write("#WRITE(PTR, $" + pos.ToString("X8") + ")\n");
                                    script_txt.Write(pos.ToString("X8") + "|");
                                }
                                script_txt.WriteLine();

                                script_txt.WriteLine("//" + myLine.Replace("\n", "\n//"));
                                script_txt.WriteLine(myLine);
                                //script_txt.Write(myLine.Replace("//", ""));
                                script_txt.WriteLine();
                                script_txt.WriteLine();

                                //script_txt.Write("#WARN($" + warnPos.ToString("X8") + ", \"WARNME\")\n");
                            }
                        }
                    }
                }
            }
            else
            {
                int break_me = 0;
            }
        }

        class Block
        {
            public uint ptr_start;
            public uint ptr_end;
            public uint scr_start;
            public uint scr_end;
            public uint offset;
        }

        static void Main(string[] args)
        {
            string[] table_entries = File.ReadAllLines("sjis.tbl", Encoding.GetEncoding("SJIS"));
            Dictionary<string, string> table = new Dictionary<string, string>();
            foreach (string table_entry in table_entries)
            {
                if (table_entry.Length > 0)
                {
                    table[table_entry.Split('=')[0]] = table_entry.Split('=')[1];
                }
            }

            BinaryReader rom = new BinaryReader(File.OpenRead("Bunny3.exe"));

            List<Block> blocks = new List<Block>()
            {
                new Block() { ptr_start = 0, ptr_end = (uint)rom.BaseStream.Length, scr_start = 0xAF110, scr_end = 0xC136A,  offset = 0x401200},
                //new Block() { ptr_start = 0, ptr_end = (uint)rom.BaseStream.Length, scr_start = 0x10E000, scr_end = 0x1138A0,  offset = 0x401200},
            };


            foreach (Block block in blocks)
            {
                List<Ptr> ptrs = FindPointers(rom, block.ptr_start, block.ptr_end, block.scr_start, block.scr_end, block.offset);

                if (File.Exists(block.scr_start.ToString("X8") + ".txt"))
                    File.Delete(block.scr_start.ToString("X8") + ".txt");

                StreamWriter script_txt = new StreamWriter(block.scr_start.ToString("X8") + ".txt", false, Encoding.Unicode);

                WriteStrings(ptrs, script_txt, rom, table, block.scr_end);


                script_txt.Close();
            }
        }
    }
}
