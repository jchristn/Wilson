namespace Wilson.Core.Tools
{
    using System;

    /// <summary>
    /// Exception carrying a safe tool error code and message.
    /// </summary>
    public sealed class ToolExecutionException : Exception
    {
        /// <summary>Stable error code.</summary>
        public string Code { get; }

        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="code">Stable error code.</param>
        /// <param name="message">Safe error message.</param>
        public ToolExecutionException(string code, string message) : base(message)
        {
            Code = code;
        }
    }
}
