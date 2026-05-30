namespace Wilson.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Npgsql;
    using Wilson.Core.Models;
    using Wilson.Core.Settings;

    /// <summary>
    /// Provider-neutral database driver for SQLite and PostgreSQL.
    /// </summary>
    public sealed class DatabaseDriver
    {
        private readonly DatabaseSettings _Settings;
        private readonly bool _Postgres;

        /// <summary>
        /// Instantiate the database driver.
        /// </summary>
        public DatabaseDriver(DatabaseSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _Settings = settings;
            _Postgres = String.Equals(settings.Type, "Postgres", StringComparison.OrdinalIgnoreCase)
                || String.Equals(settings.Type, "Postgresql", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Initialize schema.
        /// </summary>
        public async Task InitializeAsync(CancellationToken token = default)
        {
            if (!_Postgres)
            {
                string? directory = Path.GetDirectoryName(_Settings.Filename);
                if (!String.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            }

            using (IDbConnection connection = CreateConnection())
            {
                connection.Open();
                string idColumn = _Postgres ? "BIGSERIAL PRIMARY KEY" : "INTEGER PRIMARY KEY AUTOINCREMENT";
                List<string> statements = new List<string>
                {
                    "CREATE TABLE IF NOT EXISTS tenants (rowid " + idColumn + ", id TEXT UNIQUE NOT NULL, name TEXT NOT NULL, active INTEGER NOT NULL, isprotected INTEGER NOT NULL, createdutc TEXT NOT NULL, lastupdateutc TEXT NOT NULL)",
                    "CREATE TABLE IF NOT EXISTS users (rowid " + idColumn + ", id TEXT UNIQUE NOT NULL, tenantid TEXT NOT NULL, firstname TEXT NOT NULL, lastname TEXT NOT NULL, email TEXT NOT NULL, isadmin INTEGER NOT NULL, istenantadmin INTEGER NOT NULL, active INTEGER NOT NULL, isprotected INTEGER NOT NULL, createdutc TEXT NOT NULL, lastupdateutc TEXT NOT NULL, UNIQUE(tenantid,email))",
                    "CREATE TABLE IF NOT EXISTS credentials (rowid " + idColumn + ", id TEXT UNIQUE NOT NULL, tenantid TEXT NOT NULL, userid TEXT NOT NULL, name TEXT NOT NULL, accesskey TEXT UNIQUE NOT NULL, secretlast4 TEXT NOT NULL, active INTEGER NOT NULL, isprotected INTEGER NOT NULL, createdutc TEXT NOT NULL, lastupdateutc TEXT NOT NULL, lastusedutc TEXT NULL)",
                    "CREATE TABLE IF NOT EXISTS conversations (rowid " + idColumn + ", id TEXT UNIQUE NOT NULL, tenantid TEXT NOT NULL, userid TEXT NOT NULL, title TEXT NOT NULL, runnerid TEXT NOT NULL, model TEXT NOT NULL, active INTEGER NOT NULL, createdutc TEXT NOT NULL, lastupdateutc TEXT NOT NULL)",
                    "CREATE TABLE IF NOT EXISTS messages (rowid " + idColumn + ", id TEXT UNIQUE NOT NULL, tenantid TEXT NOT NULL, conversationid TEXT NOT NULL, role TEXT NOT NULL, content TEXT NOT NULL, runnerid TEXT NOT NULL, model TEXT NOT NULL, tokenestimate INTEGER NOT NULL, createdutc TEXT NOT NULL)",
                    "CREATE TABLE IF NOT EXISTS feedback (rowid " + idColumn + ", id TEXT UNIQUE NOT NULL, tenantid TEXT NOT NULL, userid TEXT NOT NULL, conversationid TEXT NOT NULL, messageid TEXT NOT NULL, rating INTEGER NOT NULL, comment TEXT NOT NULL, createdutc TEXT NOT NULL)",
                    "CREATE TABLE IF NOT EXISTS requesthistory (rowid " + idColumn + ", id TEXT UNIQUE NOT NULL, tenantid TEXT NULL, userid TEXT NULL, method TEXT NOT NULL, path TEXT NOT NULL, statuscode INTEGER NOT NULL, durationms REAL NOT NULL, createdutc TEXT NOT NULL)"
                };

                foreach (string statement in statements)
                {
                    token.ThrowIfCancellationRequested();
                    using (IDbCommand command = connection.CreateCommand())
                    {
                        command.CommandText = statement;
                        command.ExecuteNonQuery();
                    }
                }

                EnsureColumn(connection, "messages", "timetofirsttokenms", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(connection, "messages", "streamingtimems", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(connection, "messages", "totaltimems", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(connection, "messages", "tokensused", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumn(connection, "requesthistory", "requestheaders", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "requestbody", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "responseheaders", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "responsebody", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "timetofirsttokenms", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(connection, "requesthistory", "streamingtimems", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(connection, "requesthistory", "totaltimems", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(connection, "requesthistory", "tokensused", "INTEGER NOT NULL DEFAULT 0");
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Seed first-run records if missing.
        /// </summary>
        public async Task SeedAsync(SeedSettings seed, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(seed);
            List<Tenant> tenants = await GetTenantsAsync(token).ConfigureAwait(false);
            Tenant tenant = tenants.FirstOrDefault(item => String.Equals(item.Name, seed.TenantName, StringComparison.OrdinalIgnoreCase)) ?? new Tenant { Name = seed.TenantName, IsProtected = true };
            if (!tenants.Any(item => item.Id == tenant.Id)) await CreateTenantAsync(tenant, token).ConfigureAwait(false);

            User? user = await GetUserByEmailAsync(tenant.Id, seed.UserEmail, token).ConfigureAwait(false);
            if (user == null)
            {
                user = new User
                {
                    TenantId = tenant.Id,
                    FirstName = seed.FirstName,
                    LastName = seed.LastName,
                    Email = seed.UserEmail,
                    IsAdmin = true,
                    IsTenantAdmin = true,
                    IsProtected = true
                };
                await CreateUserAsync(user, token).ConfigureAwait(false);
            }

            Credential? credential = await GetCredentialByAccessKeyAsync(seed.AccessKey, token).ConfigureAwait(false);
            if (credential == null)
            {
                credential = new Credential
                {
                    TenantId = tenant.Id,
                    UserId = user.Id,
                    Name = "Seeded dashboard access key",
                    AccessKey = seed.AccessKey,
                    SecretLast4 = seed.AccessKey.Length >= 4 ? seed.AccessKey.Substring(seed.AccessKey.Length - 4) : seed.AccessKey,
                    IsProtected = true
                };
                await CreateCredentialAsync(credential, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Create a tenant.
        /// </summary>
        public async Task CreateTenantAsync(Tenant item, CancellationToken token = default)
        {
            await ExecuteAsync("INSERT INTO tenants (id,name,active,isprotected,createdutc,lastupdateutc) VALUES (@id,@name,@active,@isprotected,@createdutc,@lastupdateutc)", AddTenant, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a tenant.
        /// </summary>
        public async Task UpdateTenantAsync(Tenant item, CancellationToken token = default)
        {
            item.LastUpdateUtc = DateTime.UtcNow;
            await ExecuteAsync("UPDATE tenants SET name=@name, active=@active, isprotected=@isprotected, lastupdateutc=@lastupdateutc WHERE id=@id", AddTenant, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a tenant.
        /// </summary>
        public async Task DeleteTenantAsync(string id, CancellationToken token = default)
        {
            await ExecuteSimpleAsync("DELETE FROM tenants WHERE id=@id", command => Add(command, "@id", id), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get tenants.
        /// </summary>
        public async Task<List<Tenant>> GetTenantsAsync(CancellationToken token = default)
        {
            return await QueryAsync("SELECT * FROM tenants ORDER BY createdutc DESC", ReadTenant, null, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get tenant by identifier.
        /// </summary>
        public async Task<Tenant?> GetTenantAsync(string id, CancellationToken token = default)
        {
            List<Tenant> items = await QueryAsync("SELECT * FROM tenants WHERE id=@id", ReadTenant, command => Add(command, "@id", id), token).ConfigureAwait(false);
            return items.FirstOrDefault();
        }

        /// <summary>
        /// Create a user.
        /// </summary>
        public async Task CreateUserAsync(User item, CancellationToken token = default)
        {
            await ExecuteAsync("INSERT INTO users (id,tenantid,firstname,lastname,email,isadmin,istenantadmin,active,isprotected,createdutc,lastupdateutc) VALUES (@id,@tenantid,@firstname,@lastname,@email,@isadmin,@istenantadmin,@active,@isprotected,@createdutc,@lastupdateutc)", AddUser, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a user.
        /// </summary>
        public async Task UpdateUserAsync(User item, CancellationToken token = default)
        {
            item.LastUpdateUtc = DateTime.UtcNow;
            await ExecuteAsync("UPDATE users SET firstname=@firstname, lastname=@lastname, email=@email, isadmin=@isadmin, istenantadmin=@istenantadmin, active=@active, isprotected=@isprotected, lastupdateutc=@lastupdateutc WHERE id=@id AND tenantid=@tenantid", AddUser, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a user.
        /// </summary>
        public async Task DeleteUserAsync(string tenantId, string id, CancellationToken token = default)
        {
            await ExecuteSimpleAsync("DELETE FROM users WHERE tenantid=@tenantid AND id=@id", command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get users for a tenant or all tenants.
        /// </summary>
        public async Task<List<User>> GetUsersAsync(string? tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return await QueryAsync("SELECT * FROM users ORDER BY createdutc DESC", ReadUser, null, token).ConfigureAwait(false);
            return await QueryAsync("SELECT * FROM users WHERE tenantid=@tenantid ORDER BY createdutc DESC", ReadUser, command => Add(command, "@tenantid", tenantId), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get user by identifier.
        /// </summary>
        public async Task<User?> GetUserAsync(string tenantId, string id, CancellationToken token = default)
        {
            List<User> items = await QueryAsync("SELECT * FROM users WHERE tenantid=@tenantid AND id=@id", ReadUser, command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
            return items.FirstOrDefault();
        }

        /// <summary>
        /// Get user by email.
        /// </summary>
        public async Task<User?> GetUserByEmailAsync(string tenantId, string email, CancellationToken token = default)
        {
            List<User> items = await QueryAsync("SELECT * FROM users WHERE tenantid=@tenantid AND lower(email)=lower(@email)", ReadUser, command => { Add(command, "@tenantid", tenantId); Add(command, "@email", email); }, token).ConfigureAwait(false);
            return items.FirstOrDefault();
        }

        /// <summary>
        /// Create a credential.
        /// </summary>
        public async Task CreateCredentialAsync(Credential item, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(item.SecretLast4)) item.SecretLast4 = item.AccessKey.Length >= 4 ? item.AccessKey.Substring(item.AccessKey.Length - 4) : item.AccessKey;
            await ExecuteAsync("INSERT INTO credentials (id,tenantid,userid,name,accesskey,secretlast4,active,isprotected,createdutc,lastupdateutc,lastusedutc) VALUES (@id,@tenantid,@userid,@name,@accesskey,@secretlast4,@active,@isprotected,@createdutc,@lastupdateutc,@lastusedutc)", AddCredential, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a credential.
        /// </summary>
        public async Task UpdateCredentialAsync(Credential item, CancellationToken token = default)
        {
            item.LastUpdateUtc = DateTime.UtcNow;
            await ExecuteAsync("UPDATE credentials SET name=@name, active=@active, isprotected=@isprotected, lastupdateutc=@lastupdateutc, lastusedutc=@lastusedutc WHERE id=@id AND tenantid=@tenantid", AddCredential, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a credential.
        /// </summary>
        public async Task DeleteCredentialAsync(string tenantId, string id, CancellationToken token = default)
        {
            await ExecuteSimpleAsync("DELETE FROM credentials WHERE tenantid=@tenantid AND id=@id", command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get credentials.
        /// </summary>
        public async Task<List<Credential>> GetCredentialsAsync(string? tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return await QueryAsync("SELECT * FROM credentials ORDER BY createdutc DESC", ReadCredential, null, token).ConfigureAwait(false);
            return await QueryAsync("SELECT * FROM credentials WHERE tenantid=@tenantid ORDER BY createdutc DESC", ReadCredential, command => Add(command, "@tenantid", tenantId), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get credential by access key.
        /// </summary>
        public async Task<Credential?> GetCredentialByAccessKeyAsync(string accessKey, CancellationToken token = default)
        {
            List<Credential> items = await QueryAsync("SELECT * FROM credentials WHERE accesskey=@accesskey", ReadCredential, command => Add(command, "@accesskey", accessKey), token).ConfigureAwait(false);
            return items.FirstOrDefault();
        }

        /// <summary>
        /// Get credential by identifier.
        /// </summary>
        public async Task<Credential?> GetCredentialAsync(string tenantId, string id, CancellationToken token = default)
        {
            List<Credential> items = await QueryAsync("SELECT * FROM credentials WHERE tenantid=@tenantid AND id=@id", ReadCredential, command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
            return items.FirstOrDefault();
        }

        /// <summary>
        /// Create a conversation.
        /// </summary>
        public async Task CreateConversationAsync(Conversation item, CancellationToken token = default)
        {
            await ExecuteAsync("INSERT INTO conversations (id,tenantid,userid,title,runnerid,model,active,createdutc,lastupdateutc) VALUES (@id,@tenantid,@userid,@title,@runnerid,@model,@active,@createdutc,@lastupdateutc)", AddConversation, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a conversation.
        /// </summary>
        public async Task UpdateConversationAsync(Conversation item, CancellationToken token = default)
        {
            item.LastUpdateUtc = DateTime.UtcNow;
            await ExecuteAsync("UPDATE conversations SET title=@title, runnerid=@runnerid, model=@model, active=@active, lastupdateutc=@lastupdateutc WHERE tenantid=@tenantid AND id=@id", AddConversation, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a conversation and associated chat records.
        /// </summary>
        public async Task DeleteConversationAsync(string tenantId, string id, CancellationToken token = default)
        {
            await ExecuteSimpleAsync("DELETE FROM feedback WHERE tenantid=@tenantid AND conversationid=@id", command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
            await ExecuteSimpleAsync("DELETE FROM messages WHERE tenantid=@tenantid AND conversationid=@id", command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
            await ExecuteSimpleAsync("DELETE FROM conversations WHERE tenantid=@tenantid AND id=@id", command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Read a conversation.
        /// </summary>
        public async Task<Conversation?> GetConversationAsync(string tenantId, string id, CancellationToken token = default)
        {
            List<Conversation> items = await QueryAsync("SELECT * FROM conversations WHERE tenantid=@tenantid AND id=@id", ReadConversation, command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
            return items.FirstOrDefault();
        }

        /// <summary>
        /// Enumerate conversations.
        /// </summary>
        public async Task<List<Conversation>> GetConversationsAsync(string tenantId, string? userId, bool admin, CancellationToken token = default)
        {
            if (admin) return await QueryAsync("SELECT * FROM conversations WHERE tenantid=@tenantid ORDER BY lastupdateutc DESC", ReadConversation, command => Add(command, "@tenantid", tenantId), token).ConfigureAwait(false);
            return await QueryAsync("SELECT * FROM conversations WHERE tenantid=@tenantid AND userid=@userid ORDER BY lastupdateutc DESC", ReadConversation, command => { Add(command, "@tenantid", tenantId); Add(command, "@userid", userId ?? ""); }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create a chat message.
        /// </summary>
        public async Task CreateMessageAsync(ChatMessage item, CancellationToken token = default)
        {
            await ExecuteAsync("INSERT INTO messages (id,tenantid,conversationid,role,content,runnerid,model,tokenestimate,timetofirsttokenms,streamingtimems,totaltimems,tokensused,createdutc) VALUES (@id,@tenantid,@conversationid,@role,@content,@runnerid,@model,@tokenestimate,@timetofirsttokenms,@streamingtimems,@totaltimems,@tokensused,@createdutc)", AddMessage, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Read conversation messages.
        /// </summary>
        public async Task<List<ChatMessage>> GetMessagesAsync(string tenantId, string conversationId, CancellationToken token = default)
        {
            return await QueryAsync("SELECT * FROM messages WHERE tenantid=@tenantid AND conversationid=@conversationid ORDER BY createdutc ASC", ReadMessage, command => { Add(command, "@tenantid", tenantId); Add(command, "@conversationid", conversationId); }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create feedback.
        /// </summary>
        public async Task CreateFeedbackAsync(Feedback item, CancellationToken token = default)
        {
            await ExecuteAsync("INSERT INTO feedback (id,tenantid,userid,conversationid,messageid,rating,comment,createdutc) VALUES (@id,@tenantid,@userid,@conversationid,@messageid,@rating,@comment,@createdutc)", AddFeedback, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate feedback.
        /// </summary>
        public async Task<List<Feedback>> GetFeedbackAsync(string? tenantId, CancellationToken token = default)
        {
            string sql = "SELECT feedback.*, messages.timetofirsttokenms AS feedback_timetofirsttokenms, messages.streamingtimems AS feedback_streamingtimems, messages.totaltimems AS feedback_totaltimems, messages.tokensused AS feedback_tokensused FROM feedback LEFT JOIN messages ON feedback.tenantid=messages.tenantid AND feedback.messageid=messages.id";
            if (String.IsNullOrWhiteSpace(tenantId)) return await QueryAsync(sql + " ORDER BY feedback.createdutc DESC", ReadFeedback, null, token).ConfigureAwait(false);
            return await QueryAsync(sql + " WHERE feedback.tenantid=@tenantid ORDER BY feedback.createdutc DESC", ReadFeedback, command => Add(command, "@tenantid", tenantId), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create request history.
        /// </summary>
        public async Task CreateRequestHistoryAsync(RequestHistoryEntry item, CancellationToken token = default)
        {
            await ExecuteAsync("INSERT INTO requesthistory (id,tenantid,userid,method,path,statuscode,durationms,requestheaders,requestbody,responseheaders,responsebody,timetofirsttokenms,streamingtimems,totaltimems,tokensused,createdutc) VALUES (@id,@tenantid,@userid,@method,@path,@statuscode,@durationms,@requestheaders,@requestbody,@responseheaders,@responsebody,@timetofirsttokenms,@streamingtimems,@totaltimems,@tokensused,@createdutc)", AddRequestHistory, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate request history.
        /// </summary>
        public async Task<List<RequestHistoryEntry>> GetRequestHistoryAsync(string? tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return await QueryAsync("SELECT * FROM requesthistory ORDER BY createdutc DESC LIMIT 250", ReadRequestHistory, null, token).ConfigureAwait(false);
            return await QueryAsync("SELECT * FROM requesthistory WHERE tenantid=@tenantid ORDER BY createdutc DESC LIMIT 250", ReadRequestHistory, command => Add(command, "@tenantid", tenantId), token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete request history.
        /// </summary>
        public async Task DeleteRequestHistoryAsync(string? tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) await ExecuteSimpleAsync("DELETE FROM requesthistory WHERE id=@id", command => Add(command, "@id", id), token).ConfigureAwait(false);
            else await ExecuteSimpleAsync("DELETE FROM requesthistory WHERE tenantid=@tenantid AND id=@id", command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Summarize request history.
        /// </summary>
        public async Task<RequestHistorySummary> SummarizeRequestHistoryAsync(string? tenantId, DateTime fromUtc, DateTime toUtc, int bucketMinutes, CancellationToken token = default)
        {
            List<RequestHistoryEntry> entries = await GetRequestHistoryAsync(tenantId, token).ConfigureAwait(false);
            entries = entries.Where(item => item.CreatedUtc >= fromUtc && item.CreatedUtc <= toUtc).ToList();
            RequestHistorySummary summary = new RequestHistorySummary();
            summary.TotalCount = entries.Count;
            summary.TotalSuccess = entries.Count(item => item.StatusCode < 400);
            summary.TotalFailure = entries.Count(item => item.StatusCode >= 400);
            summary.AverageDurationMs = entries.Count > 0 ? entries.Average(item => item.DurationMs) : 0;
            DateTime cursor = fromUtc;
            while (cursor < toUtc)
            {
                DateTime end = cursor.AddMinutes(bucketMinutes);
                List<RequestHistoryEntry> bucket = entries.Where(item => item.CreatedUtc >= cursor && item.CreatedUtc < end).ToList();
                summary.Buckets.Add(new RequestHistoryBucket
                {
                    BucketStartUtc = cursor,
                    BucketEndUtc = end,
                    SuccessCount = bucket.Count(item => item.StatusCode < 400),
                    FailureCount = bucket.Count(item => item.StatusCode >= 400),
                    AverageDurationMs = bucket.Count > 0 ? bucket.Average(item => item.DurationMs) : 0
                });
                cursor = end;
            }
            return summary;
        }

        private IDbConnection CreateConnection()
        {
            if (_Postgres) return new NpgsqlConnection(_Settings.ConnectionString);
            return new SqliteConnection("Data Source=" + _Settings.Filename);
        }

        private static void EnsureColumn(IDbConnection connection, string table, string column, string definition)
        {
            try
            {
                using IDbCommand command = connection.CreateCommand();
                command.CommandText = "ALTER TABLE " + table + " ADD COLUMN " + column + " " + definition;
                command.ExecuteNonQuery();
            }
            catch
            {
            }
        }

        private async Task ExecuteAsync<T>(string sql, Action<IDbCommand, T> bind, T item, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (IDbConnection connection = CreateConnection())
            {
                connection.Open();
                using (IDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    bind(command, item);
                    command.ExecuteNonQuery();
                }
            }
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private async Task ExecuteSimpleAsync(string sql, Action<IDbCommand> bind, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (IDbConnection connection = CreateConnection())
            {
                connection.Open();
                using (IDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    bind(command);
                    command.ExecuteNonQuery();
                }
            }
            await Task.CompletedTask.ConfigureAwait(false);
        }

        private async Task<List<T>> QueryAsync<T>(string sql, Func<IDataRecord, T> read, Action<IDbCommand>? bind, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            List<T> items = new List<T>();
            using (IDbConnection connection = CreateConnection())
            {
                connection.Open();
                using (IDbCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    bind?.Invoke(command);
                    using (IDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(read(reader));
                        }
                    }
                }
            }
            return await Task.FromResult(items).ConfigureAwait(false);
        }

        private static void Add(IDbCommand command, string name, object? value)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private static string S(IDataRecord record, string name)
        {
            object value = record[name];
            return value == DBNull.Value ? String.Empty : value.ToString() ?? String.Empty;
        }

        private static bool B(IDataRecord record, string name)
        {
            return Convert.ToInt32(record[name]) == 1;
        }

        private static DateTime D(IDataRecord record, string name)
        {
            return DateTime.Parse(S(record, name)).ToUniversalTime();
        }

        private static DateTime? N(IDataRecord record, string name)
        {
            string value = S(record, name);
            if (String.IsNullOrWhiteSpace(value)) return null;
            return DateTime.Parse(value).ToUniversalTime();
        }

        private static double R(IDataRecord record, string name)
        {
            object value = record[name];
            return value == DBNull.Value ? 0 : Convert.ToDouble(value);
        }

        private static string Iso(DateTime value)
        {
            return value.ToUniversalTime().ToString("O");
        }

        private static void AddTenant(IDbCommand command, Tenant item)
        {
            Add(command, "@id", item.Id); Add(command, "@name", item.Name); Add(command, "@active", item.Active ? 1 : 0); Add(command, "@isprotected", item.IsProtected ? 1 : 0); Add(command, "@createdutc", Iso(item.CreatedUtc)); Add(command, "@lastupdateutc", Iso(item.LastUpdateUtc));
        }

        private static Tenant ReadTenant(IDataRecord record)
        {
            return new Tenant { Id = S(record, "id"), Name = S(record, "name"), Active = B(record, "active"), IsProtected = B(record, "isprotected"), CreatedUtc = D(record, "createdutc"), LastUpdateUtc = D(record, "lastupdateutc") };
        }

        private static void AddUser(IDbCommand command, User item)
        {
            Add(command, "@id", item.Id); Add(command, "@tenantid", item.TenantId); Add(command, "@firstname", item.FirstName); Add(command, "@lastname", item.LastName); Add(command, "@email", item.Email); Add(command, "@isadmin", item.IsAdmin ? 1 : 0); Add(command, "@istenantadmin", item.IsTenantAdmin ? 1 : 0); Add(command, "@active", item.Active ? 1 : 0); Add(command, "@isprotected", item.IsProtected ? 1 : 0); Add(command, "@createdutc", Iso(item.CreatedUtc)); Add(command, "@lastupdateutc", Iso(item.LastUpdateUtc));
        }

        private static User ReadUser(IDataRecord record)
        {
            return new User { Id = S(record, "id"), TenantId = S(record, "tenantid"), FirstName = S(record, "firstname"), LastName = S(record, "lastname"), Email = S(record, "email"), IsAdmin = B(record, "isadmin"), IsTenantAdmin = B(record, "istenantadmin"), Active = B(record, "active"), IsProtected = B(record, "isprotected"), CreatedUtc = D(record, "createdutc"), LastUpdateUtc = D(record, "lastupdateutc") };
        }

        private static void AddCredential(IDbCommand command, Credential item)
        {
            Add(command, "@id", item.Id); Add(command, "@tenantid", item.TenantId); Add(command, "@userid", item.UserId); Add(command, "@name", item.Name); Add(command, "@accesskey", item.AccessKey); Add(command, "@secretlast4", item.SecretLast4); Add(command, "@active", item.Active ? 1 : 0); Add(command, "@isprotected", item.IsProtected ? 1 : 0); Add(command, "@createdutc", Iso(item.CreatedUtc)); Add(command, "@lastupdateutc", Iso(item.LastUpdateUtc)); Add(command, "@lastusedutc", item.LastUsedUtc.HasValue ? Iso(item.LastUsedUtc.Value) : null);
        }

        private static Credential ReadCredential(IDataRecord record)
        {
            return new Credential { Id = S(record, "id"), TenantId = S(record, "tenantid"), UserId = S(record, "userid"), Name = S(record, "name"), AccessKey = S(record, "accesskey"), SecretLast4 = S(record, "secretlast4"), Active = B(record, "active"), IsProtected = B(record, "isprotected"), CreatedUtc = D(record, "createdutc"), LastUpdateUtc = D(record, "lastupdateutc"), LastUsedUtc = N(record, "lastusedutc") };
        }

        private static void AddConversation(IDbCommand command, Conversation item)
        {
            Add(command, "@id", item.Id); Add(command, "@tenantid", item.TenantId); Add(command, "@userid", item.UserId); Add(command, "@title", item.Title); Add(command, "@runnerid", item.RunnerId); Add(command, "@model", item.Model); Add(command, "@active", item.Active ? 1 : 0); Add(command, "@createdutc", Iso(item.CreatedUtc)); Add(command, "@lastupdateutc", Iso(item.LastUpdateUtc));
        }

        private static Conversation ReadConversation(IDataRecord record)
        {
            return new Conversation { Id = S(record, "id"), TenantId = S(record, "tenantid"), UserId = S(record, "userid"), Title = S(record, "title"), RunnerId = S(record, "runnerid"), Model = S(record, "model"), Active = B(record, "active"), CreatedUtc = D(record, "createdutc"), LastUpdateUtc = D(record, "lastupdateutc") };
        }

        private static void AddMessage(IDbCommand command, ChatMessage item)
        {
            Add(command, "@id", item.Id); Add(command, "@tenantid", item.TenantId); Add(command, "@conversationid", item.ConversationId); Add(command, "@role", item.Role); Add(command, "@content", item.Content); Add(command, "@runnerid", item.RunnerId); Add(command, "@model", item.Model); Add(command, "@tokenestimate", item.TokenEstimate); Add(command, "@timetofirsttokenms", item.TimeToFirstTokenMs); Add(command, "@streamingtimems", item.StreamingTimeMs); Add(command, "@totaltimems", item.TotalTimeMs); Add(command, "@tokensused", item.TokensUsed); Add(command, "@createdutc", Iso(item.CreatedUtc));
        }

        private static ChatMessage ReadMessage(IDataRecord record)
        {
            return new ChatMessage { Id = S(record, "id"), TenantId = S(record, "tenantid"), ConversationId = S(record, "conversationid"), Role = S(record, "role"), Content = S(record, "content"), RunnerId = S(record, "runnerid"), Model = S(record, "model"), TokenEstimate = Convert.ToInt32(record["tokenestimate"]), TimeToFirstTokenMs = R(record, "timetofirsttokenms"), StreamingTimeMs = R(record, "streamingtimems"), TotalTimeMs = R(record, "totaltimems"), TokensUsed = Convert.ToInt32(R(record, "tokensused")), CreatedUtc = D(record, "createdutc") };
        }

        private static void AddFeedback(IDbCommand command, Feedback item)
        {
            Add(command, "@id", item.Id); Add(command, "@tenantid", item.TenantId); Add(command, "@userid", item.UserId); Add(command, "@conversationid", item.ConversationId); Add(command, "@messageid", item.MessageId); Add(command, "@rating", item.Rating); Add(command, "@comment", item.Comment); Add(command, "@createdutc", Iso(item.CreatedUtc));
        }

        private static Feedback ReadFeedback(IDataRecord record)
        {
            return new Feedback { Id = S(record, "id"), TenantId = S(record, "tenantid"), UserId = S(record, "userid"), ConversationId = S(record, "conversationid"), MessageId = S(record, "messageid"), Rating = Convert.ToInt32(record["rating"]), Comment = S(record, "comment"), TimeToFirstTokenMs = R(record, "feedback_timetofirsttokenms"), StreamingTimeMs = R(record, "feedback_streamingtimems"), TotalTimeMs = R(record, "feedback_totaltimems"), TokensUsed = Convert.ToInt32(R(record, "feedback_tokensused")), CreatedUtc = D(record, "createdutc") };
        }

        private static void AddRequestHistory(IDbCommand command, RequestHistoryEntry item)
        {
            Add(command, "@id", item.Id); Add(command, "@tenantid", item.TenantId); Add(command, "@userid", item.UserId); Add(command, "@method", item.Method); Add(command, "@path", item.Path); Add(command, "@statuscode", item.StatusCode); Add(command, "@durationms", item.DurationMs); Add(command, "@requestheaders", item.RequestHeaders); Add(command, "@requestbody", item.RequestBody); Add(command, "@responseheaders", item.ResponseHeaders); Add(command, "@responsebody", item.ResponseBody); Add(command, "@timetofirsttokenms", item.TimeToFirstTokenMs); Add(command, "@streamingtimems", item.StreamingTimeMs); Add(command, "@totaltimems", item.TotalTimeMs); Add(command, "@tokensused", item.TokensUsed); Add(command, "@createdutc", Iso(item.CreatedUtc));
        }

        private static RequestHistoryEntry ReadRequestHistory(IDataRecord record)
        {
            return new RequestHistoryEntry { Id = S(record, "id"), TenantId = S(record, "tenantid"), UserId = S(record, "userid"), Method = S(record, "method"), Path = S(record, "path"), StatusCode = Convert.ToInt32(record["statuscode"]), DurationMs = Convert.ToDouble(record["durationms"]), RequestHeaders = S(record, "requestheaders"), RequestBody = S(record, "requestbody"), ResponseHeaders = S(record, "responseheaders"), ResponseBody = S(record, "responsebody"), TimeToFirstTokenMs = R(record, "timetofirsttokenms"), StreamingTimeMs = R(record, "streamingtimems"), TotalTimeMs = R(record, "totaltimems"), TokensUsed = Convert.ToInt32(R(record, "tokensused")), CreatedUtc = D(record, "createdutc") };
        }
    }
}
