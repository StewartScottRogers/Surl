namespace Surl.Extended.Protocol.OciRegistry;

/// <summary>
/// Reads and writes Token scopes, <c>repository:&lt;name&gt;:&lt;action&gt;[,&lt;action&gt;…]</c>
/// (Token "Requesting a Token"; ADR-0115, decisions 4.1 and 4.3).
/// </summary>
internal static class OciTokenScope
{
    private const string RepositoryType = "repository";

    /// <summary>
    /// Reads one scope. Actions other than <c>pull</c>, <c>push</c>, <c>delete</c> and <c>*</c> are
    /// dropped, as Token lets a server grant less than asked.
    /// </summary>
    /// <param name="scope">The scope as the client sent it.</param>
    /// <returns>
    /// The repository and its actions, or <see langword="null"/> for a scope of another resource
    /// type, a name off the distribution specification's grammar, or no known action.
    /// </returns>
    public static (string Name, OciActions Actions)? Parse(string scope)
    {
        var parts = scope.Split(':');
        if (parts.Length != 3 || parts[0] != RepositoryType || !OciNames.IsRepositoryName(parts[1]))
        {
            return null;
        }

        var actions = parts[2].Split(',').Aggregate(OciActions.None, (all, action) => all | ActionNamed(action));

        return actions == OciActions.None ? null : (parts[1], actions);
    }

    /// <summary>
    /// The scope a challenge names for a request that needs <paramref name="required"/> in
    /// <paramref name="name"/>: <c>pull</c> for a read, <c>pull,push</c> for a write and
    /// <c>delete</c> for a delete (decision 4.1).
    /// </summary>
    /// <param name="name">The repository.</param>
    /// <param name="required">
    /// What the request needs: <see cref="OciActions.Pull"/>, <see cref="OciActions.Push"/> or
    /// <see cref="OciActions.Delete"/>.
    /// </param>
    /// <returns>The scope.</returns>
    public static string Format(string name, OciActions required) => required switch
    {
        OciActions.Pull => $"{RepositoryType}:{name}:pull",
        OciActions.Push => $"{RepositoryType}:{name}:pull,push",
        _ => $"{RepositoryType}:{name}:delete",
    };

    private static OciActions ActionNamed(string action) => action switch
    {
        "pull" => OciActions.Pull,
        "push" => OciActions.Push,
        "delete" => OciActions.Delete,
        "*" => OciActions.Pull | OciActions.Push | OciActions.Delete,
        _ => OciActions.None,
    };
}
