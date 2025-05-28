using System.Collections.Generic;
using System.Runtime.Intrinsics.Arm;
using System.Text.Json;
using System.Text;
using System.Text.Json.Serialization;
using AIFlow.Cli.Helpers;
using AIFlow.Cli.Models;
using AIFlow.Cli.Services;

namespace AIFlow.Cli.Models
{
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
}
