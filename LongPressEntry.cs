namespace PKHeXMobile;

/// <summary>An entry box that raises <see cref="LongPressed"/> on a long press (instead of opening Android's text-selection menu).</summary>
public class LongPressEntry : Entry
{
    public event EventHandler? LongPressed;

    internal void RaiseLongPressed() => LongPressed?.Invoke(this, EventArgs.Empty);
}
