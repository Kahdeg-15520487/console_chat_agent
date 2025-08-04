using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LocalChatAgent.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata;

namespace LocalChatAgent.Services
{
    public class CharacterCardLoader
    {
        public static async Task<CharacterCard?> LoadFromPngAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Character card file not found: {filePath}");
                    return null;
                }

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var image = await Image.LoadAsync(fileStream);

                // Try to find character data in PNG metadata
                var characterJson = ExtractCharacterJsonFromPng(image);
                if (string.IsNullOrEmpty(characterJson))
                {
                    Console.WriteLine("No character data found in PNG metadata");
                    return null;
                }

                // Parse the character card
                var characterCard = ParseCharacterCard(characterJson);
                if (characterCard != null)
                {
                    Console.WriteLine($"Successfully loaded character card: {characterCard.Name}");
                    if (!string.IsNullOrEmpty(characterCard.Creator))
                        Console.WriteLine($"  Created by: {characterCard.Creator}");
                    if (!string.IsNullOrEmpty(characterCard.Description))
                        Console.WriteLine($"  Description: {characterCard.Description}");
                }

                return characterCard;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading character card from {filePath}: {ex.Message}");
                return null;
            }
        }

        public static async Task<CharacterCard?> LoadFromJsonAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Character card file not found: {filePath}");
                    return null;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var characterCard = ParseCharacterCard(json);
                
                if (characterCard != null)
                {
                    Console.WriteLine($"Successfully loaded character card: {characterCard.Name}");
                    if (!string.IsNullOrEmpty(characterCard.Creator))
                        Console.WriteLine($"  Created by: {characterCard.Creator}");
                    if (!string.IsNullOrEmpty(characterCard.Description))
                        Console.WriteLine($"  Description: {characterCard.Description}");
                }

                return characterCard;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading character card from {filePath}: {ex.Message}");
                return null;
            }
        }

        private static string? ExtractCharacterJsonFromPng(Image image)
        {
            try
            {
                // Look for character data in PNG text chunks
                var possibleKeys = new[] { "chara", "Comment", "Description", "UserComment", "ccv2" };
                
                // Check PNG-specific text chunks
                if (image.Metadata.GetFormatMetadata(PngFormat.Instance) is PngMetadata pngMetadata)
                {
                    foreach (var textData in pngMetadata.TextData)
                    {
                        if (possibleKeys.Any(key => string.Equals(textData.Keyword, key, StringComparison.OrdinalIgnoreCase)))
                        {
                            var decodedJson = TryDecodeBase64(textData.Value);
                            if (IsValidJson(decodedJson))
                            {
                                return decodedJson;
                            }

                            if (IsValidJson(textData.Value))
                            {
                                return textData.Value;
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting character data from PNG: {ex.Message}");
                return null;
            }
        }

        private static string TryDecodeBase64(string input)
        {
            try
            {
                if (string.IsNullOrEmpty(input))
                    return input;

                // Check if it looks like base64
                if (input.Length % 4 == 0 && 
                    System.Text.RegularExpressions.Regex.IsMatch(input, @"^[A-Za-z0-9+/]*={0,2}$"))
                {
                    var bytes = Convert.FromBase64String(input);
                    return Encoding.UTF8.GetString(bytes);
                }

                return input;
            }
            catch
            {
                return input;
            }
        }

        private static bool IsValidJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            try
            {
                JsonDocument.Parse(input);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static CharacterCard? ParseCharacterCard(string json)
        {
            try
            {
                // First try to parse as a direct CharacterCard
                var characterCard = JsonSerializer.Deserialize<CharacterCard>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (characterCard != null)
                {
                    // If this is a V2 card with nested data, flatten it
                    if (characterCard.Data != null)
                    {
                        return new CharacterCard
                        {
                            Name = string.IsNullOrEmpty(characterCard.Name) ? characterCard.Data.Name : characterCard.Name,
                            Description = string.IsNullOrEmpty(characterCard.Description) ? characterCard.Data.Description : characterCard.Description,
                            Personality = string.IsNullOrEmpty(characterCard.Personality) ? characterCard.Data.Personality : characterCard.Personality,
                            Scenario = string.IsNullOrEmpty(characterCard.Scenario) ? characterCard.Data.Scenario : characterCard.Scenario,
                            FirstMessage = string.IsNullOrEmpty(characterCard.FirstMessage) ? characterCard.Data.FirstMessage : characterCard.FirstMessage,
                            MessageExample = string.IsNullOrEmpty(characterCard.MessageExample) ? characterCard.Data.MessageExample : characterCard.MessageExample,
                            CreatorNotes = string.IsNullOrEmpty(characterCard.CreatorNotes) ? characterCard.Data.CreatorNotes : characterCard.CreatorNotes,
                            SystemPrompt = string.IsNullOrEmpty(characterCard.SystemPrompt) ? characterCard.Data.SystemPrompt : characterCard.SystemPrompt,
                            PostHistoryInstructions = string.IsNullOrEmpty(characterCard.PostHistoryInstructions) ? characterCard.Data.PostHistoryInstructions : characterCard.PostHistoryInstructions,
                            AlternateGreetings = characterCard.AlternateGreetings.Any() ? characterCard.AlternateGreetings : characterCard.Data.AlternateGreetings,
                            Tags = characterCard.Tags.Any() ? characterCard.Tags : characterCard.Data.Tags,
                            Creator = string.IsNullOrEmpty(characterCard.Creator) ? characterCard.Data.Creator : characterCard.Creator,
                            CharacterVersion = string.IsNullOrEmpty(characterCard.CharacterVersion) ? characterCard.Data.CharacterVersion : characterCard.CharacterVersion,
                            Spec = characterCard.Spec,
                            SpecVersion = characterCard.SpecVersion
                        };
                    }

                    return characterCard;
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing character card JSON: {ex.Message}");
                return null;
            }
        }

        public static string[] GetSupportedExtensions()
        {
            return new[] { ".png", ".json" };
        }

        public static bool IsSupportedFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return GetSupportedExtensions().Any(ext => ext == extension);
        }
    }
}
