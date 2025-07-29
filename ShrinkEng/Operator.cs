using System;

namespace ShrinkEng;

public class Operator(
    byte id,
    Func<string, bool> isApplied,
    Func<string, string> apply,
    Func<string, string> clean,
    bool requiresLength)
{
    public byte ID { get; } = id;

    public bool RequiresLength { get; } = requiresLength;
    public bool IsApplied(string s) => isApplied(s);
    public string Apply(string s) => apply(s);
    public string Clean(string s) => clean(s);

    public static readonly Operator FlagUTFBlock = new Operator( // UTFBlock is a stub operator that's only to identify a UTF block; application is handled elsewhere
        0,
        s => true,
        s => s,
        s => s, true);

    public static readonly Operator Capitalize = new Operator( // The first character that is a letter is capitalized
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
        }, false);
    
    public static readonly Operator AllCaps = new Operator( // All letters are capitalized
        2,
        s =>
        {
            var containsAnyCaps = false;
            foreach (var c in s.Where(char.IsLetter))
            {
                if (char.IsLower(c)) return false;
                else containsAnyCaps = true;
            }
            return containsAnyCaps;
        },
        s => s.ToUpperInvariant(),
        s => s.ToLowerInvariant(), false);
    
    public static readonly Operator Period = new Operator(
        3,
        s => s.TrimEnd().EndsWith('.'),
        s => 
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed + '.' + whitespace;
        },
        s => 
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed.EndsWith('.') ? trimmed[..^1] + whitespace : s;
        }, false);

    public static readonly Operator Exclamation = new Operator(
        4,
        s => s.TrimEnd().EndsWith('!'),
        s => 
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed + '!' + whitespace;
        },
        s => 
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed.EndsWith('!') ? trimmed[..^1] + whitespace : s;
        }, false);
    
    public static readonly Operator Question = new Operator(
        5,
        s => s.TrimEnd().EndsWith('?'),
        s => 
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed + '?' + whitespace;
        },
        s => 
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed.EndsWith('?') ? trimmed[..^1] + whitespace : s;
        }, false);
    
    public static readonly Operator Ellipses = new Operator(
        6,
        s => s.TrimEnd().EndsWith("...") || s.TrimEnd().EndsWith('…'),
        s => 
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed + "..." + whitespace;
        },
        s => 
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            if (trimmed.EndsWith('…'))
                return trimmed[..^1] + whitespace;
            else if (trimmed.EndsWith("..."))
                return trimmed[..^3] + whitespace;
            return s;
        }, false);
    
    public static readonly Operator Comma = new Operator(
        7,
        s => s.TrimEnd().EndsWith(','),
        s => 
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed + ',' + whitespace;
        },
        s => 
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed.EndsWith(',') ? trimmed[..^1] + whitespace : s;
        }, false);
    
    public static readonly Operator Colon = new Operator(
        8,
        s => s.TrimEnd().EndsWith(':'),
        s => 
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed + ':' + whitespace;
        },
        s => 
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed.EndsWith(':') ? trimmed[..^1] + whitespace : s;
        }, false);
    
    public static readonly Operator Semicolon = new Operator(
        9,
        s => s.TrimEnd().EndsWith(';'),
        s => 
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed + ';' + whitespace;
        },
        s => 
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed.EndsWith(';') ? trimmed[..^1] + whitespace : s;
        }, false);
    
    public static readonly Operator SingleLineBreak = new Operator(
        10,
        s =>
        {
            var trimmed = s.TrimEnd();
            return trimmed.EndsWith("\n") && !trimmed.EndsWith("\n\n");
        },
        s =>
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed + "\n" + whitespace;
        },
        s =>
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed.EndsWith("\n")
                ? trimmed[..^1] + whitespace
                : s;
        }, false);
    
    public static readonly Operator DoubleLineBreak = new Operator(
        11,
        s =>
        {
            var trimmed = s.TrimEnd();
            return trimmed.EndsWith("\n\n");
        },
        s =>
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            return trimmed + "\n\n" + whitespace;
        },
        s =>
        {
            var trimmed = s.TrimEnd();
            var whitespace = s[trimmed.Length..];
            if (trimmed.EndsWith("\n\n"))
                return trimmed[..^2] + whitespace;
            if (trimmed.EndsWith("\n"))
                return trimmed[..^1] + whitespace;
            return s;
        }, false);
    
    public static List<Operator> MinApplicableOps(string s, Dictionary<string, ushort> words) //TODO make this more robust and have it return the min applicable ops regardless of what all the ops are
    {
        if (words.ContainsKey(s)) return [];
        var applicableOps = new List<Operator>();
        if(AllCaps.IsApplied(s)) applicableOps.Add(AllCaps);
        else if (Capitalize.IsApplied(s)) applicableOps.Add(Capitalize);
        if (applicableOps.Count == 1)
        {
            if (words.ContainsKey(applicableOps[0].Clean(s)))
            {
                return applicableOps;
            }
        }

        if (PunctuationOp(s) is { } punctuationOp)
        {
            applicableOps.Add(punctuationOp);
        }
        if (DoubleLineBreak.IsApplied(s)) applicableOps.Add(DoubleLineBreak);
        else if (SingleLineBreak.IsApplied(s)) applicableOps.Add(SingleLineBreak);

        string cleaned = applicableOps.Aggregate(s, (current, op) => op.Clean(current));
        if (words.ContainsKey(cleaned))
        {
            return applicableOps;
        }else return [FlagUTFBlock];
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
        return OperatorMap.TryGetValue(id, out var op) ? op : throw new ArgumentException($"No operator found with ID {id}");
    }
    
    // Static dictionary mapping operator IDs to instances
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
        {10, SingleLineBreak},
        {11, DoubleLineBreak}
    };
}
