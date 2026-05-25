using WinformsMVP.Services;

namespace WinformsMVP.Modules
{
    /// <summary>
    /// Contract for a UI module that knows how to register its own Views with an
    /// <see cref="IViewMappingRegister"/>. Lets a multi-project solution delegate
    /// view-registration to each module, so the Shell project does not have to
    /// know which Views every module owns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Scope.</b> This interface is intentionally small. It only deals with the
    /// view-mapping concern, so the main <c>WinformsMVP</c> package can stay free
    /// of dependency-injection-container references. Modules that also need to
    /// register services to a DI container should implement the richer
    /// <c>IModuleRegistrar</c> from <c>WinformsMVP.DependencyInjection</c>, which
    /// extends this interface.
    /// </para>
    /// <para>
    /// <b>Typical implementation</b> just forwards to
    /// <c>RegisterFromAssembly(typeof(SomeTypeInThisModule).Assembly)</c> so
    /// every Form in the module is picked up by convention.
    /// </para>
    /// </remarks>
    public interface IViewModuleRegistrar
    {
        /// <summary>
        /// Registers every View the module owns into <paramref name="registry"/>.
        /// Called once at application startup from the Shell project.
        /// </summary>
        void RegisterViews(IViewMappingRegister registry);
    }
}
