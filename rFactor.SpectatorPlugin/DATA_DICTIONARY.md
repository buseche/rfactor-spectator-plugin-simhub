# Diccionario de datos de RfSpectator

Referencia completa de los datos que maneja el plugin, desde los bytes de la memoria compartida hasta las
propiedades que ve SimHub. Se construyo leyendo el codigo (`RfLayout.cs`, `RfSnapshot.cs`, `PitTracker.cs`,
`ShmReader.cs`, `RfSpectatorPlugin.cs`, `RfSpectatorPlugin.Carousel.cs`, `RfSpectatorSettings.cs`,
`CarouselMath.cs`, `NameParts.cs`). Si el codigo y este documento difieren, manda el codigo.

Para el uso en dashboards (ejemplos, estado de verificacion en pruebas reales) ver
[RfSpectator-propiedades-SimHub.md](RfSpectator-propiedades-SimHub.md).

Cadena de datos:

```
rFactor 1 + plugin de memoria compartida  ->  "$rFactorShared$" (bloque de bytes)
  -> ShmReader (hilo de fondo, cada PollMs)  ->  RfParser.TryParse  ->  RfSnapshot (inmutable)
  -> PitTracker.Apply (rellena PitTime, aun antes de publicar)  ->  Volatile.Write(snapshot)
  -> delegados AttachDelegate de SimHub (leen siempre el ultimo snapshot)
```

Leyenda de confianza: **V** = verificado contra un volcado real; **D** = derivado (deducido, sin
confirmacion independiente); **?** = sin confirmar (en las pruebas siempre dio 0 o no hay evidencia).

---

## 1. Memoria compartida (`RfLayout.cs`)

Formato: estructura empaquetada a 1 byte (pack 1), little endian, textos en Windows-1252 terminados en byte 0.
Proviene de ingenieria inversa; **no** coincide con `rfSharedStruct.hpp` de dallongo. La centralizacion en
`RfLayout` hace que un cambio de formato solo toque ese archivo.

### 1.1 Constantes generales

| Constante | Valor | Descripcion |
|---|---|---|
| Nombre del mapping | `$rFactorShared$` | Configurable (`MappingName`). Se abre en solo lectura. |
| `MaxVehicles` | 128 | Maximo de autos. |
| `VehicleBase` | 735 | Offset absoluto del primer auto. |
| `VehicleStride` | 304 | Bytes por auto (verificado con el volcado RAW0). |
| `MinSize` | 39647 | `735 + 128 * 304` (D). La vista real mide 40960 por redondeo de pagina. Si la vista es menor, el estado es `Layout incompatible ...`. |

Offset absoluto de un campo de auto `i` (0-based, orden de memoria, no de posicion): `735 + i * 304 + offset_relativo`.

### 1.2 Cabecera de sesion (offsets absolutos)

| Campo | Offset | Tipo | Tam. | Unidad | Conf. | Notas |
|---|---|---|---|---|---|---|
| TrackName | 76 | char[64] | 64 | texto cp1252 | V | Vacio en modo espectador. |
| Session | 587 | int32 | 4 | id | V | Numero de sesion en crudo. |
| CurrentET | 591 | float | 4 | s | V | Tiempo de sesion. Debe ser finito y >= 0 o se descarta la lectura. |
| EndET | 595 | float | 4 | s | V | `-2147483648` = sin limite (centinela). |
| MaxLaps | 599 | int32 | 4 | vueltas | D | Sin confirmar. |
| NumVehicles | 607 | int32 | 4 | autos | V | Debe estar en 0..128. |
| GamePhase | 611 | uint8 | 1 | codigo | V | Ver seccion 3.1. |
| Yellow | 612 | int8 | 1 | codigo | V | Ver seccion 3.2. |
| InRealtime | 618 | uint8 | 1 | bool | V | `!= 0` es verdadero. Se decodifica pero no se publica como propiedad. |
| PlayerName | 619 | char[32] | 32 | texto cp1252 | V | |
| AmbientTemp | 715 | float | 4 | grados C | D | |
| TrackTemp | 719 | float | 4 | grados C | D | |

Los demas bytes de la cabecera (todo lo que no figura arriba) no se leen; su contenido es desconocido.

### 1.3 Auto (offsets relativos al inicio del auto)

| Campo | Rel. | Tipo | Tam. | Unidad | Conf. | Notas |
|---|---|---|---|---|---|---|
| Driver | 0 | char[32] | 32 | texto | V | |
| Car | 32 | char[64] | 64 | texto | V | Nombre del coche segun el mod; suele llevar `#numero`. |
| TotalLaps | 96 | int16 | 2 | vueltas | V | Con signo. Vueltas completadas. |
| Sector | 98 | int8 | 1 | codigo | V | 0 = S3, 1 = S1, 2 = S2 (el juego numera desde el cruce de linea previo). |
| FinishStatus | 99 | int8 | 1 | codigo | V | Verificado solo 0 (corriendo) y 1 (terminado); 2 y 3 vienen del codigo, sin evidencia. |
| LapDist | 100 | float | 4 | m | V | Tras la meta se vuelve negativo. |
| BestS1 | 112 | float | 4 | s | V | Definido en `RfLayout`, no se lee. |
| BestLap | 120 | float | 4 | s | V | |
| LastS1 | 124 | float | 4 | s | V | Definido, no se lee. |
| LastLap | 132 | float | 4 | s | V | |
| CurS1 | 136 | float | 4 | s | V | `-1` = sin dato. |
| CurS2 | 140 | float | 4 | s | V | **Acumulado** S1+S2, no el sector 2 aislado. |
| NumPitstops | 144 | int16 | 2 | paradas | ? | Siempre 0 en pruebas. |
| NumPenalties | 146 | int16 | 2 | penalizaciones | ? | Siempre 0 en pruebas. |
| IsPlayer | 148 | uint8 | 1 | bool | V | |
| InPits | 150 | uint8 | 1 | bool | V | |
| Place | 151 | uint8 | 1 | posicion (base 1) | V | Debe ser 1..NumVehicles y sin repetirse. |
| Class | 152 | char[32] | 32 | texto | V | |
| GapNext | 184 | float | 4 | s | V | Brecha al auto de delante. |
| LapsBehindNext | 188 | int32 | 4 | vueltas | ? | Siempre 0; definido, no se lee. |
| GapLeader | 192 | float | 4 | s | V | Brecha al lider. |
| LapsBehindLeader | 196 | int32 | 4 | vueltas | ? | Siempre 0 en pruebas. |
| PosX | 204 | float | 4 | m | V | |
| PosZ | 212 | float | 4 | m | V | |
| Speed | 300 | float | 4 | m/s | V | |

El auto completo mide 304 bytes. No existe un campo de id de auto: la identidad se arma con `Driver|Car`.

### 1.4 Validacion de una lectura (`RfParser.TryParse`)

El escritor no tiene seqlock, asi que son posibles lecturas partidas. Una lectura se descarta (con motivo en `Status`) si:

- el bloque es mas corto que `MinSize` (`block too short`);
- `NumVehicles` fuera de 0..128;
- `CurrentET` no es finito o es negativo;
- algun `Place` esta fuera de 1..n o repetido;
- alguno de estos floats no es finito: LapDist, BestLap, LastLap, CurS1, CurS2, GapLeader, GapNext, Speed, PosX, PosZ.

`ShmReader.ReadOnce` reintenta hasta 3 veces por sondeo; si las tres fallan conserva el snapshot anterior y suma 1 a `Discarded` por cada intento fallido.

---

## 2. RfSnapshot y RfVehicle (`RfSnapshot.cs`)

### 2.1 RfSnapshot (una lectura valida, inmutable una vez publicada)

| Campo | Tipo | Origen | Transformacion / valor sin dato |
|---|---|---|---|
| SessionId | int | Session (587) | Directo. |
| ElapsedTime | float | CurrentET (591) | Directo (s). |
| EndTime | float | EndET (595) | `NaN` si no es finito, `<= 0` o `>= 1e9` (centinela de "sin limite"). |
| MaxLaps | int | MaxLaps (599) | `0` si no esta en 1..9999. |
| Phase | int | GamePhase (611) | Byte sin signo directo. |
| Yellow | int | Yellow (612) | Byte con signo (`sbyte`). |
| InRealtime | bool | InRealtime (618) | `!= 0`. |
| PlayerName | string | PlayerName (619) | cp1252, hasta el primer 0; ver 2.3. |
| Track | string | TrackName (76) | Idem; vacio en espectador. |
| TrackTemp / AmbientTemp | float | 719 / 715 | `NaN` si no es finito o fuera de (-100, 200) grados C. |
| NumVehicles | int | NumVehicles (607) | Directo. |
| ByPlace | RfVehicle[] | autos | Indice 0 = P1; longitud = NumVehicles. |
| ReadAtUtc | DateTime | reloj | Hora UTC de la lectura (no se publica como propiedad). |
| FastestLapPlace | int | calculado | Posicion del poseedor de la mejor vuelta; `0` = nadie. Ver 6.2. |

Metodos: `AtPlace(place)` devuelve el auto de esa posicion o `null` si esta fuera de 1..ByPlace.Length;
`FastestLapCar` es `AtPlace(FastestLapPlace)`.

### 2.2 RfVehicle

| Campo | Tipo | Origen | Transformacion / valor sin dato |
|---|---|---|---|
| Driver | string | Driver | Texto saneado (2.3). |
| Car | string | Car | Idem. |
| Class | string | Class | Idem. |
| Place | int | Place | Directo. |
| Laps | int | TotalLaps | int16 directo. |
| Sector | int | Sector | Mapeo humano: raw 0 -> 3, 1 -> 1, 2 -> 2; cualquier otro -> 0. |
| FinishStatus | int | FinishStatus | `sbyte` directo (0..3 esperado). |
| LapDist | float | LapDist | Directo (negativo tras la meta). |
| BestLap | float | BestLap | `-1` si el valor es `<= 0`. |
| LastLap | float | LastLap | `-1` si el valor es `<= 0`. |
| CurS1 | float | CurS1 | `-1` si el valor es `<= 0`. |
| CurS2 | float | CurS2 - CurS1 | S2 = acumulado - S1; `-1` si `CurS1 <= 0` o `acumulado <= CurS1`. |
| GapLeader | float | GapLeader | Segundos; congelado tras la meta (ver abajo). |
| GapAhead | float | GapNext | Segundos; congelado tras la meta. |
| LapsBehindLeader | int | LapsBehindLeader | Directo (?). |
| InPits | bool | InPits | `!= 0`. |
| PitTime | float | PitTracker | Segundos en boxes desde la entrada observada; `0` fuera de boxes. Ver 6.1. |
| IsPlayer | bool | IsPlayer | `!= 0`. |
| Pitstops | int | NumPitstops | int16 directo (?). |
| Penalties | int | NumPenalties | int16 directo (?). |
| SpeedKmh | float | Speed | `m/s * 3.6`. |
| PosX / PosZ | float | PosX / PosZ | Directo (m). |
| Key | string (calculado) | Driver + "\|" + Car | Identidad estable por sesion; clave del latch y del PitTracker. |

**Latch de brechas tras la meta.** Despues de la bandera, `LapDist` y las brechas del juego se vuelven
negativas. En el primer snapshot donde `FinishStatus != 0` el plugin guarda `max(0, gapLeader)` y
`max(0, gapNext)` por `Key` y los reutiliza en todos los snapshots siguientes. El latch se vacia al detectar
sesion nueva (ver 2.4). Las brechas de autos que aun corren se publican tal cual las da el juego.

### 2.3 Texto

- Los nombres se decodifican como Windows-1252 hasta el primer byte 0 (o el largo maximo del campo).
- Los caracteres de control se reemplazan por espacio y el resultado se recorta (`Trim`).

### 2.4 Deteccion de sesion nueva (`ShmReader`)

Antes de parsear se leen solo `Session` y `CurrentET`. Se vacian el latch y el PitTracker si cambia el id de
sesion o si `CurrentET` retrocede mas de 1 s respecto del ultimo valor aceptado.

### 2.5 Estado del lector (`ShmReader`, no pertenece al snapshot)

| Miembro | Tipo | Descripcion |
|---|---|---|
| Connected | bool | La vista se abrio y mide al menos `MinSize`. |
| DataFresh | bool | Conectado y `CurrentET` cambio hace menos de `StaleSeconds`. Con el juego en pausa pasa a `false` sin estar desconectado. |
| Status | string | `Iniciando`, `Conectado`, `Esperando el mapping '<nombre>' (rFactor con el plugin cargado)`, `Layout incompatible: la vista mide N bytes, se esperaban al menos M`, `Lectura descartada: <motivo>`, `Error: <Excepcion> - <mensaje>`. |
| Capacity | long | Tamano de la vista en bytes. |
| Discarded | int | Lecturas invalidas acumuladas. |
| LastChangeUtc | DateTime | Ultimo cambio de `CurrentET` (`MinValue` si nunca). |

Comportamiento: sondeo cada `PollMs` (minimo 20 ms); sin conexion o tras un error reintenta cada 1000 ms;
`StaleSeconds` tiene minimo 1. Un nombre de mapping vacio se reemplaza por `$rFactorShared$`. El lector no
reabre el mapping cuando rFactor se reinicia: mientras lo tenga abierto Windows lo mantiene vivo (se lee como
ceros) y el plugin de rFactor reabre el mismo objeto.

---

## 3. Tablas de codigos y textos

Los textos son los que devuelven las propiedades `*Text` / `Status`; estan en espanol (sin acentos) y son fijos.

### 3.1 Phase (`Session.Phase` -> `Session.PhaseText`)

| Valor | Texto | Significado |
|---|---|---|
| 0 | Antes de empezar | Antes de empezar la sesion |
| 1 | Vueltas de reconocimiento | Reconocimiento (solo carrera) |
| 2 | Parrilla | Parrilla (solo carrera) |
| 3 | Vuelta de formacion | Formacion (solo carrera) |
| 4 | Cuenta atras | Cuenta atras de las luces |
| 5 | Verde | Bandera verde |
| 6 | FCY | Amarilla total / safety car |
| 7 | Sesion detenida | Sesion detenida |
| 8 | Sesion terminada | Sesion terminada |
| otro | Desconocida | Valor fuera de tabla |

Valores observados en pruebas reales: 4, 5, 6 y 8.

### 3.2 Yellow (`Session.YellowState` -> `Session.YellowText`)

| Valor | Texto |
|---|---|
| 0 | Ninguna |
| 1 | Pendiente |
| 2 | Boxes cerrados |
| 3 | Boxes: vuelta lider |
| 4 | Boxes abiertos |
| 5 | Ultima vuelta |
| 6 | Reanudar |
| 7 | Carrera detenida |
| otro (incl. negativos) | (texto vacio) |

Observados: 0, 1, 2, 4, 5 y 6. Tras la meta se vio alternar 0 y 6.

### 3.3 FinishStatus (`PosNN.Status` / `CarouselNN.Status`)

| Valor | Texto |
|---|---|
| 0 | Corriendo |
| 1 | Terminado |
| 2 | DNF |
| 3 | DQ |
| otro | (texto vacio) |

Sin posicion en el listado (fila vacia) `Status` es texto vacio.

### 3.4 Sector (`PosNN.Sector`)

Crudo 0 = sector 3, 1 = sector 1, 2 = sector 2. Publicado: 1, 2 o 3 (0 si el crudo es otro).

### 3.5 Formatos de texto (`RfFormat`)

| Formato | Regla |
|---|---|
| `LapTime(t)` | `m:ss.fff` (minutos sin relleno, segundos con 2 enteros y 3 decimales, cultura invariante). Texto vacio si `t <= 0` o no es numero. Se usa tambien para `PitTimeText`. |
| `Gap(v, s)` | Vacio si `v.Place <= 1`; `+nL` si `v.LapsBehindLeader > 0` (aplica a ambos textos de brecha); si no `+s.fff` (3 decimales). |

---

## 4. Propiedades de SimHub

Prefijo comun: `RfSpectatorPlugin.` (ej. `RfSpectatorPlugin.Session.Phase`). Se registran en `Init`
(se puede ejecutar varias veces: SimHub reconstruye plugins al cambiar de juego). Los delegados son
funciones puras del ultimo snapshot. Sin snapshot (antes de la primera lectura valida o tras `Stop`) valen
lo indicado en la columna "Sin dato": texto `""`, numerico `0`, booleano `false`.

### 4.1 Conexion

| Propiedad | Tipo | Unidad | Descripcion | Sin dato |
|---|---|---|---|---|
| `Connected` | bool | | Mapping abierto con tamano valido. | `false` |
| `DataFresh` | bool | | `CurrentET` cambio en los ultimos `StaleSeconds`. | `false` |
| `Status` | string | | Estado del lector (2.5). | `""` |

### 4.2 Sesion (`Session.*`)

| Propiedad | Tipo | Unidad | Descripcion | Sin dato |
|---|---|---|---|---|
| `Session.Id` | int | | Id de sesion en crudo. | 0 |
| `Session.Phase` | int | codigo | Fase (3.1). | 0 |
| `Session.PhaseText` | string | | Texto de la fase. | `""` |
| `Session.YellowState` | int | codigo | Estado de amarilla total (3.2). | 0 |
| `Session.YellowText` | string | | Texto del anterior. | `""` |
| `Session.ElapsedTime` | double | s | Tiempo de sesion. | 0 |
| `Session.EndTime` | double | s | Fin de sesion; `0` si no hay limite de tiempo. | 0 |
| `Session.TimeLeft` | double | s | `max(0, EndTime - ElapsedTime)`; `0` si no hay limite. | 0 |
| `Session.MaxLaps` | int | vueltas | Vueltas de la carrera; `0` si no hay limite (D). | 0 |
| `Session.LapsLeft` | int | vueltas | `max(0, MaxLaps - Laps del P1)`; `0` si `MaxLaps <= 0` o no hay P1. | 0 |
| `Session.NumVehicles` | int | autos | Autos en la sesion. | 0 |
| `Session.PlayerName` | string | | Jugador local. | `""` |
| `Session.Track` | string | | Pista; vacia en espectador. | `""` |
| `Session.TrackTemp` | double | grados C | Temperatura de pista (D); `0` si `NaN`. | 0 |
| `Session.AmbientTemp` | double | grados C | Temperatura ambiente (D); `0` si `NaN`. | 0 |
| `Session.FastestLap` | double | s | Mejor vuelta de la sesion. | 0 |
| `Session.FastestLapText` | string | `m:ss.fff` | Idem como texto. | `""` |
| `Session.FastestLapDriver` | string | | Piloto poseedor. | `""` |
| `Session.FastestLapSurname` | string | | Apellido del poseedor (6.3). | `""` |
| `Session.FastestLapPlace` | int | posicion | Posicion **actual** del poseedor (cambia si sube o baja). | 0 |
| `Session.FastestLapCarNumber` | string | | Numero de auto del poseedor (6.4). | `""` |
| `Session.FastestLapClass` | string | | Clase del poseedor. | `""` |

Los `Session.FastestLap*` valen su "sin dato" tambien cuando nadie tiene vuelta valida.

### 4.3 Por posicion (`PosNN.*`)

`NN` = `01` a `MaxPositions` (dos digitos; `Pos01` = lider). Los autos se leen por posicion
(`RfSnapshot.AtPlace`). Sin auto en esa posicion rige la columna "Sin dato". Las propiedades de tiempo
publican `0` (no `-1`) cuando no hay dato (helper `Pos`: `t > 0 ? t : 0`).

| Propiedad | Tipo | Unidad | Descripcion | Sin dato |
|---|---|---|---|---|
| `Driver` | string | | Nombre del piloto. | `""` |
| `Surname` | string | | Apellido con particulas (6.3). | `""` |
| `FirstName` | string | | Nombre de pila (6.3). | `""` |
| `Car` | string | | Nombre del coche segun el mod. | `""` |
| `Class` | string | | Clase del coche. | `""` |
| `Laps` | int | vueltas | Vueltas completadas. | 0 |
| `Sector` | int | 1..3 | Sector actual (3.4). | 0 |
| `CarNumber` | string | | Numero de auto sacado de `Car` (6.4). | `""` |
| `BestLap` | double | s | Mejor vuelta. | 0 |
| `BestLapText` | string | `m:ss.fff` | Idem texto. | `""` |
| `LastLap` | double | s | Ultima vuelta. | 0 |
| `LastLapText` | string | `m:ss.fff` | Idem texto. | `""` |
| `CurS1` | double | s | Sector 1 de la vuelta en curso; `0` hasta completarlo. | 0 |
| `CurS2` | double | s | Sector 2 aislado de la vuelta en curso; `0` hasta completarlo. | 0 |
| `GapLeader` | double | s | Brecha al lider (sin transformar salvo el latch). | 0 |
| `GapLeaderText` | string | `+s.fff` / `+nL` | Brecha al lider; vacia para el P1. | `""` |
| `GapAhead` | double | s | Brecha al auto de delante (sin transformar salvo el latch). | 0 |
| `GapAheadText` | string | `+s.fff` / `+nL` | Brecha al de delante; vacia para el P1. | `""` |
| `LapsBehindLeader` | int | vueltas | Vueltas de diferencia con el lider (?). | 0 |
| `InPits` | bool | | En boxes ahora. | `false` |
| `PitTime` | double | s | Tiempo en boxes (6.1). | 0 |
| `PitTimeText` | string | `m:ss.fff` | Idem texto; vacio si `0`. | `""` |
| `HasFastestLap` | bool | | `true` si este auto es el poseedor de la vuelta rapida. | `false` |
| `Pitstops` | int | | Paradas (?). | 0 |
| `Penalties` | int | | Penalizaciones (?). | 0 |
| `Status` | string | | Estado (3.3). | `""` |
| `IsPlayer` | bool | | Es el auto del jugador local. | `false` |
| `SpeedKmh` | double | km/h | `m/s * 3.6`. | 0 |
| `LapDist` | double | m | Distancia en la vuelta actual (negativa tras la meta). | 0 |
| `PosX` | double | m | Posicion X en el mundo. | 0 |
| `PosZ` | double | m | Posicion Z en el mundo. | 0 |

Cantidad: `MaxPositions` limitado a 1..128 (`max(1, min(MaxPositions, 128))`).

### 4.4 Carrusel (`Carousel.*` y `CarouselNN.*`)

Constantes (en `RfSpectatorPlugin.Carousel.cs`, se cambian recompilando): primera posicion `CarouselStart = 11`,
filas `CarouselRows = 7`, `CarouselSeconds = 5.0` por pagina.

Globales:

| Propiedad | Tipo | Descripcion | Sin dato / vacio |
|---|---|---|---|
| `Carousel.Pages` | int | Cantidad de paginas (6.5). | 0 |
| `Carousel.Page` | int | Pagina actual desde 1; `0` si no hay paginas. | 0 |
| `Carousel.FirstPlace` | int | Posicion real de la fila 1 de la pagina actual. | 0 |
| `Carousel.LastPlace` | int | Posicion real de la ultima fila **ocupada** de la pagina actual. | 0 |
| `Carousel.RangeText` | string | `first-last` (ej. `11-17`); solo `first` si coinciden; vacio si la fila 1 esta vacia. | `""` |

Por fila (`NN` = `01` a `07`). Mismo tipo, formato y valor sin dato que en `PosNN`, salvo `Place`:

| Propiedad | Tipo | Descripcion |
|---|---|---|
| `Place` | int | **Posicion real** del auto mostrado en la fila ahora; `0` si la fila esta vacia. |
| `Driver`, `Surname`, `FirstName`, `Car`, `Class` | string | Como en `PosNN`. |
| `Laps`, `Sector` | int | Como en `PosNN`. |
| `BestLap`, `LastLap`, `GapLeader`, `GapAhead`, `PitTime` | double | Como en `PosNN`. |
| `BestLapText`, `LastLapText`, `GapLeaderText`, `GapAheadText`, `PitTimeText`, `Status` | string | Como en `PosNN`. |
| `InPits`, `HasFastestLap`, `IsPlayer` | bool | Como en `PosNN`. |

Las filas del carrusel **no** incluyen `CarNumber`, `CurS1`, `CurS2`, `LapsBehindLeader`, `Pitstops`,
`Penalties`, `SpeedKmh`, `LapDist`, `PosX` ni `PosZ`.

---

## 5. Ajustes (`RfSpectatorSettings.cs`)

Persistidos por SimHub (JSON, clave `GeneralSettings`). Se leen en `Init`: un cambio requiere reiniciar SimHub.
Se guardan en `End`. La pantalla de ajustes del plugin (`SettingsControl`) solo muestra el estado del lector,
refrescado cada 500 ms (mapping, estado, conectado, datos frescos, tamano de vista, autos, tiempo de sesion,
ultimo cambio, lecturas descartadas); los valores se editan en el archivo de ajustes.

| Campo | Tipo | Defecto | Descripcion | Limites aplicados en codigo |
|---|---|---|---|---|
| `MappingName` | string | `$rFactorShared$` | Nombre del mapping. Usar otro para pruebas con ShmFake (ej. `$rFactorShared$Test`). | Vacio o en blanco -> `$rFactorShared$`. |
| `MaxPositions` | int | 40 | Cantidad de `PosNN` registradas. | 1..128. |
| `PollMs` | int | 100 | Intervalo de lectura en ms. | Minimo 20. |
| `StaleSeconds` | double | 5 | Segundos sin cambio de `CurrentET` antes de que `DataFresh` sea `false`. | Minimo 1. |

Las constantes del carrusel **no** son ajustes: estan en el codigo (4.4).

---

## 6. Conceptos derivados

### 6.1 PitTime (`PitTracker.cs`)

- Se calcula en el hilo del lector, sobre el snapshot aun no publicado, con el unico dato disponible: `InPits`.
- Por auto (clave `Driver|Car`) guarda el ultimo `InPits` visto y el `ElapsedTime` de la entrada.
- Solo una transicion `false -> true` **observada** inicia el cronometro. Un auto visto por primera vez ya en
  boxes (garaje al empezar la sesion, plugin iniciado en plena parada) queda en `0` hasta su proxima entrada.
- Mientras `InPits` es verdadero: `PitTime = max(0, ElapsedTime - entrada)`.
- Al salir (`InPits = false`) el tiempo vuelve a `0` y se descarta la entrada.
- Usa el reloj de la sesion, no el reloj de pared: una pausa del juego no infla la parada.
- `Reset()` al detectar sesion nueva (2.4).
- Limitacion: la memoria no da hora de entrada ni de salida, por lo que el tiempo mide desde el primer
  snapshot que mostro `InPits`, con la resolucion de `PollMs`/refresco del juego. Sin probar dentro de SimHub.

### 6.2 Vuelta rapida de la sesion (`RfParser.FindFastestPlace`)

- Se recorre `ByPlace` de P1 en adelante; se toma el menor `BestLap > 0`.
- **Empate**: la comparacion es `<` estricta, asi que gana el auto mejor clasificado (el primero en el orden de
  posicion con ese tiempo).
- Si nadie tiene `BestLap > 0`, `FastestLapPlace = 0` y `FastestLapCar = null`.
- rFactor reinicia `BestLap` por sesion, por lo que la vuelta rapida es por sesion sin contabilidad adicional.
- `HasFastestLap` compara por **posicion** (`v.Place == FastestLapPlace`): sigue al auto cuando cambian las
  posiciones. `Session.FastestLapPlace` refleja la posicion actual, no la que tenia al marcar la vuelta.

### 6.3 Apellido y nombre (`NameParts.cs`)

- El primer termino siempre es nombre de pila (el apellido nunca empieza antes del indice 1).
- Apellido = ultima palabra mas las particulas contiguas anteriores: `van von der den de del della di da dos du la
  le ter ten` (sin distinguir mayusculas).
- Un sufijo final (`jr jr. sr sr. ii iii iv`) se ignora para ubicar el apellido pero queda dentro de el.
- Un nombre de una sola palabra es todo apellido y `FirstName` queda vacio.
- Ejemplo: `Jorg van Ommen` -> `FirstName` `Jorg`, `Surname` `van Ommen`.

### 6.4 CarNumber (`NameParts.CarNumber`)

Se toma de `Car` si, tras recortar espacios a la izquierda, empieza con `#`: los caracteres alfanumericos
siguientes, hasta 4 (`#07 Mercedes` -> `07`). Conserva ceros a la izquierda. Sin `#` devuelve `""`. El juego
no entrega el numero como dato separado.

### 6.5 Carrusel (`CarouselMath.cs`)

Con `N` = `NumVehicles`, `start` = 11, `rows` = 7, `secondsPerPage` = 5:

| Formula | Resultado |
|---|---|
| `Pages` | `0` si `N - start + 1 <= 0`; si no `ceil((N - start + 1) / rows)`. |
| `Page` (base 0) | `0` si `pages <= 1`; si no `floor(nowSeconds / secondsPerPage) mod pages`. `nowSeconds` es el reloj UTC desde 1970, asi que todos los dashboards de la misma maquina muestran la misma pagina. `Carousel.Page` publica `Page + 1`. |
| `PlaceAt(page, fila)` | `start + page * rows + (fila - 1)`; `0` si la fila esta fuera de 1..rows o la posicion supera `N`. |

Ejemplos con las constantes actuales: N <= 10 -> 0 paginas; N entre 11 y 17 -> 1 pagina fija; N = 20 -> 2
paginas (11-17 y 18-20, con 4 filas vacias en la segunda).

### 6.6 Sentinelas y valores sin dato

| Dato | En memoria | En el snapshot | En la propiedad |
|---|---|---|---|
| Sin limite de tiempo | EndET `-2147483648` | `EndTime = NaN` | `EndTime = 0`, `TimeLeft = 0` |
| Sin limite de vueltas | MaxLaps fuera de 1..9999 | `MaxLaps = 0` | `MaxLaps = 0`, `LapsLeft = 0` |
| Temperatura absurda | fuera de (-100, 200) | `NaN` | `0` |
| Tiempo de vuelta/sector sin dato | `<= 0` | `-1` | `0` / texto vacio |
| S2 sin dato | | `-1` | `0` |
| Sin auto en la posicion | | `AtPlace = null` | texto `""`, numerico `0`, bool `false` |
| Sin poseedor de vuelta rapida | | `FastestLapPlace = 0` | `FastestLap* ` vacios/0, `HasFastestLap = false` |
