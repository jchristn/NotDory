namespace NotDory.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using NotDory.Core.Database;
    using NotDory.Core.Helpers;
    using NotDory.Core.Models;
    using NotDory.Core.Stores;
    using NotDory.Server.Models;
    using NotDory.Server.Settings;

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
        private readonly StorageSettings _Storage;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the service.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="instructions">Supplies the agent instructions in effect (the administrator's text or the built-in
        /// protocol); read on every call so an edit applies to the next session. Null uses the built-in protocol.</param>
        /// <param name="storage">Storage settings (whether a new scope mirrors to the project's OKF bundle); null uses the defaults.</param>
        /// <exception cref="ArgumentNullException">Thrown when database is null.</exception>
        public SessionStartService(DatabaseDriverBase database, Func<string>? instructions = null, StorageSettings? storage = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Storage = storage ?? new StorageSettings();
            _Provisioner = new ScopeProvisioner(database, _Storage);
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
            string? mirrorNote = null;

            // Candidate names, most specific first: what the caller called the project, the repository name from the git
            // remote (stable across clones), then the working directory's name.
            string? repository = RepositoryName(request.Remote);
            List<string> candidates = new List<string>();
            foreach (string? candidate in new[] { request.Project, repository, request.Directory })
            {
                if (!string.IsNullOrWhiteSpace(candidate) && !candidates.Contains(candidate, StringComparer.OrdinalIgnoreCase)) candidates.Add(candidate);
            }

            foreach (string candidate in candidates)
            {
                scope = Match(scopes, candidate);
                if (scope != null) break;
            }

            // Create only for a named project or a real repository, never for a bare folder name.
            string? newName = request.Project ?? repository;
            if (scope == null && newName != null && request.CreateIfMissing)
            {
                Scope draft = new Scope { Name = newName, Description = "Memory for the " + newName + " project." };
                mirrorNote = PlanProjectMirror(draft, request.Path);
                ScopeProvisionResult provisioned = await _Provisioner.CreateAsync(tenantId, draft, token).ConfigureAwait(false);
                scope = provisioned.Scope;
                created = scope != null;
                if (scope == null) result.Notice = "No scope matches '" + newName + "' and one could not be created: " + provisioned.Message;
            }
            else if (scope == null && newName == null && scopes.Count == 1)
            {
                // Nothing names a project (at most a folder name that matched nothing): use the tenant's only scope.
                scope = scopes[0];
            }
            else if (scope == null && candidates.Count > 0)
            {
                result.Notice = "No scope matches '" + string.Join("', '", candidates) + "'. Pick one from scopes, or call session_start with project set to the repository name"
                    + (request.CreateIfMissing ? "." : " and createIfMissing true.");
            }
            else if (scope == null)
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
            if (created || result.MemoryCount == 0)
            {
                result.Notice = "The scope '" + scope.Name + "' is " + (created ? "new" : "empty") + ". If you will do a meaningful amount of work "
                    + "on this project, onboard it: examine the project structure and key details, describe the scope (scope_update), "
                    + "create categories with descriptions and instructions (category_create), and save memories covering what you found (memory_upsert).";
            }

            if (mirrorNote != null) result.Notice = result.Notice == null ? mirrorNote : result.Notice + " " + mirrorNote;
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
        /// Point a new scope's Open Knowledge Format mirror at the project (the bundle goes in its <c>.okf/</c> directory)
        /// when the server can see the project and that directory is free. OKF names no location; a bundle may be a
        /// subdirectory of the repository it describes. The mirror owns its directory (it regenerates index.md and reads
        /// every file under it), so an existing, non-empty <c>.okf/</c> is left alone.
        /// </summary>
        /// <param name="draft">The scope about to be created; its targetPath is set when the mirror can be used.</param>
        /// <param name="projectPath">The project's absolute path from the agent harness, or null.</param>
        /// <returns>Why the scope will not be mirrored, or null when it will be (or mirroring is off by default).</returns>
        private string? PlanProjectMirror(Scope draft, string? projectPath)
        {
            if (!_Storage.MirrorByDefault) return null;
            string how = " To mirror it, call scope_update with filesystemMirror true and targetPath set to the repository root as the NotDory server sees it (an absolute path); the bundle goes in its " + MirroredMemoryStore.BundleDirectoryName + " directory.";
            if (projectPath == null)
                return "The scope is not mirrored to an Open Knowledge Format bundle because session start got no project path (pass path, the repository root)." + how;
            if (!Path.IsPathRooted(projectPath) || projectPath.StartsWith("~", StringComparison.Ordinal))
                return "The scope is not mirrored because the project path '" + projectPath + "' is not absolute." + how;
            if (!Directory.Exists(projectPath))
                return "The scope is not mirrored because the NotDory server cannot see the project directory '" + projectPath + "' (it likely runs on another host or in a container)." + how;

            string bundle = Path.Combine(projectPath, MirroredMemoryStore.BundleDirectoryName);
            if (Directory.Exists(bundle) && Directory.EnumerateFileSystemEntries(bundle).Any())
                return "The scope is not mirrored because '" + bundle + "' already exists and is not empty, and the mirror would take it over." + how;

            draft.TargetPath = projectPath;
            return null;
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
        /// The repository name in a git remote URL: the last path segment without <c>.git</c>, for https, ssh, and
        /// scp-style (<c>git@host:owner/repo.git</c>) remotes.
        /// </summary>
        /// <param name="remote">The remote URL, or null.</param>
        /// <returns>The repository name, or null when there is none.</returns>
        public static string? RepositoryName(string? remote)
        {
            if (string.IsNullOrWhiteSpace(remote)) return null;
            string path = remote.Trim().TrimEnd('/', '\\');
            int cut = Math.Max(path.LastIndexOf('/'), Math.Max(path.LastIndexOf(':'), path.LastIndexOf('\\')));
            string name = cut >= 0 ? path.Substring(cut + 1) : path;
            if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - 4);
            return name.Length == 0 ? null : name;
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
            sb.Append("# NotDory memory\n\n").Append(result.Protocol).Append("\n");
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
