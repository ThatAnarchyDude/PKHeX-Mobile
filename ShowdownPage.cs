using PKHeX.Core;
using PKHeX.Core.AutoMod;

namespace PKHeXMobile;

/// <summary>Paste a Showdown set and generate a legal Pokemon for the open save with the Auto-Legality library.</summary>
public class ShowdownPage : ThemedPage
{
    private readonly SaveFile _sav;
    private readonly Func<PKM, Task> _onCreated;
    private readonly Editor _text = new()
    {
        Placeholder = "Paste a Showdown set, for example:\n\nPikachu @ Light Ball\nLevel: 50\nShiny: Yes\n- Thunderbolt\n- Surf",
        AutoSize = EditorAutoSizeOption.Disabled,
        HeightRequest = 260,
    };
    private readonly Label _status = new() { FontSize = 13 };
    private readonly ActivityIndicator _busy = new();
    private readonly Button _generate = new() { Text = "Generate legal Pokemon" };

    public ShowdownPage(SaveFile sav, Func<PKM, Task> onCreated)
    {
        (_sav, _onCreated) = (sav, onCreated);
        Title = "Create from Showdown set";
        _generate.Clicked += async (_, _) => await GenerateAsync();
        Content = new ScrollView
        {
            Content = new VerticalStackLayout { Padding = 12, Spacing = 8, Children = { _text, _generate, _busy, _status } }
        };
    }

    private async Task GenerateAsync()
    {
        if (string.IsNullOrWhiteSpace(_text.Text))
        {
            _status.Text = "Paste a set first.";
            return;
        }
        _generate.IsEnabled = false;
        _busy.IsRunning = true;
        _status.Text = "Generating… this can take a few seconds.";
        try
        {
            var set = new ShowdownSet(_text.Text);
            var result = await Task.Run(() => _sav.GetLegalFromSetAsync(set));
            if (result.Status is not LegalizationResult.Regenerated)
            {
                _status.Text = result.Status switch
                {
                    LegalizationResult.Timeout => "That set took too long to build. Try fewer or simpler requests.",
                    LegalizationResult.Failed => "No legal Pokemon could be built from that set for this save.",
                    var s => $"Generation stopped: {s}.",
                };
                return;
            }

            var pk = result.Created;
            var la = new LegalityAnalysis(pk, _sav.Personal);
            if (!la.Valid && !await DisplayAlertAsync("Not fully legal", $"{la.Report()}\n\nUse it anyway?", "Use it", "Cancel"))
            {
                _status.Text = "Cancelled.";
                return;
            }
            await _onCreated(pk);
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            _status.Text = $"Could not read that set: {ex.Message}";
        }
        finally
        {
            _busy.IsRunning = false;
            _generate.IsEnabled = true;
        }
    }
}
