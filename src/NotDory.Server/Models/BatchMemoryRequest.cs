namespace NotDory.Server.Models
{
    using System.Collections.Generic;
    using System.Linq;
    using NotDory.Core.Helpers;
    using NotDory.Core.Models;

    /// <summary>
    /// A request carrying a list of memories, used by the uniform batch upsert endpoint.
    /// </summary>
    public class BatchMemoryRequest
    {
        #region Public-Members

        /// <summary>
        /// The memories to create or update.
        /// </summary>
        public List<Memory> Items
        {
            get
            {
                return _Items;
            }
            set
            {
                _Items = InputGuard.MaxCount((value ?? new List<Memory>()).Where(i => i != null).ToList(), 100, nameof(Items))!;
            }
        }

        #endregion

        #region Private-Members

        private List<Memory> _Items = new List<Memory>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a batch memory request.
        /// </summary>
        public BatchMemoryRequest()
        {
        }

        #endregion
    }
}
