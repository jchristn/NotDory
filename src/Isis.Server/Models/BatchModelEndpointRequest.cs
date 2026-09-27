namespace Isis.Server.Models
{
    using System.Collections.Generic;
    using System.Linq;
    using Isis.Core.Helpers;
    using Isis.Core.Models;

    /// <summary>
    /// A request carrying a list of model endpoints, used by the uniform batch create endpoint.
    /// </summary>
    public class BatchModelEndpointRequest
    {
        #region Public-Members

        /// <summary>
        /// The model endpoints to create.
        /// </summary>
        public List<ModelEndpoint> Items
        {
            get
            {
                return _Items;
            }
            set
            {
                _Items = InputGuard.MaxCount((value ?? new List<ModelEndpoint>()).Where(i => i != null).ToList(), 100, nameof(Items))!;
            }
        }

        #endregion

        #region Private-Members

        private List<ModelEndpoint> _Items = new List<ModelEndpoint>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a batch model endpoint request.
        /// </summary>
        public BatchModelEndpointRequest()
        {
        }

        #endregion
    }
}
