# Local Chat Agent

A local console-based chat agent that connects to OpenAI-compatible APIs with tool use capabilities including web search and extensible custom tools.

## Features

- **OpenAI-Compatible API Support**: Works with OpenAI API or any compatible endpoint (like Ollama, LM Studio, etc.)
- **Character Card Support**: Load AI personalities from PNG character cards or JSON files to customize behavior and responses
- **Real-time Streaming**: Support for streaming responses with real-time text generation
- **Tool Use Capabilities**: Built-in support for function calling with tools
- **Multi-Engine Web Search**: Search the web using DuckDuckGo, Bing, or Google search engines with HTML parsing
- **Web Page Fetching**: Fetch and convert web pages to clean, LLM-readable text format with optional link inclusion
- **Calculator**: Perform basic mathematical calculations with expression evaluation
- **Piped Input Support**: Accept input from files or other commands via stdin redirection
- **Extensible Architecture**: Easy to add custom tools through the IToolHandler interface
- **Conversation History Management**: Maintains context across interactions with history viewing and clearing
- **Console Commands**: Built-in commands for managing the chat session and toggling features

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
- Set `Model` to your preferred model (e.g., `gpt-4`, `gpt-3.5-turbo`, `gpt-4o`)

#### For Local LLM (Ollama):
- Set `BaseUrl` to `http://localhost:11434/v1`
- Set `ApiKey` to empty string or remove it
- Set `Model` to your local model name (e.g., `llama3.2`, `mistral`, `codellama`)

#### For LM Studio:
- Set `BaseUrl` to `http://localhost:1234/v1`
- Set `ApiKey` to empty string or remove it
- Set `Model` to the loaded model name

#### For Other Local Servers:
- Set `BaseUrl` to your local server's endpoint (e.g., `http://localhost:8080/v1`)
- Configure `ApiKey` as required by your local server
- Set `Model` to the appropriate model name

### 2. Web Search Configuration (Optional)

The web search tool supports multiple search engines:

#### **DuckDuckGo (Default - No Configuration Required)**
- Works out of the box with no API keys required
- Provides direct HTML parsing from search results
- Limited results but fully functional

#### **Google Custom Search API (Enhanced Results)**
For enhanced web search capabilities with Google:

1. Go to the [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one
3. Enable the Custom Search API
4. Create credentials (API key)
5. Set up a Custom Search Engine at [Google CSE](https://cse.google.com/)
6. Add your API key and Search Engine ID to `appsettings.json`

#### **Bing Search (Alternative)**
The tool also supports Bing search through HTML parsing (no API key required).

**Search Engine Usage:**
```json
{
  "WebSearch": {
    "Enabled": true,
    "MaxResults": 5,
    "GoogleSearchApiKey": "optional-google-api-key",
    "GoogleSearchEngineId": "optional-search-engine-id"
  }
}
```

You can specify which search engine to use in your queries:
- `"What's the weather?" (uses DuckDuckGo by default)`
- `"Search Google for latest news"`
- `"Use Bing to find information about..."`

### 3. Character Cards

Character cards allow you to customize the AI's personality, behavior, and initial context. The application supports both PNG character cards (with embedded metadata) and JSON character card files.

#### **Setting Up Character Cards**

1. **Create a characters directory**: The application will automatically create a `characters` directory in the application folder on first run.

2. **Add character card files**: Place your character card files in the `characters` directory.

#### **Supported Formats**

**PNG Character Cards** (Recommended for compatibility):
- PNG images with embedded JSON metadata in tEXt chunks
- Common metadata keys: `chara`, `ccv2`, `Comment`, `Description`, `UserComment`
- Data can be base64 encoded or plain JSON
- Compatible with tools like CharacterAI, Tavern AI, and SillyTavern

**JSON Character Cards**:
- Direct JSON files with character data
- Easier to create and edit manually

#### **Character Card Format**

Both PNG and JSON character cards support the following fields:

```json
{
  "name": "Character Name",
  "description": "Brief description of the character",
  "personality": "Character's personality traits",
  "scenario": "The setting or context",
  "first_mes": "Character's initial greeting message",
  "mes_example": "Example conversation showing character's style",
  "system_prompt": "Custom system prompt (overrides other fields)",
  "post_history_instructions": "Instructions to append after conversation",
  "alternate_greetings": ["Alternative greeting 1", "Alternative greeting 2"],
  "tags": ["tag1", "tag2"],
  "creator": "Creator name",
  "character_version": "1.0"
}
```

#### **Character Card Selection**

- **Single card**: If only one character card is found, it will be loaded automatically
- **Multiple cards**: The application will present a selection menu at startup
- **No cards**: The application runs with the default assistant personality

#### **Character Card Commands**

- `/character` - Display information about the currently loaded character card
- `/clear` - Reset conversation history (character's first message will be shown again)

#### **Example Character Card**

```json
{
  "name": "Elena",
  "description": "A knowledgeable research assistant",
  "personality": "Curious, patient, thorough, and encouraging",
  "scenario": "Elena is working as a personal research assistant",
  "first_mes": "Hello! I'm Elena, your research assistant. What would you like to explore today?",
  "system_prompt": "You are Elena, a helpful research assistant who loves to learn and share knowledge.",
  "tags": ["assistant", "research", "educational"],
  "creator": "Example"
}
```

### 4. Build and Run

```bash
dotnet build
dotnet run
```

### 4. Piped Input Support

The agent supports receiving input from files or other commands:

```bash
# From a file
dotnet run < input.txt

# From echo command
echo "What is the capital of France?" | dotnet run

# From multiple lines
type questions.txt | dotnet run
```

## Usage

### Basic Chat
Just type your message and press Enter:
```
You: Hello, how are you?
Assistant: Hello! I'm doing well, thank you for asking. How can I help you today?
```

### Command Examples
```
You: /help
Available tools:
  - web_search
  - web_fetch  
  - calculator

Commands:
  /help      - Show this help message
  /clear     - Clear conversation history
  /history   - Show conversation history
  /character - Show current character card info
  /stream    - Toggle streaming mode (current: OFF)
  /exit      - Exit the application

You: /stream
Streaming mode is now ON
Note: Tool calls are not supported in streaming mode.
```

### Using Tools
The agent will automatically use tools when needed:

**Web Search (Multiple Engines):**
```
You: What's the current weather in New York?
Assistant: [Tool Call: web_search]
I'll search for the current weather in New York for you.

Based on my search results, the current weather in New York is...
```

**Search Engine Selection:**
```
You: Search Google for the latest AI news
Assistant: [Tool Call: web_search]
Searching Google for the latest AI news...

You: Search Google for the latest AI news
Assistant: [Tool Call: web_search]
Searching Google for the latest AI news...

You: Use Bing to find information about quantum computing
Assistant: [Tool Call: web_search]
Using Bing to search for information about quantum computing...
```

### Tool Parameters and Options

**Web Search Options:**
- `query`: The search terms (required)
- `num_results`: Number of results to return (1-10, default: 5)
- `search_engine`: Choose between "duckduckgo", "bing", or "google" (default: "duckduckgo")

**Web Fetch Options:**
- `url`: The webpage URL to fetch (required)
- `include_links`: Whether to include links in the output (default: false)
- `max_length`: Maximum content length to return (default: unlimited)

**Calculator Options:**
- `expression`: Mathematical expression to evaluate (supports +, -, *, /, parentheses)

```

**Mathematical Calculations:**
```
You: Calculate 15 * 23 + 47
Assistant: [Tool Call: calculator]
Result: 15 * 23 + 47 = 392
```

**Web Page Content Fetching:**
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

**Advanced Web Fetch Options:**
```
You: Fetch https://example.com and include all links
Assistant: [Tool Call: web_fetch]
Fetching webpage with links included...
```

### Console Commands

- `/help` - Show available commands and current tool list
- `/clear` - Clear conversation history
- `/history` - Show full conversation history with tool calls
- `/stream` - Toggle streaming mode ON/OFF
- `/exit` or `/quit` - Exit the application

### Streaming Mode

The agent supports real-time streaming responses where text appears as it's generated, similar to ChatGPT's interface. 

- Use `/stream` to toggle streaming mode on or off
- In streaming mode, responses appear word by word in real-time
- **Note**: Tool calls are not supported in streaming mode
- Streaming works with any OpenAI-compatible API that supports the `stream: true` parameter

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

Then register your tool in the `InitializeChatAgent()` method in `Program.cs`:

```csharp
// Register tools
toolManager.RegisterTool(new WebSearchTool());
toolManager.RegisterTool(new WebFetchTool());
toolManager.RegisterTool(new CalculatorTool());
toolManager.RegisterTool(new MyCustomTool()); // Add your custom tool here
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
- System.Text.Json (for JSON serialization and API communication)
- Microsoft.Extensions.Configuration (for appsettings.json configuration)
- Microsoft.Extensions.DependencyInjection (for dependency injection)
- Microsoft.Extensions.Hosting (for application hosting and lifecycle)
- Microsoft.Extensions.Logging (for console logging)
- HtmlAgilityPack (for HTML parsing in web search and fetch tools)
- Microsoft.Web.WebView2 (for potential future web-based features)

## Troubleshooting

### Connection Issues
- Verify your API endpoint is correct and accessible
- Check your API key if using OpenAI or other authenticated services
- For local LLMs, ensure the server is running and accepting connections
- Test the endpoint manually with curl or a REST client

### Tool Execution Issues
- Check that the model supports function calling (most modern models do)
- Verify tool parameters are correctly defined in the tool implementation
- Review console output for tool execution logs and error messages
- Ensure required dependencies are installed (e.g., HtmlAgilityPack for web tools)

### Web Search Issues
- If Google Search isn't working, verify API key and search engine ID configuration
- DuckDuckGo and Bing fallbacks work without API keys but may have rate limits
- Web search requires internet connectivity and may be blocked by firewalls
- Some websites may block automated requests - this is normal behavior

### Streaming Issues
- Streaming mode disables tool calls in most API implementations
- If streaming appears broken, try toggling it off with `/stream` command
- Streaming requires the API endpoint to support the `stream: true` parameter

### Performance Issues
- Large conversation histories may slow down responses - use `/clear` to reset
- Web scraping can be slow depending on target website response times
- Calculator tool is fast, but complex expressions may take time to parse

### Input/Output Issues
- When using piped input, the application will exit after processing all input
- For interactive use, run without pipes: `dotnet run`
- Console output formatting may vary between different terminal environments

## License

This project is provided as-is for educational and development purposes.
