using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bunny_black_3_exe_text_insertion
{
    class Program
    {
        class ScriptEntry
        {
            public List<uint> ptrPos = new List<uint>();
            public string line = "";
            public byte[] encoded;
        }
        static void Main(string[] args)
        {
            string scriptfile = args[0]; // "exe\\trans\\000AF110.txt";
            string inExe = args[1]; // "exe\\ins\\Bunny3.exe";

            string[] text = File.ReadAllLines(scriptfile, Encoding.GetEncoding("SJIS"));
            List<ScriptEntry> entries = new List<ScriptEntry>();

            ScriptEntry entry = new ScriptEntry();
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i].IndexOf("//PTR:") == 0)
                {
                    if (entry.line.Length > 0)
                    {
                        entry.encoded = Encoding.Unicode.GetBytes(entry.line);
                        entries.Add(entry);
                    }

                    entry = new ScriptEntry();
                    string ptrposline = text[i].Substring(text[i].IndexOf(":") + 1);
                    foreach (string ptrpos in ptrposline.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        entry.ptrPos.Add(UInt32.Parse(ptrpos, NumberStyles.HexNumber));
                    }
                }
                else
                {
                    if (!String.IsNullOrEmpty(text[i]) && !text[i].StartsWith("//"))
                    {
                        if (entry.line.Length > 0)
                        {
                            entry.line += Environment.NewLine;
                        }
                        entry.line += text[i];
                    }
                }
            }
            entry.encoded = Encoding.Unicode.GetBytes(entry.line);
            entries.Add(entry);

            //uint offset = 0x401200;
            uint offset = 0x408000;

            BinaryWriter exe = new BinaryWriter(File.OpenWrite(inExe));
            exe.BaseStream.Seek(0x10E000, SeekOrigin.Begin);
            foreach (ScriptEntry sentry in entries)
            {
                long curPos = exe.BaseStream.Position;
                foreach (uint ptrpos in sentry.ptrPos)
                {
                    exe.BaseStream.Seek(ptrpos, SeekOrigin.Begin);
                    exe.Write((uint)(curPos + offset));
                }

                exe.BaseStream.Seek(curPos, SeekOrigin.Begin);

                exe.Write(sentry.encoded);
                exe.Write((byte)0);
                exe.Write((byte)0);
            }
            exe.Close();
        }
    }
}
