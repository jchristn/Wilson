namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Wilson.Core.Database;
    using Wilson.Core.Models;
    using Wilson.Core.Services;
    using Wilson.Core.Settings;
    using Wilson.Core.Tools;
    using Wilson.Server;

    /// <summary>
    /// Automated Wilson test suites.
    /// </summary>
    public static class WilsonSuites
    {
        /// <summary>
        /// Run all tests.
        /// </summary>
        public static async Task RunAllAsync()
        {
            await DatabaseRoundTripAsync().ConfigureAwait(false);
            await DatabaseParameterizationAsync().ConfigureAwait(false);
            await ToolPersistenceAsync().ConfigureAwait(false);
            await ToolAuditRedactionPersistenceAsync().ConfigureAwait(false);
            IdLength();
            ToolSettingsDefaults();
            await ToolServiceFoundationAsync().ConfigureAwait(false);
            await ToolDiagnosticsApiAsync().ConfigureAwait(false);
            await PublicChatToolTraceApiAsync().ConfigureAwait(false);
            await WorkingDirectoryGuardAsync().ConfigureAwait(false);
            await ToolArgumentValidationAndOutputLimiterAsync().ConfigureAwait(false);
            await FilesystemDiscoveryToolsAsync().ConfigureAwait(false);
            await FilesystemMutationToolsAsync().ConfigureAwait(false);
            await RunProcessToolAsync().ConfigureAwait(false);
            ToolCapableInferenceParsing();
            await ToolAgentLoopAsync().ConfigureAwait(false);
            await ToolAgentApprovalPolicyAsync().ConfigureAwait(false);
            await ToolAgentLoopCoverageAsync().ConfigureAwait(false);
            await ToolAgentPerTurnOutputLimitAsync().ConfigureAwait(false);
            ContextTruncationAsync();
            HealthCheckDefaults();
            HealthStatusSnapshot();
        }

        private static async Task DatabaseRoundTripAsync()
        {
            string filename = Path.Combine(Path.GetTempPath(), "wilson-" + Guid.NewGuid().ToString("N") + ".db");
            DatabaseDriver database = new DatabaseDriver(new DatabaseSettings { Type = "Sqlite", Filename = filename });
            await database.InitializeAsync().ConfigureAwait(false);
            await database.SeedAsync(new SeedSettings { AccessKey = "test-token", UserEmail = "test@example.com" }).ConfigureAwait(false);
            List<Tenant> tenants = await database.GetTenantsAsync().ConfigureAwait(false);
            if (tenants.Count != 1) throw new InvalidOperationException("Expected one seeded tenant.");
            Credential? credential = await database.GetCredentialByAccessKeyAsync("test-token").ConfigureAwait(false);
            if (credential == null) throw new InvalidOperationException("Expected seeded credential.");
        }

        private static async Task DatabaseParameterizationAsync()
        {
            string filename = Path.Combine(Path.GetTempPath(), "wilson-db-parameterization-" + Guid.NewGuid().ToString("N") + ".db");
            DatabaseDriver database = new DatabaseDriver(new DatabaseSettings { Type = "Sqlite", Filename = filename });
            string injection = "'; DROP TABLE users; DROP TABLE tenants; --";
            string tenantId = "tenant-param-" + injection;
            string userId = "user-param-" + injection;
            string credentialId = "credential-param-" + injection;
            string conversationId = "conversation-param-" + injection;
            string messageId = "message-param-" + injection;
            string requestId = "request-param-" + injection;
            string toolRunId = "toolrun-param-" + injection;
            string toolCallRecordId = "toolrec-param-" + injection;

            try
            {
                await database.InitializeAsync().ConfigureAwait(false);

                Tenant tenant = new Tenant { Id = tenantId, Name = "Tenant " + injection };
                await database.CreateTenantAsync(tenant).ConfigureAwait(false);

                User user = new User
                {
                    Id = userId,
                    TenantId = tenantId,
                    FirstName = "First " + injection,
                    LastName = "Last " + injection,
                    Email = "user+" + injection + "@example.com"
                };
                await database.CreateUserAsync(user).ConfigureAwait(false);

                Credential credential = new Credential
                {
                    Id = credentialId,
                    TenantId = tenantId,
                    UserId = userId,
                    Name = "Credential " + injection,
                    AccessKey = "access-" + injection,
                    SecretLast4 = "1234"
                };
                await database.CreateCredentialAsync(credential).ConfigureAwait(false);

                Conversation conversation = new Conversation
                {
                    Id = conversationId,
                    TenantId = tenantId,
                    UserId = userId,
                    Title = "Conversation " + injection,
                    RunnerId = "runner-" + injection,
                    Model = "model-" + injection
                };
                await database.CreateConversationAsync(conversation).ConfigureAwait(false);

                ChatMessage message = new ChatMessage
                {
                    Id = messageId,
                    TenantId = tenantId,
                    ConversationId = conversationId,
                    Role = "assistant",
                    Content = "Message " + injection,
                    RunnerId = "runner-" + injection,
                    Model = "model-" + injection,
                    RunId = toolRunId
                };
                await database.CreateMessageAsync(message).ConfigureAwait(false);

                RequestHistoryEntry requestHistory = new RequestHistoryEntry
                {
                    Id = requestId,
                    TenantId = tenantId,
                    UserId = userId,
                    Method = "POST",
                    Path = "/v1.0/api/chat/" + injection,
                    StatusCode = 200,
                    DurationMs = 1
                };
                await database.CreateRequestHistoryAsync(requestHistory).ConfigureAwait(false);

                ToolRun toolRun = new ToolRun
                {
                    RunId = toolRunId,
                    TenantId = tenantId,
                    UserId = userId,
                    ConversationId = conversationId,
                    RunnerId = "runner-" + injection,
                    Model = "model-" + injection,
                    Status = ToolStatuses.Completed
                };
                await database.CreateToolRunAsync(toolRun).ConfigureAwait(false);

                ToolExecutionRecord toolCall = new ToolExecutionRecord
                {
                    Id = toolCallRecordId,
                    TenantId = tenantId,
                    UserId = userId,
                    ConversationId = conversationId,
                    RunId = toolRunId,
                    RequestHistoryId = requestId,
                    AssistantMessageId = messageId,
                    ToolCallId = "toolcall-" + injection,
                    ToolName = "read_file",
                    ArgumentsJson = "{}",
                    ResultJson = "{}",
                    ResultSummaryJson = "{}",
                    ResultPreview = "Preview " + injection,
                    Status = ToolStatuses.Completed,
                    Success = true
                };
                await database.CreateToolCallAsync(toolCall).ConfigureAwait(false);

                Tenant? storedTenant = await database.GetTenantAsync(tenantId).ConfigureAwait(false);
                if (storedTenant == null || !String.Equals(storedTenant.Name, tenant.Name, StringComparison.Ordinal)) throw new InvalidOperationException("Parameterized tenant value was not preserved.");

                User? storedUser = await database.GetUserByEmailAsync(tenantId, user.Email).ConfigureAwait(false);
                if (storedUser == null || !String.Equals(storedUser.Id, userId, StringComparison.Ordinal)) throw new InvalidOperationException("Parameterized user lookup failed.");

                Credential? storedCredential = await database.GetCredentialByAccessKeyAsync(credential.AccessKey).ConfigureAwait(false);
                if (storedCredential == null || !String.Equals(storedCredential.Id, credentialId, StringComparison.Ordinal)) throw new InvalidOperationException("Parameterized credential lookup failed.");

                if ((await database.GetMessagesAsync(tenantId, conversationId).ConfigureAwait(false)).Count != 1) throw new InvalidOperationException("Parameterized message lookup failed.");
                if ((await database.GetRequestHistoryAsync(tenantId).ConfigureAwait(false)).Count != 1) throw new InvalidOperationException("Parameterized request-history lookup failed.");
                if (await database.GetToolRunAsync(tenantId, toolRunId).ConfigureAwait(false) == null) throw new InvalidOperationException("Parameterized tool-run lookup failed.");
                if (await database.GetToolCallAsync(tenantId, toolCallRecordId).ConfigureAwait(false) == null) throw new InvalidOperationException("Parameterized tool-call lookup failed.");
                if ((await database.GetUsersAsync(tenantId).ConfigureAwait(false)).Count != 1) throw new InvalidOperationException("Users table was not intact after injection-shaped input.");
                if ((await database.GetTenantsAsync().ConfigureAwait(false)).Count != 1) throw new InvalidOperationException("Tenants table was not intact after injection-shaped input.");
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(filename)) File.Delete(filename);
            }
        }

        private static async Task ToolPersistenceAsync()
        {
            string filename = Path.Combine(Path.GetTempPath(), "wilson-tools-db-" + Guid.NewGuid().ToString("N") + ".db");
            DatabaseDriver database = new DatabaseDriver(new DatabaseSettings { Type = "Sqlite", Filename = filename });
            try
            {
                await database.InitializeAsync().ConfigureAwait(false);
                await database.InitializeAsync().ConfigureAwait(false);

                string tenantId = "tenant-tools-a";
                string otherTenantId = "tenant-tools-b";
                Conversation conversation = new Conversation { TenantId = tenantId, UserId = "user-tools-a", Title = "Tool Persistence", RunnerId = "runner-a", Model = "model-a" };
                await database.CreateConversationAsync(conversation).ConfigureAwait(false);
                ChatMessage message = new ChatMessage
                {
                    TenantId = tenantId,
                    ConversationId = conversation.Id,
                    Role = "assistant",
                    Content = "answer",
                    RunnerId = "runner-a",
                    Model = "model-a",
                    RunId = "toolrun_active",
                    ToolCallsJson = """[{"toolName":"read_file"}]""",
                    MetadataJson = """{"toolCallCount":1}"""
                };
                await database.CreateMessageAsync(message).ConfigureAwait(false);

                ToolRun run = new ToolRun
                {
                    RunId = message.RunId,
                    TenantId = tenantId,
                    UserId = "user-tools-a",
                    ConversationId = conversation.Id,
                    RunnerId = "runner-a",
                    Model = "model-a",
                    Status = ToolStatuses.Completed,
                    StartedUtc = DateTime.UtcNow.AddMilliseconds(-50),
                    CompletedUtc = DateTime.UtcNow,
                    ElapsedMs = 50,
                    IterationCount = 1,
                    ToolCallCount = 1,
                    ErrorCount = 0
                };
                await database.CreateToolRunAsync(run).ConfigureAwait(false);

                ToolExecutionRecord record = new ToolExecutionRecord
                {
                    TenantId = tenantId,
                    UserId = "user-tools-a",
                    ConversationId = conversation.Id,
                    RunId = run.RunId,
                    TraceId = "trace-active",
                    Origin = "chat",
                    AssistantMessageId = message.Id,
                    ToolCallId = "toolcall-active",
                    ToolName = "read_file",
                    Iteration = 1,
                    SequenceNumber = 1,
                    Status = ToolStatuses.Completed,
                    ApprovalPolicy = ToolApprovalPolicies.Auto,
                    ArgumentsJson = "{}",
                    ResultJson = """{"summary":"Completed."}""",
                    ResultSummaryJson = """{"success":true}""",
                    ResultPreview = "Completed.",
                    Success = true,
                    OutputCharacters = 10,
                    OutputBytes = 10,
                    StartedUtc = run.StartedUtc,
                    CompletedUtc = run.CompletedUtc,
                    ElapsedMs = 12,
                    Model = run.Model
                };
                await database.CreateToolCallAsync(record).ConfigureAwait(false);

                ToolRun? storedRun = await database.GetToolRunAsync(tenantId, run.RunId).ConfigureAwait(false);
                if (storedRun == null || storedRun.ToolCallCount != 1) throw new InvalidOperationException("Expected stored tool run.");
                if (await database.GetToolRunAsync(otherTenantId, run.RunId).ConfigureAwait(false) != null) throw new InvalidOperationException("Tool run leaked across tenant scope.");

                storedRun.Status = ToolStatuses.Failed;
                storedRun.ErrorCount = 1;
                await database.UpdateToolRunAsync(storedRun).ConfigureAwait(false);
                ToolRun? updatedRun = await database.GetToolRunAsync(tenantId, run.RunId).ConfigureAwait(false);
                if (updatedRun == null || updatedRun.ErrorCount != 1 || !String.Equals(updatedRun.Status, ToolStatuses.Failed, StringComparison.Ordinal)) throw new InvalidOperationException("Expected updated tool run.");

                ToolExecutionRecord? storedCall = await database.GetToolCallAsync(tenantId, record.Id).ConfigureAwait(false);
                if (storedCall == null || !storedCall.Success) throw new InvalidOperationException("Expected stored tool call.");
                if (await database.GetToolCallAsync(otherTenantId, record.Id).ConfigureAwait(false) != null) throw new InvalidOperationException("Tool call leaked across tenant scope.");

                List<ToolExecutionRecord> conversationCalls = await database.GetToolCallsForConversationAsync(tenantId, conversation.Id).ConfigureAwait(false);
                if (conversationCalls.Count != 1) throw new InvalidOperationException("Expected conversation tool call.");
                if ((await database.GetToolCallsForConversationAsync(otherTenantId, conversation.Id).ConfigureAwait(false)).Count != 0) throw new InvalidOperationException("Conversation tool calls leaked across tenant scope.");

                List<ToolExecutionRecord> messageCalls = await database.GetToolCallsForMessageAsync(tenantId, message.Id).ConfigureAwait(false);
                if (messageCalls.Count != 1) throw new InvalidOperationException("Expected message-linked tool call.");

                RequestHistoryEntry requestHistory = new RequestHistoryEntry
                {
                    TenantId = tenantId,
                    UserId = "user-tools-a",
                    Method = "POST",
                    Path = "/v1.0/api/chat",
                    StatusCode = 200,
                    DurationMs = 100,
                    ToolRunId = run.RunId,
                    ToolCallCount = 1,
                    ToolElapsedMs = 12,
                    AgentIterations = 1
                };
                await database.CreateRequestHistoryAsync(requestHistory).ConfigureAwait(false);
                await database.AttachToolCallsToRequestHistoryByRunIdAsync(tenantId, run.RunId, requestHistory.Id).ConfigureAwait(false);
                List<RequestHistoryEntry> history = await database.GetRequestHistoryAsync(tenantId).ConfigureAwait(false);
                if (history.Count != 1 || history[0].ToolCallCount != 1 || history[0].AgentIterations != 1) throw new InvalidOperationException("Expected request-history tool metrics.");
                if ((await database.GetToolCallsForRequestHistoryAsync(tenantId, requestHistory.Id).ConfigureAwait(false)).Count != 1) throw new InvalidOperationException("Expected request-history tool call linkage.");
                if ((await database.GetToolCallsForRequestHistoryAsync(otherTenantId, requestHistory.Id).ConfigureAwait(false)).Count != 0) throw new InvalidOperationException("Request-history tool calls leaked across tenant scope.");

                ToolExecutionRecord expired = new ToolExecutionRecord
                {
                    TenantId = tenantId,
                    UserId = "user-tools-a",
                    ConversationId = conversation.Id,
                    RunId = "toolrun_expired",
                    ToolCallId = "toolcall-expired",
                    ToolName = "grep",
                    Status = ToolStatuses.Completed,
                    ApprovalPolicy = ToolApprovalPolicies.Auto,
                    CreatedUtc = DateTime.UtcNow.AddDays(-10),
                    UpdatedUtc = DateTime.UtcNow.AddDays(-10),
                    StartedUtc = DateTime.UtcNow.AddDays(-10),
                    CompletedUtc = DateTime.UtcNow.AddDays(-10),
                    Success = true
                };
                await database.CreateToolRunAsync(new ToolRun { RunId = expired.RunId, TenantId = tenantId, UserId = expired.UserId, ConversationId = conversation.Id, RunnerId = "runner-a", Model = "model-a", Status = ToolStatuses.Completed, CreatedUtc = expired.CreatedUtc, StartedUtc = expired.StartedUtc, CompletedUtc = expired.CompletedUtc }).ConfigureAwait(false);
                await database.CreateToolCallAsync(expired).ConfigureAwait(false);
                await database.DeleteExpiredToolCallsAsync(tenantId, DateTime.UtcNow.AddDays(-1)).ConfigureAwait(false);
                if (await database.GetToolCallAsync(tenantId, expired.Id).ConfigureAwait(false) != null) throw new InvalidOperationException("Expected expired tool call to be deleted.");
                if (await database.GetToolRunAsync(tenantId, expired.RunId).ConfigureAwait(false) != null) throw new InvalidOperationException("Expected expired tool run to be deleted.");

                await database.DeleteConversationAsync(tenantId, conversation.Id).ConfigureAwait(false);
                if ((await database.GetToolCallsForConversationAsync(tenantId, conversation.Id).ConfigureAwait(false)).Count != 0) throw new InvalidOperationException("Expected conversation delete to remove tool calls.");
                if ((await database.GetToolRunsForConversationAsync(tenantId, conversation.Id).ConfigureAwait(false)).Count != 0) throw new InvalidOperationException("Expected conversation delete to remove tool runs.");
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(filename)) File.Delete(filename);
            }
        }

        private static void ContextTruncationAsync()
        {
            InferenceService service = new InferenceService(new Settings());
            List<ChatMessage> messages = new List<ChatMessage>();
            for (int i = 0; i < 20; i++)
            {
                messages.Add(new ChatMessage { Role = "user", Content = new String('x', 400), TokenEstimate = 100 });
            }
            string prompt = service.BuildPrompt(messages, "hello", 512);
            if (!prompt.EndsWith("user: hello", StringComparison.Ordinal)) throw new InvalidOperationException("Prompt did not include the latest user message.");
            if (InferenceService.EstimateTokens(prompt) > 512) throw new InvalidOperationException("Prompt exceeded expected context budget.");
        }

        private static async Task ToolAuditRedactionPersistenceAsync()
        {
            string filename = Path.Combine(Path.GetTempPath(), "wilson-tool-audit-redaction-" + Guid.NewGuid().ToString("N") + ".db");
            DatabaseDriver database = new DatabaseDriver(new DatabaseSettings { Type = "Sqlite", Filename = filename });
            string tenantId = "tenant-audit-redaction";
            string userId = "user-audit-redaction";
            string conversationId = "conversation-audit-redaction";
            try
            {
                await database.InitializeAsync().ConfigureAwait(false);
                await database.CreateTenantAsync(new Tenant { Id = tenantId, Name = "Audit Redaction Tenant" }).ConfigureAwait(false);
                await database.CreateUserAsync(new User { Id = userId, TenantId = tenantId, Email = "audit-redaction@example.com", FirstName = "Audit", LastName = "Redaction" }).ConfigureAwait(false);
                await database.CreateConversationAsync(new Conversation { Id = conversationId, TenantId = tenantId, UserId = userId, Title = "Audit redaction", RunnerId = "runner-audit", Model = "model-audit" }).ConfigureAwait(false);

                ToolRun fullRun = new ToolRun
                {
                    RunId = "toolrun-audit-full",
                    TenantId = tenantId,
                    UserId = userId,
                    ConversationId = conversationId,
                    RunnerId = "runner-audit",
                    Model = "model-audit",
                    Status = ToolStatuses.Completed,
                    StartedUtc = DateTime.UtcNow.AddMilliseconds(-50),
                    CompletedUtc = DateTime.UtcNow,
                    ToolCallCount = 1,
                    IterationCount = 1
                };
                await database.CreateToolRunAsync(fullRun).ConfigureAwait(false);

                List<ToolExecutionRecord> fullRecords = BuildAuditRecordsForTest(
                    fullRun,
                    CreateSecretAuditTraces(),
                    CreateSecretSafeTraces(),
                    new ToolsSettings { StoreToolArguments = true, StoreFullToolResults = true, MaxToolResultBytes = 12000 });
                if (fullRecords.Count != 1) throw new InvalidOperationException("Expected one full audit record.");
                ToolExecutionRecord fullRecord = fullRecords[0];
                AssertAuditRecordRedacted(fullRecord);
                if (!fullRecord.ResultJson.Contains("stdout", StringComparison.Ordinal) || !fullRecord.ResultJson.Contains("stderr", StringComparison.Ordinal))
                    throw new InvalidOperationException("Expected explicit full-result persistence to retain redacted stdout/stderr structure.");

                await database.CreateToolCallAsync(fullRecord).ConfigureAwait(false);
                RequestHistoryEntry history = new RequestHistoryEntry
                {
                    Id = "request-audit-redaction",
                    TenantId = tenantId,
                    UserId = userId,
                    CreatedUtc = DateTime.UtcNow,
                    Method = "POST",
                    Path = "/v1.0/api/chat",
                    StatusCode = 200,
                    RequestBody = "{}",
                    ResponseBody = "{}",
                    ToolRunId = fullRun.RunId,
                    ToolCallCount = 1,
                    ToolElapsedMs = 12,
                    AgentIterations = 1
                };
                await database.CreateRequestHistoryAsync(history).ConfigureAwait(false);
                await database.AttachToolCallsToRequestHistoryByRunIdAsync(tenantId, fullRun.RunId, history.Id).ConfigureAwait(false);
                List<ToolExecutionRecord> linked = await database.GetToolCallsForRequestHistoryAsync(tenantId, history.Id).ConfigureAwait(false);
                if (linked.Count != 1) throw new InvalidOperationException("Expected one request-history linked audit record.");
                AssertAuditRecordRedacted(linked[0]);

                ToolRun summaryRun = new ToolRun
                {
                    RunId = "toolrun-audit-summary",
                    TenantId = tenantId,
                    UserId = userId,
                    ConversationId = conversationId,
                    RunnerId = "runner-audit",
                    Model = "model-audit",
                    Status = ToolStatuses.Completed,
                    StartedUtc = DateTime.UtcNow.AddMilliseconds(-50),
                    CompletedUtc = DateTime.UtcNow,
                    ToolCallCount = 1,
                    IterationCount = 1
                };
                List<ToolExecutionRecord> summaryRecords = BuildAuditRecordsForTest(
                    summaryRun,
                    CreateSecretAuditTraces(),
                    CreateSecretSafeTraces(),
                    new ToolsSettings { StoreToolArguments = true, StoreFullToolResults = false, MaxToolResultBytes = 12000 });
                if (summaryRecords.Count != 1) throw new InvalidOperationException("Expected one summary audit record.");
                ToolExecutionRecord summaryRecord = summaryRecords[0];
                AssertAuditRecordRedacted(summaryRecord);
                if (summaryRecord.ResultJson.Contains("stdout", StringComparison.Ordinal) || summaryRecord.ResultJson.Contains("stderr", StringComparison.Ordinal))
                    throw new InvalidOperationException("Summary-only audit persistence must not store raw stdout/stderr result structure.");

                ToolRun suppressedRun = new ToolRun
                {
                    RunId = "toolrun-audit-suppressed",
                    TenantId = tenantId,
                    UserId = userId,
                    ConversationId = conversationId,
                    RunnerId = "runner-audit",
                    Model = "model-audit",
                    Status = ToolStatuses.Completed,
                    StartedUtc = DateTime.UtcNow.AddMilliseconds(-50),
                    CompletedUtc = DateTime.UtcNow,
                    ToolCallCount = 1,
                    IterationCount = 1
                };
                List<ToolExecutionRecord> suppressedRecords = BuildAuditRecordsForTest(
                    suppressedRun,
                    CreateSecretAuditTraces(),
                    CreateSecretSafeTraces(),
                    new ToolsSettings { StoreToolArguments = false, StoreFullToolResults = true, MaxToolResultBytes = 12000 });
                if (suppressedRecords.Count != 1) throw new InvalidOperationException("Expected one suppressed-argument audit record.");
                ToolExecutionRecord suppressedRecord = suppressedRecords[0];
                if (!String.Equals(suppressedRecord.ArgumentsJson, "{}", StringComparison.Ordinal))
                    throw new InvalidOperationException("Expected StoreToolArguments=false to suppress persisted arguments.");
                AssertAuditRecordRedacted(suppressedRecord);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(filename)) File.Delete(filename);
            }
        }

        private static List<ToolAuditTrace> CreateSecretAuditTraces()
        {
            return new List<ToolAuditTrace>
            {
                new ToolAuditTrace
                {
                    ProviderToolCallId = "provider-call-1",
                    ToolName = "run_process",
                    DisplayLabel = "Run Process",
                    Iteration = 1,
                    SequenceNumber = 1,
                    ArgumentsJson = """
{"command":"pwsh","api_key":"sk-live-secret","nested":{"authorization":"Bearer argument-token"},"args":["--password=p@ssword"]}
""",
                    ResultJson = """
{"stdout":"hello\nAPI_KEY=secret123\nAuthorization: Bearer output-token","stderr":"password=hunter2","token":"result-token"}
""",
                    Success = true,
                    Truncated = false,
                    OutputCharacters = 120,
                    ElapsedMs = 12,
                    StartedUtc = DateTime.UtcNow.AddMilliseconds(-12),
                    CompletedUtc = DateTime.UtcNow
                }
            };
        }

        private static List<ToolTrace> CreateSecretSafeTraces()
        {
            return new List<ToolTrace>
            {
                new ToolTrace
                {
                    ToolCallId = "safe-call-1",
                    ToolName = "run_process",
                    DisplayLabel = "Run Process",
                    Iteration = 1,
                    SequenceNumber = 1,
                    Success = true,
                    Truncated = false,
                    OutputCharacters = 120,
                    ElapsedMs = 12,
                    Summary = "Completed with token=summary-token and Bearer summary-bearer-token.",
                    StartedUtc = DateTime.UtcNow.AddMilliseconds(-12),
                    CompletedUtc = DateTime.UtcNow
                }
            };
        }

        private static List<ToolExecutionRecord> BuildAuditRecordsForTest(ToolRun run, List<ToolAuditTrace> auditTraces, List<ToolTrace> safeTraces, ToolsSettings tools)
        {
            return ToolAuditWriter.BuildExecutionRecords(run, auditTraces, safeTraces, ToolApprovalPolicies.Auto, "assistant-audit-message", tools);
        }

        private static void AssertAuditRecordRedacted(ToolExecutionRecord record)
        {
            string combined = String.Join("\n", record.ArgumentsJson, record.ResultJson, record.ResultSummaryJson, record.ResultPreview, record.ErrorMessage ?? String.Empty);
            string[] forbidden = new[]
            {
                "sk-live-secret",
                "argument-token",
                "p@ssword",
                "secret123",
                "output-token",
                "hunter2",
                "result-token",
                "summary-token",
                "summary-bearer-token"
            };

            foreach (string value in forbidden)
            {
                if (combined.Contains(value, StringComparison.Ordinal))
                    throw new InvalidOperationException("Audit record leaked sensitive value: " + value);
            }

            if (!combined.Contains("[redacted]", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected audit record to contain redaction markers.");
        }

        private static void IdLength()
        {
            List<string> ids = new List<string>
            {
                Wilson.Core.Helpers.IdGenerator.Tenant(),
                Wilson.Core.Helpers.IdGenerator.User(),
                Wilson.Core.Helpers.IdGenerator.Credential(),
                Wilson.Core.Helpers.IdGenerator.Conversation(),
                Wilson.Core.Helpers.IdGenerator.Message(),
                Wilson.Core.Helpers.IdGenerator.Feedback(),
                Wilson.Core.Helpers.IdGenerator.Request(),
                Wilson.Core.Helpers.IdGenerator.ToolRun(),
                Wilson.Core.Helpers.IdGenerator.ToolCall(),
                Wilson.Core.Helpers.IdGenerator.ToolExecution(),
                Wilson.Core.Helpers.IdGenerator.Session()
            };

            foreach (string id in ids)
            {
                if (id.Length > 32) throw new InvalidOperationException("Generated ID exceeds 32 characters: " + id);
            }
        }

        private static void ToolSettingsDefaults()
        {
            ToolsSettings tools = new ToolsSettings();
            if (tools.Enabled) throw new InvalidOperationException("Tools should be disabled by default.");
            if (!tools.BuiltInsEnabled) throw new InvalidOperationException("Built-in tools should be enabled for catalog resolution by default.");
            if (!String.Equals(tools.DefaultApprovalPolicy, ToolApprovalPolicies.Ask, StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected default tool approval policy.");
            if (tools.MaxAgentIterations != 25 || tools.MaxToolCallsPerTurn != 12) throw new InvalidOperationException("Unexpected default tool limits.");

            ModelRunnerSettings ollama = new ModelRunnerSettings { ApiType = "Ollama", Endpoint = "http://localhost:11434" };
            ModelRunnerSettings.ApplyToolDefaults(ollama);
            if (!ollama.SupportsTools || !String.Equals(ollama.ToolCallingApiFormat, "OllamaChat", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected Ollama tool defaults.");

            ModelRunnerSettings openAi = new ModelRunnerSettings { ApiType = "OpenAI", Endpoint = "https://api.openai.com" };
            ModelRunnerSettings.ApplyToolDefaults(openAi);
            if (!openAi.SupportsTools || !String.Equals(openAi.ToolCallingApiFormat, "OpenAIChatCompletions", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected OpenAI tool defaults.");
            if (!String.Equals(openAi.ChatCompletionsPath, "/v1/chat/completions", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected OpenAI chat completions path.");

            ModelRunnerSettings unsupported = new ModelRunnerSettings { ApiType = "Custom", Endpoint = "http://localhost:9999" };
            ModelRunnerSettings.ApplyToolDefaults(unsupported);
            if (unsupported.SupportsTools) throw new InvalidOperationException("Unsupported API types should not default to tool support.");
        }

        private static async Task ToolServiceFoundationAsync()
        {
            Settings disabled = new Settings();
            ToolService disabledService = new ToolService(disabled);
            if (disabledService.ListTools(false).Count != 0) throw new InvalidOperationException("Disabled tool service should not expose model tools.");
            List<ToolDescriptor> disabledDiagnostics = disabledService.ListTools(true);
            if (disabledDiagnostics.Count == 0) throw new InvalidOperationException("Disabled tool service should expose diagnostic descriptors.");
            if (disabledDiagnostics.Any(tool => tool.Available)) throw new InvalidOperationException("Disabled tool service should not report available tools.");
            if (!disabledDiagnostics.All(tool => String.Equals(tool.UnavailableReason, "Tools are disabled.", StringComparison.Ordinal))) throw new InvalidOperationException("Disabled tool service should explain the global disabled policy.");
            if (disabledService.GetModelToolDefinitions().Count != 0) throw new InvalidOperationException("Foundation tool service should not expose model tool definitions.");

            Settings enabled = new Settings { Tools = new ToolsSettings { Enabled = true } };
            ToolService enabledService = new ToolService(enabled);
            if (enabledService.ListTools(false).Count != 0) throw new InvalidOperationException("Tool service should not expose file tools without a working directory and allowed root.");
            ToolDescriptor? unavailableRead = enabledService.GetTool("read_file");
            if (unavailableRead == null || unavailableRead.Available || String.IsNullOrWhiteSpace(unavailableRead.UnavailableReason)) throw new InvalidOperationException("Expected read_file to be unavailable until workspace settings are configured.");

            Settings builtInsDisabled = new Settings { Tools = new ToolsSettings { Enabled = true, BuiltInsEnabled = false } };
            ToolService builtInsDisabledService = new ToolService(builtInsDisabled);
            List<ToolDescriptor> builtInsDisabledDiagnostics = builtInsDisabledService.ListTools(true);
            if (builtInsDisabledService.ListTools(false).Count != 0) throw new InvalidOperationException("Built-in disabled policy should not expose model tools.");
            if (!builtInsDisabledDiagnostics.All(tool => String.Equals(tool.UnavailableReason, "Built-in tools are disabled.", StringComparison.Ordinal))) throw new InvalidOperationException("Built-in disabled policy should explain why tools are unavailable.");

            string workspace = Path.Combine(Path.GetTempPath(), "wilson-tools-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try
            {
                string filename = Path.Combine(workspace, "sample.txt");
                await File.WriteAllTextAsync(filename, "hello" + Environment.NewLine + "world").ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(workspace, ".env"), "SECRET=value").ConfigureAwait(false);

                Settings configured = new Settings
                {
                    Tools = new ToolsSettings
                    {
                        Enabled = true,
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace }
                    }
                };
                ToolService configuredService = new ToolService(configured);
                if (!configuredService.ListTools(false).Exists(tool => String.Equals(tool.Name, "read_file", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Expected read_file to be available with workspace settings.");
                if (configuredService.GetModelToolDefinitions().Count == 0) throw new InvalidOperationException("Expected model tool definitions for available tools.");

                Settings disabledByName = new Settings
                {
                    Tools = new ToolsSettings
                    {
                        Enabled = true,
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace },
                        DisabledToolNames = new List<string> { "read_file" }
                    }
                };
                ToolService disabledByNameService = new ToolService(disabledByName);
                ToolDescriptor? disabledRead = disabledByNameService.GetTool("read_file");
                if (disabledRead == null || disabledRead.Available || !String.Equals(disabledRead.UnavailableReason, "Tool is disabled by name.", StringComparison.Ordinal)) throw new InvalidOperationException("Expected disabled-by-name tools to return a diagnostic descriptor.");
                if (disabledByNameService.ListTools(false).Any(tool => String.Equals(tool.Name, "read_file", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Disabled-by-name tools should not be model-visible.");
                if (disabledByNameService.GetModelToolDefinitions().Any(tool => tool.Function != null && String.Equals(tool.Function.Name, "read_file", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Disabled-by-name tools should not have model definitions.");

                Settings enabledSubset = new Settings
                {
                    Tools = new ToolsSettings
                    {
                        Enabled = true,
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace },
                        EnabledToolNames = new List<string> { "read_file" }
                    }
                };
                ToolService enabledSubsetService = new ToolService(enabledSubset);
                List<ToolDescriptor> subsetVisible = enabledSubsetService.ListTools(false);
                if (subsetVisible.Count != 1 || !String.Equals(subsetVisible[0].Name, "read_file", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Enabled tool subset should only expose allow-listed tools.");
                ToolDescriptor? subsetWrite = enabledSubsetService.GetTool("write_file");
                if (subsetWrite == null || subsetWrite.Available || !String.Equals(subsetWrite.UnavailableReason, "Tool is not in the enabled tool list.", StringComparison.Ordinal)) throw new InvalidOperationException("Expected non-allow-listed tools to return a diagnostic descriptor.");

                using JsonDocument readArgs = JsonDocument.Parse("""{"file_path":"sample.txt","offset":1,"limit":1}""");
                ToolResult readResult = await configuredService.ExecuteAsync(
                    Wilson.Core.Helpers.IdGenerator.ToolCall(),
                    "read_file",
                    readArgs.RootElement,
                    new ToolExecutionContext(),
                    CancellationToken.None).ConfigureAwait(false);
                if (!readResult.Success || !readResult.Content.Contains("hello", StringComparison.Ordinal)) throw new InvalidOperationException("Expected read_file to return file content.");

                using JsonDocument secretArgs = JsonDocument.Parse("""{"file_path":".env"}""");
                ToolResult secretResult = await configuredService.ExecuteAsync(
                    Wilson.Core.Helpers.IdGenerator.ToolCall(),
                    "read_file",
                    secretArgs.RootElement,
                    new ToolExecutionContext(),
                    CancellationToken.None).ConfigureAwait(false);
                if (secretResult.Success || !String.Equals(secretResult.ErrorCode, "secret_path_blocked", StringComparison.Ordinal)) throw new InvalidOperationException("Expected secret path guard to block .env reads.");
            }
            finally
            {
                TryDeleteDirectory(workspace);
            }
        }

        private static async Task ToolDiagnosticsApiAsync()
        {
            string workspace = Path.Combine(Path.GetTempPath(), "wilson-tools-api-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            string settingsFile = Path.Combine(workspace, "wilson.json");
            string databaseFile = Path.Combine(workspace, "wilson.db");
            int port = GetFreePort();

            try
            {
                Settings settings = new Settings
                {
                    Rest = new RestSettings { Hostname = "127.0.0.1", Port = port, Ssl = false },
                    Database = new DatabaseSettings { Type = "Sqlite", Filename = databaseFile },
                    Auth = new AuthSettings { AdminBearerTokens = new List<string> { "test-admin-token" } },
                    RequestHistory = new RequestHistorySettings { Enabled = false },
                    Seed = new SeedSettings { AccessKey = "test-user-token", UserEmail = "test-user@example.com" },
                    Tools = new ToolsSettings { Enabled = false },
                    ModelRunners = new List<ModelRunnerSettings>
                    {
                        new ModelRunnerSettings
                        {
                            Id = "tool-runner",
                            Name = "Tool Runner",
                            ApiType = "OpenAI",
                            Endpoint = "http://localhost:9999",
                            Models = new List<string> { "test-model" }
                        },
                        new ModelRunnerSettings
                        {
                            Id = "disabled-runner",
                            Name = "Disabled Runner",
                            ApiType = "OpenAI",
                            Endpoint = "http://localhost:9999",
                            Models = new List<string> { "test-model" },
                            ToolsEnabled = false
                        },
                        new ModelRunnerSettings
                        {
                            Id = "unsupported-runner",
                            Name = "Unsupported Runner",
                            ApiType = "Custom",
                            Endpoint = "http://localhost:9999",
                            Models = new List<string> { "test-model" }
                        }
                    }
                };

                File.WriteAllText(settingsFile, JsonSerializer.Serialize(settings, TestJson()));
                WilsonServer server = await WilsonServer.CreateAsync(new[] { settingsFile }).ConfigureAwait(false);

                using (CancellationTokenSource serverStop = new CancellationTokenSource())
                using (HttpClient adminClient = new HttpClient())
                using (HttpClient userClient = new HttpClient())
                using (HttpClient anonymousClient = new HttpClient())
                {
                    Task serverTask = Task.Run(() => server.Server.StartAsync(serverStop.Token), serverStop.Token);
                    adminClient.BaseAddress = new Uri("http://127.0.0.1:" + port);
                    adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin-token");
                    userClient.BaseAddress = adminClient.BaseAddress;
                    userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-user-token");
                    anonymousClient.BaseAddress = adminClient.BaseAddress;

                    try
                    {
                        await WaitForHttpAsync(adminClient).ConfigureAwait(false);

                        using (JsonDocument catalog = await GetJsonDocumentAsync(userClient, "/v1.0/api/tools").ConfigureAwait(false))
                        {
                            if (catalog.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("Expected tools catalog array.");
                            bool foundReadFile = catalog.RootElement.EnumerateArray().Any(tool => String.Equals(tool.GetProperty("name").GetString(), "read_file", StringComparison.OrdinalIgnoreCase));
                            if (!foundReadFile) throw new InvalidOperationException("Expected tools catalog to include read_file descriptor.");
                        }

                        using (JsonDocument readFileDescriptor = await GetJsonDocumentAsync(userClient, "/v1.0/api/tools/read_file").ConfigureAwait(false))
                        {
                            if (!String.Equals(readFileDescriptor.RootElement.GetProperty("name").GetString(), "read_file", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Expected single tool descriptor for read_file.");
                            if (readFileDescriptor.RootElement.GetProperty("available").GetBoolean()) throw new InvalidOperationException("Expected read_file to be unavailable while global tools are disabled.");
                        }

                        ToolPolicyValidationResult disabled = await PostJsonAsync<ToolPolicyValidationResult>(
                            adminClient,
                            "/v1.0/api/tools/validate",
                            new ToolPolicyValidationRequest { Tools = new ToolsSettings { Enabled = false } }).ConfigureAwait(false);
                        if (!disabled.Success || disabled.ToolsEnabled || !disabled.Warnings.Any(warning => warning.Contains("disabled globally", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Expected disabled tools validation warning without blocking errors.");

                        ToolsSettings validTools = new ToolsSettings
                        {
                            Enabled = true,
                            DefaultApprovalPolicy = ToolApprovalPolicies.Auto,
                            WorkingDirectory = workspace,
                            AllowedRoots = new List<string> { workspace }
                        };
                        ToolPolicyValidationResult valid = await PostJsonAsync<ToolPolicyValidationResult>(
                            adminClient,
                            "/v1.0/api/tools/validate",
                            new ToolPolicyValidationRequest { Tools = validTools }).ConfigureAwait(false);
                        if (!valid.Success || valid.AvailableToolCount < 1 || !valid.Tools.Any(tool => String.Equals(tool.Name, "read_file", StringComparison.OrdinalIgnoreCase) && tool.Available)) throw new InvalidOperationException("Expected valid tool policy to expose read_file.");

                        ToolPolicyValidationResult missingRoots = await PostJsonAsync<ToolPolicyValidationResult>(
                            adminClient,
                            "/v1.0/api/tools/validate",
                            new ToolPolicyValidationRequest { Tools = new ToolsSettings { Enabled = true, WorkingDirectory = workspace } }).ConfigureAwait(false);
                        if (missingRoots.Success || !missingRoots.Errors.Any(error => error.Contains("allowed root", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Expected missing allowed roots to fail validation.");

                        ToolPolicyValidationResult unknownTool = await PostJsonAsync<ToolPolicyValidationResult>(
                            adminClient,
                            "/v1.0/api/tools/validate",
                            new ToolPolicyValidationRequest
                            {
                                Tools = new ToolsSettings
                                {
                                    Enabled = true,
                                    DefaultApprovalPolicy = ToolApprovalPolicies.Auto,
                                    WorkingDirectory = workspace,
                                    AllowedRoots = new List<string> { workspace },
                                    EnabledToolNames = new List<string> { "missing_tool" }
                                }
                            }).ConfigureAwait(false);
                        if (unknownTool.Success || !unknownTool.Errors.Any(error => error.Contains("missing_tool", StringComparison.Ordinal))) throw new InvalidOperationException("Expected unknown enabled tool name to fail validation.");

                        ToolPolicyTestResult ready = await PostJsonAsync<ToolPolicyTestResult>(
                            adminClient,
                            "/v1.0/api/tools/test",
                            new ToolPolicyTestRequest { Tools = validTools, RunnerId = "tool-runner" }).ConfigureAwait(false);
                        if (!ready.Success || !ready.RunnerFound || !ready.RunnerSupportsTools || !String.Equals(ready.ToolCallingApiFormat, "OpenAIChatCompletions", StringComparison.Ordinal)) throw new InvalidOperationException("Expected tool-runner readiness to pass.");

                        ToolPolicyTestResult disabledRunner = await PostJsonAsync<ToolPolicyTestResult>(
                            adminClient,
                            "/v1.0/api/tools/test",
                            new ToolPolicyTestRequest { Tools = validTools, RunnerId = "disabled-runner" }).ConfigureAwait(false);
                        if (disabledRunner.Success || !disabledRunner.Errors.Any(error => error.Contains("tools disabled", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Expected disabled runner readiness to fail.");

                        ToolPolicyTestResult unsupportedRunner = await PostJsonAsync<ToolPolicyTestResult>(
                            adminClient,
                            "/v1.0/api/tools/test",
                            new ToolPolicyTestRequest { Tools = validTools, RunnerId = "unsupported-runner" }).ConfigureAwait(false);
                        if (unsupportedRunner.Success || unsupportedRunner.RunnerSupportsTools) throw new InvalidOperationException("Expected unsupported runner readiness to fail.");

                        ToolPolicyTestResult missingRunner = await PostJsonAsync<ToolPolicyTestResult>(
                            adminClient,
                            "/v1.0/api/tools/test",
                            new ToolPolicyTestRequest { Tools = validTools, RunnerId = "missing-runner" }).ConfigureAwait(false);
                        if (missingRunner.Success || missingRunner.RunnerFound || !missingRunner.Errors.Any(error => error.Contains("not found", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Expected missing runner readiness to fail.");

                        await SeedAndVerifyToolCallApiAuthorizationAsync(server, userClient, adminClient, anonymousClient).ConfigureAwait(false);

                        await ExpectStatusAsync(
                            anonymousClient,
                            "/v1.0/api/tools/validate",
                            new ToolPolicyValidationRequest { Tools = validTools },
                            HttpStatusCode.Unauthorized).ConfigureAwait(false);

                        JsonDocument openApi = await GetJsonDocumentAsync(adminClient, "/openapi.json").ConfigureAwait(false);
                        try
                        {
                            JsonElement root = openApi.RootElement;
                            if (!root.GetProperty("paths").TryGetProperty("/v1.0/api/tools/validate", out JsonElement _)) throw new InvalidOperationException("Expected OpenAPI tools validate path.");
                            if (!root.GetProperty("paths").TryGetProperty("/v1.0/api/tools/test", out JsonElement _)) throw new InvalidOperationException("Expected OpenAPI tools test path.");
                            JsonElement schemas = root.GetProperty("components").GetProperty("schemas");
                            if (!schemas.TryGetProperty("ToolPolicyValidationRequest", out JsonElement _)) throw new InvalidOperationException("Expected OpenAPI validation request schema.");
                            if (!schemas.TryGetProperty("ToolPolicyValidationResult", out JsonElement _)) throw new InvalidOperationException("Expected OpenAPI validation result schema.");
                            if (!schemas.TryGetProperty("ToolPolicyTestRequest", out JsonElement _)) throw new InvalidOperationException("Expected OpenAPI test request schema.");
                            if (!schemas.TryGetProperty("ToolPolicyTestResult", out JsonElement _)) throw new InvalidOperationException("Expected OpenAPI test result schema.");
                        }
                        finally
                        {
                            openApi.Dispose();
                        }
                    }
                    finally
                    {
                        serverStop.Cancel();
                        server.Server.Stop();
                        try
                        {
                            await serverTask.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }
                }
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (Directory.Exists(workspace)) Directory.Delete(workspace, true);
            }
        }

        private static async Task PublicChatToolTraceApiAsync()
        {
            string workspace = Path.Combine(Path.GetTempPath(), "wilson-chat-tools-api-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            string settingsFile = Path.Combine(workspace, "wilson.json");
            string databaseFile = Path.Combine(workspace, "wilson.db");
            string toolOutputSecret = "result-secret-should-not-be-public";
            await File.WriteAllTextAsync(Path.Combine(workspace, "sample.txt"), toolOutputSecret).ConfigureAwait(false);
            int serverPort = GetFreePort();
            int modelPort = GetFreePort();

            try
            {
                Settings settings = new Settings
                {
                    Rest = new RestSettings { Hostname = "127.0.0.1", Port = serverPort, Ssl = false },
                    Database = new DatabaseSettings { Type = "Sqlite", Filename = databaseFile },
                    Auth = new AuthSettings { AdminBearerTokens = new List<string> { "test-admin-token" } },
                    RequestHistory = new RequestHistorySettings { Enabled = false },
                    Seed = new SeedSettings { AccessKey = "test-user-token", UserEmail = "trace-user@example.com" },
                    Tools = new ToolsSettings
                    {
                        Enabled = true,
                        DefaultApprovalPolicy = ToolApprovalPolicies.Auto,
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace }
                    },
                    ModelRunners = new List<ModelRunnerSettings>
                    {
                        new ModelRunnerSettings
                        {
                            Id = "tool-runner",
                            Name = "Tool Runner",
                            ApiType = "OpenAI",
                            Endpoint = "http://127.0.0.1:" + modelPort,
                            Models = new List<string> { "test-model" },
                            HealthCheckEnabled = false
                        }
                    }
                };

                File.WriteAllText(settingsFile, JsonSerializer.Serialize(settings, TestJson()));
                WilsonServer server = await WilsonServer.CreateAsync(new[] { settingsFile }).ConfigureAwait(false);

                using (CancellationTokenSource fakeModelStop = new CancellationTokenSource())
                using (CancellationTokenSource serverStop = new CancellationTokenSource())
                using (HttpClient userClient = new HttpClient())
                {
                    Task fakeModelTask = RunFakeOpenAiToolServerAsync(modelPort, toolOutputSecret, fakeModelStop.Token);
                    Task serverTask = Task.Run(() => server.Server.StartAsync(serverStop.Token), serverStop.Token);
                    userClient.BaseAddress = new Uri("http://127.0.0.1:" + serverPort);
                    userClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-user-token");

                    try
                    {
                        await WaitForHttpAsync(userClient).ConfigureAwait(false);
                        string requestJson = JsonSerializer.Serialize(new
                        {
                            runnerId = "tool-runner",
                            model = "test-model",
                            prompt = "Read sample.txt",
                            toolsEnabled = true,
                            approvalPolicy = ToolApprovalPolicies.Auto,
                            settings = new CompletionRequestSettings { MaxTokens = 128, Temperature = 0, TopP = 1 }
                        }, TestJson());
                        using (StringContent content = new StringContent(requestJson, Encoding.UTF8, "application/json"))
                        using (HttpResponseMessage response = await userClient.PostAsync("/v1.0/api/chat", content).ConfigureAwait(false))
                        {
                            string payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Expected tool chat success but received " + (int)response.StatusCode + ": " + payload);
                            AssertPublicToolTracePayload(payload, toolOutputSecret);
                        }

                        await fakeModelTask.ConfigureAwait(false);
                    }
                    finally
                    {
                        fakeModelStop.Cancel();
                        serverStop.Cancel();
                        server.Server.Stop();
                        try
                        {
                            await serverTask.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }
                }
            }
            finally
            {
                TryDeleteDirectory(workspace);
            }
        }

        private static void AssertPublicToolTracePayload(string payload, string toolOutputSecret)
        {
            if (payload.Contains(toolOutputSecret, StringComparison.Ordinal)) throw new InvalidOperationException("Public chat response leaked raw tool output.");
            if (payload.Contains("provider-secret-call-id", StringComparison.Ordinal)) throw new InvalidOperationException("Public chat response leaked provider tool-call ID.");
            if (payload.Contains("argumentsJson", StringComparison.Ordinal) || payload.Contains("resultJson", StringComparison.Ordinal) || payload.Contains("providerToolCallId", StringComparison.Ordinal) || payload.Contains("approvedByUserId", StringComparison.Ordinal))
                throw new InvalidOperationException("Public chat response leaked audit-only tool fields.");

            using JsonDocument document = JsonDocument.Parse(payload);
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("toolCalls", out JsonElement toolCalls) || toolCalls.ValueKind != JsonValueKind.Array || toolCalls.GetArrayLength() != 1)
                throw new InvalidOperationException("Expected one public tool trace in chat response.");
            JsonElement trace = toolCalls[0];
            string[] forbidden = new[] { "argumentsJson", "resultJson", "resultPreview", "providerToolCallId", "provider", "model", "approvalPolicy", "approvedByUserId" };
            foreach (string property in forbidden)
            {
                if (trace.TryGetProperty(property, out JsonElement _)) throw new InvalidOperationException("Public tool trace exposed audit-only field: " + property + ".");
            }

            if (!trace.TryGetProperty("toolCallId", out JsonElement toolCallId) || !toolCallId.GetString()!.StartsWith("tcl_", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected public tool trace to use a Wilson-generated tool call ID.");
            if (!trace.TryGetProperty("toolName", out JsonElement toolName) || !String.Equals(toolName.GetString(), "read_file", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected read_file public trace.");
            if (!trace.TryGetProperty("summary", out JsonElement summary) || !String.Equals(summary.GetString(), "Completed.", StringComparison.Ordinal))
                throw new InvalidOperationException("Expected safe public tool summary.");
        }

        private static async Task RunFakeOpenAiToolServerAsync(int port, string expectedToolOutput, CancellationToken token)
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            try
            {
                for (int requestIndex = 1; requestIndex <= 2; requestIndex++)
                {
                    using TcpClient client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                    using NetworkStream stream = client.GetStream();
                    string request = await ReadHttpRequestAsync(stream, token).ConfigureAwait(false);
                    string body;
                    if (requestIndex == 1)
                    {
                        if (!request.Contains("\"tools\"", StringComparison.Ordinal) || !request.Contains("read_file", StringComparison.Ordinal) || !request.Contains("\"tool_choice\"", StringComparison.Ordinal))
                            throw new InvalidOperationException("Expected first fake model request to include tool definitions and tool choice.");
                        body = """{"choices":[{"message":{"role":"assistant","content":null,"tool_calls":[{"id":"provider-secret-call-id","type":"function","function":{"name":"read_file","arguments":"{\"file_path\":\"sample.txt\"}"}}]},"finish_reason":"tool_calls"}]}""";
                    }
                    else
                    {
                        if (!request.Contains("\"role\":\"assistant\"", StringComparison.Ordinal) || !request.Contains("\"tool_calls\"", StringComparison.Ordinal) || !request.Contains("\"role\":\"tool\"", StringComparison.Ordinal) || !request.Contains(expectedToolOutput, StringComparison.Ordinal))
                            throw new InvalidOperationException("Expected second fake model request to include assistant tool_calls and matching tool result.");
                        body = """{"choices":[{"message":{"role":"assistant","content":"safe final answer"},"finish_reason":"stop"}]}""";
                    }

                    await WriteHttpResponseAsync(stream, body, token).ConfigureAwait(false);
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task<string> ReadHttpRequestAsync(NetworkStream stream, CancellationToken token)
        {
            byte[] buffer = new byte[4096];
            using MemoryStream memory = new MemoryStream();
            int contentLength = 0;
            while (true)
            {
                int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token).ConfigureAwait(false);
                if (read <= 0) break;
                memory.Write(buffer, 0, read);
                string text = Encoding.UTF8.GetString(memory.ToArray());
                int headerEnd = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (headerEnd < 0) continue;
                if (contentLength == 0)
                {
                    foreach (string line in text.Substring(0, headerEnd).Split(new[] { "\r\n" }, StringSplitOptions.None))
                    {
                        int separator = line.IndexOf(':');
                        if (separator > 0 && String.Equals(line.Substring(0, separator), "Content-Length", StringComparison.OrdinalIgnoreCase))
                            contentLength = Int32.Parse(line.Substring(separator + 1).Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    }
                }

                int bodyBytes = memory.ToArray().Length - Encoding.UTF8.GetByteCount(text.Substring(0, headerEnd + 4));
                if (bodyBytes >= contentLength) return text;
            }

            return Encoding.UTF8.GetString(memory.ToArray());
        }

        private static async Task WriteHttpResponseAsync(NetworkStream stream, string body, CancellationToken token)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string headers = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + bodyBytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r\nConnection: close\r\n\r\n";
            byte[] headerBytes = Encoding.UTF8.GetBytes(headers);
            await stream.WriteAsync(headerBytes.AsMemory(0, headerBytes.Length), token).ConfigureAwait(false);
            await stream.WriteAsync(bodyBytes.AsMemory(0, bodyBytes.Length), token).ConfigureAwait(false);
        }

        private static async Task SeedAndVerifyToolCallApiAuthorizationAsync(WilsonServer server, HttpClient userClient, HttpClient adminClient, HttpClient anonymousClient)
        {
            Tenant tenant = (await server.Database.GetTenantsAsync().ConfigureAwait(false)).First();
            User user = (await server.Database.GetUsersAsync(tenant.Id).ConfigureAwait(false)).First(item => String.Equals(item.Email, "test-user@example.com", StringComparison.OrdinalIgnoreCase));
            Conversation conversation = new Conversation
            {
                Id = "conversation-tool-api",
                TenantId = tenant.Id,
                UserId = user.Id,
                Title = "Tool API auth",
                RunnerId = "tool-runner",
                Model = "test-model"
            };
            await server.Database.CreateConversationAsync(conversation).ConfigureAwait(false);

            ToolRun run = new ToolRun
            {
                RunId = "toolrun-api-auth",
                TenantId = tenant.Id,
                UserId = user.Id,
                ConversationId = conversation.Id,
                RunnerId = "tool-runner",
                Model = "test-model",
                Status = ToolStatuses.Completed,
                StartedUtc = DateTime.UtcNow.AddMilliseconds(-20),
                CompletedUtc = DateTime.UtcNow,
                ToolCallCount = 1,
                IterationCount = 1
            };
            await server.Database.CreateToolRunAsync(run).ConfigureAwait(false);

            ToolExecutionRecord record = new ToolExecutionRecord
            {
                TenantId = tenant.Id,
                UserId = user.Id,
                ConversationId = conversation.Id,
                RunId = run.RunId,
                TraceId = run.RunId + ":1",
                ToolCallId = "toolcall-api-auth",
                ToolName = "read_file",
                Iteration = 1,
                SequenceNumber = 1,
                Status = ToolStatuses.Completed,
                ApprovalPolicy = ToolApprovalPolicies.Auto,
                ArgumentsJson = "{}",
                ResultJson = """{"summary":"Completed."}""",
                ResultSummaryJson = """{"success":true,"summary":"Completed."}""",
                ResultPreview = "Completed.",
                Success = true,
                StartedUtc = run.StartedUtc,
                CompletedUtc = run.CompletedUtc,
                CreatedUtc = run.StartedUtc,
                UpdatedUtc = run.CompletedUtc ?? DateTime.UtcNow
            };
            await server.Database.CreateToolCallAsync(record).ConfigureAwait(false);

            RequestHistoryEntry history = new RequestHistoryEntry
            {
                Id = "request-tool-api-auth",
                TenantId = tenant.Id,
                UserId = user.Id,
                CreatedUtc = DateTime.UtcNow,
                Method = "POST",
                Path = "/v1.0/api/chat",
                StatusCode = 200,
                ToolRunId = run.RunId,
                ToolCallCount = 1,
                ToolElapsedMs = 12,
                AgentIterations = 1
            };
            await server.Database.CreateRequestHistoryAsync(history).ConfigureAwait(false);
            await server.Database.AttachToolCallsToRequestHistoryByRunIdAsync(tenant.Id, run.RunId, history.Id).ConfigureAwait(false);

            using (JsonDocument conversationCalls = await GetJsonDocumentAsync(userClient, "/v1.0/api/conversations/" + conversation.Id + "/tool-calls").ConfigureAwait(false))
            {
                AssertEnumerationCount(conversationCalls, 1, "conversation tool calls");
            }

            await ExpectGetStatusAsync(anonymousClient, "/v1.0/api/conversations/" + conversation.Id + "/tool-calls", HttpStatusCode.Unauthorized).ConfigureAwait(false);

            using (JsonDocument requestHistoryCalls = await GetJsonDocumentAsync(userClient, "/v1.0/api/request-history/" + history.Id + "/tool-calls").ConfigureAwait(false))
            {
                AssertEnumerationCount(requestHistoryCalls, 1, "request-history tool calls for tenant admin");
            }

            using (JsonDocument adminRequestHistoryCalls = await GetJsonDocumentAsync(adminClient, "/v1.0/api/request-history/" + history.Id + "/tool-calls?tenantId=" + tenant.Id).ConfigureAwait(false))
            {
                AssertEnumerationCount(adminRequestHistoryCalls, 1, "request-history tool calls for global admin");
            }

            await ExpectGetStatusAsync(anonymousClient, "/v1.0/api/request-history/" + history.Id + "/tool-calls", HttpStatusCode.Unauthorized).ConfigureAwait(false);
        }

        private static async Task WorkingDirectoryGuardAsync()
        {
            string workspace = Path.Combine(Path.GetTempPath(), "wilson-guard-" + Guid.NewGuid().ToString("N"));
            string outside = Path.Combine(Path.GetTempPath(), "wilson-guard-outside-" + Guid.NewGuid().ToString("N"));
            string linkPath = Path.Combine(workspace, "linked-outside");
            Directory.CreateDirectory(workspace);
            Directory.CreateDirectory(outside);
            try
            {
                string nested = Path.Combine(workspace, "nested");
                Directory.CreateDirectory(nested);
                string insideFile = Path.Combine(nested, "inside.txt");
                await File.WriteAllTextAsync(insideFile, "inside").ConfigureAwait(false);
                string outsideFile = Path.Combine(outside, "outside.txt");
                await File.WriteAllTextAsync(outsideFile, "outside").ConfigureAwait(false);
                ToolExecutionContext context = GuardContext(workspace, true);

                string relative = WorkingDirectoryGuard.ResolvePath(Path.Combine("nested", "inside.txt"), context);
                if (!String.Equals(Path.GetFullPath(insideFile), relative, PathComparison())) throw new InvalidOperationException("Expected relative path inside root to resolve.");

                string absolute = WorkingDirectoryGuard.ResolvePath(insideFile, context);
                if (!String.Equals(Path.GetFullPath(insideFile), absolute, PathComparison())) throw new InvalidOperationException("Expected absolute path inside root to resolve.");

                ExpectToolExecutionException("path_outside_allowed_roots", () => WorkingDirectoryGuard.ResolvePath(Path.Combine("..", Path.GetFileName(outside), "outside.txt"), context));
                ExpectToolExecutionException("path_outside_allowed_roots", () => WorkingDirectoryGuard.ResolvePath(outsideFile, context));
                ExpectToolExecutionException("missing_allowed_roots", () => WorkingDirectoryGuard.ResolvePath("nested", new ToolExecutionContext { Settings = new Settings { Tools = new ToolsSettings { WorkingDirectory = workspace } } }));
                ExpectToolExecutionException("working_directory_outside_allowed_roots", () => WorkingDirectoryGuard.ResolvePath("nested", new ToolExecutionContext { Settings = new Settings { Tools = new ToolsSettings { WorkingDirectory = outside, AllowedRoots = new List<string> { workspace } } } }));

                await File.WriteAllTextAsync(Path.Combine(workspace, ".env"), "SECRET=value").ConfigureAwait(false);
                ExpectToolExecutionException("secret_path_blocked", () => WorkingDirectoryGuard.ResolvePath(".env", context));
                string secretAllowed = WorkingDirectoryGuard.ResolvePath(".env", GuardContext(workspace, false));
                if (!String.Equals(Path.GetFullPath(Path.Combine(workspace, ".env")), secretAllowed, PathComparison())) throw new InvalidOperationException("Expected secret path blocking to be configurable.");

                bool linked = TryCreateDirectoryLink(linkPath, outside);
                if (!linked) throw new InvalidOperationException("Expected test environment to support directory symlink creation for WorkingDirectoryGuard coverage.");
                ExpectToolExecutionException("path_outside_allowed_roots", () => WorkingDirectoryGuard.ResolvePath(Path.Combine("linked-outside", "outside.txt"), context));
                ExpectToolExecutionException("path_outside_allowed_roots", () => WorkingDirectoryGuard.ResolvePath(Path.Combine("linked-outside", "new.txt"), context));
            }
            finally
            {
                TryDeleteDirectoryLink(linkPath);
                TryDeleteDirectory(workspace);
                TryDeleteDirectory(outside);
            }
        }

        private static async Task ToolArgumentValidationAndOutputLimiterAsync()
        {
            string workspace = Path.Combine(Path.GetTempPath(), "wilson-tool-validation-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try
            {
                string bigFile = Path.Combine(workspace, "big.txt");
                await File.WriteAllTextAsync(bigFile, new String('x', 2000)).ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(workspace, "small.txt"), "small").ConfigureAwait(false);
                Settings settings = new Settings
                {
                    Tools = new ToolsSettings
                    {
                        Enabled = true,
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace },
                        MaxToolOutputChars = 1024
                    }
                };
                ToolService service = new ToolService(settings);

                ToolResult nonObject = await ExecuteToolAsync(service, "read_file", "[]").ConfigureAwait(false);
                if (nonObject.Success || !String.Equals(nonObject.ErrorCode, "invalid_arguments", StringComparison.Ordinal)) throw new InvalidOperationException("Expected non-object arguments to be rejected.");

                ToolResult unknown = await ExecuteToolAsync(service, "read_file", """{"file_path":"small.txt","unexpected":true}""").ConfigureAwait(false);
                if (unknown.Success || !String.Equals(unknown.ErrorCode, "invalid_arguments", StringComparison.Ordinal)) throw new InvalidOperationException("Expected unknown tool arguments to be rejected.");

                ToolResult missing = await ExecuteToolAsync(service, "read_file", """{"offset":1}""").ConfigureAwait(false);
                if (missing.Success || !String.Equals(missing.ErrorCode, "invalid_arguments", StringComparison.Ordinal)) throw new InvalidOperationException("Expected missing required arguments to be rejected.");

                ToolResult malformedNumber = await ExecuteToolAsync(service, "read_file", """{"file_path":"small.txt","offset":"not-a-number"}""").ConfigureAwait(false);
                if (malformedNumber.Success || !String.Equals(malformedNumber.ErrorCode, "invalid_arguments", StringComparison.Ordinal)) throw new InvalidOperationException("Expected malformed numeric arguments to be rejected.");

                ToolResult numericString = await ExecuteToolAsync(service, "read_file", """{"file_path":"small.txt","offset":"1","limit":"1"}""").ConfigureAwait(false);
                if (!numericString.Success || !numericString.Content.Contains("small", StringComparison.Ordinal)) throw new InvalidOperationException("Expected read_file to accept explicit numeric strings for offset and limit.");

                ToolResult malformedList = await ExecuteToolAsync(service, "run_process", """{"command":"cmd.exe","args":"not-an-array"}""").ConfigureAwait(false);
                if (malformedList.Success || !String.Equals(malformedList.ErrorCode, "invalid_arguments", StringComparison.Ordinal)) throw new InvalidOperationException("Expected malformed list arguments to be rejected.");

                ToolResult truncated = await ExecuteToolAsync(service, "read_file", """{"file_path":"big.txt"}""").ConfigureAwait(false);
                if (!truncated.Success || !truncated.Truncated || !truncated.Content.Contains("[truncated]", StringComparison.Ordinal)) throw new InvalidOperationException("Expected per-call output truncation.");
                using JsonDocument contentJson = JsonDocument.Parse(truncated.ContentJson);
                if (!contentJson.RootElement.TryGetProperty("truncated", out JsonElement truncatedElement) || !truncatedElement.GetBoolean()) throw new InvalidOperationException("Expected per-call truncation JSON flag.");
                if (!contentJson.RootElement.TryGetProperty("content", out JsonElement contentElement) || String.IsNullOrWhiteSpace(contentElement.GetString())) throw new InvalidOperationException("Expected per-call truncation JSON content.");
            }
            finally
            {
                Directory.Delete(workspace, true);
            }
        }

        private static async Task FilesystemDiscoveryToolsAsync()
        {
            string workspace = Path.Combine(Path.GetTempPath(), "wilson-tools-discovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try
            {
                Directory.CreateDirectory(Path.Combine(workspace, "alpha"));
                Directory.CreateDirectory(Path.Combine(workspace, "zeta"));
                await File.WriteAllTextAsync(Path.Combine(workspace, "alpha", "notes.txt"), "alpha hit" + Environment.NewLine + "beta miss").ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(workspace, "alpha", "code.cs"), "public class Sample { }" + Environment.NewLine + "alpha hit").ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(workspace, "root.txt"), "root alpha").ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(workspace, "z-last.txt"), "last").ConfigureAwait(false);

                Settings settings = new Settings
                {
                    Tools = new ToolsSettings
                    {
                        Enabled = true,
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace },
                        MaxToolResultItems = 2
                    }
                };
                ToolService service = new ToolService(settings);

                ToolResult fileMetadata = await ExecuteToolAsync(service, "file_metadata", """{"path":"alpha/notes.txt"}""").ConfigureAwait(false);
                if (!fileMetadata.Success || !fileMetadata.Content.Contains("\"type\":\"file\"", StringComparison.Ordinal) || !fileMetadata.Content.Contains("\"size_bytes\"", StringComparison.Ordinal)) throw new InvalidOperationException("Expected file_metadata to return file metadata.");

                ToolResult directoryMetadata = await ExecuteToolAsync(service, "file_metadata", """{"path":"alpha"}""").ConfigureAwait(false);
                if (!directoryMetadata.Success || !directoryMetadata.Content.Contains("\"type\":\"directory\"", StringComparison.Ordinal) || !directoryMetadata.Content.Contains("\"file_count\":2", StringComparison.Ordinal)) throw new InvalidOperationException("Expected file_metadata to return directory metadata.");

                ToolResult missingMetadata = await ExecuteToolAsync(service, "file_metadata", """{"path":"missing.txt"}""").ConfigureAwait(false);
                if (missingMetadata.Success || !String.Equals(missingMetadata.ErrorCode, "not_found", StringComparison.Ordinal)) throw new InvalidOperationException("Expected file_metadata missing path error.");

                ToolResult list = await ExecuteToolAsync(service, "list_directory", """{"path":".","max_entries":3}""").ConfigureAwait(false);
                if (!list.Success || !list.Truncated) throw new InvalidOperationException("Expected list_directory max_entries truncation.");
                int alphaIndex = list.Content.IndexOf("[DIR]  alpha", StringComparison.Ordinal);
                int zetaIndex = list.Content.IndexOf("[DIR]  zeta", StringComparison.Ordinal);
                int rootIndex = list.Content.IndexOf("[FILE] root.txt", StringComparison.Ordinal);
                if (alphaIndex < 0 || zetaIndex < 0 || rootIndex < 0 || !(alphaIndex < zetaIndex && zetaIndex < rootIndex)) throw new InvalidOperationException("Expected list_directory to sort directories first, then files.");

                ToolResult glob = await ExecuteToolAsync(service, "glob", """{"pattern":"**/*.txt","max_results":2}""").ConfigureAwait(false);
                if (!glob.Success || !glob.Truncated || !glob.Content.Contains("Found 2 matching file(s):", StringComparison.Ordinal)) throw new InvalidOperationException("Expected glob to return matching text files and mark max result truncation.");

                ToolResult nestedGlob = await ExecuteToolAsync(service, "glob", """{"pattern":"alpha/*.cs","max_results":10}""").ConfigureAwait(false);
                if (!nestedGlob.Success || nestedGlob.Truncated || !nestedGlob.Content.Contains("alpha/code.cs", StringComparison.Ordinal)) throw new InvalidOperationException("Expected glob to return a specific nested match.");

                ToolResult invalidGrep = await ExecuteToolAsync(service, "grep", """{"pattern":"[","include":"*.txt"}""").ConfigureAwait(false);
                if (invalidGrep.Success || !String.Equals(invalidGrep.ErrorCode, "invalid_regex", StringComparison.Ordinal)) throw new InvalidOperationException("Expected grep invalid regex error.");

                ToolResult grep = await ExecuteToolAsync(service, "grep", """{"pattern":"alpha","path":"alpha","include":"*.txt","max_matches":1}""").ConfigureAwait(false);
                if (!grep.Success || !grep.Truncated || !grep.Content.Contains("notes.txt:1: alpha hit", StringComparison.Ordinal)) throw new InvalidOperationException("Expected grep match output and max-match truncation.");

                ToolResult grepNone = await ExecuteToolAsync(service, "grep", """{"pattern":"does-not-exist","include":"*.txt"}""").ConfigureAwait(false);
                if (!grepNone.Success || !grepNone.Content.Contains("No matches found.", StringComparison.Ordinal)) throw new InvalidOperationException("Expected grep no-match message.");
            }
            finally
            {
                Directory.Delete(workspace, true);
            }
        }

        private static async Task FilesystemMutationToolsAsync()
        {
            string workspace = Path.Combine(Path.GetTempPath(), "wilson-tools-write-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try
            {
                Settings configured = new Settings
                {
                    Tools = new ToolsSettings
                    {
                        Enabled = true,
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace }
                    }
                };
                ToolService service = new ToolService(configured);
                ToolDescriptor? writeDescriptor = service.GetTool("write_file");
                if (writeDescriptor == null || !writeDescriptor.Available || !writeDescriptor.Dangerous || !writeDescriptor.RequiresApproval) throw new InvalidOperationException("Expected write_file to be available and approval-required.");

                ToolResult writeResult = await ExecuteToolAsync(service, "write_file", """{"file_path":"nested/sample.txt","content":"one\ntwo\n"}""").ConfigureAwait(false);
                if (!writeResult.Success || !File.Exists(Path.Combine(workspace, "nested", "sample.txt"))) throw new InvalidOperationException("Expected write_file to create the file.");

                await File.WriteAllTextAsync(Path.Combine(workspace, "nested", "line-endings.txt"), "alpha\r\nbeta\r\n").ConfigureAwait(false);
                ToolResult overwriteResult = await ExecuteToolAsync(service, "write_file", """{"file_path":"nested/line-endings.txt","content":"gamma\ndelta\n"}""").ConfigureAwait(false);
                if (!overwriteResult.Success) throw new InvalidOperationException("Expected write_file overwrite to succeed.");
                string overwritten = await File.ReadAllTextAsync(Path.Combine(workspace, "nested", "line-endings.txt")).ConfigureAwait(false);
                if (!overwritten.Contains("gamma\r\ndelta\r\n", StringComparison.Ordinal)) throw new InvalidOperationException("Expected write_file to preserve CRLF line endings.");

                ToolResult editResult = await ExecuteToolAsync(service, "edit_file", """{"file_path":"nested/sample.txt","old_string":"two","new_string":"deux"}""").ConfigureAwait(false);
                if (!editResult.Success) throw new InvalidOperationException("Expected edit_file to replace exactly one match.");
                string edited = await File.ReadAllTextAsync(Path.Combine(workspace, "nested", "sample.txt")).ConfigureAwait(false);
                if (!edited.Contains("deux", StringComparison.Ordinal)) throw new InvalidOperationException("Expected edit_file replacement content.");

                ToolResult multiEditResult = await ExecuteToolAsync(service, "multi_edit", """{"file_path":"nested/sample.txt","edits":[{"old_string":"one","new_string":"uno"},{"old_string":"deux","new_string":"dos"}]}""").ConfigureAwait(false);
                if (!multiEditResult.Success) throw new InvalidOperationException("Expected multi_edit to apply sequential replacements.");
                string multiEdited = await File.ReadAllTextAsync(Path.Combine(workspace, "nested", "sample.txt")).ConfigureAwait(false);
                if (!multiEdited.Contains("uno", StringComparison.Ordinal) || !multiEdited.Contains("dos", StringComparison.Ordinal)) throw new InvalidOperationException("Expected multi_edit replacement content.");

                await File.WriteAllTextAsync(Path.Combine(workspace, "ambiguous.txt"), "same same").ConfigureAwait(false);
                ToolResult ambiguous = await ExecuteToolAsync(service, "edit_file", """{"file_path":"ambiguous.txt","old_string":"same","new_string":"other"}""").ConfigureAwait(false);
                if (ambiguous.Success || !String.Equals(ambiguous.ErrorCode, "multiple_matches", StringComparison.Ordinal)) throw new InvalidOperationException("Expected edit_file to reject multiple matches.");

                ToolResult secretWrite = await ExecuteToolAsync(service, "write_file", """{"file_path":".env","content":"SECRET=value"}""").ConfigureAwait(false);
                if (secretWrite.Success || !String.Equals(secretWrite.ErrorCode, "secret_path_blocked", StringComparison.Ordinal)) throw new InvalidOperationException("Expected write_file to respect secret path blocking.");

                ToolResult createDirectory = await ExecuteToolAsync(service, "manage_directory", """{"action":"create","path":"workdir"}""").ConfigureAwait(false);
                if (!createDirectory.Success || !Directory.Exists(Path.Combine(workspace, "workdir"))) throw new InvalidOperationException("Expected manage_directory create to succeed.");

                ToolResult renameDirectory = await ExecuteToolAsync(service, "manage_directory", """{"action":"rename","path":"workdir","new_path":"renamed"}""").ConfigureAwait(false);
                if (!renameDirectory.Success || !Directory.Exists(Path.Combine(workspace, "renamed"))) throw new InvalidOperationException("Expected manage_directory rename to succeed.");

                ToolResult deleteDirectory = await ExecuteToolAsync(service, "manage_directory", """{"action":"delete","path":"renamed"}""").ConfigureAwait(false);
                if (!deleteDirectory.Success || Directory.Exists(Path.Combine(workspace, "renamed"))) throw new InvalidOperationException("Expected manage_directory delete to succeed.");

                ToolResult deleteFile = await ExecuteToolAsync(service, "delete_file", """{"file_path":"nested/sample.txt"}""").ConfigureAwait(false);
                if (!deleteFile.Success || File.Exists(Path.Combine(workspace, "nested", "sample.txt"))) throw new InvalidOperationException("Expected delete_file to remove the file.");
            }
            finally
            {
                Directory.Delete(workspace, true);
            }
        }

        private static async Task<ToolResult> ExecuteToolAsync(ToolService service, string name, string json)
        {
            return await ExecuteToolAsync(service, name, json, CancellationToken.None).ConfigureAwait(false);
        }

        private static async Task<ToolResult> ExecuteToolAsync(ToolService service, string name, string json, CancellationToken token)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return await service.ExecuteAsync(
                Wilson.Core.Helpers.IdGenerator.ToolCall(),
                name,
                document.RootElement,
                new ToolExecutionContext(),
                token).ConfigureAwait(false);
        }

        private static ToolExecutionContext GuardContext(string workspace, bool blockSecretPaths)
        {
            return new ToolExecutionContext
            {
                Settings = new Settings
                {
                    Tools = new ToolsSettings
                    {
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace },
                        BlockSecretPaths = blockSecretPaths
                    }
                }
            };
        }

        private static StringComparison PathComparison()
        {
            return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        }

        private static void ExpectToolExecutionException(string code, Action action)
        {
            try
            {
                action();
            }
            catch (ToolExecutionException ex) when (String.Equals(ex.Code, code, StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException("Expected ToolExecutionException with code " + code + ".");
        }

        private static bool TryCreateDirectoryLink(string linkPath, string targetPath)
        {
            try
            {
                FileSystemInfo link = Directory.CreateSymbolicLink(linkPath, targetPath);
                link.Refresh();
                return link.Exists;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (PlatformNotSupportedException)
            {
            }

            if (!OperatingSystem.IsWindows()) return false;

            try
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                startInfo.ArgumentList.Add("/c");
                startInfo.ArgumentList.Add("mklink");
                startInfo.ArgumentList.Add("/J");
                startInfo.ArgumentList.Add(linkPath);
                startInfo.ArgumentList.Add(targetPath);
                using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start mklink.");
                process.WaitForExit();
                return process.ExitCode == 0 && Directory.Exists(linkPath);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void TryDeleteDirectoryLink(string path)
        {
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static JsonSerializerOptions TestJson()
        {
            JsonSerializerOptions json = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
            json.Converters.Add(new JsonStringEnumConverter());
            return json;
        }

        private static int GetFreePort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                IPEndPoint endpoint = (IPEndPoint)listener.LocalEndpoint;
                return endpoint.Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task WaitForHttpAsync(HttpClient client)
        {
            for (int i = 0; i < 50; i++)
            {
                try
                {
                    using (HttpResponseMessage response = await client.GetAsync("/health").ConfigureAwait(false))
                    {
                        if (response.IsSuccessStatusCode) return;
                    }
                }
                catch (HttpRequestException)
                {
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            throw new TimeoutException("Wilson test server did not become reachable.");
        }

        private static async Task<T> PostJsonAsync<T>(HttpClient client, string path, object body)
        {
            string json = JsonSerializer.Serialize(body, TestJson());
            using (StringContent content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = await client.PostAsync(path, content).ConfigureAwait(false))
            {
                string payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Expected success from " + path + " but received " + (int)response.StatusCode + ": " + payload);
                T? value = JsonSerializer.Deserialize<T>(payload, TestJson());
                if (value == null) throw new InvalidOperationException("Expected JSON response from " + path + ".");
                return value;
            }
        }

        private static async Task ExpectStatusAsync(HttpClient client, string path, object body, HttpStatusCode expectedStatus)
        {
            string json = JsonSerializer.Serialize(body, TestJson());
            using (StringContent content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage response = await client.PostAsync(path, content).ConfigureAwait(false))
            {
                if (response.StatusCode != expectedStatus)
                {
                    string payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new InvalidOperationException("Expected " + (int)expectedStatus + " from " + path + " but received " + (int)response.StatusCode + ": " + payload);
                }
            }
        }

        private static async Task ExpectGetStatusAsync(HttpClient client, string path, HttpStatusCode expectedStatus)
        {
            using (HttpResponseMessage response = await client.GetAsync(path).ConfigureAwait(false))
            {
                if (response.StatusCode != expectedStatus)
                {
                    string payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new InvalidOperationException("Expected " + (int)expectedStatus + " from " + path + " but received " + (int)response.StatusCode + ": " + payload);
                }
            }
        }

        private static async Task<JsonDocument> GetJsonDocumentAsync(HttpClient client, string path)
        {
            using (HttpResponseMessage response = await client.GetAsync(path).ConfigureAwait(false))
            {
                string payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Expected success from " + path + " but received " + (int)response.StatusCode + ": " + payload);
                return JsonDocument.Parse(payload);
            }
        }

        private static void AssertEnumerationCount(JsonDocument document, int expectedCount, string label)
        {
            JsonElement root = document.RootElement;
            if (!root.TryGetProperty("objects", out JsonElement objects) || objects.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Expected " + label + " enumeration objects array.");
            if (objects.GetArrayLength() != expectedCount)
                throw new InvalidOperationException("Expected " + expectedCount + " " + label + " but received " + objects.GetArrayLength() + ".");
        }

        private static async Task RunProcessToolAsync()
        {
            string workspace = Path.Combine(Path.GetTempPath(), "wilson-tools-process-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try
            {
                Settings configured = new Settings
                {
                    Tools = new ToolsSettings
                    {
                        Enabled = true,
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace },
                        ProcessTimeoutMs = 5000,
                        MaxToolOutputChars = 1024
                    }
                };
                ToolService service = new ToolService(configured);
                ToolDescriptor? descriptor = service.GetTool("run_process");
                if (descriptor == null || !descriptor.Available || !descriptor.Dangerous || !descriptor.RequiresApproval) throw new InvalidOperationException("Expected run_process to be available and approval-required.");

                string command = OperatingSystem.IsWindows() ? "cmd.exe" : "sh";
                string successArgs = OperatingSystem.IsWindows() ? """["/c","echo hello"]""" : """["-c","echo hello"]""";
                ToolResult success = await ExecuteToolAsync(service, "run_process", "{\"command\":\"" + command + "\",\"args\":" + successArgs + "}").ConfigureAwait(false);
                if (!success.Success || !success.Content.Contains("hello", StringComparison.Ordinal) || !success.Content.Contains("\"exitCode\":0", StringComparison.Ordinal)) throw new InvalidOperationException("Expected run_process to capture successful stdout and exit code.");

                string failureArgs = OperatingSystem.IsWindows() ? """["/c","exit 7"]""" : """["-c","exit 7"]""";
                ToolResult failure = await ExecuteToolAsync(service, "run_process", "{\"command\":\"" + command + "\",\"args\":" + failureArgs + "}").ConfigureAwait(false);
                if (!failure.Success || !failure.Content.Contains("\"exitCode\":7", StringComparison.Ordinal)) throw new InvalidOperationException("Expected run_process to capture non-zero exit code.");

                string timeoutArgs = OperatingSystem.IsWindows() ? """["/c","ping -n 6 127.0.0.1 > nul"]""" : """["-c","sleep 5"]""";
                ToolResult timeout = await ExecuteToolAsync(service, "run_process", "{\"command\":\"" + command + "\",\"args\":" + timeoutArgs + ",\"timeout_ms\":1000}").ConfigureAwait(false);
                if (!timeout.Success || !timeout.Content.Contains("\"timedOut\":true", StringComparison.Ordinal)) throw new InvalidOperationException("Expected run_process timeout to be captured.");

                using (CancellationTokenSource cancellation = new CancellationTokenSource(500))
                {
                    ToolResult cancelled = await ExecuteToolAsync(service, "run_process", "{\"command\":\"" + command + "\",\"args\":" + timeoutArgs + ",\"timeout_ms\":5000}", cancellation.Token).ConfigureAwait(false);
                    if (!cancelled.Success || !cancelled.Content.Contains("\"cancelled\":true", StringComparison.Ordinal)) throw new InvalidOperationException("Expected run_process cancellation to be captured.");
                }

                ToolResult outside = await ExecuteToolAsync(service, "run_process", "{\"command\":\"" + command + "\",\"args\":" + successArgs + ",\"working_directory\":\"..\"}").ConfigureAwait(false);
                if (outside.Success || !String.Equals(outside.ErrorCode, "path_outside_allowed_roots", StringComparison.Ordinal)) throw new InvalidOperationException("Expected run_process working directory guard to reject outside roots.");
            }
            finally
            {
                TryDeleteDirectory(workspace);
            }
        }

        private static void ToolCapableInferenceParsing()
        {
            string openAiResponse = """
{
  "choices": [
    {
      "finish_reason": "tool_calls",
      "message": {
        "role": "assistant",
        "content": null,
        "tool_calls": [
          {
            "id": "call_1",
            "type": "function",
            "function": {
              "name": "read_file",
              "arguments": "{\"file_path\":\"sample.txt\"}"
            }
          }
        ]
      }
    }
  ]
}
""";

            ToolCapableInferenceResponse openAi = InferenceService.ParseToolCapableResponse("OpenAIChatCompletions", openAiResponse);
            if (!openAi.Success || openAi.ToolCalls.Count != 1) throw new InvalidOperationException("Expected one OpenAI-compatible tool call.");
            if (!String.Equals(openAi.ToolCalls[0].Id, "call_1", StringComparison.Ordinal)) throw new InvalidOperationException("Expected provider tool-call ID to be preserved.");
            if (!openAi.ToolCalls[0].Function!.Arguments.Contains("sample.txt", StringComparison.Ordinal)) throw new InvalidOperationException("Expected OpenAI tool-call arguments to be preserved.");

            string ollamaResponse = """
{
  "message": {
    "role": "assistant",
    "content": "",
    "tool_calls": [
      {
        "type": "function",
        "function": {
          "name": "read_file",
          "arguments": {
            "file_path": "sample.txt"
          }
        }
      }
    ]
  }
}
""";

            ToolCapableInferenceResponse ollama = InferenceService.ParseToolCapableResponse("OllamaChat", ollamaResponse);
            if (!ollama.Success || ollama.ToolCalls.Count != 1) throw new InvalidOperationException("Expected one Ollama tool call.");
            if (!String.Equals(ollama.FinishReason, "tool_calls", StringComparison.Ordinal)) throw new InvalidOperationException("Expected missing Ollama finish reason to normalize to tool_calls.");
            if (!ollama.ToolCalls[0].Function!.Arguments.Contains("file_path", StringComparison.Ordinal)) throw new InvalidOperationException("Expected raw Ollama object arguments to be preserved.");
        }

        private static async Task ToolAgentLoopAsync()
        {
            string workspace = Path.Combine(Path.GetTempPath(), "wilson-agent-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try
            {
                await File.WriteAllTextAsync(Path.Combine(workspace, "sample.txt"), "agent loop content").ConfigureAwait(false);
                Settings settings = new Settings
                {
                    Tools = new ToolsSettings
                    {
                        Enabled = true,
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace },
                        DefaultApprovalPolicy = ToolApprovalPolicies.Auto,
                        MaxToolIterations = 4,
                        MaxToolCallsPerTurn = 4
                    }
                };
                ToolService toolService = new ToolService(settings);
                int modelCalls = 0;
                ToolAgentService agent = new ToolAgentService(
                    toolService,
                    (runner, request, token) =>
                    {
                        modelCalls++;
                        if (modelCalls == 1)
                        {
                            return Task.FromResult(new ToolCapableInferenceResponse
                            {
                                Success = true,
                                FinishReason = "tool_calls",
                                ToolCalls = new List<ModelToolCall>
                                {
                                    new ModelToolCall
                                    {
                                        Id = "call_read",
                                        Function = new ModelToolFunctionCall
                                        {
                                            Name = "read_file",
                                            Arguments = "{\"file_path\":\"sample.txt\"}"
                                        }
                                    }
                                }
                            });
                        }

                        if (!request.Messages.Exists(message => String.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase) && (message.Content ?? String.Empty).Contains("agent loop content", StringComparison.Ordinal)))
                            throw new InvalidOperationException("Expected tool result message before final model call.");

                        return Task.FromResult(new ToolCapableInferenceResponse
                        {
                            Success = true,
                            Content = "The file contains agent loop content.",
                            FinishReason = "stop"
                        });
                    });

                ToolAgentResponse response = await agent.RunAsync(
                    new ModelRunnerSettings { ApiType = "OpenAI", Endpoint = "http://localhost", Models = new List<string> { "test-model" } },
                    "test-model",
                    new List<ModelChatMessage> { new ModelChatMessage { Role = "user", Content = "Read sample.txt." } },
                    new CompletionRequestSettings { MaxTokens = 128, Temperature = 0, TopP = 1 },
                    new ToolExecutionContext { Settings = settings },
                    CancellationToken.None).ConfigureAwait(false);

                if (!response.Success) throw new InvalidOperationException("Expected tool agent loop to succeed.");
                if (modelCalls != 2) throw new InvalidOperationException("Expected two model calls in the tool agent loop.");
                if (response.ToolCalls.Count != 1 || !response.ToolCalls[0].Success) throw new InvalidOperationException("Expected one successful tool trace.");
                if (!response.Content.Contains("agent loop content", StringComparison.Ordinal)) throw new InvalidOperationException("Expected final answer from second model turn.");
            }
            finally
            {
                Directory.Delete(workspace, true);
            }
        }

        private static async Task ToolAgentApprovalPolicyAsync()
        {
            string workspace = Path.Combine(Path.GetTempPath(), "wilson-agent-approval-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try
            {
                string sourceFile = Path.Combine(workspace, "sample.txt");
                string deniedFile = Path.Combine(workspace, "created.txt");
                await File.WriteAllTextAsync(sourceFile, "approval policy content").ConfigureAwait(false);

                Settings denySettings = new Settings
                {
                    Tools = new ToolsSettings
                    {
                        Enabled = true,
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace },
                        DefaultApprovalPolicy = ToolApprovalPolicies.Deny,
                        MaxToolIterations = 2,
                        MaxToolCallsPerTurn = 2
                    }
                };
                ToolAgentResponse denyResponse = await RunSingleToolThenFinalAsync(denySettings, "read_file", """{"file_path":"sample.txt"}""", message =>
                {
                    string content = message.Content ?? String.Empty;
                    if (!content.Contains("tool_call_denied", StringComparison.Ordinal) || content.Contains("approval policy content", StringComparison.Ordinal))
                        throw new InvalidOperationException("Expected deny approval policy to return a denial result without reading file content.");
                }).ConfigureAwait(false);
                if (!denyResponse.Success || denyResponse.ToolCalls.Count != 1 || !denyResponse.ToolCalls[0].Denied || denyResponse.ToolCalls[0].Success) throw new InvalidOperationException("Expected deny approval policy to produce a denied trace.");

                Settings approvalRequiredSettings = new Settings
                {
                    Tools = new ToolsSettings
                    {
                        Enabled = true,
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace },
                        DefaultApprovalPolicy = ToolApprovalPolicies.Auto,
                        DestructiveToolsRequireApproval = true,
                        MaxToolIterations = 2,
                        MaxToolCallsPerTurn = 2
                    }
                };
                ToolDescriptor? writeDescriptor = new ToolService(approvalRequiredSettings).GetTool("write_file");
                if (writeDescriptor == null || !writeDescriptor.RequiresApproval) throw new InvalidOperationException("Expected destructive tool to require approval when configured.");

                ToolAgentResponse approvalRequiredResponse = await RunSingleToolThenFinalAsync(approvalRequiredSettings, "write_file", """{"file_path":"created.txt","content":"denied"}""", message =>
                {
                    string content = message.Content ?? String.Empty;
                    if (!content.Contains("tool_call_denied", StringComparison.Ordinal)) throw new InvalidOperationException("Expected approval-required tool to return a denial result.");
                }).ConfigureAwait(false);
                if (!approvalRequiredResponse.Success || approvalRequiredResponse.ToolCalls.Count != 1 || !approvalRequiredResponse.ToolCalls[0].Denied || approvalRequiredResponse.ToolCalls[0].Success) throw new InvalidOperationException("Expected approval-required tool to produce a denied trace.");
                if (File.Exists(deniedFile)) throw new InvalidOperationException("Approval-required write_file should not execute in the non-streaming tool loop.");

                approvalRequiredSettings.Tools.DestructiveToolsRequireApproval = false;
                ToolDescriptor? noApprovalWriteDescriptor = new ToolService(approvalRequiredSettings).GetTool("write_file");
                if (noApprovalWriteDescriptor == null || noApprovalWriteDescriptor.RequiresApproval) throw new InvalidOperationException("Expected destructive approval override to clear approval requirement.");
            }
            finally
            {
                TryDeleteDirectory(workspace);
            }
        }

        private static async Task<ToolAgentResponse> RunSingleToolThenFinalAsync(Settings settings, string toolName, string arguments, Action<ModelChatMessage> assertToolMessage)
        {
            ToolService toolService = new ToolService(settings);
            int modelCalls = 0;
            ToolAgentService agent = new ToolAgentService(
                toolService,
                (runner, request, token) =>
                {
                    modelCalls++;
                    if (modelCalls == 1)
                    {
                        return Task.FromResult(new ToolCapableInferenceResponse
                        {
                            Success = true,
                            FinishReason = "tool_calls",
                            ToolCalls = new List<ModelToolCall>
                            {
                                new ModelToolCall
                                {
                                    Id = "call_policy",
                                    Function = new ModelToolFunctionCall
                                    {
                                        Name = toolName,
                                        Arguments = arguments
                                    }
                                }
                            }
                        });
                    }

                    ModelChatMessage? toolMessage = request.Messages.FirstOrDefault(message => String.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase));
                    if (toolMessage == null) throw new InvalidOperationException("Expected tool result message before final model call.");
                    assertToolMessage(toolMessage);
                    return Task.FromResult(new ToolCapableInferenceResponse { Success = true, Content = "approval checked", FinishReason = "stop" });
                });

            ToolAgentResponse response = await agent.RunAsync(
                new ModelRunnerSettings { ApiType = "OpenAI", Endpoint = "http://localhost", Models = new List<string> { "test-model" } },
                "test-model",
                new List<ModelChatMessage> { new ModelChatMessage { Role = "user", Content = "Run policy test." } },
                new CompletionRequestSettings { MaxTokens = 128, Temperature = 0, TopP = 1 },
                new ToolExecutionContext { Settings = settings },
                CancellationToken.None).ConfigureAwait(false);

            if (modelCalls != 2) throw new InvalidOperationException("Expected denial result to be sent back to the model for a final answer.");
            return response;
        }

        private static async Task ToolAgentLoopCoverageAsync()
        {
            string workspace = Path.Combine(Path.GetTempPath(), "wilson-agent-coverage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try
            {
                await File.WriteAllTextAsync(Path.Combine(workspace, "one.txt"), "one content").ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(workspace, "two.txt"), "two content").ConfigureAwait(false);

                Settings settings = AgentTestSettings(workspace);
                await AssertNoToolPathAsync(settings).ConfigureAwait(false);
                await AssertMultipleToolCallsAsync(settings).ConfigureAwait(false);
                await AssertSequentialToolCallsAsync(settings).ConfigureAwait(false);
                await AssertUnknownAndDisabledToolsAsync(settings, workspace).ConfigureAwait(false);
                await AssertToolCallLimitAsync(settings).ConfigureAwait(false);
                await AssertIterationLimitAsync(settings).ConfigureAwait(false);
            }
            finally
            {
                TryDeleteDirectory(workspace);
            }
        }

        private static Settings AgentTestSettings(string workspace)
        {
            return new Settings
            {
                Tools = new ToolsSettings
                {
                    Enabled = true,
                    WorkingDirectory = workspace,
                    AllowedRoots = new List<string> { workspace },
                    DefaultApprovalPolicy = ToolApprovalPolicies.Auto,
                    MaxToolIterations = 4,
                    MaxToolCallsPerTurn = 4
                }
            };
        }

        private static async Task AssertNoToolPathAsync(Settings settings)
        {
            int modelCalls = 0;
            ToolAgentService agent = new ToolAgentService(new ToolService(settings), (runner, request, token) =>
            {
                modelCalls++;
                if (request.Tools.Count == 0) throw new InvalidOperationException("Expected available tool definitions in tool-capable request.");
                return Task.FromResult(new ToolCapableInferenceResponse { Success = true, Content = "plain answer", FinishReason = "stop" });
            });

            ToolAgentResponse response = await RunAgentAsync(agent, settings, "answer directly").ConfigureAwait(false);
            if (!response.Success || modelCalls != 1 || response.ToolCallCount != 0 || response.ToolCalls.Count != 0 || !String.Equals(response.Content, "plain answer", StringComparison.Ordinal)) throw new InvalidOperationException("Expected no-tool path to return the final assistant answer.");
        }

        private static async Task AssertMultipleToolCallsAsync(Settings settings)
        {
            int modelCalls = 0;
            ToolAgentService agent = new ToolAgentService(new ToolService(settings), (runner, request, token) =>
            {
                modelCalls++;
                if (modelCalls == 1)
                {
                    return Task.FromResult(new ToolCapableInferenceResponse
                    {
                        Success = true,
                        FinishReason = "tool_calls",
                        ToolCalls = new List<ModelToolCall>
                        {
                            new ModelToolCall { Id = "call_one", Function = new ModelToolFunctionCall { Name = "read_file", Arguments = """{"file_path":"one.txt"}""" } },
                            new ModelToolCall { Id = "call_two", Function = new ModelToolFunctionCall { Name = "read_file", Arguments = """{"file_path":"two.txt"}""" } }
                        }
                    });
                }

                List<ModelChatMessage> toolMessages = request.Messages.Where(message => String.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase)).ToList();
                if (toolMessages.Count != 2 || !(toolMessages[0].Content ?? String.Empty).Contains("one content", StringComparison.Ordinal) || !(toolMessages[1].Content ?? String.Empty).Contains("two content", StringComparison.Ordinal))
                    throw new InvalidOperationException("Expected both tool results before final model call.");
                return Task.FromResult(new ToolCapableInferenceResponse { Success = true, Content = "multiple complete", FinishReason = "stop" });
            });

            ToolAgentResponse response = await RunAgentAsync(agent, settings, "read both").ConfigureAwait(false);
            if (!response.Success || modelCalls != 2 || response.ToolCalls.Count != 2 || response.ToolCalls.Any(call => !call.Success)) throw new InvalidOperationException("Expected multiple tool calls to complete in one assistant message.");
        }

        private static async Task AssertSequentialToolCallsAsync(Settings settings)
        {
            int modelCalls = 0;
            ToolAgentService agent = new ToolAgentService(new ToolService(settings), (runner, request, token) =>
            {
                modelCalls++;
                if (modelCalls == 1)
                    return Task.FromResult(ToolCallResponse("call_seq_one", "read_file", """{"file_path":"one.txt"}"""));

                if (modelCalls == 2)
                {
                    if (!request.Messages.Any(message => String.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase) && (message.Content ?? String.Empty).Contains("one content", StringComparison.Ordinal)))
                        throw new InvalidOperationException("Expected first sequential tool result before second tool request.");
                    return Task.FromResult(ToolCallResponse("call_seq_two", "read_file", """{"file_path":"two.txt"}"""));
                }

                List<ModelChatMessage> toolMessages = request.Messages.Where(message => String.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase)).ToList();
                if (toolMessages.Count != 2 || !(toolMessages[1].Content ?? String.Empty).Contains("two content", StringComparison.Ordinal))
                    throw new InvalidOperationException("Expected second sequential tool result before final response.");
                return Task.FromResult(new ToolCapableInferenceResponse { Success = true, Content = "sequential complete", FinishReason = "stop" });
            });

            ToolAgentResponse response = await RunAgentAsync(agent, settings, "read sequential").ConfigureAwait(false);
            if (!response.Success || modelCalls != 3 || response.IterationCount != 3 || response.ToolCallCount != 2) throw new InvalidOperationException("Expected sequential tool calls across model iterations.");
        }

        private static async Task AssertUnknownAndDisabledToolsAsync(Settings settings, string workspace)
        {
            ToolAgentResponse unknown = await RunSingleToolThenFinalAsync(settings, "missing_tool", "{}", message =>
            {
                if (!(message.Content ?? String.Empty).Contains("unknown_tool", StringComparison.Ordinal)) throw new InvalidOperationException("Expected unknown tool result to be returned to the model.");
            }).ConfigureAwait(false);
            if (!unknown.Success || unknown.ToolCalls.Count != 1 || unknown.ToolCalls[0].Success || unknown.ToolCalls[0].Denied) throw new InvalidOperationException("Expected unknown tool to fail without denial.");

            Settings disabled = AgentTestSettings(workspace);
            disabled.Tools.DisabledToolNames = new List<string> { "read_file" };
            ToolAgentResponse disabledResponse = await RunSingleToolThenFinalAsync(disabled, "read_file", """{"file_path":"one.txt"}""", message =>
            {
                if (!(message.Content ?? String.Empty).Contains("tool_unavailable", StringComparison.Ordinal)) throw new InvalidOperationException("Expected disabled tool result to be returned to the model.");
            }).ConfigureAwait(false);
            if (!disabledResponse.Success || disabledResponse.ToolCalls.Count != 1 || disabledResponse.ToolCalls[0].Success || disabledResponse.ToolCalls[0].Denied) throw new InvalidOperationException("Expected disabled tool to fail without denial.");
        }

        private static async Task AssertToolCallLimitAsync(Settings settings)
        {
            Settings limited = AgentTestSettings(settings.Tools.WorkingDirectory ?? String.Empty);
            limited.Tools.MaxToolCallsPerTurn = 1;
            int modelCalls = 0;
            ToolAgentService agent = new ToolAgentService(new ToolService(limited), (runner, request, token) =>
            {
                modelCalls++;
                if (modelCalls == 1)
                {
                    return Task.FromResult(new ToolCapableInferenceResponse
                    {
                        Success = true,
                        FinishReason = "tool_calls",
                        ToolCalls = new List<ModelToolCall>
                        {
                            new ModelToolCall { Id = "call_allowed", Function = new ModelToolFunctionCall { Name = "read_file", Arguments = """{"file_path":"one.txt"}""" } },
                            new ModelToolCall { Id = "call_limited", Function = new ModelToolFunctionCall { Name = "read_file", Arguments = """{"file_path":"two.txt"}""" } }
                        }
                    });
                }

                List<ModelChatMessage> toolMessages = request.Messages.Where(message => String.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase)).ToList();
                if (toolMessages.Count != 2 || !(toolMessages[1].Content ?? String.Empty).Contains("tool_call_limit_reached", StringComparison.Ordinal))
                    throw new InvalidOperationException("Expected tool-call limit result before final model call.");
                return Task.FromResult(new ToolCapableInferenceResponse { Success = true, Content = "limited complete", FinishReason = "stop" });
            });

            ToolAgentResponse response = await RunAgentAsync(agent, limited, "trigger limit").ConfigureAwait(false);
            if (!response.Success || response.ToolCalls.Count != 2 || !response.ToolCalls[1].Denied || response.ToolCalls[1].Success) throw new InvalidOperationException("Expected max tool calls per turn to produce a denied trace.");
        }

        private static async Task AssertIterationLimitAsync(Settings settings)
        {
            Settings limited = AgentTestSettings(settings.Tools.WorkingDirectory ?? String.Empty);
            limited.Tools.MaxToolIterations = 2;
            ToolAgentService agent = new ToolAgentService(new ToolService(limited), (runner, request, token) =>
            {
                return Task.FromResult(ToolCallResponse("call_loop_" + request.Messages.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), "read_file", """{"file_path":"one.txt"}"""));
            });

            ToolAgentResponse response = await RunAgentAsync(agent, limited, "loop until limit").ConfigureAwait(false);
            if (response.Success || !String.Equals(response.FinishReason, "tool_iteration_limit", StringComparison.Ordinal) || response.ToolCallCount != 2) throw new InvalidOperationException("Expected max iterations to stop the tool loop.");
        }

        private static ToolCapableInferenceResponse ToolCallResponse(string id, string name, string arguments)
        {
            return new ToolCapableInferenceResponse
            {
                Success = true,
                FinishReason = "tool_calls",
                ToolCalls = new List<ModelToolCall>
                {
                    new ModelToolCall
                    {
                        Id = id,
                        Function = new ModelToolFunctionCall
                        {
                            Name = name,
                            Arguments = arguments
                        }
                    }
                }
            };
        }

        private static async Task<ToolAgentResponse> RunAgentAsync(ToolAgentService agent, Settings settings, string prompt)
        {
            return await agent.RunAsync(
                new ModelRunnerSettings { ApiType = "OpenAI", Endpoint = "http://localhost", Models = new List<string> { "test-model" } },
                "test-model",
                new List<ModelChatMessage> { new ModelChatMessage { Role = "user", Content = prompt } },
                new CompletionRequestSettings { MaxTokens = 128, Temperature = 0, TopP = 1 },
                new ToolExecutionContext { Settings = settings },
                CancellationToken.None).ConfigureAwait(false);
        }

        private static async Task ToolAgentPerTurnOutputLimitAsync()
        {
            string workspace = Path.Combine(Path.GetTempPath(), "wilson-agent-budget-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workspace);
            try
            {
                await File.WriteAllTextAsync(Path.Combine(workspace, "one.txt"), new String('a', 700)).ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(workspace, "two.txt"), new String('b', 700)).ConfigureAwait(false);
                Settings settings = new Settings
                {
                    Tools = new ToolsSettings
                    {
                        Enabled = true,
                        WorkingDirectory = workspace,
                        AllowedRoots = new List<string> { workspace },
                        DefaultApprovalPolicy = ToolApprovalPolicies.Auto,
                        MaxToolIterations = 2,
                        MaxToolCallsPerTurn = 4,
                        MaxToolOutputChars = 1024,
                        MaxToolOutputCharsPerTurn = 1100
                    }
                };
                ToolService toolService = new ToolService(settings);
                int modelCalls = 0;
                bool sawTurnTruncation = false;
                ToolAgentService agent = new ToolAgentService(
                    toolService,
                    (runner, request, token) =>
                    {
                        modelCalls++;
                        if (modelCalls == 1)
                        {
                            return Task.FromResult(new ToolCapableInferenceResponse
                            {
                                Success = true,
                                FinishReason = "tool_calls",
                                ToolCalls = new List<ModelToolCall>
                                {
                                    new ModelToolCall { Id = "call_one", Function = new ModelToolFunctionCall { Name = "read_file", Arguments = """{"file_path":"one.txt"}""" } },
                                    new ModelToolCall { Id = "call_two", Function = new ModelToolFunctionCall { Name = "read_file", Arguments = """{"file_path":"two.txt"}""" } }
                                }
                            });
                        }

                        List<ModelChatMessage> toolMessages = request.Messages.Where(message => String.Equals(message.Role, "tool", StringComparison.Ordinal)).ToList();
                        string secondToolContent = toolMessages.Count > 1 ? toolMessages[1].Content ?? String.Empty : String.Empty;
                        if (toolMessages.Count == 2)
                        {
                            using JsonDocument truncation = JsonDocument.Parse(secondToolContent);
                            sawTurnTruncation = truncation.RootElement.TryGetProperty("truncated", out JsonElement truncatedElement)
                                && truncatedElement.GetBoolean()
                                && truncation.RootElement.TryGetProperty("originalCharacters", out JsonElement originalCharactersElement)
                                && originalCharactersElement.GetInt32() > 0
                                && truncation.RootElement.TryGetProperty("content", out JsonElement contentElement)
                                && contentElement.ValueKind == JsonValueKind.String;
                        }

                        return Task.FromResult(new ToolCapableInferenceResponse { Success = true, Content = "budgeted", FinishReason = "stop" });
                    });

                ToolAgentResponse response = await agent.RunAsync(
                    new ModelRunnerSettings { ApiType = "OpenAI", Endpoint = "http://localhost", Models = new List<string> { "test-model" } },
                    "test-model",
                    new List<ModelChatMessage> { new ModelChatMessage { Role = "user", Content = "read both" } },
                    new CompletionRequestSettings { MaxTokens = 128, Temperature = 0, TopP = 1 },
                    new ToolExecutionContext { Settings = settings },
                    CancellationToken.None).ConfigureAwait(false);

                if (!response.Success) throw new InvalidOperationException("Expected budgeted tool agent loop to succeed.");
                if (!sawTurnTruncation) throw new InvalidOperationException("Expected per-turn output budget truncation before final model call.");
                if (response.ToolCalls.Count != 2 || !response.ToolCalls[1].Truncated) throw new InvalidOperationException("Expected safe trace to mark the budget-truncated tool call.");
            }
            finally
            {
                Directory.Delete(workspace, true);
            }
        }

        private static void HealthCheckDefaults()
        {
            ModelRunnerSettings ollama = new ModelRunnerSettings { ApiType = "Ollama", Endpoint = "http://localhost:11434" };
            ModelRunnerSettings.ApplyHealthCheckDefaults(ollama);
            if (!String.Equals(ollama.HealthCheckUrl, "http://localhost:11434/api/tags", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected Ollama health check URL.");
            if (ollama.HealthCheckIntervalMs != 5000 || ollama.HealthCheckTimeoutMs != 2000) throw new InvalidOperationException("Unexpected Ollama health check timing defaults.");

            ModelRunnerSettings openAi = new ModelRunnerSettings { ApiType = "OpenAI", Endpoint = "https://api.openai.com", ApiKey = "test-key" };
            ModelRunnerSettings.ApplyHealthCheckDefaults(openAi);
            if (!String.Equals(openAi.HealthCheckUrl, "https://api.openai.com/v1/models", StringComparison.Ordinal)) throw new InvalidOperationException("Unexpected OpenAI health check URL.");
            if (!openAi.HealthCheckUseAuth) throw new InvalidOperationException("Expected OpenAI health checks to use auth when an API key is configured.");
        }

        private static void HealthStatusSnapshot()
        {
            DateTime lastUnhealthy = DateTime.UtcNow.AddSeconds(-3);
            EndpointHealthState state = new EndpointHealthState
            {
                EndpointId = "runner-1",
                EndpointName = "Runner 1",
                IsHealthy = true,
                FirstCheckUtc = DateTime.UtcNow.AddSeconds(-2),
                LastUnhealthyUtc = lastUnhealthy,
                LastStateChangeUtc = DateTime.UtcNow.AddSeconds(-1),
                ConsecutiveSuccesses = 2
            };
            lock (state.HistoryLock)
            {
                state.CheckHistory.Add(new HealthCheckRecord { TimestampUtc = DateTime.UtcNow, Success = true });
            }

            EndpointHealthStatus status = EndpointHealthStatus.FromState(state);
            if (!status.IsHealthy) throw new InvalidOperationException("Expected health status to be healthy.");
            if (status.LastUnhealthyUtc != lastUnhealthy) throw new InvalidOperationException("Expected last unhealthy timestamp to be preserved.");
            if (status.UptimePercentage <= 0) throw new InvalidOperationException("Expected positive uptime percentage.");
            if (status.History.Count != 1) throw new InvalidOperationException("Expected health history snapshot.");
        }
    }
}
