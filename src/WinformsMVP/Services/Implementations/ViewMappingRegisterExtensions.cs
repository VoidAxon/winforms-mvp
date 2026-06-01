using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using WinformsMVP.MVP.Views;

namespace WinformsMVP.Services.Implementations
{
    /// <summary>
    /// Extension methods for IViewMappingRegister.
    /// Provides convenient features such as automatic scan registration.
    /// </summary>
    public static class ViewMappingRegisterExtensions
    {
        /// <summary>
        /// Wraps the register with a <see cref="ServiceProviderViewMappingRegister"/> decorator
        /// so that unregistered View interfaces fall back to the supplied
        /// <see cref="IServiceProvider"/>. Explicit registrations in the underlying register
        /// always take precedence.
        /// </summary>
        /// <param name="register">The underlying register (kept as-is and used as inner).</param>
        /// <param name="serviceProvider">DI container surfaced via the BCL abstraction.</param>
        /// <returns>A decorator that consults <paramref name="register"/> first, then
        /// <paramref name="serviceProvider"/> as a fallback.</returns>
        /// <remarks>
        /// The returned register holds a reference to <paramref name="register"/>; further
        /// <c>Register</c> calls on either object affect the same underlying mapping table.
        /// </remarks>
        public static IViewMappingRegister WithServiceProvider(
            this IViewMappingRegister register,
            IServiceProvider serviceProvider)
        {
            if (register == null) throw new ArgumentNullException(nameof(register));
            if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

            return new ServiceProviderViewMappingRegister(register, serviceProvider);
        }

        /// <summary>
        /// Automatically scans and registers mappings between View interfaces and implementation Forms from the specified assembly.
        /// </summary>
        /// <param name="register">ViewMappingRegister instance</param>
        /// <param name="assembly">Assembly to scan</param>
        /// <param name="allowOverride">Whether to override existing mappings (default: false)</param>
        /// <returns>Number of Views registered</returns>
        /// <remarks>
        /// <para>
        /// This method auto-registers types that meet the following conditions:
        /// <list type="bullet">
        /// <item>Inherits from Form</item>
        /// <item>Implements IWindowView or IViewBase</item>
        /// <item>Has a public parameterless constructor</item>
        /// <item>Is not an abstract class</item>
        /// </list>
        /// </para>
        /// <para>
        /// Example: If NavigatorDemoForm implements INavigatorDemoView,
        /// Register&lt;INavigatorDemoView, NavigatorDemoForm&gt;() will be called automatically.
        /// </para>
        /// </remarks>
        public static int RegisterFromAssembly(
            this IViewMappingRegister register,
            Assembly assembly,
            bool allowOverride = false)
        {
            if (register == null)
                throw new ArgumentNullException(nameof(register));
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            int registeredCount = 0;

            // Get all types in the assembly
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                // Even if some types cannot be loaded, process only the types that were loaded
                types = ex.Types.Where(t => t != null).ToArray();
            }

            // Search for classes that inherit from Form
            var formTypes = types
                .Where(t => t.IsClass && !t.IsAbstract && typeof(Form).IsAssignableFrom(t))
                .ToList();

            foreach (var formType in formTypes)
            {
                // Check for public parameterless constructor
                var hasParameterlessConstructor = formType.GetConstructor(
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null) != null;

                if (!hasParameterlessConstructor)
                {
                    // Skip if no parameterless constructor (no warning)
                    continue;
                }

                // Search for interfaces that implement IWindowView or IViewBase
                var viewInterface = formType.GetInterfaces()
                    .FirstOrDefault(i =>
                        i != typeof(IWindowView) &&
                        i != typeof(IViewBase) &&
                        (typeof(IWindowView).IsAssignableFrom(i) || typeof(IViewBase).IsAssignableFrom(i)));

                if (viewInterface != null)
                {
                    try
                    {
                        // Call Register<TInterface, TImplementation>() via reflection
                        var registerMethod = typeof(IViewMappingRegister)
                            .GetMethods()
                            .First(m => m.Name == "Register" &&
                                       m.IsGenericMethodDefinition &&
                                       m.GetGenericArguments().Length == 2)
                            .MakeGenericMethod(viewInterface, formType);

                        registerMethod.Invoke(register, new object[] { allowOverride });
                        registeredCount++;
                    }
                    catch (TargetInvocationException ex)
                    {
                        if (ex.InnerException is InvalidOperationException && !allowOverride)
                        {
                            // Skip if already registered (when allowOverride = false)
                            continue;
                        }
                        throw;
                    }
                }
            }

            return registeredCount;
        }

        /// <summary>
        /// Automatically scans and registers mappings between View interfaces and implementation Forms
        /// from all assemblies loaded in the current application domain.
        /// </summary>
        /// <param name="register">ViewMappingRegister instance</param>
        /// <param name="allowOverride">Whether to override existing mappings (default: false)</param>
        /// <param name="excludeSystemAssemblies">Whether to exclude system assemblies (default: true)</param>
        /// <returns>Number of Views registered</returns>
        /// <remarks>
        /// <para>
        /// This method targets all assemblies loaded in the application domain.
        /// Using RegisterFromAssembly() to specify a particular assembly is typically recommended.
        /// </para>
        /// </remarks>
        public static int RegisterFromLoadedAssemblies(
            this IViewMappingRegister register,
            bool allowOverride = false,
            bool excludeSystemAssemblies = true)
        {
            if (register == null)
                throw new ArgumentNullException(nameof(register));

            int totalRegistered = 0;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                // Exclude system assemblies
                if (excludeSystemAssemblies)
                {
                    var assemblyName = assembly.GetName().Name;
                    if (assemblyName.StartsWith("System.") ||
                        assemblyName.StartsWith("Microsoft.") ||
                        assemblyName == "mscorlib" ||
                        assemblyName == "netstandard")
                    {
                        continue;
                    }
                }

                try
                {
                    totalRegistered += RegisterFromAssembly(register, assembly, allowOverride);
                }
                catch
                {
                    // Continue even if assembly scan fails
                    continue;
                }
            }

            return totalRegistered;
        }

        /// <summary>
        /// Automatically scans and registers mappings between View interfaces and implementation Forms within the specified namespace.
        /// </summary>
        /// <param name="register">ViewMappingRegister instance</param>
        /// <param name="assembly">Assembly to scan</param>
        /// <param name="namespacePrefix">Namespace prefix (e.g., "MyApp.Views")</param>
        /// <param name="allowOverride">Whether to override existing mappings (default: false)</param>
        /// <returns>Number of Views registered</returns>
        public static int RegisterFromNamespace(
            this IViewMappingRegister register,
            Assembly assembly,
            string namespacePrefix,
            bool allowOverride = false)
        {
            if (register == null)
                throw new ArgumentNullException(nameof(register));
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            if (string.IsNullOrEmpty(namespacePrefix))
                throw new ArgumentException("Namespace prefix cannot be null or empty", nameof(namespacePrefix));

            int registeredCount = 0;

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            // Filter only Form classes in the specified namespace
            var formTypes = types
                .Where(t => t.IsClass &&
                           !t.IsAbstract &&
                           typeof(Form).IsAssignableFrom(t) &&
                           t.Namespace != null &&
                           t.Namespace.StartsWith(namespacePrefix))
                .ToList();

            foreach (var formType in formTypes)
            {
                var hasParameterlessConstructor = formType.GetConstructor(
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null) != null;

                if (!hasParameterlessConstructor)
                    continue;

                var viewInterface = formType.GetInterfaces()
                    .FirstOrDefault(i =>
                        i != typeof(IWindowView) &&
                        i != typeof(IViewBase) &&
                        (typeof(IWindowView).IsAssignableFrom(i) || typeof(IViewBase).IsAssignableFrom(i)));

                if (viewInterface != null)
                {
                    try
                    {
                        var registerMethod = typeof(IViewMappingRegister)
                            .GetMethods()
                            .First(m => m.Name == "Register" &&
                                       m.IsGenericMethodDefinition &&
                                       m.GetGenericArguments().Length == 2)
                            .MakeGenericMethod(viewInterface, formType);

                        registerMethod.Invoke(register, new object[] { allowOverride });
                        registeredCount++;
                    }
                    catch (TargetInvocationException ex)
                    {
                        if (ex.InnerException is InvalidOperationException && !allowOverride)
                            continue;
                        throw;
                    }
                }
            }

            return registeredCount;
        }
    }
}
