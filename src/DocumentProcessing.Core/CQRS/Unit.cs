namespace DocumentProcessing.Core.CQRS;

/// <summary>Represents the absence of a return value — used for void-returning commands.</summary>
public readonly struct Unit
{
    /// <summary>The single <see cref="Unit"/> value.</summary>
    public static readonly Unit Value = default;

    /// <summary>Returns a completed <see cref="Task{TResult}"/> of <see cref="Unit"/>.</summary>
    public static Task<Unit> Task => System.Threading.Tasks.Task.FromResult(Value);
}
