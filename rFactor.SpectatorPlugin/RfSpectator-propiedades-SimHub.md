# RfSpectator: catalogo de propiedades para SimHub

Plugin de SimHub que publica la **tabla de posiciones de rFactor 1 en modo espectador**: piloto, coche,
clase, vueltas, sectores, mejor y ultima vuelta, brechas y estado de cada auto, mas el estado de la sesion.

Hasta ahora, en espectador SimHub no mostraba ningun oponente (las propiedades estandar como
`DataCorePlugin.GameData.*` no se rellenan). Este plugin no pasa por el lector nativo de SimHub: lee
directamente la memoria compartida `$rFactorShared$` que escribe el plugin `rFactorSharedMemoryMap.dll`
y publica sus propias propiedades.

Todas las propiedades tienen el prefijo **`RfSpectatorPlugin.`**.

---

## 1. Requisitos y entorno probado

- rFactor 1 con el plugin `rFactorSharedMemoryMap.dll` cargado en su carpeta `Plugins`.
- SimHub con `User.RfSpectator.dll` en su carpeta de instalacion. En las pruebas bastó con copiarlo y
  abrir SimHub.
- Probado con: SimHub 9.12.9; servidor dedicado con el cliente `rFactor.exe` en modo espectador; mod
  DTM 1994; carreras de 5 y de 10 autos. Con 10 autos aparecieron los 10 pilotos, cada uno en su posicion.
- No probado: practica, clasificacion, mas de 10 autos, carreras con paradas en boxes, autos doblados,
  otros mods u otras versiones del DLL de memoria compartida.

El plugin funciona sea cual sea el juego que SimHub tenga seleccionado y aunque SimHub muestre
"Waiting for data": no depende del lector nativo de rFactor.

---

## 2. Como usar las propiedades en un dashboard

En Dash Studio, cada propiedad con boton **fx** acepta una formula. Esta forma fue **verificada** (un
texto enlazado asi mostro el nombre del piloto):

```
[RfSpectatorPlugin.Pos01.Driver]
```

Convenciones:

- Las posiciones se numeran con dos digitos: `Pos01`, `Pos02`, ... `Pos40`. `Pos01` es el lider.
- Por defecto se publican **40 posiciones**. La cantidad se cambia en el archivo de ajustes del
  plugin (`MaxPositions`, maximo 128) y se aplica al reiniciar SimHub.
- Si no hay auto en esa posicion, los textos son vacios (`""`), los numericos `0` y los booleanos
  `false`. Una carrera de 5 autos deja de `Pos06` en adelante vacias.
- Las propiedades `...Text` ya vienen formateadas para mostrar; las numericas sirven para calculos.
- Los dashboards del leaderboard nativo usan componentes especiales (`Opponent...Text`) que no se
  pueden enlazar a estas propiedades. Hay que usar **textos normales** de la Toolbox.

---

## 3. Conexion

| Propiedad | Tipo | Descripcion |
|---|---|---|
| `RfSpectatorPlugin.Connected` | Boolean | `true` si el plugin abrio la memoria compartida y tiene un tamano valido. |
| `RfSpectatorPlugin.DataFresh` | Boolean | `true` si el tiempo de sesion cambio en los ultimos 5 s. Con el juego en pausa o fuera de sesion pasa a `false` sin estar desconectado. |
| `RfSpectatorPlugin.Status` | String | Estado en texto, en espanol: `Conectado`, `Esperando el mapping ...`, `Lectura descartada: ...`, `Layout incompatible ...`. Sirve para diagnostico. |

---

## 4. Sesion

| Propiedad | Tipo | Descripcion | Notas |
|---|---|---|---|
| `RfSpectatorPlugin.Session.Id` | Int32 | Numero de sesion en crudo. | Valio `7` en una carrera. No se conoce su equivalencia con practica/clasificacion/carrera. |
| `RfSpectatorPlugin.Session.Phase` | Int32 | Fase de la sesion (codigos en la seccion 7). | Observados: 4, 5, 6 y 8. |
| `RfSpectatorPlugin.Session.PhaseText` | String | Texto de la fase, en espanol. | Para dashboards en otro idioma, usar `Phase`. |
| `RfSpectatorPlugin.Session.YellowState` | Int32 | Estado de la bandera amarilla total / safety car (codigos en la seccion 7). | Observados: 0, 1, 2, 4, 5 y 6. |
| `RfSpectatorPlugin.Session.YellowText` | String | Texto del estado anterior, en espanol. | |
| `RfSpectatorPlugin.Session.ElapsedTime` | Double | Tiempo de sesion transcurrido, en segundos. | |
| `RfSpectatorPlugin.Session.EndTime` | Double | Tiempo de fin de sesion, en segundos. `0` si la sesion no tiene limite de tiempo (carrera por vueltas). | |
| `RfSpectatorPlugin.Session.TimeLeft` | Double | Segundos restantes (`EndTime - ElapsedTime`). `0` si no hay limite de tiempo. | |
| `RfSpectatorPlugin.Session.MaxLaps` | Int32 | Vueltas de la carrera. `0` si no hay limite de vueltas. | Dio `5` en una carrera de 5 vueltas. |
| `RfSpectatorPlugin.Session.LapsLeft` | Int32 | Vueltas que le quedan al lider (`MaxLaps` menos sus vueltas completadas). | Solo con `MaxLaps > 0`. |
| `RfSpectatorPlugin.Session.NumVehicles` | Int32 | Autos en la sesion. | |
| `RfSpectatorPlugin.Session.PlayerName` | String | Nombre del jugador local. | |
| `RfSpectatorPlugin.Session.Track` | String | Nombre de la pista. | **Siempre vacio en espectador**: ese dato solo llega con la telemetria, que rFactor entrega al que conduce. |
| `RfSpectatorPlugin.Session.TrackTemp` | Double | Temperatura de la pista, en grados Celsius. | Valores plausibles (26,9); sin otra referencia con que compararlos. |
| `RfSpectatorPlugin.Session.AmbientTemp` | Double | Temperatura ambiente, en grados Celsius. | Idem. |

---

## 5. Por posicion: `RfSpectatorPlugin.PosNN.*`

`NN` va de `01` a la cantidad configurada. Ejemplo: `RfSpectatorPlugin.Pos03.BestLapText`.

### Identificacion

| Propiedad | Tipo | Descripcion | Ejemplo |
|---|---|---|---|
| `Driver` | String | Nombre del piloto. | `Jorg van Ommen` |
| `Surname` | String | Solo el apellido, incluidas las particulas (`van`, `von`, `de`, `del`...). Util para HUDs estrechos. | `van Ommen` |
| `FirstName` | String | Nombre de pila (todo lo que precede al apellido). | `Jorg` |
| `Car` | String | Nombre del coche tal como lo da el mod, normalmente con el numero y el apellido. | `#15 Mercedes C180 DTM van Ommen` |
| `Class` | String | Clase del coche. | `Mercedes C180 DTM` |
| `IsPlayer` | Boolean | `true` si es el auto del jugador local. | En espectador es `false` para todos. |

### Carrera

| Propiedad | Tipo | Descripcion | Ejemplo |
|---|---|---|---|
| `Laps` | Int32 | Vueltas **completadas**. | `1` |
| `Sector` | Int32 | Sector en el que va el auto: `1`, `2` o `3`. | `2` |
| `LapDist` | Double | Distancia recorrida en la vuelta actual, en metros. | `744.16` |
| `Status` | String | `Corriendo`, `Terminado`, `DNF` o `DQ` (en espanol). | `Corriendo` |
| `InPits` | Boolean | `true` entre la entrada y la salida de boxes. | `false` |
| `Pitstops` | Int32 | Paradas realizadas. | Sin verificar (siempre `0` en las pruebas). |
| `Penalties` | Int32 | Penalizaciones pendientes. | Sin verificar (siempre `0`). |

### Tiempos (segundos; `0` o texto vacio si no hay dato)

| Propiedad | Tipo | Descripcion | Ejemplo |
|---|---|---|---|
| `BestLap` | Double | Mejor vuelta, en segundos. | `59.946` |
| `BestLapText` | String | Mejor vuelta como `m:ss.fff`. | `0:59.946` |
| `LastLap` | Double | Ultima vuelta, en segundos. | `59.946` |
| `LastLapText` | String | Ultima vuelta como `m:ss.fff`. | `0:59.946` |
| `CurS1` | Double | Tiempo del sector 1 de la vuelta en curso. `0` hasta completarlo. | `16.836` |
| `CurS2` | Double | Tiempo del sector 2 **aislado** de la vuelta en curso. `0` hasta completarlo. | `0` |

### Brechas

| Propiedad | Tipo | Descripcion | Ejemplo |
|---|---|---|---|
| `GapLeader` | Double | Segundos de diferencia con el lider. `0` para el lider. | `0.181` |
| `GapLeaderText` | String | Brecha al lider como `+s.fff`. Vacio para el lider. | `+0.181` |
| `GapAhead` | Double | Segundos de diferencia con el auto de delante. | `0.181` |
| `GapAheadText` | String | Brecha al de delante como `+s.fff`. Vacio para el lider. | `+0.181` |
| `LapsBehindLeader` | Int32 | Vueltas de diferencia con el lider. | Sin verificar (siempre `0`: nunca hubo autos doblados). |

Si `LapsBehindLeader` es mayor que cero, los dos textos de brecha muestran `+NL` (por ejemplo `+1L`)
en lugar de segundos. Con autos doblados, `GapAheadText` puede no reflejar la diferencia real con el
auto de delante: queda por comprobar.

### Movimiento

| Propiedad | Tipo | Descripcion | Ejemplo |
|---|---|---|---|
| `SpeedKmh` | Double | Velocidad, en km/h. | `183.8` |
| `PosX` | Double | Posicion en el mundo (eje X), en metros. | `237.9` |
| `PosZ` | Double | Posicion en el mundo (eje Z), en metros. | `-10.4` |

`PosX` y `PosZ` sirven como base para un mapa de pista. Se actualizan con cada refresco de la puntuacion
del juego, no a cada frame: no son suaves.

---

## 5b. Carrusel de posiciones (`RfSpectatorPlugin.Carousel*`)

Para dashboards estilo transmision de TV: el bloque principal muestra `Pos01` a `Pos10` y un bloque
inferior de 10 filas recorre las posiciones 11 en adelante, cambiando de pagina cada 5 segundos.
El plugin calcula la rotacion; el dashboard solo lee las filas.

- Con 10 autos o menos, el carrusel esta vacio (`Carousel.Pages = 0`).
- Con 11 a 20 autos hay una sola pagina y no rota.
- Con mas, hay una pagina por cada 10 autos. En la ultima pagina, las filas sobrantes quedan vacias.
- La pagina depende del reloj del ordenador: dos pantallas de SimHub en la misma maquina muestran la misma pagina.
- Valores por defecto (primera posicion 11, 10 filas, 5 segundos): se cambian en el codigo del plugin
  (`RfSpectatorPlugin.Carousel.cs`) y se recompila.

| Propiedad | Tipo | Descripcion |
|---|---|---|
| `Carousel.Pages` | Int32 | Cantidad de paginas. `0` si no hay autos desde la posicion 11. |
| `Carousel.Page` | Int32 | Pagina actual, desde 1. `0` si no hay carrusel. |
| `Carousel.FirstPlace` / `Carousel.LastPlace` | Int32 | Primera y ultima posicion que muestra la pagina actual. |
| `Carousel.RangeText` | String | Rango de la pagina actual, por ejemplo `11-20`. Util para un titulo. |
| `CarouselNN.Place` | Int32 | **Posicion real** del auto que muestra la fila `NN` (de `01` a `10`) en este momento. `0` si la fila esta vacia. |
| `CarouselNN.Driver`, `Surname`, `FirstName`, `Car`, `Class` | String | Igual que en `PosNN`. |
| `CarouselNN.Laps`, `Sector` | Int32 | Igual que en `PosNN`. |
| `CarouselNN.BestLap`, `LastLap`, `GapLeader`, `GapAhead` | Double | Igual que en `PosNN`. |
| `CarouselNN.BestLapText`, `LastLapText`, `GapLeaderText`, `GapAheadText`, `Status` | String | Igual que en `PosNN`. |
| `CarouselNN.InPits`, `IsPlayer` | Boolean | Igual que en `PosNN`. |

Las filas del carrusel no incluyen `CurS1`, `CurS2`, `Pitstops`, `Penalties`, `LapsBehindLeader`,
`SpeedKmh`, `LapDist`, `PosX` ni `PosZ`. Estado: la logica de rotacion esta probada con pruebas de
unidad; **sin probar dentro de SimHub**.

---

## 6. Comportamientos que conviene conocer

- **El orden lo da la posicion en carrera**, no el orden de los autos en la memoria. `Pos01` es siempre
  el lider.
- **Despues de la meta**: cuando un auto cruza la linea (`Status = Terminado`), el juego deja de dar
  brechas validas. El plugin **congela** `GapLeader` y `GapAhead` en el valor del primer refresco
  posterior a la meta (nunca negativo). Se liberan al empezar otra sesion.
- **Sesion nueva**: se detecta cuando el tiempo de sesion retrocede o cambia `Session.Id`.
- **Sin autos** (`NumVehicles = 0`) es un estado normal al inicio o fin de sesion o al cerrar el juego:
  la tabla queda vacia. No es un error.
- **Datos descartados**: el escritor de la memoria no tiene sincronizacion, asi que una lectura puede
  coincidir con una escritura. El plugin valida cada lectura (rangos, posiciones sin repetir, numeros
  finitos) y conserva la anterior si no pasa. Se ve en la pantalla de estado del plugin.
- **No se publica quien controla el auto** (humano o IA): el valor del juego alternaba entre dos
  codigos para el mismo auto y no es fiable.
- **Frecuencia de actualizacion**: se reporta que el juego entrega la puntuacion unas dos veces por
  segundo. No se midio. Sirve para tablas de tiempos; no para animar autos en un mapa.

---

## 7. Codigos

### `Session.Phase`

| Valor | Significado |
|---|---|
| 0 | Antes de empezar la sesion |
| 1 | Vueltas de reconocimiento (solo carrera) |
| 2 | Parrilla (solo carrera) |
| 3 | Vuelta de formacion (solo carrera) |
| 4 | Cuenta atras de las luces de salida |
| 5 | Bandera verde |
| 6 | Amarilla total / safety car (FCY) |
| 7 | Sesion detenida |
| 8 | Sesion terminada |

### `Session.YellowState` (solo amarilla total)

| Valor | Significado |
|---|---|
| 0 | Ninguna |
| 1 | Pendiente |
| 2 | Boxes cerrados |
| 3 | Boxes abiertos para la vuelta lider |
| 4 | Boxes abiertos |
| 5 | Ultima vuelta |
| 6 | Reanudar |
| 7 | Carrera detenida (no se usa actualmente) |

Despues de la meta se observo que este valor alterna entre 0 y 6.

---

## 8. Estado de verificacion

Leyenda: **Verificado** = visto con datos reales de carrera; **Plausible** = valor razonable sin otra
referencia; **Sin probar** = nunca vimos un valor distinto de cero.

| Grupo | Estado |
|---|---|
| `Connected`, `DataFresh`, `Status` | Verificado |
| `Session.Phase`, `PhaseText`, `YellowState`, `YellowText` | Verificado en una carrera con FCY |
| `Session.ElapsedTime`, `NumVehicles`, `PlayerName`, `Id` | Verificado |
| `Session.MaxLaps`, `LapsLeft` | Verificado (carrera de 5 vueltas) |
| `Session.EndTime`, `TimeLeft` | Verificado el valor "sin limite" en carrera por vueltas; sesion por tiempo solo vista en un volcado previo |
| `Session.TrackTemp`, `AmbientTemp` | Plausible |
| `Session.Track` | Vacio en espectador (esperado) |
| `Surname`, `FirstName` | Verificado solo con pruebas de unidad; sin probar en un dashboard |
| `Driver`, `Car`, `Class`, `Laps`, `Sector`, `LapDist`, `Status`, `InPits` | Verificado (`InPits` se vio al final de la carrera) |
| `BestLap*`, `LastLap*`, `CurS1`, `CurS2`, `GapLeader*`, `GapAhead*` | Verificado |
| `SpeedKmh`, `PosX`, `PosZ` | Verificado (valores coherentes) |
| `IsPlayer` | Verificado (siempre `false` en espectador) |
| `Pitstops`, `Penalties`, `LapsBehindLeader` | **Sin probar** |

---

## 9. Limitaciones

- **Depende del layout de un binario sin codigo fuente.** El formato de la memoria compartida se dedujo
  de datos reales; no coincide con el codigo publicado en el repositorio de `rFactorSharedMemoryMap`.
  Si una version distinta del DLL cambia el formato, el plugin deberia detectarlo y mostrar
  `Layout incompatible` o descartar lecturas, pero no esta probado con otra version.
- **Procedencia del DLL no verificada.** Se uso el `rFactorSharedMemoryMap.dll` que ya estaba en
  `Plugins`. No se comparo su hash con el que distribuye SimHub.
- **Textos en espanol** (`PhaseText`, `YellowText`, `Status` por posicion y los mensajes de estado). Para
  multiples idiomas, usar los codigos numericos.
- **Sin nombre de pista** en espectador, por la razon explicada en la seccion 4.
- **Las propiedades estandar de SimHub** (`DataCorePlugin.GameData.*`) siguen sin rellenarse en
  espectador; este plugin no las modifica.
- **Licencias:** el plugin lee la memoria compartida y no incluye codigo de `rFactorSharedMemoryMap`
  (licencia GPL-2.0 en su repositorio original). Licencia de este plugin: pendiente de definir.
