using System;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using LocalChatAgent.Models;
using LocalChatAgent.Tools;

namespace LocalChatAgent.Tools
{
    public class WebSearchTool : IToolHandler
    {
        private readonly HttpClient _httpClient;
        private readonly string _searchApiKey;
        private readonly string _searchEngineId;

        public string Name => "web_search";
        public string Description => "Search the web for current information on any topic";

        public WebSearchTool(string searchApiKey, string searchEngineId)
        {
            _httpClient = new HttpClient();
            _searchApiKey = searchApiKey;
            _searchEngineId = searchEngineId;
        }

        public Tool GetToolDefinition()
        {
            return new Tool
            {
                Type = "function",
                Function = new Function
                {
                    Name = Name,
                    Description = Description,
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new
                            {
                                type = "string",
                                description = "The search query to find information on the web"
                            },
                            num_results = new
                            {
                                type = "integer",
                                description = "Number of search results to return (default: 5, max: 10)",
                                @default = 5
                            }
                        },
                        required = new[] { "query" }
                    }
                }
            };
        }

        public async Task<string> ExecuteAsync(JsonElement parameters)
        {
            try
            {
                string query = parameters.GetProperty("query").GetString() ?? "";
                int numResults = 5;
                
                if (parameters.TryGetProperty("num_results", out var numResultsProperty))
                {
                    numResults = Math.Min(numResultsProperty.GetInt32(), 10);
                }

                if (string.IsNullOrEmpty(query))
                {
                    return "Error: Search query cannot be empty";
                }

                // If using Google Custom Search API
                if (!string.IsNullOrEmpty(_searchApiKey) && !string.IsNullOrEmpty(_searchEngineId))
                {
                    return await SearchWithGoogle(query, numResults);
                }
                else
                {
                    // Fallback to a mock search or DuckDuckGo
                    return await SearchWithDuckDuckGo(query, numResults);
                }
            }
            catch (Exception ex)
            {
                return $"Error performing web search: {ex.Message}";
            }
        }

        private async Task<string> SearchWithGoogle(string query, int numResults)
        {
            try
            {
                string url = $"https://www.googleapis.com/customsearch/v1?key={_searchApiKey}&cx={_searchEngineId}&q={Uri.EscapeDataString(query)}&num={numResults}";
                
                var response = await _httpClient.GetAsync(url);
                var jsonResponse = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    return $"Search API error: {response.StatusCode} - {jsonResponse}";
                }

                var searchResult = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
                var results = new StringBuilder();
                results.AppendLine($"Web search results for: {query}");
                results.AppendLine();

                if (searchResult.TryGetProperty("items", out var items))
                {
                    int count = 1;
                    foreach (var item in items.EnumerateArray())
                    {
                        if (count > numResults) break;

                        var title = item.GetProperty("title").GetString();
                        var link = item.GetProperty("link").GetString();
                        var snippet = item.GetProperty("snippet").GetString();

                        results.AppendLine($"{count}. {title}");
                        results.AppendLine($"   URL: {link}");
                        results.AppendLine($"   Summary: {snippet}");
                        results.AppendLine();
                        count++;
                    }
                }
                else
                {
                    results.AppendLine("No search results found.");
                }

                return results.ToString();
            }
            catch (Exception ex)
            {
                return $"Error with Google search: {ex.Message}";
            }
        }

        private async Task<string> SearchWithDuckDuckGo(string query, int numResults)
        {
            try
            {
                // Using DuckDuckGo Instant Answer API (limited but free)
                string url = $"https://api.duckduckgo.com/?q={Uri.EscapeDataString(query)}&format=json&no_html=1&skip_disambig=1";
                
                var response = await _httpClient.GetAsync(url);
                var jsonResponse = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    return $"DuckDuckGo API error: {response.StatusCode}";
                }

                var searchResult = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
                var results = new StringBuilder();
                results.AppendLine($"Web search results for: {query}");
                results.AppendLine();

                // Try to get abstract first
                if (searchResult.TryGetProperty("Abstract", out var abstractProp) && !string.IsNullOrEmpty(abstractProp.GetString()))
                {
                    results.AppendLine("Summary:");
                    results.AppendLine(abstractProp.GetString());
                    results.AppendLine();

                    if (searchResult.TryGetProperty("AbstractURL", out var abstractUrl) && !string.IsNullOrEmpty(abstractUrl.GetString()))
                    {
                        results.AppendLine($"Source: {abstractUrl.GetString()}");
                        results.AppendLine();
                    }
                }

                // Try to get related topics
                if (searchResult.TryGetProperty("RelatedTopics", out var relatedTopics))
                {
                    int count = 1;
                    foreach (var topic in relatedTopics.EnumerateArray())
                    {
                        if (count > numResults) break;

                        if (topic.TryGetProperty("Text", out var text) && topic.TryGetProperty("FirstURL", out var firstUrl))
                        {
                            results.AppendLine($"{count}. {text.GetString()}");
                            results.AppendLine($"   URL: {firstUrl.GetString()}");
                            results.AppendLine();
                            count++;
                        }
                    }
                }

                if (results.Length <= $"Web search results for: {query}".Length + 4)
                {
                    results.AppendLine("No detailed search results found. You may need to configure Google Custom Search API for better results.");
                    results.AppendLine("Set GOOGLE_SEARCH_API_KEY and GOOGLE_SEARCH_ENGINE_ID in appsettings.json");
                }

                return results.ToString();
            }
            catch (Exception ex)
            {
                return $"Error with DuckDuckGo search: {ex.Message}";
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
