namespace Isis.Core.Models
{
    using System;
    using Isis.Core.Helpers;

    /// <summary>
    /// Query parameters for paginated enumeration of records.
    /// </summary>
    public class EnumerationQuery
    {
        #region Public-Members

        /// <summary>
        /// Maximum number of records to return. Minimum 1, maximum 1000, default 100.
        /// </summary>
        public int MaxResults
        {
            get
            {
                return _MaxResults;
            }
            set
            {
                if (value < 1) value = 1;
                if (value > 1000) value = 1000;
                _MaxResults = value;
            }
        }

        /// <summary>
        /// Number of records to skip from the start of the ordered result set. Minimum 0.
        /// </summary>
        public int Skip
        {
            get
            {
                return _Skip;
            }
            set
            {
                if (value < 0) value = 0;
                if (value > 10000000) value = 10000000;
                _Skip = value;
            }
        }

        /// <summary>
        /// Optional case-insensitive search term applied to the primary text column(s).
        /// </summary>
        public string? SearchTerm
        {
            get
            {
                return _SearchTerm;
            }
            set
            {
                _SearchTerm = InputGuard.MaxLength(value, 256, nameof(SearchTerm));
            }
        }

        /// <summary>
        /// Optional continuation token from a prior enumeration.
        /// </summary>
        public string? ContinuationToken
        {
            get
            {
                return _ContinuationToken;
            }
            set
            {
                _ContinuationToken = InputGuard.MaxLength(value, 1024, nameof(ContinuationToken));
            }
        }

        #endregion

        #region Private-Members

        private string? _ContinuationToken = null;
        private string? _SearchTerm = null;
        private int _MaxResults = 100;
        private int _Skip = 0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate an enumeration query.
        /// </summary>
        public EnumerationQuery()
        {
        }

        #endregion
    }
}
