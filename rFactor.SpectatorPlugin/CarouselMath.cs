using System;

namespace User.RfSpectator
{
    /// <summary>
    /// Logica del carrusel de posiciones (sin dependencias de SimHub, para poder probarla).
    /// El bloque principal muestra las posiciones 1..(start-1); el carrusel recorre start..numVehicles
    /// en paginas de "rows" filas, cambiando de pagina cada "secondsPerPage" segundos.
    /// </summary>
    internal static class CarouselMath
    {
        /// <summary>Cantidad de paginas. 0 si no hay autos en el rango del carrusel.</summary>
        public static int Pages(int numVehicles, int start, int rows)
        {
            if (rows < 1) return 0;
            int count = numVehicles - start + 1;
            if (count <= 0) return 0;
            return (count + rows - 1) / rows;
        }

        /// <summary>Pagina actual (base 0) a partir de un reloj en segundos. Con una sola pagina no rota.</summary>
        public static int Page(int pages, double nowSeconds, double secondsPerPage)
        {
            if (pages <= 1 || secondsPerPage <= 0) return 0;
            long tick = (long)Math.Floor(nowSeconds / secondsPerPage);
            return (int)(((tick % pages) + pages) % pages);
        }

        /// <summary>Posicion real que va en la fila "rowIndex" (base 1) de la pagina; 0 si la fila queda vacia.</summary>
        public static int PlaceAt(int numVehicles, int start, int rows, int page, int rowIndex)
        {
            if (rowIndex < 1 || rowIndex > rows || page < 0) return 0;
            int place = start + page * rows + (rowIndex - 1);
            return (place >= start && place <= numVehicles) ? place : 0;
        }
    }
}
