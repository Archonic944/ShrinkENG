using System;

namespace ShrinkEng;

public class Operator(sbyte id, Func<string, bool> isApplied, Func<string, string> apply, Func<string, string> clean)
{
    public sbyte ID { get; } = id;

    public bool IsApplied(string s) => isApplied(s);
    public string Apply(string s) => apply(s);
    public string Clean(string s) => clean(s);

    public static readonly Operator UTFBlock = new Operator( // UTFBlock is a stub operator that's only to identify a UTF block; application is handled elsewhere
        0,
        s => true,
        s => s,
        s => s
    );

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
        }
    );
    
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
        s => s.ToLowerInvariant()
    );
    
    public static readonly Operator Period = new Operator(
        3,
        s => s[^1] == '.',
        s => s + '.',
        s => s[..^1]
    );

    public static readonly Operator Exclamation = new Operator(
        4,
        s => s[^1] == '!',
        s => s + '!',
        s => s[..^1]
    );
    
    public static readonly Operator Question = new Operator(
        5,
        s => s[^1] == '?',
        s => s + '?',
        s => s[..^1]
    );
    
    public static readonly Operator Ellipses = new Operator(
        6,
        s => s.EndsWith("...") || s.EndsWith('…'),
        s => s + "...",
        s => s.EndsWith('…') ? s[..^1] : s[..^3] 
    );
    
    public static readonly Operator Comma = new Operator(
        7,
        s => s[^1] == ',',
        s => s + ',',
        s => s[..^1]
    );
    
    public static readonly Operator Colon = new Operator(
        8,
        s => s[^1] == ':',
        s => s + ':',
        s => s[..^1]
    );
    
    public static readonly Operator Semicolon = new Operator(
        9,
        s => s[^1] == ';',
        s => s + ';',
        s => s[..^1]
    );
}
