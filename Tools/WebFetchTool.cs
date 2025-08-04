using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LocalChatAgent.Models;
using LocalChatAgent.Tools;
using Textify;

namespace LocalChatAgent.Tools
{
    public class WebFetchTool : IToolHandler
    {
        private readonly HttpClient _httpClient;

        public string Name => "web_fetch";
        public string Description => "Fetch a webpage and convert it to clean, readable text format with basic markdown-like formatting";

        public WebFetchTool()
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
                            url = new
                            {
                                type = "string",
                                description = "The URL of the webpage to fetch and convert to readable text"
                            },
                            include_links = new
                            {
                                type = "boolean",
                                description = "Whether to include link references in the output (default: true)",
                                @default = true
                            },
                            max_length = new
                            {
                                type = "integer",
                                description = "Maximum length of the extracted text in characters (default: 10000, max: 50000)",
                                @default = 10000
                            }
                        },
                        required = new[] { "url" }
                    }
                }
            };
        }

        public async Task<string> ExecuteAsync(JsonElement parameters)
        {
            try
            {
                string url = parameters.GetProperty("url").GetString() ?? "";
                bool includeLinks = true;
                int maxLength = 10000;

                if (parameters.TryGetProperty("include_links", out var includeLinksProperty))
                {
                    includeLinks = includeLinksProperty.GetBoolean();
                }

                if (parameters.TryGetProperty("max_length", out var maxLengthProperty))
                {
                    maxLength = Math.Min(maxLengthProperty.GetInt32(), 50000);
                }

                if (string.IsNullOrEmpty(url))
                {
                    return "Error: URL cannot be empty";
                }

                // Validate URL format
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || 
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    return "Error: Invalid URL format. Please provide a valid HTTP or HTTPS URL.";
                }

                return await FetchAndConvertWebpage(url, includeLinks, maxLength);
            }
            catch (Exception ex)
            {
                return $"Error fetching webpage: {ex.Message}";
            }
        }

        private async Task<string> FetchAndConvertWebpage(string url, bool includeLinks, int maxLength)
        {
            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType?.ToLower() ?? "";

                Console.WriteLine($"Content-Type: {contentType}");
                Console.WriteLine(content);

                // Handle different content types
                if (contentType.Contains("application/json") || IsJsonContent(content))
                {
                    return FormatJsonContent(content, maxLength);
                }
                else if (contentType.Contains("text/plain") || IsPlainTextContent(contentType, content))
                {
                    return FormatPlainTextContent(content, maxLength);
                }
                else
                {
                    // Default to HTML processing
                    return ProcessHtmlContent(content, includeLinks, maxLength);
                }
            }
            catch (HttpRequestException ex)
            {
                return $"Error fetching webpage: {ex.Message}. Please check if the URL is accessible.";
            }
            catch (TaskCanceledException)
            {
                return "Error: Request timed out. The webpage took too long to respond.";
            }
            catch (Exception ex)
            {
                return $"Error processing webpage: {ex.Message}";
            }
        }

        private bool IsJsonContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return false;
            
            var trimmed = content.Trim();
            return (trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                   (trimmed.StartsWith("[") && trimmed.EndsWith("]"));
        }

        private bool IsPlainTextContent(string contentType, string content)
        {
            return contentType.Contains("text/") && !contentType.Contains("text/html");
        }

        private string FormatJsonContent(string jsonContent, int maxLength)
        {
            try
            {
                var result = new StringBuilder();
                result.AppendLine("Content Type: JSON");
                result.AppendLine("===================");
                result.AppendLine();

                // Try to pretty-print the JSON
                try
                {
                    var jsonDocument = JsonDocument.Parse(jsonContent);
                    var prettyJson = JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    result.AppendLine(prettyJson);
                }
                catch
                {
                    // If JSON parsing fails, just return the raw content
                    result.AppendLine("Raw JSON Content:");
                    result.AppendLine(jsonContent);
                }

                var text = result.ToString();
                if (text.Length > maxLength)
                {
                    text = text.Substring(0, maxLength) + "... [Content truncated]";
                }

                return text;
            }
            catch (Exception ex)
            {
                return $"Error formatting JSON content: {ex.Message}\n\nRaw content:\n{jsonContent}";
            }
        }

        private string FormatPlainTextContent(string textContent, int maxLength)
        {
            var result = new StringBuilder();
            result.AppendLine("Content Type: Plain Text");
            result.AppendLine("==========================");
            result.AppendLine();
            result.AppendLine(textContent);

            var text = result.ToString();
            if (text.Length > maxLength)
            {
                text = text.Substring(0, maxLength) + "... [Content truncated]";
            }

            return text;
        }

        private string ProcessHtmlContent(string htmlContent, bool includeLinks, int maxLength)
        {
            try
            {
                // Use Textify to convert HTML to text
                var converter = new HtmlToTextConverter();
                var textContent = converter.Convert(htmlContent);

                // Clean up the text content
                var cleanedContent = CleanTextContent(textContent);

                // Truncate if necessary
                if (cleanedContent.Length > maxLength)
                {
                    cleanedContent = cleanedContent.Substring(0, maxLength) + "... [Content truncated]";
                }

                return cleanedContent;
            }
            catch (Exception ex)
            {
                return $"Error processing HTML content: {ex.Message}";
            }
        }

        private string CleanTextContent(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            var lines = content.Split('\n', StringSplitOptions.None);
            var result = new StringBuilder();
            var previousLineEmpty = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip multiple consecutive empty lines (keep max 1 empty line)
                if (string.IsNullOrEmpty(trimmedLine))
                {
                    if (!previousLineEmpty)
                    {
                        result.AppendLine();
                        previousLineEmpty = true;
                    }
                    continue;
                }

                result.AppendLine(line);
                previousLineEmpty = false;
            }

            return result.ToString().Trim();
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
