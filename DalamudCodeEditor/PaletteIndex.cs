namespace DalamudCodeEditor;

/// <summary>
///     Represents the type of token or editor element being colorized.
///     The order matters for indexing into the highlight palette.
/// </summary>
public enum PaletteIndex
{
    // === Syntax Elements ===
    Default = 0,

    Keyword,

    Identifier,

    KnownIdentifier,

    Function,

    Number,

    String,

    Comment,

    MultiLineComment,

    Preprocessor,

    PreprocessorIdentifier,

    Punctuation,

    // === Editor UI Elements ===
    Background,

    Cursor,

    Selection,

    LineNumber,

    CurrentLineFill,

    CurrentLineFillInactive,

    CurrentLineEdge,

    // === Reserved / Internal Use ===
    Max,
}
