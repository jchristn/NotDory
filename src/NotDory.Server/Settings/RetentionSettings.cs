namespace NotDory.Server.Settings
{
    using NotDory.Core.Helpers;

    /// <summary>
    /// Retention settings for the observability history tables (request history and operation events).
    /// A background pruner periodically deletes rows older than <see cref="MaxAgeDays"/> from both tables.
    /// </summary>
    public class RetentionSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether the background retention pruner is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum age, in days, to retain request-history entries and operation events. Rows older than this
        /// are deleted on each sweep. Values below 1 are treated as 1.
        /// </summary>
        public int MaxAgeDays
        {
            get
            {
                return _MaxAgeDays;
            }
            set
            {
                _MaxAgeDays = InputGuard.Clamp(value, 1, 3650);
            }
        }

        /// <summary>
        /// Interval, in minutes, between retention sweeps. Values below 1 are treated as 1.
        /// </summary>
        public int SweepIntervalMinutes
        {
            get
            {
                return _SweepIntervalMinutes;
            }
            set
            {
                _SweepIntervalMinutes = InputGuard.Clamp(value, 1, 1440);
            }
        }

        #endregion

        #region Private-Members

        private int _SweepIntervalMinutes = 60;
        private int _MaxAgeDays = 30;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate retention settings.
        /// </summary>
        public RetentionSettings()
        {
        }

        #endregion
    }
}
