namespace NotDory.Server.Models
{
    using System.Collections.Generic;
    using System.Linq;
    using NotDory.Core.Helpers;

    /// <summary>
    /// A request carrying a list of identifiers, used by the uniform batch-get and batch-delete endpoints.
    /// </summary>
    public class BatchIdsRequest
    {
        #region Public-Members

        /// <summary>
        /// The identifiers to operate on.
        /// </summary>
        public List<string> Ids
        {
            get
            {
                return _Ids;
            }
            set
            {
                _Ids = InputGuard.CleanList(value, 1000, 128, nameof(Ids)).Distinct().ToList();
            }
        }

        #endregion

        #region Private-Members

        private List<string> _Ids = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a batch identifiers request.
        /// </summary>
        public BatchIdsRequest()
        {
        }

        #endregion
    }
}
