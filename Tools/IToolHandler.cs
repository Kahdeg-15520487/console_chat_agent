using System.Text.Json;
using System.Threading.Tasks;
using LocalChatAgent.Models;

namespace LocalChatAgent.Tools
{
    public interface IToolHandler
    {
        string Name { get; }
        string Description { get; }
        Task<string> ExecuteAsync(JsonElement parameters);
        Tool GetToolDefinition();
    }
}
