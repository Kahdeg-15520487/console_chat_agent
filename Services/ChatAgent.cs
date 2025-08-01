using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LocalChatAgent.Models;
using LocalChatAgent.Tools;

namespace LocalChatAgent.Services
{
    public class ChatAgent
    {
        private readonly OpenAIClient _openAIClient;
        private readonly ToolManager _toolManager;
        private readonly ApiConfig _config;
        private readonly List<ChatMessage> _conversationHistory;

        public ChatAgent(OpenAIClient openAIClient, ToolManager toolManager, ApiConfig config)
        {
            _openAIClient = openAIClient;
            _toolManager = toolManager;
            _config = config;
            _conversationHistory = new List<ChatMessage>();
            
            // Add system message with dynamic tool descriptions
            var systemPrompt = BuildSystemPrompt();
            _conversationHistory.Add(new ChatMessage
            {
                Role = "system",
                Content = systemPrompt
            });
        }

        private string BuildSystemPrompt()
        {
            var basePrompt = "You are a helpful AI assistant with access to tools. Be concise and direct in your responses. Do not use markdown formatting, bullet points, or special formatting. Respond in plain text only.";
            
            var availableTools = _toolManager.GetAvailableTools();
            if (availableTools.Any())
            {
                var toolDescriptions = availableTools.Select(tool => $"use the {tool.Function.Name} tool for {tool.Function.Description.ToLower()}");
                basePrompt += $" When needed, {string.Join(", ", toolDescriptions)}.";
            }
            
            return basePrompt;
        }

        public async Task<string> SendMessageAsync(string userMessage)
        {
            try
            {
                // Add user message to conversation history
                _conversationHistory.Add(new ChatMessage
                {
                    Role = "user",
                    Content = userMessage
                });

                // Create the request
                var request = new ChatRequest
                {
                    Model = _config.Model,
                    Messages = _conversationHistory.ToList(),
                    MaxTokens = _config.MaxTokens,
                    Temperature = _config.Temperature,
                    Tools = _toolManager.GetAvailableTools(),
                    ToolChoice = "auto"
                };

                // Get the response
                var response = await _openAIClient.SendChatRequestAsync(request);
                
                if (response.Choices?.Any() != true)
                {
                    return "No response received from the API";
                }

                var choice = response.Choices[0];
                var assistantMessage = choice.Message;

                // Add assistant message to conversation history
                _conversationHistory.Add(assistantMessage);

                // Check if the assistant wants to use tools
                if (assistantMessage.ToolCalls?.Any() == true)
                {
                    return await HandleToolCallsAsync(assistantMessage.ToolCalls);
                }

                return assistantMessage.Content ?? "No content in response";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        public async IAsyncEnumerable<string> SendMessageStreamAsync(string userMessage, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var chunk in SendMessageStreamInternalAsync(userMessage, cancellationToken))
            {
                yield return chunk;
            }
        }

        private async IAsyncEnumerable<string> SendMessageStreamInternalAsync(string userMessage, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Add user message to conversation history
            _conversationHistory.Add(new ChatMessage
            {
                Role = "user",
                Content = userMessage
            });

            // Create the request (streaming doesn't support tool calls in most implementations)
            var request = new ChatRequest
            {
                Model = _config.Model,
                Messages = _conversationHistory.ToList(),
                MaxTokens = _config.MaxTokens,
                Temperature = _config.Temperature,
                Stream = true
                // Note: Tools are typically not supported with streaming
            };

            var responseContent = new StringBuilder();
            
            IAsyncEnumerable<string>? stream = null;
            Exception? streamException = null;
            
            try
            {
                stream = _openAIClient.SendChatRequestStreamAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                streamException = ex;
            }

            if (streamException != null)
            {
                yield return $"Error: {streamException.Message}";
                yield break;
            }

            if (stream != null)
            {
                await foreach (var chunk in stream)
                {
                    responseContent.Append(chunk);
                    yield return chunk;
                }

                // Add the complete assistant message to conversation history
                _conversationHistory.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = responseContent.ToString()
                });
            }
        }

        private async Task<string> HandleToolCallsAsync(List<ToolCall> toolCalls)
        {
            try
            {
                var toolResults = new List<ChatMessage>();

                // Execute each tool call
                foreach (var toolCall in toolCalls)
                {
                    // Format parameters for display
                    var parametersForDisplay = FormatParametersForDisplay(toolCall.Function.Arguments);
                    Console.WriteLine($"Executing tool: {toolCall.Function.Name}({parametersForDisplay})");
                    
                    var parameters = JsonSerializer.Deserialize<JsonElement>(toolCall.Function.Arguments);
                    var result = await _toolManager.ExecuteToolAsync(toolCall.Function.Name, parameters);
                    
                    // Add tool result to conversation
                    toolResults.Add(new ChatMessage
                    {
                        Role = "tool",
                        Content = result,
                        ToolCallId = toolCall.Id
                    });
                }

                // Add tool results to conversation history
                _conversationHistory.AddRange(toolResults);

                // Send another request to get the final response
                var request = new ChatRequest
                {
                    Model = _config.Model,
                    Messages = _conversationHistory.ToList(),
                    MaxTokens = _config.MaxTokens,
                    Temperature = _config.Temperature,
                    Tools = _toolManager.GetAvailableTools(),
                    ToolChoice = "auto"
                };

                var response = await _openAIClient.SendChatRequestAsync(request);
                
                if (response.Choices?.Any() != true)
                {
                    return "No response received after tool execution";
                }

                var finalMessage = response.Choices[0].Message;
                _conversationHistory.Add(finalMessage);

                return finalMessage.Content ?? "No content in final response";
            }
            catch (Exception ex)
            {
                return $"Error handling tool calls: {ex.Message}";
            }
        }

        public void ClearHistory()
        {
            _conversationHistory.Clear();
            var systemPrompt = BuildSystemPrompt();
            _conversationHistory.Add(new ChatMessage
            {
                Role = "system",
                Content = systemPrompt
            });
        }

        public List<ChatMessage> GetConversationHistory()
        {
            return _conversationHistory.ToList();
        }

        private string FormatParametersForDisplay(string argumentsJson)
        {
            try
            {
                if (string.IsNullOrEmpty(argumentsJson))
                    return "";

                var parameters = JsonSerializer.Deserialize<JsonElement>(argumentsJson);
                var paramStrings = new List<string>();

                foreach (var property in parameters.EnumerateObject())
                {
                    var value = FormatJsonValue(property.Value);
                    paramStrings.Add($"{property.Name}: {value}");
                }

                return string.Join(", ", paramStrings);
            }
            catch
            {
                // If parsing fails, return the raw JSON
                return argumentsJson;
            }
        }

        private string FormatJsonValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => $"\"{element.GetString()}\"",
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                JsonValueKind.Array => $"[{string.Join(", ", element.EnumerateArray().Select(FormatJsonValue))}]",
                JsonValueKind.Object => $"{{{string.Join(", ", element.EnumerateObject().Select(p => $"{p.Name}: {FormatJsonValue(p.Value)}"))}}}",
                _ => element.GetRawText()
            };
        }
    }
}
