using System.Linq;
using System.Reflection;
using WinformsMVP.MVP.ViewActions;
using Xunit;

namespace WinformsMVP.Samples.Tests.ViewActions
{
    /// <summary>The shipped keys are qualified like a screen's own ("Standard.Save") and never share a name.</summary>
    public class StandardActionsTests
    {
        private static readonly FieldInfo[] Keys = typeof(StandardActions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(ViewAction))
            .ToArray();

        [Fact]
        public void EveryKey_IsStandardDotItsName()
        {
            Assert.NotEmpty(Keys);
            Assert.All(Keys, f => Assert.Equal("Standard." + f.Name, ((ViewAction)f.GetValue(null)).Name));
        }

        [Fact]
        public void NoTwoKeysShareAName()
        {
            // FileSave and Save used to be the same key ("Save"); FileOpen / Open and FileClose / Close too.
            var names = Keys.Select(f => ((ViewAction)f.GetValue(null)).Name).ToList();
            Assert.Equal(names.Count, names.Distinct().Count());
            Assert.NotEqual(StandardActions.Save, StandardActions.FileSave);
        }
    }
}
