using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using LocalChatAgent.Models;
using LocalChatAgent.Tools;

namespace LocalChatAgent.Tools
{
    public class WebFetchTool : IToolHandler
    {
        private readonly HttpClient _httpClient;

        public string Name => "web_fetch";
        public string Description => "Fetch a webpage and convert it to clean, LLM-readable text format";

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
                                description = "The URL of the webpage to fetch and convert to text"
                            },
                            include_links = new
                            {
                                type = "boolean",
                                description = "Whether to include links in the output (default: false)",
                                @default = false
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
                bool includeLinks = false;
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

                var html = await response.Content.ReadAsStringAsync();
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Remove script and style elements
                var scriptsAndStyles = doc.DocumentNode.SelectNodes("//script | //style | //noscript");
                if (scriptsAndStyles != null)
                {
                    foreach (var node in scriptsAndStyles)
                    {
                        node.Remove();
                    }
                }

                var result = new StringBuilder();

                // Extract title
                var titleNode = doc.DocumentNode.SelectSingleNode("//title");
                if (titleNode != null)
                {
                    result.AppendLine($"Title: {titleNode.InnerText.Trim()}");
                    result.AppendLine();
                }

                // Extract meta description
                var metaDesc = doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
                if (metaDesc != null)
                {
                    var description = metaDesc.GetAttributeValue("content", "");
                    if (!string.IsNullOrEmpty(description))
                    {
                        result.AppendLine($"Description: {description.Trim()}");
                        result.AppendLine();
                    }
                }

                // Extract main content
                result.AppendLine("Content:");
                result.AppendLine("--------");

                // Try to find main content areas first
                var mainContentNodes = doc.DocumentNode.SelectNodes(
                    "//main | //article | //div[@class*='content'] | //div[@id*='content'] | " +
                    "//div[@class*='article'] | //div[@id*='article'] | //section[@class*='content']");

                HtmlNode contentContainer;
                if (mainContentNodes != null && mainContentNodes.Count > 0)
                {
                    // Use the first main content area found
                    contentContainer = mainContentNodes[0];
                }
                else
                {
                    // Fall back to body if no main content area is found
                    contentContainer = doc.DocumentNode.SelectSingleNode("//body") ?? doc.DocumentNode;
                }

                ExtractTextContent(contentContainer, result, includeLinks);

                var text = result.ToString();

                // Truncate if necessary
                if (text.Length > maxLength)
                {
                    text = text.Substring(0, maxLength) + "... [Content truncated]";
                }

                return text;
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

        private void ExtractTextContent(HtmlNode node, StringBuilder result, bool includeLinks)
        {
            if (node == null) return;

            // Skip certain elements that typically don't contain useful content
            var skipTags = new[] { "nav", "header", "footer", "aside", "advertisement", "ads" };
            if (Array.Exists(skipTags, tag => string.Equals(node.Name, tag, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            // Handle different node types
            switch (node.NodeType)
            {
                case HtmlNodeType.Text:
                    var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        result.Append(text + " ");
                    }
                    break;

                case HtmlNodeType.Element:
                    // Handle specific elements
                    switch (node.Name.ToLower())
                    {
                        case "h1":
                        case "h2":
                        case "h3":
                        case "h4":
                        case "h5":
                        case "h6":
                            result.AppendLine();
                            result.AppendLine($"{new string('#', int.Parse(node.Name.Substring(1)))} {node.InnerText.Trim()}");
                            result.AppendLine();
                            break;

                        case "p":
                            result.AppendLine();
                            ExtractTextFromChildren(node, result, includeLinks);
                            result.AppendLine();
                            break;

                        case "br":
                            result.AppendLine();
                            break;

                        case "li":
                            result.AppendLine();
                            result.Append("• ");
                            ExtractTextFromChildren(node, result, includeLinks);
                            result.AppendLine();
                            break;

                        case "a":
                            var linkText = node.InnerText.Trim();
                            if (!string.IsNullOrEmpty(linkText))
                            {
                                if (includeLinks)
                                {
                                    var href = node.GetAttributeValue("href", "");
                                    result.Append($"{linkText} ({href}) ");
                                }
                                else
                                {
                                    result.Append($"{linkText} ");
                                }
                            }
                            break;

                        case "img":
                            if (includeLinks)
                            {
                                var alt = node.GetAttributeValue("alt", "");
                                var src = node.GetAttributeValue("src", "");
                                if (!string.IsNullOrEmpty(alt))
                                {
                                    result.Append($"[Image: {alt}] ");
                                }
                                else if (!string.IsNullOrEmpty(src))
                                {
                                    result.Append($"[Image: {src}] ");
                                }
                            }
                            break;

                        case "div":
                        case "span":
                        case "section":
                        case "article":
                        case "main":
                            // For container elements, just process children
                            ExtractTextFromChildren(node, result, includeLinks);
                            break;

                        default:
                            // For other elements, extract text from children
                            ExtractTextFromChildren(node, result, includeLinks);
                            break;
                    }
                    break;
            }
        }

        private void ExtractTextFromChildren(HtmlNode node, StringBuilder result, bool includeLinks)
        {
            foreach (var child in node.ChildNodes)
            {
                ExtractTextContent(child, result, includeLinks);
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
