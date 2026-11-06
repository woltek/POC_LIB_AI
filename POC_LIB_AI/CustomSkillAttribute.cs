using System.Reflection;
using Microsoft.Agents.AI;

namespace POC_LIB_AI;

/// <summary>
/// Marks a class as a custom skill that can be discovered automatically by <see cref="CustomSkillDiscovery"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CustomSkillAttribute : Attribute
{
    public CustomSkillAttribute(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Description = description;
    }

    public string Description { get; }
}

/// <summary>
/// Discovers skill classes annotated with <see cref="CustomSkillAttribute"/> in one or more assemblies.
/// </summary>
public static class CustomSkillDiscovery
{
    public static IReadOnlyList<AgentSkill> DiscoverSkillsFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return DiscoverSkillsFromAssemblies([assembly]);
    }

    public static IReadOnlyList<AgentSkill> DiscoverSkillsFromAssemblies(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var discovered = new List<AgentSkill>();

        foreach (Assembly assembly in assemblies.Distinct())
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types.OfType<Type>().ToArray();
            }

            foreach (Type type in types)
            {
                if (type is null || type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
                {
                    continue;
                }

                if (type.GetCustomAttribute<CustomSkillAttribute>() is null)
                {
                    continue;
                }

                if (!typeof(AgentSkill).IsAssignableFrom(type))
                {
                    throw new InvalidOperationException(
                        $"Type '{type.FullName}' is marked with [CustomSkill] but does not derive from AgentSkill.");
                }

                AgentSkill? instance = Activator.CreateInstance(type, nonPublic: true) as AgentSkill;
                if (instance is null)
                {
                    throw new InvalidOperationException(
                        $"Type '{type.FullName}' is marked with [CustomSkill] but could not be instantiated.");
                }

                discovered.Add(instance);
            }
        }

        return discovered;
    }
}
