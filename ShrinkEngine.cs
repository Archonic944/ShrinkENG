using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using ShrinkEng;
public class ShrinkEngine {
    static Dictionary<string, ushort> wordMap = new Dictionary<string, ushort>();
    static string[] wordsArr;

    static ShrinkEngine()
    {
        Init();
    }

    // Flags for packed operators
    const byte EXT_UTF_FLAG = 0x20;
    const byte EXT_SEMI_FLAG = 0x10;
    static void Init()
    {
        // Initialization of words list (about 25k words) from embedded resource
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "ShrinkEng.resources.wordfreq-en-25000.txt";
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                Console.WriteLine($"Dictionary resource {resourceName} not found.");
                return;
            }

            using (var reader = new StreamReader(stream))
            {
                string wordList = reader.ReadToEnd();
                wordsArr = wordList.Split("\n");
                // Process the word list as needed
                Console.WriteLine($"Loaded {wordsArr.Length} words.");
            }
        }
        // wordsArr will be around 25k words
        // Generate a hashmap of words to ushort

        for (ushort i = 0; i < wordsArr.Length; i++)
        {
            string word = wordsArr[i].Trim();
            if (!string.IsNullOrEmpty(word))
            {
                wordMap.TryAdd(word, i);
            }
        }
    }

    public static byte[] Compress(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return [];
        }

        var words = ShrinkUtils.SplitKeepTrailingDelimiter(s, "@\n\n|[\n\t ]");
        // Clean spaces from ends of words without removing other whitespace
        for (int i = 0; i < words.Count; i++)
        {
            if(words[i].EndsWith(' ')) words[i] = words[i][..^1];
        }
        
        var compressed = new List<byte>(words.Count * sizeof(ushort) + 8);

        // Determine initial ops
        var ops = Operator.MinApplicableOps(words[0], wordMap);
        bool startsWithOps = ops.Count > 0;
        // Write info byte: bit0 indicates initial operator data follows
        compressed.Add(startsWithOps ? (byte)1 : (byte)0);
        if (startsWithOps)
        {
            WritePackedOps(compressed, ops);
        }

        var idx = ushort.MaxValue;
        for (int i = 0; i < words.Count; i++)
        {
            bool hasOps = ops.Count > 0;
            bool inUtf = hasOps && ops[0] == Operator.FlagUTFBlock;
            if (inUtf)
            {
                // UTF block: write payload length and bytes
                var payload = Encoding.UTF8.GetBytes(words[i]);
                compressed.AddRange(Encode7BitVarUInt((uint)payload.Length));
                compressed.AddRange(payload);
            }
            else if(hasOps)
            {
                // Clean and lookup
                string cleaned = words[i];
                foreach (var op in ops)
                {
                    cleaned = op.Clean(cleaned);
                }
                if (!wordMap.TryGetValue(cleaned, out idx))
                {
                    throw new Exception($"Word not found in dictionary: '{words[i]}' cleaned to '{cleaned}'");
                }
                //compressed.AddRange(payload);
            }
            else
            {
                if(!wordMap.TryGetValue(words[i], out idx))
                {
                    throw new Exception($"Word not found in dictionary: '{words[i]}'");
                }
            }
            
            // Prepare ops for next word
            bool nextHasOps = false;
            List<Operator> nextOps = new List<Operator>();
            if (i + 1 < words.Count)
            {
                nextOps = Operator.MinApplicableOps(words[i + 1], wordMap);
                if (nextOps.Count > 0) nextHasOps = true;
                if (inUtf)
                {
                    if(nextHasOps)
                    {
                        compressed.Add(1);
                        WritePackedOps(compressed, nextOps);
                    }
                    else
                    {
                        compressed.Add(0);
                    }

                    goto finished;
                }
            }
            WriteIndexVarUInt(compressed, idx, nextHasOps);
            if (nextHasOps)
            {
                WritePackedOps(compressed, nextOps);
            }
            finished:
            ops = nextOps;
        }

        return compressed.ToArray();
    }

     public static string Decompress(byte[] compressed)
    {
        if (compressed == null || compressed.Length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        int i = 0;

        // Read info byte
        byte info = compressed[i++];
        bool startsWithOps = (info & 1) == 1;
        List<Operator> ops = new List<Operator>();

        if (startsWithOps)
        {
            ops = ReadPackedOps(compressed, ref i);
        }

        while (i < compressed.Length)
        {
            if (ops.Count > 0 && ops[0] == Operator.FlagUTFBlock)
            {
                // UTF block
                uint length = Decode7BitVarUInt(compressed, ref i);
                if (i + length > compressed.Length)
                    throw new Exception("Unexpected end of UTF block data.");

                string word = Encoding.UTF8.GetString(compressed, i, (int)length);
                i += (int)length;
                sb.Append(word);
                if (i < compressed.Length && word.TrimEnd().Length == word.Length)
                {
                    sb.Append(' ');
                }

                // Sentinel: 1 means next ops, 0 means clear
                if (i < compressed.Length)
                {
                    byte sentinel = compressed[i++];
                    if (sentinel == 1)
                        ops = ReadPackedOps(compressed, ref i);
                    else
                        ops.Clear();
                }
            }
            else
            {

                uint idx32 = ReadIndexVarUInt(compressed, ref i, out bool hasOpData);
                ushort idx = (ushort)idx32;  // only safe if |dict| is < 65536

                string word = idx < wordsArr.Length ? wordsArr[idx] : "OUT_OF_BOUNDS";

                bool noSpace = false;
                // Apply operators
                foreach (var op in ops)
                {
                    word = op.Apply(word);
                    noSpace = noSpace || op.OverwriteTrailingSpace;
                }

                sb.Append(word);

                if (hasOpData)
                {
                    ops = ReadPackedOps(compressed, ref i);
                }
                else
                {
                    ops.Clear();
                }

                if (i < compressed.Length)
                {
                    if (!noSpace)
                    {
                        sb.Append(' ');
                    }
                }
            }
        }

        return sb.ToString();
    }
    
    public static string FileNameNoOverwrite(string path)
    {
        string directory = Path.GetDirectoryName(path) ?? "";
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        int count = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(directory, $"{fileNameWithoutExtension}({count++}){extension}");
        }
        return path;
    }

    public static string ProcessFilepathInput(string? inp)
    {
        if (string.IsNullOrEmpty(inp))
        {
            return string.Empty;
        }
        inp = inp.Trim();
        if (inp.StartsWith('~'))
        {
            string subPath = inp[1..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            inp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), subPath);
        }
        return Path.GetFullPath(inp);
    }

    // Encodes a ulong as a 7-bit variable length unsigned int.
    // Each byte: bits 0-6 are data, bit 7 is continuation (1 if more bytes follow, 0 if last byte).
    static byte[] Encode7BitVarUInt(uint value)
    {
        var bytes = new List<byte>();
        do
        {
            byte b = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                b |= 0x80; // Set continuation bit
            }
            bytes.Add(b);
        } while (value != 0);
        return bytes.ToArray();
    }

    // Warning: also advances the index
    static uint Decode7BitVarUInt(byte[] bytes, ref int index)
    {
        uint value = 0;
        int shift = 0;
        while (index < bytes.Length)
        {
            byte b = bytes[index++];
            value |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) // If the continuation bit is not set, we're done
            {
                break;
            }
            shift += 7;
        }
        return value;
    }

    // Write a 7-bit varuint, reserving bit6 of the last byte to signal "ops follow"
    static void WriteIndexVarUInt(List<byte> dst, uint value, bool opsFollow)
    {
        // emit full 7-bit chunks with continuation bit
        while (value >= 0x40)  // leave 6 bits for last byte
        {
            dst.Add((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        // final byte: 0x80=0 (end), bit6=opsFollow, bits0–5=data
        byte last = (byte)(value & 0x3F);
        if (opsFollow) last |= 0x40;
        dst.Add(last);
    }

    // Read a 7-bit varuint; on the last byte, extract opsFollow from bit6
    static uint ReadIndexVarUInt(byte[] src, ref int idx, out bool opsFollow)
    {
        uint value = 0;
        int shift = 0;
        while (true)
        {
            byte b = src[idx++];
            if ((b & 0x80) != 0)
            {
                // continuation
                value |= (uint)(b & 0x7F) << shift;
                shift += 7;
            }
            else
            {
                // last byte
                opsFollow = (b & 0x40) != 0;
                value |= (uint)(b & 0x3F) << shift;
                break;
            }
        }
        return value;
    }

    /*
    Packed Operator State Schema (will live here until I have documentation):

    Primary Byte (1 byte):
    +---------+---------+---------+--------+
    | 7: Ext  | 6 - 5:  | 4 - 2:  | 1 - 0: |
    | (flag)  | Casing  | Punct   | Quotes |
    +---------+---------+---------+--------+

    - Bit 7: Extension byte present (1 = yes, 0 = no)
    - Bits 6-5: Casing
        00 = none
        01 = Capitalize first letter
        10 = ALL CAPS
        11 = reserved
    - Bits 4-2: Punctuation or single LF
        000 = none
        001 = period '.'
        010 = comma ','
        011 = question '?'
        100 = exclamation '!'
        101 = ellipses '...'
        110 = colon ':'
        111 = single line feed '\n'
    - Bits 1-0: Quotes
        00 = none
        01 = open quote
        10 = close quote
        11 = both quotes (rare, can be moved to extension if needed)

    Extension Byte (optional, 1 byte if Bit 7 in primary set):
    +---------+---------+---------+---------+
    | 7 - 6:  | 5: UTF   | 4: Semicolon | 3 - 0: reserved |
    | Whitespace class | Flag    | Flag      |                |
    +---------+---------+---------+---------+

    - Bits 7-6: Whitespace class
        00 = none
        01 = single LF (when punctuation slot is occupied)
        10 = double LF '\n\n'
        11 = tab '\t'
    - Bit 5: UTF block flag (1 = token is raw UTF bytes)
    - Bit 4: Semicolon flag (1 = append semicolon ';')
    - Bits 3-0: Reserved for future extensions or flags

    */

    static void WritePackedOps(List<byte> dst, List<Operator> ops)
    {
        bool isUtf  = ops.Contains(Operator.FlagUTFBlock);
        bool hasSemi= ops.Contains(Operator.Semicolon);

        bool lf1 = ops.Contains(Operator.SingleLineBreak);
        bool lf2 = ops.Contains(Operator.DoubleLineBreak);
        bool tab = ops.Contains(Operator.Tab);

        byte casing = ops.Contains(Operator.AllCaps)       ? (byte)2 :
                      ops.Contains(Operator.Capitalize)    ? (byte)1 : (byte)0;

        byte punct = 0;
             if (ops.Contains(Operator.Period))       punct = 1;
        else if (ops.Contains(Operator.Comma))        punct = 2;
        else if (ops.Contains(Operator.Question))     punct = 3;
        else if (ops.Contains(Operator.Exclamation))  punct = 4;
        else if (ops.Contains(Operator.Ellipses))     punct = 5;
        else if (ops.Contains(Operator.Colon))        punct = 6;
        // semicolon handled via ext flag

        byte wsBits = 0;                 // 0 none,1 LF,2 LF LF,3 tab
        if (lf2)      wsBits = 2;
        else if (tab) wsBits = 3;

        if (punct == 0 && lf1)           // no real punct, put LF in primary
            punct = 7;
        else if (lf1)                    // punctuation already used, LF to ext
            wsBits = 1;

        bool hasExt = isUtf || hasSemi || wsBits != 0;

        byte primary = (byte)(
            (hasExt ? 0x80 : 0) |
            (casing << 5) |
            (punct  << 2) |
            ((ops.Contains(Operator.OpenQuote)  ? 1 : 0) |
             (ops.Contains(Operator.CloseQuote) ? 2 : 0))
        );
        dst.Add(primary);

        if (hasExt)
        {
            byte ext = (byte)(wsBits << 6);
            if (isUtf)   ext |= EXT_UTF_FLAG;
            if (hasSemi) ext |= EXT_SEMI_FLAG;
            dst.Add(ext);
        }
    }

    static List<Operator> ReadPackedOps(byte[] bytes, ref int idx)
    {
        var ops = new List<Operator>();

        byte p   = bytes[idx++];
        bool ext = (p & 0x80) != 0;
        byte casing = (byte)((p >> 5) & 0x3);
        byte punct  = (byte)((p >> 2) & 0x7);
        byte quotes = (byte)( p       & 0x3);

        bool isUtf   = false;
        bool hasSemi = false;
        byte wsBits  = 0;
        if (ext)
        {
            byte e = bytes[idx++];
            wsBits  = (byte)((e >> 6) & 0x3);
            isUtf   = (e & EXT_UTF_FLAG)  != 0;
            hasSemi = (e & EXT_SEMI_FLAG) != 0;
        }

        if (isUtf) { ops.Add(Operator.FlagUTFBlock); return ops; }

        if (casing == 1) ops.Add(Operator.Capitalize);
        else if (casing == 2) ops.Add(Operator.AllCaps);

        if ((quotes & 1) != 0) ops.Add(Operator.OpenQuote);

        switch (punct)
        {
            case 1: ops.Add(Operator.Period);           break;
            case 2: ops.Add(Operator.Comma);            break;
            case 3: ops.Add(Operator.Question);         break;
            case 4: ops.Add(Operator.Exclamation);      break;
            case 5: ops.Add(Operator.Ellipses);         break;
            case 6: ops.Add(Operator.Colon);            break;
            case 7: ops.Add(Operator.SingleLineBreak);  break;
        }

        if ((quotes & 2) != 0) ops.Add(Operator.CloseQuote);

        if      (wsBits == 1) ops.Add(Operator.SingleLineBreak);
        else if (wsBits == 2) ops.Add(Operator.DoubleLineBreak);
        else if (wsBits == 3) ops.Add(Operator.Tab);

        if (hasSemi) ops.Add(Operator.Semicolon);

        return ops;
    }

    static void WriteOpsData(List<byte> compressedStream, List<Operator> ops)
    {
        if (ops.Count == 0) return;
        for (var i = 0; i < ops.Count; i++)
        {
            var opId = (byte)(ops[i].ID & 0x7F); // 0-127
            if (i < ops.Count - 1)
                opId |= 0x80; // Set leftmost bit if more ops follow
            compressedStream.Add(opId);
        }
    }

    static List<Operator> ReadOps(byte[] bytes, ref int index)
    {
        List<Operator> ops = new List<Operator>();
        while (index < bytes.Length)
        {
            byte opId = bytes[index++];
            bool hasNext = (opId & 0x80) != 0; // Check if the leftmost bit is set
            opId &= 0x7F; // Clear the leftmost bit to get the actual ID
            var op = Operator.GetByID(opId);
            ops.Add(op);
            if (!hasNext) break; // If no more ops follow, exit the loop
        }
        return ops;
    }
}