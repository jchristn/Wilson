namespace Wilson.Core.Observability
{
    using System.Collections.Generic;
    using System.Linq;
    using Radiant;

    /// <summary>
    /// Wilson metric catalog, organized by functional area. Instrument names are dotted and lowercase; units are
    /// UCUM. High-cardinality detail (ids, arguments) belongs on spans, never on these metric labels.
    /// </summary>
    public static class WilsonConventions
    {
        /// <summary>Meter and activity-source name Wilson emits under.</summary>
        public const string SourceName = "Wilson";

        /// <summary>HTTP request-path metrics.</summary>
        public static class Http
        {
            /// <summary>HTTP requests handled, labeled by method, route template, and status class.</summary>
            public static readonly Convention Requests =
                Convention.Counter("wilson.http.requests", "{request}", "http.request.method", "http.route", "status_class")
                    .WithDescription("HTTP requests handled by Wilson.");

            /// <summary>Inbound HTTP request duration (seconds).</summary>
            public static readonly Convention Duration = SemConv.Http.ServerRequestDuration;

            /// <summary>Concurrent in-flight HTTP requests.</summary>
            public static readonly Convention Active = SemConv.Http.ServerActiveRequests;
        }

        /// <summary>Chat and inference metrics.</summary>
        public static class Chat
        {
            /// <summary>Chat requests, labeled by runner, model, mode (sync|stream|tools), and outcome.</summary>
            public static readonly Convention Requests =
                Convention.Counter("wilson.chat.requests", "{request}", "runner", "model", "mode", "outcome")
                    .WithDescription("Chat requests handled by Wilson.");

            /// <summary>Chat request duration (seconds).</summary>
            public static readonly Convention Duration =
                Convention.Histogram("wilson.chat.duration", "s", LatencyBuckets.Default, "runner", "model", "mode", "outcome")
                    .WithDescription("Chat request duration.");

            /// <summary>Time to first token (seconds).</summary>
            public static readonly Convention TimeToFirstToken =
                Convention.Histogram("wilson.chat.ttft", "s", LatencyBuckets.Default, "runner", "model")
                    .WithDescription("Time to first streamed token.");

            /// <summary>Estimated tokens, labeled by direction (input|output).</summary>
            public static readonly Convention Tokens =
                Convention.Counter("wilson.chat.tokens", "{token}", "runner", "model", "direction")
                    .WithDescription("Estimated chat tokens processed.");

            /// <summary>Concurrent in-flight chat requests.</summary>
            public static readonly Convention Active =
                Convention.UpDownCounter("wilson.chat.active", "{request}")
                    .WithDescription("Concurrent in-flight chat requests.");

            /// <summary>Ollama model operations, labeled by op (pull|load), runner, model, and outcome.</summary>
            public static readonly Convention OllamaOps =
                Convention.Counter("wilson.inference.ollama.ops", "{operation}", "op", "runner", "outcome")
                    .WithDescription("Ollama model pull/load operations.");

            /// <summary>Ollama model operation duration (seconds).</summary>
            public static readonly Convention OllamaDuration =
                Convention.Histogram("wilson.inference.ollama.duration", "s", LatencyBuckets.Network, "op", "runner", "outcome")
                    .WithDescription("Ollama model pull/load duration.");
        }

        /// <summary>Tool and agent-loop metrics.</summary>
        public static class Tools
        {
            /// <summary>Agent runs, labeled by outcome.</summary>
            public static readonly Convention AgentRuns =
                Convention.Counter("wilson.agent.runs", "{run}", "outcome")
                    .WithDescription("Tool agent runs.");

            /// <summary>Iterations per agent run.</summary>
            public static readonly Convention AgentIterations =
                Convention.Histogram("wilson.agent.iterations", "{iteration}", LatencyBuckets.Fast, "outcome")
                    .WithDescription("Tool agent iterations per run.");

            /// <summary>Tool executions, labeled by tool, transport (builtin|mcp), and outcome.</summary>
            public static readonly Convention Executions =
                Convention.Counter("wilson.tool.executions", "{execution}", "tool", "transport", "outcome")
                    .WithDescription("Tool executions.");

            /// <summary>Tool execution duration (seconds).</summary>
            public static readonly Convention Duration =
                Convention.Histogram("wilson.tool.duration", "s", LatencyBuckets.Fast, "tool", "transport", "outcome")
                    .WithDescription("Tool execution duration.");

            /// <summary>Concurrent in-flight tool executions.</summary>
            public static readonly Convention Active =
                Convention.UpDownCounter("wilson.tool.active", "{execution}")
                    .WithDescription("Concurrent in-flight tool executions.");

            /// <summary>Tool approval decisions, labeled by decision (approved|denied|timeout).</summary>
            public static readonly Convention Approvals =
                Convention.Counter("wilson.tool.approvals", "{decision}", "decision")
                    .WithDescription("Tool approval decisions.");
        }

        /// <summary>Model-runner health metrics.</summary>
        public static class ModelRunners
        {
            /// <summary>Runner health (1 healthy, 0 unhealthy), labeled by runner.</summary>
            public static readonly Convention Health =
                Convention.Gauge("wilson.model_runner.health", "1", "runner")
                    .WithDescription("Model runner health (1 healthy, 0 unhealthy).");

            /// <summary>Health checks performed, labeled by runner and outcome.</summary>
            public static readonly Convention HealthChecks =
                Convention.Counter("wilson.model_runner.health_checks", "{check}", "runner", "outcome")
                    .WithDescription("Model runner health checks performed.");

            /// <summary>Health-check duration (seconds).</summary>
            public static readonly Convention HealthCheckDuration =
                Convention.Histogram("wilson.model_runner.health_check.duration", "s", LatencyBuckets.Network, "runner")
                    .WithDescription("Model runner health-check duration.");

            /// <summary>Health state transitions, labeled by runner and target state.</summary>
            public static readonly Convention StateTransitions =
                Convention.Counter("wilson.model_runner.state_transitions", "{transition}", "runner", "to_state")
                    .WithDescription("Model runner health state transitions.");
        }

        /// <summary>Database metrics.</summary>
        public static class Db
        {
            /// <summary>Database client operation duration (seconds).</summary>
            public static readonly Convention Duration = SemConv.Db.ClientOperationDuration;

            /// <summary>Database operations, labeled by operation and outcome.</summary>
            public static readonly Convention Operations =
                Convention.Counter("wilson.db.operations", "{operation}", "operation", "outcome")
                    .WithDescription("Database operations executed.");
        }

        /// <summary>MCP subsystem metrics.</summary>
        public static class Mcp
        {
            /// <summary>MCP server connectivity (1 up, 0 down), labeled by server.</summary>
            public static readonly Convention ServersUp =
                Convention.Gauge("wilson.mcp.servers_up", "1", "server")
                    .WithDescription("MCP server connectivity (1 up, 0 down).");

            /// <summary>Tools loaded per MCP server.</summary>
            public static readonly Convention ToolsLoaded =
                Convention.Gauge("wilson.mcp.tools_loaded", "{tool}", "server")
                    .WithDescription("Tools loaded per MCP server.");

            /// <summary>MCP reloads, labeled by outcome.</summary>
            public static readonly Convention Reloads =
                Convention.Counter("wilson.mcp.reloads", "{reload}", "outcome")
                    .WithDescription("MCP server reloads.");

            /// <summary>MCP reload duration (seconds).</summary>
            public static readonly Convention ReloadDuration =
                Convention.Histogram("wilson.mcp.reload.duration", "s", LatencyBuckets.Network, "outcome")
                    .WithDescription("MCP reload duration.");
        }

        /// <summary>All conventions, registered for label-policy enforcement.</summary>
        public static Convention[] All { get; } = new[]
        {
            Http.Requests, Http.Duration, Http.Active,
            Chat.Requests, Chat.Duration, Chat.TimeToFirstToken, Chat.Tokens, Chat.Active, Chat.OllamaOps, Chat.OllamaDuration,
            Tools.AgentRuns, Tools.AgentIterations, Tools.Executions, Tools.Duration, Tools.Active, Tools.Approvals,
            ModelRunners.Health, ModelRunners.HealthChecks, ModelRunners.HealthCheckDuration, ModelRunners.StateTransitions,
            Db.Duration, Db.Operations,
            Mcp.ServersUp, Mcp.ToolsLoaded, Mcp.Reloads, Mcp.ReloadDuration
        }.Distinct().ToArray();
    }
}
