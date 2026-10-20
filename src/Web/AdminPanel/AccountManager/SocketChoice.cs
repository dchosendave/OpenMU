// <copyright file="SocketChoice.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.Web.Shared.Components.ItemEdit;

/// <summary>
/// One socket of a socket item.
/// </summary>
public sealed class SocketChoice
{
    private readonly SocketViewModel _socket;
    private readonly Action _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SocketChoice"/> class.
    /// </summary>
    /// <param name="socket">The socket.</param>
    /// <param name="possibleOptions">The options which the socket can have.</param>
    /// <param name="changed">The action which is called after every change.</param>
    public SocketChoice(SocketViewModel socket, IReadOnlyList<SocketOptionViewModel> possibleOptions, Action changed)
    {
        this._socket = socket;
        this.Choices = possibleOptions;
        this._changed = changed;
    }

    /// <summary>
    /// Gets the caption.
    /// </summary>
    public string Caption => this._socket.Caption;

    /// <summary>
    /// Gets the options which the socket can have.
    /// </summary>
    public IReadOnlyList<SocketOptionViewModel> Choices { get; }

    /// <summary>
    /// Gets or sets the option (seed sphere) of the socket.
    /// </summary>
    public SocketOptionViewModel? Option
    {
        get => this._socket.Option;
        set
        {
            this._socket.Option = value;
            this._changed();
        }
    }

    /// <summary>
    /// Gets or sets the level of the seed sphere (0 to 4).
    /// </summary>
    public int Level
    {
        get => this._socket.Level;
        set
        {
            this._socket.Level = Math.Clamp(value, 0, 4);
            this._changed();
        }
    }
}
