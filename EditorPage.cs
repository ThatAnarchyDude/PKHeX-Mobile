using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Full-screen host for the Pokemon editor (used in portrait, and for Day Care Pokemon).</summary>
public class EditPage : ThemedPage
{
    private readonly PokemonEditorView _view;

    /// <summary>Edits a Pokemon stored somewhere other than a party or box slot; <paramref name="writer"/> saves it back.</summary>
    public EditPage(SaveFile sav, PKM pk, Action<PKM> writer)
    {
        Title = "Edit Pokemon";
        Content = _view = new PokemonEditorView(this, sav, pk, -1, 0, writer, Close);
    }

    public EditPage(SaveFile sav, PKM pk, int box, int index)
    {
        Title = "Edit Pokemon";
        Content = _view = new PokemonEditorView(this, sav, pk, box, index, null, Close);
    }

    private void Close() => _ = Navigation.PopAsync();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _view.Reload();
    }
}
