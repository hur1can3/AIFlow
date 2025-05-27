namespace AIFlow.Cli.Models
{
    using System.Collections.Generic;

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
}
