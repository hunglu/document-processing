using System.Text.RegularExpressions;

namespace DocumentProcessing.Core.ValueObjects;

/// <summary>SHA-256 checksum represented as a lowercase 64-char hex string.</summary>
public readonly record struct Checksum(string Value)
{
    private static readonly Regex Sha256Regex = new("^[a-f0-9]{64}$", RegexOptions.Compiled);

    /// <summary>Creates a <see cref="Checksum"/> validating the hex format.</summary>
    public static Checksum From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!Sha256Regex.IsMatch(value))
            throw new ArgumentException("Checksum must be a lowercase 64-char hex SHA-256.", nameof(value));
        return new Checksum(value);
    }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
