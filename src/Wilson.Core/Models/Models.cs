namespace Wilson.Core.Models
{
    using System;
    using System.Collections.Generic;
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
        public string Title { get; set; } = "New conversation";
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
        /// <summary>Currently loaded or running model list reported by the model server.</summary>
        public List<string> LoadedModels { get; set; } = new List<string>();
        /// <summary>Model list used by legacy dashboard chat selector.</summary>
        public List<string> Models { get; set; } = new List<string>();
        /// <summary>Context window for truncation.</summary>
        public int ContextWindowTokens { get; set; } = 8192;
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
