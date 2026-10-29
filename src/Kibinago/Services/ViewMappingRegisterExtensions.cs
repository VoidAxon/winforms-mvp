using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kibinago.Views;

namespace Kibinago.Services
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
        /// <item>Inherits from Form (and is not an open generic type)</item>
        /// <item>Implements an application view interface derived from IWindowView — the most derived one
        /// when it implements a chain (IEditView : IBaseView : IWindowView registers IEditView); several
        /// unrelated ones throw (register that Form explicitly)</item>
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

            return RegisterForms(register, types, allowOverride);
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

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            return RegisterForms(register, types.Where(t => t.Namespace != null && t.Namespace.StartsWith(namespacePrefix)), allowOverride);
        }

        // The scan of both entry points: each concrete, non-generic Form with a public parameterless
        // constructor is registered under its window view interface (see SelectWindowContract).
        internal static int RegisterForms(IViewMappingRegister register, IEnumerable<Type> types, bool allowOverride)
        {
            var registeredCount = 0;
            var registerMethod = typeof(IViewMappingRegister)
                .GetMethods()
                .First(m => m.Name == "Register" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 2);

            foreach (var formType in types.Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters && typeof(Form).IsAssignableFrom(t)))
            {
                if (formType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) == null)
                    continue;   // no parameterless constructor: register it with a factory instead

                var viewInterface = SelectWindowContract(formType);
                if (viewInterface == null)
                    continue;

                try
                {
                    registerMethod.MakeGenericMethod(viewInterface, formType).Invoke(register, new object[] { allowOverride });
                    registeredCount++;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException && !allowOverride)
                {
                    // already registered (allowOverride = false): keep the existing mapping
                }
            }

            return registeredCount;
        }

        /// <summary>
        /// The application window view interface a Form is registered under: the interfaces it implements
        /// that derive from <see cref="IWindowView"/> (not IWindowView itself), minus those another one
        /// derives from. <c>null</c> when there is none (a Form that is not an MVP window).
        /// </summary>
        /// <exception cref="InvalidOperationException">Several unrelated window view interfaces remain.</exception>
        internal static Type SelectWindowContract(Type formType)
        {
            var candidates = formType.GetInterfaces()
                .Where(i => i != typeof(IWindowView) && typeof(IWindowView).IsAssignableFrom(i) && !i.ContainsGenericParameters)
                .ToList();
            var mostDerived = candidates.Where(c => !candidates.Any(o => o != c && c.IsAssignableFrom(o))).ToList();

            if (mostDerived.Count > 1)
                throw new InvalidOperationException(
                    $"{formType.Name} implements several window view interfaces ({string.Join(", ", mostDerived.Select(i => i.Name).OrderBy(n => n))}). " +
                    $"Register it explicitly, e.g. Register<{mostDerived[0].Name}, {formType.Name}>(), and exclude it from the scan " +
                    "(RegisterFromNamespace), or give it one view interface.");
            return mostDerived.FirstOrDefault();
        }
    }
}
