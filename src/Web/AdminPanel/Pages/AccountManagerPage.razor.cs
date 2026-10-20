// <copyright file="AccountManagerPage.razor.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Pages;

using Microsoft.AspNetCore.Components;

/// <summary>
/// The page of the account manager, which edits accounts, characters, inventories and vaults.
/// </summary>
public partial class AccountManagerPage
{
    /// <summary>
    /// Gets or sets the login name of the account which is opened.
    /// </summary>
    [Parameter]
    public string? LoginName { get; set; }

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    /// <inheritdoc />
    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync().ConfigureAwait(true);
        await this.State.InitializeAsync().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(this.LoginName))
        {
            await this.State.OpenAccountAsync(this.LoginName).ConfigureAwait(true);
        }
    }

    private async Task OnAccountPickedAsync(string loginName)
    {
        await this.State.OpenAccountAsync(loginName).ConfigureAwait(true);
        if (this.State.Session is { } session && string.Equals(session.Account.LoginName, loginName, StringComparison.OrdinalIgnoreCase))
        {
            this.NavigationManager.NavigateTo($"account-manager/{Uri.EscapeDataString(session.Account.LoginName)}");
        }
    }
}
