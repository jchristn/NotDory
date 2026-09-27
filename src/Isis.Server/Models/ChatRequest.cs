namespace Isis.Server.Models
{
    using System.Collections.Generic;
    using Isis.Core.Helpers;
    using Isis.Core.Models;

    /// <summary>
    /// A chat-with-memory request body.
    /// </summary>
    public class ChatRequest
    {
        #region Public-Members

        /// <summary>
        /// The natural-language question.
        /// </summary>
        public string Question
        {
            get
            {
                return _Question;
            }
            set
            {
                _Question = InputGuard.MaxLength(value ?? string.Empty, 8000, nameof(Question))!;
            }
        }

        /// <summary>
        /// The inference endpoint to use. When null, the tenant's first active inference endpoint is used.
        /// </summary>
        public string? InferenceEndpointId
        {
            get
            {
                return _InferenceEndpointId;
            }
            set
            {
                _InferenceEndpointId = InputGuard.MaxLength(value, 128, nameof(InferenceEndpointId));
            }
        }

        /// <summary>
        /// The maximum number of memories to retrieve. 0 (the default) uses the server's default, which is 8 unless
        /// configured otherwise (see <c>MemoryChatService.DefaultTopK</c>).
        /// </summary>
        public int TopK
        {
            get
            {
                return _TopK;
            }
            set
            {
                _TopK = InputGuard.Clamp(value, 0, 100);
            }
        }

        /// <summary>
        /// Earlier messages in the conversation, oldest first, each with a role ("user" or "assistant") and content.
        /// When present, a follow-up question is rewritten into a standalone query for retrieval and the answer prompt
        /// shows the recent conversation. Only the most recent messages are used (server setting
        /// <c>retrieval.chatHistoryTurns</c>, default 6). Null or empty for a single question.
        /// </summary>
        public List<ChatTurn>? History
        {
            get
            {
                return _History;
            }
            set
            {
                if (value == null)
                {
                    _History = null;
                    return;
                }
                
                List<ChatTurn> cleaned = new List<ChatTurn>();
                foreach (ChatTurn? turn in value)
                {
                    if (turn != null && !string.IsNullOrWhiteSpace(turn.Content)) cleaned.Add(turn);
                }
                
                _History = InputGuard.MaxCount(cleaned, 100, nameof(History));
            }
        }

        #endregion

        #region Private-Members

        private List<ChatTurn>? _History = null;
        private int _TopK = 0;
        private string? _InferenceEndpointId = null;
        private string _Question = string.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a chat request.
        /// </summary>
        public ChatRequest()
        {
        }

        #endregion
    }
}
