namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
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
            await FilesystemMutationToolsAsync().ConfigureAwait(false);
            await RunProcessToolAsync().ConfigureAwait(false);
            ToolCapableInferenceParsing();
            await ToolAgentLoopAsync().ConfigureAwait(false);
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
            if (disabledService.ListTools(true).Count == 0) throw new InvalidOperationException("Disabled tool service should expose diagnostic descriptors.");
            if (disabledService.GetModelToolDefinitions().Count != 0) throw new InvalidOperationException("Foundation tool service should not expose model tool definitions.");

            Settings enabled = new Settings { Tools = new ToolsSettings { Enabled = true } };
            ToolService enabledService = new ToolService(enabled);
            if (enabledService.ListTools(false).Count != 0) throw new InvalidOperationException("Tool service should not expose file tools without a working directory and allowed root.");
            ToolDescriptor? unavailableRead = enabledService.GetTool("read_file");
            if (unavailableRead == null || unavailableRead.Available) throw new InvalidOperationException("Expected read_file to be unavailable until workspace settings are configured.");

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
                Directory.Delete(workspace, true);
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
                {
                    Task serverTask = Task.Run(() => server.Server.StartAsync(serverStop.Token), serverStop.Token);
                    adminClient.BaseAddress = new Uri("http://127.0.0.1:" + port);
                    adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin-token");
                    userClient.BaseAddress = adminClient.BaseAddress;

                    try
                    {
                        await WaitForHttpAsync(adminClient).ConfigureAwait(false);

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

                        await ExpectStatusAsync(
                            userClient,
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
            using JsonDocument document = JsonDocument.Parse(json);
            return await service.ExecuteAsync(
                Wilson.Core.Helpers.IdGenerator.ToolCall(),
                name,
                document.RootElement,
                new ToolExecutionContext(),
                CancellationToken.None).ConfigureAwait(false);
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

        private static async Task<JsonDocument> GetJsonDocumentAsync(HttpClient client, string path)
        {
            using (HttpResponseMessage response = await client.GetAsync(path).ConfigureAwait(false))
            {
                string payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Expected success from " + path + " but received " + (int)response.StatusCode + ": " + payload);
                return JsonDocument.Parse(payload);
            }
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

                ToolResult outside = await ExecuteToolAsync(service, "run_process", "{\"command\":\"" + command + "\",\"args\":" + successArgs + ",\"working_directory\":\"..\"}").ConfigureAwait(false);
                if (outside.Success || !String.Equals(outside.ErrorCode, "path_outside_allowed_roots", StringComparison.Ordinal)) throw new InvalidOperationException("Expected run_process working directory guard to reject outside roots.");
            }
            finally
            {
                Directory.Delete(workspace, true);
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
                        sawTurnTruncation = toolMessages.Count == 2 && secondToolContent.Contains("originalCharacters", StringComparison.Ordinal);
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
