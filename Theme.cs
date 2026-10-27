namespace PKHeXMobile;

/// <summary>Colours modelled on the PC PKHeX window (Windows light) with a matching dark variant.</summary>
public static class Theme
{
    public static readonly Color PageLight = Color.FromArgb("#F0F0F0"), PageDark = Color.FromArgb("#1E1E1E");
    public static readonly Color TextLight = Colors.Black, TextDark = Colors.White;
    public static readonly Color ButtonLight = Color.FromArgb("#E1E1E1"), ButtonDark = Color.FromArgb("#3A3A3A");
    public static readonly Color SlotLight = Colors.White, SlotDark = Color.FromArgb("#2B2B2B");
    public static readonly Color BorderLight = Color.FromArgb("#ADADAD"), BorderDark = Color.FromArgb("#5A5A5A");
    public static readonly Color AccentLight = Color.FromArgb("#0078D7"), AccentDark = Color.FromArgb("#3399FF");
    public static readonly Color BarLight = Color.FromArgb("#FFFFFF"), BarDark = Color.FromArgb("#2D2D30");

    private static BindingBase Pair(Color light, Color dark) =>
        ((IMarkupExtension<BindingBase>)new AppThemeBindingExtension { Light = light, Dark = dark }).ProvideValue(null!);

    /// <summary>Implicit styles so every page follows the PC-style palette without per-control setup.</summary>
    public static void Apply(ResourceDictionary resources)
    {
        resources.Add(new Style(typeof(ContentPage))
        {
            ApplyToDerivedTypes = true,
            Setters = { new Setter { Property = VisualElement.BackgroundColorProperty, Value = Pair(PageLight, PageDark) } }
        });
        resources.Add(new Style(typeof(Label))
        {
            Setters = { new Setter { Property = Label.TextColorProperty, Value = Pair(TextLight, TextDark) } }
        });
        resources.Add(new Style(typeof(Entry))
        {
            Setters = { new Setter { Property = InputView.TextColorProperty, Value = Pair(TextLight, TextDark) } }
        });
        resources.Add(new Style(typeof(Picker))
        {
            Setters = { new Setter { Property = Picker.TextColorProperty, Value = Pair(TextLight, TextDark) } }
        });
        resources.Add(new Style(typeof(SearchBar))
        {
            Setters = { new Setter { Property = InputView.TextColorProperty, Value = Pair(TextLight, TextDark) } }
        });
        resources.Add(new Style(typeof(Button))
        {
            Setters =
            {
                new Setter { Property = VisualElement.BackgroundColorProperty, Value = Pair(ButtonLight, ButtonDark) },
                new Setter { Property = Button.TextColorProperty, Value = Pair(TextLight, TextDark) },
                new Setter { Property = Button.BorderColorProperty, Value = Pair(BorderLight, BorderDark) },
                new Setter { Property = Button.BorderWidthProperty, Value = 1d },
                new Setter { Property = Button.CornerRadiusProperty, Value = 2 },
            }
        });
    }

    /// <summary>Applies the palette to a page and the controls already on it (buttons, text, inputs).</summary>
    public static void StylePage(ContentPage page)
    {
        page.SetAppThemeColor(VisualElement.BackgroundColorProperty, PageLight, PageDark);
        if (page.Content is { } root)
            StyleTree(root);
    }

    /// <summary>Applies the palette to a view and everything inside it (used for content created after the page appeared).</summary>
    public static void Style(Element element) => StyleTree(element);

    private static void StyleTree(Element element)
    {
        switch (element)
        {
            case Button b:
                b.SetAppThemeColor(VisualElement.BackgroundColorProperty, ButtonLight, ButtonDark);
                b.SetAppThemeColor(Button.TextColorProperty, TextLight, TextDark);
                b.SetAppThemeColor(Button.BorderColorProperty, BorderLight, BorderDark);
                b.BorderWidth = 1;
                b.CornerRadius = 2;
                break;
            case Label l:
                l.SetAppThemeColor(Label.TextColorProperty, TextLight, TextDark);
                break;
            case Entry e:
                e.SetAppThemeColor(InputView.TextColorProperty, TextLight, TextDark);
                break;
            case Picker p:
                p.SetAppThemeColor(Picker.TextColorProperty, TextLight, TextDark);
                break;
        }

        switch (element)
        {
            case Layout layout:
                foreach (var child in layout.Children.OfType<Element>())
                    StyleTree(child);
                break;
            case ScrollView { Content: { } sv }:
                StyleTree(sv);
                break;
            case ContentView { Content: { } cv }:
                StyleTree(cv);
                break;
            case Border { Content: { } bc }:
                StyleTree(bc);
                break;
        }
    }

    public static void StyleBar(NavigationPage nav)
    {
        nav.SetAppThemeColor(NavigationPage.BarBackgroundColorProperty, BarLight, BarDark);
        nav.SetAppThemeColor(NavigationPage.BarTextColorProperty, TextLight, TextDark);
    }

    public static Border Slot(View content, bool selected = false)
    {
        var b = new Border
        {
            Padding = 2,
            StrokeThickness = selected ? 2 : 1,
            Content = content,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 2 },
        };
        b.SetAppThemeColor(VisualElement.BackgroundColorProperty, SlotLight, SlotDark);
        b.SetAppTheme<Brush>(Border.StrokeProperty,
            new SolidColorBrush(selected ? AccentLight : BorderLight),
            new SolidColorBrush(selected ? AccentDark : BorderDark));
        return b;
    }
}
