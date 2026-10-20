// <copyright file="AccountManagerComponentBase.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.Components.AccountManager;

using Microsoft.AspNetCore.Components;
using MUnique.OpenMU.Web.AdminPanel.AccountManager;

/// <summary>
/// The base of the components of the account manager, which render again whenever the <see cref="AccountManagerState"/> changes.
/// </summary>
public abstract class AccountManagerComponentBase : ComponentBase, IDisposable
{
    /// <summary>
    /// Gets or sets the state of the account manager.
    /// </summary>
    [Inject]
    protected AccountManagerState State { get; set; } = null!;

    /// <inheritdoc />
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();
        this.State.Changed += this.OnStateChanged;
    }

    /// <summary>
    /// Releases the resources of the component.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.State.Changed -= this.OnStateChanged;
        }
    }

    /// <summary>
    /// Applies a change which the user made, and lets the other components render it.
    /// </summary>
    /// <param name="change">The change.</param>
    protected void Set(Action change)
    {
        change();
        this.State.RaiseChanged();
    }

    private void OnStateChanged() => _ = this.InvokeAsync(this.StateHasChanged);
}
