# Game Design Document (GDD) - In Side Us

## Resumen del Proyecto
**In Side Us** es un juego que combina mecánicas de supervivencia, escalada/rapel, exploración de laberintos procedimentales y gestión de inventario, enfocado en una atmósfera opresiva. Toda la interfaz (HUD) se basa en la idea de que "El Tiempo es tu Mayor Enemigo", sustituyendo los porcentajes de recursos tradicionales por contadores temporales regresivos.

---

## 1. Sistema de Supervivencia y Estado (`PlayerData.cs`)
El estado del jugador se gestiona a través de temporizadores que simulan el soporte de vida de su traje espacial.

*   **Estadísticas Principales (Temporizadores Regresivos):**
    *   **Agua (`waterSeconds`), Comida (`foodSeconds`) y Batería (`energySeconds`):** duración máxima por defecto de **600 segundos (10 minutos)** cada una. Las tres drenan **al mismo ritmo: 1s por cada segundo real** (antes comida iba a 0.4× y batería a 3×; se emparejaron por diseño). El contador **Despierto** (`horasDespierto`) también avanza en **tiempo real** (desacoplado del `multiplicadorTiempo` del reloj del mundo), de modo que los 4 contadores del HUD corren parejos.
*   **Mecánica de Fallo de Interfaz / HUD:** el HUD del casco se compone de dos referencias:
    *   **`cascoUI` (CanvasGroup, opcional):** para elementos UI dentro de un Canvas — su opacidad se modula.
    *   **`hudContenedor` (GameObject, requerido para HUD con TMP 3D):** un GameObject vacío que contiene los textos flotantes del casco (Agua, Comida, Sueño, Energía, Coordenadas, Peso). Como los TMP 3D no son afectados por CanvasGroup, el parpadeo y apagado se hacen con `SetActive`.
    *   **Batería > 15% (de `maxEnergySeconds`):** el HUD se renderiza estable. `cascoUI.alpha` disminuye linealmente de `1.0` a `0.2` a medida que se acerca al umbral del 15%. `hudContenedor` siempre activo.
    *   **Batería <= 15% y > 0%:** el HUD entra en glitch. Cada frame: `cascoUI.alpha = Random.value > 0.4f ? 0.3f : 0.05f` Y `hudContenedor.SetActive(Random.value > 0.4f)` — el contenedor parpadea on/off ~60/40%, generando un cortocircuito visual fuerte (los TMP 3D aparecen y desaparecen).
    *   **Batería == 0%:** el HUD se apaga por completo (`cascoUI.alpha = 0f`, `hudContenedor.SetActive(false)`). Esto también dispara el `GrainUI` (si lo vigila) → el grain se apaga y el jugador ve nítido sin pantalla, refortzando la sensación de "casco muerto".
*   **Tiempo, Calendario y Fatiga:**
    *   **Día de 40 Horas:** Los días del planeta están configurados para durar **40 horas** (`horasPorDia = 40`).
    *   **Paso de Tiempo Acelerado:** El paso de tiempo progresa a un ritmo de **60 minutos de juego por cada segundo real** (`multiplicadorTiempo = 60f`).
    *   **Cansancio (`horasDespierto`):** Se incrementa pasivamente con el paso del tiempo en el juego. Se muestra en el HUD con formato `d'd' HH:MM`.
    *   **Calendario Simplificado:** Registra el "Día del Año" (1 al 365) y el "Año" (iniciando en 2084), omitiendo los meses.
*   **Carga Física (`pesoTotal`):**
    *   Se **recalcula cada frame** en `PlayerData.RecalcularPesoTotal()` desde 3 fuentes: (1) items guardados en la mochila (`MochilaGrid.PesoTotalItemsGuardados()` — deduplica por instancia porque un item ocupa varias celdas), (2) item sostenido en la mano (`ManoJugador.ItemEnMano`), (3) nota sostenida (`LectorNotas.NotaSostenida`). **Fuente única de la verdad**: ningún otro script debe tocar `pesoTotal` directamente, cualquier suma/resta manual se pisa con el próximo recálculo. Afecta la capacidad de salto/vuelo del jugador.
    *   HUD: `txtPesoTotal` muestra `X.X kg`; `sliderPeso` muestra `pesoTotal / maxPesoTotal` (default 50 kg). Los campos `txtClima` y `txtTiempo` del HUD se eliminaron (nunca se pintaban); `climaActual` y el calendario (`horaActual/diaDelAnio/etc`) siguen como **estado interno** porque los consumen `ControladorClima` y `EstadisticasRun.diasSobrevividos` respectivamente.
*   **Popups de cambio puntual (`TextoFlotanteStat.cs`):** un TMP_Text chico junto a cada stat (Agua/Comida/Sueño/Batería — **Peso no tiene**) que muestra `+Xs` (verde) o `-Xs` (rojo) y se desvanece solo. **No reacciona al drenaje pasivo por frame** (si no, parpadearía constantemente con `-0.02s`) — solo se dispara con una llamada explícita (`Mostrar(delta)`), nunca comparando el valor cada frame. `PlayerData` expone 4 métodos wrapper (`MostrarPopupAgua/Comida/Sueno/Energia`) para que cualquier script lo dispare sin conocer el componente. Conectado hoy en: `ConsumibleData.Usar()` (positivo, al consumir un item) y `SistemaTirolina.Disparar()` (negativo, al gastar batería por disparo — ver §17). `desplazamientoY = 0` en el Inspector lo deja fijo en su lugar (sin subir mientras se desvanece) — así se usó en la práctica, integrado directo en la interfaz de stats en vez de cómo popup flotante que se mueve.

---

## 2. Mochila Modular (estilo Lego/Trailmakers, plano 2D)

El inventario es una **mochila construible**: una base fija a la que se le pegan módulos para ampliar su espacio, todo en 3D real y sin pausar el juego. La clave del diseño es que **todo es lógica de celdas pura** (matemática de coordenadas), sin colliders-trigger ni anclajes físicos — eso lo hace robusto.

### Datos de ítems
*   **`ItemData.cs`** (ScriptableObject `Inventario/Item`): `nombreArticulo`, `tipoMaterial`, `peso` (kg), `width`/`height` en celdas, `icon` y método virtual `Usar(PlayerData)`.
*   **`ConsumibleData.cs`**: hereda de `ItemData`; añade `restaurarComida/Agua/Bateria/Sueno` (en **segundos**) y sobreescribe `Usar()`.
*   **`ItemData.DescripcionTooltip()` (virtual):** texto que muestra `InfoObjetoUI` al apuntar/inspeccionar un objeto **ya descubierto**. La base da `nombre + peso`; `ConsumibleData` lo sobreescribe agregando las estadísticas que aporta (`Comida +m:ss`, `Agua +m:ss`, `Batería +m:ss`, `Sueño +m:ss`, y `Porciones: N` si es fraccionable), formateadas con el helper `FormatoSegundos` (mismo estilo m:ss del HUD). Cada tipo de item se describe a sí mismo — para un item nuevo con stats propias, se sobreescribe este método en su clase, sin tocar `ManoJugador`.
*   **`ModuloData.cs`**: hereda de `ItemData` (un módulo **es** un item, se recoge del mundo). Añade `forma` (lista de `Vector2Int` con las celdas que ocupa, relativas a su origen 0,0) y `prefabVisual`. La forma se edita visualmente con `ModuloDataEditor` (grilla de cuadritos clicables en el inspector; verde = celda ocupada, ◎ = origen).
*   **`InventoryItem.cs`**: componente sobre el GameObject físico del ítem. Referencia un `ItemData`, lleva `rotacion` (0..3 pasos de 90°), `isRotated`/`Width`/`Height` derivados, `gridX/gridY` (dónde se pegó si es módulo) y `escalaMundialOriginal` (para mantener tamaño constante entre reparents). En `Awake` captura su escala; en `Start` ignora colisiones con el jugador.

### Núcleo lógico (`MochilaGrid.cs`)
*   Mantiene un `HashSet<Vector2Int>` con las celdas que **existen** (base 2×2 + módulos pegados). No dibuja nada.
*   `PuedeColocarModulo(forma, origen)`: válido si ninguna celda del módulo pisa una existente Y al menos una toca una existente por un lado ortogonal (**sin diagonales**).
*   `ColocarModulo` / `QuitarModulo`: agregan/quitan celdas (la base 2×2 nunca se borra).
*   `AnclajesLibres()`: calcula dinámicamente los puntos de enganche = celdas vacías ortogonalmente adyacentes a una existente (para el 2×2 base son 8).

### Visual (`MochilaVisual.cs`)
*   Dibuja con Gizmos las celdas existentes (verde sólido) y los anclajes libres (amarillo wireframe) en el plano local XY de la mochila. Sin Play muestra un preview de la base. `tamanoCelda` ajusta el tamaño.

### Apertura de la mochila (`ControladorMochila.cs`)
*   La mochila sigue suavemente (`Lerp` exponencial) la posición `MochilaPos.Guardada` (espalda) por defecto.
*   **Tab** alterna a `MochilaPos.Abierta` (frente a la cámara) y de vuelta. Al abrir libera el cursor y bloquea la cámara (`MouseLook.canLook`); al cerrar lo revierte. *(Antes era Q — se liberó esa tecla para la linterna.)*
*   **Rotación manual (Click Der mantenido):** con la mochila abierta y **sin item en mano** (chequea `ManoJugador.ItemEnMano`, referencia en el inspector), arrastrar con click derecho rota la mochila **solo en Y** (`rotacionExtra`, sensibilidad `sensibilidadRotacion`). La rotación **se mantiene al soltar** y recién vuelve a la original (Slerp `velocidadRetorno`) al **cerrar** la mochila. Con item en mano, el click derecho sigue rotando el item 90° (sin conflicto).

### Items guardados en celdas (`PuedeColocarItem`/`ColocarItem`/`QuitarItem`)
A diferencia de los módulos (que **agregan** celdas), los items normales **ocupan** celdas que ya existen, estilo RE4. `MochilaGrid` mantiene un `Dictionary<Vector2Int, InventoryItem>` de celdas ocupadas. Un item de `Width`×`Height` solo se guarda si todas sus celdas existen y están libres. Agarrarlo de nuevo libera sus celdas (`QuitarItem`).

### Mano y construcción (`ManoJugador.cs`)
*   **Mantener Click Izquierdo** = sostener el objeto apuntado (como apretar la mano); **soltar** = soltar con la inercia del movimiento (interactivo). Raycast desde el centro de la pantalla con la mochila cerrada, desde el **cursor** con la mochila abierta.
*   **Inspección / Análisis (Click Der mantenido, objeto normal en mano, mochila cerrada):** el objeto se despega de la mano y **flota en el centro de la mira** (`distanciaInspeccionObjeto`), sus colliders pasan a trigger y se **bloquea `MouseLook`** para que el mouse **rote el objeto** en vez de la cámara (rotación libre acumulada alrededor de los ejes de la cámara, `velocidadRotacionInspeccion`). La **rueda** acerca/aleja el objeto (`velocidadZoomInspeccion`, clampeado entre `distanciaInspeccionMin/Max`). Si lo acercás más que `distanciaOcultarInfo` (default 0.5), el `InfoObjetoUI` se oculta para ver el objeto sin el panel encima. Inspeccionar **descubre** el objeto (lo registra en el codex, ver §9.6) y muestra sus datos en `InfoObjetoUI` mientras lo girás. **Soltar Click Der** → vuelve a la mano. **Soltar Click Izq** → se cae (con limpieza de colliders/cámara vía `Soltar`). Es análogo al preview del anclaje (`PilarRapel`), pero para analizar cualquier ítem — los anclajes siguen usando Click Der para su propio preview de colocación (mutuamente excluyentes: `esAnclajeMano` gatea).
*   **Mochila abierta + objeto sostenido:** el objeto flota en un plano 2D frente a la cámara siguiendo el cursor; la **rueda del ratón** lo acerca/aleja (`distanciaInspeccion`).
*   **Click Derecho** (mochila abierta) rota el objeto 90° (los 4 pasos, `rotacion` 0..3); rota tanto el mesh como la **forma lógica** del módulo/rectángulo del item.
*   **Snap de módulos:** al acercar un módulo a un borde válido, se alinea a la grilla (centrado por el **centroide de su forma**). Al soltar sobre un snap válido se **pega**: sus celdas se agregan al grid.
*   **Snap de items:** al acercar un item a celdas existentes y libres, hace snap (centrado por `CentroRectLocal`). Al soltar, ocupa esas celdas.
*   **Despegar:** agarrar (con el cursor) un módulo/item ya pegado quita/libera sus celdas y vuelve a la mano.

### Escala consistente (clave: la mochila tiene escala NO uniforme)
La mochila suele estar aplastada en Z, lo que rompe el escalado de los hijos rotados. Por eso `ManoJugador` calcula la escala leyendo el `lossyScale` del **padre** (no del objeto rotado, que Unity aproxima mal) en dos variantes:
*   **`FijarEscalaUniformeEnPadre`** (módulos): escala uniforme → quedan planos contra la mochila, sin deformarse al rotar.
*   **`FijarEscalaMundialEnPadre`** (items): compensa por eje para conservar la **proporción real** (no se achatan en Z). Como el grid es cuadrado en X/Y, rotar no produce shear.
*   `escalaMundialOriginal` se captura en `InventoryItem.Awake` y se reusa en cada reparent (mano / inspección 2D / pegado / soltado), evitando el bug de "crece cada vez".

### Origen de los módulos
Los módulos se **encuentran** en el mundo (como items) y/o se **craftean** (a futuro). En ambos casos terminan siendo un `ModuloData` que se pega a la mochila con el mismo flujo.

### Jerarquía y calibración de la grilla (evitar desfases)
La grilla es **lógica pura**: `MochilaGrid`/`MochilaVisual` no dependen del mesh. Toda su "memoria" son `tamanoBase`, `tamanoCelda`, `offsetLocalCeldas` y el transform del empty que los porta. El gizmo verde de `MochilaVisual` es **solo debug** (no existe en la build, no dicta gameplay) — sirve para ver dónde caen las celdas lógicas.

*   **Jerarquía robusta:** un empty raíz `MochilaRoot` (es lo que mueve `ControladorMochila.mochila` al abrir/cerrar) con dos hijos hermanos: `MochilaMesh` (el FBX visual) y `Grid` (empty con `MochilaGrid`+`MochilaVisual`; los items guardados se parentan acá). **El `Grid` nunca debe colgar del FBX importado:** si cuelga del mesh, al reimportar o aplicar transforms el pivote del FBX se mueve y arrastra la grilla, desfasándola. Como hermanos bajo `MochilaRoot`, reimportar el mesh no toca la grilla.
*   **Calibración:** `offsetLocalCeldas` (en celdas) y/o la posición del empty `Grid` anclan la matemática al mundo; ambos mueven **gizmo + snap juntos** (nunca divergen entre sí). Con el pivote del mesh centrado, `offsetLocalCeldas = (-1, -1)` centra la base 2×2 con el `Grid` en `(0,0,0)`. `tamanoCelda` = distancia real entre centros de celdas.
*   **Item/mesh que cae corrido ~medio cuadro:** casi siempre es el **pivote del prefab** (origin descentrado en Blender), NO la grilla — mover el `offsetLocalCeldas` no lo corrige porque desplaza celdas y snap juntos, dejando el error relativo constante. Solución: centrar el origin del prefab en Blender (Set Origin → Origin to Geometry), o compensar con `offsetVisualCeldas` en su `InventoryItem`.
*   **Regla de oro:** construir/calibrar el tooling (grid, sockets) **después** de aplicar transforms en Blender, sobre el asset final; y al reeditar el mesh **no mover el origin**.

> [!NOTE]
> Pendiente: **crafteo** de módulos y un **preview verde/rojo** explícito durante el arrastre. Para módulos multi-celda, modelar el prefab con el **pivote/origin en el centro** (Blender: Set Origin → Origin to Geometry) para que calce con la grilla.
>
> **Bug latente conocido:** la compensación de centrado por malla (`ManoJugador.ObtenerOffsetCentroMesh`) mezcla el espacio local del item con el del grid, así que solo sale exacta si la escala mundial del item == la del grid. Un prefab con pivote descentrado **y** escala distinta a la del grid se desfasa ~medio cuadro. Fix pendiente: recalcular el centrado en espacio mundo (helper `CentrarMeshEn`) para volverlo inmune a escala/pivote.

---

## 3. Movimiento y Físicas (`PlayerMovement.cs`, `MouseLook.cs`)
*   **Movimiento en Tierra:** Movimiento en primera persona con aceleración y desaceleración progresiva cuando se activa el sprint (**Left Shift**).
*   **Mecánica de Vuelo (Space to Fly):**
    *   El salto tradicional se sustituye por una mecánica de vuelo ascendente.
    *   Mantener presionado **Espacio** eleva al jugador con una velocidad de ascenso constante de **8 unidades/s** (`velocidadVueloAscenso`).
    *   Al soltar **Espacio**, el jugador desciende bajo una caída acelerada por gravedad, limitada a una velocidad máxima de caída de **-15 unidades/s** (`velocidadVueloCaida`).
    *   *Factor de Carga:* El peso total en el inventario reduce proporcionalmente la fuerza de salto básica si se aplica en tierra.

---

## 4. Sistema de Rapel y Escalera (`SistemaRapel.cs`, `PilarRapel.cs`, `SistemaEscalera.cs`, `Escalera.cs`)
Mecánicas clave para el desplazamiento y la exploración de la infraestructura vertical.

*   **Rapel (`SistemaRapel.cs`, `PilarRapel.cs`):**
    *   **Anclaje (`PilarRapel`):** Contiene un punto de enganche y un punto de llegada para desembarcar en la cornisa. Dispone de un `LineRenderer` y un collider dinámico generado a lo largo de la cuerda (`ColliderCuerdaFisica`) que permite interactuar con ella a cualquier altura.
    *   **Visual de la cuerda:** el `LineRenderer` necesita un material HDRP-compatible o se ve invisible/magenta. Campos en `PilarRapel`: `materialCuerda` (si se deja vacío se crea un `HDRP/Unlit` en runtime como fallback), `anchoCuerda` (default 0.05m) y `colorCuerda` (HDR). Ojo: el color del LineRenderer puede no teñir el material HDRP — el color/glow real se controla con la **Emission del material** asignado.
    *   **Mecánica de Colocación de Anclaje desde la Mano:**
        *   Cuando el jugador sostiene el anclaje con Click Izquierdo y mantiene presionado Click Derecho, se activa el **modo de vista previa**.
        *   El anclaje se desasocia del socket de la mano y flota de forma constante a **1.5 metros** enfrente de la cámara sin colisionar físicamente con el entorno (sus colisionadores se vuelven triggers, ignorando el de la cuerda).
        *   Al soltar el Click Derecho, se realiza un raycast vertical hacia abajo desde el preview. Si hay suelo en un rango de 10 metros, se coloca el anclaje en el punto de impacto, desactivando los triggers, haciéndose kinematic y desplegando la cuerda.
        *   Si no se detecta suelo debajo de la posición flotante, o si se suelta el Click Izquierdo, la colocación se cancela y el objeto regresa a la mano (o cae físicamente al suelo según el caso).
        *   **Recogida:** Al levantar un anclaje colocado (Click Izquierdo), la cuerda física y visual se oculta instantáneamente llamando a `pilar.MostrarCuerda(false)`.
    *   **Enganche:** Apuntar a la cuerda o al anclaje a menos de 5 metros y presionar **E** (antes F — se liberó F para la linterna, ver §9.55a) engancha al jugador en la altura correspondiente. Solo se permite si el anclaje tiene la cuerda desplegada.
    *   **Controles en Cuerda:**
        *   **W / S:** Subir o bajar por la cuerda a una velocidad constante de **4 unidades/s** (`velocidadBajada`).
        *   La longitud de la cuerda está limitada a **100 metros**.
        *   El jugador queda alineado estrictamente en las coordenadas X/Z de la cuerda para evitar atravesar la arquitectura ("Modo Tieso").
        *   **Click Derecho (Desembarcar):** Si el jugador está en la parte superior (a menos de 1.5 metros del anclaje), desembarca de forma segura en la cornisa (`puntoLlegada`).
        *   **Espacio (Soltarse):** Mantener presionado **Espacio** durante 2 segundos desengancha al jugador, dejándolo caer libremente.
*   **Escaleras de Mano (`SistemaEscalera.cs`, `Escalera.cs`):**
    *   Detección mediante triggers. Al entrar en el volumen de una escalera, el jugador se engancha de forma automática.
    *   **W / S:** Sube y baja de forma clampada entre el `puntoBase` y el `puntoTop` de la escalera a **3 unidades/s** (`velocidadEscalada`).
    *   Durante la escalada, el script `PlayerMovement` bloquea los controles de movimiento horizontal (`movementLocked = true`) y desactiva las físicas normales (`isKinematic = true`).
    *   **Click Derecho (Salir por arriba):** Si se está cerca de la cima (a menos de 1.2m del tope), hacer click derecho posiciona al jugador en el punto superior de salida y libera la escalera.
    *   **Espacio:** Presionar espacio hace que el jugador se suelte de la escalera y caiga inmediatamente.

---

## 5. Generación Procedimental (`WorldGenerator.cs`)
El mapa es infinito y se autogenera en tiempo de ejecución alrededor del jugador dividiendo el espacio en Macro Chunks de tamaño **5x5x5 celdas** (donde cada celda mide **20 unidades de lado**).

*   **Algoritmo de Densidad:** Utiliza una interpolación tridimensional basada en 3 planos de ruido Perlin (`ObtenerDensidad`) para determinar la estructura de cada Macro Chunk.
*   **Cachés de generación (optimización):** `ObtenerTipoMacroChunk`, `IsCellEmpty`, `DireccionBalcon` y `HabraCierreEn` son **funciones puras una vez fijada la semilla/offsets en `Start()`**, pero se llaman cientos de veces por celda (decoradores, predictores por hash, chequeos de vecinos) pagando Perlin + `Random.InitState` en cada llamada. Cada una está envuelta en un caché `Dictionary<Vector3Int, T>` (patrón wrapper → `Calcular*`): primera llamada calcula y guarda, las siguientes son un lookup. **No cambia ni un bit del resultado** (mismo determinismo por semilla). Los cachés se limpian en `Start()` tras fijar semilla/offsets. Los toggles de inspector (`generarBalcones`, `generarCierresPasillos`) se chequean **fuera** del caché para seguir respondiendo en runtime. Si se agrega un sistema nuevo con decisión determinista consultada repetidamente, seguir este mismo patrón.
*   **Tipos de Macro Chunk** (nomenclatura actualizada):
    *   *Densidad Alta (> 0.55):* Complejos Industriales habitables. Contienen **Sala Chica / Vivienda (Tipo 1)** — 3×3 habitable con loot, **Intersección + (Tipo 5)**, **Pozo Vertical (Tipo 6)** e **Intersección T (Tipo 8)**.
    *   *Densidad Media (0.30 - 0.55):* Pasillos y Caminos. Contienen **Pasillo Z (Tipo 2)**, **Pasillo X (Tipo 3)** y **Pasillo L (Tipo 4)**.
    *   *Densidad Baja (0.20 - 0.30):* **Sala Grande (Tipo 7)** — atrio gigante de 100×100×80m con **piso sólido continuo** en `ly==0` y **pilares gigantes** en las 4 esquinas. Acepta entradas de pasillos en las 4 direcciones a `ly==1` (HasConnection devuelve true). Antes era llamada "Abismo" pero visualmente es una sala monumental.
    *   *Densidad Muy Baja (< 0.20):* **Abismo Verdadero (Tipo 9)** — void puro, literalmente nada. Sin piso, sin paredes, sin pilares, sin conexiones. Solo se accede volando. Es el hábitat natural de los drones (`EnjambreDrones` lo detecta con raycast como "exterior").
    *   **CONECTIVIDAD SIMÉTRICA (fix estructural):** el tallado de conectores entre chunks era unilateral (cada chunk abría su franja solo si el *vecino* conectaba hacia él) → un pasillo perpendicular a una vivienda dejaba una **rama muerta** contra la pared cerrada de la sala, y la vivienda quedaba **inaccesible salvo volando**. Ahora el paso se talla si **cualquiera** de los dos conecta hacia el otro (`A→B || B→A`) — como ambos chunks evalúan la misma condición, o tallan los dos o ninguno: pasos completos, sin ramas muertas, toda sala/vivienda enterable. **Guard:** el lado propio solo cuenta si el vecino es estructura (1-6,8) — hacia atrios (7) decide el vecino como siempre, y hacia el vacío (9) **nunca** se talla (si no, las salas del borde de una isla se abrirían boquetes al vacío).
    *   **Puertas en bocas al vacío (`prefabsPuertaBocaVacio`):** cuando una boca de pasillo da al Abismo (9), además del marco de término se instala una compuerta del array (en el plano de la cara, mirando hacia afuera) — salir del casco de la isla es deliberado. Bocas a Sala Grande (7, atrios interiores) siguen abiertas. Array vacío = comportamiento anterior.
    *   **Umbrales de densidad expuestos (`umbralDensidadAlta/Media/Baja`, defaults 0.55/0.30/0.20):** el tipado por densidad ahora es configurable. Subir `umbralDensidadMedia` (ej. a 0.40) = interiores menos claustrofóbicos: más atrios (7) y huecos dentro de las islas.
    *   ***MODO BLAME!* (`modoIslas`, default ON — sección "Modo BLAME!" del inspector):** reemplaza el mundo denso continuo por **islas de estructura separadas por vacío inmenso**, conectadas por puentes/pasillos larguísimos. Grilla de regiones de `periodoIslas`³ chunks (default 10 = ~1000m): **cada región tiene SIEMPRE una isla** (`CentroIsla`: radio `radioIslaChunks` 1-2 chunks, forma caja o esfera 50/50, jitter X/Z con margen para quedar contenida en su región → `EnIsla` es O(1)). Verticalmente la isla **apoya su base en el nivel inferior de la región** (`cy = ry*p + radio`) para que los puentes, que corren en `my = ry*p`, toquen la base de toda isla sin importar su radio. **Dentro de la isla** rige el tipado por densidad clásico; **fuera**, todo es tipo 9 salvo los puentes. **Puentes (`TipoPuente`):** cada región decide por hash si conecta con su vecina +X y +Z (`probabilidadPuente`, default 0.75 — "la mayoría de las veces... a veces tenés suerte"); el camino es una **L** (X primero, Z después) de chunks pasillo forzados (3 a lo largo de X, 2 a lo largo de Z, 5 en esquinas/superposiciones), de ~7-9 chunks = **700-900m de pasillo recto rodeado de nada**. La región (0,0,0) es especial: isla caja de radio máximo con el **chunk de spawn (0,0,0) en su cara inferior**, al nivel de los puentes. Los vacíos tallados se omiten en este modo (redundantes y podrían partir una isla). Apagar `modoIslas` restaura el mundo denso clásico.
        *   **SIN PAREDES CONTENEDORAS (`CaraDaAlVacio`):** en modo islas, toda cara cuyo **lado vacío es tipo 9** se salta COMPLETA en la generación — aplica a las **6 caras**: paredes X y Z (interior + exterior + aristas + carteles), **piso Y** (losa interior + `InstanciarPisoExterior` con sus pilares) y **techo exterior** (`InstanciarTechoExterior`). El void no recibe NINGUNA superficie: ni paredes, ni techos, ni pisos — solo existen las estructuras (tipos 1-8) vestidas por su propia piel (sus caras dan a espacios caminables, que sí generan). Nunca te encontrás "otra sala gigante" al salir de un pasillo, y no hay losas solapadas entre tubos apilados. Consecuencia deliberada: las masas ya no tienen techo caminable por encima — se camina DENTRO de las estructuras, el resto es abismo. Los puentes se ven como **tubos finos de 20m** cruzando la nada.
        *   **BORDE DE ISLA = SOLO PASILLOS (`ChunkTocaVoid`):** los chunks de estructura con algún vecino void (6 direcciones) se remapean a **pasillo (2/3/4 por hash)** — las salas/cruces/pozos por densidad quedan reservados al **núcleo** de la isla, siempre bien cerrados. Motivo: al no generarse paredes hacia el void, una sala en el borde quedaba "destapada" en corte de maqueta contra el vacío. El chequeo del vecino replica su decisión con la cadena candidato+conectividad+lane (funciones puras) — NUNCA consultando su tipo final, que causaría recursión infinita entre vecinos; es exacto porque el remapeo de borde no cambia la void-ness (solo sala→pasillo).
        *   **PODA DE CONECTIVIDAD (`ChunksConectadosDeIsla`):** la porosidad puede dejar chunks de estructura **aislados** (todos sus vecinos se volvieron void) → pedazos de pasillo flotando sueltos. Se poda con un **BFS por isla** (cacheado por región, corre 1 vez): desde la columna-pozo se expande por 6-vecinos a través de candidatos a estructura (`EsCandidatoEstructura` — réplica ligera del branch: columna/lane/porosidad, SIN pasar por `ObtenerTipoMacroChunk` para evitar recursión); todo candidato fuera del set conectado → tipo 9. Garantiza coherencia visual: toda estructura cuelga del esqueleto de su isla.
        *   **POROSIDAD de islas (`umbralVacioIsla`, default 0.45 — reemplaza al "casco invertido", DESCARTADO):** las islas **no son bloques macizos encapsulados**: los chunks con densidad bajo el umbral son **vacío también dentro de la isla**. Resultado: cada isla es un **enjambre de estructuras** (tubos, salas, cruces) expuestas al void, cada una con su propia piel clásica (mismo look que los tubos de puente) — sin paredes contenedoras gigantes ni "contorno de caja". El esqueleto conectado se mantiene por los forzados: columna-pozo central + lanes de puente. `umbralVacioIsla = 0` restaura la isla maciza. *(Historia: se intentó un "casco invertido" — placa exterior girada hacia el vacío cubriendo todo el bloque — en 3 iteraciones (girar sin mover / mover sin girar / ambos+pilares+cobertura total); se descartó porque los prefabs de pared exterior están diseñados para verse desde los espacios caminables y el bloque encapsulado en sí perturbaba. La respuesta correcta fue eliminar el bloque, no forrarlo.)*
        *   **⚠️ MODO DENSO ELIMINADO (limpieza 2026-07-13):** las islas son **LA** generación — se borró del código: el toggle `modoIslas`, la **Gran Pared** completa (`generarParedFrontera`, `limiteZ`, muralla, viento de muralla en `AmbientSounds` → reconvertido a "viento del vacío" cuando el jugador está expuesto sin techo), los **Vacíos tallados** (`EstaEnVacio` y sus 4 campos), el **spawn por búsqueda de sala** (`spawnEnSalaAleatoria` y sus rangos), las **Tuberías procedurales** y las **Luces de pared** (las luces de techo se conservan). Además, regla de inspector: **si un sistema tiene slider de probabilidad, no tiene toggle** (prob 0 = apagado) — se eliminaron `generarParedesExteriores/PisosExteriores/TechosExteriores/Pasarelas/CierresPasillos/Balcones`.
        *   **Clamp del radio de burbuja:** `WorkerBurbuja` clampa el radio efectivo a **8** (el costo crece al cubo: radio 20 = 68.921 celdas y el worker se ahoga reconstruyendo sets en cada cruce en vez de generar). Para ver islas lejanas la solución futura son impostores/siluetas, no subir el radio.
        *   **Garantías de conectividad (toda isla tiene salida SIEMPRE):** (1) los dos rolls de puente (+X/+Z) de una región se deciden juntos y si **ambos** fallan se fuerza el de +X (`PuentesDeRegion`) — sin islas huérfanas; (2) las **lanes de puente atraviesan la isla** a nivel base hasta su columna central (el puente no muere en el casco, desemboca en el corazón de la isla); (3) la **columna central de cada isla se fuerza a Pozo vertical (tipo 6)** — el "shaft" del monolito: conecta todos los niveles con el nivel base donde corren los puentes (desde cualquier piso: encontrar la columna → bajar → salir); (4) el **spawn es SIEMPRE el chunk (0,0,0)** (`DefinirPuntoDeSpawn`): el jugador arranca al nivel de los puentes, sobre la lane principal de la isla del origen.
    *   *Spawn Protegido:* El Macro Chunk del centro (0, 0, 0) está forzado a ser tipo **Sala Chica (Tipo 1)**, lo que garantiza que siempre exista al menos una sala válida cerca del origen.

> **⚠️ Nota de Rename:** El "Sala Grande" original (tipo 1, 3×3 habitable) ahora se llama **Sala Chica / Vivienda** conceptualmente. El nuevo "Sala Grande" (tipo 7) es el atrio gigante (antes "Abismo"). Y "Abismo Verdadero" es el nuevo tipo 9 (void puro). El código mantiene los números de tipo originales para no romper referencias.
*   **Spawn del Jugador (`DefinirPuntoDeSpawn`, actualizado 2026-07-28):** sala de Vivienda (tipo 1) **aleatoria por semilla** dentro de la región del origen (escaneo de macro chunks `0..periodoIslas-1` en los 3 ejes; el chunk (0,0,0) está forzado a tipo 1 así que siempre hay candidato). Respeta `usarSemillaAleatoria` (la semilla ya viene randomizada de `Start`). Dentro de la sala spawnea en una celda del 3x3 habitable que **NO sea la central** — en la central vive la estructura/escalera y spawnear encima la atravesaba. Usa `Random.InitState(semillaDelMundo + 7777)` con restore de `Random.state`.
*   **⚠️ LA GRILLA ES LOCAL AL TRANSFORM DEL WG (`MundoACelda` / `CeldaACentroMundoPiso`, fix 2026-07-27):** `GenerarCelda` instancia todo en `transform.position + celda*cellSize`, o sea la grilla lógica **es local al GameObject del WorldGenerator**, que en la escena NO está en el origen (está en `(-11.24, -0.23, 42.47)`). Había 6 lugares que derivaban la celda desde una posición de mundo **sin restar ese origen** (`FloorToInt(pos.x / cellSize)` pelado): `Update` (¡el que publica `celdaObjetivo` y maneja la burbuja!), la celda vacía de paredes/carteles, el matcheo de objeto→celda, `PlayerData.coordenadaGrilla` (HUD), `NotaMundo` (misión encadenada) y el fallback de `CartelCoordenadas`. Síntoma concreto: el spawn arrancaba alineado (`WorkerBurbuja` recibe `celdaSpawn` directo) y en el **primer `Update` la burbuja saltaba ~2 celdas en Z** — no se notaba porque el radio 8 siempre deja margen de sobra, pero el streaming perseguía un centro corrido respecto a la geometría real. Ahora **todos** derivan la celda por `WorldGenerator.MundoACelda(worldPos)` (que resta `transform.position`), y la inversa es `CeldaACentroMundoPiso(celda)`. **Regla:** cualquier sistema nuevo que traduzca mundo↔celda usa esos dos métodos, NUNCA `FloorToInt` suelto — así el WG puede estar en cualquier posición sin desalinear nada. *(La alternativa era mover el WG a (0,0,0), donde ambas fórmulas coinciden; se descartó porque desplaza toda la geometría procedural ~42u en Z respecto a los objetos colocados a mano en la escena.)*
*   **Burbuja de Renderizado Asíncrona (`WorkerBurbuja`):**
    *   **Worker permanente re-apuntable:** una única coroutine que corre toda la partida reconciliando la burbuja hacia `celdaObjetivo` (la celda actual del jugador, publicada por `Update` en cada cruce). En **cada yield** chequea si el jugador se movió: si cambió de celda a mitad de pasada, **aborta lo pendiente y re-prioriza** alrededor de la posición nueva. *(Reemplaza al sistema anterior — una coroutine por cruce de celda gateada por `!generando` que debía terminar su pasada completa apuntando al centro viejo: al volar rápido la burbuja perseguía posiciones obsoletas y nunca alcanzaba al jugador.)*
    *   **Presupuesto por TIEMPO (`msPresupuestoBurbuja`, default 5ms/frame):** en vez de una cantidad fija de celdas por frame, el worker genera/enciende/apaga celdas hasta agotar el presupuesto. Celdas caras no clavan el frame; celdas baratas no desperdician cupo. Subirlo acelera la carga a costa de FPS durante ráfagas.
    *   **Carga inicial priorizada:** primero se genera la sala de spawn (3x3x3) y recién entonces se **libera al jugador**, que permanece congelado (`isKinematic`) hasta tener piso firme.
    *   **Actualización incremental (sin flash):** solo se **apagan las celdas que salen** y se **generan/encienden las que entran**, ordenadas de adentro hacia afuera (lo más cercano al jugador primero). El registro `celdasActivasSet` se actualiza incremental (celda por celda), así el abort por re-apunte nunca deja estado inconsistente.
    *   Radio de visión cúbico `radioVision = 3`. Con la burbuja completa y el jugador quieto, el worker duerme (`yield` hasta que cambie la celda objetivo).
    *   **Robustez:** cada `GenerarCelda` se ejecuta dentro de un `try/catch`; un prefab mal asignado solo registra un error y no detiene la generación ni deja al jugador colgado.
*   **Prevención de Paredes Duplicadas:**
    *   Cada celda es responsable únicamente de spawnear su pared **Izquierda (X-)**, **Atrás (Z-)** y su **Piso (Y-)**. Sus correspondientes lados opuestos serán generados por las celdas vecinas.
    *   El piso de la celda de spawn ya **no** usa un prefab "Piso Seguro" especial: se genera con el prefab normal como cualquier otra celda.
*   **Orientación por Normal de Cara (no por ángulos fijos):**
    *   Todas las paredes (interiores y exteriores) y las aristas exteriores se orientan con `Quaternion.LookRotation(normal, Vector3.up)`, usando la normal de la cara de la celda. Se eliminaron los ángulos fijos y los flips aleatorios de 180° que dejaban algunas paredes al revés.
    *   **Paredes interiores:** la cara decorada (+Z del prefab) mira hacia el lado **vacío** (interior) de la celda.
    *   **Paredes y aristas exteriores:** la cara mira hacia **afuera** de la celda (`dirExterior`).
    *   **Rotación de Techos:** Si la celda actual es sólida y la de abajo es vacía, el bloque del piso actúa como techo. Se rota `180°` en el eje X local (`rot = rot * Quaternion.Euler(180f, 0f, 0f)`) para que la cara decorada mire hacia abajo, hacia el interior del pasillo o sala.
*   **Sistema de Carteles de Coordenadas y Avisos:**
    *   **Carteles de Coordenadas (`IntentarInstanciarCartelCoordenadas`):** Se instancian en las paredes de los pasillos con una probabilidad configurable (`probabilidadCartelCoordenadas`).
        *   *Restricción de Pasillo:* Solo se generan si la celda vacía adyacente a la pared es un pasillo interactuable (`EsPasilloInteractuable`), excluyendo Sala Chica (tipo 1), Pozo (tipo 6), Sala Grande (tipo 7) y Abismo (tipo 9).
        *   *Offset de Evitación:* Se desplazan hacia el interior del pasillo por `cartelCoordenadasOffset` para evitar clipping con la malla de la pared.
        *   *Celda forzada (fix "1 vs 2 en el mismo pasillo"):* el cartel vive clavado **en el plano de la cara** = el límite exacto entre dos celdas, así que derivar la celda desde `transform.position` con `FloorToInt` es ambiguo: las caras X+/Z+ caían en la celda vecina y dos carteles del mismo pasillo mostraban números distintos. El generador ahora **inyecta la celda transitable real** (`emptyCell`) vía `CartelCoordenadas.usarCeldaForzada/celdaForzada` justo tras el `Instantiate` (antes de la activación, así `Start()` la lee). El cálculo por posición queda solo como fallback para carteles colocados a mano.
        *   *Anti-overlap con Pasarela:* Antes de spawnear, se llama al helper puro `HabraPasarelaEn(x,y,z, out alturaLocal)` que **predice** (con el mismo hash determinista que `IntentarGenerarPasarela`) si la celda vacía adyacente recibirá pasarela. Si la hay, el cartel **no se coloca** (los forzados de la Gran Muralla se colocan igual porque van en la pared frontera). Necesario porque los carteles se generan **antes** que las pasarelas en el pipeline, así que no pueden consultar el `mundoChunks` — se replica la decisión por hash en su lugar.
    *   **Carteles de Aviso de Sala (Anuncios de Zona):** En la celda central de la base 3x3 de una Sala Grande (`lx == 2, ly == 1, lz == 2`), se instancian 4 carteles (uno en cada cara lateral, apuntando hacia afuera) mostrando el nombre de la sala (ej. "VIVIENDA") en mayúsculas.
        *   *Desactivación y Destrucción:* Para evitar que el script `CartelCoordenadas` reemplace el nombre con los números de las coordenadas en su método `Start()`, el componente se desactiva y se destruye inmediatamente (`DestroyImmediate`) justo después de instanciar el prefab.

*   **Decoración y Aditamentos Procedimentales:**
    *   ~~Tuberías de Pared~~ y ~~Luces de Pared~~: **ELIMINADAS** en la limpieza 2026-07-13 (código y campos borrados). `IntentarGenerarTuberia` sobrevive solo como host de las **Luces de Techo**.
    *   **Pasarelas Industriales (`IntentarGenerarPasarela`):** ⚠️ **REHECHO 2026-08-21 — ver §5.9 (Pasarelas v2: corridas con conectores)**, que reemplaza esta lógica. Se conserva: probabilidad por celda (`probabilidadPasarela`), 3 alturas (`cellSize * 0.25/0.5/0.75` sorteadas por línea de pasillo), exclusión de finales de pasillo (≤1 conexión) y anti-overlap con cierres/carteles.
    *   **Luces de Techo (`prefabsLucesTecho`):** Solo en pasillos rectos Z/X. Se instancian **2 luces por celda, centradas en el techo** (a lo largo del pasillo) y separadas hacia los lados por `luzTechoOffset`. La separación se **recorta** para respetar `luzTechoMargenPared` (margen mínimo con la pared lateral, default 2u) → `offsetLateral = min(luzTechoOffset, cellSize*0.5 - luzTechoMargenPared)`, evitando que modelos de luz grandes atraviesen la pared. Si un slot del array está vacío (`null`) se omite la instancia para evitar excepciones; conviene dejar el array con tamaño 0 si no se usan.
    *   **Paredes Exteriores y Soportes (`InstanciarParedExterior`):** Pueden generarse por fuera de las paredes del pasillo usando `prefabsParedExterior` y `prefabsPilaresParedExterior`.
        *   No generan techos ni pisos exteriores; son solo placas/paredes y sus soportes visuales.
        *   El offset exterior se controla con `paredExteriorOffset` para conservar el margen visual respecto a la celda de 20 unidades.
        *   **Orientación:** la pared exterior se orienta con `LookRotation(dirExterior, up)` (mira hacia afuera de la cara), igual que las aristas.
        *   **Pilares Entremedio (`InstanciarPilarParaParedExterior`):** ya no se coloca un solo pilar. Se generan **entre 3 y 6 pilares** repartidos al azar a lo **ancho y alto** de la cara (`±45%` del tamaño de celda desde el centro en ambos ejes), cada uno con prefab aleatorio del array y orientado con la normal exterior. La distribución es determinista por pared (corre bajo el `Random.InitState(hash)` de esa pared).
            *   *Paredes End (esquina cóncava):* como la pared End es más corta, los pilares repartidos al ancho completo quedaban asomándose hacia la esquina. Para las End se generan **solo 3 pilares** y **solo en la mitad de la celda que apunta al tramo largo del pasillo** (lejos de la esquina). El lado de la esquina se detecta chequeando en qué extremo termina la masa sólida que respalda la pared (`IsCellEmpty` sobre los vecinos del eje paralelo a la cara) y se pasa como `signoLadoLargo` para acotar el rango del offset horizontal.
        *   *Nota:* Se eliminó por completo el sistema de "pilares delgados" en las aristas internas de celda (`prefabsPilaresDelgados` ya no existe).
    *   **Aristas Exteriores (`InstanciarAristasParaParedExterior`):** conectan la pared exterior con el techo y el piso del pasillo. Usan arrays `prefabsAristaExteriorTecho` y `prefabsAristaExteriorPiso` (se elige uno al azar). La de techo se coloca en la **cara superior** de la pared exterior (`paredPos + hs` en Y) y la de piso en la **cara inferior** (`-hs`). Se orientan con `LookRotation(dirExterior, up)`.
        *   *Cobertura de tipos:* además de los pasillos rectos Z (2) y X (3), las aristas también se generan en **Pasillo L (4), Intersección + (5) e Intersección T (8)** — en ambos ejes, porque esos chunks mezclan tramos de ambas orientaciones. Sin esto, los corredores compuestos por chunks L/T/+ quedaban sin trim exterior (huecos visibles desde arriba de la mega-estructura). Los checks de continuidad internos (masa sólida extendiéndose a ambos vecinos perpendiculares / sin paredes vecinas) siguen evitando colocarlas en esquinas y quiebres.
        *   *Marcos en términos de pasillo (`IntentarGenerarMarcoTerminoPasillo`):* cuando un pasillo interactuable termina **abriéndose a una Sala Grande (7) o Abismo (9)** (vecino lateral vacío en chunk 7/9), la boca del tubo recibe un **marco completo**: arista de techo, arista de piso y las dos **aristas verticales** en los cantos laterales. La boca se trata como si tuviera una pared exterior en esa cara (mismo `paredExteriorOffset` de piel); las verticales reusan la convención de yaw por esquina de `IntentarGenerarAristasVerticales` (prefab NW a yaw 0) y sus offsets (`offsetAristaVertical`, `offsetYAristaVertical`, `yawBaseAristaVertical`). Determinista por celda+dirección. Gateado por `generarAristasExteriores`.
        *   **Filtro de Esquina Cóncava:** Las aristas (y la pared exterior) **solo se generan en tramos rectos**. Si la masa sólida adyacente termina en cualquiera de los dos extremos (cell perpendicular vacía), se considera esquina cóncava y se usa el set "End" (paredes/aristas más angostas) si existe (`prefabsParedExteriorEnd`, `prefabsAristaExteriorTechoEnd`, `prefabsAristaExteriorPisoEnd`). Si no hay End asignado, se salta.
        *   **End VERTICAL (masa que termina arriba/abajo):** cuando la masa sólida que respalda la pared termina en `y+1` o `y-1` (XOR — si termina en ambos es pared aislada de 1 celda y queda completa), la pared completa se solaparía con el trim de techo/piso. Se usa el **mismo prefab End girado 90° sobre su normal** (`rot * Euler(0,0,±90)`): el hueco lateral del modelo pasa a apuntar arriba/abajo hacia la esquina — el panel es cuadrado así que el giro no cambia su huella. Convención fija: `signoV>0` (masa termina arriba) → giro +90° (hueco arriba). Prioridad: si la pared ya es End horizontal, se respeta esa (no se combinan). Los pilares de una End vertical se restringen a la **mitad vertical** del tramo largo (mismo patrón que el horizontal). Típico en caras altas expuestas a Sala Grande/Abismo y el borde superior de la mega-estructura.
        *   **Colocación de la End sin offsets (espejo automático):** el prefab End está modelado como una pared normal con un tramo removido y **el mismo pivote**, así que se instancia **exactamente donde iría la pared completa** — sin offsets manuales. Lo único que se decide es el **espejo**: el hueco debe apuntar hacia la esquina. `SignoLadoLargoPared(x,y,z,dirExterior)` detecta hacia qué lado continúa el tramo largo de la masa sólida (vecinos del eje paralelo a la cara) y, si la esquina cae del lado contrario al modelado (`signo<0`), se aplica un roll local de 180° — como el prefab es simétrico verticalmente, el volteo no se nota. Convención calibrada en código (sin toggles). *(Reemplaza al sistema anterior de 4 campos de offset/rotación por eje — `offsetParedExteriorEndCaraX/Z`, `rotacionEndCaraX/Z` — que solo acertaba cuando la esquina caía del lado calibrado y en los cruces dejaba la mitad de las End desfasadas.)* El mismo `signoLadoLargo` acota los pilares de la End a la mitad del tramo largo.
    *   **Cierres de Pasillos (`IntentarGenerarCierrePasillo` → `TransicionConPuerta`):** puertas (incluidas las **puertas interactivas** de panel) SOLO en **cambios de sección**. Regla única (`TransicionConPuerta(x,y,z,caraX)`): la cara Z-/X- de la celda lleva puerta si (1) cruza un **límite de macro chunk** (`lz==0`/`lx==0`), (2) **ambos lados son caminables** (conexión real), (3) los dos chunks son de **tipo distinto** (recto→T, pasillo→vivienda, entrada a sala grande — dentro de una misma sección NUNCA hay puerta), y (4) pasa el roll determinista de `probabilidadCierre`. Bocas al void (9) excluidas (las maneja `prefabsPuertaBocaVacio`). *(Reemplaza la lógica anterior por tipo de chunk, que regaba puertas por cualquier frontera de celda caminable — con animaciones de ~20s cada una, era más molestia que atmósfera.)*
        *   *Anti-overlap con Pasarela:* Las pasarelas (`IntentarGenerarPasarela`) se saltan si la celda actual **o cualquiera de sus 4 vecinas laterales** va a recibir un cierre. El predictor `HabraCierreEn(x,y,z)` ahora **llama a la MISMA función** que la generación (`TransicionConPuerta`) — predictor y generador sincronizados por construcción, ya no son réplicas gemelas que puedan divergir.

*   **Pasarelas en Sala Grande / Abismo:** El sistema de pasarelas (`IntentarGenerarPasarela`) ahora detecta cuando un vecino es Sala Grande (tipo 7) o Abismo (tipo 9) y lo trata como **pared sólida** para la lógica de "cara abierta". Sin este filtro, las pasarelas tipo T se extendían hacia el interior del void quedando flotando. Helper: `EsSalaGrandeOAbismo(x, y, z)`.

---

## 5.1. Sistema de Loot y Estructuras por Sala (`RoomData.cs`, `GenerarLootDeSala`)
Cada tipo de sala puede tener su propia configuración de objetos y estructuras que aparecen en su interior.

*   **`RoomData` (ScriptableObject `Mundo/RoomData`):** define `nombreSala`, una lista `lootPosible` de entradas `{ prefab, probabilidad }`, un rango `cantidadLoot` (min/max de objetos a spawnear), un array opcional `mobiliario` y un array de `prefabsCentrales` (estructuras principales para el centro de la sala).
*   **Estructura Central de la Sala:** En la celda central (`lx == 2, ly == 1, lz == 2`), se selecciona e instanciará un prefab al azar del array `prefabsCentrales` de la sala seleccionada. Si la sala no tiene datos asignados (`sala == null`), se instanciará por defecto un prefab del array `prefabsEscaleras` del generador para permitir la subida.
*   **Carteles de Nombre de Sala:** En la misma celda central, se instancian 4 carteles (uno por cara lateral, apuntando hacia afuera) usando el mismo `prefabCartelCoordenadas` pero con el texto reemplazado por `sala.nombreSala.ToUpper()` (ej. "VIVIENDA"). El componente `CartelCoordenadas` se destruye con `DestroyImmediate` **antes** del `SetActive(false)` para que no sobreescriba el texto al activarse; el TMP se actualiza con `ForceMeshUpdate()` para garantizar el rebuild de la mesh.
*   **Generación de Loot (`GenerarLootDeSala`):** se ejecuta **una sola vez por Macro Chunk** de Sala Grande (en la celda central, junto a la estructura central). Elige una `RoomData` al azar de forma determinista por chunk (mismo patrón de hash que el resto del generador), decide cuántos objetos spawnear y los coloca en posiciones aleatorias dentro del piso 3x3 de la sala.
*   **Auto-carga desde `Resources/RoomData/`:** en `Awake`, si el array `salasGrandesPosibles` está vacío en el inspector, el `WorldGenerator` lo rellena automáticamente con `Resources.LoadAll<RoomData>("RoomData")`. Esto evita tener que arrastrar manualmente cada asset al inspector — solo creá los assets dentro de `Assets/Resources/RoomData/` y quedan registrados. Si dejás algo asignado a mano en el array, se respeta y no se sobreescribe.

---

## 5.2. Balcones (`WorldGenerator.cs` — sección "Balcones")
Celdas de pasillo que **eliminan toda la pila de una cara lateral** (pared interior + pared exterior + pilares entremedio + aristas exteriores) para que el corredor se abra al void hacia un costado. Piso y techo se conservan.

*   **`generarBalcones`, `probabilidadBalcon`:** controlan la activación y frecuencia (default 5%). La decisión es **determinista por celda + semilla del mundo** — la misma celda siempre es o no es un balcón en una semilla dada.
*   **Dirección única por balcón (`DireccionBalcon`):** el balcón elige UNA cara lateral entre las que tienen vecino sólido (es decir, donde habría una pared). Si la celda no tiene paredes en ninguna cara (ya está abierta en todos los lados), no hay balcón.
*   **Solo en pasillos interactuables:** nunca aparece en salas grandes, pozos, abismos ni en la Gran Muralla. Tampoco en niveles `ly` distintos a 1.
*   **Solo en tramos rectos, lejos de esquinas:** helper `EsTramoRectoDePasillo(x,y,z, out eje)` — una celda es "recta" si tiene exactamente 2 vecinos laterales abiertos y **opuestos** entre sí (esquinas con vecinos perpendiculares, intersecciones y callejones quedan fuera). El balcón exige que la celda sea recta **y sus dos vecinas a lo largo del corredor también** — en una corrida de 5 celdas entre dos esquinas, solo la 2, 3 y 4 califican. Evita que la apertura del balcón se pise con la geometría de esquina (paredes End, aristas, giros).
*   **Skip simétrico de la pila completa:** el flag `paredXSalteadaPorBalcon` / `paredZSalteadaPorBalcon` se chequea contra `DireccionBalcon` desde ambos lados del par celda-vecina y se aplica a `prefabsPared`, `InstanciarParedExterior` y a las aristas continuas, de modo que ninguno de esos tres elementos se instancia para esa cara.
*   **`prefabBalcon`:** se coloca en el **borde de la celda** del lado abierto (no en el centro), empujado por `balconOffset` hacia la pared exterior. Su forward (+Z) se orienta hacia afuera con `LookRotation`. Ideal para baranda con collider que evite la caída.
*   **Sin arista inferior en la cara del balcón:** dentro de `InstanciarAristasParaParedExterior`, si la cara corresponde a la apertura de un balcón (`DireccionBalcon(celdaVacía) == dirExterior`), se omite la **arista de piso** (la plataforma/baranda del balcón ocupa ese borde y la arista la atravesaba). La de techo se conserva. El guard vive dentro de la función para cubrir todos los call sites — necesario porque el bloque "forzar" de aristas de la celda del pasillo coloca las caras X+/Z+ pero los flags `paredX/ZSalteadaPorBalcon` solo gatean X-/Z-.
*   **Anti-clip con paredes End:** la pared exterior **End** (variante angosta de esquina cóncava, `prefabsParedExteriorEnd`) se detecta por geometría pura (`IsCellEmpty`), que no se entera del balcón. Para que la End no clipee con la baranda del balcón, `InstanciarParedExterior` **omite la End** si la celda actual o la vecina que comparte esa cara tiene un balcón (`DireccionBalcon != zero`). Solo afecta a los tramos End (cóncavos); los tramos rectos normales ya se saltan con el flag `paredXSalteadaPorBalcon`/`paredZSalteadaPorBalcon`.

---

## 5.3. Techos Exteriores (`WorldGenerator.cs` — sección "Techos Exteriores")
Simétrico al de pisos exteriores, totalmente independiente. Cuando la celda actual es un pasillo (vacía) y la celda de arriba es sólida (hay techo encima), se instancia un prefab decorativo **sobre** el techo del pasillo (visible desde el cielo / por encima de la mega-estructura).

*   **Campos:** `prefabsTechoExterior`, `generarTechosExteriores`, `probabilidadTechoExterior` (default 0.5).
*   **Posición:** `(y+1) * cellSize + paredExteriorOffset` arriba del techo, espejo del piso exterior (`y * cellSize - paredExteriorOffset` abajo del piso).
*   **Determinista** por celda con hash independiente al del piso (no compiten por la misma random sequence).
*   **Pilares entremedio en techo/piso (`pilaresEnTechoPisoExterior`, default true):** las placas de techo y piso exterior reciben los mismos **3-6 pilares** de `prefabsPilaresParedExterior` que las paredes, girados hacia la normal vertical (`Vector3.up`/`down`) — no hace falta un prefab de pilar aparte. `InstanciarPilarParaParedExterior` se generalizó con una base de ejes robusta para cualquier normal (con normal vertical `LookRotation(n, up)` degenera → up-hint `forward`; los dos ejes de reparto barren el plano XZ). Para paredes el resultado es matemáticamente idéntico al comportamiento anterior. Los pilares solo aparecen cuando la placa exterior efectivamente spawnea (comparten su ventana determinista de Random).

---

## 5.4. Aristas Verticales en Esquinas de Sala (`WorldGenerator.cs` — sección "Aristas Exteriores Verticales")
Pilares/columnas verticales que se colocan en las **4 esquinas exteriores** de Sala Chica (tipo 1) y Pozo Vertical (tipo 6), marcando el ángulo donde se cruzan dos paredes exteriores en 90°.

*   **Sala Chica (tipo 1):**
    *   `ly` permitido: **1 o 3** — los 2 pisos habitables de la sala. `ly=2` es el balcón central que conecta los pisos (no recibe aristas) y `ly=0/4` quedan pegados al piso/techo del macro y se ven feos.
    *   `celdasAInterior = 1` — la zona vacía interior de la sala empieza 1 celda adentro de la esquina del macro (3×3 a partir de lx=1).
    *   Niveles ly configurables en `nivelesAristasVerticales` (default `{1, 3}`).

*   **Pozo Vertical (tipo 6):**
    *   `ly` permitido: **todos** (0, 1, 2, 3, 4) — el pozo es un cilindro continuo y la arista debe acompañar toda la altura.
    *   `celdasAInterior = 2` — la columna vacía del pozo está en lx=2, lz=2 (2 celdas adentro). La posición de la arista se calcula desde esta distancia para caer en el cruce de las paredes exteriores del pozo (que están más adentro que las del sala chica).
    *   *Exclusión de Pasillo:* Si a la altura local `ly` (`y` absoluta) existe un pasillo transitable/vacío adyacente a la esquina en cuestión en los ejes X o Z, se omite la generación de la arista vertical para no obstruir el acceso visual y físico del corredor.

*   **Posición común:** dentro de la celda esquinera, en el **cruce de las dos paredes exteriores** — a `celdasAInterior * cellSize + dx * paredExteriorOffset` desde el borde del macro chunk. Ajustable con `offsetAristaVertical` (XZ) y `offsetYAristaVertical` (Y, default `10` para pivote centrado).

*   **Rotación cardinal por esquina** (`yawBaseAristaVertical` + offset por esquina):
    *   SW (-X-Z): -90°
    *   NW (-X+Z): 0°
    *   NE (+X+Z): +90°
    *   SE (+X-Z): +180°
*   **Prefab modelado para esquina NW por default;** si está modelado para otra esquina, ajustar `yawBaseAristaVertical` en pasos de 90.

---

## 5.5. Vértices Convexos y Aristas en Ambos Pisos de Salas (`WorldGenerator.cs` — sección "Vértices Convexos")
Piezas de conexión en esquina (`VerticeConvexo`) y marcos de arista horizontales para terminar el acabado visual exterior del mapa.

*   **Vértices Convexos (`prefabsVerticeConvexo`):**
    *   **Identificación**: Se evalúan las 4 celdas alrededor del vértice South-West de la celda `(x, z)` a la altura `y` utilizando el método `EsCeldaSolidaParaVertices`. Para salas de `tipo == 1` (Vivienda/Sala Grande), la estructura sólida real del edificio se considera la caja de `3x3` (`lx` y `lz` de 1 a 3 entre `lcy` 1 y 3) para evitar el desfase de 1 celda (20m) que causaba la cuadrícula límite de `5x5`. Si exactamente **1 de las 4 celdas es sólida**, y **ninguna de las celdas vacías adyacentes es interior jugable**, se identifica como un vértice convexo exterior.
    *   **Continuidad**: Se verifica si los muros de la esquina continúan en `y - 1` o `y + 1`. Si no continúan hacia abajo, se genera un vértice convexo en el piso (`Y = y * cellSize`). Si no continúan hacia arriba, se genera en el techo (`Y = (y + 1) * cellSize`).
    *   **Rotación (Yaw)**: Rotación cardinal automática según cuál de las 4 celdas sea la sólida (asumiendo que el prefab `VerticeConvexo.fbx` a `yaw = 0f` apunta hacia el Noreste):
        *   **Noreste** (`s_SW` sólida): `0°`
        *   **Sureste** (`s_NW` sólida): `90°`
        *   **Suroeste** (`s_NE` sólida): `180°`
        *   **Noroeste** (`s_SE` sólida): `-90°`
        *   *Nota*: Modificable por la variable global `yawBaseVerticeConvexo`.
*   **Aristas Horizontales en Ambos Pisos de Salas Chicas (tipo 1, ly == 1 y ly == 3):**
    *   Para dar acabado al primer y segundo piso de las Salas Chicas (tipo 1), cuando la pared exterior está a una altura local `sly == 1` o `sly == 3` dentro del macro chunk, se generan automáticamente tanto la arista de techo (`_techo`) como la de piso (`_piso`) correspondientes a ese nivel, cerrando los marcos superior e inferior.
    *   **Exclusión central**: en `ly == 2` (el balcón central que conecta los pisos verticalmente) **no** se generan aristas horizontales en tipo 1. La columna central de la Vivienda queda visualmente limpia.

*   **Esquinas Cóncavas en Sala Chica (entradas a pasillos):**
    *   `EsParedAdyacenteEsquinaConcava` ya **no** tiene el guard `if tipo == 1 return false`. Ahora la detección geométrica de esquina cóncava también se aplica dentro de Salas Chicas, lo que permite que las paredes laterales de las entradas carved (pasillo conector entre sala y vecino) usen el **prefab End** (paredes angostas) en lugar de las completas, y se salten las aristas en esos puntos. Resultado: los conectores quedan limpios, sin solapamientos visuales ni aristas que tropiecen con la pared perpendicular.

---

## 5.6. Omisión de Paredes y Aristas en Esquinas Cóncavas (Pasillos y Puentes)
Para evitar el solapamiento visual y problemas de geometría (Z-fighting) en los giros e intersecciones de pasillos y puentes, las paredes exteriores y las aristas horizontales que colindan directamente con una esquina cóncava (ángulo interno de 90°) se omiten por completo de la generación del mundo.
*   **Detección de Esquina Cóncava (`EsEsquinaConcava`)**: Se evalúa si la celda sólida que respalda la pared tiene al menos un par de vecinos vacíos perpendiculares entre sí (Norte-Este, Este-Sur, Sur-Oeste o Oeste-Norte).
*   **Identificación del Bloque Sólido Correcto (`EsParedAdyacenteEsquinaConcava`)**: Se determina la posición del bloque sólido físico a partir de la normal `dirExterior`, validando dinámicamente si la celda actual es vacía o sólida para evitar errores de desfase (offset). Si el bloque sólido califica como esquina cóncava, se aborta la generación de la pared y de la arista adyacente.
*   **Exclusiones**: Esta regla no aplica para las salas (`tipo == 1`), cuyas esquinas y muros exteriores se mantienen cerrados por diseño.

---

## 5.7. Sala Colosal — Tipo 10 (una isla entera = UNA sola sala, agregado 2026-08-21)
Un roll por isla (`probabilidadIslaColosal`, default 0.2; la región (0,0,0) del spawn NUNCA) convierte la isla completa en **un solo espacio hueco**: con `periodoIslas=10` y radio máx, una nave de ~460m de lado y alto.

*   **Cáscara sólida (`EsCascaraSalaColosal`):** las celdas a `grosorCascaraColosal` (1-2) del borde del volumen quedan sólidas y todo lo interior hueco — el generador de superficies existente le pone piso/techo/muros solo, sin código nuevo de paredes. `CentroIsla` fuerza **caja + radio máximo** para las colosales.
*   **Entrada por puente:** el tipo 10 SÍ pasa por el tallado de conectores (`HasConnection` acepta boca a `ly==1` desde cualquier lado); la lane del puente perfora la cáscara. Contra el void nunca se talla → la cáscara solo se abre donde llega un pasillo real. Los lanes corren a nivel base → entrás a ras del piso.
*   **Casco visible e INVERTIDO (`CaraDaAlVacio` + `CaraDeCascoColosalInvertida`):** la cáscara se recubre contra el vacío (excepción al "sin paredes contenedoras" — sin esto la isla era una caja invisible). En la cara EXTERNA del casco los roles interior/exterior van al revés de lo que asume el generador ("lado vacío = interior"): se invierten `normalInterior` y `dirExterior` en paredes X/Z **y también en piso (panza) y techo (coronación)** — la panza usa piel de piso exterior, la coronación piel de techo exterior, cada una con su offset hacia afuera. Guarda anti-duplicado en el bloque 3b del techo exterior.
*   **`cascoColosalSiempreConPielExterior` (default true):** el casco ignora los 3 rolls de probabilidad de piel exterior (0.18/0.5/0.5) — si no, sale parchado con agujeros. Apagarlo = casco en ruinas deliberado.
*   **`pilaresEnSalaColosal` (default true):** reusa `prefabsPilaresGigantes` como columnas continuas cada 100m. ⚠️ Son cientos de instancias — **primer sospechoso si mirar la colosal desde lejos mata los FPS**; apagable sin tocar código.
*   **Exclusiones:** nunca spawn del jugador adentro (`TryElegirCeldaSpawn` filtra tipo 10), nunca cuartel adentro (es una sala, no hay chunks), `EsSalaGrandeOAbismo` incluye el 10 (decoradores de pasillo no se extienden; bocas reciben marco), `EsPasilloInteractuable` lo excluye.

---

## 5.8. Cuartel Garantizado por Isla + Islas sin Viviendas + Conectividad Lateral (2026-08-21)

### Cuartel garantizado (`cuartelGarantizadoPorIsla`, default ON)
**Garantía DURA: toda isla no-colosal tiene EXACTAMENTE UN cuartel de centinelas, y ninguna vivienda común puede serlo.**
*   **⚠️ Fix crítico — cuarteles fantasma en viviendas:** la ruleta ponderada de `ObtenerRoomDataParaMacro` incluía las RoomData con `esCuartelCentinelas` → ~1 de cada 11 viviendas comunes salía "cuartel" invisible (se veía como vivienda normal) y los centinelas spawneaban adentro, incluso al lado del spawn. **Las RoomData de cuartel ya NO entran en la ruleta** (su `pesoAparicion` queda sin efecto; excepción de sanidad: si TODAS las RoomData son cuartel, cae al reparto viejo). El único cuartel es el del chunk garantizado.
*   **`TryCuartelDeIsla` — cascada de candidatos** (cacheado por región, hash determinista): **núcleo** (conectado + sin tocar void, el cuartel perfecto) → **borde** (conectado pero expuesto al void — sala de casco imperfecta, se aguanta) → **lo que sea** (cualquier chunk no-pozo/no-lane/no-spawn, hasta poroso; solo islas degeneradas de radio 1). El chunk (0,0,0) del spawn nunca puede ser cuartel.
*   **Forzado ANTES de porosidad/coherencia/borde** (paso 2.5 del tipado): si estuviera después, el remapeo "borde→pasillos" pisaba al cuartel de borde y la garantía se caía en silencio. `ObtenerRoomDataParaMacro` devuelve para ese chunk una RoomData de cuartel vía `ElegirRoomDataCuartel` (hash si hay varias).

### Islas sin viviendas (`probabilidadIslaSinViviendas`, default 0.35)
Por hash de región: lo que la densidad alta hubiera tipado como Sala Chica (1) se remapea a `{5,6,8}` — isla puramente industrial, de tránsito, deshabitada. El cuartel garantizado NO se ve afectado (se fuerza antes). La región (0,0,0) queda excluida (`spawnEnVivienda` necesita casas).

### ⚠️ Conectividad de isla = SOLO LATERAL (fix "pasillo L flotando")
`ChunksConectadosDeIsla` hacía BFS de **6 vecinos**, pero la adyacencia vertical entre chunks comunes es mentira: los conectores tallados son solo laterales y lo vertical solo existe por la columna-pozo — un chunk podía darse por "conectado" a través del de arriba (enlace que no talla paso ni genera geometría) y quedaba un pasillo **flotando en el void, inalcanzable**. El BFS ahora expande **solo lateral** (la columna ya siembra todos sus niveles): cada chunk necesita cadena lateral hasta la columna EN SU NIVEL. Islas más podadas y cero flotantes.

---

## 5.9. Pasarelas Industriales v2 — Corridas con Conectores (rework 2026-08-21)
Reemplaza la lógica anterior (§5 "Decoración"). Modelo mental: una **corrida** es un grupo de celdas contiguas que generan pasarela **a la misma altura sobre la misma pared**. Toda pieza colocada pertenece a una corrida válida — cero piezas huérfanas por construcción.

*   **`ContinuaTramo(nx,nz,caraDir)` (fix estructural):** la continuidad era `IsCellEmpty` ("¿hay aire al lado?") → los `_mid` apuntaban a celdas que nunca generaban nada y los `_end` casi no salían. Ahora el vecino continúa solo si: (a) **va a generar pasarela** (`HabraPasarelaEn`, el predictor — al que se le agregó el **veto por cierres** que le faltaba, arreglando de paso los carteles que se salteaban posiciones válidas), (b) **misma altura** (las alturas se sortean por línea; distinta altura = corte de corrida con tapa), y (c) **la pared continúa** de su lado.
*   **Regla de corrida: final–recta(s)–final.** Corridas de 2 celdas (tapa+tapa sin recta) se **descartan enteras** (`FiltrarCorridaSinRecta` — ambas celdas evalúan simétrico); piezas `_isolated` no se colocan. El fin SIEMPRE está unido a una recta.
*   **Escaleras SOLO en extremos (`probabilidadEscaleraExtremo`, default 0.35):** la escalera es el ACCESO — terminación alternativa a la tapa de fin, excluyentes entre sí, por extremo. Nunca al medio (antes un roll del 20% picaba las hileras largas en segmentos). Elección de prefab determinista por hash (antes `Random.Range` sobre estado global).
*   **Sistema de CONECTORES (empties `Conector*` en los prefabs):** las piezas de terminación más cortas que la celda (tapa ~4m, escalera ~15-16m) se pegan alineando su empty al **borde de celda del lado conectado** (donde muere la recta vecina, que mide 20 exacto y se alinea sola). `BuscarConectores` junta todos los hijos "Conector*"; con varios se elige **el que ya mira hacia la unión** → la pieza siempre se extiende hacia su propia celda. Convención: **tapa de fin = 1 conector** (punta abierta; con 2, el picker podría apoyar la tapa contra la recta) + `prefabPasarelaFinInvertida` para el espejo (un giro de 180° en Y mandaba el tablón DENTRO de la pared — pivote al borde contra-pared); **escalera = 1 por punta** (2). ⚠️ Los empties se agregan **en el prefab de Unity** (no en Blender: `preserveHierarchy: 0` los descarta al importar).
*   **`yawExtraPasarelaFin`:** yaw extra solo para piezas de fin, por si el prefab tiene el largo sobre otro eje que la recta (pasos de 90).
*   **Esquina L (`prefabPasarelaL` + `yawBasePasarelaL`, caso 1.5):** pieza INTEGRAL de celda esquina — dos brazos de `20 − pasarelaOffset − ancho_tablón` (~15.4m) hasta los bordes de celda + el codo, un solo mesh (con rectas fijas de 20 los brazos no se pueden rellenar por separado: se solapan en el codo). Solo se coloca si **ambos brazos continúan** en sus vecinos; 4 orientaciones por esquina sólida (S+O modelado = yaw 0). **Regla dura: sin conectores en el prefab NO se coloca** (aviso único en consola) — la esquina cae a las dos tapas de fin, que ya calzan. Alineación por conector del lado X abierto → pivote del prefab libre. *Pendiente: modelar la pieza (hoy el slot tiene solo el codo suelto → desactivada por la regla).*
*   **T sin fallback:** `prefabPasarelaT` vacío = **nada** (antes caía en silencio a la recta y CADA cruce plantaba una recta suelta atravesada). Con prefab real, solo se coloca si el segmento de la cara sólida existe (`vm||vmn`). *Pendiente: modelar la T (travesaño de 20 borde a borde + tallo).*
*   **Diagnóstico:** el `logDebugPasarelas` que loguéaba cada pieza colocada se eliminó (2026-09-02) por ruidoso. Quedaron vivos, **sin flag**, los dos warnings que importan: prefab de extremo sin hijo `Conector`, y prefab con escalera sin hijo `PuntoEscalera`. Esos no son debug sino errores de armado del prefab — si no saltan solos, el síntoma (pieza centrada en la celda, escalera en la mitad de la pasarela) es carísimo de diagnosticar a ojo.
*   **Principio general del kit** (aplica también a tuberías): piezas de **un empalme** (fin, escalera) → conectores, el script las pega; piezas de **empalme múltiple** (recta, T, L) → medidas exactas respecto a la grilla, el cálculo las coloca. Una traslación no puede satisfacer varios puntos de unión a la vez.

---

## 5.10. Objetos de Borde de Pasillo (`IntentarGenerarObjetosBorde`, agregado 2026-08-21)
Props a nivel de piso (cajas, escombros, tachos) regados en la **franja pegada a las paredes** de los pasillos. La franja es matemática pura por seed (sin colliders de spawn): desde cada cara lateral CON pared, `anchoFranjaBorde` (2m) hacia el centro — caras sin pared (pasillo abierto, balcón, void) no tienen franja, así que los objetos quedan **siempre del lado de la pared por construcción**.

*   **Campos:** `prefabsObjetoBorde` (pivote en la BASE; array vacío = apagado), `probabilidadObjetoBorde` (0.35, **por cara** — una pared puede tener escombros y la de enfrente no), `objetosPorCara` (1-3), `margenFranjaBorde` (1m desde las esquinas de celda), `objetoBordeMiraAlPasillo` (OFF = yaw aleatorio/escombro, ON = de espaldas a la pared).
*   Solo `EsPasilloInteractuable` (las salas se decoran a mano); se saltea celdas con cierre (no tapar puertas); no choca con pasarelas/tuberías (viven a 5-17m de altura). Determinista por celda+cara, mismo patrón de todos los decoradores.

---

## 5.11. Secciones de Vivienda Orientadas al Lado Libre (2026-08-21)
Las secciones (agua/comida/batería/dormir) de las 4 esquinas del 3x3 se instanciaban con `Quaternion.identity` — a veces mirando al muro. Ahora (`orientarSeccionesALadoLibre`, default ON) cada sección **mira a uno de sus dos lados libres**: en el 3x3, las 2 caras de una esquina que apuntan AFUERA dan contra la masa sólida y las 2 que apuntan ADENTRO dan a las celdas de paso — el lado libre sale de la posición (`lx==1 → +X, lx==3 → −X`, ídem Z), sin chequear vecinos. Entre los dos libres elige por hash de celda (estable por semilla, no todas las viviendas calcadas). `yawBaseSeccion` si los prefabs no miran a +Z. La sección de **almacenamiento** central sigue sin rotar (4 lados libres, no hay desambiguación).

---


## 6. Sistema de Tooltips Globales (UI Flotante)
Muestra información flotante en el espacio del mundo al jugador de manera optimizada y sin parpadeos de interfaz.

*   **Canvas Centralizado:** Existe un único Canvas en la escena llamado `TooltipGlobal` configurado en World Space. Evita crear Canvas individuales por cada objeto.
*   **Condición Físico-Cinética de Visibilidad:** El Tooltip flotante solo se encenderá si el objeto interactuable está en reposo relativo (`rigidbody.linearVelocity.magnitude < 0.1f` o si es Kinematic). Si el objeto es empujado, cae o rueda debido a las físicas del motor, el tooltip se apaga inmediatamente por inmersión.
*   **Alineamiento del Canvas:** Cuando se muestra, el Canvas flota verticalmente sobre la posición del objeto (`Vector3.up * distanciaAltura`) o en la posición del hijo vacío llamado `PuntoUI`. Mantiene una rotación estática global mirando hacia la cámara para garantizar su lectura, ignorando la rotación física que tenga el objeto.
*   **Suavizado de Raycast:** Cuenta con un delay de apagado de **0.2 segundos** (`tiempoOcultar`) para evitar que el texto parpadee molestamente si el jugador mueve rápidamente la mirada sobre las aristas del objeto.
*   **Información de Objetos en la Mira (Tooltip dinámico):**
    *   Cuando el jugador tiene las manos vacías y la mochila cerrada, un raycast continuo en `ManoJugador.cs` detecta si se está mirando un objeto recogible (`InventoryItem`).
    *   Al detectarlo, llama a `TooltipManager` para mostrar su nombre y peso (ej. `Chatarra\nPeso: 1.5 KG`).
    *   **Exclusión de Anclaje Activo:** Si el objeto es un anclaje de rápel colocado (cuerda desplegada), no muestra los datos del objeto y cede la prioridad a `SistemaRapel.cs` para mostrar la opción `[F] Cuerda`. Si el anclaje está simplemente tirado en el suelo, actúa como un ítem común mostrando sus datos de peso y nombre.

---

## 6.1. Sonido Ambiente Interior / Exterior (`AmbientSounds.cs`)
Crossfade automático entre dos pistas de loop según si el jugador está **dentro** de una estructura o **al exterior**.

*   **Dos fuentes de audio:** `fuenteBase` (clip interior, ambiente claustrofóbico) y `fuenteExterior` (clip exterior, ambiente abierto/vacío).
*   **Detección por raycast:** cada frame, un `Physics.Raycast` desde el jugador hacia arriba (`alturaDeteccionTecho`, default 25u) con máscara de capas (`capasEstructura`). Si golpea algo → "dentro"; si no → "fuera".
*   **Crossfade suavizado:** los volúmenes de ambas fuentes hacen `Lerp` hacia su objetivo con `velocidadCrossfade`. El fade-in inicial se multiplica por encima del crossfade durante los primeros `duracionFadeIn` segundos.
*   **Robustez:** funciona con balcones (sigue siendo "dentro" si hay techo arriba), abismos (exterior), vuelos por encima de la mega-estructura (exterior), y caminar bajo un puente (interior temporal coherente).
*   **Tip de configuración:** si hay falsos positivos por colliders pequeños (carteles, cuerdas), filtrá `capasEstructura` a solo la capa de paredes/pisos/techos.

> **Nota arquitectónica:** se considera la alternativa de consultar `WorldGenerator.TipoMacroChunkEnPosicion()` (que devuelve el tipo de macro chunk — Sala/Pasillo/Abismo — en una posición mundo dada) para que "exterior" sea estrictamente "tipo 7 Abismo". Por ahora se usa el raycast por simplicidad y porque cubre más casos visuales (e.g. caminar arriba de un piso de la estructura).

---

## 7. Presencia Sonora Orbital (`OrbitadorSonoro.cs`)
Un objeto que ronda al jugador para crear tensión ambiental: orbita a distancia y, cada cierto tiempo aleatorio, emite un sonido. Pensado como una "presencia" que el jugador puede intentar seguir por el oído.

*   **Órbita:** gira alrededor del jugador a `radio`/`altura` configurables, con `velocidadAngular` (grados/s). Usa un seguimiento suave (`Lerp` exponencial, `velocidadSeguimiento`) que le permite "alcanzar" al jugador si éste se mueve — por eso se le puede seguir.
*   **Sonido:** cada `intervaloSonido` (rango aleatorio min/max) reproduce un `AudioClip` al azar del array `sonidos`. El `AudioSource` se configura como **3D** (`spatialBlend = 1`) para ubicarlo por el oído.
*   **Pausa al sonar:** mientras el clip suena, **detiene la órbita y mantiene su posición** en el mundo (no sigue al jugador). Al terminar el sonido, retoma la órbita y reprograma el próximo.
*   Si `jugador` no se asigna, busca el tag `Player`.

---

## 8. Sistema de Muerte y Cierre del Ciclo Jugable
La muerte cierra el loop: cuando el jugador muere, se registran stats de la run, se carga la escena del menú principal, y un canvas persistente muestra el resumen.

### 8.1. `PlayerData.Morir(string causa)` y Daño por Caída
*   **`PlayerData.Morir(causa)`** es idempotente: si el jugador ya está muerto no hace nada. Desactiva `PlayerMovement` y `MouseLook`, libera el cursor, registra las stats de la run en `EstadisticasRun.RegistrarMuerte(...)`, activa `MuerteUI`, espera `delayMuerte` segundos (default 1.5s) y carga la escena `escenaMenu` (default `"Menu"`).
    *   **⚠️ DESACTUALIZADO (post §19):** `Menu.unity` ya no existe — el menú vive **dentro** de `OutdoorsScene` (ver §19). Cargar una escena `"Menu"` inexistente rompe el flujo de muerte tal cual está. Pendiente: reescribir para que la muerte reinicie el estado dentro de la misma escena (reactivar `MenuInicial`/Menu2, volver a poner `ModoMenu.Activo = true`) en vez de un `SceneManager.LoadScene` a una escena separada.
*   **Daño por caída (`PlayerMovement`):** trackea la **altura máxima alcanzada en el aire** (`alturaMaximaEnAire`) y al aterrizar mide `alturaMaximaEnAire - yActual`. Si supera `distanciaCaidaLetal` (default 25m), llama a `playerData.Morir("Caída de N m")`.
*   **Toggle `danoCaidaActivo`:** permite testear vuelo sin morir aterrizando suave.

### 8.2. Stats Persistentes Entre Escenas (`EstadisticasRun.cs`)
Clase estática que sobrevive el cambio de escena al menú. Almacena:
*   `diasSobrevividos`, `notasLeidas`, `pesoMaximoCargado`, `causaMuerte`, `huboMuerte`
*   `bibliotecaNotas` (HashSet<NotaData>): notas leídas a lo largo de **todas las runs** acumuladas

`RegistrarMuerte(...)` se llama desde `PlayerData.Morir`. La biblioteca de notas se va acumulando entre runs.

### 8.3. `MuerteUI` (Canvas Persistente)
Canvas que muestra los stats al morir y **sobrevive el cambio de escena** con `DontDestroyOnLoad`:
*   Pinta `txtCausa`, `txtDias`, `txtNotas`, `txtPesoMaximo` con los valores de `EstadisticasRun`.
*   Hace **fade-in** sobre el `CanvasGroup` durante `duracionFadeIn` segundos.
*   Se autodestruye con **Escape** una vez en el menú.

---

## 9. Notas / Lore (`NotaData.cs`, `NotaMundo.cs`, `LectorNotas.cs`, `NotaPanelUI.cs`)
Sistema de lore independiente del inventario. Las notas se recogen pero **NO ocupan celdas** ni se guardan físicamente — se "leen" y se acumulan en una BD virtual del jugador.

### 9.1. Datos y Mundo
*   **`NotaData` (ScriptableObject `Lore/NotaData`):** `titulo`, `contenido` (TextArea 4-30 líneas), `autor`.
*   **`NotaMundo` (en el prefab):** componente con `data` (NotaData) y `peso` (default 0.01kg). Es lo que se recoge del mundo, NO usa `InventoryItem`.

### 9.2. Lectura (`LectorNotas` en el Player)
*   **Click Izq:** raycast desde el centro de la cámara. Si pega a un `NotaMundo`, lo agarra (kinematic, suma peso al inventario).
*   **Mientras se sostiene:** flota a `distanciaSostener` (0.6m) frente a la cámara con `offsetVertical` (-0.15m). No rota, no snap a mochila.
*   **Click Der HOLD:** muestra el `NotaPanelUI` con título / contenido / autor formateados. Soltar Click Der oculta el panel.
*   **E:** guarda la nota en `playerData.notasGuardadas` y destruye el GameObject del mundo (descarta peso). `GuardarNota` evita duplicados.
*   **Click Izq UP:** suelta la nota con física.

### 9.3. Lore Log en el Menú (`MenuLoreLog.cs`)
En la escena del menú, lista los títulos de `EstadisticasRun.bibliotecaNotas` (acumulada entre runs) en un único `TMP_Text`, formato `• Título — Autor`. Si está vacía, muestra `mensajeVacio`.

### 9.4. Misiones Encadenadas (cadenas de notas)
Mini-misiones de "andá a buscar materiales a X": una serie de 3-5 notas que cuentan una historia, cada una apuntando al lugar de la siguiente.
*   **Datos (`NotaData`, sección "Misión Encadenada"):** `esMisionEncadenada`, `distanciaMisionCeldas` (default 50), `materialesRecompensa[]` + `cantidadMateriales` (min/max), `notaSiguiente` (NotaData de la siguiente de la cadena; null = última) y `prefabNotaFisica` (prefab con `NotaMundo` al que se le asigna el data en runtime). El **cofre** reusa el campo `prefabCofre` existente.
*   **Punto inicial = donde SPAWNEA la nota:** `NotaMundo.Start` captura su celda y llama `WorldGenerator.RegistrarMisionEncadenada(data, celdaInicial)`. Idempotente por NotaData (si spawnean 2 copias, la primera fija la misión).
*   **Destino determinista:** hash de semilla+título+celda inicial → dirección aleatoria (ángulo horizontal + Y ±5) a `distanciaMisionCeldas`. En **modo islas** el objetivo se ajusta primero al centro de la isla de la región destino (si no, caería en el vacío). Luego `BuscarCeldaConPiso` busca la celda caminable (vacía con piso debajo) más cercana en cáscaras Chebyshev (radio ≤15; fallback = el objetivo pelado).
*   **Spawn de la recompensa:** el destino queda registrado en `misionesEncadenadasPorCelda`; cuando `GenerarCelda` genera esa celda, spawnea **cofre + materiales sueltos esparcidos + la siguiente nota** (determinista por celda). Si la celda ya existía al registrar (raro), spawnea al instante. La siguiente nota, al activarse, registra SU propia misión → la cadena se extiende sola con la exploración.
*   **Navegación:** al leer una nota de misión, `NotaPanelUI` agrega SIEMPRE al contenido `>> DESTINO: Celda x,y,z` y `>> ACTUAL: Celda x,y,z` — y como `Mostrar()` se llama cada frame mientras se sostiene Click Der, la coordenada actual se **refresca en vivo**. Sin beacon (por ahora): navegación manual con el HUD, muy BLAME!.

---

## 9.5. Tooltip de Objeto Apuntado (`InfoObjetoUI.cs`)
Panel **fijo en pantalla** (estilo survival: Subnautica / The Long Dark) que muestra la info del objeto que el jugador mira. Reemplaza al `TooltipManager` flotante para la info de items, que dependía del modo del Canvas (se rompía en Screen Space - Camera al calcular `WorldToScreenPoint` sobre un canvas no-Overlay).
*   **Singleton `InfoObjetoUI.Instance`.** Solo enciende/apaga un `panel` y pinta un `TMP_Text` — el panel se posiciona a mano donde el diseñador quiera. Funciona con cualquier Canvas.
*   **Se llama cada frame** mientras `ManoJugador` apunta a un `InventoryItem` (mochila cerrada, sin item en mano); un timer (`tiempoOcultar`, con `Time.unscaledDeltaTime` para andar en pausa) lo apaga solo al dejar de mirar.
*   El `TooltipManager` original sigue vivo para `PuertaInteractiva`, `SistemaRapel` y `BotonMundoInteractivo` (tooltips world-space de interacción).
*   **Modo Flotante opcional (`flotante`, default OFF):** `Mostrar(mensaje, Transform objetivo = null)` acepta un `objetivo` opcional; si `flotante` está activo, reposiciona el panel sobre él con `Camera.WorldToScreenPoint` (mismo mecanismo que `TooltipManager`, incluida la convención del hijo `PuntoUI` para la altura). **Solo es seguro en Canvas Screen Space - Overlay** — en Screen Space - Camera, `screenPos` son coordenadas de pixel pero el RectTransform espera unidades de mundo del canvas, y el panel se va lejos de pantalla (la misma falla que tuvo el `TooltipManager` original en el Canvas del menú de pausa). `ManoJugador` ya pasa el `transform` del objetivo en las 3 llamadas (item, nota, objeto en inspección) — activar el toggle es lo único que hace falta para que empiece a flotar.

## 9.55a. Linterna (`Linterna.cs`)
Una sola tecla (**F**, antes Q — se liberó Q para la Interfaz del Brazo, ver §17.1) con DOS gestos distintos, resueltos con `GetKeyUp` (el toggle se decide recién al soltar):
*   **Tap** (apretar y soltar sin scrollear) → prende/apaga (`Alternar`).
*   **Mantener + rueda del mouse** → ajusta `Light.intensity` (clamp `intensidadMin/Max`, paso `velocidadAjusteIntensidad`) SIN togglear — se trackea `huboScrollEnEsteHold` durante el hold y se cancela el toggle si hubo scroll. Expone `AjustandoIntensidad` (bool) e `IntensidadActual` (float) como hooks para el futuro slider/CanvasGroup (pendiente: el usuario todavía no armó esa UI).
*   **Toggle vía `Light.enabled`, NUNCA `GameObject.SetActive`:** si `linternaObjeto` quedara vacío (fallback = el propio GameObject del script) y arrancara apagada, un `SetActive(false)` se habría desactivado a sí misma — Q habría dejado de responder para siempre. `SetActive` queda solo de fallback si el objeto no tiene componente `Light`.
*   Sonido opcional (`fuenteSonido` + `sonidoEncender/Apagar`). Antes era solo un GameObject de Light suelto en la Hierarchy (bajo Main Camera) sin ningún script controlándolo.

## 9.55. Loot embebido en prefabs (`PuntoSpawnItem.cs`)
Alternativa a `RoomData.lootPosible` para loot **atado a un mueble específico** (cajón, locker, estante): en vez de una tabla central, el punto de spawn vive **dentro del prefab** con su propia lista de candidatos.
*   **`PuntoSpawnItem` (componente, se pone en un hijo del prefab):** `itemsPosibles[]` (prefab + probabilidad, mismo criterio que `RoomData.LootEntry` — primer candidato que pase su roll gana) y `probabilidadSpawn` (chance de que el punto genere algo en absoluto).
*   **`WorldGenerator.ProcesarPuntosSpawnItem(instancia, celda)`:** se llama **inmediatamente después** de cualquier `Instantiate` de un prefab que pueda contener `PuntoSpawnItem` en su jerarquía. Busca todos los puntos con `GetComponentsInChildren`, resuelve cada uno, instancia el elegido en la posición/rotación exacta del punto, y **desactiva el marcador** (nunca se ve en juego).
*   **⚠️ NO es automático — hay que engancharlo a mano en cada punto de instanciación.** No hay ningún mecanismo que detecte solo "este prefab recién instanciado podría tener un `PuntoSpawnItem` adentro"; cada caller tiene que acordarse de llamarlo. **Bug real que costó una sesión (2026-09-02):** el `goCentro` de las viviendas lo llama desde siempre, pero **las pasarelas nunca lo llamaron** — un `PuntoSpawnItem` bien puesto y bien asignado dentro de `Pasarela.Recta` (para un palet) simplemente no hacía nada, sin ningún warning ni error, porque nadie iba a buscarlo. Si en el futuro un `PuntoSpawnItem` "no funciona" y está bien configurado, **lo primero a revisar es si el punto de instanciación de ESE prefab llama a `ProcesarPuntosSpawnItem`**, no el propio marcador.
    *   Wireado hoy: `prefabsCentrales` de `RoomData` (vivienda), y **todas las piezas de pasarela** vía el punto único `InstanciarYRegistrarPasarelaRotacion` (recta, tapa de fin, escalera, L, T — las cinco por el mismo hook).
*   **⚠️ Regla de determinismo:** debe llamarse mientras el `Random.State` de la celda ya está sembrado (`Random.InitState(hash)` del caller) — nunca crea su propio hash **salvo que el llamador no tenga ninguno sembrado en ese punto** (caso de las pasarelas: cada roll de su cadena de decisión restaura su propio estado apenas termina, así que `InstanciarYRegistrarPasarelaRotacion` siembra un hash propio por posición — `semillaDelMundo + pos redondeada × primos` — antes de llamar y lo restaura después).
*   **Recursivo desde 2026-09-02:** el ítem que instancia (ej. un palet) se vuelve a pasar por `ProcesarPuntosSpawnItem` antes de agregarlo a `mundoChunks` — si ESE prefab tiene sus propios `PuntoSpawnItem`, se resuelven en la misma pasada, encadenando (pasarela → palet → lo que tenga el palet adentro → ...) sin que cada eslabón necesite su propio hash: todos comparten el mismo `Random.State` ya sembrado, así que la cadena entera sigue siendo determinista por semilla sin importar cuán profunda sea.
*   **Cuándo usar cada sistema:** `RoomData.lootPosible` para loot genérico de la sala (aparece en cualquier spawn point marcado por nombre `"spawnpoint"/"lootspawn"`); `PuntoSpawnItem` para loot curado a un mueble específico (ej. "este locker SOLO tira ropa", o un palet en el medio de una pasarela).

## 9.6. Codex de Descubrimientos (`RegistroDescubrimientos.cs`, `BitacoraUI.cs`)
Sistema de "bestiario de objetos": el jugador solo **conoce** un tipo de item una vez que lo **toma** al menos una vez.
*   **`RegistroDescubrimientos` (estático):** `HashSet<ItemData>` de descubiertos (lookup O(1)) + `List<ItemData>` en orden de descubrimiento (bitácora). Persiste entre escenas (como `EstadisticasRun`), no entre cierres del juego. `Descubrir(data)` devuelve true solo si era nuevo y dispara `OnNuevoDescubrimiento` (para toasts / repintar). `EstaDescubierto(data)`, `Bitacora`, `Total`, `Reset()`.
*   **Descubrimiento al INSPECCIONAR** (no al agarrar): ver §2 "Inspección / Análisis". Sostener el objeto y mantener **Click Der** lo levanta al centro de la mira → eso lo descubre (`RegistroDescubrimientos.Descubrir`). Agarrar solo no descubre — así el ítem queda "Desconocido" hasta que lo analizás deliberadamente.
*   **"Desconocido" hasta descubrir:** el tooltip de objeto (`InfoObjetoUI`) muestra `"Desconocido\n???"` si `!EstaDescubierto`, y los datos reales (`nombre` + `peso`) una vez descubierto.
*   **`ItemData.categoria` (`CategoriaItem`, enum manual):** `Chatarra / Consumible / Modulo / Herramienta / Documento / Otro`. Se asigna a mano por asset — no hay auto-detección por subclase. Es la base de las pestañas de la Bitácora.
*   **`BitacoraUI`:** ventana con **pestañas** (array `pestanas[]`, cada una con su `Button` + una `CategoriaItem` a filtrar, o marcada `esTodos` para mostrar todo junto). Lista los descubrimientos de la pestaña activa en orden (`• Nombre [Material]  X.X kg`), con `txtContador` opcional (`Descubiertos: N`, total global). Clickear un botón de pestaña (`SeleccionarPestana`) tiñe su `Image` con `colorPestanaActiva` (las demás con `colorPestanaInactiva`) y repinta el listado filtrado. Se **abre/cierra con una tecla** (`tecla`, default **B**) alternando un GameObject `ventana`; libera el cursor y bloquea `MouseLook` mientras está abierta. El script va en un objeto **siempre activo** (ej. el Canvas), no en el panel que apaga (un objeto inactivo no corre `Update`). Se repinta al abrirse y en caliente si se descubre algo con la bitácora visible (suscrito a `OnNuevoDescubrimiento`), respetando la pestaña seleccionada.
*   *Pendiente / mejora:* entradas "bloqueadas" (`???`) para items aún no descubiertos (requiere un master list de `ItemData`, ej. `Resources.LoadAll<ItemData>`); toast de "¡Nuevo descubrimiento!" enganchando `OnNuevoDescubrimiento`.

---

## 10. Drones del Mundo (`DronVagabundo.cs`, `EnjambreDrones.cs`)
Pequeños drones que vagan **por el exterior de la mega-estructura** dando sensación de mundo vivo. No están atados al jugador — son del mundo.

### 10.1. `DronVagabundo` (lógica individual)
*   **Movimiento steering, sin pathfinding:** elige waypoints en una esfera alrededor de su PROPIA posición (`radioVagar`, `rangoY`), acelera hacia ellos con `aceleracion` cappeada a `velocidadElegida` (random entre `velocidadMin/Max` por instancia → swarm con variedad).
*   **Avoidance:** `Physics.Linecast` antes de cada waypoint. Si choca con estructura, descarta y reintenta. Hasta `intentosWaypoint` veces.
*   **Estado Trabajar:** con `chanceTrabajar` (25%) al llegar a un waypoint, hace `Raycast` lateral buscando una pared. Si encuentra, se posiciona a `distanciaPared` (2m) de la pared, se queda quieto entre `tiempoTrabajarMin/Max` segundos orientado hacia ella. Activa `efectoTrabajando` (GameObject opcional, ej. partículas de chispa).
*   **Audio 3D:** `audioZumbido` (loop, mientras vuela) y `audioTrabajar` (loop, mientras trabaja). El script fuerza `spatialBlend=1` y `loop=true`, el resto (volume, distancias, rolloff) se configura desde el `AudioSource`.

### 10.2. `EnjambreDrones` (manager)
*   Mantiene hasta `cantidadObjetivo` (15) drones simultáneos en una corona alrededor del jugador.
*   **Spawn en exterior real:** `Physics.CheckSphere` para no aparecer dentro de paredes + `Raycast.up` para verificar sin techo + `Raycast.down` para no spawnear sobre un pasillo. Solo posiciones realmente "afuera".
*   **Despawn:** drones que se alejan más de `distanciaDespawn` (250m) se destruyen.

---

## 11. Desprendimientos (`SistemaDesprendimientos.cs`)
Cada `intervaloMin..Max` segundos (20-60s), toma una pieza exterior del mundo y la **desprende físicamente** — sensación de mega-estructura abandonada que se está cayendo a pedazos.

*   **⚠️ Timing arreglado (2026-08-21) — "siempre caía algo al iniciar":** el temporizador arrancaba apenas existían las referencias, así que el rato en menú/intro contaba como espera y al empezar a jugar ya estaba vencido → derrumbe de bienvenida garantizado. Ahora: (1) el reloj **no corre hasta `WorldGenerator.MundoListo`**, y se reinicia en cada transición a listo (partida nueva incluida); (2) **gracia inicial** aparte (`esperaInicialMin/Max`, 45-150s) antes de la primera pieza; (3) **`probabilidadDesprendimiento`** (0.6) por turno vencido — el tiro fallido consume su turno y reprograma, rompiendo el ritmo de metrónomo (a veces dos seguidos, a veces silencio largo; en 1 vuelve al reloj). Bonus: `BuscarReferencias()` reintenta desde `Update` — antes si el `Awake` del WG corría después, `Instance` quedaba null y el sistema moría en silencio para siempre.

*   **Selección:** llama a `WorldGenerator.TomarPiezaExteriorAleatoria(centro, radio, palabrasClave)`. Las palabras clave default identifican piezas exteriores por nombre: `_externo`, `ParedExterior`, `Pilar`, `AristaExterior`. El método itera celdas en `mundoChunks` dentro del radio, filtra por nombre, descarta del registro de chunks (la burbuja ya no la apaga) y la despega del transform del WG.
*   **Físicas:** agrega `Rigidbody` (`masa = 50`) + asegura `Collider`. Le aplica `linearVelocity` random horizontal con componente Y entre `-0.3` y `0` (nunca hacia arriba, como pidió el diseño) y `angularVelocity` random para que gire.
*   **Limpieza:** `Destroy(pieza, tiempoVida)` después de 5s.
*   La pieza desprendida **no se regenera** — el chunk piensa que sigue ahí. Para el demo no importa, cae al void igual.

---

## 12. Mira con Peso (`MouseLook.suavizadoMira`)
Filtro pasa-bajos sobre el input del mouse para que la cámara se sienta levemente pesada, simulando peso del casco. `mouseXSuave = Lerp(mouseXSuave, raw, k)` donde `k = 1 - exp(-Δt/suavizadoMira)`. Default `suavizadoMira = 0.05s` (perceptible pero sin lag). Si se setea a 0, el input pasa sin filtro.

---

## 13. Grain por UI (`GrainUI.cs`)
Granulado tipo TV antigua **cuando hay UI activa**, nítido cuando no. Implementado como toggle de un `Volume` con `Film Grain` override de HDRP.

*   El script vigila un array `uisAVigilar` de GameObjects. Si CUALQUIERA está `activeInHierarchy`, enciende el `grainVolume`. Si ninguno → apagado.
*   **Sinergia con el HUD del casco:** el `hudContenedor` del `PlayerData` se agrega típicamente a la lista vigilada. Resultado:
    *   Batería > 0 → HUD activo → grain on (estás mirando a través de la pantalla del casco)
    *   Batería = 0 → HUD inactivo → grain off (casco muerto, ves directo)
    *   Mochila / Notas / Menú abierto con batería 0 → esos GameObjects siguen vigilados → grain vuelve a aparecer porque hay UI activa
*   Default arrancan apagados (el `grainVolume` en sí está `SetActive(false)` al inicio).

---

## 14. Mecánica de Destrucción de Paredes (Soplete/Corte de Plasma)
Permite al jugador cortar y abrir agujeros en cualquier panel o pared exterior/interior del mapa en runtime para crear atajos o explorar habitaciones.

*   **Persistencia de Paredes Rotas (`WorldGenerator.paredesRotas`)**:
    *   Mantiene un registro global estático (`HashSet<string>`) con claves basadas en las coordenadas y dirección de la pared (ej: `x_y_z_direccion`).
    *   Si una pared es cortada, se registra en esta estructura. El `WorldGenerator` omitirá su instanciación en futuras cargas de chunks.
*   **Hoyo Visual Dinámico (Shader de Recorte / Clipping Shader)**:
    *   El material de las paredes usará un shader con parámetros de descarte (`_CutPosition`, `_CutRadius`). Al cortar la pared en un punto específico (mediante raycast de la herramienta del jugador), el shader descartará los píxeles dentro de la esfera de corte en tiempo real.
*   **Paso Físico y Reemplazo de Colisionador**:
    *   Al completarse el corte, el colisionador original (`BoxCollider`) de la pared se desactiva.
    *   Se instanciarán de forma dinámica colisionadores perimetrales (4 colliders en cruz: arriba, abajo, izquierda, derecha) alrededor del hoyo para formar el marco colisionable físico y permitir que el jugador atraviese el agujero.

---

## 15. Pared/Puerta Interactiva (`PuertaInteractiva.cs`)
Permite crear pasajes, compuertas o secciones de pared que pueden ser abiertas o cerradas por el jugador.

*   **Doble Sistema de Animación**:
    *   **Animación Procedimental (Fallback)**: Si no se asigna un componente `Animator`, la puerta realiza un desplazamiento lineal (`Vector3.MoveTowards` en una coroutine) basado en un `offsetAbierto` local y una `velocidadMovimiento` configurables.
    *   **Animación Exportada (Animator)**: Si se asigna un `Animator` (ej. importado desde Blender o creado en Unity), el script controla el estado de apertura reproduciendo las animaciones correspondientes mediante la modificación de un parámetro booleano (`parametroBool`, por defecto `"IsOpen"`).
*   **Interacción Directa y World Canvas**:
    *   Soporta interacción directa del jugador al apuntarle con la cámara en primera persona dentro de un `alcanceInteraccion` y presionar **[E]** (mostrando el tooltip dinámico correspondiente).
    *   Permite integrarse con botones de un Canvas en World Space llamando a su método público `AlternarPuerta()`.
    *   **Feedback Visual e Interfaz**: Actualiza de forma automática textos de estado (ej: "ACCESO ABIERTO" / "SISTEMA CERRADO") e imágenes de fondo (indicadores rojo/verde) en el Canvas de la compuerta.
    *   **Feedback Sonoro**: Reproduce efectos de sonido (`sonidoAbrir` / `sonidoCerrar`) mediante un `AudioSource` local.
*   **Atajo de Pruebas**: En tiempo de desarrollo, se puede presionar la tecla **[2]** para alternar de forma instantánea el estado de la compuerta desde cualquier distancia.
*   **Carteles de Peligro (hologramas):** array `cartelesPeligro` (GameObjects, típicamente Quads con el shader `LineasAnimadas`/HologramaPeligro — columnas animadas con scroll horizontal, ver nota abajo). Inician **desactivados** (`Start`), se **encienden al iniciar** cualquier animación (abrir o cerrar) y se **apagan al terminar**: en modo procedimental al final de `AnimarMovimiento`; con Animator vía coroutine `EsperarAnimator` (espera un frame + `GetCurrentAnimatorStateInfo(0).length`). Señal visual de "puerta en movimiento".

> **Nota — Shader Graph `LineasAnimadas` (cinta de peligro holográfica):** textura de columnas (Texture Type **Default**, NO Sprite — como Sprite el Shader Graph la lee mal; Point filter, sin mipmaps, sin compresión) → `UV + (Time × velocidad en Vector2 X)` → `Add` → `Sample Texture 2D` → `Split` canal **G** (máscara de columnas) → G × Color HDR (Multiply) → `Emission`; G → `Alpha`. Surface Type **Transparent**. La velocidad se controla con el Multiply entre Time y el Vector2 (0.1 lento / 1 normal). Se usa en los carteles de las puertas interactivas.

---

## 16. Botón de Interacción en el Mundo / Diegético (`BotonMundoInteractivo.cs`)
Permite interactuar con elementos dentro de un Canvas en World Space (pantallas de terminales, teclados, consolas) de manera totalmente diegética sin pausar la partida ni liberar la cámara.

*   **Colisión en Canvas**: En lugar de depender de raycasts de mouse tradicionales, se añade un `BoxCollider` a los elementos del Canvas en World Space.
*   **Detección por Mirada**: El script realiza un raycast desde el centro de la cámara del jugador. Si el rayo choca con el colisionador del botón dentro del `alcanceInteraccion`, se activa y muestra el tooltip interactivo centralizado (`TooltipManager`).
*   **Activación por Eventos**: Al presionar la tecla **[E]** apuntando al botón, se ejecuta el evento de Unity configurado (`alPresionar`), permitiendo conectar interacciones lógicas de manera visual en el inspector (como alternar el estado de compuertas, activar luces, etc.).

---

## 17. Sistema de Tirolina (`SistemaTirolina.cs`, `Tirolina.cs`, `ProyectilTirolina.cs`)
Grappling gun de 2 disparos físicos (no raycast instantáneo): el primer proyectil ancla el DESTINO, el segundo ancla TU extremo (el "lugar seguro"), y entre ambos puntos se arma una tirolina tensa por la que te deslizas.

*   **Modo Apuntado (Click Derecho, mano vacía):** mantener Click Derecho **con la mano vacía** (`ManoJugador.ItemEnMano == null`) entra en modo apuntado — la cámara hace zoom (Lerp de `fovNormal` a `fovApuntado`, default 45°). `ManoJugador` no intenta agarrar objetos mientras este modo está activo (`SistemaTirolina.EnModoApuntado` gatea el `Input.GetMouseButtonDown(0)` de agarre) — ambos sistemas reaccionan al mismo gesto (mano vacía) y se coordinan explícitamente para no pisarse.
*   **Disparo físico (`ProyectilTirolina.cs`):** Click Izquierdo (en modo apuntado) instancia un proyectil con `Rigidbody` real — vuela en arco por gravedad, `CollisionDetectionMode.ContinuousDynamic` + interpolación para no atravesar paredes finas. Se clava en **cualquier collider sólido** que choque (`OnCollisionEnter`, sin filtro de layer — es físico de verdad, no hay raycast mágico). Ignora colisión con el jugador (`Physics.IgnoreCollision`). Si no choca nada en `tiempoVidaMax` (6s), se autodestruye y avisa "fallo" vía callback (para no dejar el arma trabada esperando un impacto que nunca llega).
*   **Preview con arco balístico real:** el `LineRenderer` de apuntado NO es una línea recta — simula la misma trayectoria (misma velocidad + `Physics.gravity`) con `Physics.Linecast` paso a paso, coloreando verde/rojo según si "pegaría" dentro de `alcanceMaximo`. Un `TMP_Text` (`textoDistancia`) muestra la distancia real jugador→punto de impacto, mismo color.
*   **Munición: "fallar no gasta, 2 fallos seguidos sí" (diseño explícito del usuario):** NO es un cargador de N balas que se resta por disparo. Se trackea `fallosConsecutivos`: un disparo que falla (no se clava en nada) suma 1 a la racha, pero es reintentable sin costo — recién al **2do fallo consecutivo sin haber clavado nada en el medio** el cargador queda vacío y hay que recargar (**R**). Cualquier anclaje exitoso (1er o 2do disparo) resetea la racha a 0. El slider de munición muestra `disparosPorCargador - fallosConsecutivos` (el margen de fallo restante, no "balas gastadas").
*   **Límite de distancia real (post-físico):** como el proyectil vuela libre, puede llegar más lejos de lo que la cuerda "alcanza". Al clavarse el 2do proyectil, se mide la distancia REAL entre ambos anclajes; si supera `alcanceMaximo`, la tirolina **no se arma** (se descarta, sin penalizar munición — técnicamente sí clavaste algo) y el 1er anclaje se mantiene para reintentar el 2do disparo más cerca.
*   **Costo de batería (`PlayerData.energySeconds`):** cada disparo (acierte o falle) cuesta `costoEnergiaPorDisparo` (default 20s) del mismo recurso de batería que la linterna — una tirolina perfecta sin fallar cuesta 40s. Si no queda batería suficiente, el disparo simplemente no sale (ni se instancia ni se descuenta nada). Dispara el popup `PlayerData.MostrarPopupEnergia(-20)` (ver §1).
*   **Recorrido (`Tirolina.cs` + `Engancharse`):** mirar la cuerda a `distanciaEnganche` (5m) y presionar **E** (tooltip "[E] Tirolina") te engancha en el extremo más cercano; te deslizas hacia el extremo lejano con **gravedad real según la pendiente** (`dir.y` negativo = acelera en bajada) + una **velocidad mínima constante** para poder cruzar tramos horizontales o levemente cuesta arriba (`velocidadMinima`, `velocidadMaxima`, `factorGravedad`). **Space** suelta antes de llegar. Al llegar al final, se desengancha solo (reactiva gravedad para aterrizar). Bloquea `PlayerMovement.movementLocked` + `Rigidbody.isKinematic` mientras dura, mismo patrón que `SistemaRapel`.
*   **UI reactiva al modo apuntado:** al entrar/salir del modo apuntado se oculta/muestra el punto de mira normal, se oculta/muestra el slider de la linterna, y se muestra/oculta un punto de mira específico de la tirolina — todo vía referencias `GameObject` directas en el Inspector (`miraNormal`, `sliderLinterna`, `miraTirolina`).
*   **Bloqueada mientras sostienes una nota (`LectorNotas`):** para sostener una nota hay que **mantener** Click Izquierdo (soltarlo la deja caer, no es un toggle) — eso deja Click Izquierdo "ocupado" y sin poder generar el click nuevo que `Disparar()` necesita, produciendo un bug confuso (apuntas pero no puedes disparar). Fix: `manoVacia` ahora también exige `!lectorNotas.SostieneNota`, igual que ya exige `ManoJugador.ItemEnMano == null` — con una nota en mano, ni siquiera entras en modo apuntado. `lectorNotas` se auto-detecta como el resto de referencias cruzadas del Player.
*   **Iconos de munición físicos (`iconosMunicion`, 2026-08-21):** array de GameObjects (los PNG `Municion_1/2` del arma) que se **apagan en el orden del array** a medida que se gasta el cargador, y se reencienden al recargar o clavar. Actualizados en `ActualizarMunicionUI()` (ex `ActualizarSliderMunicion`), el único punto que ya tocaban los 4 call sites — slider e iconos no pueden desincronizarse. ⚠️ Reflejan el **margen de fallo** (`fallosConsecutivos`), consistente con el diseño "fallar gasta, clavar recupera": clavar un anclaje re-enciende los iconos apagados.
*   **Control manual en la cuerda (W/S + mouse libre, 2026-08-21):** `rideVel` pasó a ser velocidad **con signo** a lo largo de la cuerda (antes clamp `[velocidadMinima, velocidadMaxima]` = siempre positiva, imposible frenar o volver). Por frame: gravedad por pendiente (siempre, retroceder cuesta arriba pesa) + impulso de `Input.GetAxisRaw("Vertical")` × `aceleracionEntrada` (25) + `rozamientoSinEntrada` (8) si no hay input + clamp ±`velocidadMaxima`. El **piso de velocidad solo aplica en el sentido que empujás** (sin input no hay mínimo). Extremos asimétricos a propósito: llegar al lejano te suelta (como siempre); volver al de partida te deja **colgado** (`rideT=0, rideVel=0`) — si soltara, mantener S te tiraba al vacío. El mouse ya giraba libre (nada tocaba la rotación); no hubo que cambiarlo.
*   **Enganche donde agarrás (2026-08-21):** `Engancharse` hacía `rideT = 0` → siempre te teletransportaba al extremo cercano. Ahora recibe el `hit.point` del raycast de `DetectarEnganche` (el punto exacto de la cuerda que mirabas) y lo **proyecta sobre el segmento** origen→destino (`Dot/|delta|²`, clamp 0..1) para el `rideT` real. Arrancás con `rideVel = 0`: el movimiento sale de la pendiente (bajada = deslizás, subida = retrocedés, plano = quieto hasta tocar W/S) — se quitó el empujón inicial de `velocidadMinima`.

---

## 17.1. Interfaz del Brazo — "Ver Stats" (`InterfazBrazo.cs`)
Gesto diegético estilo Pip-Boy: mantener **Q** sube el brazo del modelo del jugador y muestra un panel de stats en **World Space** pegado al antebrazo (no HUD tradicional en pantalla).

*   **Animator por Parameters + Transitions (NO `Animator.Play` manual):** se probó primero controlar el clip manualmente escaneando el tiempo normalizado frame a frame (`Animator.Play(estado, capa, tiempoNormalizado)` + `animator.speed = 0`) — funcionaba pero generaba bugs difíciles de diagnosticar (el Animator "secuestrado" todo el tiempo impedía que el Idle se reprodujera solo). La versión final es mucho más simple y robusta: un **Bool Parameter** (`MirandoInterfaz`) + 2 transiciones normales del Animator Controller (`Player_idle → SubirBrazo` con condición `true`, la inversa con `false`, ambas **sin Exit Time**). El script solo hace `animator.SetBool(nombreParametro, sostenido)` — el Animator maneja todo el blending solo.
*   **⚠️ "Transition Duration (%)" no son segundos:** con **"Fixed Duration" desmarcado**, ese campo es un **porcentaje de la duración normalizada** del state, no segundos reales — un valor chico como `0.25` puede traducirse en una transición larguísima si el state dura varios segundos, sintiéndose "pegajoso"/lento sin que el multiplicador de Speed del clip lo arregle. Con `Transition Duration = 0` la transición es instantánea (snap) — es lo que se usó. Si se quiere algo de suavizado sin este problema, marcar **"Fixed Duration"** (el campo pasa a ser segundos reales) y usar un valor chico tipo `0.1`.
*   **Panel de stats en World Space:** el panel se activa/desactiva junto con el estado sostenido/soltado de Q (`panelInterfaz.SetActive(sostenido)`). Al ser World Space (no Screen Space Overlay), reacciona al FOV de la cámara igual que cualquier objeto 3D — es lo correcto para algo diegético (no se "compensa" el FOV, se comporta como el resto del mundo). Advertencia de diseño: en FOVs muy anchos (110°+), un panel pegado al antebrazo (lejos del centro del encuadre) sufre distorsión tipo ojo de pez — conviene acotar el rango del slider de FOV en Opciones (ej. 60-100) para no romper la lectura del panel.
*   **Props rígidos pegados a huesos (visor/pantalla): NO se skinnean en Blender, se parentan en Unity.** Se intentó (y falló, o se volvió muy complicado) todo el camino de Blender: parentesco `Bone` (Object→Bone) que no exporta limpio a FBX (Unity termina con referencias de huesos duplicadas — sigue posición pero no rotación), Vertex Groups + Armature Modifier (requiere pintar pesos a mano y el resultado seguía dando problemas), Constraints (Copy Location/Rotation, difícil de ajustar y de hornear bien). **La solución que sí funcionó:** en Unity, arrastrar el prop (ya como GameObject separado, sin nada de esto en Blender) como **hijo directo del GameObject del hueso específico** dentro de la jerarquía del Animator (ej. `mixamorig:LeftForeArm`), y ajustar su Transform local una sola vez a ojo. Al ser hijo real de ese Transform, sigue posición Y rotación en CUALQUIER animación automáticamente, sin re-exportar nada por cada clip nuevo. **Regla general para futuros props (armas, gadgets, accesorios): siempre por este camino, nunca por Blender.**

---

## 17.2. Shader del Visor — CRT / Aberración Cromática (`ShaderGraph_Stats_Background.shadergraph`)
Fondo procedural del panel de stats del brazo (§17.1): líneas verticales animadas + fringing de color (aberración cromática) + estática/ruido intermitente + parpadeo, estilo pantalla CRT vieja. Es un **Canvas Shader Graph** (target `HDCanvasSubTarget`) pensado para vivir en el Canvas World Space de `panelInterfaz`, no en un `MeshRenderer` 3D suelto.

*   **Patrón de líneas base:** `UV → Split (un solo canal, ej. R/U) → Multiply (× "Conteo Lineas") → Add (+ Time × "Movimiento Lineas", para el scroll) → Fraction` — el Fraction es lo que repite el degradado en líneas, en vez de un solo gradiente.
*   **Aberración cromática (fringing en los bordes):** la técnica NO es samplear una textura con offset (no hay textura) — es triplicar el cálculo del patrón de líneas, una vez por canal de color, cada uno partiendo de un UV levemente desplazado:
    *   Canal R: `Add(UV, OffsetACHRO)` → Split → Multiply → Fraction
    *   Canal G: `UV` original (sin offset) → Split → Multiply → Fraction
    *   Canal B: `Subtract(UV, OffsetACHRO)` → Split → Multiply → Fraction
    *   Los 3 resultados (floats sueltos) se juntan con **Combine** (R/G/B). **Los 3 Split DEBEN leer el mismo canal** (mismo R o mismo G) — si uno de los 3 usa un canal distinto (ej. G en vez de R), ese canal genera líneas en el eje perpendicular a los otros 2, y el resultado combinado se ve como una cuadrícula/rejilla en vez de líneas con fringing sutil.
    *   `OffsetACHRO` debe ser un valor MUY chico (`~0.0005-0.002`) — con valores grandes (ej. `0.05`) las 3 líneas quedan tan separadas que se ven como franjas de colores sólidos completos ("test de impresora"), no como un fringing sutil en los bordes.
*   **Step (afilar) + Lerp con Noise + parpadeo:** `Combine → Step (Edge = umbral, ej. 0.5) → Lerp(A=Step, B=Simple Noise, T="Sonido Conteo") → Multiply(× parpadeo, un Remap de un Sine(Time) mapeado a un rango que NO toca 0, para que no se apague del todo) → Multiply(× Color) → Base Color + Emission`. El Lerp con Noise simula que la señal a veces "se rompe" en estática pura según `Sonido Conteo` (0-1).
*   **⚠️ Bug de precisión de floats (Half vs Single):** con `OffsetACHRO` tan chico, si el grafo (o algún nodo de la cadena de aberración) usa precisión **Half** (16 bits) en vez de **Single** (32 bits), el offset se redondea a 0 en la mayoría del rango del UV y solo sobrevive en zonas puntuales — el fringing de color queda concentrado en un área en vez de parejo en todas las líneas. Fix: click derecho en el canvas del grafo (o sobre los nodos seleccionados) → **Precision → Single**. Aplica a TODA la cadena de offset (Add, Subtract, Split×3, Multiply×3, Fraction×3, Combine).
*   **⚠️ El "Default Value" del grafo NO actualiza materiales ya creados:** las propiedades con `Scope: Per Material` (como `Conteo Lineas`) guardan su propio valor por separado en CADA Material instanciado. Cambiar el "Default Value" de la propiedad dentro del Shader Graph (panel Node Settings → Property) **no** se propaga a un Material que ya existe — hay que cambiar el valor directo en el **Inspector del Material** (el mismo lugar donde se ven `Color`, `OffsetACHRO`, etc. como campos editables). Confundir estos dos lugares hace parecer que "el grafo no refleja los cambios" cuando en realidad sí compiló bien, solo que el Material tiene su propio override cacheado.
*   **Scene Color de un tutorial de YouTube, sacado:** el grafo original tenía un nodo `Scene Color` (muestrea lo que está renderizado DETRÁS del objeto, tipo efecto de vidrio/refracción) mezclado en el color final — como el mundo tiene ambiente muy azul, esto teñía el panel de azul sin control. Se sacó y se reemplazó por la propiedad `Color` fija — un elemento diegético como este no debería depender de qué haya alrededor.
*   **Look "e-ink" (implementado, `EInk_Include.hlsl`):** reemplaza el look CRT/neón por un panel blanco-hueso tipo Kindle gastado. Vive como **Custom Function Node** (modo File) en el mismo grafo, no reescribe el patrón de líneas de arriba — es un nodo aparte que alimenta el mismo Base Color.
    *   **Pixelado + dither ordenado:** coordenadas cuantizadas a una grilla falsa (`ScreenPixels`, ej. 96×64) y una matriz Bayer 4×4 (`EInk_Bayer4x4`) cuantiza la luminancia en `DitherLevels` escalones (3-4 = look auténtico). **Ojo:** con `Luminance` perfectamente plana (ej. 0.5 fijo) el dither se ve como un tablero de ajedrez durísimo — hay que romper la uniformidad con una entrada de luminancia que varíe (Simple Noise de escala baja `~2-3` remapeado a un rango alto tipo `0.85-1.0`, para que el panel quede casi blanco con manchas de desgaste apenas perceptibles, no un checker 50/50).
    *   **Colores:** `BoneColor` (blanco tibio, ej. `0.94, 0.93, 0.89`) e `InkColor` (gris cálido suave, ej. `0.30, 0.27, 0.24` — NO negro puro, para que cualquier mota del dither se vea como mancha vieja). Colores demasiado cercanos entre canales (ej. diferencia de solo `~0.1`) se leen como gris frío en vez de hueso — conviene una separación de canal más marcada.
    *   **Sin Emission**, tal como se planteaba en la nota original — todo a Base Color.
    *   **Glitches "super chicos":** franjas horizontales de ~3px que tiemblan 1px de vez en cuando (`GlitchSpeed` ~1.5, `GlitchAmount` bajo ~0.05-0.15 — cuanto más bajo, más raro que aparezcan), vía hash determinista por fila+bucket de tiempo. La salida `GlitchMask` queda disponible sin usar por si a futuro se quiere un flash de Emission u otro efecto extra en el momento del glitch — **no conectar a Alpha** (dejaría el panel invisible casi todo el tiempo, solo "apareciendo" en el instante del glitch).

---

## 18. Centinelas — Vigilancia No Persistente (`Centinela.cs`, `RondaCentinelas.cs`, `PuntoInteresCentinela.cs`, `AlertaCentinelas.cs`)
Vigías que **no viven en el mundo permanentemente**: aparecen en **rondas** temporales, hacen su recorrido y se retiran. Premisa de diseño: **el jugador NO es su objetivo prioritario** — andan inspeccionando cosas, y vos sos un problema solo si te acercás demasiado o los atacás. La fantasía es esconderse y dejarlos pasar, no un combate frontal.

*   **FSM manual, sin NavMesh ni frameworks:** mismo estilo que `DronVagabundo` (§10.1) — enum privado de estados, movimiento por `Vector3.MoveTowards` con `aceleracion` cappeada, y `Physics.Linecast`/`Raycast`/`CheckSphere` propios para obstáculos y clasificación interior/exterior. El proyecto no tiene NavMesh horneado (el mundo es procedural e infinito, no habría dónde hornearlo).

### 18.1. `Centinela` (lógica individual)
*   **Estados:** `Inspeccionando` (default) → `Alerta` → `Buscando` → `RetirandoseABase`.
    *   **Inspeccionando:** busca el `PuntoInteresCentinela` de tipo `Inspeccion` más cercano dentro de `radioBusquedaPuntos` (30). Si no hay ninguno, cae al muestreo aleatorio en esfera de `DronVagabundo` (`radioVagar`/`rangoY` + `Linecast` previo). Al llegar se queda quieto `tiempoInspeccionMin..Max` segundos antes de elegir el siguiente. **Acá el jugador le da igual** salvo que dispare la percepción.
    *   **Alerta:** persigue la última posición conocida del jugador a `velocidadAlerta` (6 vs 3 de inspección).
    *   **Buscando:** llegó a la última posición conocida y no lo ve → se queda `tiempoBusquedaMax` (8s) antes de desescalar. Si la ronda ya terminó mientras perseguía, recién ahí se retira (no "se rinde" a mitad de persecución).
    *   **RetirandoseABase:** camina al `PuntoInteresCentinela` de tipo `BaseSalida` más cercano (o su punto de spawn si no hay ninguno) y se autodestruye al llegar.
*   **Dos vías de escalada a `Alerta`:**
    1.  **Percepción pasiva** (`DetectaJugador`, chequeada **por tick de `intervaloPercepcion` = 0.2s, no cada frame**): cono de `anguloVision` (100°) + `distanciaVision` (18) + `Linecast` de línea de visión desde `alturaOjos`. Además, si el jugador está a menos de `distanciaMuyCerca` (3), escala **ignorando cono y LOS** — te tiene encima, no importa que esté mirando para otro lado.
    2.  **`RecibirDano(dano, origenAtaque)`**, público: cualquier arma/trampa lo llama al golpear. Escala instantáneo, sin chequeo de percepción.
*   **⚠️ EL EXTERIOR ES TU VÁLVULA DE ESCAPE (`chanceSeguirExterior`, default 0.15):** cada centinela sortea **al spawnear** si es de los que te siguen afuera. Si no lo es y se encuentra en exterior (misma heurística `EsExterior` que `DronVagabundo`: `CheckSphere` + `Raycast.up`), corta la persecución y pasa a `Buscando` con la mitad del tiempo. Decisión de diseño explícita: **el jugador siempre tiene que poder salir sin problema** — el exterior no los favorece. Se sortea por instancia (no una regla global) para que no sea 100% predecible.

### 18.2. `RondaCentinelas` (manager)
*   **Disparo de ronda por DOS vías combinadas:** temporizador aleatorio (`intervaloRondaMin..Max`, 90-240s) **y** `AlertaCentinelas.OnAlerta`. Si la ronda ya está activa, una alerta no spawnea de más: **extiende** la duración actual.
*   **`cantidadPorRonda` = 2** (tope bajo a propósito: hay poco espacio en los pasillos, 2 centinelas ya llenan un corredor).
*   **Duración y retiro:** `duracionRondaMin..Max` (30-60s). Al vencer llama `IniciarRetiro()` en cada centinela vivo — **no los destruye de golpe**, se van caminando a su base. Es lo que vende que "hicieron su ronda y se fueron", en vez de despawnear en el aire.
*   **⚠️ Reintento rápido si no hay dónde spawnear:** si una ronda no logra instanciar a nadie (típicamente porque el mundo todavía está haciendo streaming), **no** consume el intervalo normal de 90-240s — reprograma a `reintentoSinPasilloSegundos` (2s). Sin esto, un fallo en el frame 0 dejaba el sistema mudo por minutos y parecía que no funcionaba nada.

### 18.3. `PuntoInteresCentinela` y `AlertaCentinelas` (enganches)
*   **`PuntoInteresCentinela`:** componente liviano colocado a mano (pasillos, cerca de consolas/puertas). Se **auto-registra en una lista estática** en `OnEnable`/`OnDisable` — el manager nunca hace `FindObjectsOfType`. Dos tipos: `Inspeccion` (destino de patrulla) y `BaseSalida` (punto de retiro).
*   **`AlertaCentinelas.Disparar(posicion)`:** clase estática con un `event Action<Vector3>`. Es el **hook para sistemas futuros** (ruido, luz, romper algo, objetivo cumplido) para forzar una ronda sin acoplarse al manager. Hoy nadie lo llama — está listo, no inventado.

### 18.4. Modo de prueba: pasillo recto (temporal, `modoPruebaPasillo`)
Andamio para probar el movimiento antes de tener puntos de interés colocados. **Se apaga con un toggle del inspector cuando el sistema definitivo esté armado.**
*   Busca, entre las celdas **actualmente activas** (`WorldGenerator.CeldasActivas`), el **tramo recto de pasillo más largo** reutilizando `WorldGenerator.EsTramoRectoDePasillo` (expuesto como público; ya existía privado para los balcones, §5.2). Cuenta el largo del tramo retrocediendo al inicio y avanzando hasta el final — el WG solo sabía detectar "esta celda es recta", no medir corridas.
*   `teletransportarJugadorAlPasillo`: pone al jugador en un extremo y al centinela **en el otro**, mirándose. Usa `WorldGenerator.TeletransportarJugador` (nuevo método público que replica el cuidado de `PosicionarJugadorEnSpawn`: desactiva `CharacterController` y congela el `Rigidbody` durante el movimiento).
*   `centinelaVieneAlJugador`: en vez de patrullar de ida y vuelta, **proyecta la posición del jugador sobre el eje del pasillo** y camina hacia ese punto. Clave: así se acerca **sin salirse del centro del corredor** aunque el jugador se pegue a una pared.
*   `offsetLateralPasillo`: corrección manual perpendicular al pasillo, para calibrar a ojo si el centro matemático de la celda no coincide con el centro real entre las 4 paredes (pivote de prefab, grosor de pared). Mismo patrón de calibración que `offsetLocalCeldas` en la mochila (§2).
*   Ritmo avanza/pausa con `tiempoAvanceMin..Max` / `tiempoQuietoMin..Max` — el centinela no se desliza uniforme, arranca y para.

### 18.45. Cuartel de Centinelas (`RoomData.esCuartelCentinelas`, agregado 2026-07-28)
El ciclo completo de la ronda ahora tiene casa: **spawn desde el cuartel → patrulla → retiro AL cuartel → descanso → despawn silencioso.**
*   **`RoomData.esCuartelCentinelas`:** flag en el ScriptableObject de sala. Crear un RoomData "Centinelas" con el flag activo en `Assets/Resources/RoomData/` (se auto-registra por el `Awake` del WG, como cualquier sala). La asignación sala↔macro chunk es el hash determinista de siempre — los cuarteles quedan repartidos por el mundo solos.
*   **`WorldGenerator.TryEncontrarSalaCentinela(cerca, radioMacros, out celdaCentral)`:** escanea macro chunks alrededor de una posición buscando tipo 1 cuya RoomData tenga el flag; devuelve la celda central habitable. Determinista (misma `ObtenerRoomDataParaMacro` que la generación).
*   **`WorldGenerator.TryCalcularRutaCeldas(desde, hasta, maxNodos, ruta)`:** **BFS de celdas vacías (6 vecinos, `IsCellEmpty`)** — el "pathfinding" del proyecto. Devuelve la ruta como lista de celdas; el centinela la camina **por los centros de celda** (nunca en línea recta a través de paredes, que era el problema del retiro anterior). Presupuesto `maxNodos` (default 4000 en el centinela) para no colgar el frame si el destino es inalcanzable.
*   **Retiro real, no despawn:** al terminar la ronda el centinela busca cuartel + ruta; si consigue ambos, camina la ruta, y al llegar entra en **`Descansando`** (`tiempoDescansoMin..Max`, 15-40s) y recién ahí se destruye en silencio. **Mientras se retira Y mientras descansa SIGUE percibiendo** — acercate demasiado y escala a Alerta igual. Amenaza pasiva de verdad, no un actor saliendo del escenario. Fallback sin cuartel/sin ruta: comportamiento viejo (línea recta a `BaseSalida` o punto de spawn, despawn al llegar).
*   **Spawn desde el cuartel (`SpawnearRondaClasica`):** la ronda nace en la celda central del cuartel más cercano al jugador (`radioBusquedaCuartelMacros`, 3), con offsets de 2u entre centinelas para no apilarse. Fallback: puntos de interés / burbuja.

### 18.46. Actualización 2026-08-21 — Cuartel garantizado, búsqueda por cáscaras y patrulla por isla
*   **Un cuartel por isla, garantizado y ÚNICO:** ver §5.8 — la generación fuerza un chunk cuartel por isla (cascada núcleo→borde→lo-que-sea) y las RoomData de cuartel **salieron de la ruleta de viviendas** (fix: centinelas aparecían dentro de viviendas comunes que habían salido "cuartel" por el roll ponderado). Los centinelas solo pueden nacer en EL cuartel de la isla.
*   **`TryEncontrarSalaCentinela` reescrito — cáscaras concéntricas:** ya calculaba todo por semilla, pero escaneaba un cubo fijo de radio 3 y se rendía. Ahora recorre cáscaras hacia afuera y **corta en la primera con cuartel** → `radioBusquedaCuartelMacros` pasó de costo fijo a **TECHO** (subirlo no cuesta nada si hay uno cerca; recomendado 8-15 ahora que hay 1 cuartel por isla, no ~1/11 viviendas). Filtro barato primero: `ObtenerRoomDataParaMacro` (hash puro) antes que `ObtenerTipoMacroChunk` (Perlin+islas+BFS) — mismo resultado, ~10× menos trabajo caro. El log de ronda imprime la **distancia al cuartel** para tunear el techo.
*   **Patrulla por TODA la isla (`patrullarIslaCompleta`, default ON en `Centinela`):** destinos al azar en **su** isla vía `WorldGenerator.TryElegirCeldaPatrullaEnIsla` (muestrea macro chunks del volumen; celda "pisable" = vacía con sólido debajo — en pasillos da los pisos, en la Colosal solo la planta baja), caminados por `TryCalcularRutaCeldas` con presupuesto `maxNodosRutaPatrulla` (6000) y `intentosDestinoPatrulla` (3). **Nunca sale de su isla** (`EnIsla` filtra; helper público `MismaIsla(a,b)`). Cascada de decisión: punto de interés cercano → patrulla por isla → vagabundeo local (sin isla debajo o sin ruta). Patrulla y retiro comparten la misma maquinaria de ruta (`ruta` + `AvanzarPorRuta` + `ConstruirRutaHasta` — se eliminó el código duplicado del retiro). ⚠️ Con `duracionRonda` 30-60s y velocidad 3, la patrulla larga casi no se ve (~7s por celda) — subir la duración de ronda (200-400s) si se quiere que el recorrido se note.

### 18.5. Pendiente
*   **Inspección de eventos reales del mundo:** hoy los `PuntoInteresCentinela` son estáticos y colocados a mano. La intención de diseño es que revisen **cosas que el jugador movió, rompió o dejó fuera de lugar** — eso requiere que los sistemas de destrucción/objetos registren puntos dinámicos.
*   **Generación automática de puntos de interés** al generar celdas de pasillo/sala, en vez de colocarlos a mano (el gancho natural es `GenerarCelda`).
*   **Modelo, animación y audio** del centinela (hoy se prueba con una cápsula).
*   Sistema de **daño real** hacia el jugador — `RecibirDano` existe en el centinela, pero el centinela todavía no le hace nada a nadie.

---

## 19. Menú Principal Integrado, Guardado y Cámara Cinemática (`MenuInicial.cs`, `ModoMenu.cs`, `SistemaGuardado.cs`, agregado 2026-08-12)

### 19.1. Decisión de arquitectura: una sola escena, no dos
El plan original era `Menu.unity` separada de `OutdoorsScene.unity`, con el `WorldGenerator` de la escena de menú generando una "burbuja" chica alrededor del jugador para que la cámara del menú tuviera pasillos reales por donde moverse. Se descartó por dos motivos:
*   El `WorldGenerator` está atado a una única `Transform jugador` que seguir (`Update` publica `celdaObjetivo` desde su posición) — no hay forma de generarle una burbuja a un punto arbitrario (cámara de menú) sin repuntar esa referencia, y hacerlo en una escena aparte implicaba mantener sincronizada una "maqueta" con el mundo real cargado después.
*   Cargar `OutdoorsScene` de forma aditiva (`SceneManager.LoadSceneAsync(..., Additive)`) desde el menú evitaba la maqueta, pero seguía siendo más aparato del necesario.

**Se eliminó `Menu.unity` por completo.** El menú vive **dentro de `OutdoorsScene`**: el `WorldGenerator` real ya está corriendo (con su jugador real, su guardado real) detrás del Canvas del menú desde el frame 0. La cámara del menú no recorre una maqueta — recorre el mundo de verdad que va a seguir cargado un segundo después cuando el jugador tome el control. **Efecto colateral pendiente:** el flujo de muerte (`PlayerData.Morir`, §8.1) todavía intenta cargar una escena `"Menu"` que ya no existe — no se tocó en esta pasada, queda anotado ahí como pendiente.

### 19.2. Guardado (`SistemaGuardado.cs`, estático)
JSON plano en `Application.persistentDataPath/partida_guardada.json` con `semilla`, `posX/Y/Z`, `rotY`. API: `Guardar(semilla, pos, rotY)`, `Cargar(out semilla, out pos, out rotY)` (false si no hay archivo), `ExisteGuardado()`, `BorrarGuardado()`.
*   **F5** (`MenuPausa.cs`) guarda en cualquier momento vía `WorldGenerator.GuardarPartidaActual()` (usa `semillaDelMundo` + `jugador.position/eulerAngles.y` directo de la referencia que ya tiene el WG).
*   **F6** (`MenuPausa.cs`, nuevo) borra el guardado (`SistemaGuardado.BorrarGuardado()`) — útil para probar el flujo de "sin guardado" sin ir a buscar el JSON a mano en `AppData/LocalLow`.

### 19.3. Flag único de congelado (`ModoMenu.cs`)
Clase estática con un solo `public static bool Activo`. En vez de un componente maestro que deshabilite otros componentes, cada script relevante se auto-consulta:
*   `PlayerMovement.FixedUpdate`: `if (movementLocked || ModoMenu.Activo) return;` (reusa el flag que ya existía para escalada/rapel).
*   `MouseLook.Update`: `if (!canLook || ModoMenu.Activo) return;`.
*   `ManoJugador.Update`: `if (ModoMenu.Activo) return;` al principio.
Congelar/descongelar al jugador entero es un solo `ModoMenu.Activo = true/false` sin tocar ninguno de los tres scripts desde afuera.

### 19.4. `MenuInicial.cs` — las 2 pantallas
*   **Menu1 ("presiona cualquier tecla"):** ignora input durante `bloqueoInicialSegundos` (default 2s) para tapar la carga inicial del mundo sin que se sienta como pantalla de carga. Al continuar, hace fade out con DOTween (`CanvasGroup.DOFade`, `duracionFadeMenu1`) antes de desactivarse — **el `CanvasGroup` se agrega a mano en el Editor**, nunca vía `AddComponent` en Play (cualquier componente agregado en Play se pierde al parar la simulación).
*   **Menu2:** el menú real (Jugar / Continuar / Opciones). El botón **Continuar** se auto-desactiva en `Start()` si `!SistemaGuardado.ExisteGuardado()`.
*   **Cámara del menú separada del jugador:** el `Player` tiene un empty **`Cabeza`** como hijo (ancla FPS); la `Main Camera` vive suelta en la escena (no es hija de nada) hasta que el jugador toma control.

### 19.5. Posicionamiento inicial de la cámara
Corutina que **espera `WorldGenerator.MundoListo`** (no solo que `Instance`/`jugador` no sean null — `MundoListo` recién se pone en `true` después de que el WG posicionó al jugador, sea por guardado o por spawn nuevo, y generó la sala inicial). Sin este chequeo, `MundoListo` no está garantizado y la cámara puede leer la posición "cruda" que el jugador tenía en el Editor antes de ser reubicado.
*   BFS de 6 vecinos sobre celdas vacías (mismo criterio — `IsCellEmpty` — que `TryCalcularRutaCeldas`, §18.45) buscando una celda a **exactamente `distanciaCeldasCamara` pasillos** (default 2, no línea recta) del jugador.
*   Posición = `CeldaACentroMundoPiso(celda) + alturaCamara` (altura de ojos, default 1.6).
*   Mira hacia el **centro de celda del jugador** (grilla, fijo vía `CeldaACentroMundoPiso`), nunca hacia `jugador.position` en vivo — un `LookAt` a la transform real puede apuntar a un punto raro a mitad de un teletransporte o de un ajuste de físicas.

### 19.6. Botones Jugar / Continuar
*   **Continuar:** el jugador ya está en el punto guardado (lo dejó ahí `WorldGenerator.Start()` al cargar) — enganche directo, sin viaje de cámara.
*   **Jugar — prioridad de capas:**
    1.  `WorldGenerator.usarSemillaAleatoria` decide si hay **mundo nuevo de verdad** o no:
        *   **OFF → `IniciarNuevaPartidaAleatoria()`:** mismo seed/mundo, nuevo punto de spawn. `DefinirPuntoDeSpawn` ahora acepta un `semillaExtra` (antes hardcodeado a `7777`) — se le pasa un valor random distinto en cada llamada para que cada click de Jugar caiga en un lugar distinto (antes era determinístico por semilla → siempre el mismo spawn).
        *   **ON → `ReiniciarMundoConSemillaAleatoria()`:** reseed real. `Destroy()` de **todo** lo generado hasta el momento (`mundoChunks` está cacheado permanentemente — `GenerarYActivarCelda` nunca regenera una celda ya presente en el diccionario, así que sin destruir los GameObjects viejos la semilla nueva nunca se vería), limpia `celdasActivasSet` y **todos** los cachés de generación (mismo bloque que usa `Start()`: `cacheTipoChunk/CeldaVacia/DirBalcon/Cierre/IslaConectada`, `misionesEncadenadasPorCelda`, `destinoPorNota`), nueva `semillaDelMundo` + offsets, recalcula misiones de notas, recalcula spawn y relanza la carga priorizada de la sala inicial (`CargarSalaInicialYSoltar`). **El `WorkerBurbuja` permanente nunca se detiene ni se reinicia** — al vaciarle sus colecciones de estado, en su próximo ciclo trata todo como "pendiente" y regenera solo, con la semilla/cachés ya nuevos.
    2.  **`spawnEnVivienda` actúa como capa separada, siempre**, dentro de `DefinirPuntoDeSpawn` sin importar cuál de los dos caminos de arriba corrió: ON = spawn en Vivienda (fallback Pasillos), OFF = `FiltroSpawn.Cualquiera` (cualquier sala/pasillo no-cuartel — **fix**: antes usaba `ViviendasYPasillos`, que no era realmente random, excluía otros tipos de sala válidos).

### 19.7. Viaje de cámara por los pasillos (`ViajarCamaraHaciaJugador`, DOTween)
Solo en el botón **Jugar** (Continuar sigue con enganche directo).
*   **⚠️ Bug de referencia circular (resuelto):** si la cámara quedó pegada a `Cabeza` de una partida anterior en la misma sesión de Play, hay que soltarla (`SetParent(null, worldPositionStays:true)`) **antes** de reposicionar al jugador — si no, al ser hija se movería junto con él y la ruta calculada tendría largo cero (celda cámara == celda jugador), pareciendo un teletransporte instantáneo en vez de un recorrido.
*   **⚠️ Bug de distancia (resuelto):** la cámara no viaja desde su posición original del inicio del menú — el jugador con "Jugar" puede terminar en **cualquier parte del mapa** (spawn random), lejos de donde la cámara fue colocada al arrancar. Rutear desde ahí se pasaba del presupuesto de búsqueda (`maxNodosBusquedaCamara`) y `TryCalcularRutaCeldas` fallaba casi siempre, salvo que el spawn random cayera cerca por casualidad. **Fix:** una vez que se sabe dónde terminó el jugador, la cámara se **re-ubica cerca de él primero** (mismo BFS de §19.5, `distanciaCeldasCamara`) y **recién ahí** se calcula la ruta — el recorrido es siempre corto por diseño, nunca cruza medio mapa.
*   **Ruta:** `TryCalcularRutaCeldas` (§18.45) de la celda-cámara (ya reubicada) a la celda-jugador → cada celda de la ruta se convierte a su centro exacto (`CeldaACentroMundoPiso` + altura de ojos) → `Transform.DOPath(puntos, duracion, PathType.Linear)` con `Ease.Linear` y `SetLookAt` (mira hacia adelante en el recorrido). `duracion` se calcula por **velocidad constante** (`velocidadViajeCamara`, m/s) sobre la distancia real del path, no una duración fija — un recorrido más largo tarda más pero al mismo ritmo.
*   **Fallback sin viaje:** si `TryCalcularRutaCeldas` no encuentra camino (celda desconectada, ej. borde raro tras un reinicio de semilla), se salta directo al enganche sin animar — nunca rompe el flujo.
*   **Al llegar (o al fallback):** la cámara se reparenta a `Cabeza` en local `(0,0,0)`, y se corrige `MouseLook.playerBody` para que apunte al **`Player` (padre de `Cabeza`)**, no a `Cabeza` — el yaw tiene que rotar el mismo Transform que `PlayerMovement` usa para `transform.forward/right`, si no el jugador puede mirar hacia un lado y seguir caminando hacia el viejo frente. Recién ahí `ModoMenu.Activo = false`.

---

## 20. Actualización 2026-09-02 — Memoria, cupo de salas, centinelas y verticalidad

Sesión larga. Los cambios tocan varios sistemas ya documentados arriba; esta sección los agrupa por tema y referencia la sección original de cada uno.

### 20.1. ⚠️ Streaming de SALIDA — el mundo ahora descarga (`WorkerDescarga`)

**El bug de fondo:** `WorkerBurbuja` (§5) solo hacía `SetActive(false)` al salir una celda de la burbuja, y `GenerarYActivarCelda` nunca regenera lo ya generado (`if (!mundoChunks.ContainsKey(celda))`). Resultado: **cada celda pisada quedaba viva en RAM para siempre**, solo apagada. El mundo hacía streaming de entrada pero no de salida, y Unity terminaba tirando *"Freed preallocated heaps because we are running out of memory"*.

**`WorkerDescarga`** — worker permanente (como el de la burbuja, sobrevive a los reinicios de semilla) que cada `intervaloDescarga` (3s) busca celdas más allá de `radioDescarga` (16) y las **destruye de verdad**: `Destroy` + fuera de `mundoChunks` + fuera del caché. Presupuestado en ms (`msPresupuestoDescarga`) y abortable si el jugador se movió.

*   **Histéresis dura:** nunca descarga dentro de `radioBurbuja + 4`, aunque `radioDescarga` se ponga bajo. Sin esto, caminar de ida y vuelta destruiría y regeneraría lo mismo en bucle — tirones **y** sin ahorro.
*   **Es seguro porque la geometría es PURA POR SEMILLA:** si volvés, `GenerarCelda` la reconstruye idéntica.
*   **⚠️ Protección de estado (`TieneObjetosConEstado`):** antes de destruir, se escanea la celda buscando componentes cuyo estado NO es reproducible — `NPCHabitante`, `NotaMundo`, `ItemSpawner`, `PuntoSpawnItem`, `InventoryItem`, `PuertaInteractiva`, `Centinela`, `BotonMundoInteractivo`, `MarcaObjetoEspecial`, `PantallaInventarioVivienda`, `SeccionSala`. Si hay alguno, la celda se marca en `celdasNoDescargables` y **no se toca nunca más**. Sin esto, un cofre ya saqueado reaparecería lleno. La lista es deliberadamente generosa: pasarse cuesta RAM, quedarse corto duplica objetos de gameplay.
*   **Nota:** `StatsVivienda` NO es un `Component` (es una clase de datos serializable) — por eso se chequean sus portadores (`PantallaInventarioVivienda`, `SeccionSala`) y no ella.

**`PodarCacheCeldaVacia`** — descargar celdas no alcanzaba para acotar `cacheCeldaVacia` (el diccionario más grande: una entrada por **celda**, no por chunk). Los BFS de pathfinding (rutas de centinelas, cámara del menú §19.5) llaman `IsCellEmpty` sobre miles de celdas que **nunca se generan ni entran en `mundoChunks`**, así que esas entradas quedaban huérfanas. Ahora se podan por distancia cuando el diccionario pasa `maxEntradasCacheCelda` (200k). Descartar es gratis en corrección: el valor es puro por semilla.

**Limitación conocida:** las celdas con gameplay (viviendas, cofres) **nunca se descargan**, así que siguen acumulándose — más lento, pero acumulan. Para que también se descarguen hace falta una capa de persistencia (recordar qué cofre ya se abrió para no re-spawnearlo). Pendiente.

**Pendiente relacionado:** *pooling* de prefabs. La descarga arregla la RAM, no el tirón: el freeze al entrar a zonas nuevas es el pico de `Instantiate`. Ver §20.7.

### 20.2. Cupo y separación de Salas Chicas por isla (`SalasDeIsla`)

**El problema:** el tipado decidía Sala Chica **chunk por chunk** contra `umbralDensidadAlta`, así que la cantidad de viviendas dependía del **tamaño** de la isla. Al hacer las islas más grandes y macizas, se llenaban de decenas de viviendas (cada una = prefab + NPC + items en RAM).

**La solución:** la decisión pasa a ser **por isla**, cacheada en `cacheSalasIsla`:

*   **`salasPorIsla`** (default `(2,4)`) — cuántas Salas Chicas tiene cada isla. **El cuartel garantizado (§5.8) cuenta como una**, así que con (2,4) siempre hay cuartel + 1 a 3 viviendas.
*   **`separacionMinimaSalasChunks`** (default `2`) — separación mínima en **MACRO CHUNKS**, distancia Chebyshev, así que **cuenta la vertical** igual que la horizontal. Incluye al cuartel, así que una vivienda no puede quedar pegada a él.
    *   ⚠️ **Unidad:** dos macro chunks vecinos ya están `macroSize` celdas aparte (con `macroSize=5`, eso es 5 celdas). Por eso `1` permite salas en chunks pegados. `2` deja un chunk de aire en medio.
    *   **Cascada de relajación:** si la isla es chica y no entran tan separadas, la separación baja de a uno hasta llegar al mínimo de `salasPorIsla`. Preferible un poco más juntas que una isla sin viviendas.
*   **La Sala Chica (1) salió de la ruleta de densidad.** Los tipos altos ahora son `{5, 6, 8}`. Si siguiera en la ruleta, cada chunk del núcleo tiraría sus propios dados y el tope no serviría de nada. Los chunks densos que no ganan cupo se vuelven intersecciones/pozos, así que el núcleo no queda hueco.
*   Las salas se eligen **solo del núcleo** (conectado, no poroso, sin tocar el vacío): una sala en el borde quedaría destapada contra el void (misma razón que la regla de borde de §5.8). El cuartel es la excepción — conserva su cascada propia y puede caer en borde si no hay lugar mejor.
*   `probabilidadIslaSinViviendas` (§5.8) ahora se resuelve **dentro de `SalasDeIsla`**, que devuelve solo el cuartel. El remap de tipo 1 → {5,6,8} en la rama de densidad ya no hace falta.

### 20.3. ⚠️ Hallazgo: la distribución de densidad es MUY angosta

`ObtenerDensidad` promedia **3 muestras** de `Mathf.PerlinNoise`. Promediar comprime la distribución hacia el centro: la densidad queda apretada alrededor de **~0.47** con desviación ~0.075. Casi ningún chunk saca 0.2 ni 0.8.

Consecuencias que conviene tener presentes al tunear:

*   Un umbral puesto **en 0.45-0.50 está sobre la media**, o sea en el punto donde más chunks se amontonan: moverlo 0.05 cambia muchísimo. `umbralVacioIsla` en 0.45 vaciaba ~40% de la isla.
*   Un umbral en **0.62 está a ~2 desviaciones**: solo ~2% de los chunks calificaban como densidad alta.
*   **La Sala Grande (tipo 7) es inalcanzable.** Requiere densidad entre `umbralDensidadBaja` (0.2) y `umbralDensidadMedia` (0.3), pero la porosidad de isla ya devuelve `9` para todo lo que esté bajo `umbralVacioIsla`. Lo mismo el "Abismo Verdadero" del final de la cadena. **`umbralDensidadBaja` y `umbralDensidadMedia` son código muerto hoy.** Si se quieren esas salas monumentales de vuelta, hay que darles su propia regla dentro del branch de isla, no depender de umbrales que quedaron por debajo del piso de porosidad.

### 20.4. Centinelas fuera de las viviendas (`EsCeldaDeViviendaVedada`)

**Por qué volvía cada vez que se "arreglaba":** no era un bug, eran **tres caminos independientes** con el mismo síntoma, y cada intento cerraba uno solo.

1.  **Punto de interés** (`BuscarPuntoInteresCercano`, §18.63) — corre **antes** que la patrulla y manda sobre ella, y va en **línea recta sin pathfinding**. Un `PuntoInteresCentinela` dentro de un prefab de vivienda los metía derecho.
2.  **La ruta** (`ConstruirRutaHasta`) — filtrar el *destino* no alcanzaba: `TryCalcularRutaCeldas` cruzaba la casa por adentro para llegar al otro lado. **Esta es la que explicaba el "pocas veces pero pasa".**
3.  **Vagabundeo** (fallback sin isla/ruta) — solo chequeaba obstáculos con `Linecast`, cero noción de vivienda.

**La solución:** un **único** helper, `WorldGenerator.EsCeldaDeViviendaVedada(celda)` (+ `EsPosicionDeViviendaVedada`), que usan los tres. Si mañana aparece una cuarta forma de elegir destino, se engancha ahí y se acabó el whack-a-mole.

*   `TryCalcularRutaCeldas` acepta ahora un parámetro opcional `bloqueada` (`Func<Vector3Int,bool>`). **Ni el origen ni el destino se bloquean nunca.**
*   **El cuartel nunca se veta**, aunque también sea Sala Chica — si no, no podrían volver a su base.
*   **Perseguir sigue funcionando:** `ActualizarAlerta` usa `MoverHacia` directo, sin pathfinding. Esconderse en una casa **no** hace invencible al jugador.
*   ⚠️ **Caso borde resuelto:** un centinela que persiguió hasta *dentro* de una casa quedaría sin ruta para salir (sus 6 vecinos también vetados). Si ya está adentro, el veto se levanta para esa ruta: entra persiguiendo, y puede salir caminando.
*   Toggle `centinelasEntranAViviendas` (default OFF) para volver al comportamiento viejo.

### 20.5. Verticalidad: objetos en pozos y escaleras de pasarela

**Pozos verticales (tipo 6).** El pozo talla una columna de **1×1 celda** (`lx==2 && lz==2`) a **todas** las alturas, y la columna central de cada isla está forzada a tipo 6 — es el esqueleto vertical. Pero también salen pozos del sorteo de densidad `{5,6,8}` en otros chunks.

*   `prefabsCentroPozo` + `probabilidadObjetoPozo` (0.75) + `pasoRotacionPozoGrados` (90).
*   Sorteo **por celda** (si hay objeto, cuál, y su giro), así que una columna puede mezclar prefabs y tener huecos. A propósito.
*   Giro **solo en Y**. Con paso 90 el objeto cae encarado a una de las 4 paredes (el pozo es cuadrado, así que una escalera queda apoyada contra un muro); con 0, giro libre 360°.
*   **Convención de modelado:** pivote en la **BASE**, centrado en X/Z.
*   ⚠️ La rotación se sortea dentro del bloque sembrado: si no fuera determinista, al descargar y regenerar la celda (§20.1) las escaleras cambiarían de orientación cada vez.

**Escalera de bajada de pasarela** (§5.9). Las alturas posibles de pasarela son `cellSize * 0.25 / 0.50 / 0.75` — o sea **relación 1:2:3**. Por eso **un solo prefab de un cuarto de celda** las cubre las tres apilándose 1, 2 o 3 veces: sin estirar (estirar separa los peldaños) y sin modelar tres largos.

*   `prefabTramoEscalera` + `alturaTramoEscalera` (0 = auto, `cellSize * 0.25`).
*   Se apila **desde el piso hacia arriba** (`pisoY + i * alturaTramo`), para que el tramo de abajo apoye exacto en el suelo, que es lo que se nota si queda mal.
*   Va **después** del encastre por conector: la escalera cuelga de la posición **final** de la plataforma.
*   **Ancla `PuntoEscalera`:** empty marcador dentro del prefab de plataforma que define de dónde cuelga. Sin él cae al pivote, que en estos prefabs está en el **centro del largo** → la escalera aparece en la mitad de la pasarela.
*   Los tramos se registran en `mundoChunks[celda]` para que la descarga (§20.1) los limpie con el resto de la celda.

### 20.6. ⚠️ Piezas de EXTREMO de pasarela — el espejo va A LO LARGO, no por pared

**Leer esto antes de tocar cualquier prefab de extremo de pasarela.** Costó varias iteraciones y el error de fondo es conceptual, no de código.

#### El modelo mental correcto

Una pieza de extremo (tapa de fin, pasarela con escalera) tiene **dos cosas que la invierten, independientes entre sí**:

1.  **Contra qué pared va** (`esIzquierda`) → la rotación difiere **180°** entre la pared de un lado y la del otro.
2.  **Qué punta de la corrida es** (`conectaMas` / `conectaMenos`) → la unión con la recta vecina cae del otro lado.

Como son independientes, **se combinan con XOR**:

| Pared | Punta | ¿Espejo? |
|---|---|---|
| Derecha | `conectaMas` | no |
| Derecha | `conectaMenos` | **sí** |
| Izquierda | `conectaMas` | **sí** |
| Izquierda | `conectaMenos` | no |

Si se dan **las dos** inversiones se cancelan y sirve el prefab normal. Si se da **una sola**, hace falta el espejado. Está en `NecesitaEspejo(conectaMenos, esIzquierda)`.

#### 🚨 La trampa: NO son "prefab izquierdo" y "prefab derecho"

Este fue el error que costó la sesión. Lo intuitivo es modelar una variante para la pared izquierda y otra para la derecha — **y está mal**: la pared ya la resuelve la rotación sola (por eso las barandas salen del lado correcto sin hacer nada).

Las dos variantes que hay que modelar son **la misma pieza espejada A LO LARGO** — o sea, con la tapa/escalera en la **punta contraria**, y las barandas donde estaban. Si se modelan como izquierda/derecha, las dos terminan cubriendo el mismo caso y ninguna cubre el otro.

Nombrarlas `Izquierda`/`Derecha` en el proyecto es lo que hace que el error se repita: conviene `Normal` / `Invertida`.

#### 🐛 LA CAUSA RAÍZ (2026-09-02): `esIzquierda` calculaba mal la lateralidad

Este era **el** bug, y era pre-existente. Estaba así:

```csharp
bool esIzquierda = (dir.x < 0 || dir.z < 0);   // ❌ MAL
```

Traducido: *"es izquierda si la pared está en X− o en Z−"*. Pero **la izquierda depende de hacia dónde corre el pasillo**, no del signo del eje:

*   Pasillo en **Z**, pared en **X−** → mirando hacia +Z, esa pared está a la **izquierda** ✅
*   Pasillo en **X**, pared en **Z−** → mirando hacia +X, esa pared está a la **DERECHA** ❌

Las metía a las dos en la misma bolsa, así que **en los pasillos de un eje la paridad quedaba invertida respecto a los del otro**. Arreglado con el producto cruzado, que da la lateralidad de verdad:

```csharp
bool esIzquierda = Vector3.Cross(new Vector3(corrAxis.x, 0f, corrAxis.z),
                                 new Vector3(dir.x, 0f, dir.z)).y > 0f;
```

**Por qué costó tanto encontrarlo:**

*   El síntoma (piezas despegadas de la corrida) es **indistinguible** de un problema de pivote, de conector o de prefab mal modelado. La búsqueda se va siempre para el lado equivocado.
*   Ningún valor de `invertirEspejoPasarela` podía arreglar todo: **arreglaba un eje y rompía el otro**.
*   Dos piezas con el **nombre generado idéntico** (`..._izq_end_escalera`) se comportaban distinto, porque el nombre sale de `esIzquierda`, que era justamente lo que estaba mal.

**Lo que lo destrabó:** fijar la semilla y notar que de **5 instancias con nombre idéntico, 4 estaban bien y 1 mal**. Piezas nominalmente iguales comportándose distinto = la variable que las clasifica está mal calculada. Ese es el razonamiento a repetir si vuelve a pasar algo así.

#### El otro bug histórico (condición incompleta)

`prefabPasarelaFinInvertida` existía desde antes, pero su condición era:

```csharp
if (conectaMenos && prefabPasarelaFinInvertida != null)   // ← le falta esIzquierda
```

**Solo miraba la punta, ignoraba la pared.** Acertaba en 2 de las 4 combinaciones. La escalera arrastraba lo mismo. Se corrigió al XOR (`NecesitaEspejo`), pero mientras `esIzquierda` estuvo mal, ni el XOR correcto alcanzaba.

#### Los campos

*   `prefabPasarelaFinInvertida` — tapa de fin espejada a lo largo.
*   `prefabsPasarelaConEscaleraInvertida` — array paralelo al normal. Se indexa con el **mismo índice**, así que conviene mismo orden y misma cantidad: la variante espejada tiene que ser el espejo de la que hubiera tocado, no otra escalera.
#### 🔎 Cómo diagnosticar si vuelve a fallar

**Primero: fijar la semilla.** `Usar Semilla Aleatoria` destildado + un número fijo. Sin esto cada Play es un mundo distinto y no se está repitiendo el test — se comparan escenarios diferentes y el resultado parece aleatorio. Media sesión se perdió por esto.

Después, el **patrón de fallos** dice qué es:

| Qué se ve | Qué es |
|---|---|
| Piezas con el **mismo nombre generado** se comportan distinto | La variable que las clasifica está mal calculada (fue el bug de `esIzquierda`) |
| Falla **la mitad** | La condición mira una sola de las dos variables |
| Fallan **todas** | Los prefabs están cruzados en los slots → `invertirEspejoPasarela` |
| Barandas del lado equivocado | `yawExtraPasarelaEscalera` (va en **180** en este proyecto) |

⚠️ Las barandas y la punta son **problemas distintos con perillas distintas**. El yaw arregla el lado de las barandas y **además** invierte la punta como efecto colateral — de eso se encarga el toggle, no el yaw. Confundirlos lleva a girar el yaw para arreglar el encastre, que nunca funciona.

#### ❌ Callejón sin salida (no reintentar)

Se probó **espejar en runtime con escala negativa** sobre el eje de la corrida, detectando si el conector quedaba apuntando al revés. En teoría cubría las 4 combinaciones con un solo prefab y sin variantes. **En la práctica rompió el lado que ya funcionaba y se revirtió.** Si la idea vuelve a aparecer: ya se intentó, y además arrastra el problema de que la escala negativa se lleva mal con `MeshCollider`.

Tampoco sirve **girar 180° en Y**: invierte también de qué lado quedan las barandas y manda el tablón dentro del muro. Está documentado desde antes en el código.

#### Regla de modelado

*   **Un solo `Conector`** por pieza de extremo, en la punta que se une a la corrida. (Con dos, el picker resuelve la posición pero se pierde el control de hacia dónde apunta la punta cerrada.)
*   **Búsqueda de marcadores tolerante (`NormalizarNombre`).** `BuscarHijoPorPrefijo` no compara literal: normaliza dejando solo letras y dígitos en minúscula. Así `PuntoEscalera`, `Punto_Escalera`, `punto escalera`, `PUNTO-ESCALERA` y `PuntoEscalera.001` son todos válidos — los separadores y los sufijos `.001` aparecen solos trabajando entre Blender y Unity.
*   **Búsqueda de marcadores tolerante (`NormalizarNombre`).** `BuscarHijoPorPrefijo` ya no compara literal: normaliza dejando solo letras y dígitos en minúscula. Así `PuntoEscalera`, `Punto_Escalera`, `punto escalera`, `PUNTO-ESCALERA` y `PuntoEscalera.001` son todos válidos — los separadores y los sufijos `.001` aparecen solos al trabajar entre Blender y Unity.
*   `yawExtraPasarelaEscalera` (default 0, inerte) — la tapa de fin tenía su corrección de yaw desde siempre y la escalera no. ⚠️ Girar 180° en Y **también espeja de qué lado quedan las barandas**; si el tablón se mete en la pared, la solución es el prefab espejado, no el giro.
*   **La recta manda:** las tapas y la escalera se reacomodan por conector, pero la recta `_mid` se instancia **cruda** en `pos`. Si su pivote está mal, se desalinea toda la corrida y ninguna alineación posterior lo arregla. Definir el pivote en `prefabPasarelaRecta` primero y modelar el resto con el mismo criterio.

### 20.7. Carteles de coordenadas: separación mínima

`probabilidadCartelCoordenadas` sola no garantizaba nada — `YaTieneCartel` solo cubre la **misma** celda, y dos celdas vecinas pueden sacar cara las dos, quedando dos carteles pared con pared.

**`separacionCartelesCeldas`** (default 5): retícula determinista y sin estado. El cartel solo se permite donde `(x + z) % separacion == fase`. En un pasillo recto (X o Z fijos) eso da **uno cada N celdas exacto**, y dos celdas vecinas nunca pueden calificar las dos (sus `x+z` difieren en 1).

*   ⚠️ **La fase es global** (sale de `semillaDelMundo`), no por macro chunk. Si se derivara por chunk, en las fronteras las fases distintas volverían a permitir dos carteles pegados — el mismo bug, pero solo en los bordes y más difícil de reproducir.
*   **La Y no entra:** dos carteles en la misma columna pero en pisos distintos ya están a un `cellSize` de distancia vertical.
*   La probabilidad se aplica **encima** de la retícula. Con separación 5 y probabilidad 0.25 sale uno cada ~20 celdas: al activar la separación conviene **subir la probabilidad** (0.7-1.0) y dejar que la retícula haga el espaciado.

### 20.8. Diálogo diegético de NPC (`DialogoUI`, §9.x)

El diálogo dejó de ser un texto fijo en pantalla: ahora se posiciona sobre el NPC.

*   `Mostrar(mensaje, ancla)` — con ancla, el panel se reposiciona **cada frame** proyectando `ancla.position + offsetMundo` con `WorldToScreenPoint`. Sin ancla, comportamiento viejo.
*   **`panel` (Transform, no RectTransform):** lo que se mueve **y se prende/apaga** es el grupo entero (fondo + texto + lo que se le agregue), no solo el texto. Se declara como `Transform` a propósito, para poder asignar un empty común y no obligar a un RectTransform.
*   `NPCHabitante.puntoDialogo` — empty en el prefab del NPC que marca la altura exacta (pecho/cabeza). Vacío = cae al `transform` del NPC, que en los modelos Mixamo tiene el pivote **en los pies** — de ahí que el offset genérico tenga que compensar ~1.7m.
*   `ocultarMientrasHabla` — GameObjects del HUD que se apagan al hablar y se **restauran a su estado previo** (no se fuerzan a `true`).
*   ⚠️ Si la posición se calcula pero el texto no aparece donde se espera, el sospechoso es el **pivote del RectTransform**, no la proyección.

### 20.9. Nuevas frases y HUD

*   **`HabitanteData.charlaPosMision`** — categoría nueva de frases para cuando ya se escuchó el agradecimiento y se vuelve a hablar. `DatosVivienda.yaAgradecio` distingue la primera vez del resto; sin esto el NPC repetía el mismo agradecimiento para siempre. Array vacío = fallback `"..."`.
*   **Tirolina (§17):** `sliderMunicion`/`iconosMunicion` ahora se ven **solo mientras se apunta**, igual que `miraTirolina`. `ActualizarMunicionUI` no toca los iconos si no se está apuntando — si no, recargar con `R` fuera del modo apuntado los reencendía.
*   **⚠️ `SistemaTirolina` era el único sistema del jugador sin el guard de `ModoMenu.Activo`** (`PlayerMovement`, `MouseLook` y `ManoJugador` ya lo tenían). Ahora en menús y durante el viaje de cámara (§19.7) no se apunta, no se dispara y no se hace zoom. Sale de modo apuntado de forma **ordenada** (devuelve la mira normal y apaga la munición) en vez de solo cortar, y solo toca el FOV si de verdad quedó zoomeado — para no pelearle la cámara al `DOPath` del menú.
*   **`MenuInicial.ocultarMientrasMenu`** — GameObjects del HUD que se apagan mientras hay menú en pantalla y se reactivan en **todos** los puntos donde el juego arranca de verdad (incluidos los 3 early-outs de `ViajarCamaraHaciaJugador`).

### 20.11. Loot embebido, ahora con grupos excluyentes (`PuntoSpawnItem`, §9.55)

*   **⚠️ Bug real (costó una sesión):** `PuntoSpawnItem` bien puesto y bien asignado dentro de `Pasarela.Recta` (un palet) no hacía nada, sin ningún error. Causa: `ProcesarPuntosSpawnItem` **nunca fue automático** — hay que engancharlo a mano en cada punto de instanciación, y las pasarelas nunca lo llamaron (solo `goCentro` de viviendas lo hacía). Fix: enganchado en `InstanciarYRegistrarPasarelaRotacion`, el punto único por el que pasan las 5 piezas de pasarela (recta/fin/escalera/L/T). **Si un `PuntoSpawnItem` "no funciona" y está bien configurado, lo primero a revisar es si el punto de instanciación de ESE prefab llama a `ProcesarPuntosSpawnItem`, no el marcador.**
*   **Como no había ningún `Random.State` sembrado en ese punto** (cada roll de la cadena de decisión de la pasarela restaura el suyo apenas termina), `InstanciarYRegistrarPasarelaRotacion` siembra un hash propio por posición antes de llamar.
*   **Recursivo:** el ítem instanciado (ej. un palet) se vuelve a pasar por `ProcesarPuntosSpawnItem` — si tiene sus propios puntos adentro, se resuelven en la misma pasada, encadenando sin límite de profundidad. Todo comparte el mismo `Random.State`, así que la cadena entera sigue determinista por semilla.
*   **`grupoExclusivo` + `prioridad` (campos nuevos en `PuntoSpawnItem`):** vacío = comportamiento de siempre, cada punto 100% independiente. Mismo texto en varios puntos = quedan en un grupo excluyente: se evalúan por `prioridad` (mayor primero), y en cuanto UNO logra spawnear algo, **los demás del grupo se apagan sin tirar sus propios dados** — para "si entra el objeto grande, ya no caben los 4 barriles de las esquinas".
*   **⚠️ La probabilidad de un candidato NO es su chance real** — `itemsPosibles` es una CASCADA (mismo criterio que `RoomData.LootEntry`), no una ruleta ponderada. Con dos candidatos al `0.5` cada uno, el resultado real es `50% / 25% / 25% de nada` — el primero de la lista tiene ventaja estructural por evaluarse antes. Para un split parejo de verdad entre N candidatos sin chance de vacío, el ÚLTIMO de la lista va en `1.0` (gana seguro cuando le toca el turno).
*   **`Probabilidad Spawn` (del punto) × `Probabilidad` (del candidato)** son dos filtros en cadena, no se suman — las dos tienen que pasar.

### 20.12. Ambiente de pasillo: vapor de tubería y "susto" (`PuntoTuberia.cs`, `SustoVapor.cs`)

Primeras piezas de un objetivo más amplio ("los pasillos se sienten vacíos") — humo de tubería, polvo de linterna, goteo de condensación, viento visual. Esta sesión se resolvieron las dos primeras.

*   **Humo/vapor ambiente:** NO requirió código — un `ParticleSystem` hijo del prefab de tubería (Loop + Play On Awake), con **Bursts** periódicos (`Rate over Time = 0`, ráfaga cada 8-20s random) en vez de emisión constante, para que se sienta a "pssst" ocasional y no a fuga continua.
    *   ⚠️ **La escala del mundo importa:** con `cellSize = 20`, un `Start Size` de tutorial genérico (`0.1-0.3`) es invisible — quedan motas sueltas dispersas por el `Noise`, nunca leen como nube. Achicar/agrandar SIEMPRE en relación al `cellSize` del proyecto, nunca copiar valores de un tutorial a ciegas.
    *   Para que lea como **nube** y no como "motas": la textura importa más que cualquier otro ajuste (círculo perfecto = mota, sprite irregular con bordes difusos = humo); además `Start Size`/`Start Rotation` en `Random Between Two Constants` (no fijos — todas iguales se lee como sprite repetido) y overlap alto (`Burst Count` 50-80).
    *   Para el "puff rápido que se corta" (no pared avanzando): `Start Speed`/`Start Lifetime` en rango random amplio (nunca fijo — sin variación, todas las partículas viajan igual y llegan/mueren juntas = efecto pared) + `Limit Velocity over Lifetime` con `Speed` **menor** al `Start Speed` (si el techo de velocidad es mayor al de arranque, el módulo no frena nada — есto pasó en la práctica).
    *   El disparador es el propio `Emission → Bursts` del Particle System — no hace falta ningún script para que se repita solo, y como el objeto se activa/desactiva con el streaming de la burbuja, el `Interval` queda pausado mientras está invisible (no se acumulan ráfagas atrasadas al volver).
*   **`PuntoTuberia.cs`:** marcador de registro (mismo patrón que `PuntoInteresCentinela`, `OnEnable`/`OnDisable` — no `Awake`/`OnDestroy`, porque las tuberías se activan/desactivan con la burbuja y la lista tiene que reflejar solo lo visible). Se pone en un empty dentro de cada prefab de tubería.
*   **`SustoVapor.cs`:** un único emisor **compartido** (no uno por tubería) que cada tanto (timer random, mismo patrón que `SistemaDesprendimientos`: `esperaInicial` + reprogramar siempre pase lo que pase) busca la `PuntoTuberia` más cercana al jugador dentro de un radio (sin chequeo de paredes — decisión explícita) y se teletransporta ahí a disparar partícula + sonido.
    *   ⚠️ **Verificado que las tuberías NUNCA rotan sobre su propio eje longitudinal** — `InstanciarCadenaTuberiaRecta` usa `Quaternion.LookRotation(eje, Vector3.up)` (solo yaw, `Up` siempre fijo al mundo) y las curvas son puro `Euler` en Y. Por eso un marcador puesto adentro del prefab (ej. apuntando hacia abajo) queda con la MISMA orientación relativa en absolutamente todas las instancias — la partícula puede heredar la rotación del punto sin miedo a que alguna tubería salga "de canto".
    *   Idea pendiente (guardada en memoria, no implementada): que la probabilidad del susto suba justo después de escapar de un centinela, o cuando "no hay música" — hoy ninguna de las dos señales existe en el proyecto (no hay sistema de música; los `Centinela` no exponen su estado fuera de sí mismos).

### 20.13. Pendientes de esta sesión

*   **La escalera debería INICIAR el recorrido de la pasarela.** Hoy la pieza con escalera es una de las dos terminaciones posibles de un extremo (`_end_escalera`, elegida por `probabilidadEscaleraExtremo`), no el punto desde el que se planifica la corrida. Que la corrida "nazca" en la escalera es otro diseño: habría que decidir primero dónde va el acceso y recién después tender el tramo.
*   **Pooling de prefabs** — ver §20.1. La descarga arregla la RAM; el tirón al entrar a zonas nuevas es el pico de `Instantiate` y necesita reciclado de objetos.
*   **Persistencia de gameplay para poder descargar viviendas** — ver §20.1.
*   **Sala Grande (tipo 7) inalcanzable** — ver §20.3.
*   **`WorldGenerator.cs` pasa las 5700 líneas y 28 headers de Inspector.** El corte natural, sin romper referencias de escena, es `partial class` en varios archivos: streaming (`WorkerBurbuja`/`WorkerDescarga`), islas y puentes (`CentroIsla`/`EnIsla`/`TipoPuente`/`SalasDeIsla`), y red de viviendas/misiones.
*   **Ambiente de pasillo, resto de la lista** (ver §20.12) — humo y susto de tubería ya están; falta: polvo suspendido en el haz de la linterna, goteo de condensación desde el techo/tuberías, viento visual (motas siguiendo al jugador cuando está expuesto al vacío, reusando la misma condición que ya usa `AmbientSounds` para el viento sonoro), y niebla local de humedad (`Local Volumetric Fog` de HDRP, sin script).
*   **Susto atado a tensión** — ver §20.12 y la memoria `idea-susto-vapor-atado-a-tension`. Falta construir la señal (recién escapaste de un centinela / hay música) antes de poder engancharlo.

---

## 21. Actualización 2026-09-07 — Carriles de techo y centinelas que sí te cazan

### 21.1. Carriles de techo — reparto de tubería y luces (`WorldGenerator`)
**Problema:** dos sistemas independientes decoraban el MISMO techo sin hablarse — `IntentarGenerarTuberia` (luces) y `IntentarGenerarTuberiaModular` (tuberías). Resultado: luces incrustadas dentro de las tuberías.

**⚠️ Por qué NO se resolvió inspeccionando los objetos instanciados.** Fue lo primero que se descartó: las celdas se generan en orden arbitrario (la burbuja avanza según por dónde camines) y `WorkerDescarga` (§20.1) las destruye y regenera. Un chequeo del estilo "¿hay algo en esta posición?" leyendo `mundoChunks` daría resultados **distintos según el orden de visita** — la misma celda se vería vacía u ocupada según de qué lado llegaste. La generación tiene que seguir siendo **función pura de la semilla**.

**Solución — predictor por hash, mismo patrón que `HabraPasarelaEn` / `HabraCierreEn` / `HabraTuberiaEn`:**
*   El techo se divide en **3 carriles** (`CarrilesTecho`): izquierda / centro / derecha, separados por `separacionCarrilesTecho` (5.64, clampeado a `cellSize*0.5 - luzTechoMargenPared`).
*   `CarrilTuberiaEn(x,y,z)` devuelve **qué carril ocupa la tubería**, hasheando la **coordenada PERPENDICULAR a la corrida** (si la corrida va en X, hashea Z). Así toda la corrida usa el mismo carril y la tubería no zigzaguea de celda en celda. Con `tuberiaUsaCarrilAleatorio = false` (default) siempre va al centro.
*   `CarrilesOcupadosTecho(x,y,z)` arma el `bool[3]` de ocupación consultando `HabraTuberiaEn` — sin instanciar nada.
*   Las luces **reclaman carriles libres** en orden de preferencia `{0, 2, 1}` (laterales primero, para que dos luces queden simétricas en vez de pegadas).

**Defaults elegidos para que el mundo se vea IGUAL que antes** del cambio: los carriles caen exactamente donde ya estaban las luces (±5.64) y la tubería se queda en el centro.

`luzTechoOffset` y `tuberiaOffsetLateral` quedaron marcados **OBSOLETOS en el tooltip pero NO se borraron**: borrar un campo serializado vacía su valor en la escena (§17.2, y volvió a pasar esta sesión con `prefabsPasarelaConEscaleraIzquierda`).

### 21.2. Centinelas — de "weones" a cazadores (`Centinela.cs`)
Referencia de diseño pedida: **Alien Isolation** — que el bicho no sea omnisciente, pero que **razone con pistas** y decida a dónde ir.

#### Los tres bugs que los hacían tontos
1.  **La persecución iba en LÍNEA RECTA.** `ActualizarAlerta` llamaba `MoverHacia(ultimaPosJugador)` directo; la maquinaria de ruta por celdas (`ConstruirRutaHasta`, §18.45) solo la usaban la patrulla y el retiro. Cualquier pared entre medio = se quedaba empujando contra ella.
2.  **`Buscando` se quedaba QUIETO.** Solo descontaba el reloj mirando el aire 8 segundos. No revisaba nada.
3.  **Perdía el rastro al instante.** Al llegar a `ultimaPosJugador` cortaba a `Buscando` aunque lo tuviera a 3 metros corriendo, sin extrapolar hacia dónde iba.

#### Máquina de estados nueva
| Estado | Significado | Gizmo |
|---|---|---|
| `Tranquilo` | patrulla, no sospecha nada | cyan |
| `Persiguiendo` | **te está viendo** — va directo, rápido | rojo |
| `Alerta` | no te ve pero sabe dónde estabas — corre **por ruta caminable** | naranja |
| `Buscando` | te perdió — investiga las pistas una por una | amarillo |
| `RetirandoseABase` / `Descansando` | vuelve al cuartel (§18.45) | verde |

*   **`CambiarEstado(nuevo, motivo)` es el ÚNICO punto de transición** y loguea **una vez por cambio** (`logEstados`), con el motivo y la distancia al jugador. Nunca por frame. Fue lo que permitió diagnosticar todo lo de abajo leyendo la consola.

#### ⚠️ El bug del `teVe` por frame (el que hacía que te persiguiera 1 segundo)
`Persiguiendo` tenía dos salidas: memoria agotada **o** "llegué a la última posición conocida". La segunda comparaba contra un `bool teVe` **local**, que solo se calculaba en los frames de percepción (1 de cada ~12, con `intervaloPercepcion = 0.2`). En cualquier otro frame valía `false`, así que llegar al punto viejo lo degradaba a `Alerta` **con el jugador a 4 metros y a plena vista**. En el log se veía literal: `Persiguiendo → Alerta · llegó a donde te vio por última vez · dist jugador: 4,6m`.

**Fix:** el "te veo" vive en un campo persistente (`viendoAhora`), y la **única** salida de `Persiguiendo` es que se agote `memoriaVisualSegundos`. Es la misma clase de bug que el `esIzquierda` de las pasarelas (§20.6): una variable mal calculada haciendo que instancias idénticas se comporten distinto.

#### Persecución con memoria y tenacidad
*   **`memoriaVisualSegundos` (3):** sigue persiguiendo N segundos después de perderte de vista. Sin esto, cruzar una columna medio segundo lo hacía olvidarse.
*   **`bonusConoPersiguiendo` (1.8) / `bonusDistanciaPersiguiendo` (1.4):** mientras te persigue, cono y distancia se multiplican — **cuesta más salir de su vista que entrar**.
*   **`tenacidadPorAvistamiento` (0.5), tope `tenacidadMaxima` (3):** cada vez que te vuelve a ver en el mismo episodio, el tiempo de búsqueda se multiplica por `1 + veces*0.5`. La 3ra vez que te asomás ya no se rinde a los 8 segundos. `vecesQueTeVio` se resetea recién cuando se rinde del todo.
*   `Persiguiendo` se mueve **en línea recta** a propósito (tiene línea de vista, y la ruta por centros de celda se ve robótica cuando te tiene enfrente). `Alerta` y `Buscando` sí van por BFS.

#### `Buscando`: pistas, no un temporizador
`ArmarPuntosBusqueda()` construye hipótesis de más a menos confiable:
1.  Dónde te vio (`ultimaPosJugador`).
2.  **Hacia dónde ibas**: `ultimaPosJugador + velocidadJugadorEstimada * extrapolacionBusqueda`. La velocidad se estima en `RegistrarAvistamiento` (delta de posición sobre `intervaloPercepcion`).
3.  Los `PuntoInteresCentinela` más cercanos a esa extrapolación (`radioPistaCercaExtrapolado`, 12) — puertas y pasillos como "por ahí se puede haber ido".

Camina cada punto y se queda `tiempoMirarPunto` (2.5s) revisándolo. **Y si agota las pistas con tiempo de sobra, AMPLÍA el área** (`AgregarPuntosExploratorios`): un anillo de `puntosExploratoriosPorRonda` (4) puntos a `radioAmpliacionBusqueda` (20m), después a 40m, después a 60m. Antes se plantaba quieto — por eso los 24s de tenacidad no servían de nada y parecía que se rendía enseguida.

Throttle: **un intento de BFS por punto**, no uno por frame (`intentoRutaBusquedaHecho`). Si el punto es inalcanzable cae a `MoverHacia` recto y se consume rápido.

#### Percepción: el centinela es grande, el jugador es bajo
*   **`ojos` (Transform, opcional):** Empty hijo del prefab a la altura de los ojos. De ahí sale el raycast **y** desde ahí se mide el cono. Sin asignar cae a `alturaOjos` sobre el pivote, que en un bicho alto queda a la altura de las rodillas. `usarForwardDeOjos` (default OFF) para no romper nada si el Empty está rotado.
*   **`alturaObjetivoJugador` (1):** el rayo apunta al **pecho**, no al transform del jugador, que está en los pies. Mirando desde arriba, un rayo a los pies roza el piso y da "no lo veo" con el jugador enfrente.
*   **El LOS ya no es UN rayo:** `PuedeVerJugador()` prueba pecho, cabeza (`alturaCabezaJugador`, 1.8) y pies; con que **una** pase alcanza. Un solo rayo fino fallaba contra barandas, cajas y bordes de pasarela.
*   **`volumenDeteccion` (Collider, opcional):** un Empty hijo con un Box Collider grande y alargado hacia adelante. Si el jugador está adentro, cuenta como detectado sin cono ni distancia. Usa `ClosestPoint`, **no eventos de trigger** — así no hace falta `Rigidbody` en nadie. `volumenIgnoraParedes`: **conviene apagarlo** con cajas largas, si no te ve a través de las paredes; con OFF la caja define "hasta dónde y en qué dirección mira" y el raycast se encarga de las paredes. El Empty va **Is Trigger** y en una layer fuera de `capasObstaculo` (si no, su propio volumen le tapa el rayo).
*   **Gizmo de calibración** (seleccionando el centinela, sin entrar a Play): bolita magenta en los ojos + línea de mirada, y línea **verde/roja** al pecho del jugador según si el LOS pasa.

### 21.3. Debug: spawnear directo en el cuartel (`spawnEnCuartelDebug`)
Buscar un centinela a pie costaba minutos por sesión de testeo.
*   **`WorldGenerator.spawnEnCuartelDebug`** (header *Jugador y Spawn*): te spawnea en la celda central del cuartel más cercano al origen — **la misma** que usa `RondaCentinelas.SpawnearDesdeCuartel`, así que la ronda sale encima tuyo. Cortocircuita toda la cascada de `DefinirPuntoDeSpawn`.
*   **Ignora `cargarPartidaAlIniciar`** a propósito: si no, el guardado te devolvía a donde estabas y el spawn en cuartel nunca se calculaba.
*   `radioBusquedaCuartelDebugMacros` (15) — si no encuentra cuartel avisa por consola y cae al spawn normal.
*   **⚠️ Fix relacionado en `RondaCentinelas`:** la primera ronda ya no sale en `Start`. El orden de `Start` entre scripts es **arbitrario**, así que podía spawnear la ronda desde el cuartel viejo antes de que el WG te teletransportara. Ahora espera a `WorldGenerator.MundoListo`.

### 21.4. `OnDrawGizmos` del WorldGenerator
Documentado (no cambiado): dibuja cada **celda** activa como wire cube celeste, solo en Play. **No** dibuja macro chunks.

### 21.5. Pendientes que deja esta sección
*   **Daño real del centinela al jugador.** Sigue sin hacer nada al alcanzarte (§18.5). Decisión tomada esta sesión: primero que persiga bien, el daño después.
*   **Que se avisen entre ellos.** `AlertaCentinelas` solo despierta la ronda; un centinela que te ve no le pasa tu posición a los otros vivos. Es el próximo salto grande de dificultad.
*   **Verificar los carriles de techo en Play** (§21.1) con semilla 1 — se implementaron y nunca se confirmó visualmente que luces y tuberías dejaran de pisarse.
*   **Percepción por ruido.** Hoy solo hay vista. Correr, la tirolina y romper paredes deberían alimentar `AlertaCentinelas` o directamente `ultimaPosJugador`.

---

## 22. DISEÑO (sin implementar) — Necesidades de la vivienda y personas perdidas

Sección de **diseño acordado**, no de código escrito. Lo que está acá todavía no existe salvo donde se aclare lo contrario.

### 22.1. Punto de partida: media pila ya está construida y sin usar
`RegistroViviendas.cs` ya lleva **agua / comida / energía por vivienda, en SEGUNDOS** (misma unidad que `PlayerData`), con dos propiedades que conviene no perder de vista:

*   **Determinista por semilla:** `CalcularStatsIniciales(macro)` hashea `semillaDelMundo + macro` y tira los tres valores. Misma semilla = mismas reservas.
*   **Estado por excepción:** el diccionario `viviendasModificadas` solo guarda las viviendas que el jugador TOCÓ. Una vivienda nunca visitada no ocupa RAM — sus valores se calculan al vuelo. Es lo que permite viviendas infinitas.
*   **⚠️ Las reservas NO drenan solas.** Decisión de arquitectura ya tomada y documentada en el propio script: sin timestamps, sin simulación offline. Con viviendas infinitas, simular el tiempo de todas es imposible.

`SeccionSala.Tipo` (Agua / Comida / Bateria / Dormir / Almacenamiento) ya tipa las esquinas de la vivienda y **hoy no hace nada** — es el gancho natural para la entrega física.

Lo que falta **no es almacenamiento**: es que esos números signifiquen algo. Nadie los lee, nadie los consume y el NPC no sabe que existen.

### 22.2. Decisiones tomadas
*   **La necesidad es FIJA POR SEMILLA.** No drena con el tiempo ni con la cercanía. Se descartaron las dos alternativas (drenar en la burbuja, drenar con reloj global) para no romper el estado por excepción. El drama sale de que hay muchas bocas y pocos recursos, no de un reloj corriendo.
*   **Los tres recursos siguen siendo independientes**, como ya están. Se evaluó darle a cada vivienda UNA carencia marcada + una `severidad` (para que cada casa tuviera identidad y el NPC una línea clara) y **se descartó**: el NPC va a pedir el recurso que tenga más bajo en ese momento. Cero campos nuevos en `RegistroViviendas`.
*   **Recompensa: items**, por ahora.
*   **La persona perdida NO es un sistema aparte — es la consecuencia de la carencia.** Una vivienda que se queda sin agua es exactamente la que mandó a alguien a buscar y no volvió. Un sistema con dos caras en vez de dos sistemas.
*   **Viva o muerta: sorteado por semilla, con la probabilidad atada a la DISTANCIA** del destino. Cuanto más lejos fue, más probable que no la haya contado — así la distancia significa riesgo narrativo y no solo caminata.

### 22.3. ⚠️ La trampa del estado derivado (resolver ANTES de escribir código)
Como no hay campo `severidad`, "esta vivienda está en crisis" se deriva: *su recurso más bajo está debajo de un umbral*. Eso es correcto para **elegir** qué pide el NPC, pero es una **bomba** si se usa para decidir si hay una persona perdida:

> Le llevás agua a la vivienda → deja de estar en crisis → **la persona perdida desaparece de la existencia**, con el NPC a mitad de la misión.

**Solución, y es la que el proyecto ya usa** para `habitante` y `macroChunkBuscado`: decidirlo **UNA sola vez, al registrar la vivienda**, mirando las stats **iniciales**, y guardarlo en `DatosVivienda`. De ahí en más manda el dato guardado, no el cálculo. `DatosVivienda` ya es exactamente eso (el registro persistente por vivienda), así que son tres campos más y nada de arquitectura nueva:

*   `tienePerdido` (bool)
*   `celdaPerdido` (Vector3Int) — destino, a N macro chunks en una dirección por hash
*   `perdidoVivo` (bool)

Es la misma clase de error que ya mordió esta sesión con los carriles de techo (§21.1): **estado derivado de algo que muta ≠ estado determinista**.

### 22.4. La persona perdida
*   **Destino:** una celda a N macro chunks en una dirección por hash. El NPC te canta la coordenada y los **carteles de coordenadas** (§20.7) hacen el resto — la infraestructura de orientación ya está construida.
*   **Muerta:** cuerpo + nota (reusa `NotaMundo`, §9) + lo que llevaba encima — muchas veces el agua que sí consiguió. Volver con eso a la vivienda es el remate.
*   **Viva:** la versión completa necesita un acompañante con pathfinding. Hay una versión barata que se siente casi igual y cuesta cero: la encontrás, hablás, y **se vuelve sola** — desaparece y aparece en la vivienda cuando volvés. El acompañante real puede venir después reusando el BFS de `TryCalcularRutaCeldas` (§18.45), igual que los centinelas.
*   Engancha natural con los centinelas: el perdido puede haber terminado cerca de un cuartel.

### 22.5. Karma — la red de viviendas ES el canal de reputación
Pedido del usuario, a construir más adelante. La recomendación de diseño registrada acá para no perderla:

**No hacerlo una barra global.** La **red de viviendas conectadas** (§ red de viviendas / `conectada`, `conectadaCon`) ya es un grafo construido, y es un canal de reputación mucho mejor que un número: cada vivienda recuerda qué hiciste ahí (ayudaste / robaste / la dejaste morir / trajiste el cuerpo) y eso **se propaga solo a las viviendas conectadas con ella**.

Eso da algo que un número no puede dar: **reputación LOCAL**. Podés ser un héroe en un sector y un ladrón en otro, y hay una razón mecánica de por qué se enteran. Y le da peso real a conectar viviendas, que hoy es una misión un poco abstracta.

### 22.6. Pendiente de decidir
*   **Entrega física:** ¿depositás el bidón en la `SeccionSala` de Agua, se lo das al NPC hablando, o las dos? (`SeccionSala.Tipo` existe justo para esto y hoy no se usa).
*   **Consecuencia de no ayudar:** la idea es que los NPC **empiecen a desaparecer** y que eso baje el karma. Encaja con el estado por excepción si el deterioro se resuelve **en el momento de mirar** (al volver a visitar) en vez de simularse — nada corre en background, se decide cuando la mirás.
*   **Escasez real:** hoy `rangoAguaInicial = (0, 1800)` es uniforme, y el jugador aguanta 600s con el tanque lleno — la vivienda promedio tiene el TRIPLE de lo que necesita el jugador. Para que "necesidad" sea una palabra con sentido, la distribución tiene que estar sesgada hacia abajo, con una cola de viviendas en crisis real.

### 22.7. Primer paso acordado
**Visualizar las necesidades**: un World Canvas dentro de la vivienda que muestre sus stats. Antes de cualquier mecánica de entrega o de misión, poder VER los números que ya existen. Mismo patrón que `PantallaInventarioVivienda` (§ pantalla LED), que ya resuelve lo difícil: deduce su propia vivienda por posición, sin que nadie le inyecte nada al instanciarla, y funciona embebida en el prefab de la sala.

---

## 23. Actualización 2026-09-10 — Pantallas de vivienda, drenaje, audio de centinelas y ducking

### 23.1. Panel de necesidades de la vivienda (`PanelNecesidadesVivienda.cs`, NUEVO)
World Canvas dentro del prefab de la sala que muestra las reservas. Solo LEE. Mismo patrón que
`PantallaInventarioVivienda`: **deduce su propia vivienda por posición**, sin que nadie le inyecte
nada al instanciarlo. Los dos componentes conviven en el mismo panel — uno muestra los números, el
otro lista los objetos.

*   **Un grupo por recurso** (`InfoStatVivienda`): `icono` (Image) + `texto` (TMP). Se evaluaron
    slider y barra Filled y **se sacaron a pedido del usuario** — no los necesitaba.
*   **Título con comodines**: `{COORD}` (macro chunk) y `{NOMBRE}` (habitante, vía
    `TryObtenerDatosVivienda`). El nombre sale vacío hasta que la vivienda se registra y se
    completa solo en el refresco siguiente.
*   Umbral crítico (300s por defecto) tiñe icono y texto.

**⚠️ Fix importante en `RegistroViviendas` que salió de esto:** las lecturas (`TryObtenerStats`,
`ObtenerRecurso`) iban por `ObtenerOCrear`, así que **leer CREABA la entrada** en el diccionario.
Un panel refrescando cada segundo habría materializado estado permanente por cada vivienda que
mirás, sin que el jugador cambiara nada — rompiendo el estado por excepción de la forma más
silenciosa posible. Ahora hay `ObtenerSoloLectura`, que calcula al vuelo y descarta.

### 23.2. ⚠️ Drenaje de reservas: reloj global SIN simulación
Cambio de la decisión de §22.2 (que era "fija por semilla, no drena"), a pedido explícito del
usuario. **Pero sin romper el estado por excepción**, que era el motivo original de no drenar.

El truco: en vez de restarle a cada vivienda todos los frames (imposible con viviendas infinitas),
cada `StatsVivienda` guarda **`tiempoUltimaActualizacion`** — a qué momento de la partida
corresponden sus valores. El descuento es **una sola resta en el momento de leerla**. Una vivienda
nunca visitada tiene la marca en 0, así que se le descuenta la partida entera de golpe la primera
vez que preguntás. **El costo depende de cuántas viviendas MIRÁS, no de cuántas existen.**

*   `RegistroViviendas.TiempoMundo` avanza con `Time.deltaTime` ⇒ **pausar no drena**.
*   Campos nuevos: `drenajeAgua/Comida/Energia` (0.1 por defecto = 1800s se vacían en 5 horas).
    0 vuelve al comportamiento congelado.
*   **Leer no muta**: `ObtenerSoloLectura` devuelve una COPIA con el drenaje aplicado; la entrada
    del diccionario queda intacta. Da igual cuántas veces pregunte el panel.
*   **Las escrituras LIQUIDAN primero** (`ObtenerOCrear` llama a `AplicarDrenaje` antes de devolver).
    Sin eso, sumarle 60s de agua a una vivienda con media hora de drenaje pendiente lo sumaría al
    valor viejo y el orden de las operaciones regalaría o se comería reservas.

**Pendiente:** `TiempoMundo` arranca en 0 en cada Play y **no se guarda**. Al cargar una partida el
mundo vuelve a estar recién empezado.

### 23.3. Bitácora: pestaña Misiones, y arreglos del registro
*   **Modo `esMisiones`** en `BitacoraUI.Pestana`. Muestra el mismo formato que
    `HUDMisionViviendas` (`[SIN ESCUCHAR]` / `[EN CURSO]` / `[COMPLETADA]`, nombre, celda, y a dónde
    ir), con **una diferencia deliberada: solo lista las viviendas que YA VISITASTE**. El HUD lista
    todas las registradas, y el generador las registra apenas la burbuja las genera — mostrarlas
    todas le chivaría al jugador viviendas donde nunca estuvo.
*   **Refresco periódico** (`intervaloRefresco`, 1s, solo si `ventana.activeInHierarchy`). Hacía
    falta porque los descubrimientos de items y lugares avisan por evento, pero **aceptar una misión
    o conectar dos viviendas cambia `DatosVivienda` sin disparar nada**.
*   **⚠️ La vivienda del SPAWN no se descubría nunca.** El descubrimiento vivía dentro del
    `if (celdaJugador != celdaActualJugador)` del Update, y al arrancar `celdaActualJugador` ya vale
    `celdaSpawn` — no hay cambio que dispare. Se extrajo a `IntentarDescubrirLugarActual()` y se
    llama también en los dos puntos donde `mundoListo` pasa a true.
*   **`ReiniciarCodex()`**: `RegistroDescubrimientos.Reset()` + `RegistroViviendasVisitadas.Reset()`,
    enganchado en `Start`, `IniciarNuevaPartidaAleatoria` y `ReiniciarMundoConSemillaAleatoria`.
    Nadie los llamaba. En un reinicio con semilla nueva **no es cosmético**: las viviendas visitadas
    se guardan por COORDENADA de macro chunk, y en el mundo nuevo esa coordenada es otra cosa. El
    de `Start` cubre el editor con "Enter Play Mode" sin domain reload, donde los estáticos
    sobreviven a salir y volver a entrar.
*   Recordatorio: **un item se descubre al INSPECCIONARLO** (`ManoJugador.EntrarInspeccion`), no al
    tomarlo — el comentario del script decía lo contrario.

### 23.4. Audio de centinelas (`Centinela.cs`)
Antes no hacían **ningún ruido**: un enemigo del que hay que esconderse y que no se escucha venir no
es una amenaza, porque en sigilo la información llega por el oído antes que por la vista.

*   **`sonidoPatrulla`** — loop 3D que suena SIEMPRE mientras el centinela vive, también durante la
    persecución. Es lo que permite ubicarlo de oído.
*   **`sonidosAlerta`** (array, sorteo) — one-shot al pasar a Persiguiendo, con **`cooldownAlerta`**
    configurable: 0 = suena cada vez que te vuelve a ver, alto = una por episodio. Sin cooldown,
    perder y recuperar el contacto detrás de una columna lo dispara en ráfaga.
*   Va por `PlayOneShot` para **no pisar el loop**: el bicho grita sin dejar de sonar a máquina.
*   **El disparo está en `CambiarEstado`, no en `EntrarPersiguiendo`** — ahí pasan TODAS las
    transiciones, así que una vía de detección futura queda cubierta sola.
*   Rolloff **LINEAL** con `distanciaMaximaSonido` (40m): en sigilo conviene un radio de audición
    predecible, no una curva logarítmica.
*   El AudioSource se configura por código (3D, rolloff, distancias) para que un centinela nuevo
    suene bien asignando solo el clip.

### 23.5. Ducking del ambiente (`DuckingAmbiente.cs`, NUEVO)
Pedido del usuario: que el ambiente base **se agache cuando suena algo importante**, en vez de
competir con él.

Bus estático, mismo idioma que `ModoMenu`/`ModoTablet`. El emisor llama a `Pedir(factor)` **todos
los frames** mientras quiera el ducking; **no hay que avisar cuándo termina** — si deja de pedir, el
factor vuelve solo a 1. Eso evita el bug clásico de que el ducking quede colgado cuando el emisor
muere, despawnea o cambia de estado. Gana el pedido más agresivo del frame.

**⚠️ `AmbientSounds` lo consume en `LateUpdate`, no en Update.** El orden de Update entre scripts es
arbitrario: leerlo ahí se perdería los pedidos de los emisores que corrieran después. Es la misma
clase de bug que el `teVe` por frame (§21.2) y el flash de la tirolina con `ModoTablet`.

`Centinela.duckingAmbiente` (0.3) pide el ducking **proporcional a la distancia**: encima tuyo baja
al máximo, en el borde de `distanciaMaximaSonido` no baja nada.

### 23.6. Ambiente dinámico por contexto (`AmbientSounds.cs`)
Capas que se suman ENCIMA del base según lo que estés haciendo, cada una con su propio AudioSource
creado en runtime. Contextos, **no excluyentes entre sí**: `Vivienda`, `Pasillo`, `Exterior`,
`Quieto`, `Trepando`. Cada capa tiene loop + un `clipEntrada` opcional que suena **solo en el flanco**
de entrada al contexto.

*   `Quieto`: `segundosParaQuieto` sin moverte más de `umbralMovimientoQuieto`. El umbral va en
    **metros y no en velocidad** para que mirar alrededor o el jitter del CharacterController no
    rompan el estado. El contador corre todos los frames; la evaluación de contexto va throttleada
    (`intervaloEvaluacionContexto`) porque consultar el tipo de macro chunk cuesta.
*   `Trepando` lee `SistemaEscalera.estaEnEscalera`, `SistemaRapel.estaEnRapel` y
    `SistemaTirolina.EnTirolina` (esta última se expuso, era privada).

### 23.7. ⚠️ Auditoría: que NADA suene antes de tomar el personaje
Se revisó sistema por sistema. Dos sonaban durante el menú:

*   **`OrbitadorSonoro`** no tenía ningún gate: programaba su primer sonido en `Start` y disparaba al
    vencer el reloj. Ahora espera `MundoListo && !ModoMenu.Activo` y **REPROGRAMA** el temporizador
    en la transición — si solo se silenciara, al terminar el menú dispararía de inmediato el sonido
    acumulado. Mismo criterio que ya usaban `SustoVapor` y `SistemaDesprendimientos`.
*   **`AmbientSounds`**: el `retrasoDespuesDelMenu` solo cubría el base y el exterior. El **viento
    del vacío** entraba a volumen completo si el spawn caía expuesto, y el **temporizador de ráfagas**
    corría desde `Start` — un recorrido de cámara largo consumía los 15-35s y metía una ráfaga en el
    primer segundo de partida. `puedeSonar` pasó de local a campo y ahora lo miran los tres.
*   **Centinelas**: `RondaCentinelas` esperaba `MundoListo`, pero **el mundo queda listo ANTES de que
    termine el recorrido de cámara del menú**, así que la primera ronda salía sonando sobre la
    intro. Ahora espera también `!ModoMenu.Activo`, y el propio `Centinela` se auto-mutea mientras
    el menú esté activo.

### 23.8. Arreglos varios de esta sesión
*   **`MouseLook`: la cámara giraba sola con el juego en pausa.** Con `timeScale 0`, `Time.deltaTime`
    vale 0 ⇒ el factor del filtro queda en `k = 1 - exp(0) = 0` ⇒ el `Lerp` devuelve el valor
    ANTERIOR ⇒ `mouseXSuave/mouseYSuave` se congelan con la última velocidad, y `xRotation` se sigue
    aplicando cada frame. Se agregó `FrenarSuavizado()`, llamado al pausar Y al perder `canLook` —
    esto último arregla de paso el tirón al cerrar la mochila o la tablet girando.
*   **`MenuPausa`: el cuadro de diálogo quedaba pegado al reanudar.** `SetOcultosAlPausar(true)`
    **encendía todos** los objetos del array sin mirar cómo estaban. El panel del diálogo está
    normalmente apagado, reanudar lo prendía, y como `DialogoUI` ya había consumido su temporizador
    no tenía nada que vencer. Ahora guarda y repone el estado previo de cada uno.
    **Regla general del proyecto: todo array de "ocultar mientras X" tiene que guardar el estado
    previo**, porque siempre hay algo que ya estaba apagado por otro motivo.
*   **`TabletaMouse`: la tablet se perdía.** `LateUpdate` la congelaba con `SetPositionAndRotation`
    en coordenadas de MUNDO, pero es hija de la cámara, así que esa llamada le reescribe el
    `localPosition` contra el padre. Caminando con el botón apretado, el offset local se iba a
    cualquier lado, y al soltar no se le devolvía su pose local ⇒ quedaba clavada lejísimos, con la
    mecánica funcionando igual porque el objeto seguía activo. Ahora guarda la pose LOCAL en
    `OnEnable` (único momento garantizado en que está en su lugar) y la repone al soltar.
*   **`BitacoraUI` sin tecla propia.** Vive dentro de la tablet, se abre con ella (tecla 1). Se le
    quitó el `Update` y el campo `tecla`; `Awake` ya no la apaga (queda siempre activa) y `Start`
    la pinta. `TogglePantalla` se hizo cargo del cursor y del `MouseLook`.
*   **Carteles de sala: `cartelSalaSoloAlNorte`** (ON). El cartel con el nombre de la sala va solo en
    la cara norte (+Z, misma convención que `DireccionCardinal`) en vez de las 4 — deja de ser
    decoración repetida y pasa a funcionar de brújula: si lo ves, mirás al norte.
*   **`PuntoSpawnItem` en prefabs de PARED.** `ProcesarPuntosSpawnItem` no se llamaba para paredes
    (igual que antes pasaba con las pasarelas). Se agregó `ProcesarPuntosSpawnItemDePared`, que
    siembra el Random con un hash de **posición + normal interior** — la normal hace falta porque las
    dos paredes que comparten un plano están en la misma posición y sin ella sortearían idéntico.
    **Las paredes son el lugar correcto para los props laterales**: su rotación es determinista
    (`RotacionDesdeNormalInterior`), mientras que los prefabs de PISO reciben un yaw aleatorio de
    0/90/180/270 — un punto pegado a un borde ahí termina contra una pared o en mitad del camino,
    mitad y mitad.
*   **Linterna: enfoque fijo.** De los 3 pasos solo "Medio" se veía bien. El ciclado por click de
    rueda quedó COMENTADO (no borrado) y hay `enfoqueFijo` + `indiceEnfoqueFijo`. Se usó un campo
    NUEVO porque `enfoqueInicial` ya estaba serializado en 0 en la escena y Unity ignora el default
    del script cuando el campo ya existe guardado. Efecto colateral útil: **el botón del medio del
    mouse quedó libre**.
*   **`spawnEnCuartelDebug`** en el WorldGenerator (§21.3) — buscar un centinela a pie costaba
    minutos por sesión de testeo.

### 23.9. Pendientes que deja esta sección
*   **Persistencia**: ni los codex (`RegistroDescubrimientos`, `RegistroViviendasVisitadas`) ni
    `RegistroViviendas.TiempoMundo` se guardan. `SistemaGuardado` hoy solo guarda semilla, posición
    y rotación.
*   **Pestaña "Notas"** de la bitácora: no hay de dónde leer. Habría que crear un registro de notas
    leídas y que `LectorNotas` lo alimente.
*   **Escasez real** (§22.6): con `rango*Inicial` en (0, 1800) uniforme, la vivienda promedio tiene
    el triple de lo que necesita el jugador.
*   **Resto del audio que falta**: `ManoJugador` (agarrar/soltar/guardar — la acción más frecuente
    del juego y es muda), los aparatos (`InterfazBrazo`, tablet, bitácora, botones de mundo, menú),
    avisos de stat baja en `PlayerData`, `LectorNotas`, `DronVagabundo` (posicional),
    `SistemaRapel`, `PantallaInventarioVivienda`.
*   **Entrega física de recursos** a la vivienda y **karma** (§22.5, §22.6).

---

## 24. Actualización 2026-09-11 — La red de viviendas estaba muerta (caso de estudio de debugging)

### 24.1. El síntoma
Todos los NPC de vivienda decían `"..."` y ninguno daba misión. `CONEXIONES: 0 / 1` en el HUD.

⚠️ **Y la tablet mentía:** al hablarle, la misión pasaba a `[EN CURSO]`. Eso hizo perder tiempo,
porque parecía que el sistema funcionaba. En realidad `OnHablarConVivienda` pone
`misionAceptada = true` **siempre**, sin mirar si había objetivo — **ver "EN CURSO" NO significa
que haya misión de verdad.**

### 24.2. La causa real, encadenada
1.  Se bajó **`periodoIslas` a 4** (buscando islas más juntas — ver §24.5).
2.  Eso hace `rMax = Min(radioIslaChunks.y, (p-1)/2 - 1) = Min(2, 0) = 0`, que el `Max(1, …)` sube
    a **1**. Todas las islas pasan a radio 1: cubos de **3×3×3** macro chunks.
3.  En un cubo 3×3×3 **todos** los chunks tocan el vacío, y el único interior es el centro — ya
    reservado para la columna-pozo.
4.  `SalasDeIsla` tenía `if (ChunkTocaVoid(...)) continue;  // solo núcleo` ⇒ **cero candidatos**.
    Cada isla quedaba solo con su cuartel, que sí tenía su cascada de respaldo
    (núcleo → borde → loQueSea).
5.  La única vivienda del mundo era la del spawn (forzada por el tipado). Sin una segunda vivienda,
    nadie tiene con quién emparejarse ⇒ `tieneObjetivo = false` ⇒ sin misión.

**Fix:** en `SalasDeIsla` el borde pasó de **descarte** a **respaldo**, igual que ya hacía
`TryCuartelDeIsla`. Se barajan núcleo y borde por separado y se concatenan con el núcleo primero;
como la cascada de separación recorre en orden y corta al llegar al objetivo, el casco solo entra
cuando el núcleo no alcanzó. Una isla chica ahora tiene gente en el casco en vez de quedar desierta.

### 24.3. ⚠️ Cómo se encontró — el método, que importa más que el bug
Se tiraron **tres teorías razonables y las tres eran falsas**. Lo resolvió instrumentar y medir.

Logs progresivos en `BuscarViviendaCercana`, cada uno partiendo el espacio de causas al medio:
1.  *"salas tipo 1 vistas / descartadas por ser cuartel"* → **148 de 148 eran cuartel**. Descartó de
    un saque que fuera el radio de búsqueda.
2.  *"¿por la garantía de isla o por la ruleta ponderada?"* → **148 por garantía**, que era
    **aritméticamente imposible**: con `periodoIslas: 9` entran ~32 regiones en el radio, o sea 32
    cuarteles como techo.
3.  Imprimir **`periodoIslas` en el log** → valía **4**, no 9. Ahí cerró todo.

⚠️ **La lección:** el prefab `Assets/Prefabs/WorldGenerator.prefab` decía 9, pero el valor real en el
Inspector era 4. **Los valores de generación NO están en la escena** (el WG es instancia de prefab) y
el prefab puede estar desfasado de lo que corre. **Nunca asumir un valor de configuración: loguearlo.**

Teorías falsas que se descartaron con datos, por si vuelven a tentar:
*   **No era TMP** — el rect del diálogo es 700×150 y el overflow está en `Overflow`, no `Ellipsis`.
*   **No era `HabitanteData`** — el generador tiene textos por defecto; el log confirmó 2 RoomData
    cargadas, 1 no-cuartel, pesos correctos.
*   **No era el alcance vertical** de la búsqueda (aunque sí era un bug real: ver §24.4).

### 24.4. Bugs reales encontrados de paso (arreglados, no eran la causa)
*   **`radioYBusquedaViviendaMacros`** (campo nuevo, default 5). `BuscarViviendaCercana` escaneaba
    `dy` de **-2 a +2** hardcodeado. En una isla de radio 2 (5 macro chunks de alto) desde el chunk
    de spawn la distancia Chebyshev máxima en XZ es 2, y `separacionMinimaSalasChunks` (3) se mide
    **incluyendo Y** — o sea que el generador está OBLIGADO a empujar las otras salas a `my = 3` o
    `4`, justo fuera del alcance. Va aparte del radio horizontal porque el volumen crece al cubo.
    ⚠️ **Regla: nunca menor que `radioIslaChunks.y * 2 + 1`.**
*   **La cáscara de búsqueda se define solo en XZ**, así que la columna `dx==0 && dz==0` nunca entra
    (daría `Max=0` y `r` arranca en 1). En la práctica no importa —`SalasDeIsla` ya excluye la
    columna central de la isla, que es justo esa— pero quedó documentado en el código.

### 24.5. Hallazgo de diseño: la generación es PLANA a propósito
Investigando por qué el mundo "no tiene pisos", se encontró en `CentroIsla`:

```csharp
int cx = rx * p + Random.Range(margen, p - margen + 1);
int cz = rz * p + Random.Range(margen, p - margen + 1);
return new Vector3Int(cx, ry * p + radio, cz);   // ← Y FORZADO
```

**El centro de la isla se jitea en X y Z, pero en Y está clavado** en `ry * p`, el piso de su región.
Es deliberado: los puentes corren en `my = ry*p` y así tocan la base de toda isla sin importar su
radio. Consecuencia: **todas las islas de una banda están perfectamente a nivel.** Y las bandas
verticales están a `periodoIslas` macro chunks una de otra, sin nada que las conecte (los puentes son
horizontales) — de ahí que tirarse al vacío no genere nada.

Tres caminos anotados, sin implementar, de más barato a más caro:
1.  **Período de Y separado.** Hoy el período es cúbico (el mismo en X, Y, Z). Un `periodoIslasY` más
    chico apilaría bandas más cerca sin tocar los puentes ni el contrato de la base.
2.  **El pilar central como pozo pasante / ascensor.** La columna central de cada isla **ya está
    forzada a pozo (tipo 6)**; extenderlo hacia la banda de arriba o abajo es la continuación natural.
3.  **Jitter en Y.** Una línea, pero rompe el contrato de los puentes: dejarían de encontrar la base.

### 24.6. Diálogo: "sin objetivo" dejó de ser un hueco
*   `TryObtenerPistaVivienda` **ya no devuelve `false`** cuando la vivienda no tiene objetivo:
    responde con una frase real. `NPCHabitante` interpretaba el `false` como "nada que decir" y
    mostraba `"..."`, que en pantalla se lee como un bug. Una vivienda realmente aislada es **parte
    de la ficción**, no un error.
*   Campo nuevo **`HabitanteData.frasesSinObjetivo`** para escribirlo por personaje. Es buen lugar
    para sembrar **La Oquedad**: alguien aislado es justo quien diría "el último que salió se fue
    para allá".
*   El `"..."` de "ya te agradeció y no hay `charlaPosMision`" pasó a una frase real. Si se quiere un
    habitante callado, hay que cargarle los puntos a mano — que sea una decisión, no un hueco.
*   El `"..."` que queda en `NPCHabitante` ahora **loguea un warning**: desde estos cambios solo se
    llega ahí si el NPC está parado donde NO hay vivienda registrada.
*   Toggle **`logBusquedaVivienda`** (prendido) con todo el diagnóstico. Apagarlo cuando moleste.

### 24.7. Otros arreglos de la sesión
*   **`TabletaMouse`: la tablet se perdía al caminar.** `LateUpdate` la congelaba con
    `SetPositionAndRotation` en coordenadas de MUNDO, pero es hija de la cámara: esa llamada le
    reescribe el `localPosition` contra el padre, así que caminando con el botón apretado el offset
    local se iba a cualquier lado. Ahora guarda la pose **LOCAL** en `OnEnable` (único momento
    garantizado en que está en su lugar) y la repone al soltar. ⚠️ Con `MouseLook` bloqueado durante
    el modo mouse, **`congelarEnElMundo` ya no aporta nada** — la cámara no puede girar igual.
*   **Bitácora: pestaña Misiones** (`esMisiones`), refresco periódico, y la vivienda del spawn que no
    se descubría nunca (ver §23.3).
*   **Avisos pendientes que aparecieron en consola y NO se tocaron:** decenas de
    `BoxCollider does not support negative scale or size` por celda (algún prefab se instancia con
    escala negativa ⇒ collider desalineado del modelo), y
    `'PasarelaDerecha_Recta_Escalera' no tiene ningún hijo 'Conector'`.

## 25. Actualización 2026-11-11 — La comunidad: forma, gente, misiones y zonas
> Desde acá, **vivienda y comunidad son UNA sola cosa**: el único lugar donde vive gente es la
> comunidad (tipo 11). El tipo 1 es el **cuartel de centinelas** y conserva su forma fija 3x3 + balcón.

### 25.1. Silueta tetris (`WorldGenerator.FormaComunidad`)
*   Mapa de alturas sobre la planta 5x5, repartido por **bloques** de 3x3 (bandas {0,1},{2},{3,4}):
    esquinas 2x2, lados 1x2, centro 1x1. Cada bloque recibe 1..4 pisos (`pisosPorBloqueComunidad`);
    el centro siempre llega al techo. Columna hueca de ly=1 hasta su altura, sólida arriba.
*   **Mordiscos** de esquina (`probabilidadMordiscoComunidad`): tapa la esquina y uno o los dos brazos (L).
*   Intocables en planta baja: el 3x3 interior (centro + 4 zonas de secciones) y las 4 puertas.
*   `System.Random` propio por semilla+macro y caché (`cacheFormaComunidad`) limpiada junto a `cacheTipoChunk`.
*   ⚠️ Lección: variar solo el piso alto no se nota; lo que hace ver "cuadrada" una sala es la **planta**.
    Receta reutilizable para otras salas.

### 25.2. Andamios, caminos y pilares (debug, base de lo que viene)
*   `PuntosAndamioComunidad(macro)`: 1..3 puntos por piso en paredes (ly≥2), prefiriendo paredes distintas,
    **nunca pegados** (ni al lado, ni encima, ni en diagonal), a veces se salta un piso.
*   `CaminosComunidad(macro)`: Dijkstra de celdas desde el piso hasta cada punto y el centro del techo,
    pegado a las paredes (`costoLejosDePared`, `costoEscalera`), más una **segunda subida** separada.
*   `DebugAndamiosComunidad` (lo agrega solo el WorldGenerator) dibuja puntos y caminos de la comunidad
    donde está la cámara. Toggles `dibujarPuntosAndamio` / `dibujarCaminosAndamio`.
*   Pilares (`prefabPilarComunidad`, 1..2) en vértices interiores hasta el techo más bajo.
*   **Pendiente:** prefabs colgantes en los puntos (con `ParedPozo` en el lado que va contra la pared) y
    escaleras/pasillos reales sobre los caminos.

### 25.3. Habitantes con rutina (`HabitanteRutina.cs`)
*   **Sin estado**: posición y actividad salen del reloj global, igual que el drenaje (§23.2).
*   `Profesion {Centro, Agua, Comida, Energia, General}`; cada uno va a trabajar a su zona
    (`ObtenerZonaDeSeccion` — **única fuente** del reparto sección↔zona, `OrdenZonasSecciones`).
*   Frases por profesión: assets `FrasesProfesion` en `Resources/Profesiones` (Create → Mundo → FrasesProfesion),
    con fallback a frases por defecto.
*   **Esquivan al jugador** (`radioEsquive`, `esquiveMaximo`, `suavidadEsquive`): de costado si caminan,
    se apartan si están quietos. Collider trigger: nadie empuja a nadie.
*   **Animados**: Y Bot (Humanoid) + clips de Quaternius UAL (gratis). Controller `NPC_Rutina_Animator`
    con bool `Caminando`. ⚠️ Los clips se sacan de un FBX **ya en Humanoid** (si se duplican siendo
    Generic, caminan "sentados"). Root motion apagado. `alturaBase` sale del pivote humanoide, no de los bounds.
*   `NPCHabitante.cs` sigue vivo: es el de la misión de la red de viviendas (Cylinder en Centro.Vivienda).

### 25.4. Misiones de cofre (`MisionData.cs`)
*   Uno o varios objetos pedidos con cantidad (`objetosPedidos`), garantizados en el cofre; se entregan de a uno con [E].
*   **[G] Quedármelo**: con el objeto en la mano podés negarte (`frasesRechazo`, `karmaQuedarse`).
*   El cofre se apoya en el **piso real** (`ApoyarEnPiso`: raycast + base de los renderers), ya no se entierra.

### 25.5. Secciones orientadas por marcadores
*   Si el prefab de sección trae `ParedPozo`, esos lados son **cerrados** y se gira (de a 90°) para que
    ninguna entrada quede contra algo sólido (`RotacionSeccionSegunParedes`). Sin marcadores: lado libre de siempre.

### 25.6. Zona de relleno / Zona General (`ZonaGeneral.cs`, `RoomData.seccionesRelleno`)
*   Las celdas huecas del **anillo de afuera** en planta baja (sin puertas ni arranques de escalera)
    reciben un prefab de `seccionesRelleno`, elegido por hash de celda. La gente usa ese espacio para lo
    que haga falta (comer, armar casas a la rápida…).
*   `ZonaGeneral` en el root del prefab lo llena ordenado y por semilla:
    *   **Faroles**: 1 al centro (`probabilidadFarolCentro`) o en 1..4 esquinas, mirando al centro.
    *   **Mesas** en grilla pareja según el tamaño de la celda (`margen`, `espacioMesa`), mismo giro base
        + `giroAlAzar` (≤20° por defecto), `desorden` de posición.
    *   `alturaExtra`: compensa el grosor del piso (todo se instancia en el piso de la grilla).
*   Sillas: `PuntoSpawnItem` dentro de la mesa con **`mirarAlCentro`** (apunta al centro del padre +
    `giroAlAzar`; `yawBase` si el modelo mira al revés).
*   ⚠️ Reglas que costaron: los props necesitan el **pivote en su base y centrado** (el farol de la
    Sección Energía tenía el modelo corrido 7 m y aparecía en una esquina); y los cambios en la copia de
    la **escena** no llegan al generador — se aplican al **prefab** (Overrides → Apply All).
*   **Siguiente:** cajas, barriles y "casas a la rápida" en la misma zona.

### 25.7. Arreglos
*   **Marco de boca de pasillo dentro de las puertas**: `IntentarGenerarMarcoTerminoPasillo` ponía las
    aristas `_termino_*` aunque en esa cara fuera una puerta de cambio de sección (`_cierre`). Ahora
    consulta `TransicionConPuerta` (la misma regla que el cierre) y se salta esa cara.

### 25.8. Ideas pendientes (no empezar sin que se pida)
*   **Sucesos emergentes**: las misiones no aparecen al llegar ni se marcan (nada "pintado de amarillo").
    Pasan solas en la comunidad: alguien ya está en cama, en algún momento se enferma, la gente se
    reúne, hay murmullos; el jugador lo descubre mirando. Fases latente → desencadenado → descubierto →
    resuelto/fallido, con huella (cama vacía, frases, karma). Asset "Suceso" genérico. Timeline ya está instalado.
*   Variedad de caminatas por habitante (varios clips).
*   Assets: varios FBX pesan >10 MB (mesas, silla, farol, escaleras 78 MB) y no se suben al repo; reexportar livianos.
