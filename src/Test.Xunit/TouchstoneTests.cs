namespace Test.Xunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using global::Xunit;

    /// <summary>
    /// xUnit adapter for the shared Wilson Touchstone suites.
    /// </summary>
    public sealed class TouchstoneTests : TouchstoneFactBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return TouchstoneSuiteCatalog.GetSuites(); }
        }

        /// <summary>
        /// Execute all shared Touchstone suites.
        /// </summary>
        [Fact]
        public async Task SharedTouchstoneSuitesPass()
        {
            await RunAllAsync();
        }
    }
}
