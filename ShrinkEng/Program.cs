using System.Numerics;
using System.Reflection;
using System.Text;

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
// wordsArr will be around 370k words
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
    var compressed = new List<byte>(words.Length * sizeof(ushort));
    bool utfBlock = false;
    for (int i = 0; i < words.Length; i++)
    {
        string lower = words[i].ToLower(); // TODO handle uppercase, somehow?
        if (utfBlock)
        {
            string utfWord = words[i];
            if (utfWord.Length == 0) // if the length is 0, it must be a space, since we split by space
            {
                utfBlock = false; // reset the UTF block
                // see just how many spaces we have without modifying i
                uint j = 1;
                while (i + j < words.Length && words[i + j].Length == 0)
                {
                    j++;
                }
                compressed.AddRange(Encode7BitVarUInt(j)); ;
                while (j > 0)
                {
                    compressed.Add((byte) ' ');
                    j--;
                }
                continue;
            }
            // Write utf bytes of this unknown word to the compressed array
            byte[] utfBytes = Encoding.UTF8.GetBytes(utfWord);
            // First, signal the length of the UTF block to the decompressor with a variable length 7 bit integer
            compressed.AddRange(Encode7BitVarUInt((uint) utfBytes.Length)); // Long length is probably not necessary lol. Just in case...
            compressed.AddRange(utfBytes);
            utfBlock = false;
        }else if (wordMap.TryGetValue(lower, out var index))
        {
            if (i + 1 < words.Length && !wordMap.ContainsKey(words[i+1].ToLower()))
            {
                utfBlock = true;
                // set first bit of index to 1 to indicate UTF block on next word to the decompressor
                index = (ushort)(index | 0x8000);
                byte[] bytes = BitConverter.GetBytes(index);
                compressed.Add(bytes[0]);
                compressed.Add(bytes[1]);
            }
            else
            {
                // Write the short to the current index of the byte array + update index (can't simply set the short since it's a byte array)
                byte[] bytes = BitConverter.GetBytes(index);
                compressed.Add(bytes[0]);
                compressed.Add(bytes[1]);
            }
        }else
        {
            throw new Exception("Word not found in dictionary: " + lower + ", but UTF block was not enabled. \n" +
                                "This should not happen, please report this bug to the developer.");
        }
    }

    return compressed.ToArray();
}

string Decompress(byte[] compressed)
{
    StringBuilder sb = new StringBuilder();
    int i = 0;
    bool utfBlock = false;
    
    while (i < compressed.Length)
    {
        if (utfBlock)
        {
            // Read the length of the UTF block using variable-length encoding
            uint utfLength = Decode7BitVarUInt(compressed, ref i);
            if (i + utfLength <= compressed.Length)
            {
                byte[] utfBytes = new byte[utfLength];
                Array.Copy(compressed, i, utfBytes, 0, (int)utfLength);
                i += (int)utfLength;
                
                string utfWord = Encoding.UTF8.GetString(utfBytes);
                sb.Append(utfWord + " ");
            }
            utfBlock = false;
        }
        else
        {
            if (i + 1 < compressed.Length)
            {
                ushort index = BitConverter.ToUInt16(compressed, i);
                i += 2;
                // check if the high bit is set (UTF block flag)
                if ((index & 0x8000) != 0)
                {
                    utfBlock = true;
                    index = (ushort)(index & 0x7FFF); // Clear the UTF block flag
                }
                // add word from dictionary
                if (index < wordsArr.Length)
                {
                    sb.Append(wordsArr[index] + " ");
                }
                else
                {
                    sb.Append("OUT_OF_BOUNDS ");
                }
            }
            else
            {
                // we're done! unfortunately we don't have a full short for this word and the UTF block wasn't enabled.
                Console.WriteLine("Warning: Incomplete data at the end of the compressed file. "
                                + "This may indicate a corrupted file or an error in compression.");
                
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
