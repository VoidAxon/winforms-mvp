// Polyfill for [CallerMemberName] / [CallerFilePath] / [CallerLineNumber]
// on .NET Framework 4.0 (these attributes shipped in BCL starting with 4.5).
//
// The C# 5+ compiler recognises these attributes BY NAME and NAMESPACE,
// regardless of which assembly defines them and regardless of accessibility.
// Declaring them as `internal` keeps them out of the public API surface and
// avoids CS0436 collisions for net48+ consumers whose BCL already defines
// the real attributes.

#if NET40
namespace System.Runtime.CompilerServices
{
    [System.AttributeUsage(System.AttributeTargets.Parameter, Inherited = false)]
    internal sealed class CallerMemberNameAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Parameter, Inherited = false)]
    internal sealed class CallerFilePathAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Parameter, Inherited = false)]
    internal sealed class CallerLineNumberAttribute : System.Attribute { }
}
#endif
