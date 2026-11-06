using System.Reflection;
using Microsoft.Agents.AI;

namespace POC_LIB_AI;

/// <summary>
/// Marks a class as a custom skill for discovery by <see cref="CustomSkillDiscovery"/>.
/// The description should match the skill's <see cref="AgentSkill.Frontmatter"/> description.
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
/// Finds and creates explicitly marked skill classes in assemblies selected by the application.
/// </summary>
public static class CustomSkillDiscovery
{
    /// <summary>Discovers marked skills in one explicitly selected assembly.</summary>
    public static IReadOnlyList<AgentSkill> DiscoverSkillsFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return DiscoverSkillsFromAssemblies([assembly]);
    }

    /// <summary>Discovers marked skills in the supplied assemblies.</summary>
    public static IReadOnlyList<AgentSkill> DiscoverSkillsFromAssemblies(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        var result = new List<AgentSkill>();

        foreach (Assembly assembly in assemblies.Distinct())
        {
            ArgumentNullException.ThrowIfNull(assembly);
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
                CustomSkillAttribute? marker = type.GetCustomAttribute<CustomSkillAttribute>();
                if (marker is null || type.IsAbstract || type.IsInterface || type.ContainsGenericParameters)
                {
                    continue;
                }

                if (!typeof(AgentSkill).IsAssignableFrom(type))
                {
                    throw new InvalidOperationException(
                        $"Type '{type.FullName}' is marked with [CustomSkill] but does not derive from AgentSkill.");
                }

                if (Activator.CreateInstance(type, nonPublic: true) is not AgentSkill skill)
                {
                    throw new InvalidOperationException(
                        $"Type '{type.FullName}' is marked with [CustomSkill] but could not be instantiated. " +
                        "Custom skills need a parameterless constructor.");
                }

                if (!string.Equals(marker.Description, skill.Frontmatter.Description, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"The [CustomSkill] description on '{type.FullName}' must match its AgentSkillFrontmatter description.");
                }

                result.Add(skill);
            }
        }

        return result;
    }
}
