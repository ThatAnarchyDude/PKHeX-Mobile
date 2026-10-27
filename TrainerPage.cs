using PKHeX.Core;

namespace PKHeXMobile;

/// <summary>Edits trainer name, IDs, money, gender and play time.</summary>
public class TrainerPage : ThemedPage
{
    private readonly SaveFile _sav;
    private readonly Entry _ot = new(), _tid = new(), _sid = new(), _money = new(), _hours = new(), _minutes = new();
    private readonly Picker _gender = new() { ItemsSource = new[] { "Male", "Female" } };

    public TrainerPage(SaveFile sav)
    {
        _sav = sav;
        Title = "Trainer";
        _ot.Text = sav.OT;
        _tid.Text = sav.TID16.ToString();
        _sid.Text = sav.SID16.ToString();
        _money.Text = sav.Money.ToString();
        _hours.Text = sav.PlayedHours.ToString();
        _minutes.Text = sav.PlayedMinutes.ToString();
        _gender.SelectedIndex = sav.Gender & 1;
        foreach (var e in new[] { _tid, _sid, _money, _hours, _minutes })
            e.Keyboard = Keyboard.Numeric;

        var apply = new Button { Text = "Apply" };
        apply.Clicked += async (_, _) => { Apply(); await Navigation.PopAsync(); };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 12, Spacing = 6,
                Children =
                {
                    new Label { Text = "Trainer name" }, _ot, new Label { Text = "TID" }, _tid, new Label { Text = "SID" }, _sid,
                    new Label { Text = "Money" }, _money, new Label { Text = "Gender" }, _gender,
                    new Label { Text = "Play time (hours / minutes)" }, _hours, _minutes, apply,
                }
            }
        };
    }

    private static uint Num(Entry e, uint max) => uint.TryParse(e.Text, out var v) ? Math.Min(v, max) : 0;

    private void Apply()
    {
        if (!string.IsNullOrWhiteSpace(_ot.Text))
            _sav.OT = _ot.Text;
        _sav.TID16 = (ushort)Num(_tid, ushort.MaxValue);
        _sav.SID16 = (ushort)Num(_sid, ushort.MaxValue);
        _sav.Money = Math.Min(Num(_money, uint.MaxValue), (uint)_sav.MaxMoney);
        _sav.Gender = (byte)Math.Max(_gender.SelectedIndex, 0);
        _sav.PlayedHours = (int)Num(_hours, 999);
        _sav.PlayedMinutes = (int)Num(_minutes, 59);
    }
}
