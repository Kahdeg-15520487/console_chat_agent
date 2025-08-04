using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LocalChatAgent.Models
{
    public class ChatSession
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("character_name")]
        public string CharacterName { get; set; } = string.Empty;

        [JsonPropertyName("character_file")]
        public string CharacterFile { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonPropertyName("last_modified")]
        public DateTime LastModified { get; set; } = DateTime.Now;

        [JsonPropertyName("messages")]
        public List<SessionMessage> Messages { get; set; } = new();
    }

    public class SessionMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
