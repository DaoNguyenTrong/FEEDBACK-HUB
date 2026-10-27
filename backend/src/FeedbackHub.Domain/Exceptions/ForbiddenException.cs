namespace FeedbackHub.Domain.Exceptions;

public sealed class ForbiddenException : AppException
{
    private static readonly IReadOnlyDictionary<string, string> NoExtensions = new Dictionary<string, string>();

    // Kept as its own overload, not an optional parameter: reflection (Activator) binds only exact-arity constructors.
    public ForbiddenException(string message)
        : this(message, null)
    {
    }

    /// <param name="message">The message key, written as the problem's <c>code</c>.</param>
    /// <param name="extensions">Members written at the top level of the problem body, beside <c>code</c>.</param>
    public ForbiddenException(string message, IReadOnlyDictionary<string, string>? extensions)
        : base(message)
    {
        Extensions = extensions is null ? NoExtensions : new Dictionary<string, string>(extensions);
    }

    public IReadOnlyDictionary<string, string> Extensions { get; }
}
