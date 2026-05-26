namespace DocumentProcessing.Core.Exceptions;

/// <summary>Thrown when a domain invariant is violated on the Document aggregate.</summary>
public sealed class DocumentDomainException : Exception
{
    /// <inheritdoc/>
    public DocumentDomainException(string message) : base(message) { }

    /// <inheritdoc/>
    public DocumentDomainException(string message, Exception inner) : base(message, inner) { }
}
