namespace Test.Shared
{
    using System.Collections.Generic;
    using Touchstone.Core;

    /// <summary>
    /// Catalog of shared Wilson Touchstone suites.
    /// </summary>
    public static class TouchstoneSuiteCatalog
    {
        /// <summary>
        /// Get all shared Wilson test suites.
        /// </summary>
        /// <returns>Test suite descriptors.</returns>
        public static IReadOnlyList<TestSuiteDescriptor> GetSuites()
        {
            return WilsonSuites.GetSuites();
        }
    }
}
