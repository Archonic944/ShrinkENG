# ShrinkENG Natural Language Compression <img src="/images/shrinkeng.png" height=70>

ShrinkENG is a tool for compressing English text by storing each word as an index of a word within a dictionary. 
For capitalization, punctuation, and whitespace other than a space, static "operators" are declared in a byte before the word (if necessary).
ShrinkENG falls back automatically on UTF-8 encoding for words not in the dictionary.

If you'd like to **read through the code**, the two main files are [Operator.cs](/Operator.cs) and [ShrinkEngine.cs](/ShrinkEngine.cs).

Theoretically, ShrinkENG could be used to compress 
languages other than English, but it would require a new dictionary and new operator code.

ShrinkENG is less of a compression algorithm and more of a "bytecode generator" for English,
since it's not compressing by mathematical means, but rather translating English
into a more compact representation. 

It is recommended to use ShrinkENG in conjunction with a
traditional compression algorithm like zlib or LZMA for maximum compression 
(first compress with ShrinkENG, then compress the output with zlib or LZMA).

# Usage

Simply visit https://shrinkeng.vercel.app/, and upload a file to compress!

Please use a raw text file (txt) instead of a pdf, docx, or rtf.

# Compression Ratio
The compression ratio of ShrinkENG is about 50% for a large English text corpus.
If there are many words not in the dictionary, or a large amount of punctuation, or, say,
an uncommon text structure that causes a lot of UTF-8 fallbacks, the compression ratio will be lower.

<img src="https://i.ibb.co/hx5HGmjc/readme-img1.png" width="600">

*The above image shows War and Peace compressed with ShrinkENG only, which results in a 52.9% file size reduction.*

The same document (War and Peace.txt) compressed with ZIP results in 1.20MB. ShrinkENG + ZIP results in 1.06MB. Here is a table:

| Compression Method | File Size | Compression Ratio |
|--------------------|-----------|-------------------|
| ShrinkENG          | 1.42MB    | 52.9%             |
| ZIP                | 1.20MB    | 61.2%             |
| ShrinkENG + ZIP    | 1.06MB    | 67.1%             |

# Operators

Simply replacing each word with its index wouldn't work. What about capital letters? Punctuation? What about words that are not in the dictionary? That's why each word in an ENG file can have multiple "operators" encoded into it. Operators do things like capitalize the first letter of the word or add quotation marks. The index of an operator corresponds to some code in [Operator.cs](/Operator.cs), which applies the operator. There can be a maximum of 127 operators in the current format, of which I have only used about 12.

# Dictionary

The dictionary is a line-separated text file. It contains the 25,000
most common English words, [dumped from wordfreq](https://github.com/aparrish/wordfreq-en-25000/blob/main/wordfreq-en-25000-log.json).

They are sorted by frequency. This saves space because word bytes are stored
as variable length integers, so the smaller the index, the fewer bytes it takes to store.

