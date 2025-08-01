using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using LocalChatAgent.Models;
using LocalChatAgent.Services;
using LocalChatAgent.Tools;

namespace LocalChatAgent
{
    class Program
    {
        private static ApiConfig? _apiConfig;
        private static ChatAgent? _chatAgent;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Local Chat Agent - OpenAI Compatible");
            Console.WriteLine("=====================================");
            Console.WriteLine();

            try
            {
                // Load configuration
                if (!LoadConfiguration())
                {
                    Console.WriteLine("Failed to load configuration. Please check appsettings.json");
                    return;
                }

                // Initialize the chat agent
                InitializeChatAgent();

                // Display help
                DisplayHelp();

                // Main chat loop
                await RunChatLoopAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        private static bool LoadConfiguration()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"Configuration file not found at: {configPath}");
                    return false;
                }

                var json = File.ReadAllText(configPath);
                var configDocument = JsonDocument.Parse(json);
                
                if (configDocument.RootElement.TryGetProperty("ApiConfig", out var apiConfigElement))
                {
                    _apiConfig = JsonSerializer.Deserialize<ApiConfig>(apiConfigElement.GetRawText());
                }

                if (_apiConfig == null)
                {
                    Console.WriteLine("ApiConfig section not found in appsettings.json");
                    return false;
                }

                Console.WriteLine($"Loaded configuration:");
                Console.WriteLine($"  Base URL: {_apiConfig.BaseUrl}");
                Console.WriteLine($"  Model: {_apiConfig.Model}");
                Console.WriteLine($"  Max Tokens: {_apiConfig.MaxTokens}");
                Console.WriteLine($"  Temperature: {_apiConfig.Temperature}");
                Console.WriteLine();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
                return false;
            }
        }

        private static void InitializeChatAgent()
        {
            if (_apiConfig == null)
                throw new InvalidOperationException("Configuration not loaded");

            // Initialize OpenAI client
            var openAIClient = new OpenAIClient(_apiConfig);

            // Initialize tool manager
            var toolManager = new ToolManager();

            // Load configuration for web search
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            var json = File.ReadAllText(configPath);
            var configDocument = JsonDocument.Parse(json);
            
            string searchApiKey = "";
            string searchEngineId = "";
            
            if (configDocument.RootElement.TryGetProperty("WebSearch", out var webSearchElement))
            {
                if (webSearchElement.TryGetProperty("GoogleSearchApiKey", out var apiKeyElement))
                {
                    searchApiKey = apiKeyElement.GetString() ?? "";
                }
                if (webSearchElement.TryGetProperty("GoogleSearchEngineId", out var engineIdElement))
                {
                    searchEngineId = engineIdElement.GetString() ?? "";
                }
            }

            // Register tools
            toolManager.RegisterTool(new WebSearchTool(searchApiKey, searchEngineId));
            toolManager.RegisterTool(new CalculatorTool());

            // Initialize chat agent
            _chatAgent = new ChatAgent(openAIClient, toolManager, _apiConfig);

            Console.WriteLine("Available tools:");
            foreach (var toolName in toolManager.GetToolNames())
            {
                Console.WriteLine($"  - {toolName}");
            }
            Console.WriteLine();
        }

        private static void DisplayHelp()
        {
            Console.WriteLine("Commands:");
            Console.WriteLine("  /help     - Show this help message");
            Console.WriteLine("  /clear    - Clear conversation history");
            Console.WriteLine("  /history  - Show conversation history");
            Console.WriteLine("  /exit     - Exit the application");
            Console.WriteLine();
            Console.WriteLine("You can ask questions and the agent will use tools when needed.");
            Console.WriteLine("Example: 'What's the weather like today?' or 'Calculate 15 * 23 + 47'");
            Console.WriteLine();
        }

        private static async Task RunChatLoopAsync()
        {
            if (_chatAgent == null)
                throw new InvalidOperationException("Chat agent not initialized");

            bool isInputRedirected = Console.IsInputRedirected;

            while (true)
            {
                if (!isInputRedirected)
                {
                    Console.Write("You: ");
                }

                var input = Console.ReadLine();

                // Handle null input (end of piped stream or Ctrl+C)
                if (input == null)
                {
                    if (isInputRedirected)
                    {
                        // If input is piped and we reached the end, exit gracefully
                        break;
                    }
                    continue;
                }

                // For piped input, show what was received
                if (isInputRedirected)
                {
                    Console.WriteLine($"You: {input}");
                }

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                // Handle commands
                if (input.StartsWith("/"))
                {
                    if (await HandleCommandAsync(input.ToLower()))
                        break;
                    continue;
                }

                try
                {
                    Console.Write("Assistant: ");
                    var response = await _chatAgent.SendMessageAsync(input);
                    Console.WriteLine(response);
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine();
                }
            }
        }

        private static Task<bool> HandleCommandAsync(string command)
        {
            if (_chatAgent == null)
                return Task.FromResult(false);

            switch (command)
            {
                case "/help":
                    DisplayHelp();
                    return Task.FromResult(false);

                case "/clear":
                    _chatAgent.ClearHistory();
                    Console.WriteLine("Conversation history cleared.");
                    Console.WriteLine();
                    return Task.FromResult(false);

                case "/history":
                    var history = _chatAgent.GetConversationHistory();
                    Console.WriteLine("Conversation History:");
                    Console.WriteLine("====================");
                    foreach (var message in history)
                    {
                        if (message.Role == "system")
                            continue;
                            
                        Console.WriteLine($"{message.Role.ToUpper()}: {message.Content}");
                        
                        if (message.ToolCalls?.Count > 0)
                        {
                            foreach (var toolCall in message.ToolCalls)
                            {
                                Console.WriteLine($"  [Tool Call: {toolCall.Function.Name}]");
                            }
                        }
                    }
                    Console.WriteLine();
                    return Task.FromResult(false);

                case "/exit":
                case "/quit":
                    Console.WriteLine("Goodbye!");
                    return Task.FromResult(true);

                default:
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine();
                    return Task.FromResult(false);
            }
        }
    }
}
