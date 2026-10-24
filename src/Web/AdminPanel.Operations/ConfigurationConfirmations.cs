// <copyright file="ConfigurationConfirmations.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Operations;

using System.Collections.Concurrent;
using System.Security.Cryptography;

/// <summary>Issues short-lived, single-use tokens which bind the apply call of a change to its preview.</summary>
/// <remarks>
/// A token is bound to the caller, the operation and a hash of the arguments, so the applied change is exactly
/// the previewed one. Tokens only live in memory; a restart of the admin host requires a new preview.
/// </remarks>
/// <param name="time">The time provider.</param>
public sealed class ConfigurationConfirmations(TimeProvider time)
{
    /// <summary>The lifetime of a confirmation token.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const int MaximumPending = 1000;

    private readonly ConcurrentDictionary<string, Pending> _pending = new(StringComparer.Ordinal);

    /// <summary>Issues a token for a previewed change.</summary>
    /// <param name="actor">The caller.</param>
    /// <param name="operation">The operation name.</param>
    /// <param name="argumentsHash">The hash of the change arguments.</param>
    /// <param name="changes">The previewed changes.</param>
    /// <returns>The token and its expiration time.</returns>
    public (string Token, DateTimeOffset ExpiresAt) Issue(AdminActor actor, string operation, string argumentsHash, IReadOnlyList<ConfigurationFieldChange> changes)
    {
        var now = time.GetUtcNow();
        foreach (var expired in this._pending.Where(p => p.Value.ExpiresAt <= now).Select(p => p.Key).ToList())
        {
            this._pending.TryRemove(expired, out _);
        }

        if (this._pending.Count >= MaximumPending)
        {
            throw new AdminOperationException("Too many pending previews. Apply them or let them expire first.", AdminOperationErrorKind.Unavailable);
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var expiresAt = now + Lifetime;
        this._pending[token] = new Pending(actor.Id, operation, argumentsHash, changes, expiresAt);
        return (token, expiresAt);
    }

    /// <summary>Redeems a token. It can't be used again, even when it doesn't match.</summary>
    /// <param name="actor">The caller.</param>
    /// <param name="operation">The operation name.</param>
    /// <param name="argumentsHash">The hash of the change arguments.</param>
    /// <param name="token">The token from the preview.</param>
    /// <returns>The previewed changes.</returns>
    public IReadOnlyList<ConfigurationFieldChange> Redeem(AdminActor actor, string operation, string argumentsHash, string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || !this._pending.TryRemove(token, out var pending) || pending.ExpiresAt <= time.GetUtcNow())
        {
            throw new AdminOperationException("Unknown, used or expired confirmation token. Preview the change again with apply=false.", AdminOperationErrorKind.InvalidInput);
        }

        if (pending.ActorId != actor.Id || pending.Operation != operation || pending.ArgumentsHash != argumentsHash)
        {
            throw new AdminOperationException("The confirmation token belongs to a different caller, tool or arguments. Preview this exact change again.", AdminOperationErrorKind.InvalidInput);
        }

        return pending.Changes;
    }

    private sealed record Pending(string ActorId, string Operation, string ArgumentsHash, IReadOnlyList<ConfigurationFieldChange> Changes, DateTimeOffset ExpiresAt);
}
