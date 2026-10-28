using Xunit;

namespace Kibinago.Samples.Tests.TestHelpers
{
    /// <summary>
    /// Tests that count the process-wide Application.OpenForms: run alone, after the parallel tests,
    /// since other test classes show and close forms at the same time.
    /// </summary>
    [CollectionDefinition("OpenForms", DisableParallelization = true)]
    public sealed class OpenFormsCollection
    {
    }
}
