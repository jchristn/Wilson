namespace Wilson.Core.Helpers
{
    using PrettyId;

    /// <summary>
    /// Generates prefixed K-sortable identifiers.
    /// </summary>
    public static class IdGenerator
    {
        private static readonly PrettyId.IdGenerator _Generator = new PrettyId.IdGenerator();
        private const int _Length = 32;

        /// <summary>
        /// Generate a tenant identifier.
        /// </summary>
        public static string Tenant()
        {
            return _Generator.GenerateKSortable("ten_", _Length);
        }

        /// <summary>
        /// Generate a user identifier.
        /// </summary>
        public static string User()
        {
            return _Generator.GenerateKSortable("usr_", _Length);
        }

        /// <summary>
        /// Generate a credential identifier.
        /// </summary>
        public static string Credential()
        {
            return _Generator.GenerateKSortable("crd_", _Length);
        }

        /// <summary>
        /// Generate a conversation identifier.
        /// </summary>
        public static string Conversation()
        {
            return _Generator.GenerateKSortable("cnv_", _Length);
        }

        /// <summary>
        /// Generate a message identifier.
        /// </summary>
        public static string Message()
        {
            return _Generator.GenerateKSortable("msg_", _Length);
        }

        /// <summary>
        /// Generate a feedback identifier.
        /// </summary>
        public static string Feedback()
        {
            return _Generator.GenerateKSortable("fbk_", _Length);
        }

        /// <summary>
        /// Generate a request history identifier.
        /// </summary>
        public static string Request()
        {
            return _Generator.GenerateKSortable("req_", _Length);
        }

        /// <summary>
        /// Generate a tool run identifier.
        /// </summary>
        public static string ToolRun()
        {
            return _Generator.GenerateKSortable("trn_", _Length);
        }

        /// <summary>
        /// Generate a tool call identifier.
        /// </summary>
        public static string ToolCall()
        {
            return _Generator.GenerateKSortable("tcl_", _Length);
        }

        /// <summary>
        /// Generate a tool execution record identifier.
        /// </summary>
        public static string ToolExecution()
        {
            return _Generator.GenerateKSortable("tex_", _Length);
        }

        /// <summary>
        /// Generate an auth session identifier.
        /// </summary>
        public static string Session()
        {
            return _Generator.GenerateKSortable("ses_", _Length);
        }

        /// <summary>
        /// Generate a bearer token.
        /// </summary>
        public static string Token()
        {
            return _Generator.Generate(64);
        }
    }
}
