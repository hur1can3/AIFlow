namespace AIFlow.Cli.Services.Payloads
{
    using System;
    using System.Collections.Generic;

    public class HumanRequestPart
    {
        public HumanRequestPayload HumanRequest { get; set; } = new();
    }

    public class HumanRequestPayload
    {
        public string? HumanRequestGroupId { get; set; }
        public int PartNumber { get; set; }
        public int TotalParts { get; set; }
        public string TaskId { get; set; } = string.Empty;
        public string? TaskDescription { get; set; }
        public List<HumanRequestFileToProcess>? FilesToProcess { get; set; }
        public List<HumanRequestFileData>? FileData { get; set; }
        public object? RelevantMetadata { get; set; }
    }

    public class HumanRequestFileToProcess
    {
        public string Path { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? Hash { get; set; }
        public string? ContentBase64 { get; set; }
        public string? ContentDetail { get; set; }
    }

    public class HumanRequestFileData
    {
        public string Path { get; set; } = string.Empty;
        public string ContentBase64 { get; set; } = string.Empty;
    }

    public class AiPreliminaryResponseWrapper
    {
        public AiPreliminaryResponse AiPreliminaryResponse { get; set; } = new();
    }

    public class AiPreliminaryResponse
    {
        public string AiChangesetId { get; set; } = string.Empty;
        public string HumanRequestId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public long? EstimatedTotalSizeBytes { get; set; }
        public int NumberOfBatches { get; set; }
        public string RetrievalGuid { get; set; } = string.Empty;
        public string? InstructionsForCli { get; set; }
    }

    public class AiOutputPackageWrapper
    {
        public AiOutputPackage AiOutputPackage { get; set; } = new();
    }

    public class AiBatchResponseWrapper
    {
        public AiBatchResponse AiBatchResponse { get; set; } = new();
    }

    public class AiOutputPackage
    {
        public string AiChangesetId { get; set; } = string.Empty;
        public string HumanRequestId { get; set; } = string.Empty;
        public AiTaskUpdate? TaskUpdates { get; set; }
        public List<AiFileChange>? FileChanges { get; set; }
        public List<string>? RoadmapSuggestions { get; set; }
        public string? OverallAiComment { get; set; }
        public string? RetrievalGuid { get; set; }
        public int? BatchNumber { get; set; }
        public int? TotalBatches { get; set; }
        public bool? IsLastBatch { get; set; }
    }

    public class AiBatchResponse
    {
        public string RetrievalGuid { get; set; } = string.Empty;
        public string AiChangesetId { get; set; } = string.Empty;
        public int BatchNumber { get; set; }
        public int TotalBatches { get; set; }
        public bool IsLastBatch { get; set; }
        public string PayloadType { get; set; } = string.Empty;
        public AiOutputPackage Payload { get; set; } = new();
    }

    public class AiTaskUpdate
    {
        public string TaskId { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public string? AiNotes { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AiFileChange
    {
        public string Path { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? NewHash { get; set; }
        public string? BasedOnHash { get; set; }
        public string? ContentBase64 { get; set; }
        public string? ContentDelivery { get; set; }
        public int? TotalContentChunksForFile { get; set; }
        public int? ChunkNumberForFile { get; set; }
    }
}
