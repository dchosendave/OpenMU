// <copyright file="ConfigurationFieldChange.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Operations;

using System.Text.Json.Nodes;

/// <summary>One field of a previewed or applied configuration change.</summary>
/// <param name="Target">The caption of the changed object.</param>
/// <param name="Field">The invariant field name.</param>
/// <param name="Caption">The caption of the field.</param>
/// <param name="Before">The value before the change.</param>
/// <param name="After">The value after the change.</param>
public sealed record ConfigurationFieldChange(string Target, string Field, string Caption, JsonNode? Before, JsonNode? After)
{
    /// <summary>Gets a comparable representation, independent of JSON node identity.</summary>
    internal string Fingerprint => $"{this.Target}\n{this.Field}\n{this.Before?.ToJsonString()}\n{this.After?.ToJsonString()}";
}
