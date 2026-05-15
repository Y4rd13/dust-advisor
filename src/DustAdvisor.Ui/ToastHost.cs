using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace DustAdvisor.Ui
{
    public sealed class ToastHost
    {
        private readonly StackPanel _container;

        public ToastHost(StackPanel container)
        {
            _container = container;
        }

        public void Show(string message, TimeSpan? linger = null, Action onClick = null)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x2D, 0x2D, 0x2D)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xC5, 0xA5, 0x72)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(12, 7, 12, 7),
                Margin = new Thickness(0, 4, 0, 0),
                Cursor = onClick != null ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
            };
            var text = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE4, 0xD9)),
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI"),
            };
            border.Child = text;
            if (onClick != null)
                border.MouseLeftButtonUp += (s, e) => { onClick(); _container.Children.Remove(border); };

            _container.Children.Add(border);

            var timer = new DispatcherTimer { Interval = linger ?? TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                if (_container.Children.Contains(border))
                    _container.Children.Remove(border);
            };
            timer.Start();
        }
    }
}
