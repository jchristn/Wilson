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
                    "CREATE TABLE IF NOT EXISTS requesthistory (rowid " + idColumn + ", id TEXT UNIQUE NOT NULL, tenantid TEXT NULL, userid TEXT NULL, method TEXT NOT NULL, path TEXT NOT NULL, statuscode INTEGER NOT NULL, durationms REAL NOT NULL, createdutc TEXT NOT NULL)",
                    "CREATE TABLE IF NOT EXISTS prompttemplates (rowid " + idColumn + ", id TEXT UNIQUE NOT NULL, tenantid TEXT NOT NULL, kind TEXT NOT NULL, name TEXT NOT NULL, description TEXT NOT NULL, content TEXT NOT NULL, isdefault INTEGER NOT NULL, isprotected INTEGER NOT NULL, active INTEGER NOT NULL, createdbyuserid TEXT NOT NULL, updatedbyuserid TEXT NOT NULL, createdutc TEXT NOT NULL, lastupdateutc TEXT NOT NULL)",
                    "CREATE TABLE IF NOT EXISTS toolruns (rowid " + idColumn + ", id TEXT UNIQUE NOT NULL, tenantid TEXT NOT NULL, userid TEXT NOT NULL, conversationid TEXT NOT NULL, runnerid TEXT NOT NULL, model TEXT NOT NULL, status TEXT NOT NULL, startedutc TEXT NOT NULL, completedutc TEXT NULL, elapsedms REAL NOT NULL, iterationcount INTEGER NOT NULL, toolcallcount INTEGER NOT NULL, errorcount INTEGER NOT NULL, createdutc TEXT NOT NULL)",
                    "CREATE TABLE IF NOT EXISTS toolcalls (rowid " + idColumn + ", id TEXT UNIQUE NOT NULL, tenantid TEXT NOT NULL, userid TEXT NOT NULL, conversationid TEXT NOT NULL, runid TEXT NOT NULL, requesthistoryid TEXT NULL, traceid TEXT NULL, origin TEXT NULL, assistantmessageid TEXT NULL, providertoolcallid TEXT NULL, toolcallid TEXT NOT NULL, toolname TEXT NOT NULL, iteration INTEGER NOT NULL, sequencenumber INTEGER NOT NULL, status TEXT NOT NULL, approvalpolicy TEXT NOT NULL, approvedbyuserid TEXT NULL, argumentsjson TEXT NOT NULL, resultjson TEXT NOT NULL, resultsummaryjson TEXT NOT NULL, resultpreview TEXT NOT NULL, success INTEGER NOT NULL, denied INTEGER NOT NULL, truncated INTEGER NOT NULL, outputcharacters INTEGER NOT NULL, inputbytes INTEGER NOT NULL, outputbytes INTEGER NOT NULL, errortype TEXT NULL, errorcode TEXT NULL, errormessage TEXT NULL, provider TEXT NULL, model TEXT NULL, startedutc TEXT NOT NULL, completedutc TEXT NULL, elapsedms REAL NOT NULL, active INTEGER NOT NULL, createdutc TEXT NOT NULL, updatedutc TEXT NOT NULL)"
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
                EnsureColumn(connection, "messages", "runid", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "messages", "toolcallsjson", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "messages", "toolcallid", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "messages", "metadatajson", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "requestheaders", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "requestbody", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "responseheaders", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "responsebody", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "timetofirsttokenms", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(connection, "requesthistory", "streamingtimems", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(connection, "requesthistory", "totaltimems", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(connection, "requesthistory", "tokensused", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumn(connection, "requesthistory", "toolrunid", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "toolcallcount", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumn(connection, "requesthistory", "toolelapsedms", "REAL NOT NULL DEFAULT 0");
                EnsureColumn(connection, "requesthistory", "agentiterations", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumn(connection, "requesthistory", "systempromptid", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "systempromptname", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "systempromptdefault", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumn(connection, "requesthistory", "systemprompthash", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "toolpromptid", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "toolpromptname", "TEXT NOT NULL DEFAULT ''");
                EnsureColumn(connection, "requesthistory", "toolpromptdefault", "INTEGER NOT NULL DEFAULT 0");
                EnsureColumn(connection, "requesthistory", "toolprompthash", "TEXT NOT NULL DEFAULT ''");
                EnsureIndex(connection, "CREATE INDEX IF NOT EXISTS idx_prompttemplates_tenant_kind_active ON prompttemplates (tenantid,kind,active)");
                EnsureIndex(connection, "CREATE INDEX IF NOT EXISTS idx_prompttemplates_tenant_kind_default ON prompttemplates (tenantid,kind,isdefault)");
                EnsureIndex(connection, "CREATE INDEX IF NOT EXISTS idx_prompttemplates_tenant_name ON prompttemplates (tenantid,name)");
                EnsureIndex(connection, "CREATE INDEX IF NOT EXISTS idx_prompttemplates_tenant_updated ON prompttemplates (tenantid,lastupdateutc)");
                EnsureIndex(connection, "CREATE INDEX IF NOT EXISTS idx_toolruns_tenant_conversation_created ON toolruns (tenantid,conversationid,createdutc)");
                EnsureIndex(connection, "CREATE INDEX IF NOT EXISTS idx_toolcalls_tenant_conversation_run ON toolcalls (tenantid,conversationid,runid)");
                EnsureIndex(connection, "CREATE INDEX IF NOT EXISTS idx_toolcalls_tenant_assistantmessage ON toolcalls (tenantid,assistantmessageid)");
                EnsureIndex(connection, "CREATE INDEX IF NOT EXISTS idx_toolcalls_tenant_trace ON toolcalls (tenantid,traceid)");
                EnsureIndex(connection, "CREATE INDEX IF NOT EXISTS idx_toolcalls_tenant_requesthistory ON toolcalls (tenantid,requesthistoryid)");
                EnsureIndex(connection, "CREATE INDEX IF NOT EXISTS idx_toolcalls_tenant_toolname_created ON toolcalls (tenantid,toolname,createdutc)");
                EnsureIndex(connection, "CREATE INDEX IF NOT EXISTS idx_toolcalls_tenant_success_created ON toolcalls (tenantid,success,createdutc)");
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
        /// Ensure every tenant has default prompt templates.
        /// </summary>
        public async Task EnsureDefaultPromptTemplatesAsync(CancellationToken token = default)
        {
            List<Tenant> tenants = await GetTenantsAsync(token).ConfigureAwait(false);
            foreach (Tenant tenant in tenants)
            {
                await EnsureDefaultPromptTemplatesAsync(tenant.Id, token).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Ensure a tenant has default prompt templates.
        /// </summary>
        public async Task EnsureDefaultPromptTemplatesAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            await EnsureDefaultPromptTemplateAsync(tenantId, PromptTemplateKind.System, PromptTemplateDefaults.DefaultSystemPromptName, "Default Wilson behavior for chat responses.", PromptTemplateDefaults.DefaultSystemPromptContent, token).ConfigureAwait(false);
            await EnsureDefaultPromptTemplateAsync(tenantId, PromptTemplateKind.Tool, PromptTemplateDefaults.DefaultToolPromptName, "Default Wilson instructions for tool-capable chat.", PromptTemplateDefaults.DefaultToolPromptContent, token).ConfigureAwait(false);
        }

        private async Task EnsureDefaultPromptTemplateAsync(string tenantId, PromptTemplateKind kind, string name, string description, string content, CancellationToken token)
        {
            PromptTemplate? existing = await GetDefaultPromptTemplateAsync(tenantId, kind, token).ConfigureAwait(false);
            if (existing != null) return;
            PromptTemplate prompt = new PromptTemplate
            {
                TenantId = tenantId,
                Kind = kind,
                Name = name,
                Description = description,
                Content = content,
                IsDefault = true,
                IsProtected = true,
                Active = true,
                CreatedByUserId = "system",
                UpdatedByUserId = "system"
            };
            await CreatePromptTemplateAsync(prompt, token).ConfigureAwait(false);
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
        /// Create a prompt template.
        /// </summary>
        public async Task CreatePromptTemplateAsync(PromptTemplate item, CancellationToken token = default)
        {
            ValidatePromptTemplate(item);
            if (item.IsDefault) await ClearDefaultPromptTemplateAsync(item.TenantId, item.Kind, token).ConfigureAwait(false);
            await ExecuteAsync("INSERT INTO prompttemplates (id,tenantid,kind,name,description,content,isdefault,isprotected,active,createdbyuserid,updatedbyuserid,createdutc,lastupdateutc) VALUES (@id,@tenantid,@kind,@name,@description,@content,@isdefault,@isprotected,@active,@createdbyuserid,@updatedbyuserid,@createdutc,@lastupdateutc)", AddPromptTemplate, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a prompt template.
        /// </summary>
        public async Task UpdatePromptTemplateAsync(PromptTemplate item, CancellationToken token = default)
        {
            ValidatePromptTemplate(item);
            item.LastUpdateUtc = DateTime.UtcNow;
            if (item.IsDefault) await ClearDefaultPromptTemplateAsync(item.TenantId, item.Kind, token).ConfigureAwait(false);
            await ExecuteAsync("UPDATE prompttemplates SET kind=@kind, name=@name, description=@description, content=@content, isdefault=@isdefault, isprotected=@isprotected, active=@active, updatedbyuserid=@updatedbyuserid, lastupdateutc=@lastupdateutc WHERE tenantid=@tenantid AND id=@id", AddPromptTemplate, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete a prompt template.
        /// </summary>
        public async Task DeletePromptTemplateAsync(string tenantId, string id, CancellationToken token = default)
        {
            PromptTemplate? existing = await GetPromptTemplateAsync(tenantId, id, token).ConfigureAwait(false);
            if (existing == null) return;
            if (existing.IsDefault || existing.IsProtected) throw new InvalidOperationException("Default or protected prompt templates cannot be deleted.");
            await ExecuteSimpleAsync("DELETE FROM prompttemplates WHERE tenantid=@tenantid AND id=@id", command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get prompt templates.
        /// </summary>
        public async Task<List<PromptTemplate>> GetPromptTemplatesAsync(string? tenantId, PromptTemplateKind? kind = null, bool includeInactive = false, CancellationToken token = default)
        {
            List<string> filters = new List<string>();
            if (!String.IsNullOrWhiteSpace(tenantId)) filters.Add("tenantid=@tenantid");
            if (kind.HasValue) filters.Add("kind=@kind");
            if (!includeInactive) filters.Add("active=1");
            string sql = "SELECT * FROM prompttemplates" + (filters.Count > 0 ? " WHERE " + String.Join(" AND ", filters) : String.Empty) + " ORDER BY kind ASC, isdefault DESC, name ASC";
            return await QueryAsync(sql, ReadPromptTemplate, command =>
            {
                if (!String.IsNullOrWhiteSpace(tenantId)) Add(command, "@tenantid", tenantId);
                if (kind.HasValue) Add(command, "@kind", PromptKindValue(kind.Value));
            }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a prompt template by identifier.
        /// </summary>
        public async Task<PromptTemplate?> GetPromptTemplateAsync(string tenantId, string id, CancellationToken token = default)
        {
            List<PromptTemplate> items = await QueryAsync("SELECT * FROM prompttemplates WHERE tenantid=@tenantid AND id=@id", ReadPromptTemplate, command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
            return items.FirstOrDefault();
        }

        /// <summary>
        /// Get a tenant default prompt template by kind.
        /// </summary>
        public async Task<PromptTemplate?> GetDefaultPromptTemplateAsync(string tenantId, PromptTemplateKind kind, CancellationToken token = default)
        {
            List<PromptTemplate> items = await QueryAsync("SELECT * FROM prompttemplates WHERE tenantid=@tenantid AND kind=@kind AND isdefault=1 AND active=1 ORDER BY lastupdateutc DESC", ReadPromptTemplate, command => { Add(command, "@tenantid", tenantId); Add(command, "@kind", PromptKindValue(kind)); }, token).ConfigureAwait(false);
            return items.FirstOrDefault();
        }

        /// <summary>
        /// Set the default prompt template for a tenant and kind.
        /// </summary>
        public async Task SetDefaultPromptTemplateAsync(string tenantId, string id, PromptTemplateKind kind, string updatedByUserId, CancellationToken token = default)
        {
            PromptTemplate? existing = await GetPromptTemplateAsync(tenantId, id, token).ConfigureAwait(false);
            if (existing == null) throw new KeyNotFoundException("Prompt template not found.");
            if (existing.Kind != kind) throw new ArgumentException("Prompt template kind mismatch.");
            if (!existing.Active) throw new ArgumentException("Inactive prompt templates cannot be made default.");
            await ClearDefaultPromptTemplateAsync(tenantId, kind, token).ConfigureAwait(false);
            await ExecuteSimpleAsync("UPDATE prompttemplates SET isdefault=1, updatedbyuserid=@updatedbyuserid, lastupdateutc=@lastupdateutc WHERE tenantid=@tenantid AND id=@id", command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); Add(command, "@updatedbyuserid", updatedByUserId ?? String.Empty); Add(command, "@lastupdateutc", Iso(DateTime.UtcNow)); }, token).ConfigureAwait(false);
        }

        private async Task ClearDefaultPromptTemplateAsync(string tenantId, PromptTemplateKind kind, CancellationToken token)
        {
            await ExecuteSimpleAsync("UPDATE prompttemplates SET isdefault=0, lastupdateutc=@lastupdateutc WHERE tenantid=@tenantid AND kind=@kind AND isdefault=1", command => { Add(command, "@tenantid", tenantId); Add(command, "@kind", PromptKindValue(kind)); Add(command, "@lastupdateutc", Iso(DateTime.UtcNow)); }, token).ConfigureAwait(false);
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
            await ExecuteSimpleAsync("DELETE FROM toolcalls WHERE tenantid=@tenantid AND conversationid=@id", command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
            await ExecuteSimpleAsync("DELETE FROM toolruns WHERE tenantid=@tenantid AND conversationid=@id", command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
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
            await ExecuteAsync("INSERT INTO messages (id,tenantid,conversationid,role,content,runnerid,model,tokenestimate,timetofirsttokenms,streamingtimems,totaltimems,tokensused,runid,toolcallsjson,toolcallid,metadatajson,createdutc) VALUES (@id,@tenantid,@conversationid,@role,@content,@runnerid,@model,@tokenestimate,@timetofirsttokenms,@streamingtimems,@totaltimems,@tokensused,@runid,@toolcallsjson,@toolcallid,@metadatajson,@createdutc)", AddMessage, item, token).ConfigureAwait(false);
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
            await ExecuteAsync("INSERT INTO requesthistory (id,tenantid,userid,method,path,statuscode,durationms,requestheaders,requestbody,responseheaders,responsebody,timetofirsttokenms,streamingtimems,totaltimems,tokensused,toolrunid,toolcallcount,toolelapsedms,agentiterations,systempromptid,systempromptname,systempromptdefault,systemprompthash,toolpromptid,toolpromptname,toolpromptdefault,toolprompthash,createdutc) VALUES (@id,@tenantid,@userid,@method,@path,@statuscode,@durationms,@requestheaders,@requestbody,@responseheaders,@responsebody,@timetofirsttokenms,@streamingtimems,@totaltimems,@tokensused,@toolrunid,@toolcallcount,@toolelapsedms,@agentiterations,@systempromptid,@systempromptname,@systempromptdefault,@systemprompthash,@toolpromptid,@toolpromptname,@toolpromptdefault,@toolprompthash,@createdutc)", AddRequestHistory, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Enumerate request history.
        /// </summary>
        public async Task<List<RequestHistoryEntry>> GetRequestHistoryAsync(string? tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return await QueryAsync("SELECT * FROM requesthistory ORDER BY createdutc DESC", ReadRequestHistory, null, token).ConfigureAwait(false);
            return await QueryAsync("SELECT * FROM requesthistory WHERE tenantid=@tenantid ORDER BY createdutc DESC", ReadRequestHistory, command => Add(command, "@tenantid", tenantId), token).ConfigureAwait(false);
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
        /// Create a tool run.
        /// </summary>
        public async Task CreateToolRunAsync(ToolRun item, CancellationToken token = default)
        {
            await ExecuteAsync("INSERT INTO toolruns (id,tenantid,userid,conversationid,runnerid,model,status,startedutc,completedutc,elapsedms,iterationcount,toolcallcount,errorcount,createdutc) VALUES (@id,@tenantid,@userid,@conversationid,@runnerid,@model,@status,@startedutc,@completedutc,@elapsedms,@iterationcount,@toolcallcount,@errorcount,@createdutc)", AddToolRun, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a tool run.
        /// </summary>
        public async Task UpdateToolRunAsync(ToolRun item, CancellationToken token = default)
        {
            await ExecuteAsync("UPDATE toolruns SET userid=@userid, conversationid=@conversationid, runnerid=@runnerid, model=@model, status=@status, startedutc=@startedutc, completedutc=@completedutc, elapsedms=@elapsedms, iterationcount=@iterationcount, toolcallcount=@toolcallcount, errorcount=@errorcount WHERE tenantid=@tenantid AND id=@id", AddToolRun, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a tool run by identifier.
        /// </summary>
        public async Task<ToolRun?> GetToolRunAsync(string tenantId, string id, CancellationToken token = default)
        {
            List<ToolRun> items = await QueryAsync("SELECT * FROM toolruns WHERE tenantid=@tenantid AND id=@id", ReadToolRun, command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
            return items.FirstOrDefault();
        }

        /// <summary>
        /// Get tool runs for a conversation.
        /// </summary>
        public async Task<List<ToolRun>> GetToolRunsForConversationAsync(string tenantId, string conversationId, CancellationToken token = default)
        {
            return await QueryAsync("SELECT * FROM toolruns WHERE tenantid=@tenantid AND conversationid=@conversationid ORDER BY createdutc ASC", ReadToolRun, command => { Add(command, "@tenantid", tenantId); Add(command, "@conversationid", conversationId); }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Create a tool-call audit record.
        /// </summary>
        public async Task CreateToolCallAsync(ToolExecutionRecord item, CancellationToken token = default)
        {
            await ExecuteAsync("INSERT INTO toolcalls (id,tenantid,userid,conversationid,runid,requesthistoryid,traceid,origin,assistantmessageid,providertoolcallid,toolcallid,toolname,iteration,sequencenumber,status,approvalpolicy,approvedbyuserid,argumentsjson,resultjson,resultsummaryjson,resultpreview,success,denied,truncated,outputcharacters,inputbytes,outputbytes,errortype,errorcode,errormessage,provider,model,startedutc,completedutc,elapsedms,active,createdutc,updatedutc) VALUES (@id,@tenantid,@userid,@conversationid,@runid,@requesthistoryid,@traceid,@origin,@assistantmessageid,@providertoolcallid,@toolcallid,@toolname,@iteration,@sequencenumber,@status,@approvalpolicy,@approvedbyuserid,@argumentsjson,@resultjson,@resultsummaryjson,@resultpreview,@success,@denied,@truncated,@outputcharacters,@inputbytes,@outputbytes,@errortype,@errorcode,@errormessage,@provider,@model,@startedutc,@completedutc,@elapsedms,@active,@createdutc,@updatedutc)", AddToolExecutionRecord, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Update a tool-call audit record.
        /// </summary>
        public async Task UpdateToolCallAsync(ToolExecutionRecord item, CancellationToken token = default)
        {
            item.UpdatedUtc = DateTime.UtcNow;
            await ExecuteAsync("UPDATE toolcalls SET userid=@userid, conversationid=@conversationid, runid=@runid, requesthistoryid=@requesthistoryid, traceid=@traceid, origin=@origin, assistantmessageid=@assistantmessageid, providertoolcallid=@providertoolcallid, toolcallid=@toolcallid, toolname=@toolname, iteration=@iteration, sequencenumber=@sequencenumber, status=@status, approvalpolicy=@approvalpolicy, approvedbyuserid=@approvedbyuserid, argumentsjson=@argumentsjson, resultjson=@resultjson, resultsummaryjson=@resultsummaryjson, resultpreview=@resultpreview, success=@success, denied=@denied, truncated=@truncated, outputcharacters=@outputcharacters, inputbytes=@inputbytes, outputbytes=@outputbytes, errortype=@errortype, errorcode=@errorcode, errormessage=@errormessage, provider=@provider, model=@model, startedutc=@startedutc, completedutc=@completedutc, elapsedms=@elapsedms, active=@active, updatedutc=@updatedutc WHERE tenantid=@tenantid AND id=@id", AddToolExecutionRecord, item, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a tool-call audit record by identifier.
        /// </summary>
        public async Task<ToolExecutionRecord?> GetToolCallAsync(string tenantId, string id, CancellationToken token = default)
        {
            List<ToolExecutionRecord> items = await QueryAsync("SELECT * FROM toolcalls WHERE tenantid=@tenantid AND id=@id", ReadToolExecutionRecord, command => { Add(command, "@tenantid", tenantId); Add(command, "@id", id); }, token).ConfigureAwait(false);
            return items.FirstOrDefault();
        }

        /// <summary>
        /// Get tool-call audit records for a conversation.
        /// </summary>
        public async Task<List<ToolExecutionRecord>> GetToolCallsForConversationAsync(string tenantId, string conversationId, CancellationToken token = default)
        {
            return await QueryAsync("SELECT * FROM toolcalls WHERE tenantid=@tenantid AND conversationid=@conversationid ORDER BY createdutc ASC, sequencenumber ASC", ReadToolExecutionRecord, command => { Add(command, "@tenantid", tenantId); Add(command, "@conversationid", conversationId); }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get tool-call audit records for an assistant message.
        /// </summary>
        public async Task<List<ToolExecutionRecord>> GetToolCallsForMessageAsync(string tenantId, string messageId, CancellationToken token = default)
        {
            return await QueryAsync("SELECT * FROM toolcalls WHERE tenantid=@tenantid AND assistantmessageid=@assistantmessageid ORDER BY createdutc ASC, sequencenumber ASC", ReadToolExecutionRecord, command => { Add(command, "@tenantid", tenantId); Add(command, "@assistantmessageid", messageId); }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Get tool-call audit records for a request-history entry.
        /// </summary>
        public async Task<List<ToolExecutionRecord>> GetToolCallsForRequestHistoryAsync(string? tenantId, string requestHistoryId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return await QueryAsync("SELECT * FROM toolcalls WHERE requesthistoryid=@requesthistoryid ORDER BY createdutc ASC, sequencenumber ASC", ReadToolExecutionRecord, command => Add(command, "@requesthistoryid", requestHistoryId), token).ConfigureAwait(false);
            return await QueryAsync("SELECT * FROM toolcalls WHERE tenantid=@tenantid AND requesthistoryid=@requesthistoryid ORDER BY createdutc ASC, sequencenumber ASC", ReadToolExecutionRecord, command => { Add(command, "@tenantid", tenantId); Add(command, "@requesthistoryid", requestHistoryId); }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Attach tool-call records with a trace identifier to an assistant message.
        /// </summary>
        public async Task AttachToolCallsToMessageByTraceIdAsync(string tenantId, string traceId, string assistantMessageId, CancellationToken token = default)
        {
            await ExecuteSimpleAsync("UPDATE toolcalls SET assistantmessageid=@assistantmessageid, updatedutc=@updatedutc WHERE tenantid=@tenantid AND traceid=@traceid", command => { Add(command, "@tenantid", tenantId); Add(command, "@traceid", traceId); Add(command, "@assistantmessageid", assistantMessageId); Add(command, "@updatedutc", Iso(DateTime.UtcNow)); }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Attach tool-call records with a run identifier to a request-history entry.
        /// </summary>
        public async Task AttachToolCallsToRequestHistoryByRunIdAsync(string tenantId, string runId, string requestHistoryId, CancellationToken token = default)
        {
            await ExecuteSimpleAsync("UPDATE toolcalls SET requesthistoryid=@requesthistoryid, updatedutc=@updatedutc WHERE tenantid=@tenantid AND runid=@runid", command => { Add(command, "@tenantid", tenantId); Add(command, "@runid", runId); Add(command, "@requesthistoryid", requestHistoryId); Add(command, "@updatedutc", Iso(DateTime.UtcNow)); }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete expired tool-call audit records.
        /// </summary>
        public async Task DeleteExpiredToolCallsAsync(string? tenantId, DateTime beforeUtc, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId))
            {
                await ExecuteSimpleAsync("DELETE FROM toolcalls WHERE createdutc<@beforeutc", command => Add(command, "@beforeutc", Iso(beforeUtc)), token).ConfigureAwait(false);
                await ExecuteSimpleAsync("DELETE FROM toolruns WHERE createdutc<@beforeutc", command => Add(command, "@beforeutc", Iso(beforeUtc)), token).ConfigureAwait(false);
                return;
            }

            await ExecuteSimpleAsync("DELETE FROM toolcalls WHERE tenantid=@tenantid AND createdutc<@beforeutc", command => { Add(command, "@tenantid", tenantId); Add(command, "@beforeutc", Iso(beforeUtc)); }, token).ConfigureAwait(false);
            await ExecuteSimpleAsync("DELETE FROM toolruns WHERE tenantid=@tenantid AND createdutc<@beforeutc", command => { Add(command, "@tenantid", tenantId); Add(command, "@beforeutc", Iso(beforeUtc)); }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Summarize request history.
        /// </summary>
        public async Task<RequestHistorySummary> SummarizeRequestHistoryAsync(string? tenantId, DateTime fromUtc, DateTime toUtc, int bucketMinutes, CancellationToken token = default)
        {
            List<RequestHistoryEntry> entries = await GetRequestHistoryRangeAsync(tenantId, fromUtc, toUtc, token).ConfigureAwait(false);
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

        private async Task<List<RequestHistoryEntry>> GetRequestHistoryRangeAsync(string? tenantId, DateTime fromUtc, DateTime toUtc, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(tenantId))
            {
                return await QueryAsync("SELECT * FROM requesthistory WHERE createdutc>=@fromutc AND createdutc<=@toutc ORDER BY createdutc ASC", ReadRequestHistory, command => { Add(command, "@fromutc", Iso(fromUtc)); Add(command, "@toutc", Iso(toUtc)); }, token).ConfigureAwait(false);
            }

            return await QueryAsync("SELECT * FROM requesthistory WHERE tenantid=@tenantid AND createdutc>=@fromutc AND createdutc<=@toutc ORDER BY createdutc ASC", ReadRequestHistory, command => { Add(command, "@tenantid", tenantId); Add(command, "@fromutc", Iso(fromUtc)); Add(command, "@toutc", Iso(toUtc)); }, token).ConfigureAwait(false);
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

        private static void EnsureIndex(IDbConnection connection, string statement)
        {
            try
            {
                using IDbCommand command = connection.CreateCommand();
                command.CommandText = statement;
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
            object? value = V(record, name);
            if (value == null || value == DBNull.Value) return String.Empty;
            return value.ToString() ?? String.Empty;
        }

        private static bool B(IDataRecord record, string name)
        {
            object? value = V(record, name);
            return value != null && value != DBNull.Value && Convert.ToInt32(value) == 1;
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
            object? value = V(record, name);
            return value == DBNull.Value ? 0 : Convert.ToDouble(value);
        }

        private static object? V(IDataRecord record, string name)
        {
            for (int i = 0; i < record.FieldCount; i++)
            {
                if (String.Equals(record.GetName(i), name, StringComparison.OrdinalIgnoreCase)) return record.GetValue(i);
            }

            return null;
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

        private static void ValidatePromptTemplate(PromptTemplate item)
        {
            if (String.IsNullOrWhiteSpace(item.TenantId)) throw new ArgumentException("Prompt template tenant ID is required.");
            if (String.IsNullOrWhiteSpace(item.Name)) throw new ArgumentException("Prompt template name is required.");
            if (String.IsNullOrWhiteSpace(item.Content)) throw new ArgumentException("Prompt template content is required.");
            item.Name = item.Name.Trim();
            item.Description = item.Description?.Trim() ?? String.Empty;
            item.Content = item.Content.Trim();
        }

        private static string PromptKindValue(PromptTemplateKind kind)
        {
            return kind == PromptTemplateKind.Tool ? "tool" : "system";
        }

        private static PromptTemplateKind ReadPromptKind(IDataRecord record)
        {
            string value = S(record, "kind");
            return String.Equals(value, "tool", StringComparison.OrdinalIgnoreCase) ? PromptTemplateKind.Tool : PromptTemplateKind.System;
        }

        private static void AddPromptTemplate(IDbCommand command, PromptTemplate item)
        {
            Add(command, "@id", item.Id); Add(command, "@tenantid", item.TenantId); Add(command, "@kind", PromptKindValue(item.Kind)); Add(command, "@name", item.Name); Add(command, "@description", item.Description); Add(command, "@content", item.Content); Add(command, "@isdefault", item.IsDefault ? 1 : 0); Add(command, "@isprotected", item.IsProtected ? 1 : 0); Add(command, "@active", item.Active ? 1 : 0); Add(command, "@createdbyuserid", item.CreatedByUserId); Add(command, "@updatedbyuserid", item.UpdatedByUserId); Add(command, "@createdutc", Iso(item.CreatedUtc)); Add(command, "@lastupdateutc", Iso(item.LastUpdateUtc));
        }

        private static PromptTemplate ReadPromptTemplate(IDataRecord record)
        {
            return new PromptTemplate { Id = S(record, "id"), TenantId = S(record, "tenantid"), Kind = ReadPromptKind(record), Name = S(record, "name"), Description = S(record, "description"), Content = S(record, "content"), IsDefault = B(record, "isdefault"), IsProtected = B(record, "isprotected"), Active = B(record, "active"), CreatedByUserId = S(record, "createdbyuserid"), UpdatedByUserId = S(record, "updatedbyuserid"), CreatedUtc = D(record, "createdutc"), LastUpdateUtc = D(record, "lastupdateutc") };
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
            Add(command, "@id", item.Id); Add(command, "@tenantid", item.TenantId); Add(command, "@conversationid", item.ConversationId); Add(command, "@role", item.Role); Add(command, "@content", item.Content); Add(command, "@runnerid", item.RunnerId); Add(command, "@model", item.Model); Add(command, "@tokenestimate", item.TokenEstimate); Add(command, "@timetofirsttokenms", item.TimeToFirstTokenMs); Add(command, "@streamingtimems", item.StreamingTimeMs); Add(command, "@totaltimems", item.TotalTimeMs); Add(command, "@tokensused", item.TokensUsed); Add(command, "@runid", item.RunId); Add(command, "@toolcallsjson", item.ToolCallsJson); Add(command, "@toolcallid", item.ToolCallId); Add(command, "@metadatajson", item.MetadataJson); Add(command, "@createdutc", Iso(item.CreatedUtc));
        }

        private static ChatMessage ReadMessage(IDataRecord record)
        {
            return new ChatMessage { Id = S(record, "id"), TenantId = S(record, "tenantid"), ConversationId = S(record, "conversationid"), Role = S(record, "role"), Content = S(record, "content"), RunnerId = S(record, "runnerid"), Model = S(record, "model"), TokenEstimate = Convert.ToInt32(R(record, "tokenestimate")), TimeToFirstTokenMs = R(record, "timetofirsttokenms"), StreamingTimeMs = R(record, "streamingtimems"), TotalTimeMs = R(record, "totaltimems"), TokensUsed = Convert.ToInt32(R(record, "tokensused")), RunId = S(record, "runid"), ToolCallsJson = S(record, "toolcallsjson"), ToolCallId = S(record, "toolcallid"), MetadataJson = S(record, "metadatajson"), CreatedUtc = D(record, "createdutc") };
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
            Add(command, "@id", item.Id); Add(command, "@tenantid", item.TenantId); Add(command, "@userid", item.UserId); Add(command, "@method", item.Method); Add(command, "@path", item.Path); Add(command, "@statuscode", item.StatusCode); Add(command, "@durationms", item.DurationMs); Add(command, "@requestheaders", item.RequestHeaders); Add(command, "@requestbody", item.RequestBody); Add(command, "@responseheaders", item.ResponseHeaders); Add(command, "@responsebody", item.ResponseBody); Add(command, "@timetofirsttokenms", item.TimeToFirstTokenMs); Add(command, "@streamingtimems", item.StreamingTimeMs); Add(command, "@totaltimems", item.TotalTimeMs); Add(command, "@tokensused", item.TokensUsed); Add(command, "@toolrunid", item.ToolRunId); Add(command, "@toolcallcount", item.ToolCallCount); Add(command, "@toolelapsedms", item.ToolElapsedMs); Add(command, "@agentiterations", item.AgentIterations); Add(command, "@systempromptid", item.SystemPromptId); Add(command, "@systempromptname", item.SystemPromptName); Add(command, "@systempromptdefault", item.SystemPromptDefault ? 1 : 0); Add(command, "@systemprompthash", item.SystemPromptHash); Add(command, "@toolpromptid", item.ToolPromptId); Add(command, "@toolpromptname", item.ToolPromptName); Add(command, "@toolpromptdefault", item.ToolPromptDefault ? 1 : 0); Add(command, "@toolprompthash", item.ToolPromptHash); Add(command, "@createdutc", Iso(item.CreatedUtc));
        }

        private static RequestHistoryEntry ReadRequestHistory(IDataRecord record)
        {
            return new RequestHistoryEntry { Id = S(record, "id"), TenantId = S(record, "tenantid"), UserId = S(record, "userid"), Method = S(record, "method"), Path = S(record, "path"), StatusCode = Convert.ToInt32(R(record, "statuscode")), DurationMs = R(record, "durationms"), RequestHeaders = S(record, "requestheaders"), RequestBody = S(record, "requestbody"), ResponseHeaders = S(record, "responseheaders"), ResponseBody = S(record, "responsebody"), TimeToFirstTokenMs = R(record, "timetofirsttokenms"), StreamingTimeMs = R(record, "streamingtimems"), TotalTimeMs = R(record, "totaltimems"), TokensUsed = Convert.ToInt32(R(record, "tokensused")), ToolRunId = S(record, "toolrunid"), ToolCallCount = Convert.ToInt32(R(record, "toolcallcount")), ToolElapsedMs = R(record, "toolelapsedms"), AgentIterations = Convert.ToInt32(R(record, "agentiterations")), SystemPromptId = S(record, "systempromptid"), SystemPromptName = S(record, "systempromptname"), SystemPromptDefault = B(record, "systempromptdefault"), SystemPromptHash = S(record, "systemprompthash"), ToolPromptId = S(record, "toolpromptid"), ToolPromptName = S(record, "toolpromptname"), ToolPromptDefault = B(record, "toolpromptdefault"), ToolPromptHash = S(record, "toolprompthash"), CreatedUtc = D(record, "createdutc") };
        }

        private static void AddToolRun(IDbCommand command, ToolRun item)
        {
            Add(command, "@id", item.RunId); Add(command, "@tenantid", item.TenantId); Add(command, "@userid", item.UserId); Add(command, "@conversationid", item.ConversationId); Add(command, "@runnerid", item.RunnerId); Add(command, "@model", item.Model); Add(command, "@status", item.Status); Add(command, "@startedutc", Iso(item.StartedUtc)); Add(command, "@completedutc", item.CompletedUtc.HasValue ? Iso(item.CompletedUtc.Value) : null); Add(command, "@elapsedms", item.ElapsedMs); Add(command, "@iterationcount", item.IterationCount); Add(command, "@toolcallcount", item.ToolCallCount); Add(command, "@errorcount", item.ErrorCount); Add(command, "@createdutc", Iso(item.CreatedUtc));
        }

        private static ToolRun ReadToolRun(IDataRecord record)
        {
            return new ToolRun { RunId = S(record, "id"), TenantId = S(record, "tenantid"), UserId = S(record, "userid"), ConversationId = S(record, "conversationid"), RunnerId = S(record, "runnerid"), Model = S(record, "model"), Status = S(record, "status"), StartedUtc = D(record, "startedutc"), CompletedUtc = N(record, "completedutc"), ElapsedMs = R(record, "elapsedms"), IterationCount = Convert.ToInt32(R(record, "iterationcount")), ToolCallCount = Convert.ToInt32(R(record, "toolcallcount")), ErrorCount = Convert.ToInt32(R(record, "errorcount")), CreatedUtc = D(record, "createdutc") };
        }

        private static void AddToolExecutionRecord(IDbCommand command, ToolExecutionRecord item)
        {
            Add(command, "@id", item.Id); Add(command, "@tenantid", item.TenantId); Add(command, "@userid", item.UserId); Add(command, "@conversationid", item.ConversationId); Add(command, "@runid", item.RunId); Add(command, "@requesthistoryid", item.RequestHistoryId); Add(command, "@traceid", item.TraceId); Add(command, "@origin", item.Origin); Add(command, "@assistantmessageid", item.AssistantMessageId); Add(command, "@providertoolcallid", item.ProviderToolCallId); Add(command, "@toolcallid", item.ToolCallId); Add(command, "@toolname", item.ToolName); Add(command, "@iteration", item.Iteration); Add(command, "@sequencenumber", item.SequenceNumber); Add(command, "@status", item.Status); Add(command, "@approvalpolicy", item.ApprovalPolicy); Add(command, "@approvedbyuserid", item.ApprovedByUserId); Add(command, "@argumentsjson", item.ArgumentsJson); Add(command, "@resultjson", item.ResultJson); Add(command, "@resultsummaryjson", item.ResultSummaryJson); Add(command, "@resultpreview", item.ResultPreview); Add(command, "@success", item.Success ? 1 : 0); Add(command, "@denied", item.Denied ? 1 : 0); Add(command, "@truncated", item.Truncated ? 1 : 0); Add(command, "@outputcharacters", item.OutputCharacters); Add(command, "@inputbytes", item.InputBytes); Add(command, "@outputbytes", item.OutputBytes); Add(command, "@errortype", item.ErrorType); Add(command, "@errorcode", item.ErrorCode); Add(command, "@errormessage", item.ErrorMessage); Add(command, "@provider", item.Provider); Add(command, "@model", item.Model); Add(command, "@startedutc", Iso(item.StartedUtc)); Add(command, "@completedutc", item.CompletedUtc.HasValue ? Iso(item.CompletedUtc.Value) : null); Add(command, "@elapsedms", item.ElapsedMs); Add(command, "@active", item.Active ? 1 : 0); Add(command, "@createdutc", Iso(item.CreatedUtc)); Add(command, "@updatedutc", Iso(item.UpdatedUtc));
        }

        private static ToolExecutionRecord ReadToolExecutionRecord(IDataRecord record)
        {
            return new ToolExecutionRecord { Id = S(record, "id"), TenantId = S(record, "tenantid"), UserId = S(record, "userid"), ConversationId = S(record, "conversationid"), RunId = S(record, "runid"), RequestHistoryId = S(record, "requesthistoryid"), TraceId = S(record, "traceid"), Origin = S(record, "origin"), AssistantMessageId = S(record, "assistantmessageid"), ProviderToolCallId = S(record, "providertoolcallid"), ToolCallId = S(record, "toolcallid"), ToolName = S(record, "toolname"), Iteration = Convert.ToInt32(R(record, "iteration")), SequenceNumber = Convert.ToInt32(R(record, "sequencenumber")), Status = S(record, "status"), ApprovalPolicy = S(record, "approvalpolicy"), ApprovedByUserId = S(record, "approvedbyuserid"), ArgumentsJson = S(record, "argumentsjson"), ResultJson = S(record, "resultjson"), ResultSummaryJson = S(record, "resultsummaryjson"), ResultPreview = S(record, "resultpreview"), Success = B(record, "success"), Denied = B(record, "denied"), Truncated = B(record, "truncated"), OutputCharacters = Convert.ToInt32(R(record, "outputcharacters")), InputBytes = Convert.ToInt32(R(record, "inputbytes")), OutputBytes = Convert.ToInt32(R(record, "outputbytes")), ErrorType = S(record, "errortype"), ErrorCode = S(record, "errorcode"), ErrorMessage = S(record, "errormessage"), Provider = S(record, "provider"), Model = S(record, "model"), StartedUtc = D(record, "startedutc"), CompletedUtc = N(record, "completedutc"), ElapsedMs = R(record, "elapsedms"), Active = B(record, "active"), CreatedUtc = D(record, "createdutc"), UpdatedUtc = D(record, "updatedutc") };
        }
    }
}
