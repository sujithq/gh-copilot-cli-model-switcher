using CopilotX.Commands;
using CopilotX.Services;
using Spectre.Console.Cli;

// Build a simple DI-like type registrar so commands can receive ConfigService via constructor.
var registrations = new ServiceCollection();
registrations.AddSingleton<ConfigService>();
var registrar = new TypeRegistrar(registrations);

var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("copilotx");
    config.SetApplicationVersion("1.0.0");

    config.AddCommand<ListCommand>("list")
        .WithAlias("ls")
        .WithDescription("List all configured profiles");

    config.AddCommand<UseCommand>("use")
        .WithDescription("Switch to a profile and launch GitHub Copilot CLI");

    config.AddCommand<AddCommand>("add")
        .WithDescription("Interactively add a new model profile");

    config.AddCommand<LastCommand>("last")
        .WithDescription("Show the last used profile, or re-activate it");

    config.AddCommand<DefaultCommand>("default")
        .WithDescription("Show or set the default profile");

    config.AddCommand<EnvCommand>("env")
        .WithDescription("Print shell export commands for a profile (eval-friendly)");

    config.AddCommand<RemoveCommand>("remove")
        .WithAlias("rm")
        .WithDescription("Remove a profile from the configuration");
});

return app.Run(args);

// ── Minimal DI plumbing ──────────────────────────────────────────────────────

/// <summary>Minimal Microsoft.Extensions.DI-like service collection used by Spectre.Console.Cli.</summary>
internal sealed class ServiceCollection : IEnumerable<ServiceDescriptor>
{
    private readonly List<ServiceDescriptor> _services = [];

    public void AddSingleton<T>() where T : class => _services.Add(ServiceDescriptor.ByType(typeof(T), typeof(T)));
    public void AddSingleton(Type serviceType, object instance) => _services.Add(ServiceDescriptor.ByInstance(serviceType, instance));
    public void AddSingleton(Type serviceType, Type implType) => _services.Add(ServiceDescriptor.ByType(serviceType, implType));

    public IEnumerator<ServiceDescriptor> GetEnumerator() => _services.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => _services.GetEnumerator();
}

internal sealed record ServiceDescriptor(Type ServiceType, Type? ImplementationType, object? Instance)
{
    public static ServiceDescriptor ByType(Type serviceType, Type implType) =>
        new(serviceType, implType, null);

    public static ServiceDescriptor ByInstance(Type serviceType, object instance) =>
        new(serviceType, null, instance);
}

/// <summary>Bridges the service collection to Spectre.Console.Cli's ITypeRegistrar.</summary>
internal sealed class TypeRegistrar(ServiceCollection services) : ITypeRegistrar
{
    private readonly ServiceCollection _services = services;

    public ITypeResolver Build() => new TypeResolver(_services);

    public void Register(Type service, Type implementation) =>
        _services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) =>
        _services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory) =>
        _services.AddSingleton(service, factory());
}

/// <summary>Resolves types from the service collection.</summary>
internal sealed class TypeResolver(ServiceCollection services) : ITypeResolver, IDisposable
{
    private readonly ServiceCollection _services = services;
    private readonly Dictionary<Type, object> _singletons = [];

    public object? Resolve(Type? type)
    {
        if (type is null) return null;

        // Check cached singletons first
        if (_singletons.TryGetValue(type, out var cached)) return cached;

        // Handle IEnumerable<T> – return an empty list of the right element type
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            var elementType = type.GetGenericArguments()[0];
            var listType = typeof(List<>).MakeGenericType(elementType);
            var empty = Activator.CreateInstance(listType)!;
            _singletons[type] = empty;
            return empty;
        }

        // Look up service descriptor
        foreach (var desc in _services)
        {
            if (desc.ServiceType != type && desc.ImplementationType != type) continue;

            if (desc.Instance is not null)
            {
                _singletons[type] = desc.Instance;
                return desc.Instance;
            }

            if (desc.ImplementationType is not null)
            {
                var instance = CreateInstance(desc.ImplementationType);
                if (instance is not null)
                {
                    _singletons[type] = instance;
                    return instance;
                }
            }
        }

        // Fall back to activating the type directly
        return CreateInstance(type);
    }

    private object? CreateInstance(Type type)
    {
        var ctors = type.GetConstructors();
        foreach (var ctor in ctors.OrderByDescending(c => c.GetParameters().Length))
        {
            var parameters = ctor.GetParameters();
            var args = new object?[parameters.Length];
            bool canResolve = true;
            foreach (var (param, i) in parameters.Select((p, i) => (p, i)))
            {
                var resolved = Resolve(param.ParameterType);
                if (resolved is null && !param.HasDefaultValue)
                {
                    canResolve = false;
                    break;
                }
                args[i] = resolved ?? param.DefaultValue;
            }
            if (canResolve)
                return ctor.Invoke(args);
        }
        return null;
    }

    public void Dispose() { }
}
