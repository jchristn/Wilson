namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Catalog of shared Wilson Touchstone suites.
    /// </summary>
    public static class TouchstoneSuiteCatalog
    {
        private static readonly string SharedSuiteLockPath = Path.Combine(Path.GetTempPath(), "wilson-touchstone-shared-suite.lock");

        /// <summary>
        /// Get all shared Wilson test suites.
        /// </summary>
        /// <returns>Test suite descriptors.</returns>
        public static IReadOnlyList<TestSuiteDescriptor> GetSuites()
        {
            return WilsonSuites.GetSuites();
        }

        /// <summary>
        /// Run a shared suite through one adapter process at a time.
        /// </summary>
        /// <param name="runAsync">Suite runner.</param>
        public static async Task RunWithSharedSuiteLockAsync(Func<Task> runAsync)
        {
            ArgumentNullException.ThrowIfNull(runAsync);
            using FileStream lockFile = await AcquireSharedSuiteLockAsync().ConfigureAwait(false);
            await runAsync().ConfigureAwait(false);
        }

        private static async Task<FileStream> AcquireSharedSuiteLockAsync()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromMinutes(5))
            {
                try
                {
                    return new FileStream(SharedSuiteLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }
                catch (IOException)
                {
                    await Task.Delay(250).ConfigureAwait(false);
                }
            }

            throw new TimeoutException("Timed out waiting for the Wilson shared Touchstone suite lock.");
        }
    }
}
