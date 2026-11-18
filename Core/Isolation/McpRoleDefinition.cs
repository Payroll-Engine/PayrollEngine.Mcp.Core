using System;
using System.Collections.Generic;
using System.Linq;

namespace PayrollEngine.Mcp.Core.Isolation;

/// <summary>A role defined by a server in addition to the built-in roles of <see cref="McpRole"/>.
/// Tool classes reference it by name (<see cref="ToolRoleAttribute(string, McpPermission)"/>);
/// its permission is read from McpServer:Permissions:&lt;Name&gt; and defaults to None.</summary>
public sealed class McpRoleDefinition
{
    /// <summary>The role name, also the configuration key below McpServer:Permissions</summary>
    public string Name { get; }

    /// <summary>The isolation levels at which the role is applicable</summary>
    public IReadOnlyCollection<IsolationLevel> IsolationLevels { get; }

    /// <summary>True when the tools of the role need a backend connection</summary>
    public bool RequiresBackend { get; }

    /// <param name="name">Role name: a letter followed by letters or digits, not a built-in role name</param>
    /// <param name="isolationLevels">The isolation levels at which the role is applicable, at least one</param>
    /// <param name="requiresBackend">True when the tools of the role need a backend connection</param>
    /// <exception cref="ArgumentException">Invalid name or no isolation level</exception>
    public McpRoleDefinition(string name, IEnumerable<IsolationLevel> isolationLevels, bool requiresBackend = true)
    {
        if (!IsValidName(name))
        {
            throw new ArgumentException(
                $"Invalid role name '{name}': use a letter followed by letters or digits.", nameof(name));
        }
        if (IsBuiltInName(name))
        {
            throw new ArgumentException(
                $"The role name '{name}' is a built-in role.", nameof(name));
        }

        var levels = isolationLevels?.Distinct().ToList() ?? [];
        if (levels.Count == 0)
        {
            throw new ArgumentException(
                $"The role '{name}' needs at least one isolation level.", nameof(isolationLevels));
        }

        Name = name;
        IsolationLevels = levels;
        RequiresBackend = requiresBackend;
    }

    /// <summary>True when the role is applicable at the given isolation level</summary>
    /// <param name="level">The active isolation level</param>
    public bool IsApplicable(IsolationLevel level) =>
        IsolationLevels.Contains(level);

    /// <summary>True when the name is a built-in role of <see cref="McpRole"/> (ignoring case)</summary>
    /// <param name="name">The role name</param>
    public static bool IsBuiltInName(string name) =>
        TryGetBuiltIn(name, out _);

    /// <summary>Returns the built-in role with the given name (ignoring case)</summary>
    /// <param name="name">The role name</param>
    /// <param name="role">The built-in role</param>
    /// <returns>True when the name is a built-in role</returns>
    internal static bool TryGetBuiltIn(string name, out McpRole role)
    {
        // compare against the names: Enum.TryParse also accepts numbers and comma lists
        foreach (var builtIn in Enum.GetValues<McpRole>())
        {
            if (string.Equals(builtIn.ToString(), name?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                role = builtIn;
                return true;
            }
        }
        role = default;
        return false;
    }

    private static bool IsValidName(string name) =>
        !string.IsNullOrEmpty(name) &&
        char.IsAsciiLetter(name[0]) &&
        name.All(char.IsAsciiLetterOrDigit);
}
