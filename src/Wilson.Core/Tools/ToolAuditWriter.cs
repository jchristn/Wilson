namespace Wilson.Core.Tools
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Text.Json;
    using Wilson.Core.Helpers;
    using Wilson.Core.Models;
    using Wilson.Core.Settings;

    /// <summary>
    /// Builds redacted persistent tool audit records from internal audit traces.
    /// </summary>
    public static class ToolAuditWriter
    {
        /// <summary>
        /// Build tool execution records for persistence.
        /// </summary>
        /// <param name="run">Tool run metadata.</param>
        /// <param name="auditTraces">Internal audit traces containing raw tool payloads.</param>
        /// <param name="safeTraces">Safe public traces used for user-facing summaries.</param>
        /// <param name="approvalPolicy">Effective approval policy.</param>
        /// <param name="assistantMessageId">Assistant message identifier when available.</param>
        /// <param name="tools">Effective tool settings.</param>
        /// <returns>Redacted tool execution records.</returns>
        public static List<ToolExecutionRecord> BuildExecutionRecords(ToolRun run, List<ToolAuditTrace> auditTraces, List<ToolTrace> safeTraces, string approvalPolicy, string? assistantMessageId, ToolsSettings tools)
        {
            ArgumentNullException.ThrowIfNull(run);
            ArgumentNullException.ThrowIfNull(auditTraces);
            ArgumentNullException.ThrowIfNull(safeTraces);
            ArgumentNullException.ThrowIfNull(tools);

            List<ToolExecutionRecord> records = new List<ToolExecutionRecord>();
            int maxPayloadCharacters = Math.Clamp(tools.MaxToolResultBytes, 1024, 200000);
            for (int i = 0; i < auditTraces.Count; i++)
            {
                ToolAuditTrace trace = auditTraces[i];
                ToolTrace? safeTrace = i < safeTraces.Count ? safeTraces[i] : null;
                string internalToolCallId = IdGenerator.ToolCall();
                string summary = safeTrace == null ? SafeToolSummary(trace) : SafeToolSummary(safeTrace);
                string summaryJson = JsonSerializer.Serialize(new Dictionary<string, object?>
                {
                    { "toolName", trace.ToolName },
                    { "displayLabel", trace.DisplayLabel },
                    { "success", trace.Success },
                    { "denied", trace.Denied },
                    { "truncated", trace.Truncated },
                    { "outputCharacters", trace.OutputCharacters },
                    { "resultCount", trace.ResultCount },
                    { "elapsedMs", trace.ElapsedMs },
                    { "summary", summary }
                });
                summaryJson = ToolAuditSanitizer.RedactAndCapJson(summaryJson, maxPayloadCharacters);
                string argumentsJson = tools.StoreToolArguments ? ToolAuditSanitizer.RedactAndCapJson(trace.ArgumentsJson, maxPayloadCharacters) : "{}";
                string resultJson = tools.StoreFullToolResults ? ToolAuditSanitizer.RedactAndCapJson(trace.ResultJson, maxPayloadCharacters) : summaryJson;
                string errorMessage = ToolAuditSanitizer.RedactAndCapText(trace.ErrorMessage ?? summary, 1000);

                records.Add(new ToolExecutionRecord
                {
                    TenantId = run.TenantId,
                    UserId = run.UserId,
                    ConversationId = run.ConversationId,
                    RunId = run.RunId,
                    TraceId = run.RunId + ":" + trace.SequenceNumber.ToString(),
                    Origin = "chat",
                    AssistantMessageId = assistantMessageId,
                    ToolCallId = internalToolCallId,
                    ToolName = trace.ToolName,
                    Iteration = trace.Iteration,
                    SequenceNumber = trace.SequenceNumber,
                    Status = trace.Success ? ToolStatuses.Completed : trace.Denied ? ToolStatuses.Denied : ToolStatuses.Failed,
                    ApprovalPolicy = String.IsNullOrWhiteSpace(approvalPolicy) ? ToolApprovalPolicies.Ask : approvalPolicy,
                    ArgumentsJson = argumentsJson,
                    ResultJson = resultJson,
                    ResultSummaryJson = summaryJson,
                    ResultPreview = ToolAuditSanitizer.RedactAndCapText(summary, 4000),
                    Success = trace.Success,
                    Denied = trace.Denied,
                    Truncated = trace.Truncated,
                    OutputCharacters = trace.OutputCharacters,
                    InputBytes = Encoding.UTF8.GetByteCount(argumentsJson),
                    OutputBytes = Encoding.UTF8.GetByteCount(resultJson),
                    ErrorType = trace.Success ? null : "tool_execution",
                    ErrorCode = trace.Success ? null : trace.ErrorCode ?? "tool_failed",
                    ErrorMessage = trace.Success ? null : errorMessage,
                    Model = run.Model,
                    StartedUtc = trace.StartedUtc ?? run.StartedUtc,
                    CompletedUtc = trace.CompletedUtc,
                    ElapsedMs = trace.ElapsedMs,
                    Active = true,
                    CreatedUtc = trace.StartedUtc ?? run.CreatedUtc,
                    UpdatedUtc = trace.CompletedUtc ?? DateTime.UtcNow
                });
            }

            return records;
        }

        private static string SafeToolSummary(ToolTrace trace)
        {
            string summary = String.IsNullOrWhiteSpace(trace.Summary) ? (trace.Success ? "Completed." : "Tool call failed.") : trace.Summary!;
            return ToolAuditSanitizer.RedactAndCapText(summary, 4000);
        }

        private static string SafeToolSummary(ToolAuditTrace trace)
        {
            string summary = trace.Success ? "Completed." : trace.ErrorMessage ?? "Tool call failed.";
            return ToolAuditSanitizer.RedactAndCapText(summary, 4000);
        }
    }
}
