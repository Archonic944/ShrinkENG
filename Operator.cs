using System;
using System.Collections.Generic;
using System.Linq;

namespace ShrinkEng;

public class Operator(
    byte id,
    Func<string, bool> isApplied,
    Func<string, string> apply,
    Func<string, string> clean,
    bool requiresLength,
    bool overwriteTrailingSpace)
{
    public byte ID { get; } = id;

    public bool RequiresLength { get; } = requiresLength;

    public bool OverwriteTrailingSpace { get; } = overwriteTrailingSpace;
    public bool IsApplied(string s) => isApplied(s);
    public string Apply(string s) => apply(s);
    public string Clean(string s) => clean(s);

    public static readonly Operator FlagUTFBlock =
        new( // UTFBlock is a stub operator that's only to identify a UTF block; application is handled elsewhere
            0,
            _ => true,
            s => s,
            s => s, true, false);

    public static readonly Operator Capitalize = new( // The first character that is a letter is capitalized
        1,
        s => (from c in s where char.IsLetter(c) select !char.IsLower(c)).FirstOrDefault(),
        s =>
        {
            for (var i = 0; i < s.Length; i++)
            {
                if (char.IsLetter(s[i]))
                {
                    return s[..i] + char.ToUpper(s[i]) + s[(i + 1)..];
                }
            }

            return s;
        },
        s =>
        {
            for (var i = 0; i < s.Length; i++)
            {
                if (char.IsLetter(s[i]))
                {
                    return s[..i] + char.ToLower(s[i]) + s[(i + 1)..];
                }
            }

            return s;
        }, false, false);

    public static readonly Operator AllCaps = new( // All letters are capitalized
        2,
        s =>
        {
            var containsAnyCaps = false;
            foreach (var c in s.Where(char.IsLetter))
            {
                if (char.IsLower(c)) return false;
                containsAnyCaps = true;
            }

            return containsAnyCaps;
        },
        s => s.ToUpperInvariant(),
        s => s.ToLowerInvariant(), false, false);

    public static readonly Operator Period = new(
        3,
        s => trimStringEnd(s).EndsWith('.'),
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            return trimmed + '.' + whitespace;
        },
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            return trimmed.EndsWith('.') ? trimmed[..^1] + whitespace : s;
        }, false, false);

    public static readonly Operator Exclamation = new(
        4,
        s => trimStringEnd(s).EndsWith('!'),
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            return trimmed + '!' + whitespace;
        },
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            return trimmed.EndsWith('!') ? trimmed[..^1] + whitespace : s;
        }, false, false);

    public static readonly Operator Question = new(
        5,
        s => trimStringEnd(s).EndsWith('?'),
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            return trimmed + '?' + whitespace;
        },
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            return trimmed.EndsWith('?') ? trimmed[..^1] + whitespace : s;
        }, false, false);

    public static readonly Operator Ellipses = new(
        6,
        s => trimStringEnd(s).EndsWith("...") || trimStringEnd(s).EndsWith('…'),
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            return trimmed + "..." + whitespace;
        },
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            if (trimmed.EndsWith('…'))
                return trimmed[..^1] + whitespace;
            if (trimmed.EndsWith("..."))
                return trimmed[..^3] + whitespace;
            return s;
        }, false, false);

    public static readonly Operator Comma = new(
        7,
        s => trimStringEnd(s).EndsWith(','),
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            return trimmed + ',' + whitespace;
        },
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            return trimmed.EndsWith(',') ? trimmed[..^1] + whitespace : s;
        }, false, false);

    public static readonly Operator Colon = new(
        8,
        s => trimStringEnd(s).EndsWith(':'),
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            return trimmed + ':' + whitespace;
        },
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            return trimmed.EndsWith(':') ? trimmed[..^1] + whitespace : s;
        }, false, false);

    public static readonly Operator Semicolon = new(
        9,
        s => trimStringEnd(s).EndsWith(';'),
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            return trimmed + ';' + whitespace;
        },
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            return trimmed.EndsWith(';') ? trimmed[..^1] + whitespace : s;
        }, false, false);

    public static readonly Operator SingleLineBreak = new(
        10,
        s => s.EndsWith("\n") && !s.EndsWith("\n\n"),
        s => s + "\n",
        s => s[..^1], false, true);

    public static readonly Operator DoubleLineBreak = new(
        11,
        s => s.EndsWith("\n\n"),
        s => s + "\n\n",
        s => s[..^2], false, true);

    public static readonly Operator Tab = new(
        12,
        s => s.EndsWith("\t"),
        s => s + "\t",
        s => s[..^1], false, true);

    public static readonly Operator CloseQuote = new(
        13,
        s => trimStringEnd(s, false).EndsWith('”') || trimStringEnd(s, false).EndsWith('"'),
        s =>
        {
            var trimmed = trimStringEnd(s);
            var whitespace = s[trimmed.Length..];
            return trimmed + '"' + whitespace;
        },
        s =>
        {
            var trimmed = trimStringEnd(s, false);
            var whitespace = s[trimmed.Length..];
            if (trimmed.EndsWith('“') || trimmed.EndsWith('"'))
            {
                return trimmed[..^1] + whitespace;
            }

            return s;
        }, false, false);

    public static readonly Operator OpenQuote = new(
        14,
        s =>
        {
            s = trimStringStart(s, false);
            return s.StartsWith('“') || s.StartsWith('"');
        },
        s => '"' + s,
        s => s[1..],
        false, false);

    public static List<Operator>
        MinApplicableOps(string s,
            Dictionary<string, ushort> words) //TODO make this more robust and have it return the min applicable ops regardless of what all the ops are
    {
        if (words.ContainsKey(s)) return [];
        var applicableOps = new List<Operator>();
        if (AllCaps.IsApplied(s)) applicableOps.Add(AllCaps);
        else if (Capitalize.IsApplied(s)) applicableOps.Add(Capitalize);
        if (applicableOps.Count == 1)
        {
            if (words.ContainsKey(applicableOps[0].Clean(s)))
            {
                return applicableOps;
            }
        }

        if (OpenQuote.IsApplied(s)) applicableOps.Add(OpenQuote);

        if (PunctuationOp(s) is { } punctuationOp)
        {
            applicableOps.Add(punctuationOp);
        }

        if (CloseQuote.IsApplied(s)) applicableOps.Add(CloseQuote);

        if (Tab.IsApplied(s)) applicableOps.Add(Tab);
        else if (DoubleLineBreak.IsApplied(s)) applicableOps.Add(DoubleLineBreak);
        else if (SingleLineBreak.IsApplied(s)) applicableOps.Add(SingleLineBreak);

        string cleaned = applicableOps.Aggregate(s, (current, op) => op.Clean(current));
        if (words.ContainsKey(cleaned))
        {
            return applicableOps;
        }

        return [FlagUTFBlock];
    }

    private static Operator? PunctuationOp(string s)
    {
        if (Ellipses.IsApplied(s)) return Ellipses;
        if (Exclamation.IsApplied(s)) return Exclamation;
        if (Question.IsApplied(s)) return Question;
        if (Period.IsApplied(s)) return Period;
        if (Comma.IsApplied(s)) return Comma;
        if (Colon.IsApplied(s)) return Colon;
        if (Semicolon.IsApplied(s)) return Semicolon;
        return null;
    }

    public static Operator GetByID(byte id)
    {
        return OperatorMap.TryGetValue(id, out var op)
            ? op
            : throw new ArgumentException($"No operator found with ID {id}");
    }

    // Static dictionary mapping operator IDs to instances (too lazy to use reflection)
    private static readonly Dictionary<byte, Operator> OperatorMap = new()
    {
        { 0, FlagUTFBlock },
        { 1, Capitalize },
        { 2, AllCaps },
        { 3, Period },
        { 4, Exclamation },
        { 5, Question },
        { 6, Ellipses },
        { 7, Comma },
        { 8, Colon },
        { 9, Semicolon },
        { 10, SingleLineBreak },
        { 11, DoubleLineBreak },
        { 12, Tab },
        { 13, CloseQuote },
        { 14, OpenQuote }
    };

    private static readonly string Whitespace = " \t\n\r";
    private static string trimStringEnd(string s, bool trimQuotes = true)
    {
        if (trimQuotes)
        {
            return s.TrimEnd((Whitespace + "\"”").ToCharArray());
        }
        return s.TrimEnd(Whitespace.ToCharArray());
    }
    
    private static string trimStringStart(string s, bool trimQuotes = true)
    {
        if (trimQuotes)
        {
            return s.TrimStart((Whitespace + "“\"").ToCharArray());
        }
        return s.TrimStart(Whitespace.ToCharArray());
    }
}
