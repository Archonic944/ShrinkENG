# ShrinkENG Natural Language Compression <img src="/images/shrinkeng.png" height=70>

ShrinkENG is a tool for compressing English text by storing each word as an index of a word within a dictionary. 
For capitalization, punctuation, and whitespace other than a space, static "operators" are declared in a byte before the word (if necessary).
ShrinkENG falls back automatically on UTF-8 encoding for words not in the dictionary.

Theoretically, ShrinkENG could be used to compress 
languages other than English, but it would require a new dictionary and new operator code.

I think of ShrinkENG as less of a compression algorithm and more of a "bytecode generator" for English,
since it's not traditionally compressing by mathematical means, but rather translating English
into a more compact representation. 

It is actually recommended to use ShrinkENG in conjunction with a
traditional compression algorithm like zlib or LZMA for maximum compression 
(first compress with ShrinkENG, then compress the output with zlib or LZMA).

# Usage

The files are available in the Releases section of the GitHub repository. Find it on the right.

**On macOS**: You have to do some extra stuff. Copy the app in Finder (cmd + c), then open terminal, then type:

`xattr -d com.apple.quarantine` then space, then paste, then enter. Now try running the app (right click => open just to be safe).

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

# Advantages Over ZIP
Using ShrinkENG to compress English text instead of ZIP compression has several advantages:
- **Compression Ratio**: ShrinkENG usually compresses English text to around the same size as ZIP. Combining it with ZIP results in a smaller size than just ZIP.
- **Streamable**: ShrinkENG is designed to be streamable, meaning that you can start from either the front or back of the stream and decompress an arbitrary number of words.
*Note: starting from the middle of the stream is technically possible but introduces challenges with keeping track of operators and UTF-8 fallbacks.*
- **Lightweight Decompression**: ShrinkENG is designed to be lightweight, and could probably be used to compress and decompress text (such as user messages) on the fly.

***Everything's better when we work as a team...***<br>Using ShrinkENG *with* ZIP results in the lowest file size.

# Dictionary

The dictionary is a line-separated text file. It contains the 25,000
most common English words, [dumped from wordfreq](https://github.com/aparrish/wordfreq-en-25000/blob/main/wordfreq-en-25000-log.json).

They are sorted by frequency. This saves space because word bytes are stored
as variable length integers, so the smaller the index, the fewer bytes it takes to store.

