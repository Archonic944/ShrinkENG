# ShrinkENG English Language Compression

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

# Compression Ratio
The compression ratio of ShrinkENG is about 50% for a large English text corpus.
If there are many words not in the dictionary, or a large amount of punctuation, or, say,
an uncommon text structure that causes a lot of UTF-8 fallbacks, the compression ratio will be lower.

<img src="https://i.ibb.co/hx5HGmjc/readme-img1.png" width="600">

*The above image shows War and Peace compressed with ShrinkENG only, which results in a 52.9% file size reduction.*

# Dictionary

The dictionary is a line-separated text file. It contains the 25,000
most common English words, [dumped from wordfreq](https://github.com/aparrish/wordfreq-en-25000/blob/main/wordfreq-en-25000-log.json).

They are sorted by frequency. This saves space because word bytes are stored
as variable length integers, so the smaller the index, the fewer bytes it takes to store.

# Usage

(details about MAUI application will go here when it's ready)

