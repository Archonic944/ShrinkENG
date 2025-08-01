using System.Numerics;
using System.Reflection;
using System.Text;
using ShrinkEng;

// Initialization of words list
var assembly = Assembly.GetExecutingAssembly();
var resourceName = "ShrinkEng.resources.wordfreq-en-25000.txt";
var test = "ShrinkEng\nIs Awesome\n";
string textTest = "apple, banana;cherry|date";
// delimiters: comma, semicolon, or pipe
var parts = ShrinkUtils.SplitKeepTrailingDelimiter(textTest, "[,;|]");

// parts: ["apple,", " banana;", "cherry|", "date"]
foreach (var part in parts)
    Console.WriteLine($"[{part}]");
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
// generate a hashmap of words to ushort
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

    string[] words = s.Split(' ');
    var compressed = new List<byte>(words.Length * sizeof(ushort) + 8);

    // Determine initial ops
    var ops = Operator.MinApplicableOps(words[0], wordMap);
    bool startsWithOps = ops.Count > 0;
    // Write info byte: bit0 indicates initial operator data follows
    compressed.Add(startsWithOps ? (byte)1 : (byte)0);
    if (startsWithOps)
    {
        WriteOpsData(compressed, ops);
    }

    for (int i = 0; i < words.Length; i++)
    {
        bool hasOps = ops.Count > 0;
        bool inUtf = hasOps && ops[0] == Operator.FlagUTFBlock;
        byte[] payload;

        if (inUtf)
        {
            // UTF block: write payload length and bytes
            payload = Encoding.UTF8.GetBytes(words[i]);
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
            if (!wordMap.TryGetValue(cleaned, out ushort idx))
            {
                throw new Exception($"Word not found in dictionary: '{words[i]}' cleaned to '{cleaned}'");
            }
            payload = BitConverter.GetBytes(idx);
            //compressed.AddRange(payload);
        }
        else
        {
            if(!wordMap.TryGetValue(words[i], out var idx))
            {
                throw new Exception($"Word not found in dictionary: '{words[i]}'");
            }
            payload = BitConverter.GetBytes(idx);
        }

        // Prepare ops for next word
        List<Operator> nextOps = new List<Operator>();
        if (i + 1 < words.Length)
        {
            nextOps = Operator.MinApplicableOps(words[i + 1], wordMap);
            if (nextOps.Count > 0)
            {
                if (inUtf)
                {
                    compressed.AddRange(payload);
                    // 1 => ops next, 0 => clear
                    compressed.Add(nextOps.Count > 0 ? (byte)1 : (byte)0); // can't do the high bit trick because it's UTF not an ENG short
                }
                else
                {
                    var raw = BitConverter.ToUInt16(payload, 0);
                    raw |= 0x8000; // Set high bit to indicate ops follow
                    compressed.AddRange(BitConverter.GetBytes(raw));
                }
                WriteOpsData(compressed, nextOps);
            }
        }
        else
        {
            compressed.AddRange(payload);
        }

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
        ops = ReadOps(compressed, ref i);
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

            // Sentinel: 1 means next ops, 0 means clear
            if (i < compressed.Length)
            {
                byte sentinel = compressed[i++];
                if (sentinel == 1)
                    ops = ReadOps(compressed, ref i);
                else
                    ops.Clear();
            }
        }
        else
        {
            if (i + 2 > compressed.Length)
                break;

            ushort raw = BitConverter.ToUInt16(compressed, i);
            i += 2;
            bool hasOpData = (raw & 0x8000) != 0;
            ushort idx = (ushort)(raw & 0x7FFF);

            string word = idx < wordsArr.Length ? wordsArr[idx] : "OUT_OF_BOUNDS";

            // Apply operators
            foreach (var op in ops)
            {
                word = op.Apply(word);
            }

            sb.Append(word);

            if (hasOpData)
            {
                ops = ReadOps(compressed, ref i);
            }
            else
            {
                ops.Clear();
            }

            if (i < compressed.Length)
            {
                sb.Append(' ');
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
