using Microsoft.Maui.Handlers;

namespace PKHeXMobile;

public static class MauiProgram
{
    private const int NoExtractUi = (int)Android.Views.InputMethods.ImeFlags.NoExtractUi;

    public static MauiApp CreateMauiApp()
    {
        // In landscape Android's keyboard can take over the whole screen ("extract" mode) and hide the field being edited.
        // MAUI re-applies ImeOptions whenever the return key type is mapped, so add the flag after that mapping and again on focus.
        EntryHandler.Mapper.ModifyMapping(nameof(Microsoft.Maui.IEntry.ReturnType), (handler, view, previous) =>
        {
            previous?.Invoke(handler, view);
            handler.PlatformView.ImeOptions |= (Android.Views.InputMethods.ImeAction)NoExtractUi;
        });
        EditorHandler.Mapper.AppendToMapping("NoExtractUi", (handler, _) =>
            handler.PlatformView.ImeOptions |= (Android.Views.InputMethods.ImeAction)NoExtractUi);
        EntryHandler.Mapper.AppendToMapping("NoExtractUi", (handler, _) =>
        {
            handler.PlatformView.ImeOptions |= (Android.Views.InputMethods.ImeAction)NoExtractUi;
            handler.PlatformView.FocusChange += (s, e) =>
            {
                if (e.HasFocus && s is Android.Widget.EditText edit)
                    edit.ImeOptions |= (Android.Views.InputMethods.ImeAction)NoExtractUi;
            };
        });

        EntryHandler.Mapper.AppendToMapping("LongPress", (handler, view) =>
        {
            if (view is not LongPressEntry entry) return;
            handler.PlatformView.LongClickable = true;
            handler.PlatformView.LongClick += (_, e) =>
            {
                e.Handled = true; // swallow the default text-selection menu
                entry.RaiseLongPressed();
            };
        });

        return MauiApp.CreateBuilder().UseMauiApp<App>().Build();
    }
}