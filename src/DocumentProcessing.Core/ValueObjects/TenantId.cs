namespace DocumentProcessing.Core.ValueObjects;

/// <summary>Strongly-typed tenant identifier (max 64 chars, URL-safe).</summary>
public readonly record struct TenantId(string Value)
{
    /// <summary>Creates a <see cref="TenantId"/> validating that the value is non-empty and ≤ 64 chars.</summary>
    public static TenantId From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 64)
            throw new ArgumentException("TenantId must not exceed 64 characters.", nameof(value));
        return new TenantId(value);
    }

    /// <inheritdoc/>
    public override string ToString() => Value;
}
