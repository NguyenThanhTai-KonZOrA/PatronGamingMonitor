using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PatronGamingMonitor.Helpers
{
    public static class TextBlockHelper
    {
        public static readonly DependencyProperty RemoveUnderlineProperty =
            DependencyProperty.RegisterAttached(
                "RemoveUnderline",
                typeof(bool),
                typeof(TextBlockHelper),
                new PropertyMetadata(false, OnRemoveUnderlineChanged));

        public static bool GetRemoveUnderline(DependencyObject obj)
        {
            return (bool)obj.GetValue(RemoveUnderlineProperty);
        }

        public static void SetRemoveUnderline(DependencyObject obj, bool value)
        {
            obj.SetValue(RemoveUnderlineProperty, value);
        }

        private static void OnRemoveUnderlineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBlock textBlock && (bool)e.NewValue)
            {
                textBlock.Loaded += (s, args) =>
                {
                    textBlock.TextDecorations = null;
                };
                textBlock.TextDecorations = null;
            }
        }
    }
}