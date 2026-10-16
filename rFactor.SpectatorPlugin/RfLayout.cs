// Layout de la memoria compartida "$rFactorShared$" tal como la escribe el
// plugin que corre dentro de rFactor. Sale de ingenieria inversa (ver plan.md):
// NO coincide con rfSharedStruct.hpp del repo de dallongo.
// pack 1, little endian, textos en Windows-1252 terminados en byte 0.
namespace User.RfSpectator
{
    internal static class RfLayout
    {
        public const int MaxVehicles = 128;
        public const int VehicleBase = 735;
        public const int VehicleStride = 304;
        // 735 + 128 * 304 = 39647 (D: derivado, la vista real mide 40960 por redondeo a paginas)
        public const int MinSize = VehicleBase + MaxVehicles * VehicleStride;

        // ---- Cabecera de sesion (offsets absolutos) ----
        public const int OffTrackName = 76;      // char[64]  V (vacio en espectador)
        public const int LenTrackName = 64;
        public const int OffSession = 587;       // int32     V
        public const int OffCurrentET = 591;     // float     V
        public const int OffEndET = 595;         // float     V (-2147483648 = sin limite)
        public const int OffMaxLaps = 599;       // int32     D
        public const int OffNumVehicles = 607;   // int32     V
        public const int OffGamePhase = 611;     // uint8     V
        public const int OffYellow = 612;        // int8      V
        public const int OffInRealtime = 618;    // uint8     V
        public const int OffPlayerName = 619;    // char[32]  V
        public const int LenPlayerName = 32;
        public const int OffAmbientTemp = 715;   // float     D
        public const int OffTrackTemp = 719;     // float     D

        // ---- Auto (offsets relativos al inicio del auto) ----
        public const int VDriver = 0;            // char[32]  V
        public const int VDriverLen = 32;
        public const int VCar = 32;              // char[64]  V
        public const int VCarLen = 64;
        public const int VTotalLaps = 96;        // int16     V
        public const int VSector = 98;           // int8      V (0=S3, 1=S1, 2=S2)
        public const int VFinishStatus = 99;     // int8      V (0 corriendo, 1 termino)
        public const int VLapDist = 100;         // float     V
        public const int VBestS1 = 112;          // float     V
        public const int VBestLap = 120;         // float     V
        public const int VLastS1 = 124;          // float     V
        public const int VLastLap = 132;         // float     V
        public const int VCurS1 = 136;           // float     V (-1 = sin dato)
        public const int VCurS2 = 140;           // float     V (acumulado S1+S2)
        public const int VNumPitstops = 144;     // int16     ? (siempre 0 en la prueba)
        public const int VNumPenalties = 146;    // int16     ? (siempre 0 en la prueba)
        public const int VIsPlayer = 148;        // uint8     V
        public const int VInPits = 150;          // uint8     V
        public const int VPlace = 151;           // uint8     V (1-based)
        public const int VClass = 152;           // char[32]  V
        public const int VClassLen = 32;
        public const int VGapNext = 184;         // float     V (segundos)
        public const int VLapsBehindNext = 188;  // int32     ? (siempre 0)
        public const int VGapLeader = 192;       // float     V (segundos)
        public const int VLapsBehindLeader = 196;// int32     ? (siempre 0)
        public const int VPosX = 204;            // float     V
        public const int VPosZ = 212;            // float     V
        public const int VSpeed = 300;           // float     V (m/s)
        // El auto completo mide 304 bytes (verificado con el volcado crudo RAW0).
    }
}
