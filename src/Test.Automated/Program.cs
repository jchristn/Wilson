namespace Test.Automated
{
    using System;
    using System.Threading.Tasks;
    using Test.Shared;

    /// <summary>
    /// Automated test runner.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        public static async Task<int> Main(string[] args)
        {
            try
            {
                await WilsonSuites.RunAllAsync().ConfigureAwait(false);
                Console.WriteLine("PASS Wilson automated tests");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL " + ex.Message);
                return 1;
            }
        }
    }
}
