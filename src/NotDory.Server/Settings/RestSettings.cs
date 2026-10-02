namespace NotDory.Server.Settings
{
    using System;
    using NotDory.Core.Helpers;

    /// <summary>
    /// REST listener settings.
    /// </summary>
    public class RestSettings
    {
        #region Public-Members

        /// <summary>
        /// The hostname to bind. Defaults to 127.0.0.1.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                _Hostname = string.IsNullOrWhiteSpace(value) ? "127.0.0.1" : value.Trim();
            }
        }

        /// <summary>
        /// The port to listen on. Defaults to 8700.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                _Port = value < 1 || value > 65535 ? 8700 : value;
            }
        }

        /// <summary>
        /// Whether TLS is enabled.
        /// </summary>
        public bool Ssl { get; set; } = false;

        /// <summary>
        /// Largest request body accepted, in bytes; larger requests are answered with 413. Minimum 1 KB, maximum 256 MB,
        /// default 16 MB.
        /// </summary>
        public long MaxRequestBodyBytes
        {
            get
            {
                return _MaxRequestBodyBytes;
            }
            set
            {
                _MaxRequestBodyBytes = Math.Clamp(value, 1024L, 256L * 1024 * 1024);
            }
        }

        #endregion

        #region Private-Members

        private long _MaxRequestBodyBytes = 16L * 1024 * 1024;
        private int _Port = 8700;
        private string _Hostname = "127.0.0.1";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate REST settings.
        /// </summary>
        public RestSettings()
        {
        }

        #endregion
    }
}
