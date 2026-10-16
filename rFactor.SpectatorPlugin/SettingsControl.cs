using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace User.RfSpectator
{
    /// <summary>Pantalla de ajustes: solo muestra el estado del lector (se arma en codigo, sin XAML).</summary>
    public class SettingsControl : UserControl
    {
        readonly Func<ShmReader> _get;
        readonly TextBlock _text = new TextBlock { Margin = new Thickness(12), FontSize = 14, TextWrapping = TextWrapping.Wrap };
        readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };

        public SettingsControl(Func<ShmReader> getReader)
        {
            _get = getReader;
            Content = _text;
            _timer.Tick += (s, e) => Refresh();
            Loaded += (s, e) => { Refresh(); _timer.Start(); };
            Unloaded += (s, e) => _timer.Stop();
        }

        void Refresh()
        {
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
                "Lecturas descartadas: " + r.Discarded + "\n\n" +
                "Los ajustes (nombre del mapping, posiciones, intervalo) se editan en el archivo de ajustes y se aplican al reiniciar SimHub.";
        }
    }
}
