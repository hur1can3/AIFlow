using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AIFlow.Cli.Models;
using AIFlow.Cli.Helpers;

namespace AIFlow.Cli.Services
{
    /// <summary>
    /// Processes files to determine the appropriate payload representation for AIFlow.
    /// </summary>
    public class FileProcessorService
    {
        // Configuration constants (can be moved to a config file or settings class)
        private const int MaxRawStringFileSizeKB = 32; // Max file size in KB for raw string embedding
        private const int MaxBase64ChunkSizeBytes = 1 * 1024 * 1024; // 1MB per chunk (for the Base64 string itself)
        private static readonly HashSet<string> BinaryFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".dll", ".exe", ".so", ".dylib", ".bin", ".data", // Executables and libraries
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp", // Images
            ".mp3", ".wav", ".ogg", ".aac", // Audio
            ".mp4", ".mov", ".avi", ".mkv", // Video
            ".zip", ".gz", ".tar", ".rar", ".7z", // Archives
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", // Documents (often binary or complex structure)
            ".o", ".obj", ".lib", ".a", // Object files
            ".pdb", // Program database
            ".dat", ".idx" // Generic data files often binary
        };

        /// <summary>
        /// Processes a file and returns its JSON payload representation.
        /// </summary>
        /// <param name="filePath">The full path to the file.</param>
        /// <returns>A JSON string representing the file payload.</returns>
        public async Task<string> ProcessFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                // Consider how to handle this error in the CLI (e.g., log and skip, or throw)
                Console.Error.WriteLine($"Error: File not found at '{filePath}'. Skipping.");
                // Returning null or an error JSON structure might be options.
                // For now, let's assume the CLI handles this by not generating a payload.
                return null; // Or throw new FileNotFoundException("File not found.", filePath);
            }

            FileInfo fileInfo = new FileInfo(filePath);
            string fileName = fileInfo.Name;
            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);

            FilePayloadBase payload;

            // 1. Determine if it's a binary file by extension
            bool isBinaryByExtension = BinaryFileExtensions.Contains(fileInfo.Extension);

            // 2. Check for raw string candidacy
            if (!isBinaryByExtension && (fileInfo.Length / 1024) <= MaxRawStringFileSizeKB)
            {
                string rawContent = Encoding.UTF8.GetString(fileBytes); // Assuming UTF-8 for text files
                if (IsValidForRawJsonString(rawContent))
                {
                    payload = new RawContentPayload(fileName, filePath, rawContent);
                    return JsonPayloadSerializer.Serialize(payload);
                }
            }

            // 3. If not raw, it's Base64 (either single or chunked)
            string overallChecksum = ChecksumService.CalculateCRC32(fileBytes); // Checksum of the original full file

            // Estimate Base64 length: (bytes / 3) * 4, rounded up to multiple of 4
            long estimatedBase64Length = (long)Math.Ceiling(fileBytes.Length / 3.0) * 4;

            if (estimatedBase64Length <= MaxBase64ChunkSizeBytes)
            {
                // Small enough for a single Base64 string
                string base64Content = Convert.ToBase64String(fileBytes);
                payload = new Base64Payload(fileName, filePath, base64Content);
            }
            else
            {
                // Needs chunking
                List<ChunkInfo> chunks = new List<ChunkInfo>();
                int partNumber = 1;
                // Calculate how many original bytes correspond roughly to MaxBase64ChunkSizeBytes
                // Base64 expands data by 4/3. So, original data for one chunk is roughly MaxBase64ChunkSizeBytes * 3/4.
                int originalBytesPerChunk = (MaxBase64ChunkSizeBytes * 3) / 4;

                for (int offset = 0; offset < fileBytes.Length; offset += originalBytesPerChunk)
                {
                    int length = Math.Min(originalBytesPerChunk, fileBytes.Length - offset);
                    byte[] chunkBytes = new byte[length];
                    Array.Copy(fileBytes, offset, chunkBytes, 0, length);

                    string partChecksum = ChecksumService.CalculateCRC32(chunkBytes);
                    string base64ChunkData = Convert.ToBase64String(chunkBytes);

                    chunks.Add(new ChunkInfo(partNumber++, base64ChunkData, partChecksum));
                }
                payload = new ChunkedBase64Payload(fileName, filePath, chunks.Count, overallChecksum, chunks);
            }

            return JsonPayloadSerializer.Serialize(payload);
        }

        /// <summary>
        /// Checks if the string content is suitable for direct embedding in a JSON string.
        /// This is a basic check for control characters (0x00-0x1F) excluding TAB, LF, CR.
        /// More sophisticated checks might be needed for complex cases or specific JSON parsers.
        /// </summary>
        private bool IsValidForRawJsonString(string content)
        {
            foreach (char c in content)
            {
                if (c < 0x20 && c != '\t' && c != '\n' && c != '\r')
                {
                    // Contains a control character other than tab, newline, or carriage return
                    return false;
                }
            }
            // Consider also checking for very high density of quotes or backslashes if that's a concern.
            // For this implementation, the control character check is the primary heuristic.
            return true;
        }
    }
}
