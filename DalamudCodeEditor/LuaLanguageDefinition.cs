namespace DalamudCodeEditor;

public class LuaLanguageDefinition : LanguageDefinition
{
    public LuaLanguageDefinition()
    {
        Name = "Lua";
        CommentStart = "--[[";
        CommentEnd = "]]";
        SingleLineComment = "--";
        PreprocChar = '#';
        CaseSensitive = true;
        AutoIndentation = false;

        Keywords.AddRange([
            "and", "break", "do", "else", "elseif", "end", "false", "for",
            "function", "goto", "if", "in", "local", "nil", "not", "or",
            "repeat", "return", "then", "true", "until", "while"
        ]);

        var builtins = new[]
        {
            "_G", "_VERSION", "_ENV", "assert", "collectgarbage", "dofile",
            "error", "getmetatable", "ipairs", "load", "loadfile", "next",
            "pairs", "pcall", "print", "rawequal", "rawget", "rawlen", "rawset",
            "select", "setmetatable", "tonumber", "tostring", "type", "xpcall",
            "require", "module", "coroutine", "table", "string", "math", "utf8",
            "io", "os", "debug", "package", "self", "..."
        };

        foreach (var ident in builtins)
            Identifiers[ident] = new Identifier { mDeclaration = "Built-in" };

        TokenizeLine = TokenizeLuaLine;
    }

    private IEnumerable<Token> TokenizeLuaLine(string line)
    {
        var i = 0;
        while (i < line.Length)
        {
            var c = line[i];

            // Skip whitespace
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            var start = i;

            // Long string / multiline comment
            if (c == '[' && i + 1 < line.Length && line[i + 1] == '[')
            {
                var end = line.IndexOf("]]", i + 2);
                end = end == -1 ? line.Length : end + 2;
                yield return new Token(i, end, PaletteIndex.String);
                i = end;
                continue;
            }

            // Line comment --
            if (c == '-' && i + 1 < line.Length && line[i + 1] == '-')
            {
                yield return new Token(i, line.Length, PaletteIndex.Comment);
                break;
            }

            // Strings
            if (c == '"' || c == '\'')
            {
                var quote = c;
                i++;
                while (i < line.Length)
                    if (line[i] == '\\')
                    {
                        i += 2;
                    }
                    else if (line[i] == quote)
                    {
                        i++;
                        break;
                    }
                    else
                    {
                        i++;
                    }

                yield return new Token(start, i, PaletteIndex.String);
                continue;
            }

            // Numbers
            if (char.IsDigit(c) || (c == '.' && i + 1 < line.Length && char.IsDigit(line[i + 1])))
            {
                i++;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '.'))
                    i++;
                yield return new Token(start, i, PaletteIndex.Number);
                continue;
            }

            // Identifiers (and dotted chains + method calls)
            if (char.IsLetter(c) || c == '_')
            {
                while (i < line.Length)
                {
                    var partStart = i;

                    // Identifier
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                        i++;

                    var partEnd = i;
                    var ident = line.Substring(partStart, partEnd - partStart);
                    var lookup = CaseSensitive ? ident : ident.ToLower();

                    PaletteIndex kind;

                    if (Keywords.Contains(lookup))
                    {
                        kind = PaletteIndex.Keyword;
                    }
                    else if (Identifiers.ContainsKey(lookup))
                    {
                        kind = PaletteIndex.KnownIdentifier;
                    }
                    else
                    {
                        // Look ahead to see if it's a function call (skip whitespace)
                        var lookahead = i;
                        while (lookahead < line.Length && char.IsWhiteSpace(line[lookahead]))
                            lookahead++;

                        kind = lookahead < line.Length && line[lookahead] == '('
                            ? PaletteIndex.Function
                            : PaletteIndex.Identifier;
                    }

                    yield return new Token(partStart, partEnd, kind);

                    // Dot or colon separator
                    if (i < line.Length && (line[i] == '.' || line[i] == ':'))
                    {
                        yield return new Token(i, i + 1, PaletteIndex.Punctuation);
                        i++;
                    }
                    else
                    {
                        break;
                    }
                }

                continue;
            }

            // Punctuation
            yield return new Token(i, i + 1, PaletteIndex.Punctuation);
            i++;
        }
    }
}