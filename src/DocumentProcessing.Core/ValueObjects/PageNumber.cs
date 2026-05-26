namespace DocumentProcessing.Core.ValueObjects;

/// <summary>1-based page number with invariant enforcement.</summary>
public readonly record struct PageNumber(int Value)
{
    /// <summary>Creates a <see cref="PageNumber"/> ensuring the value is ≥ 1.</summary>
    public static PageNumber From(int value)
    {
        if (value < 1)
            throw new ArgumentOutOfRangeException(nameof(value), "Page number must be ≥ 1.");
        return new PageNumber(value);
    }

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}
