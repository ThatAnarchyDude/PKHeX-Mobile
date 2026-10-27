using System.Reflection;
using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Reads and edits Pokemon properties by name, using named lists, dates and full years where the PC editor does.</summary>
public sealed class PropertyEditor(Page host, PKM pk, SaveFile sav)
{
    private readonly Dictionary<string, IReadOnlyList<ComboItem>?> _choices = [];

    public PKM Pokemon => pk;

    public PropertyInfo? Prop(string name) => pk.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);

    /// <summary>The game stores these as years since 2000; show and accept the full year.</summary>
    public static bool IsYear(PropertyInfo p) => p.PropertyType == typeof(byte) && p.Name.EndsWith("Year", StringComparison.Ordinal);

    public IReadOnlyList<ComboItem>? Choices(PropertyInfo p)
    {
        if (!_choices.TryGetValue(p.Name, out var list))
        {
            try { list = FieldChoices.For(p, pk, sav); }
            catch (Exception) { list = null; }
            _choices[p.Name] = list;
        }
        return list;
    }

    public void ClearCache() => _choices.Clear();

    public string Read(PropertyInfo p)
    {
        try
        {
            var v = p.GetValue(pk);
            if (v is DateOnly d)
                return d.ToString("yyyy-MM-dd");
            if (IsYear(p))
                return $"{2000 + Convert.ToInt32(v)}";
            if (v is bool or string or null)
                return $"{v}";
            if (Choices(p) is { } list)
                return ChoicePage.Name(list, (int)Convert.ToInt64(v));
            return $"{v}";
        }
        catch (Exception)
        {
            return "?";
        }
    }

    /// <summary>Edits one property through the right control (list, date, year, flip or prompt). Returns true if it changed.</summary>
    public async Task<bool> EditAsync(PropertyInfo p)
    {
        var type = p.PropertyType;
        try
        {
            if (type == typeof(bool))
            {
                p.SetValue(pk, !(bool)p.GetValue(pk)!);
                return true;
            }
            if (Choices(p) is { } list)
            {
                var current = (int)Convert.ToInt64(p.GetValue(pk));
                var page = new ChoicePage(p.Name, list, current);
                await host.Navigation.PushAsync(page);
                if (await page.Result is not { } picked) return false;
                p.SetValue(pk, type.IsEnum ? Enum.ToObject(type, picked) : Convert.ChangeType(picked, type));
                return true;
            }
            if (type == typeof(DateOnly?))
            {
                var current = (p.GetValue(pk) as DateOnly?)?.ToString("yyyy-MM-dd") ?? string.Empty;
                var text = await host.DisplayPromptAsync(p.Name, "Date as YYYY-MM-DD (leave blank to clear)", initialValue: current);
                if (text is null) return false;
                if (string.IsNullOrWhiteSpace(text))
                    p.SetValue(pk, null);
                else if (DateOnly.TryParse(text, out var date) && date.Year is >= 2000 and <= 2099)
                    p.SetValue(pk, date);
                else
                {
                    await host.DisplayAlertAsync(p.Name, "Enter a date between 2000-01-01 and 2099-12-31, like 2026-10-24.", "OK");
                    return false;
                }
                return true;
            }
            if (IsYear(p))
            {
                var text = await host.DisplayPromptAsync(p.Name, "Full year, 2000 to 2255 (stored as years since 2000)", keyboard: Keyboard.Numeric,
                    initialValue: (2000 + Convert.ToInt32(p.GetValue(pk))).ToString());
                if (text is null) return false;
                if (int.TryParse(text, out var year) && year is >= 2000 and <= 2255)
                {
                    p.SetValue(pk, (byte)(year - 2000));
                    return true;
                }
                await host.DisplayAlertAsync(p.Name, "Enter a year from 2000 to 2255.", "OK");
                return false;
            }
            return await PromptAsync(p);
        }
        catch (Exception ex)
        {
            await host.DisplayAlertAsync(p.Name, ex.InnerException?.Message ?? ex.Message, "OK");
            return false;
        }
    }

    private async Task<bool> PromptAsync(PropertyInfo p)
    {
        var type = p.PropertyType;
        var text = await host.DisplayPromptAsync(p.Name, type.Name, initialValue: Convert.ToString(p.GetValue(pk)) ?? string.Empty);
        if (text is null) return false;
        p.SetValue(pk, type == typeof(string) ? text : Convert.ChangeType(text, type));
        return true;
    }

    /// <summary>Sets a numeric or text property from typed text (used by inline entries). Returns false when the text is not valid.</summary>
    public bool TrySetText(PropertyInfo p, string? text)
    {
        try
        {
            var type = p.PropertyType;
            p.SetValue(pk, type == typeof(string) ? text ?? string.Empty : Convert.ChangeType(text, type));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>True when the property is edited with an inline text box rather than a list, date or switch.</summary>
    public bool IsInlineText(PropertyInfo p) =>
        p.PropertyType != typeof(bool) && p.PropertyType != typeof(DateOnly?) && !IsYear(p) && Choices(p) is null;
}
