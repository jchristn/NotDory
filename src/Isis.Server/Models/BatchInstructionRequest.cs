namespace Isis.Server.Models
{
    using System.Collections.Generic;
    using System.Linq;
    using Isis.Core.Helpers;
    using Isis.Core.Models;

    /// <summary>
    /// A request carrying a list of instructions, used by the uniform batch create endpoint.
    /// </summary>
    public class BatchInstructionRequest
    {
        #region Public-Members

        /// <summary>
        /// The instructions to create.
        /// </summary>
        public List<Instruction> Items
        {
            get
            {
                return _Items;
            }
            set
            {
                _Items = InputGuard.MaxCount((value ?? new List<Instruction>()).Where(i => i != null).ToList(), 100, nameof(Items))!;
            }
        }

        #endregion

        #region Private-Members

        private List<Instruction> _Items = new List<Instruction>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a batch instruction request.
        /// </summary>
        public BatchInstructionRequest()
        {
        }

        #endregion
    }
}
