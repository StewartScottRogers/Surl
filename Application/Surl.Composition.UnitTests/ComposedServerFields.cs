using System.Reflection;

namespace Surl.Composition;

/// <summary>
/// Reads what a composed protocol server was built over: its one private field of a given type,
/// so a test can show two servers were handed the same instance without either exposing it.
/// </summary>
internal static class ComposedServerFields
{
    /// <summary>The value of <paramref name="server"/>'s one instance field of type <typeparamref name="T"/>.</summary>
    public static T Single<T>(object server)
        where T : class =>
        (T)server.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(field => field.FieldType == typeof(T))
            .GetValue(server)!;
}
