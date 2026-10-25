using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using ResourceManagementSystem.Models.Exceptions;

namespace ResourceManagementSystem.Models.Validation;

public sealed record ValidationError(string PropertyName, string Message);

/// <summary>
/// Validates any object against its <see cref="RuleAttribute"/>s.
/// Reflection is expensive (GetProperties + GetCustomAttributes allocate on every call),
/// so the metadata for each type is discovered once and cached in a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Property getters are compiled to
/// delegates, so after the first call validating a command costs about as much as
/// hand-written <c>if</c> statements.
/// </summary>
public static class ReflectionValidator
{
    private static readonly ConcurrentDictionary<Type, PropertyRules[]> Cache = new();

    /// <summary>Number of types whose metadata has been cached (exposed for tests).</summary>
    public static int CachedTypeCount => Cache.Count;

    public static IReadOnlyList<ValidationError> Validate(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        var errors = new List<ValidationError>();
        foreach (var property in Cache.GetOrAdd(instance.GetType(), BuildRules))
        {
            var value = property.Getter(instance);
            foreach (var rule in property.Rules)
            {
                if (rule.Validate(property.Name, value) is { } message)
                {
                    errors.Add(new ValidationError(property.Name, message));
                }
            }
        }

        return errors;
    }

    /// <summary>Throws <see cref="CommandValidationException"/> when any rule fails.</summary>
    public static void EnsureValid(object instance)
    {
        var errors = Validate(instance);
        if (errors.Count > 0)
        {
            throw new CommandValidationException(errors);
        }
    }

    private static PropertyRules[] BuildRules(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => (Property: p, Rules: p.GetCustomAttributes<RuleAttribute>(inherit: true).ToArray()))
            .Where(x => x.Rules.Length > 0 && x.Property.GetMethod is not null)
            .Select(x => new PropertyRules(x.Property.Name, CompileGetter(type, x.Property), x.Rules))
            .ToArray();

    private static Func<object, object?> CompileGetter(Type type, PropertyInfo property)
    {
        // (object o) => (object)((T)o).Property
        var parameter = Expression.Parameter(typeof(object), "o");
        var body = Expression.Convert(Expression.Property(Expression.Convert(parameter, type), property), typeof(object));
        return Expression.Lambda<Func<object, object?>>(body, parameter).Compile();
    }

    private sealed record PropertyRules(string Name, Func<object, object?> Getter, RuleAttribute[] Rules);
}
