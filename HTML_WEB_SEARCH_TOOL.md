# HTML Web Search Tool

## Overview
The `HtmlWebSearchTool` is a cross-platform web search implementation that fetches and parses HTML pages from popular search engines. Unlike API-based search tools, this approach works by scraping search result pages directly, making it free and not requiring API keys.

## Features
- **Cross-platform compatibility**: Works on Windows, macOS, and Linux
- **Multiple search engines**: Supports DuckDuckGo, Bing, and Google
- **No API keys required**: Scrapes public search result pages
- **Configurable results**: Specify number of results (1-10)
- **Robust parsing**: Uses HtmlAgilityPack for reliable HTML parsing

## Supported Search Engines

### DuckDuckGo (Default)
- **URL**: `https://html.duckduckgo.com/html/`
- **Pros**: Most reliable, rarely blocks automated requests
- **Cons**: Sometimes fewer results than other engines

### Bing
- **URL**: `https://www.bing.com/search`
- **Pros**: Good results, moderate blocking
- **Cons**: May occasionally detect and block automated requests

### Google
- **URL**: `https://www.google.com/search`
- **Pros**: Comprehensive results
- **Cons**: Most likely to detect and block automated requests

## Usage

The tool accepts the following parameters:
- `query` (required): The search query string
- `num_results` (optional): Number of results to return (default: 5, max: 10)
- `search_engine` (optional): Which engine to use - "duckduckgo", "bing", or "google" (default: "duckduckgo")

## Technical Implementation

### HTTP Client Configuration
- Uses a realistic User-Agent string to avoid detection
- 30-second timeout for requests
- Follows standard HTTP practices

### HTML Parsing
- Uses HtmlAgilityPack for robust HTML parsing
- Handles HTML entities and encoding properly
- Extracts titles, URLs, and snippets from search results

### Error Handling
- Graceful degradation when search engines are unavailable
- Clear error messages for debugging
- Automatic fallback to alternative parsing strategies

## Example Output

```
Web search results for: artificial intelligence news
Source: DuckDuckGo

1. Latest AI Developments in 2025
   URL: https://example.com/ai-news-2025
   Summary: Recent breakthroughs in artificial intelligence including new language models...

2. AI Industry Report
   URL: https://example.com/ai-industry-report
   Summary: Comprehensive analysis of the current state of AI technology...
```

## Advantages over API-based Solutions

1. **No API limits**: No rate limiting or usage quotas
2. **Free**: No API keys or paid subscriptions required
3. **Immediate deployment**: Works out of the box
4. **Multiple sources**: Can switch between search engines as needed

## Limitations

1. **Parsing brittleness**: Search engines may change their HTML structure
2. **Rate limiting**: Search engines may block excessive requests
3. **Legal considerations**: Check terms of service for automated access
4. **Performance**: Slower than direct API calls due to HTML parsing overhead

## Best Practices

1. **Use DuckDuckGo as default**: Most reliable for automated requests
2. **Implement caching**: Cache results to reduce requests
3. **Add delays**: Space out requests to avoid rate limiting
4. **Handle failures gracefully**: Have fallback mechanisms
5. **Respect robots.txt**: Follow website scraping guidelines

## Integration

The tool is automatically registered in the `Program.cs` file:

```csharp
toolManager.RegisterTool(new HtmlWebSearchTool());
```

It appears alongside other tools in the chat agent and can be invoked by the AI model when web search functionality is needed.
