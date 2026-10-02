namespace NotDory.Core.Models
{
    using System;
    using NotDory.Core.Enums;
    using NotDory.Core.Helpers;

    /// <summary>
    /// A configured AI model endpoint (embedding or inference), including health-check parameters. NotDory
    /// computes embeddings and runs inference through these endpoints; RecallDB is bring-your-own-vector.
    /// </summary>
    public class ModelEndpoint
    {
        #region Public-Members

        /// <summary>
        /// Endpoint identifier. Defaults to a generated value; may not be set to null or empty.
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
        /// Human-readable endpoint name. May not be set to null or empty.
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Name));
                _Name = value;
            }
        }

        /// <summary>
        /// Whether this endpoint provides embeddings or inference.
        /// </summary>
        public EndpointKindEnum Kind
        {
            get
            {
                return _Kind;
            }
            set
            {
                // Rerankers are ordinary inference endpoints; the legacy Rerank kind is stored as Inference.
                EndpointKindEnum kind = InputGuard.Defined(value, EndpointKindEnum.Embedding);
                _Kind = kind == EndpointKindEnum.Rerank ? EndpointKindEnum.Inference : kind;
            }
        }

        /// <summary>
        /// The wire format the endpoint speaks.
        /// </summary>
        public ApiFormatEnum ApiFormat
        {
            get
            {
                return _ApiFormat;
            }
            set
            {
                _ApiFormat = InputGuard.Defined(value, ApiFormatEnum.OpenAI);
            }
        }

        /// <summary>
        /// The full base URL of the endpoint, onto which the API-format-specific path (for example
        /// <c>/api/embed</c> or <c>/v1/embeddings</c>) is appended. Example:
        /// <c>http://conductor.example.com:8900/v1.0/api/all-minilm-latest</c>.
        /// </summary>
        public string BaseUrl
        {
            get
            {
                return _BaseUrl;
            }
            set
            {
                _BaseUrl = InputGuard.MaxLength(value ?? string.Empty, 2048, nameof(BaseUrl))!;
            }
        }

        /// <summary>
        /// The authentication mechanism NotDory applies when calling this endpoint.
        /// </summary>
        public EndpointAuthTypeEnum AuthType
        {
            get
            {
                return _AuthType;
            }
            set
            {
                _AuthType = InputGuard.Defined(value, EndpointAuthTypeEnum.None);
            }
        }

        /// <summary>
        /// For <see cref="EndpointAuthTypeEnum.ApiKeyHeader"/>, the request header name that carries the key
        /// (for example <c>x-api-key</c>). For <see cref="EndpointAuthTypeEnum.AccessKeySecret"/>, the header
        /// that carries the access key.
        /// </summary>
        public string? AuthHeaderName { get; set; } = null;

        /// <summary>
        /// For <see cref="EndpointAuthTypeEnum.AccessKeySecret"/>, the request header name that carries the
        /// secret key.
        /// </summary>
        public string? AuthSecretHeaderName { get; set; } = null;

        /// <summary>
        /// For <see cref="EndpointAuthTypeEnum.QueryParam"/>, the query-string parameter name that carries the
        /// key (for example <c>key</c>).
        /// </summary>
        public string? AuthQueryParam { get; set; } = null;

        /// <summary>
        /// The credential identifier: the username for <see cref="EndpointAuthTypeEnum.BasicAuth"/>, or the
        /// access key for <see cref="EndpointAuthTypeEnum.AccessKeySecret"/>. Unused by other schemes.
        /// </summary>
        public string? AuthKeyId { get; set; } = null;

        /// <summary>
        /// The secret credential material: the bearer token, header value, query value, Basic password, or
        /// secret key, depending on <see cref="AuthType"/>.
        /// </summary>
        public string? AuthSecret { get; set; } = null;

        /// <summary>
        /// The model identifier to request (for example an embedding or completion model name).
        /// </summary>
        public string? Model
        {
            get
            {
                return _Model;
            }
            set
            {
                _Model = InputGuard.MaxLength(value, 256, nameof(Model));
            }
        }

        /// <summary>
        /// Optional override of the model's maximum input token budget used for chunk sizing. Zero means the
        /// budget is resolved automatically from the API format and model name.
        /// </summary>
        public int MaxInputTokens
        {
            get
            {
                return _MaxInputTokens;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(MaxInputTokens), "MaxInputTokens may not be negative.");
                _MaxInputTokens = Math.Min(value, 1000000);
            }
        }

        /// <summary>
        /// For embedding endpoints, the vector dimensionality produced. Zero when unknown.
        /// </summary>
        public int Dimensionality
        {
            get
            {
                return _Dimensionality;
            }
            set
            {
                if (value < 0) throw new ArgumentOutOfRangeException(nameof(Dimensionality), "Dimensionality may not be negative.");
                _Dimensionality = Math.Min(value, 16384);
            }
        }

        /// <summary>
        /// Request timeout in milliseconds. Default 60000.
        /// </summary>
        public int TimeoutMs
        {
            get
            {
                return _TimeoutMs;
            }
            set
            {
                _TimeoutMs = value <= 0 ? 60000 : InputGuard.Clamp(value, 1000, 3600000);
            }
        }

        /// <summary>
        /// How much a reasoning model thinks on the calls NotDory makes to this inference endpoint (chat answers, query steps,
        /// and reranking). Default sends nothing, so the model's own default applies; an undefined value falls back to
        /// Default. Thinking costs seconds per call: a reranker or query model usually wants Off or Low.
        /// </summary>
        public ReasoningModeEnum Reasoning
        {
            get
            {
                return _Reasoning;
            }
            set
            {
                _Reasoning = InputGuard.Defined(value, ReasoningModeEnum.Default);
            }
        }

        /// <summary>
        /// Indicates whether the endpoint is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// What the health check requests: either a path appended to the base URL (for example "/api/tags"), or a full
        /// http or https URL used as-is (for example "http://proxy.example.com:8900/" when the base URL points at one
        /// model behind a proxy). Default "/".
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the value has a scheme that is not an absolute http or https URL.</exception>
        public string HealthCheckUrl
        {
            get
            {
                return _HealthCheckUrl;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    _HealthCheckUrl = "/";
                    return;
                }

                string trimmed = InputGuard.MaxLength(value.Trim(), 2048, nameof(HealthCheckUrl))!;
                if (trimmed.Contains("://", StringComparison.Ordinal) && !IsAbsoluteHttpUrl(trimmed))
                    throw new ArgumentException("The health check URL must be a path such as /health or a full http or https URL.", nameof(HealthCheckUrl));
                _HealthCheckUrl = trimmed;
            }
        }

        /// <summary>
        /// The health-check HTTP method.
        /// </summary>
        public HealthCheckMethodEnum HealthCheckMethod { get; set; } = HealthCheckMethodEnum.GET;

        /// <summary>
        /// The health-check interval in milliseconds. Default 5000.
        /// </summary>
        public int HealthCheckIntervalMs
        {
            get
            {
                return _HealthCheckIntervalMs;
            }
            set
            {
                _HealthCheckIntervalMs = InputGuard.Clamp(value, 1000, 3600000);
            }
        }

        /// <summary>
        /// The health-check timeout in milliseconds. Default 5000.
        /// </summary>
        public int HealthCheckTimeoutMs
        {
            get
            {
                return _HealthCheckTimeoutMs;
            }
            set
            {
                _HealthCheckTimeoutMs = InputGuard.Clamp(value, 100, 60000);
            }
        }

        /// <summary>
        /// The HTTP status code considered healthy. Default 200.
        /// </summary>
        public int HealthCheckExpectedStatusCode
        {
            get
            {
                return _HealthCheckExpectedStatusCode;
            }
            set
            {
                _HealthCheckExpectedStatusCode = value < 100 || value > 599 ? 200 : value;
            }
        }

        /// <summary>
        /// Consecutive healthy probes required before flipping to healthy. Default 2.
        /// </summary>
        public int HealthyThreshold
        {
            get
            {
                return _HealthyThreshold;
            }
            set
            {
                _HealthyThreshold = InputGuard.Clamp(value, 1, 100);
            }
        }

        /// <summary>
        /// Consecutive unhealthy probes required before flipping to unhealthy. Default 2.
        /// </summary>
        public int UnhealthyThreshold
        {
            get
            {
                return _UnhealthyThreshold;
            }
            set
            {
                _UnhealthyThreshold = InputGuard.Clamp(value, 1, 100);
            }
        }

        /// <summary>
        /// Whether the health-check request includes the endpoint's auth credential.
        /// </summary>
        public bool HealthCheckUseAuth { get; set; } = false;

        /// <summary>
        /// UTC timestamp when the endpoint was created.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// UTC timestamp when the endpoint was last updated.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        #endregion

        #region Private-Members

        private EndpointAuthTypeEnum _AuthType = EndpointAuthTypeEnum.None;
        private ApiFormatEnum _ApiFormat = ApiFormatEnum.OpenAI;
        private EndpointKindEnum _Kind = EndpointKindEnum.Embedding;
        private int _UnhealthyThreshold = 2;
        private int _HealthyThreshold = 2;
        private int _HealthCheckExpectedStatusCode = 200;
        private int _HealthCheckTimeoutMs = 5000;
        private int _HealthCheckIntervalMs = 5000;
        private string _HealthCheckUrl = "/";
        private int _TimeoutMs = 60000;
        private string? _Model = null;
        private string _BaseUrl = "http://127.0.0.1:11434";
        private string _Id = IdGenerator.EmbeddingEndpoint();
        private string _TenantId = String.Empty;
        private string _Name = String.Empty;
        private int _Dimensionality = 0;
        private int _MaxInputTokens = 0;
        private ReasoningModeEnum _Reasoning = ReasoningModeEnum.Default;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a model endpoint.
        /// </summary>
        public ModelEndpoint()
        {
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Get the normalized base URL for the endpoint (any trailing slash removed), onto which
        /// API-format-specific paths are appended.
        /// </summary>
        /// <returns>The base URL.</returns>
        public string GetBaseUrl()
        {
            return String.IsNullOrEmpty(BaseUrl) ? String.Empty : BaseUrl.TrimEnd('/');
        }

        /// <summary>
        /// Get the URL the health check requests: <see cref="HealthCheckUrl"/> itself when it is a full http or https
        /// URL, otherwise that path appended to the base URL.
        /// </summary>
        /// <returns>The health check URL.</returns>
        public string GetHealthCheckUrl()
        {
            if (IsAbsoluteHttpUrl(HealthCheckUrl)) return HealthCheckUrl;
            string path = HealthCheckUrl.StartsWith("/", StringComparison.Ordinal) ? HealthCheckUrl : "/" + HealthCheckUrl;
            return GetBaseUrl() + path;
        }

        #endregion

        #region Private-Methods

        private static bool IsAbsoluteHttpUrl(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && !string.IsNullOrEmpty(uri.Host);
        }

        #endregion
    }
}
