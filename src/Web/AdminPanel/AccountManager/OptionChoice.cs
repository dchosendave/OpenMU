// <copyright file="OptionChoice.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Web.AdminPanel.AccountManager;

using MUnique.OpenMU.DataModel.Configuration.Items;

/// <summary>
/// An option which can be switched on or off, e.g. an excellent or wing option.
/// </summary>
public sealed class OptionChoice
{
    private readonly IList<IncreasableItemOption> _options;
    private readonly Action _changed;

    /// <summary>
    /// Initializes a new instance of the <see cref="OptionChoice"/> class.
    /// </summary>
    /// <param name="option">The option.</param>
    /// <param name="options">The options of the item, which contain the option when it's switched on.</param>
    /// <param name="changed">The action which is called after every change.</param>
    public OptionChoice(IncreasableItemOption option, IList<IncreasableItemOption> options, Action changed)
    {
        this.Option = option;
        this._options = options;
        this._changed = changed;
    }

    /// <summary>
    /// Gets the option.
    /// </summary>
    public IncreasableItemOption Option { get; }

    /// <summary>
    /// Gets the caption.
    /// </summary>
    public string Caption => this.Option.ToString() ?? string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the option is switched on.
    /// </summary>
    public bool IsChecked
    {
        get => this._options.Contains(this.Option);
        set
        {
            if (value == this.IsChecked)
            {
                return;
            }

            if (value)
            {
                this._options.Add(this.Option);
            }
            else
            {
                this._options.Remove(this.Option);
            }

            this._changed();
        }
    }
}
