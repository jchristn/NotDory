namespace NotDory.Server.Settings
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using NotDory.Core.Database;

    /// <summary>
    /// Root server settings, loaded from a JSON file and overridable by environment variables.
    /// </summary>
    public class NotDorySettings
    {
        #region Public-Members

        /// <summary>
        /// The node identifier for this server instance.
        /// </summary>
        public string NodeId
        {
            get
            {
                return _NodeId;
            }
            set
            {
                _NodeId = string.IsNullOrWhiteSpace(value) ? "notdory-1" : value.Trim();
            }
        }

        /// <summary>
        /// REST listener settings.
        /// </summary>
        public RestSettings Rest
        {
            get
            {
                return _Rest;
            }
            set
            {
                _Rest = value ?? new RestSettings();
            }
        }

        /// <summary>
        /// Relational metadata store settings.
        /// </summary>
        public DatabaseSettings Database
        {
            get
            {
                return _Database;
            }
            set
            {
                _Database = value ?? new DatabaseSettings();
            }
        }

        /// <summary>
        /// RecallDB integration settings.
        /// </summary>
        public RecallDbSettings RecallDb
        {
            get
            {
                return _RecallDb;
            }
            set
            {
                _RecallDb = value ?? new RecallDbSettings();
            }
        }

        /// <summary>
        /// Authentication settings.
        /// </summary>
        public AuthSettings Auth
        {
            get
            {
                return _Auth;
            }
            set
            {
                _Auth = value ?? new AuthSettings();
            }
        }

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging
        {
            get
            {
                return _Logging;
            }
            set
            {
                _Logging = value ?? new LoggingSettings();
            }
        }

        /// <summary>
        /// Request history capture settings.
        /// </summary>
        public RequestHistorySettings RequestHistory
        {
            get
            {
                return _RequestHistory;
            }
            set
            {
                _RequestHistory = value ?? new RequestHistorySettings();
            }
        }

        /// <summary>
        /// Lookup cache settings (credentials, users, scopes, and endpoints read on every request).
        /// </summary>
        public CacheSettings Cache
        {
            get
            {
                return _Cache;
            }
            set
            {
                _Cache = value ?? new CacheSettings();
            }
        }

        /// <summary>
        /// Storage settings (whether new RecallDb scopes mirror to an OKF bundle by default).
        /// </summary>
        public StorageSettings Storage
        {
            get
            {
                return _Storage;
            }
            set
            {
                _Storage = value ?? new StorageSettings();
            }
        }

        /// <summary>
        /// Retrieval settings (similarity check on upsert, reranker input size, chat link expansion).
        /// </summary>
        public RetrievalSettings Retrieval
        {
            get
            {
                return _Retrieval;
            }
            set
            {
                _Retrieval = value ?? new RetrievalSettings();
            }
        }

        /// <summary>
        /// Retention settings for observability history tables (request history and operation events).
        /// </summary>
        public RetentionSettings Retention
        {
            get
            {
                return _Retention;
            }
            set
            {
                _Retention = value ?? new RetentionSettings();
            }
        }

        /// <summary>
        /// What agents are told when they connect (MCP server instructions and tool descriptions), editable by administrators.
        /// </summary>
        public AgentSettings Agent
        {
            get
            {
                return _Agent;
            }
            set
            {
                _Agent = value ?? new AgentSettings();
            }
        }

        /// <summary>
        /// Observability (metrics and tracing) settings.
        /// </summary>
        public ObservabilitySettings Observability
        {
            get
            {
                return _Observability;
            }
            set
            {
                _Observability = value ?? new ObservabilitySettings();
            }
        }

        #endregion

        #region Private-Members

        private string _NodeId = "notdory-1";
        private AgentSettings _Agent = new AgentSettings();
        private ObservabilitySettings _Observability = new ObservabilitySettings();
        private RetentionSettings _Retention = new RetentionSettings();
        private RetrievalSettings _Retrieval = new RetrievalSettings();
        private StorageSettings _Storage = new StorageSettings();
        private CacheSettings _Cache = new CacheSettings();
        private RequestHistorySettings _RequestHistory = new RequestHistorySettings();
        private LoggingSettings _Logging = new LoggingSettings();
        private AuthSettings _Auth = new AuthSettings();
        private RecallDbSettings _RecallDb = new RecallDbSettings();
        private DatabaseSettings _Database = new DatabaseSettings();
        private RestSettings _Rest = new RestSettings();
        private static readonly JsonSerializerOptions _Options = BuildOptions();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate default settings.
        /// </summary>
        public NotDorySettings()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Load settings from a JSON file, returning defaults when the file does not exist.
        /// </summary>
        /// <param name="path">The settings file path.</param>
        /// <returns>The loaded or default settings.</returns>
        public static NotDorySettings FromFile(string path)
        {
            if (String.IsNullOrEmpty(path) || !File.Exists(path)) return new NotDorySettings();
            string json = File.ReadAllText(path);
            if (String.IsNullOrWhiteSpace(json)) return new NotDorySettings();
            NotDorySettings? settings = JsonSerializer.Deserialize<NotDorySettings>(json, _Options);
            return settings ?? new NotDorySettings();
        }

        /// <summary>
        /// Persist settings to a JSON file.
        /// </summary>
        /// <param name="path">The settings file path.</param>
        public void ToFile(string path)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, JsonSerializer.Serialize(this, _Options));
        }

        #endregion

        #region Private-Methods

        private static JsonSerializerOptions BuildOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;
            options.WriteIndented = true;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        #endregion
    }
}
