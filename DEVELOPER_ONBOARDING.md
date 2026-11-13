# Guía de Onboarding para el Desarrollador (Agentes AI / Humanos)

¡Bienvenido al desarrollo de **In Side Us**! Este documento está diseñado para ayudarte a engancharte al proyecto instantáneamente, entender cómo están conectadas sus piezas clave y guiar tus futuras contribuciones de manera precisa y coherente.

---

## 🚀 Visión General del Proyecto
**In Side Us** es un juego de supervivencia opresivo en primera persona donde el jugador explora una mega-infraestructura generada procedimentalmente.
*   **Temática Core:** *"El tiempo es tu mayor enemigo"*. No existen barras de porcentaje tradicionales (0-100%). La comida, el agua, la fatiga y la batería se gestionan y visualizan exclusivamente como **relojes regresivos y contadores de horas**.
*   **Estilo Visual:** Casco con HUD inmersivo (falla y parpadea en estado crítico), entornos industriales opresivos, niebla densa y física inercial de objetos.

---

## 🛠️ Arquitectura de Código (Scripts Clave)

### 1. Datos del Jugador y Ciclo Vital (`PlayerData.cs`)
*   **Contadores de Supervivencia:** `waterSeconds` (Agua), `foodSeconds` (Comida) y `energySeconds` (Batería del casco). Todos tienen un valor máximo por defecto de `600f` (10 minutos reales).
*   **Tasas de Desgaste (Update):**
    *   **Agua:** -1.0s de recurso por segundo real.
    *   **Comida:** -0.4s de recurso por segundo real.
    *   **Batería:** -3.0s de recurso por segundo real.
*   **Glitch del Casco:** Si la batería cae por debajo del 15% (`<= maxEnergySeconds * 0.15f`), entra en modo glitch. Dos referencias en `PlayerData`:
    *   `cascoUI` (CanvasGroup) → modula `alpha` (para UI en Canvas, opcional)
    *   `hudContenedor` (GameObject) → modula `SetActive` (para textos flotantes TMP 3D, recomendado en este proyecto)

    En rango glitch ambos parpadean cada frame: `cascoUI.alpha = Random.value > 0.4f ? 0.3f : 0.05f` y `hudContenedor.SetActive(Random.value > 0.4f)`. A `0f`, ambos se apagan total.

    **Sinergia con `GrainUI`:** agregando `hudContenedor` al `uisAVigilar`, el grain se apaga automáticamente cuando muere el casco — sensación de "ya no hay pantalla entre el jugador y el mundo".
*   **Ciclo del Tiempo:** El día dura **40 horas** en este mundo. El paso del tiempo se acelera con `multiplicadorTiempo = 60f` (1 segundo real = 1 hora de juego). El calendario mide únicamente el `diaDelAnio` (1-365) y el `anioActual` (comienza en 2084).
*   **Fatiga:** `horasDespierto` acumula las horas que lleva el jugador sin dormir.
*   **Coordenadas del Mundo (HUD):** `coordenadaMundo = transform.position` y `coordenadaGrilla = FloorToInt(transform.position / cellSize)` se recalculan **cada frame** en `PlayerData.Update`. El cálculo ya no depende de que el `WorldGenerator` avise vía `ActualizarCoordenadaGrilla` (eso solo se llamaba al cruzar celdas, así que en el frame del spawn el HUD mostraba 0,0,0 incorrectamente). Ahora el HUD muestra siempre la celda real del mundo, consistente con la fórmula que usa `CartelCoordenadas`.

> [!WARNING]
> **Discrepancia en Consumibles y Sueño:** `horasDespierto` se mide en **horas** dentro de `PlayerData.cs`, pero `ConsumibleData.restaurarSueno` se describe en segundos. Actualmente se resta directamente (`player.horasDespierto -= restaurarSueno`). Si configuras consumibles de descanso, ten en cuenta este factor de escala.

---

### 2. Mochila Modular (`MochilaGrid.cs`, `MochilaVisual.cs`, `ControladorMochila.cs`, `ManoJugador.cs`, `ModuloData.cs`, `InventoryItem.cs`)
Inventario tipo Lego/Trailmakers (plano 2D): mochila base 2×2 a la que se pegan módulos para crecer. **Toda la lógica es matemática de celdas** (`HashSet<Vector2Int>` en `MochilaGrid`), sin colliders-trigger ni anclajes físicos — por eso es estable.

*   **`MochilaGrid.cs`** (núcleo): celdas existentes, `PuedeColocarModulo`/`ColocarModulo`/`QuitarModulo` (sin diagonales, debe tocar una celda existente), `AnclajesLibres()` (celdas vacías adyacentes, calculadas solas).
*   **`MochilaVisual.cs`**: gizmos de celdas (verde) y anclajes (amarillo). `tamanoCelda` define el tamaño.
*   **`ControladorMochila.cs`**: la mochila sigue `MochilaPos.Guardada` con `Lerp`; **Q** alterna a `MochilaPos.Abierta` (libera cursor + bloquea cámara) y de vuelta.
*   **`ManoJugador.cs`** (interacción):
    *   **Mantener Click Izquierdo** sostiene el objeto apuntado; **soltar** lo suelta con inercia. Raycast desde el centro (mochila cerrada) o el cursor (mochila abierta).
    *   Mochila abierta: el objeto flota en 2D siguiendo el cursor; **rueda** = acercar/alejar.
    *   **Click Derecho** rota 90° (4 pasos): mesh + forma lógica.
    *   Acercar un **módulo** a un borde válido hace **snap** (centrado por el centroide de su forma); soltar lo **pega** (sus celdas se *suman* al grid). Acercar un **item** a celdas existentes y libres hace snap y al soltar las *ocupa*. Agarrarlo de nuevo lo **despega/libera**.
    *   **Escala:** lee el `lossyScale` del *padre* (no del objeto rotado, que Unity aproxima mal). Módulos → escala uniforme (planos); items → por eje (proporción real, no se achatan en Z).
*   **`ModuloData.cs`**: hereda de `ItemData`; define la `forma` (lista de celdas) editable con la grilla visual del inspector (`ModuloDataEditor`).

> [!NOTE]
> Pendiente: **crafteo** de módulos y un **preview verde/rojo** explícito. Modelar prefabs de módulo con el **pivote centrado** para que calcen con la grilla.

---

### 3. Locomoción: Vuelo y Escala (`PlayerMovement.cs`, `SistemaRapel.cs`, `SistemaEscalera.cs`)
*   **Mecánica de Vuelo (Space to Fly):** No hay salto normal. Mantener **Espacio** eleva al jugador flotando verticalmente a `velocidadVueloAscenso = 8f`. Al soltarlo, cae con gravedad límite de `velocidadVueloCaida = -15f`.
*   **Sistema de Rápel (Cuerdas):**
    *   **Colocación de Anclaje (`PilarRapel`):** Se sostiene con Click Izquierdo y se apunta con Click Derecho para activar la vista previa flotante a **1.5 metros** enfrente de la cámara. Al soltar el Click Derecho, se tira un raycast vertical hacia abajo y se coloca en el suelo detectado (rango de 10m), desplegando una cuerda de hasta 100m. Si se suelta el Click Izquierdo, se cancela y se dropea.
    *   **Recogida:** Al levantar un anclaje colocado con Click Izquierdo, la cuerda se pliega y oculta automáticamente (`pilar.MostrarCuerda(false)`).
    *   **Enganche:** Apuntar a la cuerda/pilar a menos de 5m y pulsar **F** engancha al jugador. Solo se permite la interacción si la cuerda está desplegada.
    *   **W / S:** Subir/bajar cuerda a 4m/s.
    *   **Click Derecho (en la cima):** Si estás a menos de 1.5m del pilar, te sube a la cornisa segura (`puntoLlegada`).
    *   **Espacio (mantener 2s):** Te suelta de la cuerda y reactiva tu gravedad.
*   **Sistema de Escaleras:**
    *   Al entrar en un trigger de Escalera (`Escalera.cs`), el jugador se engancha automáticamente.
    *   **W / S:** Sube/bajar. Se bloquea el movimiento horizontal del jugador.
    *   **Click Derecho (en la cima):** Te saca de la escalera posicionándote en el tope.
    *   **Espacio:** Te suelta al instante.

---

### 4. Generación Procedimental Infinita (`WorldGenerator.cs`)
Genera un laberinto en tiempo de ejecución basado en una grilla tridimensional de **Macro Chunks** (5x5x5 celdas de `cellSize = 20f`).

> ### ⚠️ LEER ANTES DE TOCAR COORDENADAS: la grilla es LOCAL al transform del WG
> `GenerarCelda` instancia en `transform.position + celda*cellSize`, y el GameObject del `WorldGenerator` **no está en el origen** (`(-11.24, -0.23, 42.47)` en `OutdoorsScene`). Para traducir mundo↔celda usá **siempre**:
> *   `WorldGenerator.MundoACelda(Vector3 worldPos)` → `Vector3Int`
> *   `WorldGenerator.CeldaACentroMundoPiso(Vector3Int celda)` → `Vector3`
>
> **Nunca** `Mathf.FloorToInt(pos.x / cellSize)` pelado. Ese patrón estaba copiado en 6 lugares (incluido el `Update` que maneja la burbuja de streaming, y las coords del HUD en `PlayerData`) y desalineaba la grilla lógica respecto a la geometría real — corregido el 2026-07-27, ver §5 del GDD para el detalle y el síntoma.


*   **⚠️ Desactualizado — "Gran Pared"/`limiteZ` NO EXISTEN:** se eliminaron en la limpieza 2026-07-13 (modo islas es LA generación). Spawn real hoy: chunk `(0,0,0)` forzado a Sala Chica (tipo 1); `DefinirPuntoDeSpawn` busca sala aleatoria por semilla (`spawnEnVivienda` ON=vivienda, OFF=cualquiera no-cuartel), nunca en un cuartel de centinelas. Ver GDD §5 (Spawn Protegido) y §5.8 (cuartel garantizado).
*   **Generación asíncrona (`WorkerBurbuja`):** worker permanente re-apuntable (ya no coroutine-por-cruce), presupuesto por **tiempo** (`msPresupuestoBurbuja`, 5ms/frame) en vez de celdas/frame. Primero genera la sala de spawn y libera al jugador (congelado hasta tener piso); luego sigue el resto. Incremental (apaga solo lo que sale, enciende lo que entra), radio clampeado a 8. Cada celda en `try/catch`. Ver GDD §5 "Burbuja de Renderizado Asíncrona".
*   **⚠️ Tipos de Chunk desactualizados abajo — lista real (10 tipos):** 1=Sala Chica/Vivienda, 2/3/4=Pasillos (Z/X/L), 5=Intersección+, 6=Pozo, 7=Sala Grande (atrio 100×80m), 8=Intersección T, 9=Abismo Verdadero (void puro), **10=Sala Colosal** (isla entera = una sola sala, GDD §5.7, nuevo 2026-08-21). Todo corre dentro del **modo islas** (GDD §5): fuera de una isla, todo es vacío salvo puentes.
*   **Construcción Optimizada:** cada celda genera solo su piso, pared izquierda (X-) y pared trasera (Z-).
*   **Orientación por NORMAL de cara (no ángulos fijos):** todas las paredes interiores/exteriores y las aristas usan `Quaternion.LookRotation(normal, Vector3.up)`. Las interiores miran al lado vacío; las exteriores hacia afuera (`dirExterior`). Se eliminaron los flips aleatorios de 180° y las rotaciones fijas que dejaban piezas al revés.
    *   **Rotación de Techos:** Si la celda actual es sólida y la de abajo es vacía, el piso actúa como techo y se rota `180°` en el eje X local (`rot = rot * Quaternion.Euler(180f, 0f, 0f)`) para orientar la cara decorada hacia abajo.
*   **Sistema de Carteles y Avisos:**
    *   **Carteles de Coordenadas (`IntentarInstanciarCartelCoordenadas`):** Se instancian en las paredes de los pasillos. Solo se generan si la celda vacía adyacente es un pasillo interactuable (`EsPasilloInteractuable`), excluyendo salas grandes (tipo 1), pozos (tipo 6) y abismos (tipo 7). Se aplica `cartelCoordenadasOffset` (default 1.0f) para alejarlos del muro y evitar clipping. En la pared frontera (`limiteZ`), se fuerza su spawn al 100%.
        *   **El prefab DEBE tener el componente `CartelCoordenadas`** + un `TMP_Text` (o un hijo con uno). Si falta el script, los carteles aparecen con el texto cocinado del prefab y nunca corren `Start()`.
        *   El cálculo de celda de fallback (solo carteles colocados a mano) delega en `WorldGenerator.MundoACelda` — consistente con cómo el `WorldGenerator` indexa sus celdas, **incluido el offset de su transform** (antes era `FloorToInt` pelado, ver el aviso al inicio de §4). Sigue evitando el bug histórico de `RoundToInt` que reportaba la celda vecina cuando el cartel quedaba pegado al borde alto de la celda. Los carteles procedurales no pasan por acá: el generador les inyecta la celda transitable real (`usarCeldaForzada`).
    *   **Carteles de Aviso de Sala:** En la celda central de la base 3x3 de una Sala Grande (`lx == 2, ly == 1, lz == 2`), se instancian 4 carteles (uno por cara, apuntando hacia afuera) con el nombre de la sala (ej. "VIVIENDA"). El script `CartelCoordenadas` se elimina con `DestroyImmediate` para evitar que el texto sea sobreescrito con números en su método `Start()`.
*   **Decoración:**
    *   ~~Tuberías / Luces de pared~~: **eliminadas** (limpieza 2026-07-13; sobreviven las Luces de Techo y las Tuberías Modulares por conectores).
    *   **Pasarelas (`IntentarGenerarPasarela`):** ⚠️ **REHECHAS — ver GDD §5.9.** Modelo de CORRIDAS (final–recta(s)–final, corridas de 2 descartadas, piezas sueltas prohibidas) + sistema de CONECTORES: empties `Conector*` en los prefabs de terminación (fin: 1; escalera: 1 por punta) que el script alinea al borde de celda. Escaleras SOLO en extremos. L y T requieren prefab integral + conectores o no se colocan (sin fallbacks silenciosos). Regla de oro del kit: pieza de UN empalme → conector; pieza de empalme múltiple → medidas exactas contra la grilla.
    *   **Paredes Exteriores (`InstanciarParedExterior`):** arrays `prefabsParedExterior`/`prefabsPilaresParedExterior`. Offset configurable (`paredExteriorOffset`).
    *   **Pilares Entremedio:** **3 a 6** por cara, repartidos al azar en **ancho y alto** (±45%), prefab aleatorio, orientados por la normal. (Se eliminó el sistema viejo de "pilares delgados".)
    *   **Aristas Exteriores (`InstanciarAristasParaParedExterior`):** arrays `prefabsAristaExteriorTecho`/`Piso`. La de techo va en la cara superior de la pared exterior (`+hs`), la de piso en la inferior (`-hs`). Offsets de posición y rotación por inspector. **Modelar el prefab con el pivote en la base** que conecta con la pared.
    *   **Cierres de Pasillo (`IntentarGenerarCierrePasillo`):** pared de cierre al 70% en pasillos sin salida o entradas a salas.
*   **Loot y Estructuras por Sala (`RoomData.cs` + `GenerarLootDeSala`):** cada Sala Grande elige un `RoomData` del array `salasGrandesPosibles` de forma determinista.
    *   **Estructura Central:** En la celda central (`lx == 2, ly == 1, lz == 2`), se selecciona e instanciará un prefab al azar del array `prefabsCentrales` de la sala seleccionada. Si la sala no tiene datos asignados (`sala == null`), se instanciará por defecto un prefab del array `prefabsEscaleras` del generador para permitir la subida.
    *   **Loot:** Se instancian en posiciones aleatorias dentro del piso 3x3 de la sala en base a las probabilidades de `lootPosible` y el rango `cantidadLoot`.
    *   **Auto-carga (`Awake`):** si `salasGrandesPosibles` está vacío en el inspector, se rellena automáticamente con `Resources.LoadAll<RoomData>("RoomData")`. Para registrar una sala nueva basta crear el asset en `Assets/Resources/RoomData/` — no hace falta arrastrarlo al inspector.
    *   **Carteles de Nombre de Sala (bug fix histórico):** la asignación del texto (`textMesh.text = sala.nombreSala.ToUpper()`) y `ForceMeshUpdate()` ahora se hace **antes** del `SetActive(false)`, porque `GetComponentInChildren<TMP_Text>()` no encuentra componentes en objetos inactivos por default. El componente `CartelCoordenadas` se destruye con `DestroyImmediate` para que su `Start()` no sobreescriba el texto cuando la burbuja active el cartel.

*   **Balcones (Sección 5.2 del GDD):** celdas de pasillo que abren UNA cara lateral al void. `DireccionBalcon(x,y,z)` decide deterministamente la dirección entre las caras con vecino sólido. La pila completa (pared interior + `InstanciarParedExterior` + aristas) se saltea con el flag `paredXSalteadaPorBalcon` / `paredZSalteadaPorBalcon`. El `prefabBalcon` se coloca en el **borde de la celda** (no en el centro) hacia el lado abierto.

*   **Techos Exteriores (Sección 5.3 del GDD):** simétrico al de pisos exteriores. Cuando la celda actual es pasillo y la de arriba es sólida, se instancia un prefab de `prefabsTechoExterior` en `(y+1)*cellSize + paredExteriorOffset` (encima del techo). Hash determinista independiente del piso.

*   **Aristas Verticales en Esquinas de Sala (Sección 5.4 del GDD):** se generan solo en las 4 esquinas de los Macro Chunks de Sala Grande, en `ly == 1` y `ly == 3` (las salas chicas tienen 2 pisos). Posición en el cruce de las dos paredes exteriores dentro de la celda esquinera. Rotación cardinal por esquina (NW=0°, NE=+90°, SE=+180°, SW=-90°), ajustable con `yawBaseAristaVertical`. `offsetYAristaVertical` (default 10) sube el prefab para compensar pivote centrado.

*   **NUEVO 2026-08-21 — Sala Colosal (GDD §5.7):** tipo 10, isla entera = una sola sala hueca con cáscara sólida (`grosorCascaraColosal`), entrada por la lane del puente. `pilaresEnSalaColosal` (default ON) = **primer sospechoso si mirar una colosal de lejos te mata los FPS** (cientos de pilares gigantes). `cascoColosalSiempreConPielExterior` debe quedar ON o el casco sale parchado.

*   **NUEVO 2026-08-21 — Cuartel garantizado + islas sin vivienda + fix de conectividad (GDD §5.8):** cada isla no-colosal tiene EXACTAMENTE un cuartel (las RoomData de cuartel salieron de la ruleta de viviendas — antes cualquier vivienda podía salir cuartel por azar y los centinelas aparecían adentro). `probabilidadIslaSinViviendas` puebla islas puramente industriales. **Fix importante de robustez:** `ChunksConectadosDeIsla` era BFS de 6 vecinos y dejaba pasillos colgados por un enlace vertical que no genera geometría (chunk "conectado" pero flotando en el void, inalcanzable) — el BFS ahora es **solo lateral**.

*   **NUEVO 2026-08-21 — Objetos de Borde de Pasillo (GDD §5.10):** `prefabsObjetoBorde` riega props (pivote en la BASE) en la franja pegada a paredes de pasillos, por cálculo puro (sin colliders) — solo existe franja donde hay pared, así que quedan siempre del lado correcto por construcción.

*   **NUEVO 2026-08-21 — Secciones de vivienda orientadas (GDD §5.11):** las 4 secciones de esquina (agua/comida/batería/dormir) ya no usan `Quaternion.identity` — miran a uno de sus 2 lados libres (`orientarSeccionesALadoLibre`, ON default). Si tus prefabs no miran a +Z, ajustá `yawBaseSeccion`.

---

### 4.4. Sonido Ambiente Interior/Exterior (`AmbientSounds.cs`)
Crossfade automático entre `clipBase` (interior) y `clipExterior` según un `Physics.Raycast` hacia arriba desde el jugador. Si golpea algo dentro de `alturaDeteccionTecho` → "dentro"; si no → "fuera". Los volúmenes de las dos `AudioSource` hacen `Lerp` con `velocidadCrossfade` hacia el objetivo. Funciona bien con balcones, abismos, vuelos sobre estructuras y puentes. Para evitar falsos positivos por colliders chicos, filtrá `capasEstructura` a la capa de paredes/techos solamente. El `WorldGenerator` expone `TipoMacroChunkEnPosicion(Vector3)` como alternativa para detección estricta basada en el generador (no usado por default).

---

### 4.5. Presencia Sonora Orbital (`OrbitadorSonoro.cs`)
Objeto que orbita al jugador (`radio`/`altura`/`velocidadAngular`) con seguimiento suave, y cada `intervaloSonido` (rango aleatorio) reproduce un `AudioClip` al azar. Mientras suena **pausa la órbita y mantiene su posición**; al terminar, retoma. `AudioSource` en 3D (`spatialBlend = 1`) para ubicarlo por el oído. Crea tensión ambiental y se le puede seguir.

---

### 5. UI Flotante (`TooltipManager.cs`)
*   Usa un Canvas global interactivo (`TooltipGlobal`) posicionado dinámicamente en Screen Space sobre el objeto que se mira.
*   **Condición Física:** Solo se muestra si el Rigidbody del objeto tiene una velocidad lineal `< 0.1f`. Si está cayendo o rodando, el HUD se apaga por realismo.
*   **Giro Estático:** El tooltip no hereda la rotación física del objeto (mantiene `Vector3.up` y mira al jugador).
*   **Evitar Parpadeo:** Posee un retardo de ocultado de **0.2 segundos** (`tiempoOcultar`).
*   **Información de Objetos en la Mira:** Cuando el jugador no sostiene nada y la mochila está cerrada, un raycast continuo detecta si mira un `InventoryItem` y muestra su nombre y peso (ej. `Chatarra\nPeso: 1.5 KG`). Si el objeto es un anclaje colocado con cuerda desplegada, este tooltip se omite para dar prioridad al prompt `[F] Cuerda` de rapel.

---

### 6. Cierre del Ciclo Jugable: Muerte y Stats

*   **`PlayerData.Morir(causa)`** (sección 8 del GDD): idempotente, apaga input, registra stats en `EstadisticasRun`, dispara `MuerteUI`, y cambia a la escena `escenaMenu` (default `"Menu"`). El `delayMuerte` permite que `MuerteUI` haga fade-in antes del cambio.
*   **Daño por caída (`PlayerMovement`):** trackea `alturaMaximaEnAire` cada frame en aire y al aterrizar calcula `alturaMaximaEnAire - yActual`. Si supera `distanciaCaidaLetal` (25m default), llama `Morir`. Toggle `danoCaidaActivo` para testing.
*   **`EstadisticasRun.cs`** (estático): persiste entre escenas. Stats de la última run + `bibliotecaNotas` (HashSet acumulado de notas leídas en TODAS las runs). En el menú, `MenuLoreLog` itera esta biblioteca para listar títulos.
*   **`MuerteUI.cs`:** Canvas con `DontDestroyOnLoad`. Al llamar `Activar()` se despega del Player.UI (`SetParent(null)`), se marca para sobrevivir el load, pinta stats y hace fade-in. Se autodestruye con Escape en el menú.

### 7. Notas / Lore

*   **`NotaData`** (ScriptableObject `Lore/NotaData`): título, contenido (TextArea), autor.
*   **`NotaMundo`** (prefab de la nota): tiene `NotaData data` y `peso`. NO usa `InventoryItem` — el inventario no la maneja.
*   **`LectorNotas`** (en el Player): es un sistema paralelo a `ManoJugador`. Click Izq agarra una nota (raycast desde cámara), Click Der HOLD muestra el `NotaPanelUI`, **E** la guarda en `playerData.notasGuardadas` y la destruye. Click Izq UP la suelta.
*   La nota flota a `distanciaSostener` (0.6m) frente a la cámara con `offsetVertical` (-0.15m), sin rotación ni snap a mochila. Modo "se lee nomás".
*   **`NotaPanelUI`**: Canvas Screen Space del Player con 3 TMP_Text (título / contenido / autor) + Image de fondo. `Mostrar(data)` / `Ocultar()`.
*   **`PlayerData.GuardarNota(nota)`**: agrega a `notasGuardadas` (evita duplicados). Al morir, `EstadisticasRun.RegistrarMuerte` copia esta lista a `bibliotecaNotas`.

### 8. Drones del Mundo

*   **`DronVagabundo.cs`**: cada dron es independiente, sin referencia al jugador. Elige waypoints en una esfera alrededor de su propia posición (`radioVagar`/`rangoY`), con `Linecast` previo para evitar atravesar estructuras. Velocidad random por instancia (`velocidadMin..Max`). Estado `Trabajar`: ocasionalmente busca una pared cercana con `Raycast` lateral, se queda quieto orientado hacia ella unos segundos. Audio 3D (zumbido continuo + sonido de trabajo).
*   **`EnjambreDrones.cs`**: manager. Mantiene `cantidadObjetivo` drones simultáneos en una corona alrededor del jugador. Spawn solo en posiciones **realmente exterior**: `CheckSphere` para no aparecer en pared + `Raycast.up` para no tener techo encima + `Raycast.down` para no estar sobre un pasillo.

### 8.5. Centinelas — Vigilancia No Persistente (`Centinela.cs`, `RondaCentinelas.cs`, `PuntoInteresCentinela.cs`, `AlertaCentinelas.cs`)

Ver **§18 del GDD** para el detalle completo. Lo mínimo para no romperlo:

*   **`Centinela.cs`**: FSM manual (`Inspeccionando` → `Alerta` → `Buscando` → `RetirandoseABase`), mismo estilo que `DronVagabundo` — sin NavMesh, movimiento por `MoveTowards` + raycasts propios. Percepción **por tick de 0.2s, no cada frame** (cono + LOS + un radio corto que ignora ambos). Escala también por `RecibirDano(dano, origen)` público — **es el método que tiene que llamar cualquier arma nueva.**
*   **`RondaCentinelas.cs`**: manager. Rondas temporales (no población constante como `EnjambreDrones`): dispara por temporizador aleatorio **y** por `AlertaCentinelas.Disparar(pos)`. Al terminar la ronda llama `IniciarRetiro()` — los centinelas se van caminando, no se destruyen en el aire. **Ojo:** si una ronda no consigue spawnear a nadie reprograma a 2s, no al intervalo normal (si no, un fallo en el frame 0 dejaba el sistema mudo por minutos).
*   **`PuntoInteresCentinela.cs`**: se coloca a mano, se **auto-registra en una lista estática** (`OnEnable`/`OnDisable`). Tipos `Inspeccion` (patrulla) y `BaseSalida` (retiro). El manager nunca hace `FindObjectsOfType`.
*   **`AlertaCentinelas.cs`**: `static event Action<Vector3>`. Hook para sistemas futuros de ruido/luz. **Hoy nadie lo llama** — está listo, no inventado.
*   **Diseño, no bug:** que el centinela corte la persecución al salir al exterior es **deliberado** (`chanceSeguirExterior` = 0.15, sorteado por instancia al spawnear). El exterior es la válvula de escape garantizada del jugador. No lo "arregles".
*   **`modoPruebaPasillo` está ON:** es un andamio temporal — spawnea en el pasillo recto más largo activo y opcionalmente teletransporta al jugador ahí. Se apaga por inspector cuando haya `PuntoInteresCentinela` colocados de verdad.
*   **⚠️ NUEVO 2026-08-21 (GDD §18.46):** un cuartel garantizado y único por isla (ver §4 más arriba). `TryEncontrarSalaCentinela` se reescribió con **cáscaras concéntricas** (`radioBusquedaCuartelMacros` pasó de costo fijo a TECHO — subilo a 8-15, ya no cuesta si hay uno cerca). `Centinela.patrullarIslaCompleta` (ON default): destinos al azar en toda SU isla vía `WorldGenerator.TryElegirCeldaPatrullaEnIsla`, caminados por el mismo BFS de celdas que el retiro — nunca cruza a otra isla.

### 9. Desprendimientos (`SistemaDesprendimientos.cs`)

*   Cada `intervaloMin..Max` segundos (20-60), llama a `WorldGenerator.TomarPiezaExteriorAleatoria(centro, radio, palabrasClave)` que **itera `mundoChunks`** en el radio, filtra por nombre (default: `_externo`, `ParedExterior`, `Pilar`, `AristaExterior`), elige uno y lo **desregistra del chunk** (la burbuja deja de gestionarlo) + lo despega del transform del WG.
*   El script le mete `Rigidbody` + `Collider`, le aplica `linearVelocity` random horizontal (Y entre -0.3 y 0 — NUNCA hacia arriba) y `angularVelocity` random para que rote.
*   `Destroy(pieza, tiempoVida)` después de 5s.
*   La pieza no se regenera (el chunk piensa que sigue ahí). Suficiente para el demo.
*   **⚠️ Fix 2026-08-21 (GDD §11) — "siempre caía algo al iniciar partida":** el reloj arrancaba con la escena, así que el tiempo de menú/intro ya lo dejaba vencido. Ahora no corre hasta `WorldGenerator.MundoListo` + gracia inicial (`esperaInicialMin/Max`, 45-150s) + `probabilidadDesprendimiento` (0.6) por turno para romper el ritmo de metrónomo.

### 10. Mira con Peso (`MouseLook.suavizadoMira`)

Filtro pasa-bajos sobre el input del mouse. `mouseSuave = Lerp(mouseSuave, raw, k)` con `k = 1 - exp(-Δt / suavizadoMira)`. Default 0.05s — peso del casco perceptible sin lag. Range [0, 0.25].

### 11. Grain por UI (`GrainUI.cs`)

Toggle un `Volume` (con Film Grain override de HDRP) según si alguna UI está activa. Vigila un array `uisAVigilar` — si CUALQUIERA está `activeInHierarchy`, prende el grain. Sin UI → grain off.

### 11.5. Tirolina (`SistemaTirolina.cs`, `Tirolina.cs`) — GDD §17, actualizado 2026-08-21
Grappling gun de 2 disparos físicos (no raycast): 1er proyectil ancla el destino, 2do ancla tu extremo, se arma cuerda tensa. Munición: **fallar no gasta, 2 fallos seguidos sí** (no es cargador N-balas — `fallosConsecutivos`, resetea al clavar). `iconosMunicion` (array de GameObjects) refleja ese margen, no "disparos hechos".
*   **Control manual en la cuerda (nuevo):** W/S con signo a lo largo de la cuerda (`Input.GetAxisRaw("Vertical")` × `aceleracionEntrada`) + gravedad por pendiente + rozamiento sin input. Antes `rideVel` estaba clampeada solo positiva → imposible frenar o volver. El mouse ya giraba libre (nada lo tocaba).
*   **Enganche donde agarrás (nuevo):** usa el `hit.point` del raycast, proyectado sobre el segmento de la cuerda — antes `rideT=0` te teletransportaba siempre al extremo cercano sin importar dónde apuntaras.

### 12. Spawn Determinista

`DefinirPuntoDeSpawn` ahora usa `Random.InitState(semillaDelMundo + 7777)` (en vez de `DateTime.Now.Ticks`). Con `usarSemillaAleatoria=false` y una semilla fija → siempre spawneas en la MISMA sala. Útil para tests y para garantizar consistencia del demo.

---

## 🎯 Atajos de Teclas para Probar en el Editor
*   `Q`: Abrir / Cerrar la mochila (`ControladorMochila`).
*   `Mantener Click Izquierdo`: Sostener el objeto apuntado (como apretar la mano). Soltar el click = soltar con inercia.
*   `Click Derecho` (mochila abierta, objeto sostenido): Rotar el objeto 90°.
*   `Rueda del ratón` (mochila abierta, objeto sostenido): Acercar / alejar el objeto.
*   `Mantener Click Izquierdo + Mantener Click Derecho` (con anclaje en mano): Activa la vista previa flotante del anclaje a 1.5m enfrente.
*   `Soltar Click Derecho` (en vista previa de anclaje): Confirma la colocación (cae verticalmente al suelo y despliega la cuerda).
*   `Soltar Click Izquierdo` (en vista previa de anclaje): Cancela la vista previa y suelta el objeto con física de gravedad.
*   `F`: Interactuar con cuerdas de rapel / anclajes (solo si tienen la cuerda desplegada).
*   `Espacio`: Mantener para volar. En cuerda de rapel, mantener 2 segundos para soltarse. En escalera, pulsar para soltarse.

> Nota: las mecánicas antiguas de consumir (`E`), lanzamiento cargado y linterna pertenecían al `InventarioSystem` eliminado; se reintegrarán sobre el nuevo sistema cuando corresponda.
