using System;

namespace User.RfSpectator
{
    /// <summary>
    /// Position-carousel logic, kept free of SimHub dependencies so it can be unit tested.
    /// The main block shows places 1..(start-1); the carousel walks start..numVehicles
    /// in pages of "rows" rows, switching page every "secondsPerPage" seconds.
    /// </summary>
    internal static class CarouselMath
    {
        /// <summary>Bounds a user-configured row count to 1..max so a bad setting can never break paging or registration.</summary>
        public static int ClampRows(int rows, int max)
        {
            if (rows < 1) return 1;
            return rows > max ? max : rows;
        }

        /// <summary>Number of pages; 0 when no car falls in the carousel range.</summary>
        public static int Pages(int numVehicles, int start, int rows)
        {
            if (rows < 1) return 0;
            int count = numVehicles - start + 1;
            if (count <= 0) return 0;
            return (count + rows - 1) / rows;
        }

        /// <summary>Current page (0-based) from a clock in seconds. A single page never rotates. Driven by the wall clock so every dashboard stays in sync.</summary>
        public static int Page(int pages, double nowSeconds, double secondsPerPage)
        {
            if (pages <= 1 || secondsPerPage <= 0) return 0;
            long tick = (long)Math.Floor(nowSeconds / secondsPerPage);
            return (int)(((tick % pages) + pages) % pages);
        }

        /// <summary>Real place shown in row "rowIndex" (1-based) of the page; 0 when the row is empty.</summary>
        public static int PlaceAt(int numVehicles, int start, int rows, int page, int rowIndex)
        {
            if (rowIndex < 1 || rowIndex > rows || page < 0) return 0;
            int place = start + page * rows + (rowIndex - 1);
            return (place >= start && place <= numVehicles) ? place : 0;
        }
    }
}
