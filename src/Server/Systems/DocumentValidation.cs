namespace Vtt.Server.Systems;

/// <summary>One thing wrong with a document, and where.</summary>
/// <remarks>
/// The path matters more than the message. "Invalid" tells nobody anything about a nested character
/// sheet; <c>/abilities/strength</c> is actionable.
/// </remarks>
public sealed record DocumentError(string Path, string Message);

public sealed record DocumentValidation(bool IsValid, IReadOnlyList<DocumentError> Errors)
{
    public static readonly DocumentValidation Valid = new(true, []);

    public static DocumentValidation Invalid(IReadOnlyList<DocumentError> errors) => new(false, errors);
}
