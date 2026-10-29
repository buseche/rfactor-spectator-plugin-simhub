using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace User.RfSpectator
{
    /// <summary>
    /// Settings screen: reader status, carousel row count and class/car colors. Built in code to avoid a XAML
    /// dependency. Every edit mutates the shared settings object and saves immediately; the property delegates
    /// re-read the settings on each call, so changes apply live.
    /// </summary>
    public class SettingsControl : UserControl
    {
        readonly RfSpectatorSettings _settings;
        readonly Action _save;
        readonly Func<ShmReader> _get;
        readonly TextBlock _text = new TextBlock { Margin = new Thickness(0, 0, 0, 12), FontSize = 14, TextWrapping = TextWrapping.Wrap };
        readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        readonly TextBox _rowsBox = new TextBox { Width = 48, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
        readonly ColorListEditor _classEditor;
        readonly ColorListEditor _carEditor;

        public SettingsControl(RfSpectatorSettings settings, Action save, Func<ShmReader> getReader)
        {
            _settings = settings;
            _save = save;
            _get = getReader;

            var root = new StackPanel { Margin = new Thickness(12) };
            root.Children.Add(_text);

            if (_settings != null)
            {
                root.Children.Add(BuildCarouselPanel());
                _classEditor = new ColorListEditor("Colores por clase", "Clase", () => _settings.ClassColors,
                    m => { _settings.ClassColors = m; Persist(); }, () => Distinct(v => v.Class));
                _carEditor = new ColorListEditor("Colores por auto (tienen prioridad sobre la clase)", "Auto", () => _settings.CarColors,
                    m => { _settings.CarColors = m; Persist(); }, () => Distinct(v => v.Car));
                root.Children.Add(_classEditor.Root);
                root.Children.Add(_carEditor.Root);
                root.Children.Add(new TextBlock
                {
                    Margin = new Thickness(0, 12, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                    Text = "Los cambios de filas y colores se guardan y se aplican al instante. " +
                           "El nombre del mapping, las posiciones y el intervalo de lectura se editan en el archivo de ajustes y se aplican al reiniciar SimHub."
                });
            }

            Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = root };
            _timer.Tick += (s, e) => Refresh();
            Loaded += (s, e) => { Refresh(); _timer.Start(); };
            Unloaded += (s, e) => _timer.Stop();
        }

        void Persist()
        {
            try { _save(); }
            catch (Exception ex) { SimHub.Logging.Current.Error("RfSpectator: no se pudieron guardar los ajustes: " + ex.Message); }
        }

        UIElement BuildCarouselPanel()
        {
            int rows = CarouselMath.ClampRows(_settings.CarouselRows, RfSpectatorSettings.CarouselMaxRows);
            _rowsBox.Text = rows.ToString();
            _rowsBox.LostFocus += (s, e) => CommitRows(ParseRows());
            _rowsBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) CommitRows(ParseRows()); };

            var minus = new Button { Content = "-", Width = 28, Margin = new Thickness(0, 0, 4, 0) };
            var plus = new Button { Content = "+", Width = 28, Margin = new Thickness(4, 0, 8, 0) };
            minus.Click += (s, e) => CommitRows(ParseRows() - 1);
            plus.Click += (s, e) => CommitRows(ParseRows() + 1);

            var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            line.Children.Add(minus);
            line.Children.Add(_rowsBox);
            line.Children.Add(plus);
            line.Children.Add(new TextBlock { Text = "(1 a " + RfSpectatorSettings.CarouselMaxRows + ")", VerticalAlignment = VerticalAlignment.Center });

            var box = new GroupBox { Header = "Filas visibles del carrusel", Margin = new Thickness(0, 0, 0, 12) };
            box.Content = line;
            return box;
        }

        int ParseRows()
        {
            int v;
            if (!int.TryParse(_rowsBox.Text.Trim(), out v)) v = _settings.CarouselRows;
            return v;
        }

        void CommitRows(int value)
        {
            int rows = CarouselMath.ClampRows(value, RfSpectatorSettings.CarouselMaxRows);
            _rowsBox.Text = rows.ToString();
            if (_settings.CarouselRows == rows) return;
            _settings.CarouselRows = rows;
            Persist();
        }

        /// <summary>Distinct non-empty values (class or car) among the cars in the live snapshot, sorted.</summary>
        IList<string> Distinct(Func<RfVehicle, string> pick)
        {
            var r = _get();
            var snap = r == null ? null : r.Snapshot;
            if (snap == null) return new List<string>();
            return snap.ByPlace.Where(v => v != null).Select(pick)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Polled by a timer because the reader runs on another thread and raises no events.</summary>
        void Refresh()
        {
            if (_classEditor != null) _classEditor.RefreshSuggestions();
            if (_carEditor != null) _carEditor.RefreshSuggestions();

            var r = _get();
            if (r == null) { _text.Text = "Plugin detenido."; return; }
            var snap = r.Snapshot;
            var last = r.LastChangeUtc;
            _text.Text =
                "Mapping: " + r.MappingName + "\n" +
                "Estado: " + r.Status + "\n" +
                "Conectado: " + (r.Connected ? "si" : "no") + "   Datos frescos: " + (r.DataFresh ? "si" : "no") + "\n" +
                "Tamano de la vista: " + r.Capacity + " bytes\n" +
                "Autos: " + (snap == null ? "-" : snap.NumVehicles.ToString()) + "\n" +
                "Tiempo de sesion: " + (snap == null ? "-" : snap.ElapsedTime.ToString("0.0")) + " s\n" +
                "Ultimo cambio: " + (last == DateTime.MinValue ? "-" : last.ToLocalTime().ToString("HH:mm:ss")) + "\n" +
                "Lecturas descartadas: " + r.Discarded;
        }

        /// <summary>Editable name -> "#RRGGBB" list with a color preview and suggestions taken from the live snapshot.</summary>
        sealed class ColorListEditor
        {
            readonly Func<Dictionary<string, string>> _read;
            readonly Action<Dictionary<string, string>> _write;
            readonly Func<IList<string>> _suggest;
            readonly StackPanel _rows = new StackPanel();
            readonly ComboBox _name = new ComboBox { IsEditable = true, MinWidth = 200, Margin = new Thickness(0, 0, 8, 0) };
            readonly TextBox _hex = new TextBox { Width = 80, Text = "#FFFFFF", Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
            readonly Border _swatch = Swatch();
            IList<string> _shown = new List<string>();

            public readonly GroupBox Root;

            public ColorListEditor(string title, string nameLabel, Func<Dictionary<string, string>> read,
                Action<Dictionary<string, string>> write, Func<IList<string>> suggest)
            {
                _read = read;
                _write = write;
                _suggest = suggest;

                var add = new Button { Content = "Agregar / actualizar", Padding = new Thickness(8, 2, 8, 2) };
                add.Click += (s, e) => AddFromInputs();
                _hex.TextChanged += (s, e) => Paint(_swatch, _hex);

                var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
                line.Children.Add(new TextBlock { Text = nameLabel + ":", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
                line.Children.Add(_name);
                line.Children.Add(_hex);
                line.Children.Add(_swatch);
                line.Children.Add(add);

                var body = new StackPanel { Margin = new Thickness(4) };
                body.Children.Add(_rows);
                body.Children.Add(line);
                body.Children.Add(new TextBlock { Text = "Formato #RRGGBB. Elige de la lista (autos en pista) o escribe el nombre.", Margin = new Thickness(0, 4, 0, 0), Opacity = 0.7 });

                Root = new GroupBox { Header = title, Margin = new Thickness(0, 0, 0, 12), Content = body };
                Paint(_swatch, _hex);
                Rebuild();
            }

            static Border Swatch()
            {
                return new Border { Width = 22, Height = 22, BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            }

            /// <summary>Shows the color in the swatch; an invalid hex clears it and flags the text box.</summary>
            static void Paint(Border swatch, TextBox box)
            {
                string hex = ColorResolver.Normalize(box.Text);
                if (hex == null) { swatch.Background = Brushes.Transparent; box.Foreground = Brushes.Red; return; }
                box.ClearValue(Control.ForegroundProperty);
                swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            }

            void AddFromInputs()
            {
                string name = _name.Text == null ? "" : _name.Text.Trim();
                var current = _read();
                var updated = ColorResolver.With(current, name, _hex.Text);
                // With returns the same instance for an empty name or an invalid color: nothing to save.
                if (object.ReferenceEquals(updated, current)) return;
                _write(updated);
                Rebuild();
            }

            void Remove(string key)
            {
                _write(ColorResolver.Without(_read(), key));
                Rebuild();
            }

            void Rebuild()
            {
                _rows.Children.Clear();
                var map = _read();
                foreach (var kv in map.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    _rows.Children.Add(BuildRow(kv.Key, kv.Value));
            }

            UIElement BuildRow(string key, string value)
            {
                string k = key;   // copy for the closures
                var swatch = Swatch();
                var hex = new TextBox { Width = 80, Text = value, Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
                hex.TextChanged += (s, e) => Paint(swatch, hex);
                Action commit = () =>
                {
                    var current = _read();
                    var updated = ColorResolver.With(current, k, hex.Text);
                    if (object.ReferenceEquals(updated, current)) return;   // invalid text: keep the stored color
                    _write(updated);
                };
                hex.LostFocus += (s, e) => commit();
                hex.KeyDown += (s, e) => { if (e.Key == Key.Enter) commit(); };
                Paint(swatch, hex);

                var remove = new Button { Content = "Quitar", Padding = new Thickness(8, 0, 8, 0) };
                remove.Click += (s, e) => Remove(k);

                var line = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                line.Children.Add(new TextBlock { Text = key, Width = 200, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                line.Children.Add(hex);
                line.Children.Add(swatch);
                line.Children.Add(remove);
                return line;
            }

            /// <summary>Refills the picker only when the live set changed, so typing in the editable box is not disturbed.</summary>
            public void RefreshSuggestions()
            {
                var now = _suggest();
                if (now.SequenceEqual(_shown)) return;
                string typed = _name.Text;
                _shown = now;
                _name.ItemsSource = now;
                _name.Text = typed;
            }
        }
    }
}
