using System;
using System.Text.Json;
using System.Threading.Tasks;
using LocalChatAgent.Models;
using LocalChatAgent.Tools;

namespace LocalChatAgent.Tools
{
    public class CalculatorTool : IToolHandler
    {
        public string Name => "calculator";
        public string Description => "Perform basic mathematical calculations";

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
                            expression = new
                            {
                                type = "string",
                                description = "Mathematical expression to evaluate (e.g., '2 + 3 * 4')"
                            }
                        },
                        required = new[] { "expression" }
                    }
                }
            };
        }

        public Task<string> ExecuteAsync(JsonElement parameters)
        {
            try
            {
                string expression = parameters.GetProperty("expression").GetString() ?? "";
                
                if (string.IsNullOrEmpty(expression))
                {
                    return Task.FromResult("Error: Expression cannot be empty");
                }

                // Simple calculator implementation
                var result = EvaluateExpression(expression);
                return Task.FromResult($"Result: {expression} = {result}");
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Error calculating expression: {ex.Message}");
            }
        }

        private double EvaluateExpression(string expression)
        {
            // This is a simple implementation. For production use, consider using
            // a proper expression parser like NCalc or System.Data.DataTable.Compute
            try
            {
                var table = new System.Data.DataTable();
                var result = table.Compute(expression, null);
                return Convert.ToDouble(result);
            }
            catch
            {
                throw new ArgumentException($"Invalid mathematical expression: {expression}");
            }
        }
    }
}
