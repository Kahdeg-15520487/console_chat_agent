# Local Chat Agent

A local console-based chat agent that connects to OpenAI-compatible APIs with tool use capabilities including web search and extensible custom tools.

## Features

- **OpenAI-Compatible API Support**: Works with OpenAI API or any compatible endpoint (like Ollama, LM Studio, etc.)
- **Tool Use Capabilities**: Built-in support for function calling with tools
- **Web Search**: Search the web for current information using Google Custom Search API or DuckDuckGo
- **Web Page Fetching**: Fetch and convert web pages to clean, LLM-readable text format
- **Calculator**: Perform mathematical calculations
- **Extensible Architecture**: Easy to add custom tools
- **Conversation History**: Maintains context across interactions
- **Console Commands**: Built-in commands for managing the chat session

## Setup

### 1. Configuration

Edit `appsettings.json` to configure your API endpoint and settings:

```json
{
  "ApiConfig": {
    "BaseUrl": "https://api.openai.com/v1",
    "ApiKey": "your-api-key-here",
    "Model": "gpt-3.5-turbo",
    "MaxTokens": 1000,
    "Temperature": 0.7
  },
  "WebSearch": {
    "Enabled": true,
    "MaxResults": 5,
    "GoogleSearchApiKey": "optional-google-api-key",
    "GoogleSearchEngineId": "optional-search-engine-id"
  },
  "Tools": {
    "Enabled": true
  }
}
```

#### For OpenAI API:
- Set `BaseUrl` to `https://api.openai.com/v1`
- Set `ApiKey` to your OpenAI API key
- Set `Model` to your preferred model (e.g., `gpt-4`, `gpt-3.5-turbo`)

#### For Local LLM (Ollama):
- Set `BaseUrl` to `http://localhost:11434/v1`
- Set `ApiKey` to empty string or remove it
- Set `Model` to your local model name (e.g., `llama3.2`, `mistral`)

#### For LM Studio:
- Set `BaseUrl` to `http://localhost:1234/v1`
- Set `ApiKey` to empty string or remove it
- Set `Model` to the loaded model name

### 2. Web Search Configuration (Optional)

For enhanced web search capabilities, you can configure Google Custom Search API:

1. Go to the [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the Custom Search API
4. Create credentials (API key)
5. Set up a Custom Search Engine at [Google CSE](https://cse.google.com/)
6. Add your API key and Search Engine ID to `appsettings.json`

If not configured, the agent will use DuckDuckGo's API with limited results.

### 3. Build and Run

```bash
dotnet build
dotnet run
```

## Usage

### Basic Chat
Just type your message and press Enter:
```
You: Hello, how are you?
Assistant: Hello! I'm doing well, thank you for asking. How can I help you today?
```

### Using Tools
The agent will automatically use tools when needed:

```
You: What's the current weather in New York?
Assistant: [Tool Call: web_search]
I'll search for the current weather in New York for you.

Based on my search results, the current weather in New York is...
```

```
You: Calculate 15 * 23 + 47
Assistant: [Tool Call: calculator]
Result: 15 * 23 + 47 = 392
```

```
You: Please fetch the content from https://example.com/article
Assistant: [Tool Call: web_fetch]
I'll fetch the content from that webpage for you.

Title: Example Article
Description: This is an example article...

Content:
--------
# Main Heading
This is the main content of the article...
```

### Console Commands

- `/help` - Show available commands
- `/clear` - Clear conversation history
- `/history` - Show conversation history
- `/exit` or `/quit` - Exit the application

## Adding Custom Tools

To add a custom tool, implement the `IToolHandler` interface:

```csharp
using System;
using System.Text.Json;
using System.Threading.Tasks;
using LocalChatAgent.Models;
using LocalChatAgent.Tools;

public class MyCustomTool : IToolHandler
{
    public string Name => "my_custom_tool";
    public string Description => "Description of what this tool does";

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
                        input_parameter = new
                        {
                            type = "string",
                            description = "Description of the parameter"
                        }
                    },
                    required = new[] { "input_parameter" }
                }
            }
        };
    }

    public Task<string> ExecuteAsync(JsonElement parameters)
    {
        try
        {
            string inputValue = parameters.GetProperty("input_parameter").GetString() ?? "";
            
            // Your tool logic here
            string result = ProcessInput(inputValue);
            
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error: {ex.Message}");
        }
    }

    private string ProcessInput(string input)
    {
        // Implement your tool's functionality
        return $"Processed: {input}";
    }
}
```

Then register your tool in `Program.cs`:

```csharp
toolManager.RegisterTool(new MyCustomTool());
```

## Project Structure

```
LocalChatAgent/
├── Models/
│   └── ApiModels.cs          # API request/response models
├── Services/
│   ├── OpenAIClient.cs       # HTTP client for API communication
│   └── ChatAgent.cs          # Main chat logic and tool orchestration
├── Tools/
│   ├── IToolHandler.cs       # Tool interface
│   ├── ToolManager.cs        # Tool registration and execution
│   ├── WebSearchTool.cs      # Web search functionality
│   ├── WebFetchTool.cs       # Web page content fetching
│   └── CalculatorTool.cs     # Mathematical calculations
├── Program.cs                # Main console application
├── appsettings.json          # Configuration file
└── LocalChatAgent.csproj     # Project file
```

## Dependencies

- .NET 8.0
- System.Text.Json
- Microsoft.Extensions.Configuration
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.Logging
- HtmlAgilityPack

## Troubleshooting

### Connection Issues
- Verify your API endpoint is correct and accessible
- Check your API key if using OpenAI or other authenticated services
- For local LLMs, ensure the server is running and accepting connections

### Tool Execution Issues
- Check that the model supports function calling
- Verify tool parameters are correctly defined
- Review console output for tool execution logs

### Search Issues
- If Google Search isn't working, check API key and search engine ID
- DuckDuckGo fallback provides limited results
- Web search requires internet connectivity

## License

This project is provided as-is for educational and development purposes.
