namespace Wilson.Server
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Application entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        public static async Task Main(string[] args)
        {
            WilsonServer server = await WilsonServer.CreateAsync(args).ConfigureAwait(false);
            await server.RunAsync().ConfigureAwait(false);
        }
    }
}
