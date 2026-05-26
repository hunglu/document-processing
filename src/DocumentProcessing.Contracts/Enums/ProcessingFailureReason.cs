namespace DocumentProcessing.Contracts.Enums;

/// <summary>Categorises why document processing failed.</summary>
public enum ProcessingFailureReason
{
    /// <summary>No failure — document is not in a failed state.</summary>
    None = 0,

    /// <summary>The uploaded blob could not be located in storage.</summary>
    BlobNotFound = 1,

    /// <summary>The file is not a valid PDF or is corrupt.</summary>
    InvalidPdf = 2,

    /// <summary>Page rendering threw an unhandled exception.</summary>
    RenderError = 3,

    /// <summary>Writing rendered pages to blob storage failed.</summary>
    StorageWriteError = 4,

    /// <summary>Database persistence of page records failed.</summary>
    DatabaseError = 5,

    /// <summary>Processing exceeded the maximum allowed duration.</summary>
    Timeout = 6,

    /// <summary>An unexpected error occurred that does not fit other categories.</summary>
    Unknown = 99
}
