namespace Isis.Core.Models
{
    using System;
    using Isis.Core.Helpers;

    /// <summary>
    /// One earlier message in a chat conversation, sent by the caller so a follow-up question can be understood.
    /// </summary>
    public class ChatTurn
    {
        #region Public-Members

        /// <summary>
        /// Who sent the message: "user" or "assistant". Any other value is stored as "user".
        /// </summary>
        public string Role
        {
            get
            {
                return _Role;
            }
            set
            {
                _Role = string.Equals(value?.Trim(), "assistant", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
            }
        }

        /// <summary>
        /// The message text, at most 20,000 characters.
        /// </summary>
        public string Content
        {
            get
            {
                return _Content;
            }
            set
            {
                _Content = InputGuard.MaxLength(value ?? string.Empty, 20000, nameof(Content))!;
            }
        }

        #endregion

        #region Private-Members

        private string _Content = string.Empty;
        private string _Role = "user";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a chat turn.
        /// </summary>
        public ChatTurn()
        {
        }

        #endregion
    }
}
