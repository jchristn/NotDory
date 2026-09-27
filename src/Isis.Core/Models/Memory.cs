namespace Isis.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Isis.Core.Enums;
    using Isis.Core.Helpers;

    /// <summary>
    /// One atomic memory. The body and embedding live in the scope's memory store; this record is the
    /// Isis-owned index row carrying identity, structure, and provenance.
    /// </summary>
    public class Memory
    {
        #region Public-Members

        /// <summary>
        /// Memory identifier. Defaults to a generated value; may not be set to null or empty.
        /// </summary>
        public string Id
        {
            get
            {
                return _Id;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Id));
                _Id = value;
            }
        }

        /// <summary>
        /// Owning tenant identifier. May not be set to null or empty.
        /// </summary>
        public string TenantId
        {
            get
            {
                return _TenantId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(TenantId));
                _TenantId = value;
            }
        }

        /// <summary>
        /// Owning scope identifier. May not be set to null or empty.
        /// </summary>
        public string ScopeId
        {
            get
            {
                return _ScopeId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(ScopeId));
                _ScopeId = value;
            }
        }

        /// <summary>
        /// Owning category identifier. May not be set to null or empty.
        /// </summary>
        public string CategoryId
        {
            get
            {
                return _CategoryId;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(CategoryId));
                _CategoryId = value;
            }
        }

        /// <summary>
        /// Stable, link-addressable slug, unique within a (scope, category). May not be set to null or empty.
        /// </summary>
        public string Slug
        {
            get
            {
                return _Slug;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Slug));
                _Slug = InputGuard.MaxLength(value, 256, nameof(Slug))!;
            }
        }

        /// <summary>
        /// The join key into the scope's memory store (for RecallDB, the document key).
        /// </summary>
        public string? StoreKey { get; set; } = null;

        /// <summary>
        /// Human-readable title.
        /// </summary>
        public string? Title
        {
            get
            {
                return _Title;
            }
            set
            {
                _Title = InputGuard.MaxLength(value, 1024, nameof(Title));
            }
        }

        /// <summary>
        /// Classification of the memory.
        /// </summary>
        public MemoryTypeEnum Type
        {
            get
            {
                return _Type;
            }
            set
            {
                _Type = InputGuard.Defined(value, MemoryTypeEnum.Project);
            }
        }

        /// <summary>
        /// A one-line recall hook, cheap to return in listings and search results.
        /// </summary>
        public string? Summary
        {
            get
            {
                return _Summary;
            }
            set
            {
                _Summary = InputGuard.MaxLength(value, 8192, nameof(Summary));
            }
        }

        /// <summary>
        /// Optional URI reference to the underlying resource this memory describes (for example a
        /// console link, document URL, or repository path). Maps to the OKF <c>resource</c> field.
        /// </summary>
        public string? Resource
        {
            get
            {
                return _Resource;
            }
            set
            {
                _Resource = InputGuard.MaxLength(value, 4096, nameof(Resource));
            }
        }

        /// <summary>
        /// The full memory body. Stored as the memory store document content.
        /// </summary>
        public string Body
        {
            get
            {
                return _Body;
            }
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(Body));
                _Body = InputGuard.MaxLength(value, 4 * 1024 * 1024, nameof(Body))!;
            }
        }

        /// <summary>
        /// Free-form tags.
        /// </summary>
        public List<string> Tags
        {
            get
            {
                return _Tags;
            }
            set
            {
                _Tags = InputGuard.CleanList(value, 256, 256, nameof(Tags));
            }
        }

        /// <summary>
        /// Slugs of related memories, forming the link graph.
        /// </summary>
        public List<string> Links
        {
            get
            {
                return _Links;
            }
            set
            {
                _Links = InputGuard.CleanList(value, 256, 256, nameof(Links));
            }
        }

        /// <summary>
        /// Slugs (or ids) of memories in the same scope that this memory replaces, for example an earlier decision
        /// this one reverses. Search demotes replaced memories behind their replacement. Empty by default.
        /// </summary>
        public List<string> Supersedes
        {
            get
            {
                return _Supersedes;
            }
            set
            {
                _Supersedes = InputGuard.CleanList(value, 64, 256, nameof(Supersedes));
            }
        }

        /// <summary>
        /// Id of the memory that replaces this one, maintained by the server from other memories'
        /// <see cref="Supersedes"/> lists. Null when the memory is current. Ignored on upsert.
        /// </summary>
        public string? SupersededBy { get; set; } = null;

        /// <summary>
        /// On an upsert response only: existing memories in the scope that closely resemble this one, so the writer
        /// can decide whether it duplicates or replaces them. Not persisted, and omitted from the response when empty.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SimilarMemory>? SimilarMemories { get; set; } = null;

        /// <summary>
        /// Extensible structured metadata (for example referenced files, confidence).
        /// </summary>
        public Dictionary<string, string> Metadata
        {
            get
            {
                return _Metadata;
            }
            set
            {
                Dictionary<string, string> cleaned = new Dictionary<string, string>();
                if (value != null)
                {
                    foreach (KeyValuePair<string, string> entry in value)
                    {
                        if (string.IsNullOrWhiteSpace(entry.Key)) continue;
                        InputGuard.MaxLength(entry.Key, 256, nameof(Metadata) + " key");
                        cleaned[entry.Key] = InputGuard.MaxLength(entry.Value ?? string.Empty, 8192, nameof(Metadata) + " value")!;
                    }
                }
                
                if (cleaned.Count > 128) throw new ArgumentOutOfRangeException(nameof(Metadata), "Metadata may have at most 128 entries (got " + cleaned.Count + ").");
                _Metadata = cleaned;
            }
        }

        /// <summary>
        /// Ranking signal in the range 0.0 to 1.0, bumped when a memory is read. Default 0.5.
        /// </summary>
        public double Salience
        {
            get
            {
                return _Salience;
            }
            set
            {
                _Salience = InputGuard.Clamp(value, 0.0, 1.0, 0.5);
            }
        }

        /// <summary>
        /// Who authored the memory (agent or human).
        /// </summary>
        public string? Author { get; set; } = null;

        /// <summary>
        /// The session that authored the memory, if applicable.
        /// </summary>
        public string? SessionId { get; set; } = null;

        /// <summary>
        /// The model that authored the memory, if applicable.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Monotonic version counter, incremented on update.
        /// </summary>
        public int Version
        {
            get
            {
                return _Version;
            }
            set
            {
                _Version = value < 1 ? 1 : value;
            }
        }

        /// <summary>
        /// UTC timestamp when the memory was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the memory was last updated.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the memory was last read, if ever.
        /// </summary>
        public DateTime? LastAccessedUtc { get; set; } = null;

        #endregion

        #region Private-Members

        private int _Version = 1;
        private Dictionary<string, string> _Metadata = new Dictionary<string, string>();
        private List<string> _Supersedes = new List<string>();
        private List<string> _Links = new List<string>();
        private List<string> _Tags = new List<string>();
        private MemoryTypeEnum _Type = MemoryTypeEnum.Project;
        private string? _Resource = null;
        private string? _Summary = null;
        private string? _Title = null;
        private string _Id = IdGenerator.Memory();
        private string _TenantId = String.Empty;
        private string _ScopeId = String.Empty;
        private string _CategoryId = String.Empty;
        private string _Slug = String.Empty;
        private string _Body = String.Empty;
        private double _Salience = 0.5;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a memory.
        /// </summary>
        public Memory()
        {
        }

        #endregion
    }
}
