namespace NotDory.McpServer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Voltaic.Core;

    /// <summary>
    /// A tool the MCP server publishes, kept so the tool can be re-registered when an administrator edits its description.
    /// </summary>
    public class McpToolRegistration
    {
        #region Public-Members

        /// <summary>
        /// Tool name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// JSON schema of the tool's input.
        /// </summary>
        public object InputSchema { get; }

        /// <summary>
        /// The tool's handler.
        /// </summary>
        public Func<RpcParameters?, CancellationToken, Task<object>> Handler { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a registration.
        /// </summary>
        /// <param name="name">Tool name.</param>
        /// <param name="inputSchema">Input schema.</param>
        /// <param name="handler">Handler.</param>
        /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
        public McpToolRegistration(string name, object inputSchema, Func<RpcParameters?, CancellationToken, Task<object>> handler)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            InputSchema = inputSchema ?? throw new ArgumentNullException(nameof(inputSchema));
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        #endregion
    }
}
