namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// NUnit adapter for the shared Wilson Touchstone suites.
    /// </summary>
    public sealed class TouchstoneTests : TouchstoneNunitBase
    {
        /// <inheritdoc />
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return TouchstoneSuiteCatalog.GetSuites(); }
        }

        /// <summary>
        /// Execute all shared Touchstone suites.
        /// </summary>
        [Test]
        public async Task SharedTouchstoneSuitesPass()
        {
            await TouchstoneSuiteCatalog.RunWithSharedSuiteLockAsync(() => RunAllAsync());
        }
    }
}
