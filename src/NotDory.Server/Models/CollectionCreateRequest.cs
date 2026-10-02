namespace NotDory.Server.Models
{
    using System;
    using NotDory.Core.Helpers;

    /// <summary>
    /// A request to create a RecallDB collection via the pass-through.
    /// </summary>
    public class CollectionCreateRequest
    {
        #region Public-Members

        /// <summary>
        /// The collection name.
        /// </summary>
        public string Name
        {
            get
            {
                return _Name;
            }
            set
            {
                _Name = InputGuard.MaxLength(value ?? string.Empty, 256, nameof(Name))!;
            }
        }

        /// <summary>
        /// The vector dimensionality.
        /// </summary>
        public int Dimensionality
        {
            get
            {
                return _Dimensionality;
            }
            set
            {
                _Dimensionality = value < 0 ? 0 : Math.Min(value, 16384);
            }
        }

        /// <summary>
        /// An optional description.
        /// </summary>
        public string? Description
        {
            get
            {
                return _Description;
            }
            set
            {
                _Description = InputGuard.MaxLength(value, 4096, nameof(Description));
            }
        }

        #endregion

        #region Private-Members

        private string? _Description = null;
        private int _Dimensionality = 0;
        private string _Name = string.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a collection create request.
        /// </summary>
        public CollectionCreateRequest()
        {
        }

        #endregion
    }
}
