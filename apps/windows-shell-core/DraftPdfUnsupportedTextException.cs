namespace JanLabel.WindowsShell.Core;

public sealed class DraftPdfUnsupportedTextException : InvalidOperationException
{
    public DraftPdfUnsupportedTextException(string message)
        : base(message)
    {
    }
}
