using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using LocalChatAgent.Models;
using LocalChatAgent.Tools;

namespace LocalChatAgent.Tools
{
    public class WebSearchTool : IToolHandler
    {
        private readonly HttpClient _httpClient;

        public string Name => "web_search";
        public string Description => "Search the web by fetching and parsing HTML pages from search engines";

        public WebSearchTool()
        {
            _httpClient = new HttpClient();
            // Set a realistic user agent to avoid blocking
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
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
                            },
                            search_engine = new
                            {
                                type = "string",
                                description = "Search engine to use: 'duckduckgo', 'bing', or 'google' (default: 'duckduckgo')",
                                @default = "duckduckgo"
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
                string searchEngine = "duckduckgo";

                if (parameters.TryGetProperty("num_results", out var numResultsProperty))
                {
                    numResults = Math.Min(numResultsProperty.GetInt32(), 10);
                }

                if (parameters.TryGetProperty("search_engine", out var searchEngineProperty))
                {
                    searchEngine = searchEngineProperty.GetString()?.ToLower() ?? "duckduckgo";
                }

                if (string.IsNullOrEmpty(query))
                {
                    return "Error: Search query cannot be empty";
                }

                return searchEngine switch
                {
                    "bing" => await SearchBing(query, numResults),
                    "google" => await SearchGoogle(query, numResults),
                    _ => await SearchDuckDuckGo(query, numResults)
                };
            }
            catch (Exception ex)
            {
                return $"Error performing web search: {ex.Message}";
            }
        }

        private async Task<string> SearchDuckDuckGo(string query, int numResults)
        {
            try
            {
                string encodedQuery = Uri.EscapeDataString(query);
                string url = $"https://html.duckduckgo.com/html/?q={encodedQuery}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return $"DuckDuckGo search failed: {response.StatusCode}";
                }

                string html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var results = new StringBuilder();
                results.AppendLine($"Web search results for: {query}");
                results.AppendLine($"Source: DuckDuckGo");
                results.AppendLine();

                // DuckDuckGo results are in divs with class "result"
                var resultNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'result')]");

                if (resultNodes != null)
                {
                    int count = 1;
                    foreach (var node in resultNodes)
                    {
                        if (count > numResults) break;

                        var titleNode = node.SelectSingleNode(".//a[contains(@class, 'result__a')]");
                        var snippetNode = node.SelectSingleNode(".//a[contains(@class, 'result__snippet')]");

                        if (titleNode != null)
                        {
                            string title = HtmlEntity.DeEntitize(titleNode.InnerText?.Trim() ?? "");
                            string link = titleNode.GetAttributeValue("href", "");
                            string snippet = HtmlEntity.DeEntitize(snippetNode?.InnerText?.Trim() ?? "");

                            if (!string.IsNullOrEmpty(title))
                            {
                                results.AppendLine($"{count}. {title}");
                                results.AppendLine($"   URL: {link}");
                                if (!string.IsNullOrEmpty(snippet))
                                {
                                    results.AppendLine($"   Summary: {snippet}");
                                }
                                results.AppendLine();
                                count++;
                            }
                        }
                    }
                }

                if (results.Length <= $"Web search results for: {query}".Length + 50)
                {
                    results.AppendLine("No search results found or failed to parse results.");
                }

                return results.ToString();
            }
            catch (Exception ex)
            {
                return $"Error with DuckDuckGo search: {ex.Message}";
            }
        }

        private async Task<string> SearchBing(string query, int numResults)
        {
            try
            {
                string encodedQuery = Uri.EscapeDataString(query);
                string url = $"https://www.bing.com/search?q={encodedQuery}&count={numResults}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return $"Bing search failed: {response.StatusCode}";
                }

                string html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var results = new StringBuilder();
                results.AppendLine($"Web search results for: {query}");
                results.AppendLine($"Source: Bing");
                results.AppendLine();

                // Bing results are in li elements with class "b_algo"
                var resultNodes = doc.DocumentNode.SelectNodes("//li[contains(@class, 'b_algo')]");

                if (resultNodes != null)
                {
                    int count = 1;
                    foreach (var node in resultNodes)
                    {
                        if (count > numResults) break;

                        var titleNode = node.SelectSingleNode(".//h2/a");
                        var snippetNode = node.SelectSingleNode(".//p[contains(@class, 'b_lineclamp')]");

                        if (titleNode != null)
                        {
                            string title = HtmlEntity.DeEntitize(titleNode.InnerText?.Trim() ?? "");
                            string link = titleNode.GetAttributeValue("href", "");
                            string snippet = HtmlEntity.DeEntitize(snippetNode?.InnerText?.Trim() ?? "");

                            if (!string.IsNullOrEmpty(title))
                            {
                                results.AppendLine($"{count}. {title}");
                                results.AppendLine($"   URL: {link}");
                                if (!string.IsNullOrEmpty(snippet))
                                {
                                    results.AppendLine($"   Summary: {snippet}");
                                }
                                results.AppendLine();
                                count++;
                            }
                        }
                    }
                }

                if (results.Length <= $"Web search results for: {query}".Length + 50)
                {
                    results.AppendLine("No search results found or failed to parse results.");
                }

                return results.ToString();
            }
            catch (Exception ex)
            {
                return $"Error with Bing search: {ex.Message}";
            }
        }

        private async Task<string> SearchGoogle(string query, int numResults)
        {
            try
            {
                string encodedQuery = Uri.EscapeDataString(query);
                string url = $"https://www.google.com/search?q={encodedQuery}&num={numResults}";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    return $"Google search failed: {response.StatusCode}";
                }

                string html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var results = new StringBuilder();
                results.AppendLine($"Web search results for: {query}");
                results.AppendLine($"Source: Google");
                results.AppendLine();

                // Google results structure can vary, try multiple selectors
                var resultNodes = doc.DocumentNode.SelectNodes("//div[@class='g']") ??
                                 doc.DocumentNode.SelectNodes("//div[contains(@class, 'tF2Cxc')]") ??
                                 doc.DocumentNode.SelectNodes("//div[contains(@class, 'MjjYud')]");

                if (resultNodes != null)
                {
                    int count = 1;
                    foreach (var node in resultNodes)
                    {
                        if (count > numResults) break;

                        var titleNode = node.SelectSingleNode(".//h3/../a") ?? node.SelectSingleNode(".//a/h3/..");
                        var snippetNode = node.SelectSingleNode(".//span[contains(@class, 'aCOpRe')] | .//div[contains(@class, 'VwiC3b')]");

                        if (titleNode != null)
                        {
                            string title = HtmlEntity.DeEntitize(titleNode.SelectSingleNode(".//h3")?.InnerText?.Trim() ?? "");
                            string link = titleNode.GetAttributeValue("href", "");
                            string snippet = HtmlEntity.DeEntitize(snippetNode?.InnerText?.Trim() ?? "");

                            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(link))
                            {
                                results.AppendLine($"{count}. {title}");
                                results.AppendLine($"   URL: {link}");
                                if (!string.IsNullOrEmpty(snippet))
                                {
                                    results.AppendLine($"   Summary: {snippet}");
                                }
                                results.AppendLine();
                                count++;
                            }
                        }
                    }
                }

                if (results.Length <= $"Web search results for: {query}".Length + 50)
                {
                    results.AppendLine("No search results found or failed to parse results.");
                    results.AppendLine("Note: Google may be blocking automated requests. Try using DuckDuckGo or Bing instead.");
                }

                return results.ToString();
            }
            catch (Exception ex)
            {
                return $"Error with Google search: {ex.Message}";
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
