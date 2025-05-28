namespace AIFlow.Cli.Models;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;


public class AIFlowFile
{
    public string ProjectName { get; set; } = "MyAIFlowProject";
    public string CurrentVersion { get; set; } = "0.1.0";
    public string CurrentBranch { get; set; } = "develop";
    public int NextTaskId { get; set; } = 1;
    public int NextHumanRequestGroupId { get; set; } = 101;
    public AIFlowConfigSettings Config { get; set; } = new();
    public List<AIFlowResource> Resources { get; set; } = new();
    public List<AIFlowTask> Tasks { get; set; } = new();
    public List<string> Roadmap { get; set; } = new();
    public ActiveRetrievalSession? ActiveAiRetrievalSession { get; set; }
}

public class AIFlowConfigSettings
{
    public long MaxRequestPayloadSizeBytes { get; set; } = 500 * 1024;
    public long MaxAiSingleMessageSizeBytes { get; set; } = 500 * 1024;
    public int ApproxMaxAiContextTokens { get; set; } = 8000;
    public string? CurrentBranch { get; internal set; }
}

public class AIFlowResource
{
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = ResourceType.LocalFile;
    public string LocalHash { get; set; } = string.Empty;
    public string? LastSentToAiHash { get; set; }
    public string Status { get; set; } = ResourceStatus.Unmodified;
    public string? ConflictResolutionFile { get; set; }
    public string? Notes { get; set; }
}

public static class ResourceType
{
    public const string LocalFile = "LocalFile";
    public const string URL = "URL";
    public const string TextSnippet = "TextSnippet";
}

public static class ResourceStatus
{
    public const string Unmodified = "unmodified";
    public const string ModifiedLocally = "modified_locally";
    public const string AwaitingAiChanges = "awaiting_ai_changes";
    public const string AiModified = "ai_modified";
    public const string NeedsManualMerge = "needs_manual_merge";
    public const string Merged = "merged";
}

public class AIFlowTask
{
    public string TaskId { get; set; } = string.Empty;
    public string Branch { get; set; } = "develop";
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = TaskStatus.ToDo;
    public string AssignedTo { get; set; } = "ai";
    public List<AIFlowTaskRelatedResource> RelatedResources { get; set; } = new();
    public string? HumanRequestGroupId { get; set; }
    public int? HumanRequestTotalParts { get; set; }
    public int HumanRequestPartsSent { get; set; } = 0;
    public string? HumanNotes { get; set; }
    public string? AiNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? Type { get; set; }
    public int? StoryPoints { get; set; }
    public string? Priority { get; set; }
    public string? Sprint { get; set; }
    public string? EpicLink { get; set; }
    public DateTime? DueDate { get; set; }
    public List<string> Labels { get; set; } = new();
}

public static class TaskStatus
{
    public const string ToDo = "todo";
    public const string InProgress = "in_progress";
    public const string PendingHumanInput = "pending_human_input";
    public const string PendingHumanInputParts = "pending_human_input_parts";
    public const string PendingAiProcessing = "pending_ai_processing";
    public const string Blocked = "blocked";
    public const string InReview = "in_review";
    public const string PendingHumanReview = "pending_human_review";
    public const string Done = "done";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string AwaitingAiBatches = "awaiting_ai_batches";
    public const string Archived = "archived";
}

public static class TaskType
{
    public const string Story = "Story";
    public const string Task = "Task";
    public const string Bug = "Bug";
    public const string Epic = "Epic";
    public const string Spike = "Spike";
}

public static class TaskPriority
{
    public const string Highest = "Highest";
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";
    public const string Lowest = "Lowest";
}

public class AIFlowTaskRelatedResource
{
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = ResourceType.LocalFile;
    public int? SentInPart { get; set; }
    public string Action { get; set; } = "update";
    public string? HashAtRequestTime { get; set; }
    public string? ContentDetail { get; set; }
}

public class ActiveRetrievalSession
{
    public string RetrievalGuid { get; set; } = string.Empty;
    public string AiChangesetId { get; set; } = string.Empty;
    public int TotalBatches { get; set; }
    public int ReceivedBatchesCount { get; set; }
    public List<string> ReceivedBatchPayloads { get; set; } = new();
}


public class BackupInfo
{
    public string BackupId { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string? RelatedTaskId { get; set; }
    public string? AiChangesetId { get; set; }
    public List<string> BackedUpFileRelativePaths { get; set; } = new List<string>();
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// Base class for file payload representations.
/// </summary>
public abstract class FilePayloadBase
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; }

    [JsonPropertyName("originalPath")]
    public string OriginalPath { get; set; }

    [JsonPropertyName("encodingType")]
    public string EncodingType { get; protected set; }

    protected FilePayloadBase(string fileName, string originalPath, string encodingType)
    {
        FileName = fileName;
        OriginalPath = originalPath;
        EncodingType = encodingType;
    }
}

/// <summary>
/// Payload for files embedded as raw string content.
/// </summary>
public class RawContentPayload : FilePayloadBase
{
    [JsonPropertyName("content")]
    public string Content { get; set; }

    public RawContentPayload(string fileName, string originalPath, string content)
        : base(fileName, originalPath, "raw")
    {
        Content = content;
    }
}

/// <summary>
/// Payload for files embedded as a single Base64 encoded string.
/// </summary>
public class Base64Payload : FilePayloadBase
{
    [JsonPropertyName("content")]
    public string Content { get; set; } // Base64 encoded content

    public Base64Payload(string fileName, string originalPath, string content)
        : base(fileName, originalPath, "base64")
    {
        Content = content;
    }
}

/// <summary>
/// Represents a single chunk of a Base64 encoded file.
/// </summary>
public class ChunkInfo
{
    [JsonPropertyName("partNumber")]
    public int PartNumber { get; set; }

    [JsonPropertyName("data")]
    public string Data { get; set; } // Base64 encoded chunk data

    [JsonPropertyName("partChecksum")]
    public string PartChecksum { get; set; } // Checksum of this part's original (pre-Base64) data

    public ChunkInfo(int partNumber, string data, string partChecksum)
    {
        PartNumber = partNumber;
        Data = data;
        PartChecksum = partChecksum;
    }
}

/// <summary>
/// Payload for large files split into multiple Base64 encoded chunks.
/// </summary>
public class ChunkedBase64Payload : FilePayloadBase
{
    [JsonPropertyName("totalChunks")]
    public int TotalChunks { get; set; }

    [JsonPropertyName("chunkChecksumType")]
    public string ChunkChecksumType { get; set; } = "CRC32"; // Default checksum type

    [JsonPropertyName("overallChecksum")]
    public string OverallChecksum { get; set; } // Checksum of the entire original file data

    [JsonPropertyName("chunks")]
    public List<ChunkInfo> Chunks { get; set; }

    public ChunkedBase64Payload(string fileName, string originalPath, int totalChunks, string overallChecksum, List<ChunkInfo> chunks)
        : base(fileName, originalPath, "base64_chunked")
    {
        TotalChunks = totalChunks;
        OverallChecksum = overallChecksum;
        Chunks = chunks;
    }
}

public class FlowTemplateCollection
{
    public List<FlowTemplate> Templates { get; set; } = new List<FlowTemplate>();
}

public class FlowTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? CurrentBranch { get; set; }
    public AIFlowConfigSettings? ConfigOverrides { get; set; }
    public List<AIFlowTaskStub> InitialTasks { get; set; } = new List<AIFlowTaskStub>();
    public List<InitialResourceStub> InitialResources { get; set; } =
        new List<InitialResourceStub>();
    public List<string> InitialRoadmap { get; set; } = new List<string>();
}

public class AIFlowTaskStub
{
    public string? TaskId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Branch { get; set; }
    public string? Status { get; set; }
    public string? AssignedTo { get; set; }
    public string? Type { get; set; }
    public int? StoryPoints { get; set; }
    public string? Priority { get; set; }
    public string? Sprint { get; set; }
    public string? EpicLink { get; set; }
    public string? DueDate { get; set; }
    public List<string>? Labels { get; set; }
    public string? HumanNotes { get; set; }
    public List<AIFlowTaskRelatedResourceStub>? RelatedResources { get; set; }
}

public class AIFlowTaskRelatedResourceStub
{
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = ResourceType.LocalFile;
    public string Action { get; set; } = "create";
}

public class InitialResourceStub
{
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = ResourceType.LocalFile;
    public string? InitialContent { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
}

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


