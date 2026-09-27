namespace Isis.Core.Stores
{
    using System;
    using Isis.Core.Enums;
    using Isis.Core.Helpers;

    /// <summary>
    /// An extra query searched alongside a search's main query, with its own weight in the fusion of the rankings and,
    /// optionally, its own search mode. Used for query expansion (a drafted answer searched by vector, keywords searched
    /// as text) and available to callers that want to weight their own extra queries.
    /// </summary>
    public class MemorySubQuery
    {
        #region Public-Members

        /// <summary>
        /// The query text, at most 4,000 characters.
        /// </summary>
        public string Text
        {
            get
            {
                return _Text;
            }
            set
            {
                _Text = InputGuard.MaxLength(value ?? string.Empty, 4000, nameof(Text))!;
            }
        }

        /// <summary>
        /// The ranking's weight in the fusion, 0.0 to 1.0, where the main query counts 1.0. Values outside the range are
        /// clamped; 0 leaves the sub-query out. Default 1.0.
        /// </summary>
        public double Weight
        {
            get
            {
                return _Weight;
            }
            set
            {
                _Weight = InputGuard.Clamp(value, 0.0, 1.0, 0.0);
            }
        }

        /// <summary>
        /// The search mode for this sub-query, for example Semantic for a drafted answer or Keyword for a keyword list.
        /// Null (the default) uses the search's mode.
        /// </summary>
        public SearchModeEnum? Mode
        {
            get
            {
                return _Mode;
            }
            set
            {
                _Mode = value.HasValue && Enum.IsDefined(value.Value) ? value : null;
            }
        }

        #endregion

        #region Private-Members

        private SearchModeEnum? _Mode = null;
        private string _Text = string.Empty;
        private double _Weight = 1.0;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a sub-query.
        /// </summary>
        public MemorySubQuery()
        {
        }

        #endregion
    }
}
