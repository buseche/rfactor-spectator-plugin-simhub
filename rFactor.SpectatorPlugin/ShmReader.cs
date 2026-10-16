using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace User.RfSpectator
{
    /// <summary>
    /// Lee "$rFactorShared$" en un hilo propio y publica snapshots inmutables.
    /// Solo lectura: nunca escribe en el mapping.
    /// Nota: mientras este lector tenga el mapping abierto, Windows lo mantiene vivo aunque rFactor se cierre
    /// (queda en ceros). Por eso no hace falta reabrirlo cuando rFactor reinicia: el plugin de rFactor
    /// vuelve a abrir el mismo objeto y lo reescribe.
    /// </summary>
    public sealed class ShmReader : IDisposable
    {
        readonly string _name;
        readonly int _pollMs;
        readonly double _staleSeconds;
        readonly ManualResetEvent _stop = new ManualResetEvent(false);
        readonly byte[] _buf = new byte[RfLayout.MinSize];
        readonly Dictionary<string, float[]> _latch = new Dictionary<string, float[]>();

        Thread _thread;
        MemoryMappedFile _mmf;
        MemoryMappedViewAccessor _acc;

        RfSnapshot _snapshot;            // se accede solo con Volatile
        long _lastChangeTicks;           // idem (Interlocked)
        float _lastEt = float.NaN;
        int _lastSession = int.MinValue;
        int _connected;                  // 0/1
        long _capacity;
        int _discarded;
        string _status = "Iniciando";

        public ShmReader(string mappingName, int pollMs, double staleSeconds)
        {
            _name = string.IsNullOrWhiteSpace(mappingName) ? "$rFactorShared$" : mappingName;
            _pollMs = Math.Max(20, pollMs);
            _staleSeconds = Math.Max(1.0, staleSeconds);
        }

        public string MappingName { get { return _name; } }
        public RfSnapshot Snapshot { get { return Volatile.Read(ref _snapshot); } }
        public bool Connected { get { return Volatile.Read(ref _connected) == 1; } }
        public long Capacity { get { return Interlocked.Read(ref _capacity); } }
        public int Discarded { get { return Volatile.Read(ref _discarded); } }
        public string Status { get { return Volatile.Read(ref _status); } }

        public DateTime LastChangeUtc
        {
            get { long t = Interlocked.Read(ref _lastChangeTicks); return t == 0 ? DateTime.MinValue : new DateTime(t, DateTimeKind.Utc); }
        }

        /// <summary>currentET cambio en los ultimos N segundos (distinto de "conectado": el juego en pausa deja ET quieto).</summary>
        public bool DataFresh
        {
            get
            {
                if (!Connected) return false;
                var t = LastChangeUtc;
                return t != DateTime.MinValue && (DateTime.UtcNow - t).TotalSeconds <= _staleSeconds;
            }
        }

        public void Start()
        {
            if (_thread != null) return;
            _stop.Reset();
            _thread = new Thread(Loop) { IsBackground = true, Name = "RfSpectator.ShmReader" };
            _thread.Start();
        }

        public void Stop()
        {
            var t = _thread;
            _thread = null;
            if (t == null) return;
            _stop.Set();
            t.Join(2000);
            Close();
            Volatile.Write(ref _connected, 0);
            Volatile.Write(ref _snapshot, null);
        }

        public void Dispose() { Stop(); }

        void Loop()
        {
            while (true)
            {
                int wait = _pollMs;
                try
                {
                    if (_acc == null)
                    {
                        if (!TryOpen()) wait = 1000;      // rFactor puede arrancar despues que SimHub
                    }
                    else
                    {
                        ReadOnce();
                    }
                }
                catch (Exception ex)
                {
                    // Nunca dejar escapar una excepcion del hilo: tumbaria SimHub.
                    Volatile.Write(ref _status, "Error: " + ex.GetType().Name + " - " + ex.Message);
                    Close();
                    Volatile.Write(ref _connected, 0);
                    wait = 1000;
                }
                if (_stop.WaitOne(wait)) return;
            }
        }

        bool TryOpen()
        {
            try
            {
                _mmf = MemoryMappedFile.OpenExisting(_name, MemoryMappedFileRights.Read);
            }
            catch (FileNotFoundException)
            {
                Volatile.Write(ref _status, "Esperando el mapping '" + _name + "' (rFactor con el plugin cargado)");
                Volatile.Write(ref _connected, 0);
                return false;
            }
            _acc = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            Interlocked.Exchange(ref _capacity, _acc.Capacity);
            if (_acc.Capacity < RfLayout.MinSize)
            {
                Volatile.Write(ref _status, "Layout incompatible: la vista mide " + _acc.Capacity + " bytes, se esperaban al menos " + RfLayout.MinSize);
                Close();
                Volatile.Write(ref _connected, 0);
                return false;
            }
            Volatile.Write(ref _connected, 1);
            Volatile.Write(ref _status, "Conectado");
            return true;
        }

        void ReadOnce()
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                _acc.ReadArray(0, _buf, 0, _buf.Length);

                // Sesion nueva (ET retrocede o cambia la sesion): se olvidan las brechas congeladas.
                int sess; float et;
                RfParser.PeekSession(_buf, out sess, out et);
                if (sess != _lastSession || (!float.IsNaN(_lastEt) && et < _lastEt - 1f)) _latch.Clear();

                RfSnapshot snap; string err;
                if (RfParser.TryParse(_buf, _latch, out snap, out err))
                {
                    if (float.IsNaN(_lastEt) || snap.ElapsedTime != _lastEt)
                        Interlocked.Exchange(ref _lastChangeTicks, DateTime.UtcNow.Ticks);
                    _lastEt = snap.ElapsedTime;
                    _lastSession = snap.SessionId;
                    Volatile.Write(ref _snapshot, snap);
                    Volatile.Write(ref _status, "Conectado");
                    return;
                }
                Interlocked.Increment(ref _discarded);
                Volatile.Write(ref _status, "Lectura descartada: " + err);
            }
            // Tres intentos invalidos: se conserva el snapshot anterior.
        }

        void Close()
        {
            if (_acc != null) { _acc.Dispose(); _acc = null; }
            if (_mmf != null) { _mmf.Dispose(); _mmf = null; }
        }
    }
}
