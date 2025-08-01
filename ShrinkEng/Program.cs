using System.Numerics;
using System.Reflection;
using System.Text;
using ShrinkEng;

// Initialization of words list (about 25k words) from embedded resource
var assembly = Assembly.GetExecutingAssembly();
var resourceName = "ShrinkEng.resources.wordfreq-en-25000.txt";
const byte EXT_UTF_FLAG = 0x20; // bit5 in extension denotes UTF block (used in WritePackedOps and ReadPackedOps)
string[] wordsArr;
using (var stream = assembly.GetManifestResourceStream(resourceName))
{
    if (stream == null)
    {
        Console.WriteLine("Resource not found.");
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
Dictionary<string, ushort> wordMap = new Dictionary<string, ushort>();
for (ushort i = 0; i < wordsArr.Length; i++)
{
    string word = wordsArr[i].Trim();
    if (!string.IsNullOrEmpty(word))
    {
        wordMap.TryAdd(word, i);
    }
}

byte[] Compress(string s)
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

string Decompress(byte[] compressed)
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


// ask the user for a file path to compress
// ask the user if they want to compress or decompress
Console.WriteLine("Welcome to ShrinkEng!");
compressordecompress:
Console.WriteLine("Do you want to compress or decompress a file? (c/d)");
if (Console.ReadLine()?.ToLower() is { } choice)
{
    if (choice is "d" or "decompress")
    {
        goto decompress;
    } else if (choice is "c" or "compress")
    {
        goto compress; // Yes I know this particular goto is redundant just LET ME HAVE THIS
    }else
    {
        Console.WriteLine("Invalid choice. Please enter 'c' to compress or 'd' to decompress.");
        goto compressordecompress;
    }
}
else
{
    goto compressordecompress;
}
compress:
Console.WriteLine("Enter the file path to compress:");
string filePath = ProcessFilepathInput(Console.ReadLine());
if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
{
    Console.WriteLine("Invalid file path: " + filePath);
    goto compress;
}
// read the file
string text = File.ReadAllText(filePath);
// compress the text
byte[] compressed = Compress(text);
// write the compressed data to a file
string compressedFilePath = Path.ChangeExtension(filePath, ".eng");
File.WriteAllBytes(compressedFilePath, compressed);
// inform the user
Console.WriteLine($"Compressed {new FileInfo(filePath).Length} bytes to {compressed.Length} bytes.");
Console.WriteLine($"Compressed file saved to: {compressedFilePath}");
goto compressordecompress;

decompress:
Console.WriteLine("Enter the file path to decompress:");
string decompressFilePath = ProcessFilepathInput(Console.ReadLine());
if (string.IsNullOrEmpty(decompressFilePath) || !File.Exists(decompressFilePath))
{
    Console.WriteLine("Invalid file path.");
    goto decompress;
}
// read the compressed data as bytes
byte[] compressedData = File.ReadAllBytes(decompressFilePath);

// decompress the data
string decompressedText = Decompress(compressedData);
// write the decompressed text to a file
string decompressedFilePath = FileNameNoOverwrite(Path.ChangeExtension(decompressFilePath, ".txt"));
File.WriteAllText(decompressedFilePath, decompressedText);
// inform the user
Console.WriteLine($"Decompressed {compressedData.Length} bytes to {new FileInfo(decompressedFilePath).Length} bytes ({decompressedText.Length} characters).");
Console.WriteLine($"File path: {decompressedFilePath}");
goto compressordecompress;

string FileNameNoOverwrite(string path)
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

string ProcessFilepathInput(string? inp)
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
byte[] Encode7BitVarUInt(uint value)
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
uint Decode7BitVarUInt(byte[] bytes, ref int index)
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

// Packed operator state (primary + optional extension)
static void WritePackedOps(List<byte> dst, List<Operator> ops)
{
    // Detect UTF block
    bool isUtf = ops.Count > 0 && ReferenceEquals(ops[0], Operator.FlagUTFBlock);

    byte casing = 0; // 0 none, 1 Capitalize, 2 AllCaps
    byte punct  = 0; // 0 none, 1 ., 2 ,, 3 ?, 4 !, 5 ..., 6 :, 7 ;
    byte quotes = 0; // bits: 1=open, 2=close (3=both)
    byte ws     = 0; // 0 none, 1 \n, 2 \n\n, 3 \t

    if (!isUtf)
    {
        if (ops.Contains(Operator.AllCaps)) casing = 2;
        else if (ops.Contains(Operator.Capitalize)) casing = 1;

        if (ops.Contains(Operator.Period)) punct = 1;
        else if (ops.Contains(Operator.Comma)) punct = 2;
        else if (ops.Contains(Operator.Question)) punct = 3;
        else if (ops.Contains(Operator.Exclamation)) punct = 4;
        else if (ops.Contains(Operator.Ellipses)) punct = 5;
        else if (ops.Contains(Operator.Colon)) punct = 6;
        else if (ops.Contains(Operator.Semicolon)) punct = 7;

        if (ops.Contains(Operator.OpenQuote))  quotes |= 0x1;
        if (ops.Contains(Operator.CloseQuote)) quotes |= 0x2;

        if (ops.Contains(Operator.Tab)) ws = 3;
        else if (ops.Contains(Operator.DoubleLineBreak)) ws = 2;
        else if (ops.Contains(Operator.SingleLineBreak)) ws = 1;
    }

    // Primary: [b7 ext][b6..5 casing][b4..2 punct][b1..b0 quotes]
    byte primary = 0;
    primary |= (byte)(casing << 5);
    primary |= (byte)(punct  << 2);
    primary |= (byte)(quotes);

    bool hasExt = isUtf || ws != 0;   // need extension if UTF or whitespace present
    if (hasExt) primary |= 0x80;
    dst.Add(primary);

    if (hasExt)
    {
        // Extension: [b7..b6 whitespace][b5 utfFlag][b4..b0 reserved=0]
        byte ext = 0;
        ext |= (byte)(ws << 6);
        if (isUtf) ext |= EXT_UTF_FLAG;
        dst.Add(ext);
    }
}


static List<Operator> ReadPackedOps(byte[] bytes, ref int index)
{
    var ops = new List<Operator>();

    byte primary = bytes[index++];

    byte casing = (byte)((primary >> 5) & 0x3);
    byte punct  = (byte)((primary >> 2) & 0x7);
    byte quotes = (byte)(primary & 0x3);
    bool hasExt = (primary & 0x80) != 0;

    byte ws = 0;
    bool isUtf = false;
    if (hasExt)
    {
        byte ext = bytes[index++];
        ws    = (byte)((ext >> 6) & 0x3);
        isUtf = (ext & EXT_UTF_FLAG) != 0;
    }

    if (isUtf)
    {
        ops.Add(Operator.FlagUTFBlock);
        return ops;
    }

    // Rebuild in canonical order: casing, open-quote, punctuation, close-quote, whitespace
    if (casing == 2) ops.Add(Operator.AllCaps);
    else if (casing == 1) ops.Add(Operator.Capitalize);

    if ((quotes & 0x1) != 0) ops.Add(Operator.OpenQuote);

    switch (punct)
    {
        case 1: ops.Add(Operator.Period); break;
        case 2: ops.Add(Operator.Comma); break;
        case 3: ops.Add(Operator.Question); break;
        case 4: ops.Add(Operator.Exclamation); break;
        case 5: ops.Add(Operator.Ellipses); break;
        case 6: ops.Add(Operator.Colon); break;
        case 7: ops.Add(Operator.Semicolon); break;
    }

    if ((quotes & 0x2) != 0) ops.Add(Operator.CloseQuote);

    switch (ws)
    {
        case 1: ops.Add(Operator.SingleLineBreak); break;
        case 2: ops.Add(Operator.DoubleLineBreak); break;
        case 3: ops.Add(Operator.Tab); break;
    }

    return ops;
}

void WriteOpsData(List<byte> compressedStream, List<Operator> ops)
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

List<Operator> ReadOps(byte[] bytes, ref int index)
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
