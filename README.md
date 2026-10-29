# rfactor-spectator-plugin-simhub

rFactor 1 Spectator Plugin to load info in dashboards.

Plugin de SimHub que publica la tabla de posiciones de rFactor 1 en modo espectador (piloto, coche, clase,
vueltas, sectores, mejor y ultima vuelta, brechas, boxes y estado de sesion) para usarla en dashboards.
En espectador SimHub no rellena los oponentes con su lector nativo; este plugin lee directamente la memoria
compartida de rFactor y publica sus propias propiedades `RfSpectatorPlugin.*`.

## Como funciona

```
rFactor 1 + plugin de memoria compartida  ->  "$rFactorShared$"
  -> ShmReader (hilo de fondo, solo lectura, cada 100 ms)
  -> RfParser: valida y convierte -> RfSnapshot (inmutable)
  -> PitTracker (tiempo en boxes) -> snapshot publicado
  -> propiedades de SimHub (AttachDelegate) -> dashboards
```

1. El plugin de memoria compartida cargado en rFactor 1 (`rFactorSharedMemoryMap.dll`) escribe el bloque
   `$rFactorShared$`.
2. `ShmReader` lo lee en su propio hilo, sin escribir nunca en el mapping, y como el escritor no tiene
   sincronizacion valida cada lectura (rangos, posiciones sin repetir, numeros finitos); si falla, conserva la anterior.
3. Cada lectura valida se convierte en un `RfSnapshot` inmutable, ordenado por posicion en carrera.
4. Los delegados de SimHub (`AttachDelegate`) leen siempre el ultimo snapshot, sin bloqueos: SimHub nunca
   toca el mapping.

El plugin funciona sea cual sea el juego seleccionado en SimHub y aunque SimHub muestre "Waiting for data".

## Requisitos

- Windows, SimHub y rFactor 1 con el plugin de memoria compartida (`rFactorSharedMemoryMap.dll`) en su carpeta `Plugins`.
- .NET Framework 4.8 y Visual Studio (o MSBuild) para compilar el plugin.
- Variable de entorno `SIMHUB_INSTALL_PATH` apuntando a la carpeta de instalacion de SimHub (con barra final):
  el proyecto toma de ahi las DLL de referencia y copia alli el resultado.
- Para las pruebas: SDK de .NET 8 (Windows o Linux).

## Compilar e instalar

1. Definir `SIMHUB_INSTALL_PATH` (ej. `C:\Program Files (x86)\SimHub\`) y reiniciar el IDE.
2. Abrir `rFactor.SpectatorPlugin/User.PluginSdkDemo.sln` y compilar `User.RfSpectator.csproj`.
3. El evento posterior a la compilacion copia `User.RfSpectator.dll` y su `.pdb` (y los `.resx` de idioma) a
   `%SIMHUB_INSTALL_PATH%`.
4. Abrir SimHub: el plugin aparece como **RfSpectator**. Iniciar rFactor con el plugin de memoria compartida.

Los dashboards enlazan las propiedades con formulas como `[RfSpectatorPlugin.Pos01.Driver]` en textos normales
de Dash Studio (los componentes `Opponent...Text` del leaderboard nativo no se pueden enlazar).

## Ajustes

Definidos en `rFactor.SpectatorPlugin/RfSpectatorSettings.cs` y guardados por SimHub (clave `GeneralSettings`).
Los cuatro primeros se leen al iniciar: un cambio en el archivo de ajustes requiere reiniciar SimHub. Las filas del
carrusel y los colores se editan en la pantalla del plugin (junto al estado del lector) y se aplican al instante.

| Ajuste | Defecto | Descripcion |
|---|---|---|
| `MappingName` | `$rFactorShared$` | Nombre del mapping (usar otro al probar con ShmFake). |
| `MaxPositions` | 40 | Cantidad de `PosNN` publicadas (1 a 128). |
| `PollMs` | 100 | Intervalo de lectura en ms (minimo 20). |
| `StaleSeconds` | 5 | Segundos sin avanzar el reloj de sesion antes de que `DataFresh` sea `false` (minimo 1). |
| `CarouselRows` | 7 | Filas visibles del carrusel (1 a 20). Se edita en la pantalla del plugin; aplica en vivo. |
| `ClassColors` | vacio | Clase -> `#RRGGBB`. Se edita en la pantalla del plugin; aplica en vivo. |
| `CarColors` | vacio | Coche (`PosNN.Car`) -> `#RRGGBB`. Se edita en la pantalla del plugin; aplica en vivo. |

Colores: solo `#RRGGBB` (se guarda en mayusculas, lo invalido se ignora), nombres sin distinguir mayusculas. El color
por auto gana al de la clase; sin coincidencia la propiedad es `""`. Se publican como `PosNN.Color`, `CarouselNN.Color`
y `Session.FastestLapColor`.

## Estructura de `rFactor.SpectatorPlugin/`

| Archivo | Funcion |
|---|---|
| `RfSpectatorPlugin.cs` | Punto de entrada: crea el lector y registra las propiedades `Session.*` y `PosNN.*`. |
| `RfSpectatorPlugin.Carousel.cs` | Parte del plugin que registra `Carousel.*` y `CarouselNN.*`. |
| `RfSpectatorSettings.cs` | Ajustes persistidos. |
| `SettingsControl.cs` | Pantalla del plugin en SimHub: estado del lector, filas del carrusel y colores (WPF, creada en codigo). |
| `ColorResolver.cs` | Validacion `#RRGGBB` y resolucion de color (auto sobre clase), sin dependencias de SimHub. |
| `ShmReader.cs` | Hilo lector del mapping, deteccion de sesion nueva, estado de conexion. |
| `RfLayout.cs` | Offsets del bloque de memoria compartida (unico sitio a tocar si cambia el formato). |
| `RfSnapshot.cs` | `RfSnapshot`, `RfVehicle`, `RfParser` (validacion y decodificacion) y `RfFormat` (textos). |
| `PitTracker.cs` | Tiempo en boxes en vivo a partir de `InPits`. |
| `CarouselMath.cs` | Logica de paginas del carrusel, sin dependencias de SimHub. |
| `NameParts.cs` | Apellido, nombre de pila y numero de auto a partir de los textos del juego. |
| `RfSpectator-propiedades-SimHub.md` | Catalogo de propiedades para quien arma dashboards. |
| `DATA_DICTIONARY.md` | Diccionario de datos completo. |

`rFactor.SpectatorPlugin.Tests/` contiene las pruebas de unidad; enlaza solo los fuentes sin dependencia de SimHub.

## Carrusel de posiciones

Para dashboards estilo transmision de TV: el bloque principal muestra `Pos01` a `Pos10` y un bloque inferior
recorre las posiciones 11 en adelante (`CarouselNN`, 7 filas por defecto, configurable de 1 a 20 en la pantalla del plugin)
cambiando de pagina cada 5 segundos. Se registran siempre 20 filas; las que exceden el ajuste quedan vacias.
`Carousel.Pages`, `Page`, `FirstPlace`, `LastPlace` y `RangeText` describen la pagina actual. La rotacion
depende del reloj del ordenador. La primera posicion (11) y los segundos por pagina (5) estan en
`RfSpectatorPlugin.Carousel.cs` y se cambian recompilando.

## Funciones recientes

- **Tiempo en boxes en vivo** (`PosNN.PitTime`, `PitTimeText`, tambien en `CarouselNN`): segundos desde que el
  plugin observa la entrada a boxes (`InPits` pasa de `false` a `true`), con el reloj de la sesion; `0` fuera
  de boxes o si el auto ya estaba en boxes al verlo por primera vez.
- **Vuelta rapida de la sesion** (`Session.FastestLap`, `FastestLapText`, `FastestLapDriver`,
  `FastestLapSurname`, `FastestLapPlace`, `FastestLapCarNumber`, `FastestLapClass`) y `PosNN.HasFastestLap`
  para resaltar la fila. En empate gana el auto mejor clasificado.
- **Colores por clase y por auto** (`PosNN.Color`, `CarouselNN.Color`, `Session.FastestLapColor`) y **filas del carrusel
  configurables**, ambos desde la pantalla del plugin.
- Numero de auto (`PosNN.CarNumber`) tomado del nombre del coche cuando empieza con `#`.

## Pruebas

```
dotnet test rFactor.SpectatorPlugin.Tests
```

Corren en Windows y Linux (proyecto `net8.0` con xUnit, sin SimHub). Cubren el parser, `PitTracker`, la vuelta
rapida, el carrusel, `ColorResolver` y `NameParts`. La capa de SimHub (registro de propiedades, ajustes, pantalla) no tiene
pruebas automaticas.

## Limites y datos sin confirmar

- El formato de la memoria compartida se dedujo por ingenieria inversa; no coincide con el codigo publicado de
  `rFactorSharedMemoryMap`. En `RfLayout.cs` cada campo lleva su confianza: **V** verificado, **D** derivado,
  **?** sin confirmar.
- Derivados (D): `MaxLaps`, `AmbientTemp`, `TrackTemp`. Sin confirmar (?): `Pitstops`, `Penalties`,
  `LapsBehindLeader` (siempre 0 en las pruebas; con autos doblados `GapAhead` puede no ser fiable).
- `Session.Track` esta vacio en espectador. `IsPlayer` es `false` para todos en espectador.
- `PitTime`, `HasFastestLap`, `Session.FastestLap*`, el carrusel y la resolucion de colores estan probados con pruebas de unidad, no dentro de SimHub.
- Los textos (`PhaseText`, `YellowText`, `Status`) estan en espanol; para otros idiomas usar los codigos numericos.
- No probado: practica, clasificacion, paradas en boxes reales, autos doblados, otras versiones del DLL de
  memoria compartida.

## Documentacion

- [Diccionario de datos](rFactor.SpectatorPlugin/DATA_DICTIONARY.md): layout de la memoria, snapshot, codigos, propiedades, ajustes y conceptos derivados.
- [Catalogo de propiedades para SimHub](rFactor.SpectatorPlugin/RfSpectator-propiedades-SimHub.md): uso en dashboards y estado de verificacion.

## Licencia

Ver [LICENSE](LICENSE) (GNU GPL v3).
