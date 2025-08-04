using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace LocalChatAgent.Models
{
    public class CharacterCard
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("personality")]
        public string Personality { get; set; } = string.Empty;

        [JsonPropertyName("scenario")]
        public string Scenario { get; set; } = string.Empty;

        [JsonPropertyName("first_mes")]
        public string FirstMessage { get; set; } = string.Empty;

        [JsonPropertyName("mes_example")]
        public string MessageExample { get; set; } = string.Empty;

        [JsonPropertyName("creator_notes")]
        public string CreatorNotes { get; set; } = string.Empty;

        [JsonPropertyName("system_prompt")]
        public string SystemPrompt { get; set; } = string.Empty;

        [JsonPropertyName("post_history_instructions")]
        public string PostHistoryInstructions { get; set; } = string.Empty;

        [JsonPropertyName("alternate_greetings")]
        public List<string> AlternateGreetings { get; set; } = new();

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("creator")]
        public string Creator { get; set; } = string.Empty;

        [JsonPropertyName("character_version")]
        public string CharacterVersion { get; set; } = string.Empty;

        // V2 Card format fields
        [JsonPropertyName("spec")]
        public string Spec { get; set; } = string.Empty;

        [JsonPropertyName("spec_version")]
        public string SpecVersion { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public CharacterCardData? Data { get; set; }

        public string GetSystemPrompt()
        {
            var prompt = "";

            // Use the explicit system prompt if available
            if (!string.IsNullOrEmpty(SystemPrompt))
            {
                prompt = SystemPrompt;
            }
            else
            {
                // Build system prompt from character data
                var parts = new List<string>();

                if (!string.IsNullOrEmpty(Name))
                    parts.Add($"You are {Name}.");

                if (!string.IsNullOrEmpty(Description))
                    parts.Add(Description);

                if (!string.IsNullOrEmpty(Personality))
                    parts.Add($"Personality: {Personality}");

                if (!string.IsNullOrEmpty(Scenario))
                    parts.Add($"Scenario: {Scenario}");

                prompt = string.Join(" ", parts);
            }

            // Add post history instructions if available
            if (!string.IsNullOrEmpty(PostHistoryInstructions))
            {
                prompt += $"\n\n{PostHistoryInstructions}";
            }

            return prompt;
        }

        public string GetFirstMessage()
        {
            if (!string.IsNullOrEmpty(FirstMessage))
                return FirstMessage;

            if (AlternateGreetings.Any())
                return AlternateGreetings[0];

            return $"Hello! I'm {Name}. How can I help you today?";
        }
    }

    public class CharacterCardData
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("personality")]
        public string Personality { get; set; } = string.Empty;

        [JsonPropertyName("scenario")]
        public string Scenario { get; set; } = string.Empty;

        [JsonPropertyName("first_mes")]
        public string FirstMessage { get; set; } = string.Empty;

        [JsonPropertyName("mes_example")]
        public string MessageExample { get; set; } = string.Empty;

        [JsonPropertyName("creator_notes")]
        public string CreatorNotes { get; set; } = string.Empty;

        [JsonPropertyName("system_prompt")]
        public string SystemPrompt { get; set; } = string.Empty;

        [JsonPropertyName("post_history_instructions")]
        public string PostHistoryInstructions { get; set; } = string.Empty;

        [JsonPropertyName("alternate_greetings")]
        public List<string> AlternateGreetings { get; set; } = new();

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("creator")]
        public string Creator { get; set; } = string.Empty;

        [JsonPropertyName("character_version")]
        public string CharacterVersion { get; set; } = string.Empty;
    }
}
