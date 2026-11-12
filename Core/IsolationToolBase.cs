using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PayrollEngine.Mcp.Core.Isolation;

namespace PayrollEngine.Mcp.Core;

/// <summary>Base class for all MCP tool classes, independent of the backend.
/// Provides the isolation context, permission checks, workspace path resolution,
/// uniform error handling and integrity signing.
/// Tool classes without backend access (e.g. file based tools) derive directly from this class;
/// backend tool classes derive from <see cref="ToolBase"/>.</summary>
public abstract class IsolationToolBase(IsolationContext isolation)
{
    /// <summary>The active data isolation context. Every tool call is filtered through this context.</summary>
    protected IsolationContext Isolation { get; } = isolation;

    /// <summary>The workspace context with the permitted root folders for file based tools.</summary>
    protected WorkspaceContext Workspace => Isolation.Workspace;

    #region Permissions

    /// <summary>Returns true if the deployment grants at least the required permission for the role.</summary>
    /// <param name="role">The tool role</param>
    /// <param name="required">The minimum required permission</param>
    protected bool IsGranted(McpRole role, McpPermission required) =>
        Isolation.Permissions.IsGranted(role, required);

    #endregion

    #region Workspace

    /// <summary>Resolves a path argument below the configured workspace roots.
    /// Throws <see cref="UnauthorizedAccessException"/> when the path leaves the workspace.</summary>
    /// <param name="path">Relative or absolute path provided by the AI agent</param>
    /// <returns>The resolved workspace path</returns>
    protected WorkspacePath ResolveWorkspacePath(string path) =>
        Workspace.ResolvePath(path);

    #endregion

    #region Error handling

    /// <summary>Serializes a caught exception as a structured JSON error result.
    /// All public tool methods must catch exceptions and delegate to this method
    /// so the MCP client always receives valid JSON, never a raw exception message.</summary>
    /// <param name="exception">The caught exception</param>
    /// <returns>JSON: { "error": "...", "type": "..." }</returns>
    protected static string Error(Exception exception)
    {
        var message = exception.GetBaseException().Message;
        var type = exception.GetBaseException().GetType().Name;
        return JsonSerializer.Serialize(new { error = message, type });
    }

    #endregion

    #region Integrity signing

    /// <summary>Wraps a JSON result in an HMAC-SHA256 signed envelope when IntegrityKey is configured.
    /// Returns the original JSON unchanged when no key is set (backward compatible).
    /// The recipient verifies the signature by recomputing HMAC-SHA256 over the data JSON
    /// with the same shared secret.</summary>
    /// <param name="dataJson">Serialized JSON payload to sign</param>
    /// <returns>Original JSON (no key) or signed envelope JSON</returns>
    protected string SignWithIntegrity(string dataJson)
    {
        if (string.IsNullOrWhiteSpace(Isolation.IntegrityKey))
        {
            return dataJson;
        }

        var keyBytes = Encoding.UTF8.GetBytes(Isolation.IntegrityKey);
        var dataBytes = Encoding.UTF8.GetBytes(dataJson);
        var hash = HMACSHA256.HashData(keyBytes, dataBytes);
        var signature = Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var envelope = new
        {
            data = JsonSerializer.Deserialize<JsonElement>(dataJson),
            integrity = new
            {
                algorithm = "HMAC-SHA256",
                signature,
                timestamp = DateTime.UtcNow.ToString("o")
            }
        };
        return JsonSerializer.Serialize(envelope);
    }

    #endregion
}
