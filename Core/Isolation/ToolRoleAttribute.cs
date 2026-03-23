using System;

namespace PayrollEngine.Mcp.Core.Isolation;

/// <summary>Declares the role and minimum permission required to register a tool class.
/// Applied to classes decorated with [McpServerToolType].
/// Tool classes without this attribute are always registered regardless of permissions.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ToolRoleAttribute : Attribute
{
    /// <summary>The functional domain this tool class belongs to.</summary>
    public McpRole Role { get; }

    /// <summary>Minimum permission level required to register this tool class.
    /// Defaults to Read.</summary>
    public McpPermission Required { get; }

    /// <summary>When set, the tool class is only registered at exactly this isolation level.
    /// Null means the role × isolation-level compatibility matrix applies (default behaviour).</summary>
    public IsolationLevel? RequiredIsolationLevel { get; }

    /// <param name="role">The role this tool class belongs to.</param>
    /// <param name="required">Minimum permission to register. Default: Read.</param>
    public ToolRoleAttribute(McpRole role, McpPermission required = McpPermission.Read)
    {
        Role = role;
        Required = required;
        RequiredIsolationLevel = null;
    }

    /// <param name="role">The role this tool class belongs to.</param>
    /// <param name="required">Minimum permission to register.</param>
    /// <param name="requiredIsolationLevel">Exact isolation level required.
    /// Use this for tools that are only meaningful at a specific isolation level
    /// (e.g. consolidation tools require MultiTenant).</param>
    public ToolRoleAttribute(McpRole role, McpPermission required, IsolationLevel requiredIsolationLevel)
    {
        Role = role;
        Required = required;
        RequiredIsolationLevel = requiredIsolationLevel;
    }
}
