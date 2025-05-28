using System.Text.Json;
using System.Text.Json.Serialization;
using AIFlow.Cli.Models;

namespace AIFlow.Cli.Helpers
{
    /// <summary>
    /// Helper class for consistent JSON serialization.
    /// </summary>
    public static class JsonPayloadSerializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            WriteIndented = true, // For readability; set to false for compact output
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// Serializes a FilePayloadBase object to a JSON string.
        /// </summary>
        public static string Serialize(FilePayloadBase payload)
        {
            return JsonSerializer.Serialize(payload, payload.GetType(), Options);
        }

        // You can add a Deserialize method here if needed for AIFlow to consume these payloads.
        // public static FilePayloadBase Deserialize(string json) { ... }
    }
}
