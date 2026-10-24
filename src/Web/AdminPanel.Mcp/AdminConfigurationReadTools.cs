// <copyright file="AdminConfigurationReadTools.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Mcp;

using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ModelContextProtocol.Server;
using MUnique.OpenMU.Web.AdminPanel.Operations;

/// <summary>Read tools of the task oriented configuration tools.</summary>
[McpServerToolType]
public sealed class AdminConfigurationReadTools
{
    private AdminConfigurationReadTools()
    {
    }

    /// <summary>Read the global experience rates and the experience multipliers of the maps.</summary>
    /// <param name="operations">The shared admin operations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rates.</returns>
    [McpServerTool(Name = "rates_get", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Read the global experience and master experience rates, and the experience multiplier of each map.")]
    [Authorize(Roles = "Viewer")]
    [ExposesOperation(typeof(ConfigurationWriteOperations), nameof(ConfigurationWriteOperations.GetRatesAsync))]
    public static ValueTask<JsonObject> RatesGetAsync(
        [FromServices] ConfigurationWriteOperations operations,
        CancellationToken cancellationToken)
        => AdminToolExecution.ExecuteAsync(() => operations.GetRatesAsync(cancellationToken));
}
