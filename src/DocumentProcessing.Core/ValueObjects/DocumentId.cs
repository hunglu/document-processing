namespace DocumentProcessing.Core.ValueObjects;

/// <summary>Strongly-typed identifier for a <see cref="Domain.Document"/>.</summary>
public readonly record struct DocumentId(Guid Value)
{
    /// <summary>Creates a new random <see cref="DocumentId"/>.</summary>
    public static DocumentId New() => new(Guid.NewGuid());

    /// <summary>Parses a <see cref="DocumentId"/> from a <see cref="Guid"/> string; throws on invalid input.</summary>
    public static DocumentId Parse(string value) => new(Guid.Parse(value));

    /// <summary>Attempts to parse a <see cref="DocumentId"/> without throwing.</summary>
    public static bool TryParse(string value, out DocumentId result)
    {
        if (Guid.TryParse(value, out var guid))
        {
            result = new DocumentId(guid);
            return true;
        }
        result = default;
        return false;
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}
