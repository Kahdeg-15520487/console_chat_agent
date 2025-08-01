using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
                
                Console.WriteLine($"Sending request to: {endpoint}");
                
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

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
