using System.Reflection;
using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Lists every editable property of the Pokemon (met data, OT, contest stats, markings, PID, flags, ...) and edits it in place.</summary>
public class AdvancedPage : ThemedPage
{
    private readonly PropertyEditor _editor;
    private readonly List<PropertyInfo> _props;
    private readonly SearchBar _search = new() { Placeholder = "Filter fields" };
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.Single };

    private sealed record Row(PropertyInfo Prop, string Text);

    public AdvancedPage(SaveFile sav, PKM pk)
    {
        _editor = new PropertyEditor(this, pk, sav);
        Title = "All fields";
        _props = pk.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0
                        && !p.Name.StartsWith(RibbonInfo.PropertyPrefix, StringComparison.Ordinal)
                        && p.Name is not ("Data" or "Raw" or "BallDPPt" or "BallHGSS") // use Ball: it keeps both stored copies in step
                        && IsSimple(p.PropertyType))
            .OrderBy(p => p.Name)
            .ToList();

        _search.TextChanged += (_, _) => Refresh();
        _list.ItemTemplate = new DataTemplate(() =>
        {
            var l = new Label { Padding = new Thickness(8, 10) };
            l.SetBinding(Label.TextProperty, nameof(Row.Text));
            return l;
        });
        _list.SelectionChanged += async (_, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() is not Row row) return;
            _list.SelectedItem = null;
            await _editor.EditAsync(row.Prop);
            Refresh();
        };

        Content = new Grid
        {
            Padding = 12,
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) },
            Children = { _search, At(_list, 1) }
        };
        Refresh();
    }

    private static T At<T>(T v, int row) where T : View { Grid.SetRow(v, row); return v; }

    private static bool IsSimple(Type t) =>
        t == typeof(DateOnly?) || t == typeof(string) || t == typeof(bool) || t.IsEnum || (t.IsPrimitive && t != typeof(char) && t != typeof(IntPtr) && t != typeof(UIntPtr));

    private void Refresh()
    {
        _editor.ClearCache();
        var q = _search.Text?.Trim() ?? string.Empty;
        _list.ItemsSource = _props
            .Where(p => q.Length == 0 || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Select(p => new Row(p, $"{p.Name}: {_editor.Read(p)}"))
            .ToList();
    }
}
