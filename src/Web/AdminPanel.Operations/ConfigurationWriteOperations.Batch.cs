// <copyright file="ConfigurationWriteOperations.Batch.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Operations;

using System.Text.Json.Nodes;
using System.Threading;
using MUnique.OpenMU.Persistence.AdminAuth;

/// <content>Several configuration patches which are previewed and applied together.</content>
public sealed partial class ConfigurationWriteOperations
{
    /// <summary>The maximum number of objects in one batch.</summary>
    internal const int MaximumBatchObjects = 50;

    /// <summary>The maximum number of fields of all patches in one batch.</summary>
    internal const int MaximumBatchFields = 500;

    /// <summary>Changes scalar fields and single references of several configuration objects at once.</summary>
    /// <param name="changes">The changes: objects with <c>type</c>, <c>id</c> and <c>patch</c>, like config_update takes them.</param>
    /// <param name="reason">The audit reason.</param>
    /// <param name="apply">Whether to apply a previewed change.</param>
    /// <param name="confirmationToken">The token of the preview, when applying.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The preview or the applied changes.</returns>
    [AdminOperation(AdminRole.Administrator, ReadOnly = false)]
    public ValueTask<JsonObject> UpdateBatchAsync(JsonArray changes, string reason, bool apply, string? confirmationToken, CancellationToken cancellationToken)
    {
        if (changes.Count is 0 or > MaximumBatchObjects)
        {
            throw new ArgumentException($"A batch changes 1-{MaximumBatchObjects} objects.", nameof(changes));
        }

        var items = new List<(Type Type, Guid Id, JsonObject Patch)>(changes.Count);
        foreach (var node in changes)
        {
            if (node is not JsonObject change
                || change["type"] is not JsonValue typeValue || !typeValue.TryGetValue<string>(out var type)
                || change["id"] is not JsonValue idValue || !idValue.TryGetValue<string>(out var idText) || !Guid.TryParse(idText, out var id)
                || change["patch"] is not JsonObject patch)
            {
                throw new ArgumentException("Every change needs \"type\", \"id\" and a \"patch\" object, like config_update takes them.", nameof(changes));
            }

            items.Add((ConfigurationModel.ResolveType(type), id, ConfigurationPatch.Canonicalize(patch)));
        }

        if (items.Sum(i => i.Patch.Count) > MaximumBatchFields)
        {
            throw new ArgumentException($"A batch changes at most {MaximumBatchFields} fields.", nameof(changes));
        }

        if (items.GroupBy(i => i.Id).Any(g => g.Count() > 1))
        {
            throw new ArgumentException("Every object may only appear once in a batch; merge its patches.", nameof(changes));
        }

        return this.RunAsync(
            "config_update_batch",
            items.Select(i => new { type = i.Type.Name, id = i.Id, patch = i.Patch }).ToList(),
            reason,
            apply,
            confirmationToken,
            (configuration, _) => items.SelectMany(item =>
            {
                var target = ConfigurationModel.Find(configuration, item.Type, item.Id)
                    ?? throw new ArgumentException($"{item.Type.Name} {item.Id} not found. Use config_query to find its identifier.", nameof(changes));
                return ConfigurationPatch.Plan(target, item.Type, item.Patch, configuration);
            }).ToList(),
            cancellationToken);
    }
}
