namespace AIFlow.Cli.Models
{
    using System;
    using System.Collections.Generic;

    public class BackupInfo
    {
        public string BackupId { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; }
        public string? RelatedTaskId { get; set; }
        public string? AiChangesetId { get; set; }
        public List<string> BackedUpFileRelativePaths { get; set; } = new List<string>();
        public string Notes { get; set; } = string.Empty;
    }
}
