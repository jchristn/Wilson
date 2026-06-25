namespace Wilson.Core.Tools
{
    using System;
    using System.Text;
    using System.Text.Json;
    using Wilson.Core.Models;

    internal static class ToolResultFactory
    {
        private static readonly JsonSerializerOptions _Json = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        public static ToolResult SuccessText(string toolCallId, string content, ToolExecutionContext context)
        {
            bool truncated = false;
            string output = content ?? String.Empty;
            int maxChars = context.SafetyLimits.MaxToolOutputChars;
            if (output.Length > maxChars)
            {
                output = output.Substring(0, maxChars) + Environment.NewLine + "[truncated]";
                truncated = true;
            }

            return new ToolResult
            {
                ToolCallId = toolCallId,
                Success = true,
                Content = output,
                ContentJson = JsonSerializer.Serialize(new { content = output, truncated }, _Json),
                Truncated = truncated,
                OutputBytes = Encoding.UTF8.GetByteCount(output)
            };
        }

        public static ToolResult SuccessJson(string toolCallId, object payload, ToolExecutionContext context)
        {
            string json = JsonSerializer.Serialize(payload, _Json);
            bool truncated = false;
            string output = json;
            int maxChars = context.SafetyLimits.MaxToolOutputChars;
            if (output.Length > maxChars)
            {
                output = JsonSerializer.Serialize(new
                {
                    truncated = true,
                    originalCharacters = json.Length,
                    content = json.Substring(0, maxChars)
                }, _Json);
                truncated = true;
            }

            return new ToolResult
            {
                ToolCallId = toolCallId,
                Success = true,
                Content = output,
                ContentJson = output,
                Truncated = truncated,
                OutputBytes = Encoding.UTF8.GetByteCount(output)
            };
        }

        public static ToolResult Error(string toolCallId, string code, string message)
        {
            string json = JsonSerializer.Serialize(new { success = false, error = code, message }, _Json);
            return new ToolResult
            {
                ToolCallId = toolCallId,
                Success = false,
                Content = json,
                ContentJson = json,
                ErrorCode = code,
                ErrorMessage = message,
                OutputBytes = Encoding.UTF8.GetByteCount(json)
            };
        }

        public static ToolResult Error(string toolCallId, ToolExecutionException ex)
        {
            return Error(toolCallId, ex.Code, ex.Message);
        }
    }
}
