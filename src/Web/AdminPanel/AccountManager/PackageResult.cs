// <copyright file="PackageResult.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// What applying an item package did.
/// </summary>
/// <param name="Added">The number of added items.</param>
/// <param name="SkippedForClass">The number of items which were skipped, because they're for other classes.</param>
/// <param name="Zen">The added zen.</param>
/// <param name="Problems">The problems, e.g. items or options which the server doesn't know.</param>
public sealed record PackageResult(int Added, int SkippedForClass, int Zen, IReadOnlyList<string> Problems);
