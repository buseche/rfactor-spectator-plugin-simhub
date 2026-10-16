namespace User.RfSpectator
{
    /// <summary>Ajustes guardados por SimHub (JSON). Se leen en Init; cambiarlos requiere reiniciar SimHub.</summary>
    public class RfSpectatorSettings
    {
        /// <summary>Nombre del mapping. Para pruebas con ShmFake usar otro (p. ej. "$rFactorShared$Test").</summary>
        public string MappingName = "$rFactorShared$";

        /// <summary>Cantidad de posiciones (PosNN) que se registran como propiedades. Maximo 128.</summary>
        public int MaxPositions = 40;

        /// <summary>Intervalo de lectura en milisegundos.</summary>
        public int PollMs = 100;

        /// <summary>Segundos sin cambio de currentET para marcar DataFresh = false.</summary>
        public double StaleSeconds = 5;
    }
}
