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

ushort[] Compress(string s)
{
    string[] words = s.Split(' ');
    ushort[] compressed = new ushort[words.Length];
    for (int i = 0; i < words.Length; i++)
    {
        string lower = wordsArr[i].ToLower(); // TODO handle uppercase, somehow?
        if (wordMap.TryGetValue(words[i], out ushort index))
        {
            compressed[i] = index;
        }else
        {
            compressed[i] = 65535;
        }
    }

    return compressed;
}

string Decompress(ushort[] compressed)
{
    StringBuilder sb = new StringBuilder();
    foreach (ushort index in compressed)
    {
        if (index == 65535)
        {
            sb.Append("UNKNOWN ");
        }
        else if (index < wordsArr.Length)
        {
            sb.Append(wordsArr[index] + " ");
        }
        else
        {
            sb.Append("OUT_OF_BOUNDS ");
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
ushort[] compressed = Compress(text);
// write the compressed data to a file
string compressedFilePath = Path.ChangeExtension(filePath, ".eng");
File.WriteAllBytes(compressedFilePath, compressed.SelectMany(BitConverter.GetBytes).ToArray());
// inform the user
Console.WriteLine($"Compressed {new FileInfo(filePath).Length} bytes to {compressed.Length * sizeof(ushort)} bytes.");
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
// read the compressed data
ushort[] compressedData;
using (var fs = new FileStream(decompressFilePath, FileMode.Open, FileAccess.Read))
{
    int length = (int)fs.Length / sizeof(ushort);
    compressedData = new ushort[length];
    byte[] buffer = new byte[sizeof(ushort)];
    for (int i = 0; i < length; i++)
    {
        fs.ReadExactly(buffer, 0, sizeof(ushort));
        compressedData[i] = BitConverter.ToUInt16(buffer, 0);
    }
}

// decompress the data
string decompressedText = Decompress(compressedData);
// write the decompressed text to a file
string decompressedFilePath = FileNameNoOverwrite(Path.ChangeExtension(decompressFilePath, ".txt"));
File.WriteAllText(decompressedFilePath, decompressedText);
// inform the user
Console.WriteLine($"Decompressed {compressedData.Length * sizeof(ushort)} bytes to {new FileInfo(decompressedFilePath).Length} bytes ({decompressedText.Length} characters).");
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