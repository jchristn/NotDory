namespace Isis.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Isis.Core.Database;
    using Isis.Core.Helpers;
    using Isis.Core.Models;
    using Isis.Server.Models;

    /// <summary>
    /// Session start: one call that takes an agent from "connected" to "working with memory". It resolves the caller's
    /// tenant, finds (or creates) the project's scope, and returns the protocol, the scope's categories and instructions,
    /// and its most recent memories, replacing the whoami, instructions, scope_enumerate, guide sequence.
    /// </summary>
    public class SessionStartService
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly ScopeProvisioner _Provisioner;
        private readonly Func<string> _Instructions;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the service.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="instructions">Supplies the agent instructions in effect (the administrator's text or the built-in
        /// protocol); read on every call so an edit applies to the next session. Null uses the built-in protocol.</param>
        /// <exception cref="ArgumentNullException">Thrown when database is null.</exception>
        public SessionStartService(DatabaseDriverBase database, Func<string>? instructions = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Provisioner = new ScopeProvisioner(database);
            _Instructions = instructions ?? (() => AgentProtocol.ServerInstructions);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Start a session for a caller in their tenant.
        /// </summary>
        /// <param name="tenantId">The caller's tenant.</param>
        /// <param name="principal">The caller's principal name, echoed back.</param>
        /// <param name="request">The request.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The session context.</returns>
        /// <exception cref="ArgumentNullException">Thrown when tenantId or request is null.</exception>
        public async Task<SessionStartResult> StartAsync(string tenantId, string? principal, SessionStartRequest request, CancellationToken token = default)
        {
            if (tenantId == null) throw new ArgumentNullException(nameof(tenantId));
            if (request == null) throw new ArgumentNullException(nameof(request));

            SessionStartResult result = new SessionStartResult { TenantId = tenantId, Principal = principal };
            List<Scope> scopes = (await _Database.Scopes.EnumerateAsync(tenantId, new EnumerationQuery { MaxResults = 1000 }, token).ConfigureAwait(false)).Objects;

            Scope? scope = null;
            bool created = false;
            if (request.Project != null)
            {
                scope = Match(scopes, request.Project);
                if (scope == null && request.CreateIfMissing)
                {
                    Scope draft = new Scope { Name = request.Project, Description = "Memory for the " + request.Project + " project." };
                    ScopeProvisionResult provisioned = await _Provisioner.CreateAsync(tenantId, draft, token).ConfigureAwait(false);
                    scope = provisioned.Scope;
                    created = scope != null;
                    if (scope == null) result.Notice = "No scope matches '" + request.Project + "' and one could not be created: " + provisioned.Message;
                }
                else if (scope == null)
                {
                    result.Notice = "No scope matches '" + request.Project + "'. Pick one from scopes, or call again with createIfMissing true.";
                }
            }
            else if (scopes.Count == 1)
            {
                scope = scopes[0];
            }
            else
            {
                result.Notice = scopes.Count == 0
                    ? "This tenant has no scopes yet. Call session_start with project set to the repository or project name to create one."
                    : "Several scopes exist. Call session_start with project set to the repository or project name, or pick one from scopes.";
            }

            if (scope == null)
            {
                result.Protocol = _Instructions();
                result.Scopes = scopes.Take(50).Select(s => Summary(s, false)).ToList();
                return result;
            }

            result.Scope = Summary(scope, created);
            result.Protocol = AgentProtocol.ForScope(scope.Id, scope.Name, _Instructions());

            EnumerationQuery all = new EnumerationQuery { MaxResults = 1000 };
            result.Categories = (await _Database.Categories.EnumerateAsync(tenantId, scope.Id, all, token).ConfigureAwait(false)).Objects;
            List<Instruction> globals = (await _Database.Instructions.EnumerateAsync(tenantId, null, all, token).ConfigureAwait(false)).Objects;
            List<Instruction> scoped = (await _Database.Instructions.EnumerateAsync(tenantId, scope.Id, all, token).ConfigureAwait(false)).Objects;
            result.Instructions = InstructionResolver.Resolve(globals, scoped, scope.Id);

            // Newest first: what was written most recently is usually what the next session needs.
            EnumerationResult<Memory> memories = await _Database.Memories.EnumerateAsync(tenantId, scope.Id, null, new EnumerationQuery { MaxResults = Math.Max(1, request.MaxMemories) }, token).ConfigureAwait(false);
            result.MemoryCount = memories.TotalRecords;
            Dictionary<string, string> categoryNames = result.Categories.ToDictionary(c => c.Id, c => c.Name, StringComparer.Ordinal);
            if (request.MaxMemories > 0)
            {
                result.RecentMemories = memories.Objects.Take(request.MaxMemories).Select(m => new SessionMemorySummary
                {
                    Id = m.Id,
                    Slug = m.Slug,
                    Category = categoryNames.TryGetValue(m.CategoryId, out string? name) ? name : m.CategoryId,
                    Title = m.Title,
                    Summary = m.Summary,
                    LastUpdateUtc = m.LastUpdateUtc
                }).ToList();
            }

            return result;
        }

        /// <summary>
        /// Match a project name to a scope: exact name first (case-insensitive), then ignoring punctuation and spacing, so
        /// "AgentMemory", "agent-memory", and "Agent Memory" find the same scope.
        /// </summary>
        /// <param name="scopes">The tenant's scopes.</param>
        /// <param name="project">The project name.</param>
        /// <returns>The matching scope, or null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when scopes or project is null.</exception>
        public static Scope? Match(IReadOnlyList<Scope> scopes, string project)
        {
            if (scopes == null) throw new ArgumentNullException(nameof(scopes));
            if (project == null) throw new ArgumentNullException(nameof(project));
            Scope? exact = scopes.FirstOrDefault(s => string.Equals(s.Name, project, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;
            string key = Normalize(project);
            return key.Length == 0 ? null : scopes.FirstOrDefault(s => Normalize(s.Name) == key);
        }

        /// <summary>
        /// Render a session start as compact markdown, for injecting into an agent's context (for example from a harness
        /// session-start hook).
        /// </summary>
        /// <param name="result">The session context.</param>
        /// <returns>The markdown.</returns>
        /// <exception cref="ArgumentNullException">Thrown when result is null.</exception>
        public static string ToMarkdown(SessionStartResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            StringBuilder sb = new StringBuilder();
            sb.Append("# Isis memory\n\n").Append(result.Protocol).Append("\n");
            if (result.Notice != null) sb.Append("\nNote: ").Append(result.Notice).Append("\n");

            if (result.Scope == null)
            {
                if (result.Scopes != null && result.Scopes.Count > 0)
                {
                    sb.Append("\n## Scopes\n");
                    foreach (SessionScopeSummary s in result.Scopes) sb.Append("- ").Append(s.Name).Append(" (").Append(s.Id).Append(")\n");
                }

                return sb.ToString();
            }

            sb.Append("\n## Scope\n").Append(result.Scope.Name).Append(" (").Append(result.Scope.Id).Append("), ")
                .Append(result.MemoryCount).Append(" memories").Append(result.Scope.Created ? ", created for this session" : string.Empty).Append(".\n");

            if (result.Categories.Count > 0)
            {
                sb.Append("\n## Categories\n");
                foreach (Category c in result.Categories)
                {
                    sb.Append("- ").Append(c.Name);
                    if (!string.IsNullOrEmpty(c.Description)) sb.Append(": ").Append(c.Description);
                    sb.Append("\n");
                }
            }

            if (result.Instructions.Count > 0)
            {
                sb.Append("\n## Instructions\n");
                foreach (ResolvedInstruction i in result.Instructions) sb.Append("- ").Append(i.Name).Append(": ").Append(i.Content).Append("\n");
            }

            if (result.RecentMemories.Count > 0)
            {
                sb.Append("\n## Recent memories (newest first; memory_search for more, memory_read for the full text)\n");
                foreach (SessionMemorySummary m in result.RecentMemories)
                {
                    sb.Append("- [").Append(m.Category).Append("] ").Append(m.Slug);
                    if (!string.IsNullOrEmpty(m.Title)) sb.Append(": ").Append(m.Title);
                    if (!string.IsNullOrEmpty(m.Summary)) sb.Append(". ").Append(m.Summary);
                    sb.Append("\n");
                }
            }

            return sb.ToString();
        }

        #endregion

        #region Private-Methods

        private static SessionScopeSummary Summary(Scope scope, bool created)
        {
            return new SessionScopeSummary
            {
                Id = scope.Id,
                Name = scope.Name,
                Description = scope.Description,
                StoreProvider = scope.StoreProvider.ToString(),
                Created = created
            };
        }

        private static string Normalize(string value)
        {
            StringBuilder sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }

        #endregion
    }
}
