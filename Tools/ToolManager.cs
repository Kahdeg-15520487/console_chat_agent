using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LocalChatAgent.Models;

namespace LocalChatAgent.Tools
{
    public class ToolManager
    {
        private readonly Dictionary<string, IToolHandler> _tools;

        public ToolManager()
        {
            _tools = new Dictionary<string, IToolHandler>();
        }

        public void RegisterTool(IToolHandler tool)
        {
            _tools[tool.Name] = tool;
        }

        public List<Tool> GetAvailableTools()
        {
            return _tools.Values.Select(t => t.GetToolDefinition()).ToList();
        }

        public async Task<string> ExecuteToolAsync(string toolName, JsonElement parameters)
        {
            if (!_tools.TryGetValue(toolName, out var tool))
            {
                return $"Error: Tool '{toolName}' not found";
            }

            try
            {
                return await tool.ExecuteAsync(parameters);
            }
            catch (Exception ex)
            {
                return $"Error executing tool '{toolName}': {ex.Message}";
            }
        }

        public bool HasTool(string toolName)
        {
            return _tools.ContainsKey(toolName);
        }

        public IEnumerable<string> GetToolNames()
        {
            return _tools.Keys;
        }
    }
}
