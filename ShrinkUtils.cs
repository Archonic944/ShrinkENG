using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ShrinkEng;

public class ShrinkUtils
{
    /// <summary>
    /// Non-destructively splits <paramref name="input"/>, leaving every matched
    /// delimiter stuck to the *end* of the preceding chunk.
    /// Supports any regex-compatible delimiter pattern, including multi-char ones.
    /// </summary>
    public static List<string> SplitKeepTrailingDelimiter(
        string input,
        string delimiterPattern)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (string.IsNullOrWhiteSpace(delimiterPattern))
            throw new ArgumentException("Delimiter pattern cannot be empty.", nameof(delimiterPattern));

        // 1) .*? lazily so we stop as soon as a delimiter appears
        // 2) (?:{delimiterPattern}) puts the delimiter *inside* the same match
        // 3) | .+$ mops up the final tail when no delimiter follows
        string pattern = $@"(.*?(?:{delimiterPattern})|.+$)";

        return Regex.Matches(input, pattern)
            .Cast<Match>()
            .Select(m => m.Value) // whole match already includes the delimiter
            .Where(s => s.Length > 0)
            .ToList();
    }
}