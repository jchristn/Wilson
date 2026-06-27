namespace Wilson.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Wilson.Core.Helpers;

    /// <summary>
    /// Pagination query for enumerating records.
    /// </summary>
    public class EnumerationQuery
    {
        /// <summary>One-based page number.</summary>
        public int PageNumber { get; set; } = 1;
        /// <summary>Page size.</summary>
        public int PageSize { get; set; } = 25;
        /// <summary>Optional search term.</summary>
        public string? Search { get; set; } = null;
        /// <summary>Optional tenant scope.</summary>
        public string? TenantId { get; set; } = null;
    }

    /// <summary>
    /// Paginated enumeration result.
    /// </summary>
    public class EnumerationResult<T>
    {
        /// <summary>Objects in the requested page.</summary>
        public List<T> Objects { get; set; } = new List<T>();
        /// <summary>One-based page number.</summary>
        public int PageNumber { get; set; } = 1;
        /// <summary>Page size.</summary>
        public int PageSize { get; set; } = 25;
        /// <summary>Total matching records.</summary>
        public int TotalRecords { get; set; } = 0;
        /// <summary>Total pages.</summary>
        public int TotalPages { get; set; } = 1;
    }

    /// <summary>
    /// Tenant record.
    /// </summary>
    public class Tenant
    {
        /// <summary>Tenant identifier.</summary>
        public string Id { get; set; } = IdGenerator.Tenant();
        /// <summary>Name.</summary>
        public string Name { get; set; } = String.Empty;
        /// <summary>Active flag.</summary>
        public bool Active { get; set; } = true;
        /// <summary>Protected flag.</summary>
        public bool IsProtected { get; set; } = false;
        /// <summary>Created UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        /// <summary>Last update UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// User record.
    /// </summary>
    public class User
    {
        /// <summary>User identifier.</summary>
        public string Id { get; set; } = IdGenerator.User();
        /// <summary>Tenant identifier.</summary>
        public string TenantId { get; set; } = String.Empty;
        /// <summary>First name.</summary>
        public string FirstName { get; set; } = String.Empty;
        /// <summary>Last name.</summary>
        public string LastName { get; set; } = String.Empty;
        /// <summary>Email.</summary>
        public string Email { get; set; } = String.Empty;
        /// <summary>Global administrator flag.</summary>
        public bool IsAdmin { get; set; } = false;
        /// <summary>Tenant administrator flag.</summary>
        public bool IsTenantAdmin { get; set; } = false;
        /// <summary>Active flag.</summary>
        public bool Active { get; set; } = true;
        /// <summary>Protected flag.</summary>
        public bool IsProtected { get; set; } = false;
        /// <summary>Created UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        /// <summary>Last update UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Credential record.
    /// </summary>
    public class Credential
    {
        /// <summary>Credential identifier.</summary>
        public string Id { get; set; } = IdGenerator.Credential();
        /// <summary>Tenant identifier.</summary>
        public string TenantId { get; set; } = String.Empty;
        /// <summary>User identifier.</summary>
        public string UserId { get; set; } = String.Empty;
        /// <summary>Name.</summary>
        public string Name { get; set; } = String.Empty;
        /// <summary>Bearer access key.</summary>
        public string AccessKey { get; set; } = IdGenerator.Token();
        /// <summary>Last four characters.</summary>
        public string SecretLast4 { get; set; } = String.Empty;
        /// <summary>Active flag.</summary>
        public bool Active { get; set; } = true;
        /// <summary>Protected flag.</summary>
        public bool IsProtected { get; set; } = false;
        /// <summary>Created UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        /// <summary>Last update UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
        /// <summary>Last used UTC.</summary>
        public DateTime? LastUsedUtc { get; set; } = null;
    }

    /// <summary>
    /// Authenticated request context.
    /// </summary>
    public class RequestContext
    {
        /// <summary>Whether the request is authenticated.</summary>
        public bool IsAuthenticated { get; set; } = false;
        /// <summary>Tenant identifier.</summary>
        public string? TenantId { get; set; } = null;
        /// <summary>User identifier.</summary>
        public string? UserId { get; set; } = null;
        /// <summary>Global administrator flag.</summary>
        public bool IsAdmin { get; set; } = false;
        /// <summary>Tenant administrator flag.</summary>
        public bool IsTenantAdmin { get; set; } = false;
        /// <summary>Principal display name.</summary>
        public string PrincipalName { get; set; } = String.Empty;
    }

    /// <summary>
    /// Prompt template kind.
    /// </summary>
    public enum PromptTemplateKind
    {
        /// <summary>System prompt applied to chat requests.</summary>
        System,
        /// <summary>Tool prompt applied when tool calls are enabled.</summary>
        Tool
    }

    /// <summary>
    /// Built-in prompt template defaults.
    /// </summary>
    public static class PromptTemplateDefaults
    {
        /// <summary>Default system prompt template name.</summary>
        public const string DefaultSystemPromptName = "Default system prompt";
        /// <summary>Default tool prompt template name.</summary>
        public const string DefaultToolPromptName = "Default tool prompt";
        /// <summary>Default system prompt template content.</summary>
        public const string DefaultSystemPromptContent = "Use prior turns only as context. Respond to the latest user message directly and accurately. Do not replay or quote earlier assistant responses unless the user asks. Be clear about uncertainty, ask concise clarifying questions only when necessary, and keep the answer focused on the user's requested outcome.";
        /// <summary>Default tool prompt template content.</summary>
        public const string DefaultToolPromptContent = "You can use Wilson tools when they help answer the user's request. The available tools, their arguments, and their execution rules are listed below.\n\n{{tool_catalog}}\n\nUse tools only when they materially improve correctness, freshness, inspection, calculation, or action. Before calling a tool, choose the smallest safe action that satisfies the request. Respect approval requirements. If a tool is unavailable, denied, fails, or returns incomplete information, explain the limitation and continue with the best available answer. After tool use, summarize results in plain language and do not expose raw internal payloads unless the user asks for them.";
    }

    /// <summary>
    /// Tenant-scoped prompt template.
    /// </summary>
    public class PromptTemplate
    {
        /// <summary>Prompt template identifier.</summary>
        public string Id { get; set; } = IdGenerator.PromptTemplate();
        /// <summary>Tenant identifier.</summary>
        public string TenantId { get; set; } = String.Empty;
        /// <summary>Prompt template kind.</summary>
        public PromptTemplateKind Kind { get; set; } = PromptTemplateKind.System;
        /// <summary>Human-readable prompt template name.</summary>
        public string Name { get; set; } = String.Empty;
        /// <summary>Optional prompt template description.</summary>
        public string Description { get; set; } = String.Empty;
        /// <summary>Prompt template content.</summary>
        public string Content { get; set; } = String.Empty;
        /// <summary>Whether this prompt is the default for its tenant and kind.</summary>
        public bool IsDefault { get; set; } = false;
        /// <summary>Whether this prompt is protected from deletion.</summary>
        public bool IsProtected { get; set; } = false;
        /// <summary>Whether this prompt can be selected for chat.</summary>
        public bool Active { get; set; } = true;
        /// <summary>User who created this prompt template.</summary>
        public string CreatedByUserId { get; set; } = String.Empty;
        /// <summary>User who last updated this prompt template.</summary>
        public string UpdatedByUserId { get; set; } = String.Empty;
        /// <summary>Created UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        /// <summary>Last update UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Conversation record.
    /// </summary>
    public class Conversation
    {
        /// <summary>Conversation identifier.</summary>
        public string Id { get; set; } = IdGenerator.Conversation();
        /// <summary>Tenant identifier.</summary>
        public string TenantId { get; set; } = String.Empty;
        /// <summary>User identifier.</summary>
        public string UserId { get; set; } = String.Empty;
        /// <summary>Title.</summary>
        public string Title { get; set; } = "New Conversation";
        /// <summary>Runner identifier.</summary>
        public string RunnerId { get; set; } = String.Empty;
        /// <summary>Model.</summary>
        public string Model { get; set; } = String.Empty;
        /// <summary>Active flag.</summary>
        public bool Active { get; set; } = true;
        /// <summary>Created UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
        /// <summary>Last update UTC.</summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Chat message record.
    /// </summary>
    public class ChatMessage
    {
        /// <summary>Message identifier.</summary>
        public string Id { get; set; } = IdGenerator.Message();
        /// <summary>Tenant identifier.</summary>
        public string TenantId { get; set; } = String.Empty;
        /// <summary>Conversation identifier.</summary>
        public string ConversationId { get; set; } = String.Empty;
        /// <summary>Role.</summary>
        public string Role { get; set; } = String.Empty;
        /// <summary>Content.</summary>
        public string Content { get; set; } = String.Empty;
        /// <summary>Runner identifier.</summary>
        public string RunnerId { get; set; } = String.Empty;
        /// <summary>Model.</summary>
        public string Model { get; set; } = String.Empty;
        /// <summary>Token estimate.</summary>
        public int TokenEstimate { get; set; } = 0;
        /// <summary>Time to first token in milliseconds.</summary>
        public double TimeToFirstTokenMs { get; set; } = 0;
        /// <summary>Streaming duration in milliseconds.</summary>
        public double StreamingTimeMs { get; set; } = 0;
        /// <summary>Total inference duration in milliseconds.</summary>
        public double TotalTimeMs { get; set; } = 0;
        /// <summary>Estimated tokens used.</summary>
        public int TokensUsed { get; set; } = 0;
        /// <summary>Tool run identifier associated with this message.</summary>
        public string RunId { get; set; } = String.Empty;
        /// <summary>Safe tool-call summary JSON associated with this message.</summary>
        public string ToolCallsJson { get; set; } = String.Empty;
        /// <summary>Tool-call identifier associated with this message when it represents a tool response.</summary>
        public string ToolCallId { get; set; } = String.Empty;
        /// <summary>Additional message metadata JSON.</summary>
        public string MetadataJson { get; set; } = String.Empty;
        /// <summary>Created UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Chat context truncation notice.
    /// </summary>
    public class ChatTruncationNotice
    {
        /// <summary>Conversation identifier.</summary>
        public string ConversationId { get; set; } = String.Empty;
        /// <summary>Whether prior history was omitted from the model prompt.</summary>
        public bool Truncated { get; set; } = false;
        /// <summary>Number of previous messages included in the model prompt.</summary>
        public int IncludedMessageCount { get; set; } = 0;
        /// <summary>Number of previous messages omitted from the model prompt.</summary>
        public int OmittedMessageCount { get; set; } = 0;
        /// <summary>Estimated prompt tokens sent to the model.</summary>
        public int PromptTokenEstimate { get; set; } = 0;
        /// <summary>Token budget used for history selection.</summary>
        public int PromptBudgetTokens { get; set; } = 0;
        /// <summary>Configured context window tokens.</summary>
        public int ContextWindowTokens { get; set; } = 0;
    }

    /// <summary>
    /// Feedback record.
    /// </summary>
    public class Feedback
    {
        /// <summary>Feedback identifier.</summary>
        public string Id { get; set; } = IdGenerator.Feedback();
        /// <summary>Tenant identifier.</summary>
        public string TenantId { get; set; } = String.Empty;
        /// <summary>User identifier.</summary>
        public string UserId { get; set; } = String.Empty;
        /// <summary>Conversation identifier.</summary>
        public string ConversationId { get; set; } = String.Empty;
        /// <summary>Message identifier.</summary>
        public string MessageId { get; set; } = String.Empty;
        /// <summary>Rating.</summary>
        public int Rating { get; set; } = 0;
        /// <summary>Comment.</summary>
        public string Comment { get; set; } = String.Empty;
        /// <summary>Time to first token in milliseconds.</summary>
        public double TimeToFirstTokenMs { get; set; } = 0;
        /// <summary>Streaming duration in milliseconds.</summary>
        public double StreamingTimeMs { get; set; } = 0;
        /// <summary>Total inference duration in milliseconds.</summary>
        public double TotalTimeMs { get; set; } = 0;
        /// <summary>Estimated tokens used.</summary>
        public int TokensUsed { get; set; } = 0;
        /// <summary>Created UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Request history entry.
    /// </summary>
    public class RequestHistoryEntry
    {
        /// <summary>Request identifier.</summary>
        public string Id { get; set; } = IdGenerator.Request();
        /// <summary>Tenant identifier.</summary>
        public string? TenantId { get; set; } = null;
        /// <summary>User identifier.</summary>
        public string? UserId { get; set; } = null;
        /// <summary>HTTP method.</summary>
        public string Method { get; set; } = String.Empty;
        /// <summary>Path.</summary>
        public string Path { get; set; } = String.Empty;
        /// <summary>Status code.</summary>
        public int StatusCode { get; set; } = 0;
        /// <summary>Duration in milliseconds.</summary>
        public double DurationMs { get; set; } = 0;
        /// <summary>Request headers as JSON.</summary>
        public string RequestHeaders { get; set; } = String.Empty;
        /// <summary>Request body.</summary>
        public string RequestBody { get; set; } = String.Empty;
        /// <summary>Response headers as JSON.</summary>
        public string ResponseHeaders { get; set; } = String.Empty;
        /// <summary>Response body.</summary>
        public string ResponseBody { get; set; } = String.Empty;
        /// <summary>Time to first token in milliseconds.</summary>
        public double TimeToFirstTokenMs { get; set; } = 0;
        /// <summary>Streaming duration in milliseconds.</summary>
        public double StreamingTimeMs { get; set; } = 0;
        /// <summary>Total inference duration in milliseconds.</summary>
        public double TotalTimeMs { get; set; } = 0;
        /// <summary>Estimated tokens used.</summary>
        public int TokensUsed { get; set; } = 0;
        /// <summary>Tool run identifier associated with the request.</summary>
        public string ToolRunId { get; set; } = String.Empty;
        /// <summary>Tool calls executed during the request.</summary>
        public int ToolCallCount { get; set; } = 0;
        /// <summary>Total elapsed milliseconds spent in tools during the request.</summary>
        public double ToolElapsedMs { get; set; } = 0;
        /// <summary>Tool-agent iterations used during the request.</summary>
        public int AgentIterations { get; set; } = 0;
        /// <summary>Selected system prompt template identifier.</summary>
        public string SystemPromptId { get; set; } = String.Empty;
        /// <summary>Selected system prompt template name.</summary>
        public string SystemPromptName { get; set; } = String.Empty;
        /// <summary>Whether the selected system prompt was the default prompt.</summary>
        public bool SystemPromptDefault { get; set; } = false;
        /// <summary>SHA-256 hash of the system prompt content sent to the model.</summary>
        public string SystemPromptHash { get; set; } = String.Empty;
        /// <summary>Selected tool prompt template identifier.</summary>
        public string ToolPromptId { get; set; } = String.Empty;
        /// <summary>Selected tool prompt template name.</summary>
        public string ToolPromptName { get; set; } = String.Empty;
        /// <summary>Whether the selected tool prompt was the default prompt.</summary>
        public bool ToolPromptDefault { get; set; } = false;
        /// <summary>SHA-256 hash of the tool prompt content sent to the model.</summary>
        public string ToolPromptHash { get; set; } = String.Empty;
        /// <summary>Created UTC.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Request history summary.
    /// </summary>
    public class RequestHistorySummary
    {
        /// <summary>Total count.</summary>
        public int TotalCount { get; set; } = 0;
        /// <summary>Total success.</summary>
        public int TotalSuccess { get; set; } = 0;
        /// <summary>Total failure.</summary>
        public int TotalFailure { get; set; } = 0;
        /// <summary>Average duration.</summary>
        public double AverageDurationMs { get; set; } = 0;
        /// <summary>Buckets.</summary>
        public List<RequestHistoryBucket> Buckets { get; set; } = new List<RequestHistoryBucket>();
    }

    /// <summary>
    /// Request history bucket.
    /// </summary>
    public class RequestHistoryBucket
    {
        /// <summary>Bucket start UTC.</summary>
        public DateTime BucketStartUtc { get; set; }
        /// <summary>Bucket end UTC.</summary>
        public DateTime BucketEndUtc { get; set; }
        /// <summary>Success count.</summary>
        public int SuccessCount { get; set; }
        /// <summary>Failure count.</summary>
        public int FailureCount { get; set; }
        /// <summary>Average duration.</summary>
        public double AverageDurationMs { get; set; }
    }

    /// <summary>
    /// A single model server health check result.
    /// </summary>
    public class HealthCheckRecord
    {
        /// <summary>UTC timestamp of the check.</summary>
        public DateTime TimestampUtc { get; set; }
        /// <summary>Whether the check succeeded.</summary>
        public bool Success { get; set; }
    }

    /// <summary>
    /// In-memory runtime health state for a model server.
    /// </summary>
    public class EndpointHealthState
    {
        /// <summary>Endpoint identifier.</summary>
        public string EndpointId { get; set; } = String.Empty;
        /// <summary>Endpoint display name.</summary>
        public string EndpointName { get; set; } = String.Empty;
        /// <summary>Tenant scope. Wilson model servers are global, so this is empty.</summary>
        public string TenantId { get; set; } = String.Empty;
        /// <summary>Current health state. Starts false until enough successful checks occur.</summary>
        public bool IsHealthy { get; set; } = false;
        /// <summary>Whether active background health monitoring is enabled.</summary>
        public bool HealthCheckEnabled { get; set; } = true;
        /// <summary>When monitoring began.</summary>
        public DateTime FirstCheckUtc { get; set; } = DateTime.UtcNow;
        /// <summary>Most recent check time.</summary>
        public DateTime? LastCheckUtc { get; set; }
        /// <summary>Last transition to healthy.</summary>
        public DateTime? LastHealthyUtc { get; set; }
        /// <summary>Most recent unhealthy transition, or first failed check before initial recovery.</summary>
        public DateTime? LastUnhealthyUtc { get; set; }
        /// <summary>Last transition in either direction.</summary>
        public DateTime? LastStateChangeUtc { get; set; }
        /// <summary>Cumulative healthy milliseconds.</summary>
        public long TotalUptimeMs { get; set; } = 0;
        /// <summary>Cumulative unhealthy milliseconds.</summary>
        public long TotalDowntimeMs { get; set; } = 0;
        /// <summary>Running counter of consecutive successes.</summary>
        public int ConsecutiveSuccesses { get; set; } = 0;
        /// <summary>Running counter of consecutive failures.</summary>
        public int ConsecutiveFailures { get; set; } = 0;
        /// <summary>Error message from the most recent failed check.</summary>
        public string? LastError { get; set; }
        /// <summary>Rolling window of individual check results.</summary>
        public List<HealthCheckRecord> CheckHistory { get; } = new List<HealthCheckRecord>();
        /// <summary>Per-state lock for thread safety.</summary>
        [JsonIgnore]
        public object Lock { get; } = new object();
        /// <summary>Separate lock for history list access.</summary>
        [JsonIgnore]
        public object HistoryLock { get; } = new object();
    }

    /// <summary>
    /// Model server health status for API responses and dashboard display.
    /// </summary>
    public class EndpointHealthStatus
    {
        /// <summary>Endpoint identifier.</summary>
        public string EndpointId { get; set; } = String.Empty;
        /// <summary>Endpoint display name.</summary>
        public string EndpointName { get; set; } = String.Empty;
        /// <summary>Tenant scope. Wilson model servers are global, so this is empty.</summary>
        public string TenantId { get; set; } = String.Empty;
        /// <summary>Current health state.</summary>
        public bool IsHealthy { get; set; }
        /// <summary>Whether active background health monitoring is enabled.</summary>
        public bool HealthCheckEnabled { get; set; } = true;
        /// <summary>When monitoring began.</summary>
        public DateTime FirstCheckUtc { get; set; }
        /// <summary>Most recent check time.</summary>
        public DateTime? LastCheckUtc { get; set; }
        /// <summary>Last transition to healthy.</summary>
        public DateTime? LastHealthyUtc { get; set; }
        /// <summary>Most recent unhealthy transition, or first failed check before initial recovery.</summary>
        public DateTime? LastUnhealthyUtc { get; set; }
        /// <summary>Last transition in either direction.</summary>
        public DateTime? LastStateChangeUtc { get; set; }
        /// <summary>Cumulative healthy milliseconds.</summary>
        public long TotalUptimeMs { get; set; }
        /// <summary>Cumulative unhealthy milliseconds.</summary>
        public long TotalDowntimeMs { get; set; }
        /// <summary>Uptime percentage from monitoring start until now.</summary>
        public double UptimePercentage { get; set; }
        /// <summary>Consecutive successful checks.</summary>
        public int ConsecutiveSuccesses { get; set; }
        /// <summary>Consecutive failed checks.</summary>
        public int ConsecutiveFailures { get; set; }
        /// <summary>Error message from the most recent failed check.</summary>
        public string? LastError { get; set; }
        /// <summary>Rolling window of check history records.</summary>
        public List<HealthCheckRecord> History { get; set; } = new List<HealthCheckRecord>();

        /// <summary>
        /// Create an API health status snapshot from runtime state.
        /// </summary>
        /// <param name="state">Runtime state.</param>
        /// <returns>Health status snapshot.</returns>
        public static EndpointHealthStatus FromState(EndpointHealthState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            EndpointHealthStatus status = new EndpointHealthStatus();

            lock (state.Lock)
            {
                status.EndpointId = state.EndpointId;
                status.EndpointName = state.EndpointName;
                status.TenantId = state.TenantId;
                status.IsHealthy = state.IsHealthy;
                status.HealthCheckEnabled = state.HealthCheckEnabled;
                status.FirstCheckUtc = state.FirstCheckUtc;
                status.LastCheckUtc = state.LastCheckUtc;
                status.LastHealthyUtc = state.LastHealthyUtc;
                status.LastUnhealthyUtc = state.LastUnhealthyUtc;
                status.LastStateChangeUtc = state.LastStateChangeUtc;
                status.ConsecutiveSuccesses = state.ConsecutiveSuccesses;
                status.ConsecutiveFailures = state.ConsecutiveFailures;
                status.LastError = state.LastError;

                long uptimeMs = state.TotalUptimeMs;
                long downtimeMs = state.TotalDowntimeMs;
                if (state.LastStateChangeUtc.HasValue)
                {
                    long currentPeriodMs = (long)(DateTime.UtcNow - state.LastStateChangeUtc.Value).TotalMilliseconds;
                    if (currentPeriodMs < 0) currentPeriodMs = 0;
                    if (state.IsHealthy) uptimeMs += currentPeriodMs;
                    else downtimeMs += currentPeriodMs;
                }

                status.TotalUptimeMs = uptimeMs;
                status.TotalDowntimeMs = downtimeMs;
                long totalMs = uptimeMs + downtimeMs;
                status.UptimePercentage = totalMs > 0 ? Math.Round((double)uptimeMs / totalMs * 100.0, 2) : 0.0;
            }

            lock (state.HistoryLock)
            {
                status.History = new List<HealthCheckRecord>(state.CheckHistory);
            }

            return status;
        }
    }

    /// <summary>
    /// Model runner status for dashboard display.
    /// </summary>
    public class ModelRunnerStatus
    {
        /// <summary>Runner identifier.</summary>
        public string Id { get; set; } = String.Empty;
        /// <summary>Display name.</summary>
        public string Name { get; set; } = String.Empty;
        /// <summary>API type.</summary>
        public string ApiType { get; set; } = String.Empty;
        /// <summary>Endpoint URL.</summary>
        public string Endpoint { get; set; } = String.Empty;
        /// <summary>Configured model list from settings.</summary>
        public List<string> ConfiguredModels { get; set; } = new List<string>();
        /// <summary>Available model list resolved from configuration or server API.</summary>
        public List<string> AvailableModels { get; set; } = new List<string>();
        /// <summary>Models suitable for chat and completion requests.</summary>
        public List<string> ChatModels { get; set; } = new List<string>();
        /// <summary>Models suitable only for embedding requests.</summary>
        public List<string> EmbeddingModels { get; set; } = new List<string>();
        /// <summary>Models that advertise native tool-call support.</summary>
        public List<string> ToolModels { get; set; } = new List<string>();
        /// <summary>Currently loaded or running model list reported by the model server.</summary>
        public List<string> LoadedModels { get; set; } = new List<string>();
        /// <summary>Model list used by legacy dashboard chat selector.</summary>
        public List<string> Models { get; set; } = new List<string>();
        /// <summary>Context window for truncation.</summary>
        public int ContextWindowTokens { get; set; } = 8192;
        /// <summary>Whether tools are enabled for this runner.</summary>
        public bool ToolsEnabled { get; set; } = true;
        /// <summary>Whether this runner supports tool calls.</summary>
        public bool SupportsTools { get; set; } = true;
        /// <summary>Tool-calling API format.</summary>
        public string ToolCallingApiFormat { get; set; } = String.Empty;
        /// <summary>Whether this runner supports parallel tool calls.</summary>
        public bool SupportsParallelToolCalls { get; set; } = true;
        /// <summary>Whether this runner supports streaming tool-call deltas.</summary>
        public bool SupportsStreamingToolCalls { get; set; } = true;
        /// <summary>Chat-completions path for tool-capable transports.</summary>
        public string ChatCompletionsPath { get; set; } = String.Empty;
        /// <summary>Whether background health checks are enabled.</summary>
        public bool HealthCheckEnabled { get; set; } = true;
        /// <summary>Effective health check URL.</summary>
        public string? HealthCheckUrl { get; set; }
        /// <summary>Health check HTTP method.</summary>
        public string HealthCheckMethod { get; set; } = "GET";
        /// <summary>Milliseconds between health checks.</summary>
        public int HealthCheckIntervalMs { get; set; }
        /// <summary>Per-check timeout in milliseconds.</summary>
        public int HealthCheckTimeoutMs { get; set; }
        /// <summary>Expected HTTP status code for a healthy response.</summary>
        public int HealthCheckExpectedStatusCode { get; set; } = 200;
        /// <summary>Consecutive successes required to mark healthy.</summary>
        public int HealthyThreshold { get; set; } = 2;
        /// <summary>Consecutive failures required to mark unhealthy.</summary>
        public int UnhealthyThreshold { get; set; } = 2;
        /// <summary>Whether the API key is sent with health check requests.</summary>
        public bool HealthCheckUseAuth { get; set; }
        /// <summary>Latest background health check status, if health checks are enabled.</summary>
        public EndpointHealthStatus? Health { get; set; }
        /// <summary>Whether the model server status query succeeded.</summary>
        public bool Online { get; set; } = true;
        /// <summary>Status or error message.</summary>
        public string StatusMessage { get; set; } = String.Empty;
    }

    /// <summary>
    /// Model pull result for dashboard display.
    /// </summary>
    public class ModelPullResult
    {
        /// <summary>Runner identifier.</summary>
        public string RunnerId { get; set; } = String.Empty;
        /// <summary>Requested model name.</summary>
        public string Model { get; set; } = String.Empty;
        /// <summary>Model server status message.</summary>
        public string Status { get; set; } = String.Empty;
    }

    /// <summary>
    /// Chat completion request settings.
    /// </summary>
    public class CompletionRequestSettings
    {
        /// <summary>Default system prompt.</summary>
        public const string DefaultSystemPrompt = "Use prior turns only as context. Respond only to the latest user message, and do not replay or quote earlier assistant responses unless the user explicitly asks for them.";
        /// <summary>System prompt.</summary>
        public string SystemPrompt { get; set; } = DefaultSystemPrompt;
        /// <summary>Tool system prompt. Leave blank to use Wilson's generated tool instructions.</summary>
        public string ToolSystemPrompt { get; set; } = String.Empty;
        /// <summary>Sampling temperature.</summary>
        public double? Temperature { get; set; } = 0.7;
        /// <summary>Nucleus sampling threshold.</summary>
        public double? TopP { get; set; } = 0.9;
        /// <summary>Maximum response tokens.</summary>
        public int? MaxTokens { get; set; } = 2048;
        /// <summary>Ollama top-K sampling.</summary>
        public int? TopK { get; set; } = 40;
        /// <summary>Ollama minimum probability threshold.</summary>
        public double? MinP { get; set; } = 0.0;
        /// <summary>Ollama repeat penalty.</summary>
        public double? RepeatPenalty { get; set; } = 1.1;
        /// <summary>Ollama repeat lookback.</summary>
        public int? RepeatLastN { get; set; } = 64;
        /// <summary>Optional random seed.</summary>
        public int? Seed { get; set; } = null;
    }
}
