using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Sanierungsplaner.Desktop.Behaviors;

public static class SelectAllOnFocus
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(SelectAllOnFocus), new PropertyMetadata(false, Changed));
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);
    private static void Changed(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not TextBox box) return;
        if ((bool)args.OldValue) { box.GotKeyboardFocus -= Focused; box.PreviewMouseLeftButtonDown -= Clicked; }
        if ((bool)args.NewValue) { box.GotKeyboardFocus += Focused; box.PreviewMouseLeftButtonDown += Clicked; }
    }
    private static void Focused(object sender, KeyboardFocusChangedEventArgs args) => ((TextBox)sender).SelectAll();
    private static void Clicked(object sender, MouseButtonEventArgs args)
    {
        var box = (TextBox)sender;
        if (box.IsKeyboardFocusWithin) return;
        box.Focus();
        box.SelectAll();
        args.Handled = true;
    }
}
