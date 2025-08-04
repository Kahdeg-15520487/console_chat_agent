using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
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
        private static bool _useStreaming = false;
        private static bool _conversationOnlyMode = false;
        private static CancellationTokenSource? _currentRequestCancellation;
        private static List<string> _commandHistory = new List<string>();
        private static readonly string _historyFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "LocalChatAgent", 
            "command_history.json");

        private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            if (_currentRequestCancellation != null && !_currentRequestCancellation.Token.IsCancellationRequested)
            {
                // Cancel the current request
                _currentRequestCancellation.Cancel();
                e.Cancel = true; // Prevent the application from terminating
            }
            else
            {
                // No active request, allow normal termination
                e.Cancel = false;
            }
        }

        private static CharacterCard? LoadCharacterCard()
        {
            try
            {
                // Look for character card files in the application directory
                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var characterDirectory = Path.Combine(appDirectory, "characters");
                
                // Create characters directory if it doesn't exist
                if (!Directory.Exists(characterDirectory))
                {
                    Directory.CreateDirectory(characterDirectory);
                    Console.WriteLine($"Created characters directory: {characterDirectory}");
                    Console.WriteLine("You can place character card PNG or JSON files in this directory.");
                    Console.WriteLine();
                    return null;
                }

                // Look for character card files
                var files = Directory.GetFiles(characterDirectory)
                    .Where(f => CharacterCardLoader.IsSupportedFile(f))
                    .ToArray();

                if (files.Length == 0)
                {
                    Console.WriteLine("No character card files found in the characters directory.");
                    Console.WriteLine("You can place character card PNG or JSON files there to load a character.");
                    Console.WriteLine();
                    return null;
                }

                if (files.Length == 1)
                {
                    // Auto-load single character card
                    var file = files[0];
                    Console.WriteLine($"Loading character card: {Path.GetFileName(file)}");
                    
                    var task = Path.GetExtension(file).ToLowerInvariant() == ".png" 
                        ? CharacterCardLoader.LoadFromPngAsync(file) 
                        : CharacterCardLoader.LoadFromJsonAsync(file);
                    
                    var result = task.GetAwaiter().GetResult();
                    Console.WriteLine();
                    return result;
                }

                // Multiple files - let user choose
                Console.WriteLine("Multiple character cards found:");
                for (int i = 0; i < files.Length; i++)
                {
                    Console.WriteLine($"  {i + 1}. {Path.GetFileName(files[i])}");
                }
                Console.WriteLine($"  {files.Length + 1}. Continue without character card");
                Console.WriteLine();

                int choice = -1;
                while (choice < 1 || choice > files.Length + 1)
                {
                    Console.Write("Select a character card (enter number): ");
                    var input = Console.ReadLine();
                    if (!int.TryParse(input, out choice) || choice < 1 || choice > files.Length + 1)
                    {
                        Console.WriteLine("Invalid selection. Please enter a valid number.");
                    }
                }

                if (choice == files.Length + 1)
                {
                    Console.WriteLine("Continuing without character card.");
                    Console.WriteLine();
                    return null;
                }

                var selectedFile = files[choice - 1];
                Console.WriteLine($"Loading character card: {Path.GetFileName(selectedFile)}");
                
                var loadTask = Path.GetExtension(selectedFile).ToLowerInvariant() == ".png" 
                    ? CharacterCardLoader.LoadFromPngAsync(selectedFile) 
                    : CharacterCardLoader.LoadFromJsonAsync(selectedFile);
                
                var characterCard = loadTask.GetAwaiter().GetResult();
                Console.WriteLine();
                return characterCard;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during character card loading: {ex.Message}");
                Console.WriteLine();
                return null;
            }
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine("Local Chat Agent - OpenAI Compatible");
            Console.WriteLine("=====================================");
            Console.WriteLine();

            // Set up console cancel key handler
            Console.CancelKeyPress += OnCancelKeyPress;

            try
            {
                // Load command history
                LoadCommandHistory();

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
            finally
            {
                // Save command history on exit
                SaveCommandHistory();
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
            toolManager.RegisterTool(new WebSearchTool());
            toolManager.RegisterTool(new WebFetchTool());
            toolManager.RegisterTool(new CalculatorTool());

            // Load character card if available
            var characterCard = LoadCharacterCard();

            // Initialize chat agent with character card
            _chatAgent = new ChatAgent(openAIClient, toolManager, _apiConfig, characterCard);
            
            // Set initial conversation-only mode state
            _chatAgent.SetConversationOnlyMode(_conversationOnlyMode);

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
            Console.WriteLine("  /help      - Show this help message");
            Console.WriteLine("  /clear     - Clear conversation history");
            Console.WriteLine("  /clearall  - Clear both conversation and command history");
            Console.WriteLine("  /history   - Show conversation history with token counts");
            Console.WriteLine("  /commands  - Show recent command history");
            Console.WriteLine("  /stream    - Toggle streaming mode (current: " + (_useStreaming ? "ON" : "OFF") + ")");
            Console.WriteLine("  /conversation - Toggle conversation-only mode (current: " + (_conversationOnlyMode ? "ON" : "OFF") + ")");
            Console.WriteLine("  /character - Show current character card info");
            Console.WriteLine("  /prompt    - Show current system prompt with token count");
            Console.WriteLine("  /exit      - Exit the application");
            Console.WriteLine();
            Console.WriteLine("Character Cards:");
            Console.WriteLine("  Place character card PNG or JSON files in the 'characters' directory");
            Console.WriteLine("  Character cards define AI personality and behavior");
            Console.WriteLine("  Supported formats: PNG with embedded metadata, JSON files");
            Console.WriteLine();
            Console.WriteLine("Conversation Mode:");
            Console.WriteLine("  Normal mode: LLM sees system prompts, tool calls, and full context");
            Console.WriteLine("  Conversation-only mode: LLM sees only user/assistant messages (no tools, no system prompts)");
            Console.WriteLine("  Use /conversation to toggle between modes");
            Console.WriteLine();
            Console.WriteLine("Hotkeys:");
            Console.WriteLine("  Ctrl+C     - Cancel current chat request");
            Console.WriteLine("  Up Arrow   - Navigate to previous command/input");
            Console.WriteLine("  Down Arrow - Navigate to next command/input");
            Console.WriteLine();
            Console.WriteLine("You can ask questions and the agent will use tools when needed.");
            Console.WriteLine("Example: 'What's the weather like today?' or 'Calculate 15 * 23 + 47'");
            Console.WriteLine();
        }

        private static string? ReadLineWithHistory(string prompt)
        {
            Console.Write(prompt);
            
            var input = new StringBuilder();
            var currentHistoryIndex = -1; // -1 means we're not navigating history
            string? currentInput = null; // Store the current input when we start navigating history
            
            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                
                switch (keyInfo.Key)
                {
                    case ConsoleKey.Enter:
                        Console.WriteLine();
                        var result = input.ToString();
                        
                        // Add non-empty inputs to history
                        AddToHistory(result);
                        
                        return result;
                        
                    case ConsoleKey.UpArrow:
                        if (_commandHistory.Count > 0)
                        {
                            // If we're not navigating history yet, store the current input
                            if (currentHistoryIndex == -1)
                            {
                                currentInput = input.ToString();
                                currentHistoryIndex = _commandHistory.Count - 1;
                            }
                            else if (currentHistoryIndex > 0)
                            {
                                currentHistoryIndex--;
                            }
                            
                            // Clear current line and show history item
                            ClearCurrentLine();
                            Console.Write(prompt);
                            input.Clear();
                            input.Append(_commandHistory[currentHistoryIndex]);
                            Console.Write(input.ToString());
                        }
                        break;
                        
                    case ConsoleKey.DownArrow:
                        if (currentHistoryIndex != -1)
                        {
                            if (currentHistoryIndex < _commandHistory.Count - 1)
                            {
                                currentHistoryIndex++;
                                
                                // Clear current line and show history item
                                ClearCurrentLine();
                                Console.Write(prompt);
                                input.Clear();
                                input.Append(_commandHistory[currentHistoryIndex]);
                                Console.Write(input.ToString());
                            }
                            else
                            {
                                // Back to current input
                                currentHistoryIndex = -1;
                                ClearCurrentLine();
                                Console.Write(prompt);
                                input.Clear();
                                if (currentInput != null)
                                {
                                    input.Append(currentInput);
                                    Console.Write(input.ToString());
                                }
                            }
                        }
                        break;
                        
                    case ConsoleKey.Backspace:
                        if (input.Length > 0)
                        {
                            input.Remove(input.Length - 1, 1);
                            Console.Write("\b \b");
                            
                            // Reset history navigation when user starts editing
                            currentHistoryIndex = -1;
                            currentInput = null;
                        }
                        break;
                        
                    case ConsoleKey.Escape:
                        // Clear the current line
                        ClearCurrentLine();
                        Console.Write(prompt);
                        input.Clear();
                        currentHistoryIndex = -1;
                        currentInput = null;
                        break;
                        
                    default:
                        // Handle regular character input
                        if (!char.IsControl(keyInfo.KeyChar))
                        {
                            input.Append(keyInfo.KeyChar);
                            Console.Write(keyInfo.KeyChar);
                            
                            // Reset history navigation when user starts editing
                            currentHistoryIndex = -1;
                            currentInput = null;
                        }
                        break;
                }
            }
        }
        
        private static void ClearCurrentLine()
        {
            int currentLineCursor = Console.CursorTop;
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, currentLineCursor);
        }

        private static void LoadCommandHistory()
        {
            try
            {
                if (File.Exists(_historyFilePath))
                {
                    var json = File.ReadAllText(_historyFilePath);
                    var history = JsonSerializer.Deserialize<List<string>>(json);
                    if (history != null)
                    {
                        _commandHistory = history;
                        Console.WriteLine($"Loaded {_commandHistory.Count} commands from history.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load command history: {ex.Message}");
            }
        }

        private static void SaveCommandHistory()
        {
            try
            {
                // Create directory if it doesn't exist
                var directory = Path.GetDirectoryName(_historyFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(_commandHistory, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_historyFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not save command history: {ex.Message}");
            }
        }

        private static void AddToHistory(string input)
        {
            if (!string.IsNullOrWhiteSpace(input))
            {
                // Remove duplicate if it's the same as the last entry
                if (_commandHistory.Count == 0 || _commandHistory[_commandHistory.Count - 1] != input)
                {
                    _commandHistory.Add(input);
                    
                    // Keep history size manageable (last 500 commands for persistent storage)
                    if (_commandHistory.Count > 500)
                    {
                        _commandHistory.RemoveAt(0);
                    }
                    
                    // Save to file after each addition
                    SaveCommandHistory();
                }
            }
        }

        private static async Task RunChatLoopAsync()
        {
            if (_chatAgent == null)
                throw new InvalidOperationException("Chat agent not initialized");

            bool isInputRedirected = Console.IsInputRedirected;

            while (true)
            {
                string? input;
                
                if (isInputRedirected)
                {
                    // For piped input, use regular ReadLine
                    Console.Write("You: ");
                    input = Console.ReadLine();
                }
                else
                {
                    // For interactive input, use our custom reader with history
                    input = ReadLineWithHistory("You: ");
                }

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
                    // Create a new cancellation token for this request
                    _currentRequestCancellation = new CancellationTokenSource();
                    
                    Console.Write("Assistant: ");
                    
                    if (_useStreaming)
                    {
                        // Use streaming response
                        await foreach (var chunk in _chatAgent.SendMessageStreamAsync(input, _currentRequestCancellation.Token))
                        {
                            Console.Write(chunk);
                        }
                        Console.WriteLine();
                        Console.WriteLine();
                    }
                    else
                    {
                        // Use regular response (we'll need to update ChatAgent to support cancellation for non-streaming too)
                        var response = await _chatAgent.SendMessageAsync(input, _currentRequestCancellation.Token);
                        Console.WriteLine(response);
                        Console.WriteLine();
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine();
                    Console.WriteLine("Request cancelled by user.");
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.WriteLine();
                }
                finally
                {
                    // Clean up the cancellation token
                    _currentRequestCancellation?.Dispose();
                    _currentRequestCancellation = null;
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

                case "/clearall":
                    _chatAgent.ClearHistory();
                    _commandHistory.Clear();
                    SaveCommandHistory(); // Save the cleared history to file
                    Console.WriteLine("Both conversation and command history cleared.");
                    Console.WriteLine();
                    return Task.FromResult(false);

                case "/history":
                    var history = _chatAgent.GetConversationHistory();
                    var totalTokens = _chatAgent?.GetConversationTokenCount() ?? 0;
                    Console.WriteLine("Conversation History:");
                    Console.WriteLine("====================");
                    Console.WriteLine($"Total estimated tokens: {totalTokens}");
                    Console.WriteLine($"Max tokens configured: {_apiConfig?.MaxTokens ?? 0}");
                    Console.WriteLine();
                    
                    foreach (var message in history)
                    {
                        if (message.Role == "system")
                            continue;
                        
                        var messageTokens = _chatAgent?.EstimateTokenCount(message.Content ?? "") ?? 0;
                        Console.WriteLine($"{message.Role.ToUpper()}: {message.Content}");
                        Console.WriteLine($"  [Tokens: {messageTokens}]");
                        
                        if (message.ToolCalls?.Count > 0)
                        {
                            foreach (var toolCall in message.ToolCalls)
                            {
                                var toolTokens = _chatAgent?.EstimateTokenCount(toolCall.Function.Name + toolCall.Function.Arguments) ?? 0;
                                Console.WriteLine($"  [Tool Call: {toolCall.Function.Name}, Tokens: {toolTokens}]");
                            }
                        }
                    }
                    Console.WriteLine();
                    return Task.FromResult(false);

                case "/commands":
                    Console.WriteLine("Command History:");
                    Console.WriteLine("================");
                    Console.WriteLine($"History file: {_historyFilePath}");
                    Console.WriteLine($"Total commands: {_commandHistory.Count}");
                    Console.WriteLine();
                    
                    if (_commandHistory.Count > 0)
                    {
                        Console.WriteLine("Recent commands (last 20):");
                        var recentCommands = _commandHistory.Skip(Math.Max(0, _commandHistory.Count - 20)).ToArray();
                        for (int i = 0; i < recentCommands.Length; i++)
                        {
                            Console.WriteLine($"  {_commandHistory.Count - recentCommands.Length + i + 1}: {recentCommands[i]}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No commands in history.");
                    }
                    Console.WriteLine();
                    return Task.FromResult(false);

                case "/stream":
                    _useStreaming = !_useStreaming;
                    Console.WriteLine($"Streaming mode is now {(_useStreaming ? "ON" : "OFF")}");
                    if (_useStreaming)
                    {
                        Console.WriteLine("Note: Tool calls are not supported in streaming mode.");
                    }
                    Console.WriteLine();
                    return Task.FromResult(false);

                case "/conversation":
                    _conversationOnlyMode = !_conversationOnlyMode;
                    _chatAgent?.SetConversationOnlyMode(_conversationOnlyMode);
                    Console.WriteLine($"Conversation-only mode is now {(_conversationOnlyMode ? "ON" : "OFF")}");
                    if (_conversationOnlyMode)
                    {
                        Console.WriteLine("LLM will only see user and assistant messages (no system prompts, no tool calls).");
                        Console.WriteLine("Tools are disabled in this mode.");
                    }
                    else
                    {
                        Console.WriteLine("LLM will see full conversation context including system prompts and tool calls.");
                    }
                    Console.WriteLine();
                    return Task.FromResult(false);

                case "/character":
                    var characterCard = _chatAgent?.GetCharacterCard();
                    if (characterCard != null)
                    {
                        Console.WriteLine("Current Character Card:");
                        Console.WriteLine("======================");
                        Console.WriteLine($"Name: {characterCard.Name}");
                        if (!string.IsNullOrEmpty(characterCard.Creator))
                            Console.WriteLine($"Creator: {characterCard.Creator}");
                        if (!string.IsNullOrEmpty(characterCard.Description))
                            Console.WriteLine($"Description: {characterCard.Description}");
                        if (!string.IsNullOrEmpty(characterCard.Personality))
                            Console.WriteLine($"Personality: {characterCard.Personality}");
                        if (!string.IsNullOrEmpty(characterCard.Scenario))
                            Console.WriteLine($"Scenario: {characterCard.Scenario}");
                        if (characterCard.Tags.Any())
                            Console.WriteLine($"Tags: {string.Join(", ", characterCard.Tags)}");
                        if (!string.IsNullOrEmpty(characterCard.CharacterVersion))
                            Console.WriteLine($"Version: {characterCard.CharacterVersion}");
                    }
                    else
                    {
                        Console.WriteLine("No character card is currently loaded.");
                        Console.WriteLine("Place character card PNG or JSON files in the 'characters' directory and restart the application.");
                    }
                    Console.WriteLine();
                    return Task.FromResult(false);

                case "/prompt":
                    var systemPrompt = _chatAgent?.GetCurrentSystemPrompt();
                    var systemPromptTokens = _chatAgent?.GetSystemPromptTokenCount() ?? 0;
                    Console.WriteLine("Current System Prompt:");
                    Console.WriteLine("=====================");
                    if (!string.IsNullOrEmpty(systemPrompt))
                    {
                        Console.WriteLine(systemPrompt);
                        Console.WriteLine();
                        Console.WriteLine($"Estimated tokens: {systemPromptTokens}");
                    }
                    else
                    {
                        Console.WriteLine("No system prompt available.");
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
