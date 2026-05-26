namespace DocumentProcessing.Contracts.Enums;

/// <summary>Represents the lifecycle state of a document in the processing pipeline.</summary>
public enum DocumentStatus
{
    /// <summary>Upload intent created; awaiting client SAS upload.</summary>
    PendingUpload = 0,

    /// <summary>Upload confirmed; message dispatched to processing queue.</summary>
    Uploaded = 1,

    /// <summary>Worker has picked up the message and is rendering pages.</summary>
    Processing = 2,

    /// <summary>All pages rendered and indexed; document is servable.</summary>
    Ready = 3,

    /// <summary>Processing failed; see <see cref="ProcessingFailureReason"/> for detail.</summary>
    Failed = 4
}
