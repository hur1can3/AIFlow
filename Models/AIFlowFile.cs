namespace AIFlow.Cli.Models
{
    using System;
    using System.Collections.Generic;

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
}
