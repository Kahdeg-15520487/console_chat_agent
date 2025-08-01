using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LocalChatAgent.Models;

namespace LocalChatAgent.Services
{
    public class OpenAIClient
    {
        private readonly HttpClient _httpClient;
        private readonly ApiConfig _config;

        public OpenAIClient(ApiConfig config)
        {
            _config = config;
            _httpClient = new HttpClient();
            
            if (!string.IsNullOrEmpty(_config.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
            }
            
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "LocalChatAgent/1.0");
        }

        public async Task<ChatResponse> SendChatRequestAsync(ChatRequest request)
        {
            try
            {
                var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var endpoint = $"{_config.BaseUrl.TrimEnd('/')}/chat/completions";
                
                Console.WriteLine($"LLM: Sending request to: {endpoint}");
                
                var response = await _httpClient.PostAsync(endpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"API request failed with status {response.StatusCode}: {responseContent}");
                }

                var chatResponse = JsonSerializer.Deserialize<ChatResponse>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return chatResponse ?? throw new InvalidOperationException("Failed to deserialize response");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error sending chat request: {ex.Message}", ex);
            }
        }

        public async IAsyncEnumerable<string> SendChatRequestStreamAsync(ChatRequest request, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Ensure streaming is enabled
            request.Stream = true;

            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var endpoint = $"{_config.BaseUrl.TrimEnd('/')}/chat/completions";
            
            Console.WriteLine($"LLM: Sending streaming request to: {endpoint}");

            using var request2 = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = content
            };

            using var response = await _httpClient.SendAsync(request2, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API request failed with status {response.StatusCode}: {errorContent}");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6); // Remove "data: " prefix
                    
                    if (data == "[DONE]")
                    {
                        Console.WriteLine("LLM: Stream completed");
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(data))
                        continue;

                    ChatCompletionChunk? chunk;
                    try
                    {
                        chunk = JsonSerializer.Deserialize<ChatCompletionChunk>(data, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"LLM: Failed to parse chunk: {ex.Message}");
                        // Skip malformed chunks
                        continue;
                    }
                    
                    var deltaContent = chunk?.Choices?[0]?.Delta?.Content;
                    
                    if (!string.IsNullOrEmpty(deltaContent))
                    {
                        yield return deltaContent;
                    }
                }
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
