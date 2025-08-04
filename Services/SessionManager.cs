using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LocalChatAgent.Models;

namespace LocalChatAgent.Services
{
    public class SessionManager
    {
        private readonly string _sessionsDirectory;
        private ChatSession? _currentSession;

        public SessionManager()
        {
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LocalChatAgent", "Sessions");
            
            _sessionsDirectory = appDataFolder;
            
            // Create sessions directory if it doesn't exist
            if (!Directory.Exists(_sessionsDirectory))
            {
                Directory.CreateDirectory(_sessionsDirectory);
            }
        }

        public ChatSession? CurrentSession => _currentSession;

        public async Task<ChatSession> CreateNewSessionAsync(string sessionName, CharacterCard? characterCard, string? characterFile = null)
        {
            var session = new ChatSession
            {
                Name = sessionName,
                CharacterName = characterCard?.Name ?? "Assistant",
                CharacterFile = characterFile ?? "",
                CreatedAt = DateTime.Now,
                LastModified = DateTime.Now
            };

            _currentSession = session;
            await SaveCurrentSessionAsync();
            return session;
        }

        public async Task<List<ChatSession>> GetAllSessionsAsync()
        {
            var sessions = new List<ChatSession>();
            
            if (!Directory.Exists(_sessionsDirectory))
                return sessions;

            var sessionFiles = Directory.GetFiles(_sessionsDirectory, "*.json");
            
            foreach (var file in sessionFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var session = JsonSerializer.Deserialize<ChatSession>(json);
                    if (session != null)
                    {
                        sessions.Add(session);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not load session from {file}: {ex.Message}");
                }
            }

            // Sort by last modified date (newest first)
            return sessions.OrderByDescending(s => s.LastModified).ToList();
        }

        public async Task<List<ChatSession>> GetSessionsForCharacterAsync(string characterName)
        {
            var allSessions = await GetAllSessionsAsync();
            return allSessions.Where(s => s.CharacterName.Equals(characterName, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public async Task<ChatSession?> GetLatestSessionForCharacterAsync(string characterName)
        {
            var characterSessions = await GetSessionsForCharacterAsync(characterName);
            return characterSessions.FirstOrDefault(); // Already sorted by LastModified desc
        }

        public async Task<ChatSession?> LoadSessionAsync(string sessionId)
        {
            var sessionFile = Path.Combine(_sessionsDirectory, $"{sessionId}.json");
            
            if (!File.Exists(sessionFile))
                return null;

            try
            {
                var json = await File.ReadAllTextAsync(sessionFile);
                var session = JsonSerializer.Deserialize<ChatSession>(json);
                _currentSession = session;
                return session;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading session {sessionId}: {ex.Message}");
                return null;
            }
        }

        public async Task SaveCurrentSessionAsync()
        {
            if (_currentSession == null)
                return;

            _currentSession.LastModified = DateTime.Now;

            var sessionFile = Path.Combine(_sessionsDirectory, $"{_currentSession.Id}.json");
            
            try
            {
                var json = JsonSerializer.Serialize(_currentSession, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                await File.WriteAllTextAsync(sessionFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving session: {ex.Message}");
            }
        }

        public async void AddMessageToCurrentSession(string role, string content)
        {
            if (_currentSession == null)
                return;

            // Only save user and assistant messages
            if (role == "user" || role == "assistant")
            {
                _currentSession.Messages.Add(new SessionMessage
                {
                    Role = role,
                    Content = content,
                    Timestamp = DateTime.Now
                });
                
                // Automatically save the session after adding a message
                await SaveCurrentSessionAsync();
            }
        }

        public Task<bool> DeleteSessionAsync(string sessionId)
        {
            var sessionFile = Path.Combine(_sessionsDirectory, $"{sessionId}.json");
            
            if (!File.Exists(sessionFile))
                return Task.FromResult(false);

            try
            {
                File.Delete(sessionFile);
                
                // If this was the current session, clear it
                if (_currentSession?.Id == sessionId)
                {
                    _currentSession = null;
                }
                
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting session {sessionId}: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public List<SessionMessage> GetCurrentSessionMessages()
        {
            return _currentSession?.Messages ?? new List<SessionMessage>();
        }

        public string GetSessionsDirectory()
        {
            return _sessionsDirectory;
        }
    }
}
