using System.Numerics;
using System.Reflection;
using System.Text;
using ShrinkEng;

// Initialization of words list
var assembly = Assembly.GetExecutingAssembly();
var resourceName = "ShrinkEng.resources.wordfreq-en-25000.txt";
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
    string[] words = s.Split(' ');
    //resize vector
    var compressed = new List<byte>(words.Length * sizeof(ushort) + 8);
    // first, write the "info byte"
    // currently only the first bit is used to indicate whether the file starts with a UTF block or not
    // the rest of the bits are reserved for future use
    List<Operator> ops = Operator.MinApplicableOps(words[0], wordMap);
    compressed.Add(ops.Count != 0 ? (byte)1 : (byte)0);
    var utfBlock = false;
    for (uint i = 0; i < words.Length; i++)
    {
        string word = words[i];
        byte[] data = [];
        if (ops.Count > 0)
        {
            if (ops[0] == Operator.FlagUTFBlock)
            {
                utfBlock = true;
                string utfWord = words[i];
                if (utfWord.Length == 0) // if the length is 0, it must be a space, since we split by space
                {
                    // see just how many spaces we have without modifying i
                    uint j = 1;
                    while (i + j < words.Length && words[i + j].Length == 0)
                    {
                        Console.WriteLine("Found " + j + " spaces in a row.");
                        j++;
                    }
                    WriteOpsData(compressed, ops);
                    compressed.AddRange(Encode7BitVarUInt(j-1));
                    i += --j; // make hay while the sun shines (before we reset j, I mean)
                    var spaces = "";
                    while (j > 0)
                    {
                        Console.WriteLine("Adding space to compressed array.");
                        spaces += ' ';
                        j--;
                    }

                    data = Encoding.UTF8.GetBytes(spaces);
                }

                // Write utf bytes of this unknown word to the compressed array
                byte[] utfBytes = Encoding.UTF8.GetBytes(utfWord);
                WriteOpsData(compressed, ops);
                compressed.AddRange(Encode7BitVarUInt((uint)utfBytes.Length));
                data = utfBytes;
            }
            else
            {
                var cleaned = words[i];
                ops.ForEach(op => cleaned = op.Clean(cleaned));
                if(wordMap.TryGetValue(cleaned, out var value))
                {
                    data = BitConverter.GetBytes(value);
                }else
                {
                    throw new Exception($"Word not found in dictionary after cleaning. \nWord before cleaning: {words[i]}, after cleaning: {cleaned}");
                }
            }
        }
        else
        {
            if (wordMap.TryGetValue(words[i], out var value))
            {
                data = BitConverter.GetBytes(value);
            }
            else throw new Exception($"Word not found in dictionary: {words[i]}");
        }
        
        // Calculate next ops
        if (words.Length < i + 1)
        {
            ops = Operator.MinApplicableOps(words[i + 1], wordMap);
            if (ops.Count > 0)
            {
                if (utfBlock)
                {
                    compressed.AddRange(data);
                    compressed.Add(1); // A byte of 1 at the end of a UTF block indicates that operator data is next
                }
                else
                {
                    data[0] &= 0b10000000;
                    compressed.AddRange(data);
                }
            }
            else
            {
                compressed.AddRange(data);
                if(utfBlock) compressed.Add(0);
            }
        }
        
    }
    return compressed.ToArray();
}

string Decompress(byte[] compressed)
{
    StringBuilder sb = new StringBuilder();
    int i = 0;
    
    // Read info byte
    byte info = compressed[i++];
    bool startsWithUTF = (info & 1) == 1;
    
    List<Operator> ops = new List<Operator>();
    if (startsWithUTF)
    {
        ops = ReadOps(compressed, ref i);
    }
    
    while (i < compressed.Length)
    {
        if (ops.Count > 0 && ops[0] == Operator.FlagUTFBlock)
        {
            // Read the length of the UTF block using variable-length encoding
            uint utfLength = Decode7BitVarUInt(compressed, ref i);
            if (i + utfLength <= compressed.Length)
            {
                byte[] utfBytes = new byte[utfLength];
                Array.Copy(compressed, i, utfBytes, 0, (int)utfLength);
                i += (int)utfLength;
                string utfWord = Encoding.UTF8.GetString(utfBytes);
                sb.Append(utfWord);
                if (i < compressed.Length && sb.Length > 0 && !sb.ToString().EndsWith(" "))
                {
                    sb.Append(" ");
                }
            }
            
            // Check if there's operator data next or if we continue with normal words
            if (i < compressed.Length)
            {
                byte nextByte = compressed[i];
                if (nextByte == 1)
                {
                    i++; // Skip the indicator byte
                    ops = ReadOps(compressed, ref i);
                }
                else if (nextByte == 0)
                {
                    i++; // Skip the indicator byte
                    ops.Clear();
                }
                else
                {
                    ops.Clear();
                }
            }
            else
            {
                ops.Clear();
            }
        }
        else
        {
            if (i + 1 < compressed.Length)
            {
                ushort index = BitConverter.ToUInt16(compressed, i);
                i += 2;
                
                // Check if the high bit is set (indicates operator data follows)
                bool hasOperatorData = (index & 0x8000) != 0;
                if (hasOperatorData)
                {
                    index = (ushort)(index & 0x7FFF); // Clear the high bit
                }
                
                // Add word from dictionary
                if (index < wordsArr.Length)
                {
                    string word = wordsArr[index];
                    
                    // Apply operators if they exist
                    if (ops.Count > 0)
                    {
                        foreach (var op in ops)
                        {
                            word = op.Apply(word);
                        }
                    }
                    
                    sb.Append(word);
                    if (i < compressed.Length)
                    {
                        sb.Append(' ');
                    }
                }
                else
                {
                    sb.Append("OUT_OF_BOUNDS ");
                }
                
                // Read operator data if it follows
                if (hasOperatorData)
                {
                    ops = ReadOps(compressed, ref i);
                }
                else
                {
                    ops.Clear();
                }
            }
            else if (i < compressed.Length)
            {
                // Handle single byte at end
                Console.WriteLine("Warning: Incomplete data at the end of the compressed file. "
                                + "This may indicate a corrupted file or an error in compression.");
                break;
            }
        }
    }
    return sb.ToString().TrimEnd();
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
