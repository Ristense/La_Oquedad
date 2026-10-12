using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class WorldGenerator : MonoBehaviour
{
    public static WorldGenerator Instance { get; private set; }
    public bool MundoListo => mundoListo;

    [Header("Configuración Base")]
    public float cellSize = 20f; 
    
    [Header("Prefabs de Construcción")]
    public GameObject[] prefabsPared;

    public GameObject[] prefabsPiso; 
    public GameObject prefabPisoSeguro;

    [Header("Carteles de Coordenadas")]
    [Tooltip("Prefab del cartel de coordenadas que se instanciará en las paredes.")]
    public GameObject prefabCartelCoordenadas;
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que aparezca un cartel de coordenadas en una pared.")]
    public float probabilidadCartelCoordenadas = 0.25f;
    [Tooltip("Distancia desde la pared hacia el interior del pasillo/sala para evitar que el cartel quede dentro del modelo de pared.")]
    public float cartelCoordenadasOffset = 1.0f;
    [Tooltip("Separación mínima (en CELDAS) entre carteles de coordenadas.\n\nLa probabilidad sola no garantiza nada: dos celdas vecinas pueden sacar cara las dos y quedan dos carteles pared con pared. Esto los fuerza a una retícula, así que en un pasillo recto sale uno cada N celdas exacto.\n\n1 = sin separación (comportamiento viejo).\n\nOJO: la probabilidad se sigue aplicando ENCIMA de la retícula. Con separación 5 y probabilidad 0.25 solo 1 de cada 4 puntos permitidos recibe cartel, o sea uno cada ~20 celdas. Si ponés separación, probablemente quieras SUBIR la probabilidad (0.7-1.0) y dejar que la separación haga el trabajo de espaciar.")]
    [Min(1)]
    public int separacionCartelesCeldas = 5;
    [Tooltip("ON = el cartel con el NOMBRE DE LA SALA (el de la celda central) se pone SOLO en la cara NORTE, no en las 4. Así deja de ser decoración repetida y pasa a ser una brújula: si lo estás viendo, estás mirando al norte.\n\nNorte = +Z, la misma convención que usa DireccionCardinal para las pistas de los NPC ('hacia el NORESTE'), así que las dos cosas concuerdan.\n\nOFF = las 4 caras, como antes.")]
    public bool cartelSalaSoloAlNorte = true;
    [Header("Pilares Estructurales")]
    public GameObject[] prefabsPilaresGigantes;
    [Header("Paredes Exteriores")]
    [Tooltip("Prefabs de pared para la carcasa exterior del mapa.")]
    public GameObject[] prefabsParedExterior;
    [Tooltip("Prefabs de pared exterior final de grupo (más cortos, opcionales) para esquinas cerradas/cóncavas.")]
    public GameObject[] prefabsParedExteriorEnd;
    [Tooltip("Prefabs de pilares chicos que sostienen las paredes exteriores.")]
    public GameObject[] prefabsPilaresParedExterior;
    [Tooltip("Generar también estos pilares entre el pasillo y las placas de techo/piso exterior (mismo prefab, girado hacia la normal vertical).")]
    public bool pilaresEnTechoPisoExterior = true;
    [Tooltip("Prefabs de arista que conectan la pared exterior con el techo del pasillo (se elige uno al azar).")]
    public GameObject[] prefabsAristaExteriorTecho;
    [Tooltip("Prefabs de aristas de techo final de grupo (más cortos, opcionales) para esquinas cerradas/cóncavas.")]
    public GameObject[] prefabsAristaExteriorTechoEnd;
    [Tooltip("Prefabs de arista que conectan la pared exterior con el piso del pasillo (se elige uno al azar).")]
    public GameObject[] prefabsAristaExteriorPiso;
    [Tooltip("Prefabs de aristas de piso final de grupo (más cortos, opcionales) para esquinas cerradas/cóncavas.")]
    public GameObject[] prefabsAristaExteriorPisoEnd;
    [Tooltip("Prefabs de arista VERTICAL para las 4 esquinas exteriores de las salas. Modelar mirando hacia +Z con pivote en la base inferior centrado.")]
    public GameObject[] prefabsAristaExteriorVertical;
    [Tooltip("Activa la generación de aristas horizontales en las paredes exteriores de los pasillos.")]
    public bool generarAristasExteriores = true;
    [Tooltip("Probabilidad de generar aristas horizontales en los pasillos (0 a 1).")]
    [Range(0f, 1f)]
    public float probabilidadAristaExterior = 1f;
    [Tooltip("Activa la generación de aristas verticales en las 4 esquinas de las salas (tipo 1).")]
    public bool generarAristasVerticales = true;
    [Tooltip("Niveles ly dentro del macro chunk de sala en los que se generan las aristas verticales (default: 1 y 3 — los 2 pisos).")]
    public int[] nivelesAristasVerticales = new int[] { 1, 3 };
    [Tooltip("Offset adicional hacia afuera (en la diagonal) desde la esquina interior de la pared exterior.")]
    public float offsetAristaVertical = 0f;
    [Tooltip("Offset en Y para la arista vertical (sube/baja el prefab respecto al piso de la celda). Si el pivote del prefab está en su centro, dejá esto en cellSize/2 (ej. 10).")]
    public float offsetYAristaVertical = 10f;
    [Tooltip("Yaw base (grados). Default 0 asume prefab modelado para esquina NW. Ajustá en pasos de 90 si está modelado para otra.")]
    public float yawBaseAristaVertical = 0f;
    [Tooltip("Prefabs de vértices convexos exteriores de techo para conectar aristas horizontales en las esquinas superiores.")]
    public GameObject[] prefabsVerticeConvexo;
    [Tooltip("Prefabs de vértices convexos exteriores de piso (volteados) para conectar aristas en las esquinas inferiores.")]
    public GameObject[] prefabsVerticeConvexoPiso;
    [Tooltip("Yaw base para los vértices convexos exteriores (grados).")]
    public float yawBaseVerticeConvexo = 0f;



    [Tooltip("Probabilidad de generar una pared exterior en cada muro que lo permita (0 = apagado, 1 = siempre).")]
    [Range(0f, 1f)]
    public float probabilidadParedExterior = 0.18f;
    [Tooltip("Distancia desde la pared interior hacia afuera para colocar la pared exterior.")]
    public float paredExteriorOffset = 4f;

    [Header("Pisos Exteriores")]
    [Tooltip("Prefabs de piso para la cara exterior (debajo del suelo de los pasillos).")]
    public GameObject[] prefabsPisoExterior;
    [Tooltip("Probabilidad de generar un piso exterior (0 = apagado, 1 = siempre).")]
    [Range(0f, 1f)]
    public float probabilidadPisoExterior = 0.5f;

    [Header("Techos Exteriores")]
    [Tooltip("Prefabs de techo para la cara exterior (encima del techo de los pasillos, visible desde afuera/arriba de la mega-estructura).")]
    public GameObject[] prefabsTechoExterior;
    [Tooltip("Probabilidad de generar un techo exterior (0 = apagado, 1 = siempre).")]
    [Range(0f, 1f)]
    public float probabilidadTechoExterior = 0.5f;

    [Header("Navegación Vertical")]
    [Tooltip("LEGACY: solo se usa en Salas Chicas que NO tienen RoomData asignada. No tiene nada que ver con los pozos verticales — eso es prefabsCentroPozo.")]
    public GameObject[] prefabsEscaleras;

    [Tooltip("Objetos que pueden aparecer en el centro de cada celda de un POZO VERTICAL (tipo 6): escaleras, andamios, tubos, lo que sea. Se sortea uno POR CELDA, así que una columna puede mezclar prefabs distintos entre nivel y nivel. El pozo talla una columna de 1x1 celda (lx=2, lz=2) a todas las alturas, así que el objeto va centrado y apoyado en el piso de su celda.")]
    public GameObject[] prefabsCentroPozo;

    [Tooltip("Probabilidad de que una celda de pozo reciba objeto. Se tira POR CELDA: con 0.7 vas a ver columnas con huecos. 0 = apagado (ningún pozo lleva nada), 1 = todas las celdas llevan. OJO: el pozo central de cada isla es su esqueleto vertical — si acá poné un valor bajo, puede tocarte una isla sin forma de subir a pie y vas a depender del rapel/tirolina.")]
    [Range(0f, 1f)]
    public float probabilidadObjetoPozo = 0.75f;

    [Tooltip("Paso (en grados) del giro aleatorio de los objetos del pozo. SOLO gira en Y: nunca se inclinan.\n\n90 = cae encarado a una de las 4 paredes. Como el pozo es cuadrado, una escalera queda siempre apoyada contra un muro — desordenado pero coherente.\n\n0 = giro libre 360°, más caótico, pero una escalera puede quedar en diagonal sin apoyarse en nada.\n\nTambién sirven 180 (dos orientaciones) o 45 (ocho).")]
    public float pasoRotacionPozoGrados = 90f;

    [Header("Datos de Salas (Loot)")]
    [Tooltip("Se cargan automáticamente todos los RoomData en Resources/RoomData/ en Awake. Si dejás algo asignado a mano acá, se respeta y NO se sobreescribe.")]
    public RoomData[] salasGrandesPosibles;

    [Header("Secciones de Vivienda (agua / comida / batería / dormir)")]
    [Tooltip("Las secciones van en las 4 esquinas del 3x3, y cada esquina tiene 2 lados contra la masa sólida y 2 LIBRES (hacia las celdas de paso). Con esto activo cada sección se gira para mirar a uno de sus dos lados libres — cuál de los dos lo decide la semilla — y así queda de espaldas a una pared en vez de con la rotación fija de siempre, que a veces la dejaba mirando al muro.")]
    public bool orientarSeccionesALadoLibre = true;
    [Tooltip("Yaw base (grados) por si tus prefabs de sección están modelados mirando a otro eje que +Z. Ajustar en pasos de 90 (mismo criterio que yawBaseVerticeConvexo).")]
    public float yawBaseSeccion = 0f;

    private Dictionary<Vector3Int, NotaData> misionesPorCelda = new Dictionary<Vector3Int, NotaData>();

    // --- Misiones encadenadas (cadenas de notas) ---
    // celda destino → nota que la originó (al generar esa celda se spawnea la recompensa)
    private Dictionary<Vector3Int, NotaData> misionesEncadenadasPorCelda = new Dictionary<Vector3Int, NotaData>();
    // nota → su celda destino (para que la UI muestre las coordenadas)
    private Dictionary<NotaData, Vector3Int> destinoPorNota = new Dictionary<NotaData, Vector3Int>();

    [Header("Red de Viviendas (conexión narrativa entre viviendas)")]
    [Tooltip("NPC opcional (con componente NPCHabitante) que se instancia en el centro de la vivienda. Tiene prioridad sobre el cartel si están los dos asignados. Dejalo vacío si el NPC ya viene dentro del prefab de la sala.")]
    public GameObject prefabNPCHabitante;
    [Tooltip("Cartel opcional (con un TMP_Text hijo) que se instancia en la celda central de la vivienda mostrando hacia qué dirección 'creen' que está la otra vivienda que buscan. Se usa solo si NO hay NPC asignado.")]
    public GameObject prefabCartelBusquedaVivienda;
    [Tooltip("Radio de búsqueda en macro chunks (horizontal) donde cada vivienda puede 'creer' que está la vecina que le toca buscar.")]
    public int radioBusquedaViviendaMacros = 15;
    [Tooltip("Lo mismo pero en VERTICAL. Va aparte del horizontal porque el volumen crece al cubo: con un radio grande en los tres ejes la búsqueda se vuelve carísima y además te manda a viviendas absurdamente lejos.\n\n⚠️ NO lo bajes de radioIslaChunks.y * 2 + 1. Si es menor que la altura de una isla, las viviendas de tu propia isla quedan fuera de alcance y nadie tiene a quién buscar — separacionMinimaSalasChunks las empuja hacia arriba justamente porque a lo ancho no entran.")]
    public int radioYBusquedaViviendaMacros = 5;
    [Tooltip("Loguea un aviso cuando una vivienda NO encuentra pareja, con cuántas salas tipo 1 vio en el rango y cuántas descartó por ser cuartel. Es lo que distingue 'no hay salas cerca' de 'hay salas pero todas son cuartel'.")]
    public bool logBusquedaVivienda = true;
    [Tooltip("Altura sobre el PISO de la celda central a la que se coloca el NPC/cartel instanciado. 0 = parado en el piso (lo normal para un NPC). Subilo si usás un cartel colgado.")]
    public float alturaPistaVivienda = 0f;
    [Tooltip("Se cargan automáticamente todos los HabitanteData en Resources/NPCData/ en Awake. Cada vivienda recibe uno determinista por semilla, así su NPC tiene nombre y frases propias. Vacío = todos los NPC usan el texto genérico.")]
    public HabitanteData[] habitantesPosibles;

    // --- Red de viviendas (id natural = coordenada de macro chunk) ---
    // macro chunk de la vivienda → sus datos de instancia (petición/conexión).
    private Dictionary<Vector3Int, DatosVivienda> registroViviendas = new Dictionary<Vector3Int, DatosVivienda>();
    // macro chunk BUSCADO → LISTA de macro chunks que lo están buscando. Es lista (no un solo
    // valor) porque dos viviendas distintas pueden apuntar al mismo objetivo por casualidad —
    // con un solo valor, la segunda en registrarse pisaba el rastro de la primera y esa conexión
    // se perdía para siempre al hablarle al NPC del objetivo.
    private Dictionary<Vector3Int, List<Vector3Int>> viviendaBuscadaPor = new Dictionary<Vector3Int, List<Vector3Int>>();

    [Header("Luces de Techo")]
    [Tooltip("Activa la generación de luces en el techo de los pasillos")]
    public bool generarLucesTecho = true;
    [Tooltip("Array de prefabs de luces para el techo. Puedes tener: 1 luz, 2 luces, 3 luces, versiones parpadeantes, apagadas, etc.")]
    public GameObject[] prefabsLucesTecho;
    [Tooltip("Probabilidad de generar luces en el techo de un pasillo (0 a 1)")]
    [Range(0f, 1f)]
    public float probabilidadLuzTecho = 0.5f;
    [Tooltip("OBSOLETO — lo reemplazó el sistema de CARRILES DE TECHO (ver separacionCarrilesTecho). Se ignora: la separación lateral de las luces ahora sale del carril que les queda libre.")]
    public float luzTechoOffset = 3.0f;
    [Tooltip("Margen mínimo entre cada luz y la pared lateral (unidades). La separación se recorta para respetarlo, evitando que modelos grandes atraviesen la pared.")]
    public float luzTechoMargenPared = 2.0f;

    [Header("Carriles de Techo (reparto tubería / luces)")]
    [Tooltip("El techo del pasillo se divide en 3 CARRILES a lo ancho (izquierda / centro / derecha) y cada sistema reclama uno, para que nunca se superpongan.\n\nEste valor es la distancia del centro del pasillo a los carriles laterales. El carril del medio va en 0. Con 5.64 los carriles quedan donde ya estaban tus luces.\n\n⚠️ Se recorta igual por luzTechoMargenPared para no atravesar las paredes.")]
    public float separacionCarrilesTecho = 5.64f;
    [Tooltip("Si está tildado, cada corrida de tubería elige su carril por semilla (a veces izquierda, a veces centro, a veces derecha). Destildado = SIEMPRE por el carril del medio, que es el comportamiento actual.\n\nEl carril se decide con un hash sobre la coordenada PERPENDICULAR al recorrido, así toda la corrida usa el mismo — si se decidiera por celda, la tubería quedaría en zigzag.")]
    public bool tuberiaUsaCarrilAleatorio = false;


    [Header("Pasarelas Industriales")]
    [Tooltip("Prefabs de pasarela recta CON escalera (vertical/caracol). Van SOLO en los EXTREMOS de una corrida, como terminación alternativa a la tapa de fin — la escalera es el acceso a la pasarela. Nunca caen al medio del tramo (cortaban la línea de baranda y la hilera se veía picada).")]
    public GameObject[] prefabsPasarelaConEscalera;
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que un EXTREMO de corrida termine en escalera en vez de en tapa de fin. Se evalúa por extremo (una corrida puede tener escalera de un lado y tapa del otro, escalera en ambos, o tapa en ambos).")]
    public float probabilidadEscaleraExtremo = 0.35f;
    [Tooltip("Prefab de UN TRAMO de escalera, para bajar desde la pasarela con escalera hasta el piso del pasillo. Se APILA 1, 2 o 3 veces según a qué altura quedó la corrida.\n\nPor qué un solo prefab alcanza: las alturas posibles de pasarela son cellSize*0.25, *0.50 y *0.75 — o sea 1:2:3. Un tramo de un cuarto de celda las cubre las tres exacto, sin estirar (que separaría los peldaños) y sin modelar tres largos distintos.\n\nMODELADO: pivote en la BASE del tramo y centrado en X/Z (mismo criterio que los objetos de pozo). Se instancia con la misma rotación que la plataforma, así que queda encarado igual que la pasarela. Vacío = no se genera ninguna escalera de bajada (comportamiento viejo).")]
    public GameObject prefabTramoEscalera;
    [Tooltip("Alto real (en unidades) de tu tramo de escalera. Dejalo en 0 y lo calcula solo como cellSize * 0.25 — que es la medida que hace que la relación 1:2:3 calce perfecta. Solo tocalo si modelaste el tramo con otra altura y te quedan huecos o solapes entre tramos.")]
    public float alturaTramoEscalera = 0f;
    [Tooltip("Prefab de pasarela recta")]
    public GameObject prefabPasarelaRecta;

    [Space(4)]
    [Tooltip("El mismo prefab de extremo pero ESPEJADO A LO LARGO (la tapa en la punta contraria).\n\nPOR QUÉ HACE FALTA: hay DOS cosas que invierten la pieza — contra qué pared va (la rotación difiere 180°) y qué punta de la corrida es. Cuando se dan las dos a la vez se cancelan y sirve el prefab normal; cuando se da UNA SOLA, hace falta el espejo. Por eso sin esto acertaba en 2 de las 4 combinaciones.\n\nNo se puede resolver girando 180° en Y: eso también manda las barandas contra el muro.\n\nVacío = se usa siempre el prefab normal, y la tapa queda en la punta equivocada en la mitad de los extremos.")]
    public GameObject prefabPasarelaFinInvertida;

    [Tooltip("Lo mismo que prefabPasarelaFinInvertida pero para las pasarelas CON ESCALERA: el mismo modelo espejado A LO LARGO (escalera en la otra punta, barandas donde están).\n\nSe elige con el MISMO ÍNDICE que el array normal, así que conviene mismo orden y misma cantidad de elementos — así la variante espejada es el espejo de la que hubiera tocado, y no una escalera distinta.\n\nVacío = usa el array normal.")]
    public GameObject[] prefabsPasarelaConEscaleraInvertida;

    [Tooltip("Yaw extra (grados) SOLO para las pasarelas con escalera. Corrige de qué lado quedan las BARANDAS si el prefab está modelado al revés que la recta.\n\nEn 180 en este proyecto: con 0 las barandas salen para el lado equivocado.\n\n⚠️ Girar 180 también cambia qué punta de la pieza mira a la unión — eso NO se arregla acá, se arregla con `invertirEspejoPasarela`. Son dos problemas distintos: este es el lado de las barandas, el otro es la punta.")]
    public float yawExtraPasarelaEscalera = 180f;

    [Tooltip("⇄ Da vuelta la elección entre el prefab NORMAL y el INVERTIDO (tanto para la tapa de fin como para la escalera).\n\nEl código sabe CUÁNDO hace falta el espejo, pero no cuál de los dos prefabs asignados es el espejado — eso depende de cómo los modelaste, y encima el yaw de arriba también invierte la punta. Si las piezas te quedan DESPEGADAS de la corrida, tildá o destildá esto: una de las dos posiciones es la correcta.\n\nSíntoma para distinguir: si fallan TODAS es este toggle; si falla la mitad, el problema es otro.")]
    public bool invertirEspejoPasarela = false;

    [Tooltip("Prefab de extremo / cierre de pasarela (1 conexión)")]
    public GameObject prefabPasarelaFin;
    [Tooltip("Rotación extra en Y (grados) SOLO para las piezas de fin. Existe porque el prefab de fin puede estar modelado con el largo sobre OTRO eje que la recta — misma rotación, pieza atravesada 90° contra la pared. Ajustar en pasos de 90: probá 90, y si la tapa queda del lado equivocado, -90.")]
    public float yawExtraPasarelaFin = 0f;
    [Tooltip("Prefab de pasarela en T. Vacío = las intersecciones en T no llevan pieza de T: las corridas llegan al cruce y terminan con sus tapas de fin, sin basura flotante. (Antes caía en silencio al prefab de recta y CADA cruce plantaba una recta suelta atravesada en el medio.)")]
    public GameObject prefabPasarelaT;
    [Tooltip("Prefab de ESQUINA en L: la celda esquina COMPLETA en una sola pieza — los dos brazos (que llegan hasta los bordes de celda, donde empalman con las rectas vecinas) más el codo con la baranda doblando. NO es solo el cuadrado del codo: con rectas fijas de 20m los brazos no se pueden rellenar por separado (se solapan en el codo). Modelado asumido: paredes al SUR y OESTE, abiertos hacia +Z y +X. Si lo modelaste para otra esquina, corregí con el yaw base.")]
    public GameObject prefabPasarelaL;
    private bool avisoPasarelaLSinConector;
    [Tooltip("Distancia desde la pared hacia el interior del pasillo para colocar pasarelas (unidades)")]
    public float pasarelaOffset = 1.0f;
    [Tooltip("Probabilidad de generar pasarelas en un pasillo (0 a 1)")]
    [Range(0f, 1f)]
    public float probabilidadPasarela = 0.6f;
    [Tooltip("Offset de rotación adicional si necesitas ajustar el modelo globalmente. Normalmente 0.")]
    [Range(-180f, 180f)]
    public float pasarelaRotationOffset = 0f;

    [Header("Objetos de Borde de Pasillo (props contra las paredes)")]
    [Tooltip("Prefabs que se riegan en la franja pegada a las paredes de los pasillos (cajas, escombros, tachos, fierros...). Pivote en la BASE del modelo: se instancian a nivel de piso. Vacío = sistema apagado.")]
    public GameObject[] prefabsObjetoBorde;
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que UNA CARA con pared de una celda de pasillo reciba objetos (se evalúa por cara, no por celda: una celda puede tener objetos contra una pared y la de enfrente limpia).")]
    public float probabilidadObjetoBorde = 0.35f;
    [Tooltip("Cuántos objetos caen en una franja que salió sorteada (min/max, aleatorio determinista).")]
    public Vector2Int objetosPorCara = new Vector2Int(1, 3);
    [Tooltip("Ancho de la franja junto a la pared (m). Los objetos caen entre la pared y esta distancia hacia el centro del pasillo. La franja NUNCA existe en caras sin pared — pasillo abierto, balcón o void.")]
    public float anchoFranjaBorde = 2f;
    [Tooltip("Margen desde las esquinas de la celda a lo largo del pasillo (m), para que los objetos no queden clavados en la juntura entre celdas.")]
    public float margenFranjaBorde = 1f;
    [Tooltip("ON: los objetos miran hacia el centro del pasillo (dándole la espalda a la pared). OFF: yaw aleatorio, más caótico/escombro.")]
    public bool objetoBordeMiraAlPasillo = false;

    [Header("Cierres de Pasillos (puertas en cambios de sección)")]
    [Tooltip("Array de prefabs que se usarán para cerrar los pasillos. Puedes añadir varios.")]
    public GameObject[] prefabsCierrePasillo;
    [Tooltip("Desplazamiento desde el centro de la celda hacia el borde al colocar el cierre (unidades)")]
    public float cierreOffset = 0f;
    [Tooltip("Probabilidad de que un cierre aparezca en un final de pasillo (0 a 1)")]
    [Range(0f, 1f)]
    public float probabilidadCierre = 0.7f;

    [Header("Balcones (celdas de pasillo sin paredes laterales)")]
    [Tooltip("Probabilidad de que una celda de pasillo se convierta en balcón (0 = apagado, 1 = siempre)")]
    [Range(0f, 1f)]
    public float probabilidadBalcon = 0.05f;
    [Tooltip("Prefab opcional que se instancia en el borde de cada celda de balcón (ej. plataforma/baranda que sobresale hacia la pared exterior).")]
    public GameObject prefabBalcon;
    [Tooltip("Ajuste fino de cuánto sobresale el prefab del balcón desde el borde de la celda hacia la pared exterior (unidades). 0 = justo en el borde.")]
    public float balconOffset = 0f;

    [Header("Tuberías Modulares (kit por conectores, v1: solo rectas y curvas 90°)")]
    [Tooltip("Probabilidad de que un pasillo elegible (tramo recto o esquina, sin intersecciones/callejones) genere tubería (0 = apagado, 1 = siempre).")]
    [Range(0f, 1f)]
    public float probabilidadTuberia = 0.5f;
    [Tooltip("Piezas rectas de 1m. Se combinan con las de 2m y 5m para cubrir cualquier largo (ej. 7 = 5+2).")]
    public GameObject[] prefabsTuberiaRecta1m;
    [Tooltip("Piezas rectas de 2m.")]
    public GameObject[] prefabsTuberiaRecta2m;
    [Tooltip("Piezas rectas de 5m.")]
    public GameObject[] prefabsTuberiaRecta5m;
    [Tooltip("Pieza de curva 90°, para celdas esquina (2 vecinos abiertos en ángulo).")]
    public GameObject[] prefabsTuberiaCurva90;
    [Tooltip("Tapón / extremo roto. Se instancia SOLO en los bordes donde el vecino no continúa la tubería (empieza o termina el recorrido), nunca en el medio de un tramo.")]
    public GameObject[] prefabsTuberiaTapon;
    [Tooltip("Altura local (Y dentro de la celda) a la que corre la tubería. Por defecto cerca del techo.")]
    public float alturaTuberia = 17f;
    [Tooltip("OBSOLETO — lo reemplazó el sistema de CARRILES DE TECHO (ver separacionCarrilesTecho). Se ignora: la posición lateral de la tubería ahora sale de qué carril le tocó, no de este offset.")]
    public float tuberiaOffsetLateral = 0f;
    [Tooltip("Cuánto largo 'consume' la pieza de curva en cada uno de sus 2 tramos (unidades). Ajustar según el tamaño real del prefab modelado.")]
    public float longitudCurvaTuberia = 2f;
    [Tooltip("Rotación base de la curva si el prefab está modelado mirando hacia otra esquina que la NE por defecto. Ajustar en pasos de 90 (mismo patrón que yawBaseVerticeConvexo).")]
    public float yawBaseTuberiaCurva = 0f;

    [Header("Generación de Biomas")]
    public float escalaDensidad = 0.15f;
    // DESACTIVADO: probabilidadPuenteAbismo controlaba el puente flotante que el viejo Abismo
    // (tipo 7) generaba en ly==0. Con el rename de Abismo → Sala Grande (que ahora tiene piso sólido
    // continuo) este sistema ya no se usa. Lo dejo comentado por si se quiere reactivar para algo
    // similar en el futuro (ej. puentes opcionales en Abismo Verdadero / tipo 9).
    // [Tooltip("Probabilidad de que el abismo genere un puente (0 a 1)")]
    // [Range(0f, 1f)]
    // public float probabilidadPuenteAbismo = 0.5f;

    [Header("Jugador y Spawn")]
    [Tooltip("ON = el jugador arranca siempre en una vivienda (sala tipo 1, en una celda de paso del 3x3 — nunca esquinas con secciones ni la central con estructura). OFF = spawn aleatorio: cualquier vivienda O pasillo. En AMBOS modos los cuarteles de centinelas quedan excluidos — nunca arrancás en la sala de los vigías.")]
    public bool spawnEnVivienda = true;
    [Tooltip("SOLO DEBUG. ON = arrancás DENTRO del cuartel de centinelas más cercano al origen, ignorando spawnEnVivienda Y el guardado (cargarPartidaAlIniciar se saltea, si no la partida guardada te devolvía a donde estabas). Es exactamente la misma celda que usa RondaCentinelas para spawnear la ronda, así que los centinelas aparecen encima tuyo. Acordate de apagarlo antes de jugar en serio.")]
    public bool spawnEnCuartelDebug = false;
    [Tooltip("SOLO DEBUG. ON = arrancás en la boca de pasillo pegada al POZO VERTICAL (tipo 6) más cercano al origen. Igual que el de cuartel: ignora spawnEnVivienda y el guardado. Si también está prendido el de cuartel, manda el cuartel.")]
    public bool spawnEnPozoVerticalDebug = false;
    [Tooltip("SOLO DEBUG. ON = arrancás en el centro de la SALA GRANDE / atrio (tipo 7) más cercana al origen. Igual que el de cuartel: ignora spawnEnVivienda y el guardado. Prioridad: cuartel > pozo vertical > sala grande.")]
    public bool spawnEnSalaGrandeDebug = false;
    [Tooltip("Solo con los Spawn ... Debug (cuartel, pozo vertical, sala grande). Techo (en macro chunks) de la búsqueda desde el origen del WorldGenerator. 1 macro chunk = 100m: si no encuentra ninguno, subilo.")]
    public int radioBusquedaCuartelDebugMacros = 15;

    private bool AlgunSpawnDebug => spawnEnCuartelDebug || spawnEnPozoVerticalDebug || spawnEnSalaGrandeDebug;
    [Tooltip("Calculada por DefinirPuntoDeSpawn a partir de lugarDeSpawn. Aleatoria por semilla (respeta usarSemillaAleatoria).")]
    public Vector3 posicionSpawn = new Vector3(-58.6f, 21.4f, 170.3f);
    public Transform jugador;
    [Tooltip("Transform opcional para definir el punto de inicio físico del jugador.")]
    public Transform puntoSpawnJugador;
    private Vector3Int celdaSpawn;

    [Header("Generación Procedural Infinita")]
    public int radioVision = 3;   // Distancia de bloques que se renderizan
    public int macroSize = 5;     // Tamaño del chunk estructural (5x5x5)

    private Dictionary<Vector3Int, List<GameObject>> mundoChunks = new Dictionary<Vector3Int, List<GameObject>>();

    /// <summary>
    /// Objetos generados que quedaron registrados en esa celda (paredes, loot, etc.), o null si
    /// la celda no se generó todavía. Sirve para leer "qué hay ahí ahora" sin mantener una
    /// estructura de datos aparte — ej. pantallas de inventario que listan lo que quede.
    /// </summary>
    public IReadOnlyList<GameObject> ObtenerObjetosDeCelda(Vector3Int celda)
    {
        return mundoChunks.TryGetValue(celda, out List<GameObject> objs) ? objs : null;
    }

    // Un contenedor por celda, en la RAÍZ de la escena (no bajo el WorldGenerator).
    //
    // ⚠️ Antes todas las piezas eran hijas directas del WorldGenerator: tras un rato jugando eran
    // miles de hijos bajo un solo transform (las celdas con estado nunca se descargan). En 6000.6
    // eso crasheaba el editor al seleccionar el WorldGenerator en la Hierarchy y al parar Play
    // (destruir esa jerarquía de golpe). Además, una raíz gigante es lenta: cualquier cambio de
    // transform ensucia toda la jerarquía y Unity no la puede repartir entre hilos.
    private readonly Dictionary<Vector3Int, Transform> contenedoresCelda = new Dictionary<Vector3Int, Transform>();

    /// <summary>
    /// Registra la pieza en la celda y la cuelga del contenedor de esa celda. Solo mueve piezas
    /// cuyo padre es el WorldGenerator; si alguna está colgada de otra cosa, la deja donde está.
    /// </summary>
    private void RegistrarEnCelda(Vector3Int celda, GameObject go)
    {
        mundoChunks[celda].Add(go);
        if (go != null && go.transform.parent == transform)
            go.transform.SetParent(ContenedorDeCelda(celda), false);
    }

    /// <summary>
    /// El contenedor copia posición/rotación/escala del WorldGenerator, así el espacio local es el
    /// mismo: SetParent(..., false) deja la pieza exactamente donde estaba y cualquier cuenta con
    /// localPosition sigue valiendo.
    /// </summary>
    private Transform ContenedorDeCelda(Vector3Int celda)
    {
        if (contenedoresCelda.TryGetValue(celda, out Transform cont) && cont != null) return cont;

        GameObject go = new GameObject($"Celda {celda.x},{celda.y},{celda.z}");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        cont = go.transform;
        cont.SetPositionAndRotation(transform.position, transform.rotation);
        cont.localScale = transform.lossyScale;
        contenedoresCelda[celda] = cont;
        return cont;
    }

    private void DestruirContenedorCelda(Vector3Int celda)
    {
        if (contenedoresCelda.TryGetValue(celda, out Transform cont))
        {
            if (cont != null) Destroy(cont.gameObject);
            contenedoresCelda.Remove(celda);
        }
    }

    // Tracker de paredes End instanciadas para poder reaplicar offsets en runtime sin regenerar.
    private List<Vector3Int> celdasActivas = new List<Vector3Int>();
    private Vector3Int celdaActualJugador = new Vector3Int(-9999, -9999, -9999);

    [Header("Semilla")]
    public bool usarSemillaAleatoria = false;
    public int semillaDelMundo = 7777777;

    private float offsetX, offsetY, offsetZ;

    // --- Cachés de generación ---
    // Estas funciones son PURAS una vez fijada la semilla/offsets (en Start): mismo input → mismo
    // output toda la partida. Se llaman cientos de veces por celda (decoradores, predictores por
    // hash, chequeos de vecinos) y las originales pagan Perlin + Random.InitState en cada llamada.
    // Los cachés eliminan ese costo sin cambiar ni un bit del resultado. Se limpian en Start().

    // Celdas que YA se evaluaron y resultaron NO descargables porque contienen objetos con estado
    // propio (cofres, items, notas, NPCs, puertas...). Se recuerda el veredicto para no repetir el
    // escaneo de componentes en cada pasada de descarga.
    private readonly HashSet<Vector3Int> celdasNoDescargables = new HashSet<Vector3Int>();

    private readonly Dictionary<Vector3Int, int> cacheTipoChunk = new Dictionary<Vector3Int, int>();
    private readonly Dictionary<Vector3Int, bool> cacheCeldaVacia = new Dictionary<Vector3Int, bool>();
    private readonly Dictionary<Vector3Int, Vector3Int> cacheDirBalcon = new Dictionary<Vector3Int, Vector3Int>();
    private readonly Dictionary<Vector3Int, bool> cacheCierre = new Dictionary<Vector3Int, bool>();
    // Conectividad por isla (región → set de chunks estructura conectados al esqueleto)
    private readonly Dictionary<Vector3Int, HashSet<Vector3Int>> cacheIslaConectada = new Dictionary<Vector3Int, HashSet<Vector3Int>>();
    // Región → ¿su isla es una Sala Colosal? Cacheado aparte porque CentroIsla lo consulta, y
    // CentroIsla está en el camino caliente de EnIsla/tipado: sin caché, cada llamada pagaría un
    // Random.InitState extra en TODO el mundo, haya salas colosales o no.
    private readonly Dictionary<Vector3Int, bool> cacheIslaColosal = new Dictionary<Vector3Int, bool>();
    // Región → ¿su isla se generó sin viviendas? (hash puro, mismo motivo de caché que el de arriba)
    private readonly Dictionary<Vector3Int, bool> cacheIslaSinViviendas = new Dictionary<Vector3Int, bool>();
    // Región → chunk elegido como cuartel de esa isla. El valor null significa "ya se buscó y esta
    // isla no tiene ningún chunk de núcleo donde meterlo" (islas colosales, o islas tan porosas que
    // todo su volumen quedó tocando el vacío) — se cachea igual para no repetir el escaneo.
    private readonly Dictionary<Vector3Int, Vector3Int?> cacheCuartelIsla = new Dictionary<Vector3Int, Vector3Int?>();

    // Set de chunks que serán Sala Chica en cada isla (el cuartel incluido). Se decide UNA vez por
    // isla, no chunk por chunk, que es lo que permite ponerle un tope real a la cantidad.
    private readonly Dictionary<Vector3Int, HashSet<Vector3Int>> cacheSalasIsla = new Dictionary<Vector3Int, HashSet<Vector3Int>>();

    [Header("Islas + Puentes (LA generación del mundo)")]
    [Tooltip("Período de la grilla de islas en macro chunks: 10 = una isla cada ~1000m (con jitter, quedan a 800-1200m entre sí).")]
    public int periodoIslas = 10;
    [Tooltip("Radio de cada isla en macro chunks (min/max, aleatorio por isla). Radio 2 = estructura de ~500m de lado.")]
    public Vector2Int radioIslaChunks = new Vector2Int(1, 2);
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que exista puente entre islas vecinas (por eje X y Z). Lo que no tiene puente directo obliga a rodear por otra isla... o a rapelear. 'A veces tenés suerte.'")]
    public float probabilidadPuente = 0.75f;
    [Tooltip("Puertas para las bocas de pasillo que dan al VACÍO (tipo 9): salir del casco de la isla pasa a ser deliberado (abrís la compuerta y ves la nada). Vacío = bocas abiertas como antes. Las bocas a Sala Grande (7, atrios interiores) siguen abiertas siempre.")]
    public GameObject[] prefabsPuertaBocaVacio;
    [Range(0f, 0.6f)]
    [Tooltip("POROSIDAD de las islas: chunks con densidad bajo este umbral son VACÍO también dentro de la isla. La isla deja de ser un bloque encapsulado y pasa a ser un enjambre de estructuras (tubos, salas) expuestas al void, cada una con su propia piel — sin paredes contenedoras. 0 = isla maciza clásica.")]
    public float umbralVacioIsla = 0.45f;

    [Header("Salas Colosales (una isla entera = UNA sola sala)")]
    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que una isla, en vez de ser un enjambre de pasillos/salas, sea UNA sala colosal: todo el volumen de la isla vaciado en un único espacio. Con radioIslaChunks=2 y cellSize=20 son ~460m de lado y de alto. La isla del spawn NUNCA es colosal.")]
    public float probabilidadIslaColosal = 0.2f;
    [Tooltip("Grosor (en celdas) de la cáscara sólida que envuelve la sala colosal. 1 = muros/piso/techo de un cellSize (20m). Tope 2: el tallado de conectores perfora 2 celdas desde cada cara, así que una cáscara más gruesa dejaría la sala sellada sin entrada.")]
    [Range(1, 2)]
    public int grosorCascaraColosal = 1;
    [Tooltip("Generar los pilares gigantes (los mismos de Sala Grande) dentro de la sala colosal. Forman columnas continuas cada 100m — dan escala, pero son muchos objetos. Apagalo si te come performance.")]
    public bool pilaresEnSalaColosal = true;
    [Tooltip("En el casco (la cara EXTERNA de la cáscara: paredes, panza y coronación) poner siempre la piel exterior, ignorando probabilidadParedExterior/PisoExterior/TechoExterior. Con los valores por defecto (0.18 y 0.5) el casco saldría parchado, con agujeros por donde se ve la cara de atrás. Apagalo si querés un casco deliberadamente incompleto/en ruinas.")]
    public bool cascoColosalSiempreConPielExterior = true;

    [Header("Viviendas y Cuarteles por Isla")]
    [Tooltip("Cuántas SALAS CHICAS tiene cada isla, mínimo y máximo — y el cuartel garantizado CUENTA como una. Con (2,4): siempre el cuartel + 1 a 3 viviendas. Antes no había tope: cada chunk del núcleo tiraba los dados por su cuenta, así que una isla grande y maciza podía llenarse de decenas de viviendas (y cada una cuesta prefab + NPC + items en RAM). Los chunks del núcleo que NO salen elegidos pasan a intersecciones/pozos (5,6,8), así que la isla no queda hueca.")]
    public Vector2Int salasPorIsla = new Vector2Int(2, 4);

    [Tooltip("Separación mínima entre salas, medida en MACRO CHUNKS (distancia Chebyshev, así que cuenta también la VERTICAL). Cuenta el cuartel: una vivienda no puede quedar pegada a él.\n\nOJO CON LA UNIDAD: dos macro chunks vecinos ya están macroSize celdas aparte (con macroSize=5, eso es 5 celdas). Por eso 1 = permite salas en chunks pegados, que es justo lo que se veía mal. 2 = deja al menos un chunk de aire en medio (~10 celdas).\n\nSi la isla es chica y no entran tan separadas, se relaja de a uno hasta llegar al mínimo de salasPorIsla — preferible un poco más juntas que quedarse sin viviendas.")]
    [Min(1)]
    public int separacionMinimaSalasChunks = 2;

    [Range(0f, 1f)]
    [Tooltip("Probabilidad de que una isla NO tenga viviendas: sus chunks de densidad alta que hubieran salido Sala Chica (1) se remapean a intersecciones/pozos (5,6,8). Queda una isla puramente industrial, de tránsito, sin nadie viviendo ahí. El cuartel garantizado NO se ve afectado — esa sala aparece igual.")]
    public float probabilidadIslaSinViviendas = 0.35f;
    [Tooltip("Garantizar UN cuartel de centinelas por isla, cueste lo que cueste: se elige un chunk (núcleo idealmente; si no hay, borde; si no hay, cualquiera que no sea pozo/lane/spawn) y se fuerza a Sala Chica con la RoomData de cuartel, desplazando lo que hubiera tocado ahí — viviendas incluidas. Es el ÚNICO cuartel de la isla: las RoomData de cuartel ya no entran en la ruleta de viviendas, así que ninguna vivienda común puede hacer de cuartel ni spawnear centinelas. Las islas colosales quedan fuera: son una sola sala, no hay dónde meterlo.")]
    public bool cuartelGarantizadoPorIsla = true;

    [Tooltip("Si está tildado, la ronda de centinelas puede elegir destinos DENTRO de las viviendas (entran a las casas). Destildado (por defecto) patrullan solo pasillos/cruces/pozos y su propio cuartel. Ojo: esto solo cubre la RONDA — si un centinela te persigue o va a investigar un ruido, entra igual adonde sea.")]
    public bool centinelasEntranAViviendas = false;

    [Header("Densidad (tipado de chunks)")]
    [Tooltip("Densidad sobre este umbral = complejos industriales densos (tipos 1,5,6,8). Subirlo = menos zonas densas.")]
    public float umbralDensidadAlta = 0.55f;
    [Tooltip("Entre este umbral y el alto = pasillos (2,3,4). SUBIRLO = interiores menos claustrofóbicos: más atrios (7) y huecos dentro de las islas.")]
    public float umbralDensidadMedia = 0.30f;
    [Tooltip("Entre este umbral y el medio = Sala Grande/atrio (7). Debajo = Abismo (9).")]
    public float umbralDensidadBaja = 0.20f;

    void Awake()
    {
        Instance = this;
        Application.targetFrameRate = 60;

        // Auto-cargar RoomData desde Resources/RoomData/ si el array está vacío en el inspector
        if (salasGrandesPosibles == null || salasGrandesPosibles.Length == 0)
        {
            salasGrandesPosibles = Resources.LoadAll<RoomData>("RoomData");
        }

        // Volcado de lo que quedó cargado. Si TODAS son cuartel, la ruleta de ObtenerRoomDataParaMacro
        // entra en su modo de emergencia ("soloHayCuarteles") y devuelve cuartel para cada sala del
        // mundo — con lo cual ninguna vivienda encuentra pareja y nadie da misiones.
        if (logBusquedaVivienda && salasGrandesPosibles != null)
        {
            var sb = new System.Text.StringBuilder();
            int normales = 0;
            foreach (RoomData s in salasGrandesPosibles)
            {
                if (s == null) { sb.Append("[NULL] "); continue; }
                if (!s.esCuartelCentinelas) normales++;
                sb.Append($"'{s.nombreSala}'{(s.esCuartelCentinelas ? "(CUARTEL)" : $"(peso {s.pesoAparicion})")} ");
            }
            Debug.Log($"[RedViviendas] RoomData cargadas: {salasGrandesPosibles.Length} · no-cuartel: {normales} → {sb}");
        }

        // Ídem con los habitantes de vivienda (Resources/NPCData/).
        // Si preferís control manual, arrastrá los assets al array en el inspector: con el array
        // ya lleno este bloque ni corre. El warning existe porque LoadAll busca por STRING —
        // si alguien renombra/mueve la carpeta no hay error de compilación, solo un array vacío
        // y los NPC quedarían mudos sin explicación.
        // Se chequea que haya al menos uno NO NULO, no solo que el array tenga largo: un array
        // de tamaño 1 con el slot vacío (lo que deja Unity al tocar el "+" del inspector) haría
        // creer que está asignado a mano y saltearía la auto-carga en silencio.
        if (!TieneAlgunoNoNulo(habitantesPosibles))
        {
            habitantesPosibles = Resources.LoadAll<HabitanteData>("NPCData");
            if (!TieneAlgunoNoNulo(habitantesPosibles))
                Debug.LogWarning("[WorldGenerator] No se encontró ningún HabitanteData en Resources/NPCData/ — los NPC de vivienda van a usar el texto genérico. Revisá que la carpeta exista con ese nombre exacto, o asigná los assets a mano en 'Habitantes Posibles'.");
        }
    }

    private static bool TieneAlgunoNoNulo<T>(T[] array) where T : Object
    {
        if (array == null) return false;
        foreach (T item in array)
            if (item != null) return true;
        return false;
    }

    [Header("Sistema de Guardado")]
    [Tooltip("Si hay una partida guardada, la carga al iniciar en vez de generar un spawn aleatorio.")]
    public bool cargarPartidaAlIniciar = true;
    private bool cargadoDesdeGuardado = false;
    private float rotYGuardada = 0f;

    void Start()
    {
        // El toggle de debug manda sobre el guardado: si no, cargar la partida te devolvía a donde
        // estabas y el spawn en cuartel nunca llegaba a calcularse.
        if (!AlgunSpawnDebug && cargarPartidaAlIniciar && SistemaGuardado.Cargar(out int seedGuardada, out Vector3 posGuardada, out rotYGuardada))
        {
            semillaDelMundo = seedGuardada;
            usarSemillaAleatoria = false;
            cargadoDesdeGuardado = true;
            posicionSpawn = posGuardada;
            celdaSpawn = MundoACelda(posGuardada);
            Debug.Log($"[WorldGenerator] Partida cargada exitosamente. Semilla: {semillaDelMundo} | Pos: {posicionSpawn}");
        }
        else if (usarSemillaAleatoria)
        {
            semillaDelMundo = Random.Range(0, 99999999);
        }

        Random.InitState(semillaDelMundo);

        offsetX = Random.Range(-10000f, 10000f);
        offsetY = Random.Range(-10000f, 10000f);
        offsetZ = Random.Range(-10000f, 10000f);

        // Los codex son estáticos: en el editor con "Enter Play Mode" sin domain reload sobreviven
        // a salir y volver a entrar. Sin esto arrancabas viendo los descubrimientos de la corrida
        // anterior — justo lo que confunde al testear con semillas distintas.
        ReiniciarCodex();

        // La semilla/offsets quedaron fijos: invalidar los cachés de generación
        celdasNoDescargables.Clear();
        cacheTipoChunk.Clear();
        cacheCeldaVacia.Clear();
        cacheDirBalcon.Clear();
        cacheCierre.Clear();
        cacheIslaConectada.Clear();
        cacheIslaColosal.Clear();
        cacheIslaSinViviendas.Clear();
        cacheCuartelIsla.Clear();
        cacheSalasIsla.Clear();
        misionesEncadenadasPorCelda.Clear();
        destinoPorNota.Clear();
        registroViviendas.Clear();
        viviendaBuscadaPor.Clear();

        // Precalcular misiones de las notas
        NotaData[] todasLasNotas = Resources.LoadAll<NotaData>("");
        misionesPorCelda.Clear();
        foreach (NotaData nota in todasLasNotas)
        {
            if (nota != null && nota.tieneMision && nota.prefabCofre != null)
            {
                int seed = nota.titulo.GetHashCode() + semillaDelMundo;
                if (ObtenerCeldaDestinoMision(seed, out Vector3Int celdaDestino))
                {
                    misionesPorCelda[celdaDestino] = nota;
                }
            }
        }

        if (cargadoDesdeGuardado)
        {
            if (jugador != null)
            {
                PosicionarJugadorEn(posicionSpawn, rotYGuardada);
                CongelarJugador(true);
            }
        }
        else
        {
            DefinirPuntoDeSpawn();

            if (jugador != null)
            {
                PosicionarJugadorEnSpawn();
                CongelarJugador(true); // Congelado hasta que el mundo inicial esté generado
            }
        }

        celdaActualJugador = celdaSpawn;
        StartCoroutine(WorkerBurbuja(celdaSpawn));
        // Worker permanente igual que la burbuja: no se re-lanza en los reinicios de semilla.
        StartCoroutine(WorkerDescarga());
    }

    private bool mundoListo = false;

    void Update()
    {
        if (jugador != null && mundoListo)
        {
            // MundoACelda resta transform.position: la grilla lógica es LOCAL al WorldGenerator,
            // igual que GenerarCelda (que instancia en origin + celda*cellSize). Calcularlo sin
            // restar el origen desalinea la burbuja respecto a la geometría cuando el WG no está
            // en (0,0,0) — el spawn arrancaba bien y saltaba de celda en el primer Update.
            Vector3Int celdaJugador = MundoACelda(jugador.position);

            // Sin gate de "generando": el worker permanente re-apunta solo. Acá únicamente
            // publicamos la celda objetivo — el worker la recoge en su próximo yield.
            if (celdaJugador != celdaActualJugador)
            {
                celdaActualJugador = celdaJugador;
                celdaObjetivo = celdaJugador;

                PlayerData pd = jugador.GetComponent<PlayerData>();
                if (pd != null)
                {
                    pd.ActualizarCoordenadaGrilla(celdaJugador);
                }

                IntentarDescubrirLugarActual(celdaJugador);
            }
        }
    }

    /// <summary>
    /// Descubrimiento de LUGAR (bitácora): pisar el macro chunk alcanza, no hace falta hablarle al
    /// NPC (eso es aparte, para la misión de conexión).
    ///
    /// ⚠️ Se llama desde DOS lados a propósito. Antes vivía solo dentro del "cambié de celda" del
    /// Update, y por eso la vivienda donde SPAWNEÁS no se descubría nunca: al arrancar,
    /// celdaActualJugador ya vale celdaSpawn, así que no hay cambio que disparar. Ahora también se
    /// llama una vez cuando el mundo queda listo.
    /// </summary>
    private void IntentarDescubrirLugarActual(Vector3Int celdaJugador)
    {
        Vector3Int macroJugador = new Vector3Int(
            Mathf.FloorToInt((float)celdaJugador.x / macroSize),
            Mathf.FloorToInt((float)celdaJugador.y / macroSize),
            Mathf.FloorToInt((float)celdaJugador.z / macroSize));

        if (registroViviendas.ContainsKey(macroJugador))
        {
            RegistroViviendasVisitadas.Visitar(macroJugador);
        }
    }

    private void CongelarJugador(bool congelar)
    {
        if (jugador == null) return;

        Rigidbody rb = jugador.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (congelar)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = congelar;
        }
    }

    private enum FiltroSpawn { Viviendas, Pasillos, ViviendasYPasillos, Cualquiera }

    /// <summary>
    /// Busca una celda de spawn entre los macro chunks de la región del origen, filtrando por
    /// tipo. REGLA DURA: nunca devuelve una celda de un cuartel de centinelas, sea cual sea el
    /// filtro. En viviendas se limita a las celdas de PASO del 3x3 (el medio de cada lado): las
    /// 4 esquinas tienen secciones y la central la estructura/escalera.
    /// </summary>
    private bool TryElegirCeldaSpawn(FiltroSpawn filtro, out Vector3Int celda)
    {
        celda = default;

        List<Vector3Int> candidatos = new List<Vector3Int>();
        for (int mx = 0; mx < periodoIslas; mx++)
            for (int my = 0; my < periodoIslas; my++)
                for (int mz = 0; mz < periodoIslas; mz++)
                {
                    int tipo = ObtenerTipoMacroChunk(mx, my, mz);
                    if (tipo == 9) continue;  // abismo puro: no hay nada donde pararse
                    if (tipo == 10) continue; // sala colosal: arrancar en medio de la nave, sin
                                              // contexto ni salida a la vista, es desorientador

                    bool esVivienda = tipo == 1;
                    bool esPasillo = tipo == 2 || tipo == 3 || tipo == 4;

                    bool aceptado = filtro switch
                    {
                        FiltroSpawn.Viviendas => esVivienda,
                        FiltroSpawn.Pasillos => esPasillo,
                        FiltroSpawn.ViviendasYPasillos => esVivienda || esPasillo,
                        _ => true,
                    };
                    if (!aceptado) continue;

                    // NUNCA un cuartel de centinelas, en ningún filtro ni fallback.
                    if (esVivienda)
                    {
                        RoomData rd = ObtenerRoomDataParaMacro(mx, my, mz);
                        if (rd != null && rd.esCuartelCentinelas) continue;
                    }

                    candidatos.Add(new Vector3Int(mx, my, mz));
                }

        // Sortea macro chunks (sin repetir) hasta encontrar uno con celdas transitables en ly=1
        while (candidatos.Count > 0)
        {
            int idx = Random.Range(0, candidatos.Count);
            Vector3Int macro = candidatos[idx];
            candidatos.RemoveAt(idx);
            bool esVivienda = ObtenerTipoMacroChunk(macro.x, macro.y, macro.z) == 1;

            List<Vector3Int> celdasVacias = new List<Vector3Int>();
            for (int lx = 0; lx < macroSize; lx++)
                for (int lz = 0; lz < macroSize; lz++)
                {
                    if (esVivienda)
                    {
                        bool esPaso = (lx == 2 && (lz == 1 || lz == 3)) || (lz == 2 && (lx == 1 || lx == 3));
                        if (!esPaso) continue;
                    }

                    Vector3Int c = new Vector3Int(macro.x * macroSize + lx, macro.y * macroSize + 1, macro.z * macroSize + lz);
                    if (IsCellEmpty(c.x, c.y, c.z)) celdasVacias.Add(c);
                }

            if (celdasVacias.Count > 0)
            {
                celda = celdasVacias[Random.Range(0, celdasVacias.Count)];
                return true;
            }
        }

        return false;
    }

    private void DefinirPuntoDeSpawn(int semillaExtra = 7777)
    {
        // Aleatorio por semilla (respeta usarSemillaAleatoria: si está activo, la semilla ya fue
        // randomizada en Start). Cascada de fallbacks, todos con la MISMA regla dura de nunca
        // caer en un cuartel de centinelas:
        //   toggle ON  -> vivienda; si no hay ninguna válida -> cualquier pasillo
        //   toggle OFF -> vivienda o pasillo, lo que salga
        //   último recurso (mundo degenerado) -> cualquier celda transitable no-cuartel
        Random.State prev = Random.state;
        Random.InitState(semillaDelMundo + semillaExtra);

        // DEBUG: cortocircuita toda la cascada y te deja en el cuartel. Misma celda que usa
        // RondaCentinelas.SpawnearDesdeCuartel, así que la ronda sale encima tuyo en vez de
        // tener que buscarla por la isla durante minutos.
        if (spawnEnCuartelDebug)
        {
            if (TryEncontrarSalaCentinela(transform.position, radioBusquedaCuartelDebugMacros, out Vector3Int celdaCuartelDebug))
            {
                celdaSpawn = celdaCuartelDebug;
                posicionSpawn = transform.position + new Vector3(
                    celdaSpawn.x * cellSize + (cellSize / 2f),
                    celdaSpawn.y * cellSize + 2f,
                    celdaSpawn.z * cellSize + (cellSize / 2f)
                );
                Debug.LogWarning($"[WorldGenerator] SPAWN EN CUARTEL (DEBUG) · celda {celdaSpawn} · pos {posicionSpawn}. Acordate de apagar 'Spawn En Cuartel Debug'.");
                Random.state = prev;
                return;
            }
            Debug.LogWarning($"[WorldGenerator] 'Spawn En Cuartel Debug' activo pero no hay ningún cuartel dentro de {radioBusquedaCuartelDebugMacros} macro chunks del origen — spawn normal. Subí el radio o cambiá de semilla.");
        }

        if (spawnEnPozoVerticalDebug)
        {
            if (TryEncontrarMacroDeTipo(6, transform.position, radioBusquedaCuartelDebugMacros, out Vector3Int macroPozo))
            {
                celdaSpawn = CeldaSpawnJuntoAPozo(macroPozo);
                FijarSpawnDebug("POZO VERTICAL", "Spawn En Pozo Vertical Debug");
                Random.state = prev;
                return;
            }
            Debug.LogWarning($"[WorldGenerator] 'Spawn En Pozo Vertical Debug' activo pero no hay ningún pozo vertical dentro de {radioBusquedaCuartelDebugMacros} macro chunks del origen — spawn normal. Subí el radio o cambiá de semilla.");
        }

        if (spawnEnSalaGrandeDebug)
        {
            if (TryEncontrarMacroDeTipo(7, transform.position, radioBusquedaCuartelDebugMacros, out Vector3Int macroSala))
            {
                // Piso sólido en ly=0 → se para en ly=1, en el centro (los pilares van en las esquinas).
                int m = macroSize / 2;
                celdaSpawn = new Vector3Int(macroSala.x * macroSize + m, macroSala.y * macroSize + 1, macroSala.z * macroSize + m);
                FijarSpawnDebug("SALA GRANDE", "Spawn En Sala Grande Debug");
                Random.state = prev;
                return;
            }
            Debug.LogWarning($"[WorldGenerator] 'Spawn En Sala Grande Debug' activo pero no hay ninguna sala grande dentro de {radioBusquedaCuartelDebugMacros} macro chunks del origen — spawn normal. Subí el radio o cambiá de semilla.");
        }

        bool encontrada;
        if (spawnEnVivienda)
        {
            encontrada = TryElegirCeldaSpawn(FiltroSpawn.Viviendas, out celdaSpawn);
            if (!encontrada)
            {
                Debug.LogWarning("[WorldGenerator] No hay ninguna vivienda no-cuartel en la región del origen — spawneando en un pasillo.");
                encontrada = TryElegirCeldaSpawn(FiltroSpawn.Pasillos, out celdaSpawn);
            }
        }
        else
        {
            // Spawn En Vivienda apagado = posicion totalmente random (cualquier sala/pasillo
            // valido, respetando la unica regla dura: nunca en un cuartel de centinelas).
            encontrada = TryElegirCeldaSpawn(FiltroSpawn.Cualquiera, out celdaSpawn);
        }

        if (!encontrada)
            encontrada = TryElegirCeldaSpawn(FiltroSpawn.Cualquiera, out celdaSpawn);

        if (!encontrada)
        {
            // Solo alcanzable si no hay NI UNA celda transitable no-cuartel en toda la región.
            Debug.LogError("[WorldGenerator] No se encontró ninguna celda de spawn válida. Revisá periodoIslas / los pesos de las RoomData: si TODAS las salas son cuartel, el jugador no tiene dónde arrancar.");
            celdaSpawn = new Vector3Int(2, 1, 2);
        }

        posicionSpawn = transform.position + new Vector3(
            celdaSpawn.x * cellSize + (cellSize / 2f),
            celdaSpawn.y * cellSize + 2f,
            celdaSpawn.z * cellSize + (cellSize / 2f)
        );

        // Log de verificación: si alguna vez volvés a aparecer donde no toca, esto dice exactamente
        // en qué macro chunk caíste y qué RoomData tiene asignada.
        {
            int smx = Mathf.FloorToInt((float)celdaSpawn.x / macroSize);
            int smy = Mathf.FloorToInt((float)celdaSpawn.y / macroSize);
            int smz = Mathf.FloorToInt((float)celdaSpawn.z / macroSize);
            int tipoSpawn = ObtenerTipoMacroChunk(smx, smy, smz);
            RoomData rdSpawn = tipoSpawn == 1 ? ObtenerRoomDataParaMacro(smx, smy, smz) : null;
            string desc = rdSpawn != null ? $"'{rdSpawn.nombreSala}' (cuartel: {rdSpawn.esCuartelCentinelas})" : "—";
            Debug.Log($"[WorldGenerator] Spawn en celda {celdaSpawn} · macro ({smx},{smy},{smz}) tipo {tipoSpawn} · sala {desc}");
        }

        Random.state = prev;
    }

    /// <summary>posicionSpawn a partir de celdaSpawn (misma cuenta que el spawn normal) + aviso.</summary>
    private void FijarSpawnDebug(string lugar, string nombreToggle)
    {
        posicionSpawn = transform.position + new Vector3(
            celdaSpawn.x * cellSize + (cellSize / 2f),
            celdaSpawn.y * cellSize + 2f,
            celdaSpawn.z * cellSize + (cellSize / 2f)
        );
        Debug.LogWarning($"[WorldGenerator] SPAWN EN {lugar} (DEBUG) · celda {celdaSpawn} · pos {posicionSpawn}. Acordate de apagar '{nombreToggle}'.");
    }

    /// <summary>
    /// Celda pegada a la columna del pozo (tipo 6). La columna (centro del macro) tiene la escalera
    /// adentro, así que no se arranca ahí sino en la boca de pasillo de al lado: CalcularCeldaVacia
    /// talla esas 4 celdas (centro ± 1 en X/Z) cuando el pozo conecta con el vecino. Primero a la
    /// altura de pasillo (ly=1), después cualquier piso; si ninguna está abierta, la columna misma.
    /// </summary>
    private Vector3Int CeldaSpawnJuntoAPozo(Vector3Int macro)
    {
        int m = macroSize / 2;
        Vector3Int[] lados = { Vector3Int.right, Vector3Int.left, Vector3Int.forward, Vector3Int.back };

        int[] niveles = new int[macroSize];
        niveles[0] = 1;
        for (int i = 1, ly = 0; i < macroSize; ly++)
            if (ly != 1) niveles[i++] = ly;

        foreach (int ly in niveles)
        {
            Vector3Int columna = new Vector3Int(macro.x * macroSize + m, macro.y * macroSize + ly, macro.z * macroSize + m);
            foreach (Vector3Int lado in lados)
            {
                Vector3Int c = columna + lado;
                if (IsCellEmpty(c.x, c.y, c.z)) return c;
            }
        }

        Debug.LogWarning($"[WorldGenerator] El pozo del macro {macro} no tiene ninguna boca abierta — spawneando dentro de la columna.");
        return new Vector3Int(macro.x * macroSize + m, macro.y * macroSize + 1, macro.z * macroSize + m);
    }

    /// <summary>
    /// Macro chunk de ese tipo más cercano a `cerca`, abriendo cáscaras de radio creciente (mismo
    /// recorrido que TryEncontrarSalaCentinela). Para los spawns de debug.
    /// </summary>
    private bool TryEncontrarMacroDeTipo(int tipoBuscado, Vector3 cerca, int radioMacros, out Vector3Int macro)
    {
        macro = default;
        Vector3Int c = CeldaAMacroChunk(MundoACelda(cerca));
        int m = macroSize / 2;

        for (int r = 0; r <= Mathf.Max(0, radioMacros); r++)
        {
            float mejorDist = float.MaxValue;
            Vector3Int mejor = default;
            bool encontrada = false;

            void Considerar(int mx, int my, int mz)
            {
                if (ObtenerTipoMacroChunk(mx, my, mz) != tipoBuscado) return;

                Vector3Int central = new Vector3Int(mx * macroSize + m, my * macroSize + 1, mz * macroSize + m);
                float d = Vector3.Distance(CeldaACentroMundoPiso(central), cerca);
                if (d < mejorDist) { mejorDist = d; mejor = new Vector3Int(mx, my, mz); encontrada = true; }
            }

            for (int mx = c.x - r; mx <= c.x + r; mx++)
                for (int my = c.y - r; my <= c.y + r; my++)
                {
                    if (Mathf.Abs(mx - c.x) == r || Mathf.Abs(my - c.y) == r)
                    {
                        for (int mz = c.z - r; mz <= c.z + r; mz++) Considerar(mx, my, mz);
                    }
                    else
                    {
                        Considerar(mx, my, c.z - r);
                        if (r > 0) Considerar(mx, my, c.z + r);
                    }
                }

            if (encontrada) { macro = mejor; return true; }
        }

        return false;
    }

    private void PosicionarJugadorEnSpawn()
    {
        CharacterController cc = jugador.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Rigidbody rb = jugador.GetComponent<Rigidbody>();
        bool wasKinematic = false;
        if (rb != null)
        {
            wasKinematic = rb.isKinematic;
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        jugador.position = posicionSpawn;
        if (rb != null) rb.position = posicionSpawn;

        if (puntoSpawnJugador != null)
        {
            jugador.rotation = puntoSpawnJugador.rotation;
            if (rb != null) rb.rotation = puntoSpawnJugador.rotation;
        }

        PlayerData pd = jugador.GetComponent<PlayerData>();
        if (pd != null) pd.SetearSpawn(posicionSpawn);

        if (rb != null) rb.isKinematic = wasKinematic;
        if (cc != null) cc.enabled = true;

        // Debug.Log("Jugador posicionado en: " + posicionSpawn);
    }

    public void PosicionarJugadorEn(Vector3 destino, float rotY)
    {
        TeletransportarJugador(destino, Quaternion.Euler(0f, rotY, 0f));
    }

    /// <summary>
    /// Elige un punto de spawn (mismo criterio que una partida nueva sin guardado) y
    /// reposiciona al jugador ahi. Pensado para el boton "Jugar" (partida nueva) cuando
    /// ya hay un mundo cargado (por ejemplo si se cargo un guardado al iniciar la escena).
    /// </summary>
    /// <summary>
    /// Vacía los codex (items descubiertos y viviendas visitadas). Son clases ESTÁTICAS, así que
    /// sobreviven a un reinicio de partida — y en el editor con "Enter Play Mode" sin domain reload
    /// sobreviven incluso a salir y volver a entrar a Play.
    ///
    /// ⚠️ En un reinicio con semilla nueva no es cosmético: las viviendas visitadas se guardan por
    /// COORDENADA de macro chunk, y en el mundo nuevo esa misma coordenada es otra cosa. Sin esto,
    /// la pestaña Lugares listaría lugares que nunca existieron.
    /// </summary>
    private void ReiniciarCodex()
    {
        RegistroDescubrimientos.Reset();
        RegistroViviendasVisitadas.Reset();
    }

    public void IniciarNuevaPartidaAleatoria()
    {
        ReiniciarCodex();
        int semillaExtra = Random.Range(0, int.MaxValue);
        DefinirPuntoDeSpawn(semillaExtra);
        if (jugador != null)
        {
            PosicionarJugadorEnSpawn();
        }
    }

    /// <summary>
    /// Partida nueva de verdad: nueva semilla (mundo distinto), destruyendo todo lo ya generado
    /// para que se reconstruya desde cero. El WorkerBurbuja sigue corriendo (es permanente); al
    /// vaciar mundoChunks/celdasActivasSet y reapuntar la celda objetivo, en su proximo ciclo
    /// regenera todo con la semilla nueva sin que haya que tocarlo.
    /// Respeta spawnEnVivienda igual que cualquier spawn nuevo (via DefinirPuntoDeSpawn).
    /// </summary>
    public void ReiniciarMundoConSemillaAleatoria()
    {
        mundoListo = false;
        CongelarJugador(true);
        ReiniciarCodex();

        foreach (List<GameObject> objs in mundoChunks.Values)
            foreach (GameObject obj in objs)
                if (obj != null) Destroy(obj);

        foreach (Transform cont in contenedoresCelda.Values)
            if (cont != null) Destroy(cont.gameObject);
        contenedoresCelda.Clear();

        mundoChunks.Clear();
        celdasActivasSet.Clear();

        semillaDelMundo = Random.Range(0, 99999999);
        cargadoDesdeGuardado = false;

        Random.InitState(semillaDelMundo);
        offsetX = Random.Range(-10000f, 10000f);
        offsetY = Random.Range(-10000f, 10000f);
        offsetZ = Random.Range(-10000f, 10000f);

        celdasNoDescargables.Clear();
        cacheTipoChunk.Clear();
        cacheCeldaVacia.Clear();
        cacheDirBalcon.Clear();
        cacheCierre.Clear();
        cacheIslaConectada.Clear();
        cacheIslaColosal.Clear();
        cacheIslaSinViviendas.Clear();
        cacheCuartelIsla.Clear();
        cacheSalasIsla.Clear();
        misionesEncadenadasPorCelda.Clear();
        destinoPorNota.Clear();
        registroViviendas.Clear();
        viviendaBuscadaPor.Clear();

        NotaData[] todasLasNotas = Resources.LoadAll<NotaData>("");
        misionesPorCelda.Clear();
        foreach (NotaData nota in todasLasNotas)
        {
            if (nota != null && nota.tieneMision && nota.prefabCofre != null)
            {
                int seed = nota.titulo.GetHashCode() + semillaDelMundo;
                if (ObtenerCeldaDestinoMision(seed, out Vector3Int celdaDestino))
                {
                    misionesPorCelda[celdaDestino] = nota;
                }
            }
        }

        DefinirPuntoDeSpawn();
        if (jugador != null)
        {
            PosicionarJugadorEnSpawn();
        }

        celdaActualJugador = celdaSpawn;
        celdaObjetivo = celdaSpawn;

        StartCoroutine(CargarSalaInicialYSoltar(celdaSpawn));
    }

    private IEnumerator CargarSalaInicialYSoltar(Vector3Int centro)
    {
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    Vector3Int celdaSala = new Vector3Int(centro.x + x, centro.y + y, centro.z + z);
                    GenerarYActivarCelda(celdaSala);
                    celdasActivasSet.Add(celdaSala);
                }
        mundoListo = true;
        // La vivienda del spawn cuenta como descubierta: estás parado adentro.
        if (jugador != null) IntentarDescubrirLugarActual(MundoACelda(jugador.position));
        CongelarJugador(false);
        yield return null;
    }

    public void GuardarPartidaActual()
    {
        if (jugador != null)
        {
            SistemaGuardado.Guardar(semillaDelMundo, jugador.position, jugador.eulerAngles.y);
        }
    }

    /// <summary>
    /// Teletransporta al jugador a una posición arbitraria, con el mismo cuidado que
    /// PosicionarJugadorEnSpawn (desactiva CharacterController y congela el Rigidbody durante
    /// el movimiento para que no lo arrastre la física ni lo frene un collider intermedio).
    /// NO cambia el punto de respawn. La celda destino debe estar ya generada.
    /// </summary>
    public void TeletransportarJugador(Vector3 destino, Quaternion? rotacion = null)
    {
        if (jugador == null) return;

        CharacterController cc = jugador.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        Rigidbody rb = jugador.GetComponent<Rigidbody>();
        bool wasKinematic = false;
        if (rb != null)
        {
            wasKinematic = rb.isKinematic;
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        jugador.position = destino;
        if (rb != null) rb.position = destino;

        if (rotacion.HasValue)
        {
            jugador.rotation = rotacion.Value;
            if (rb != null) rb.rotation = rotacion.Value;
        }

        if (rb != null) rb.isKinematic = wasKinematic;
        if (cc != null) cc.enabled = true;

        // No se toca celdaObjetivo acá: el Update detecta el cambio de celda el próximo frame
        // y re-apunta la burbuja solo (el worker de streaming es re-apuntable en cada yield).
    }

    [Header("Generación Async")]
    [Tooltip("Presupuesto de TIEMPO (ms) por frame para el streaming de la burbuja (generar/encender/apagar celdas). Subilo para que el mundo cargue más rápido a costa de FPS durante ráfagas de carga; 4-6 es un buen rango a 60fps.")]
    public float msPresupuestoBurbuja = 5f;

    [Header("Descarga de memoria (streaming de SALIDA)")]
    [Tooltip("Radio en celdas más allá del cual una celda ya generada se DESTRUYE de verdad (Destroy + sale de mundoChunks) para devolverle la RAM al sistema. Sin esto la burbuja solo apaga (SetActive(false)) y todo lo que pisaste queda vivo en memoria para siempre. DEBE ser bastante mayor que el radio de burbuja (8) para no destruir/regenerar en bucle al caminar de ida y vuelta. 0 = nunca descargar (comportamiento viejo).")]
    public int radioDescarga = 16;

    [Tooltip("Cada cuántos segundos se barre buscando celdas lejanas para descargar. No hace falta que sea seguido: 2-5s está bien.")]
    public float intervaloDescarga = 3f;

    [Tooltip("Presupuesto de TIEMPO (ms) por frame para la descarga. Destruir también cuesta, así que se trocea igual que la generación.")]
    public float msPresupuestoDescarga = 2f;

    [Tooltip("Descartar también las entradas de caché (cacheCeldaVacia, por CELDA — el diccionario más grande de lejos) de las celdas descargadas. Son deterministas por semilla: se recalculan solas si volvés. Dejalo en true salvo que estés depurando determinismo.")]
    public bool descartarCacheAlDescargar = true;

    [Tooltip("Tope de entradas de cacheCeldaVacia. Al pasarse, se barren y descartan las de celdas lejanas. Hace falta aparte de la descarga porque los BFS (rutas de centinelas, cámara del menú) consultan IsCellEmpty en MILES de celdas que nunca llegan a generarse ni a entrar en mundoChunks — sin este tope, esas entradas no las limpiaría nadie. 0 = sin tope.")]
    public int maxEntradasCacheCelda = 200000;

    private HashSet<Vector3Int> celdasActivasSet = new HashSet<Vector3Int>();
    private Vector3Int celdaObjetivo;

    /// <summary>
    /// Worker PERMANENTE de streaming: reconcilia continuamente la burbuja hacia la celda actual
    /// del jugador (`celdaObjetivo`). A diferencia del sistema anterior (una coroutine por cruce de
    /// celda, gateada por `!generando`, que debía TERMINAR su pasada completa apuntando al centro
    /// viejo), este worker re-apunta en cada yield: si el jugador se movió a mitad de pasada,
    /// descarta lo pendiente y re-prioriza alrededor de la posición nueva. Presupuesto por TIEMPO
    /// (msPresupuestoBurbuja) en vez de por cantidad de celdas: celdas caras no clavan el frame y
    /// celdas baratas no desperdician presupuesto.
    /// </summary>
    private IEnumerator WorkerBurbuja(Vector3Int celdaInicial)
    {
        // --- Carga inicial priorizada: sala de spawn (3x3x3) y recién ahí soltar al jugador ---
        celdaObjetivo = celdaInicial;
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
                for (int z = -1; z <= 1; z++)
                {
                    Vector3Int celdaSala = new Vector3Int(celdaInicial.x + x, celdaInicial.y + y, celdaInicial.z + z);
                    GenerarYActivarCelda(celdaSala);
                    celdasActivasSet.Add(celdaSala);
                }
        mundoListo = true;
        // La vivienda del spawn cuenta como descubierta: estás parado adentro.
        if (jugador != null) IntentarDescubrirLugarActual(MundoACelda(jugador.position));
        CongelarJugador(false);
        yield return null;

        var sw = new System.Diagnostics.Stopwatch();

        while (true)
        {
            Vector3Int centro = celdaObjetivo;

            // Clamp de seguridad: el costo de la burbuja crece al CUBO del radio (radio 20 =
            // 68.921 celdas y el worker se pasa el presupuesto reconstruyendo sets en cada
            // cruce en vez de generar). Para "ver más lejos" la solución son impostores, no radio.
            int radio = Mathf.Min(radioVision, 8);

            // 1. Burbuja deseada alrededor del centro ACTUAL
            HashSet<Vector3Int> nuevaBurbuja = new HashSet<Vector3Int>();
            for (int x = -radio; x <= radio; x++)
                for (int y = -radio; y <= radio; y++)
                    for (int z = -radio; z <= radio; z++)
                    {
                        nuevaBurbuja.Add(new Vector3Int(centro.x + x, centro.y + y, centro.z + z));
                    }

            // 2. Diff contra lo que está activo
            List<Vector3Int> salidas = new List<Vector3Int>();
            foreach (Vector3Int c in celdasActivasSet)
                if (!nuevaBurbuja.Contains(c)) salidas.Add(c);

            List<Vector3Int> pendientes = new List<Vector3Int>();
            foreach (Vector3Int c in nuevaBurbuja)
                if (!celdasActivasSet.Contains(c)) pendientes.Add(c);

            // De adentro hacia afuera: lo más cercano al jugador primero
            pendientes.Sort((a, b) => (a - centro).sqrMagnitude.CompareTo((b - centro).sqrMagnitude));

            bool retarget = false;
            sw.Restart();

            // 3. Apagar las celdas que salieron (el set se actualiza incremental → abortable sin inconsistencias)
            foreach (Vector3Int c in salidas)
            {
                if (mundoChunks.TryGetValue(c, out List<GameObject> objsViejos))
                    foreach (GameObject obj in objsViejos)
                        if (obj != null) obj.SetActive(false);
                celdasActivasSet.Remove(c);

                if (sw.ElapsedMilliseconds >= msPresupuestoBurbuja)
                {
                    yield return null;
                    sw.Restart();
                    if (celdaObjetivo != centro) { retarget = true; break; }
                }
            }

            // 4. Generar/encender las que faltan (presupuestado y re-apuntable)
            if (!retarget)
            {
                foreach (Vector3Int c in pendientes)
                {
                    GenerarYActivarCelda(c);
                    celdasActivasSet.Add(c);

                    if (sw.ElapsedMilliseconds >= msPresupuestoBurbuja)
                    {
                        yield return null;
                        sw.Restart();
                        if (celdaObjetivo != centro) { retarget = true; break; }
                    }
                }
            }

            // 5. Sincronizar la lista pública (gizmos/depuración)
            celdasActivas.Clear();
            celdasActivas.AddRange(celdasActivasSet);

            // Burbuja completa y jugador quieto: dormir hasta que cambie de celda
            if (!retarget)
                while (celdaObjetivo == centro) yield return null;
        }
    }

    /// <summary>
    /// Streaming de SALIDA: destruye de verdad las celdas que quedaron lejos y las saca de
    /// `mundoChunks`, devolviéndole la RAM al sistema. La burbuja sola nunca libera nada — solo
    /// hace SetActive(false), así que sin esto la memoria crece de forma monótona con todo lo que
    /// hayas pisado alguna vez.
    ///
    /// Es seguro porque la geometría es PURA POR SEMILLA: si volvés, `GenerarCelda` la reconstruye
    /// idéntica. Lo que NO es reproducible (un cofre saqueado, un item que agarraste, una puerta
    /// que abriste) vive en celdas marcadas como no descargables — esas se dejan en memoria,
    /// apagadas, exactamente como antes. Son pocas: la mayoría del mundo es pared y pasillo.
    ///
    /// Worker permanente, igual que WorkerBurbuja: sobrevive a los reinicios de semilla porque
    /// lee `celdaObjetivo` en cada pasada y `mundoChunks` se vacía solo.
    /// </summary>
    private IEnumerator WorkerDescarga()
    {
        var sw = new System.Diagnostics.Stopwatch();
        var candidatas = new List<Vector3Int>();

        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(0.5f, intervaloDescarga));

            if (radioDescarga <= 0 || !mundoListo) continue;

            Vector3Int centro = celdaObjetivo;

            // Histéresis dura: nunca descargar dentro de (burbuja + 4). Si el radio de descarga
            // quedara pegado al de la burbuja, caminar de ida y vuelta destruiría y regeneraría
            // las mismas celdas en bucle — el peor de los mundos (tirones Y sin ahorro real).
            int radioBurbuja = Mathf.Min(radioVision, 8);
            int r = Mathf.Max(radioDescarga, radioBurbuja + 4);

            // Se juntan primero y se destruye después: no se puede mutar mundoChunks mientras se
            // lo recorre.
            candidatas.Clear();
            foreach (KeyValuePair<Vector3Int, List<GameObject>> kv in mundoChunks)
            {
                Vector3Int c = kv.Key;
                if (celdasNoDescargables.Contains(c)) continue;

                int d = Mathf.Max(Mathf.Abs(c.x - centro.x),
                        Mathf.Max(Mathf.Abs(c.y - centro.y), Mathf.Abs(c.z - centro.z)));
                if (d > r) candidatas.Add(c);
            }

            sw.Restart();
            foreach (Vector3Int c in candidatas)
            {
                if (!mundoChunks.TryGetValue(c, out List<GameObject> objs)) continue;

                if (TieneObjetosConEstado(objs))
                {
                    // Veredicto permanente: esta celda no se toca más en ninguna pasada futura.
                    celdasNoDescargables.Add(c);
                    continue;
                }

                foreach (GameObject go in objs)
                    if (go != null) Destroy(go);

                DestruirContenedorCelda(c);
                mundoChunks.Remove(c);
                celdasActivasSet.Remove(c);
                if (descartarCacheAlDescargar) cacheCeldaVacia.Remove(c);

                if (sw.ElapsedMilliseconds >= msPresupuestoDescarga)
                {
                    yield return null;
                    sw.Restart();
                    // El jugador pudo haberse movido: si el centro cambió, esta lista ya no vale.
                    if (celdaObjetivo != centro) break;
                }
            }

            PodarCacheCeldaVacia(centro, r);
        }
    }

    /// <summary>
    /// Mantiene acotado `cacheCeldaVacia`, que es el diccionario más grande del generador (una
    /// entrada por CELDA, no por chunk). Descargar celdas no alcanza para acotarlo: los BFS de
    /// pathfinding (rutas de centinelas, búsqueda de celda de la cámara del menú) llaman a
    /// IsCellEmpty sobre miles de celdas que nunca se generan ni entran en `mundoChunks`, así que
    /// esas entradas quedarían huérfanas para siempre.
    ///
    /// Solo barre cuando se pasó del tope, para no pagar el escaneo en cada pasada. Descartar es
    /// gratis en corrección: el valor es puro por semilla y se recalcula solo si vuelve a hacer
    /// falta.
    /// </summary>
    private void PodarCacheCeldaVacia(Vector3Int centro, int radio)
    {
        if (maxEntradasCacheCelda <= 0) return;
        if (cacheCeldaVacia.Count <= maxEntradasCacheCelda) return;

        var lejanas = new List<Vector3Int>();
        foreach (KeyValuePair<Vector3Int, bool> kv in cacheCeldaVacia)
        {
            Vector3Int c = kv.Key;
            int d = Mathf.Max(Mathf.Abs(c.x - centro.x),
                    Mathf.Max(Mathf.Abs(c.y - centro.y), Mathf.Abs(c.z - centro.z)));
            if (d > radio) lejanas.Add(c);
        }

        foreach (Vector3Int c in lejanas) cacheCeldaVacia.Remove(c);
    }

    /// <summary>
    /// ¿Esta celda contiene algo cuyo estado NO se puede reconstruir desde la semilla? Si sí, no
    /// se descarga nunca (un cofre ya saqueado volvería lleno, un item que agarraste reaparecería).
    /// La lista es deliberadamente generosa: equivocarse de más solo cuesta un poco de RAM,
    /// equivocarse de menos duplica objetos de gameplay.
    /// </summary>
    private static bool TieneObjetosConEstado(List<GameObject> objs)
    {
        foreach (GameObject go in objs)
        {
            if (go == null) continue;

            if (go.GetComponentInChildren<NPCHabitante>(true) != null) return true;
            if (go.GetComponentInChildren<NotaMundo>(true) != null) return true;
            if (go.GetComponentInChildren<ItemSpawner>(true) != null) return true;
            if (go.GetComponentInChildren<PuntoSpawnItem>(true) != null) return true;
            if (go.GetComponentInChildren<InventoryItem>(true) != null) return true;
            if (go.GetComponentInChildren<PuertaInteractiva>(true) != null) return true;
            if (go.GetComponentInChildren<Centinela>(true) != null) return true;
            if (go.GetComponentInChildren<BotonMundoInteractivo>(true) != null) return true;
            if (go.GetComponentInChildren<MarcaObjetoEspecial>(true) != null) return true;
            // Viviendas: guardan inventario/reservas propias (StatsVivienda no se chequea directo
            // porque es una clase de datos plana, no un Component — se chequean sus portadores).
            if (go.GetComponentInChildren<PantallaInventarioVivienda>(true) != null) return true;
            if (go.GetComponentInChildren<SeccionSala>(true) != null) return true;
        }
        return false;
    }

    private void GenerarYActivarCelda(Vector3Int celda)
    {
        if (!mundoChunks.ContainsKey(celda))
        {
            try
            {
                GenerarCelda(celda);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error generando celda {celda}: {e.Message}");
            }
        }

        if (mundoChunks.ContainsKey(celda))
        {
            foreach (GameObject obj in mundoChunks[celda])
            {
                if (obj != null) obj.SetActive(true);
            }
        }
    }

    private float ObtenerDensidad(int mx, int my, int mz)
    {
        // Usamos los offsets inicializados en Start para que cambie según la semilla
        float nx = (mx + offsetX) * escalaDensidad;
        float ny = (my + offsetY) * escalaDensidad;
        float nz = (mz + offsetZ) * escalaDensidad;

        // Simulamos ruido 3D promediando 3 muestras 2D en distintos planos
        float xy = Mathf.PerlinNoise(nx, ny);
        float yz = Mathf.PerlinNoise(ny, nz);
        float zx = Mathf.PerlinNoise(nz, nx);

        return (xy + yz + zx) / 3f;
    }

    /// <summary>
    /// Toma una pieza exterior aleatoria activa cerca de la posición dada, la desregistra del
    /// chunk al que pertenecía y la devuelve. Quien la reciba puede agregarle Rigidbody, aplicar
    /// fuerzas, programar su destrucción, etc. — esta función NO añade físicas.
    /// </summary>
    public GameObject TomarPiezaExteriorAleatoria(Vector3 centro, float radio, string[] palabrasClave)
    {
        if (palabrasClave == null || palabrasClave.Length == 0) return null;
        if (mundoChunks == null || mundoChunks.Count == 0) return null;

        int cellsR = Mathf.CeilToInt(radio / cellSize);
        Vector3 origin = transform.position;
        Vector3 local = centro - origin;
        Vector3Int centroCelda = new Vector3Int(
            Mathf.FloorToInt(local.x / cellSize),
            Mathf.FloorToInt(local.y / cellSize),
            Mathf.FloorToInt(local.z / cellSize)
        );

        // Recolectar candidatos: piezas activas, dentro del radio y que matcheen las palabras clave
        List<(Vector3Int celda, int idx, GameObject obj)> candidatos = new List<(Vector3Int, int, GameObject)>();
        for (int x = -cellsR; x <= cellsR; x++)
            for (int y = -cellsR; y <= cellsR; y++)
                for (int z = -cellsR; z <= cellsR; z++)
                {
                    Vector3Int c = centroCelda + new Vector3Int(x, y, z);
                    if (!mundoChunks.TryGetValue(c, out List<GameObject> lista)) continue;
                    for (int i = 0; i < lista.Count; i++)
                    {
                        GameObject obj = lista[i];
                        if (obj == null || !obj.activeInHierarchy) continue;
                        if (!CoincideAlgunaPalabra(obj.name, palabrasClave)) continue;
                        if (Vector3.Distance(obj.transform.position, centro) > radio) continue;
                        candidatos.Add((c, i, obj));
                    }
                }

        if (candidatos.Count == 0) return null;
        var sel = candidatos[Random.Range(0, candidatos.Count)];

        // Desregistrar: el chunk ya no es responsable de ella (la burbuja no la apagará)
        mundoChunks[sel.celda].RemoveAt(sel.idx);
        // Despegarla del transform del WorldGenerator para que vuele libre
        sel.obj.transform.SetParent(null);
        return sel.obj;
    }

    private bool CoincideAlgunaPalabra(string nombre, string[] palabras)
    {
        for (int i = 0; i < palabras.Length; i++)
            if (!string.IsNullOrEmpty(palabras[i]) && nombre.Contains(palabras[i])) return true;
        return false;
    }

    /// <summary>
    /// Devuelve el tipo de Macro Chunk en la posición mundo dada.
    /// Útil para sistemas externos (sonido ambiente, niebla, etc.) que necesitan saber
    /// si el jugador está dentro de una estructura o en el vacío.
    /// Tipos: 1=Sala Chica (Vivienda), 2/3/4=Pasillos, 5/8=Intersecciones, 6=Pozo,
    ///        7=Sala Grande (espacio enorme con pilares), 9=Abismo (void puro, nada),
    ///        10=Sala Colosal (la isla entera vaciada en una sola nave).
    /// </summary>
    public int TipoMacroChunkEnPosicion(Vector3 worldPos)
    {
        Vector3 local = worldPos - transform.position;
        int x = Mathf.FloorToInt(local.x / cellSize);
        int y = Mathf.FloorToInt(local.y / cellSize);
        int z = Mathf.FloorToInt(local.z / cellSize);
        int mx = Mathf.FloorToInt((float)x / macroSize);
        int my = Mathf.FloorToInt((float)y / macroSize);
        int mz = Mathf.FloorToInt((float)z / macroSize);
        return ObtenerTipoMacroChunk(mx, my, mz);
    }

    /// <summary>
    /// Celdas actualmente activas (generadas y encendidas) en la burbuja alrededor del jugador.
    /// Lee directo del set en tiempo real (`celdasActivasSet`), NO de `celdasActivas` (esa lista
    /// solo se sincroniza al completar cada pasada entera del streaming — puede quedar vacía o
    /// desactualizada por varios segundos justo después de cargar la escena).
    /// </summary>
    public IEnumerable<Vector3Int> CeldasActivas => celdasActivasSet;

    /// <summary>Convierte una posición de mundo a coordenadas de celda de la grilla.</summary>
    public Vector3Int MundoACelda(Vector3 worldPos)
    {
        Vector3 local = worldPos - transform.position;
        return new Vector3Int(
            Mathf.FloorToInt(local.x / cellSize),
            Mathf.FloorToInt(local.y / cellSize),
            Mathf.FloorToInt(local.z / cellSize));
    }

    /// <summary>Macro chunk (el id natural de una vivienda) que contiene una posición de mundo.</summary>
    public Vector3Int MundoAMacroChunk(Vector3 worldPos)
    {
        return CeldaAMacroChunk(MundoACelda(worldPos));
    }

    /// <summary>Macro chunk que contiene la celda dada.</summary>
    public Vector3Int CeldaAMacroChunk(Vector3Int celda)
    {
        return new Vector3Int(
            Mathf.FloorToInt((float)celda.x / macroSize),
            Mathf.FloorToInt((float)celda.y / macroSize),
            Mathf.FloorToInt((float)celda.z / macroSize));
    }

    /// <summary>Posición de mundo del piso, en el centro horizontal de la celda dada.</summary>
    public Vector3 CeldaACentroMundoPiso(Vector3Int celda)
    {
        return transform.position + new Vector3(
            celda.x * cellSize + cellSize / 2f,
            celda.y * cellSize,
            celda.z * cellSize + cellSize / 2f);
    }

    /// <summary>
    /// Cuartel de centinelas (Sala Chica tipo 1 cuya RoomData tenga `esCuartelCentinelas`) MÁS
    /// CERCANO a una posición. Devuelve la celda central habitable de la sala (lx=2, ly=1, lz=2).
    ///
    /// Se resuelve ENTERAMENTE POR CÁLCULO sobre la semilla: no mira la escena, ni la burbuja, ni
    /// lo que esté generado — solo las mismas funciones de hash/densidad que usa la generación.
    /// Misma semilla + misma posición ⇒ mismo cuartel, esté ese chunk instanciado o no.
    ///
    /// Recorre CÁSCARAS concéntricas de macro chunks hacia afuera y corta en la primera que tenga
    /// uno, así que `radioMacros` pasa a ser un TECHO y no un costo fijo: si hay un cuartel al
    /// lado, sale en la primera cáscara sin importar lo alto que esté el tope. Eso permite subir
    /// el radio para que la ronda nunca falle, sin pagarlo cuando hay uno cerca.
    ///
    /// Dentro de cada chunk el filtro BARATO va primero: ObtenerRoomDataParaMacro es un hash puro,
    /// mientras que ObtenerTipoMacroChunk paga Perlin + lógica de isla + BFS de conectividad. Como
    /// la RoomData se asigna por hash con independencia del tipo, preguntar "¿le tocaría cuartel?"
    /// antes de "¿es vivienda?" descarta la gran mayoría de los chunks sin tocar la parte cara, y
    /// da exactamente el mismo resultado que el orden inverso.
    /// </summary>
    public bool TryEncontrarSalaCentinela(Vector3 cerca, int radioMacros, out Vector3Int celdaCentral)
    {
        celdaCentral = default;
        Vector3Int c = CeldaAMacroChunk(MundoACelda(cerca));

        for (int r = 0; r <= Mathf.Max(0, radioMacros); r++)
        {
            float mejorDist = float.MaxValue;
            Vector3Int mejor = default;
            bool encontrada = false;

            void Considerar(int mx, int my, int mz)
            {
                RoomData sala = ObtenerRoomDataParaMacro(mx, my, mz);
                if (sala == null || !sala.esCuartelCentinelas) return;
                if (ObtenerTipoMacroChunk(mx, my, mz) != 1) return;

                Vector3Int central = new Vector3Int(mx * macroSize + 2, my * macroSize + 1, mz * macroSize + 2);
                float d = Vector3.Distance(CeldaACentroMundoPiso(central), cerca);
                if (d < mejorDist) { mejorDist = d; mejor = central; encontrada = true; }
            }

            // Solo la CÁSCARA de radio r: si (mx,my) ya está en el borde, toda su columna Z
            // pertenece a la cáscara; si no, solo las dos tapas en Z. Lo de adentro ya se miró
            // en las iteraciones anteriores.
            for (int mx = c.x - r; mx <= c.x + r; mx++)
                for (int my = c.y - r; my <= c.y + r; my++)
                {
                    if (Mathf.Abs(mx - c.x) == r || Mathf.Abs(my - c.y) == r)
                    {
                        for (int mz = c.z - r; mz <= c.z + r; mz++) Considerar(mx, my, mz);
                    }
                    else
                    {
                        Considerar(mx, my, c.z - r);
                        if (r > 0) Considerar(mx, my, c.z + r);
                    }
                }

            // Primera cáscara con un cuartel ⇒ es el más cercano, no hace falta seguir abriendo.
            if (encontrada) { celdaCentral = mejor; return true; }
        }

        return false;
    }

    /// <summary>
    /// ¿Estas dos posiciones caen en la MISMA isla? Fuera de toda isla (puentes, vacío) devuelve
    /// false aunque las dos estén afuera: "el mismo vacío" no es una isla compartida.
    /// </summary>
    public bool MismaIsla(Vector3 a, Vector3 b)
    {
        Vector3Int ma = CeldaAMacroChunk(MundoACelda(a));
        Vector3Int mb = CeldaAMacroChunk(MundoACelda(b));
        if (!EnIsla(ma.x, ma.y, ma.z) || !EnIsla(mb.x, mb.y, mb.z)) return false;
        return RegionDeChunk(ma.x, ma.y, ma.z) == RegionDeChunk(mb.x, mb.y, mb.z);
    }

    /// <summary>
    /// Elige AL AZAR una celda pisable de la isla que contiene `desde`, para que una entidad
    /// patrulle toda su isla en vez de dar vueltas siempre por el mismo rincón. Devuelve false si
    /// `desde` no está en ninguna isla (ej. cruzando un puente) — ahí quien llame debería caer a
    /// su comportamiento de vagabundeo local.
    ///
    /// "Pisable" = celda vacía con algo sólido justo debajo. Ese test vale para todos los tipos sin
    /// casos especiales: en pasillos y salas da los ly de piso, y en una Sala Colosal da solo la
    /// planta baja (el resto del volumen es aire y no se puede caminar).
    ///
    /// El muestreo es por macro chunk al azar dentro del volumen de la isla, y dentro de él se
    /// juntan las celdas pisables de las dos alturas que usa el generador (ly 1 y 3). Determinista
    /// solo en la forma del mundo, NO en la elección: usa el Random de Unity a propósito para que
    /// dos centinelas de la misma isla no patrullen exactamente el mismo recorrido.
    /// </summary>
    public bool TryElegirCeldaPatrullaEnIsla(Vector3 desde, out Vector3Int celda, int intentos = 16)
    {
        celda = default;

        Vector3Int macro = CeldaAMacroChunk(MundoACelda(desde));
        if (!EnIsla(macro.x, macro.y, macro.z)) return false;

        Vector3Int region = RegionDeChunk(macro.x, macro.y, macro.z);
        Vector3Int centro = CentroIsla(region.x, region.y, region.z, out int radio, out _);

        List<Vector3Int> pisables = new List<Vector3Int>();

        for (int intento = 0; intento < Mathf.Max(1, intentos); intento++)
        {
            int mx = centro.x + Random.Range(-radio, radio + 1);
            int my = centro.y + Random.Range(-radio, radio + 1);
            int mz = centro.z + Random.Range(-radio, radio + 1);
            if (!EnIsla(mx, my, mz)) continue;

            int tipo = ObtenerTipoMacroChunk(mx, my, mz);
            if (tipo == 9) continue; // vacío puro: nada donde pararse

            // Las viviendas son casas de gente: la ronda pasa por fuera (pasillos, cruces, pozos),
            // no se mete adentro. El CUARTEL sí está permitido aunque también sea Sala Chica —
            // es su propia base.
            if (!centinelasEntranAViviendas && tipo == 1 && !EsChunkCuartelDeIsla(mx, my, mz))
                continue;

            pisables.Clear();
            foreach (int ly in new[] { 1, 3 })
                for (int lx = 0; lx < macroSize; lx++)
                    for (int lz = 0; lz < macroSize; lz++)
                    {
                        int x = mx * macroSize + lx;
                        int y = my * macroSize + ly;
                        int z = mz * macroSize + lz;
                        if (IsCellEmpty(x, y, z) && !IsCellEmpty(x, y - 1, z))
                            pisables.Add(new Vector3Int(x, y, z));
                    }

            if (pisables.Count > 0)
            {
                celda = pisables[Random.Range(0, pisables.Count)];
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// ¿Esta celda cae en una vivienda donde los centinelas NO tienen por qué meterse?
    ///
    /// El CUARTEL queda siempre permitido aunque también sea Sala Chica: es su base y tienen que
    /// poder volver. Filtrar solo el destino de la ronda no alcanzaba — por eso este helper se usa
    /// en las TRES vías por las que un centinela elige a dónde ir (punto de interés, ruta de
    /// patrulla y vagabundeo). La ruta es la que más se escapaba: un destino legítimo del otro
    /// lado de una casa hacía que la cruzaran por adentro.
    /// </summary>
    public bool EsCeldaDeViviendaVedada(Vector3Int celda)
    {
        if (centinelasEntranAViviendas) return false;
        Vector3Int m = CeldaAMacroChunk(celda);
        if (ObtenerTipoMacroChunk(m.x, m.y, m.z) != 1) return false;
        return !EsChunkCuartelDeIsla(m.x, m.y, m.z);
    }

    /// <summary>Igual que EsCeldaDeViviendaVedada pero desde una posición de mundo.</summary>
    public bool EsPosicionDeViviendaVedada(Vector3 mundo) => EsCeldaDeViviendaVedada(MundoACelda(mundo));

    /// <summary>
    /// BFS de celdas vacías (6 vecinos) desde `desde` hasta `hasta`. Si encuentra camino
    /// dentro del presupuesto `maxNodos`, llena `ruta` (incluye ambos extremos) y devuelve
    /// true. Pensado para que entidades (centinelas) caminen por los CENTROS de las celdas
    /// en vez de cortar camino en línea recta a través de paredes.
    ///
    /// `bloqueada` (opcional) marca celdas por las que NO se puede pasar aunque estén vacías.
    /// La celda de ORIGEN nunca se bloquea: si un centinela ya quedó dentro de una vivienda, tiene
    /// que poder salir caminando. El DESTINO tampoco, para no romper llamadas con destino explícito
    /// (volver al cuartel, etc.).
    /// </summary>
    public bool TryCalcularRutaCeldas(Vector3Int desde, Vector3Int hasta, int maxNodos, List<Vector3Int> ruta, System.Func<Vector3Int, bool> bloqueada = null)
    {
        ruta.Clear();
        if (desde == hasta) { ruta.Add(desde); return true; }

        Vector3Int[] vecinos =
        {
            new Vector3Int( 1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int( 0, 0, 1), new Vector3Int( 0, 0,-1),
            new Vector3Int( 0, 1, 0), new Vector3Int( 0,-1, 0),
        };

        var visitado = new Dictionary<Vector3Int, Vector3Int>(); // celda -> desde dónde se llegó
        var cola = new Queue<Vector3Int>();
        visitado[desde] = desde;
        cola.Enqueue(desde);

        while (cola.Count > 0 && visitado.Count < maxNodos)
        {
            Vector3Int actual = cola.Dequeue();
            foreach (Vector3Int d in vecinos)
            {
                Vector3Int sig = actual + d;
                if (visitado.ContainsKey(sig)) continue;
                if (!IsCellEmpty(sig.x, sig.y, sig.z)) continue;
                if (bloqueada != null && sig != hasta && bloqueada(sig)) continue;
                visitado[sig] = actual;
                if (sig == hasta)
                {
                    // Reconstruir camino de atrás hacia adelante
                    Vector3Int cursor = hasta;
                    while (cursor != desde)
                    {
                        ruta.Add(cursor);
                        cursor = visitado[cursor];
                    }
                    ruta.Add(desde);
                    ruta.Reverse();
                    return true;
                }
                cola.Enqueue(sig);
            }
        }
        return false;
    }

    /// <summary>
    /// Devuelve la RoomData asignada deterministamente a un macro chunk de Sala (tipo 1).
    /// Si el chunk no es una sala o no hay RoomData configuradas, retorna null.
    /// La asignación es estable: mismo macro chunk siempre devuelve la misma RoomData.
    ///
    /// La elección es PONDERADA por `RoomData.pesoAparicion` (ruleta): así una sala especial
    /// (ej. el cuartel de centinelas) puede ser rara sin necesidad de rellenar el array con
    /// copias de las comunes. Con todos los pesos en 1 el reparto es uniforme.
    ///
    /// ÚNICO punto donde se elige la RoomData de un macro chunk: si esto cambia, cambia para
    /// todos los consumidores (celda central, loot, secciones, cuarteles) — no replicar la
    /// lógica en otro lado o se desincronizan.
    /// </summary>
    private RoomData ObtenerRoomDataParaMacro(int mx, int my, int mz)
    {
        if (salasGrandesPosibles == null || salasGrandesPosibles.Length == 0) return null;

        // Cuartel garantizado de la isla: ese chunk devuelve SIEMPRE una RoomData de cuartel, sin
        // pasar por la ruleta ponderada. Es lo que hace que la garantía sea real y no un "quizás".
        if (EsChunkCuartelDeIsla(mx, my, mz))
        {
            RoomData cuartel = ElegirRoomDataCuartel(mx, my, mz);
            if (cuartel != null) return cuartel;
        }

        int hash = semillaDelMundo + mx * 91827 + my * 12453 + mz * 36251;
        Random.State prev = Random.state;
        Random.InitState(hash);

        // Las RoomData de CUARTEL no entran en la ruleta: el ÚNICO cuartel de una isla es el del
        // chunk garantizado (rama de arriba). Antes el roll ponderado podía asignarle la RoomData
        // de cuartel a cualquier vivienda común — se veía como vivienda pero calificaba en
        // TryEncontrarSalaCentinela, y los centinelas aparecían "adentro de una vivienda",
        // incluso pegados al spawn. pesoAparicion de los cuarteles queda sin efecto.
        // (Único caso donde se respetan en la ruleta: si TODAS las RoomData son cuartel, para no
        // devolver null y dejar salas vacías en un proyecto mal configurado.)
        int normalesDisponibles = 0;
        float pesoTotal = 0f;
        foreach (RoomData s in salasGrandesPosibles)
        {
            if (s == null || s.esCuartelCentinelas) continue;
            normalesDisponibles++;
            pesoTotal += Mathf.Max(0f, s.pesoAparicion);
        }
        bool soloHayCuarteles = normalesDisponibles == 0;

        bool Elegible(RoomData s) => s != null && (soloHayCuarteles || !s.esCuartelCentinelas);

        RoomData sala = null;
        if (pesoTotal <= 0f)
        {
            // Todos los pesos en 0 (o solo cuarteles): reparto uniforme entre las elegibles
            int n = 0;
            foreach (RoomData s in salasGrandesPosibles)
                if (Elegible(s)) n++;
            if (n > 0)
            {
                int idx = Random.Range(0, n);
                foreach (RoomData s in salasGrandesPosibles)
                {
                    if (!Elegible(s)) continue;
                    if (idx-- == 0) { sala = s; break; }
                }
            }
        }
        else
        {
            float tirada = Random.value * pesoTotal;
            foreach (RoomData s in salasGrandesPosibles)
            {
                if (s == null || s.esCuartelCentinelas) continue;
                tirada -= Mathf.Max(0f, s.pesoAparicion);
                if (tirada <= 0f) { sala = s; break; }
            }
            // Guard por error de redondeo del float: quedarse con la última válida
            if (sala == null)
                for (int i = salasGrandesPosibles.Length - 1; i >= 0; i--)
                    if (salasGrandesPosibles[i] != null && !salasGrandesPosibles[i].esCuartelCentinelas) { sala = salasGrandesPosibles[i]; break; }
        }

        Random.state = prev;
        return sala;
    }

    private int ObtenerTipoMacroChunk(int mx, int my, int mz)
    {
        Vector3Int k = new Vector3Int(mx, my, mz);
        if (cacheTipoChunk.TryGetValue(k, out int cached)) return cached;
        int tipoCalc = CalcularTipoMacroChunk(mx, my, mz);
        cacheTipoChunk[k] = tipoCalc;
        return tipoCalc;
    }

    private int CalcularTipoMacroChunk(int mx, int my, int mz)
    {
        // Forzar Sala Grande en el spawn para evitar quedar atrapado
        if (mx == 0 && my == 0 && mz == 0) return 1;

        // ISLAS: fuera de las islas todo es vacío, salvo los puentes que las conectan.
        // (Bloque de alcance para las variables locales del branch de isla.)
        {
            bool enIsla = EnIsla(mx, my, mz);
            int tipoPuente = TipoPuente(mx, my, mz);

            if (!enIsla)
                return tipoPuente != 0 ? tipoPuente : 9;

            // 0) SALA COLOSAL: esta isla no se subdivide en nada — su volumen ENTERO es una sola
            //    sala. Se decide antes que la columna-pozo, las lanes y la porosidad: ninguno de
            //    esos sistemas aplica adentro, porque no hay "adentro" que tallar más que el hueco.
            //    El acceso lo abre el puente que llega desde afuera: su lane termina contra la
            //    cáscara y el tallado de conectores (HasConnection del tipo 10) le perfora la boca.
            if (EsIslaColosalDeChunk(mx, my, mz)) return 10;

            // DENTRO de la isla, dos forzados que garantizan que siempre se puede salir:
            // 1) Columna central = POZO vertical (tipo 6): el "shaft" del monolito. Conecta
            //    todos los niveles de la isla entre sí y desemboca en el nivel base, donde
            //    corren los puentes. Desde cualquier piso, encontrás la columna → bajás → salís.
            int pIsla = Mathf.Max(3, periodoIslas);
            Vector3Int centroIsla = CentroIsla(
                Mathf.FloorToInt((float)mx / pIsla),
                Mathf.FloorToInt((float)my / pIsla),
                Mathf.FloorToInt((float)mz / pIsla), out _, out _);
            if (mx == centroIsla.x && mz == centroIsla.z) return 6;

            // 2) Las lanes de puente ATRAVIESAN la isla a nivel base hasta la columna central:
            //    el puente no muere en el casco — desemboca en el corazón de la isla.
            if (tipoPuente != 0) return tipoPuente;

            // 2.5) CUARTEL GARANTIZADO: el chunk elegido como cuartel de la isla se fuerza a Sala
            //    Chica ANTES de porosidad/coherencia/borde — cueste lo que cueste. Si el único
            //    lugar disponible toca el vacío, el cuartel va igual ahí: preferimos una sala de
            //    borde imperfecta antes que una isla sin cuartel (los centinelas no tendrían de
            //    dónde salir) o que una vivienda común haga de cuartel.
            if (EsChunkCuartelDeIsla(mx, my, mz)) return 1;

            // 2.6) CUPO DE SALAS: las viviendas de la isla son las que eligió SalasDeIsla, y nada
            //    más. Se pueden forzar acá porque se eligen SOLO entre chunks de núcleo (no porosos,
            //    conectados, sin tocar el vacío), así que igual habrían pasado los pasos 3/4/5.
            if (EsChunkSalaDeIsla(mx, my, mz)) return 1;

            // 3) POROSIDAD: chunks de baja densidad son VACÍO también dentro de la isla.
            //    La isla no es un bloque encapsulado sino un ENJAMBRE de estructuras expuestas
            //    al void, cada una con su propia piel (como los tubos de puente) — sin paredes
            //    contenedoras gigantes. La columna-pozo y las lanes (forzadas arriba) garantizan
            //    el esqueleto conectado igual.
            if (ObtenerDensidad(mx, my, mz) < umbralVacioIsla) return 9;

            // 4) COHERENCIA: si la porosidad dejó este chunk AISLADO del esqueleto de la isla
            //    (sin cadena de vecinos hasta la columna-pozo/lanes), se descarta → void.
            //    Nada de pedazos de pasillo flotando sueltos en la nada.
            Vector3Int regionIsla = new Vector3Int(
                Mathf.FloorToInt((float)mx / pIsla),
                Mathf.FloorToInt((float)my / pIsla),
                Mathf.FloorToInt((float)mz / pIsla));
            if (!ChunksConectadosDeIsla(regionIsla).Contains(new Vector3Int(mx, my, mz))) return 9;

            // 5) BORDE DE ISLA → SOLO PASILLOS: los chunks de estructura que tocan el void no
            //    pueden ser salas/cruces (sus interiores tallados grandes quedarían "destapados"
            //    en corte contra el vacío). Las salas viven en el NÚCLEO, siempre cerradas.
            //    (El chequeo del vecino usa la misma cadena candidato+conectividad+lane — NO el
            //    tipo final, que causaría recursión infinita entre vecinos.)
            if (ChunkTocaVoid(mx, my, mz))
            {
                int hashBorde = semillaDelMundo + mx * 60493 + my * 19087 + mz * 43093;
                Random.State prevBorde = Random.state;
                Random.InitState(hashBorde);
                int[] tiposBorde = { 2, 3, 4 }; // pasillos: recto Z, recto X, en L
                int tipoBorde = tiposBorde[Random.Range(0, tiposBorde.Length)];
                Random.state = prevBorde;
                return tipoBorde;
            }

            // 6) El núcleo de la isla: tipado por densidad clásico (cae al código de abajo).
        }

        float densidad = ObtenerDensidad(mx, my, mz);

        // Generador pseudoaleatorio basado en coordenadas del Macro Chunk
        int hash = semillaDelMundo + mx * 73856 + my * 19349 + mz * 83492;
        Random.State estadoViejo = Random.state;
        Random.InitState(hash);

        int tipo;

        if (densidad > umbralDensidadAlta)
        {
            // Densidad Alta: Complejos Industriales habitables
            // Tipos: 5 (Intersección +), 6 (Pozo), 8 (Intersección T).
            //
            // La Sala Chica (1) YA NO sale por acá: las viviendas las reparte SalasDeIsla con un
            // cupo por isla (paso 2.6). Si siguiera en esta ruleta, cada chunk del núcleo tiraría
            // sus propios dados y el tope no serviría de nada. Los chunks densos que no ganaron
            // cupo se vuelven intersecciones/pozos, así que el núcleo no queda hueco.
            // (Por lo mismo, el remap de IslaSinViviendas ya no hace falta acá: esa decisión se
            // toma dentro de SalasDeIsla, que devuelve solo el cuartel.)
            int[] tiposAltos = { 5, 6, 8 };
            tipo = tiposAltos[Random.Range(0, tiposAltos.Length)];
        }
        else if (densidad > umbralDensidadMedia)
        {
            // Densidad Media: Pasillos y Caminos
            // Tipos: 2 (Pasillo Z), 3 (Pasillo X), 4 (Pasillo L)
            int[] tiposMedios = { 2, 3, 4 };
            tipo = tiposMedios[Random.Range(0, tiposMedios.Length)];
        }
        else if (densidad > umbralDensidadBaja)
        {
            // Densidad Baja: Sala Grande — espacio enorme con pilares gigantes en las esquinas.
            // Antes era llamada "Abismo" pero visualmente es más una sala monumental que un vacío.
            tipo = 7;
        }
        else
        {
            // Densidad Muy Baja: Abismo Verdadero — literalmente nada (sin pilares, sin puentes).
            // Solo void puro, perfecto para que los drones vuelen y para sensación de inmensidad.
            tipo = 9;
        }

        Random.state = estadoViejo;
        return tipo;
    }

    private bool HasConnection(int type, Vector3Int dir, int ly)
    {
        // Abismo Verdadero (9): NUNCA conecta — void puro, sin entradas.
        if (type == 9) return false;

        // Sala Grande (7): conecta en TODAS las direcciones a la altura del pasillo (ly==1).
        // El piso sólido del propio caso 7 garantiza que el jugador pise algo al cruzar la entrada.
        if (type == 7)
        {
            return ly == 1; // toda dirección sirve mientras sea la altura de pasillo
        }

        // Sala Colosal (10): igual que la Sala Grande, acepta boca desde cualquier lado a ly==1.
        // A diferencia del 7, el 10 SÍ pasa por el tallado de conectores: así el puente que llega
        // desde afuera perfora la cáscara. Contra el vacío (9) no se talla nunca, así que la
        // cáscara solo se abre donde de verdad llega un pasillo.
        if (type == 10)
        {
            return ly == 1;
        }

        // Para los demás tipos, las conexiones horizontales ocurren a la altura estándar ly == 1
        if (ly != 1) return false;

        switch (type)
        {
            case 1: // Sala Grande: conecta a todos lados
            case 5: // Intersección +: conecta a todos lados
            case 6: // Pozo Vertical: conecta a todos lados para poder entrar
                return true;

            case 2: // Pasillo Recto Z: conecta en Z (atrás/adelante)
                return dir == Vector3Int.back || dir == Vector3Int.forward;

            case 3: // Pasillo Recto X: conecta en X (izquierda/derecha)
                return dir == Vector3Int.left || dir == Vector3Int.right;

            case 4: // Pasillo en L: conecta a la derecha (X+) y adelante (Z+)
                return dir == Vector3Int.right || dir == Vector3Int.forward;

            case 8: // Intersección T: conecta a la izquierda (X-), derecha (X+) y adelante (Z+)
                return dir == Vector3Int.left || dir == Vector3Int.right || dir == Vector3Int.forward;

            default:
                return false;
        }
    }

    public bool IsCellEmpty(int x, int y, int z)
    {
        Vector3Int k = new Vector3Int(x, y, z);
        if (cacheCeldaVacia.TryGetValue(k, out bool cached)) return cached;
        bool vacia = CalcularCeldaVacia(x, y, z);
        cacheCeldaVacia[k] = vacia;
        return vacia;
    }

    private bool CalcularCeldaVacia(int x, int y, int z)
    {
        // 1. Coordenadas del Macro Chunk
        int mx = Mathf.FloorToInt((float)x / macroSize);
        int my = Mathf.FloorToInt((float)y / macroSize);
        int mz = Mathf.FloorToInt((float)z / macroSize);

        // 2. Coordenadas locales dentro del Macro Chunk (0 a 4)
        int lx = (x % macroSize + macroSize) % macroSize;
        int ly = (y % macroSize + macroSize) % macroSize;
        int lz = (z % macroSize + macroSize) % macroSize;

        int tipo = ObtenerTipoMacroChunk(mx, my, mz);

        // La fuerza de Sala Grande en el spawn ya se hace dentro de ObtenerTipoMacroChunk

        // --- CONEXIONES DINÁMICAS ENTRE CHUNKS ---
        // Sala Grande (7) y Abismo (9) son macro chunks completamente vacíos — no tallan conectores
        // ni interactúan con la lógica de pasillos.
        if (tipo != 7 && tipo != 9)
        {
            // CONEXIÓN SIMÉTRICA: el paso se abre si CUALQUIERA de los dos chunks conecta hacia
            // el otro (antes era unilateral: cada chunk tallaba solo si el VECINO conectaba hacia
            // él → un pasillo perpendicular a una vivienda dejaba una rama muerta contra la pared
            // cerrada de la sala, y la vivienda quedaba inaccesible). Como ambos chunks evalúan
            // la MISMA condición (A→B || B→A), o tallan los dos o ninguno: pasos completos,
            // sin ramas muertas, y toda sala/vivienda queda enterable desde cualquier vecino.
            // (El lado propio solo cuenta si el vecino es ESTRUCTURA (1-6,8): hacia Sala Grande (7)
            // decide el vecino como siempre, y hacia el Abismo/vacío (9) NUNCA se talla — si no,
            // toda sala en el borde de una isla se abriría un boquete al vacío.)
            // Conexión con el vecino izquierdo (X-)
            if (lz == 2 && (lx == 0 || lx == 1))
            {
                int tipoVecino = ObtenerTipoMacroChunk(mx - 1, my, mz);
                bool vecinoEstructura = tipoVecino != 7 && tipoVecino != 9;
                if (HasConnection(tipoVecino, Vector3Int.right, ly) || (vecinoEstructura && HasConnection(tipo, Vector3Int.left, ly))) return true;
            }
            // Conexión con el vecino derecho (X+)
            if (lz == 2 && (lx == 3 || lx == 4))
            {
                int tipoVecino = ObtenerTipoMacroChunk(mx + 1, my, mz);
                bool vecinoEstructura = tipoVecino != 7 && tipoVecino != 9;
                if (HasConnection(tipoVecino, Vector3Int.left, ly) || (vecinoEstructura && HasConnection(tipo, Vector3Int.right, ly))) return true;
            }
            // Conexión con el vecino de atrás (Z-)
            if (lx == 2 && (lz == 0 || lz == 1))
            {
                int tipoVecino = ObtenerTipoMacroChunk(mx, my, mz - 1);
                bool vecinoEstructura = tipoVecino != 7 && tipoVecino != 9;
                if (HasConnection(tipoVecino, Vector3Int.forward, ly) || (vecinoEstructura && HasConnection(tipo, Vector3Int.back, ly))) return true;
            }
            // Conexión con el vecino de adelante (Z+)
            if (lx == 2 && (lz == 3 || lz == 4))
            {
                int tipoVecino = ObtenerTipoMacroChunk(mx, my, mz + 1);
                bool vecinoEstructura = tipoVecino != 7 && tipoVecino != 9;
                if (HasConnection(tipoVecino, Vector3Int.back, ly) || (vecinoEstructura && HasConnection(tipo, Vector3Int.forward, ly))) return true;
            }
        }

        switch (tipo)
        {
            case 1: // Sala Grande (Con Altillo/Balcón)
                if (ly == 1) 
                    return lx > 0 && lx < 4 && lz > 0 && lz < 4; // Piso 1 hueco 3x3
                if (ly == 2) 
                    return lx == 2 && lz == 2; // Piso 2 es balcón: solo el centro es hueco
                if (ly == 3)
                    return lx > 0 && lx < 4 && lz > 0 && lz < 4; // Piso 3 hueco 3x3
                return false;

            case 2: // Pasillo Recto Z
                return lx == 2 && ly == 1;

            case 3: // Pasillo Recto X
                return lz == 2 && ly == 1;

            case 4: // Pasillo en L
                return ly == 1 && ((lx == 2 && lz >= 2) || (lz == 2 && lx >= 2));

            case 5: // Intersección + (Cruce total)
                return ly == 1 && (lx == 2 || lz == 2);

            case 6: // Pozo Vertical
                return lx == 2 && lz == 2;

            case 7: // Sala Grande: atrio gigante con PISO SÓLIDO en ly==0 (player puede caminar adentro).
                // Resto del macro vacío (ly 1-4) → 100m de ancho × 80m de altura de espacio abierto.
                // Sin el puente flotante del viejo abismo: el piso es continuo.
                return ly > 0;

            case 8: // Intersección T
                return ly == 1 && (lx == 2 || (lz == 2 && lx >= 2));

            case 9: // Abismo Verdadero: TODO vacío. Sin piso, sin paredes, sin nada.
                return true;

            case 10: // Sala Colosal: la ISLA ENTERA es una sola sala. La cáscara (las celdas del
                     // borde del volumen de isla) queda sólida y todo lo de adentro hueco — así el
                     // generador de superficies ya existente le pone piso abajo, techo arriba y
                     // muros a los lados, porque ninguna de esas caras da al vacío (tipo 9).
                return !EsCascaraSalaColosal(x, y, z);

            default:
                return false;
        }
    }

    /// <summary>
    /// Busca PuntoSpawnItem dentro de un prefab RECIÉN INSTANCIADO y resuelve cada uno con el
    /// Random.State YA SEMBRADO de la celda (determinismo por semilla — nunca llamar esto sin
    /// que el caller haya hecho Random.InitState(hash) antes). Cada item elegido se instancia
    /// EN la posición/rotación del punto y el punto original se desactiva (o destruye) para no
    /// dejar el marcador visible en juego.
    /// </summary>
    /// <summary>
    /// Resuelve los `PuntoSpawnItem` que traiga adentro un prefab de PARED. Es su propio método
    /// porque necesita SEMBRAR el Random: al llegar a la generación de paredes no hay ningún
    /// `Random.State` sembrado por celda, así que sin esto el resultado dependería del orden en
    /// que la burbuja visitó las celdas (y `WorkerDescarga` las regenera al volver, con lo cual
    /// el mismo pasillo cambiaría de contenido cada vez que te alejás y volvés).
    ///
    /// El hash usa la posición de la pared MÁS su normal interior: las dos paredes que comparten
    /// un plano (una por cada celda vecina) están en la misma posición y sin la normal sortearían
    /// exactamente lo mismo.
    ///
    /// ⚠️ Las paredes son el lugar CORRECTO para los props laterales: su rotación la fija
    /// `RotacionDesdeNormalInterior` (determinista, la cara decorada mira al pasillo). Los prefabs
    /// de PISO reciben un yaw aleatorio de 0/90/180/270 — un punto pegado a un borde ahí termina
    /// contra una pared o en mitad del camino, mitad y mitad.
    /// </summary>
    private void ProcesarPuntosSpawnItemDePared(GameObject instancia, Vector3Int celda, Vector3 pos, Vector3 normalInterior)
    {
        if (instancia == null) return;
        if (instancia.GetComponentInChildren<PuntoSpawnItem>(true) == null) return; // caso normal: pared pelada

        int hash = semillaDelMundo
                 + Mathf.RoundToInt(pos.x) * 73856
                 + Mathf.RoundToInt(pos.y) * 19349
                 + Mathf.RoundToInt(pos.z) * 83492
                 + Mathf.RoundToInt(normalInterior.x) * 3571
                 + Mathf.RoundToInt(normalInterior.z) * 6151;

        Random.State prev = Random.state;
        Random.InitState(hash);
        ProcesarPuntosSpawnItem(instancia, celda);
        Random.state = prev;
    }

    private void ProcesarPuntosSpawnItem(GameObject instancia, Vector3Int celda)
    {
        if (instancia == null) return;
        PuntoSpawnItem[] puntos = instancia.GetComponentsInChildren<PuntoSpawnItem>(true);
        if (puntos.Length == 0) return;

        // Separar en "sin grupo" (comportamiento de siempre: cada uno 100% independiente) y
        // grupos excluyentes (mismo texto en grupoExclusivo). Dentro de un grupo se evalúa por
        // Prioridad — apenas UNO logra spawnear algo, se queda con el grupo entero: los demás se
        // apagan sin tirar sus propios dados (ej. el objeto grande gana y ya no caben los barriles
        // de las esquinas).
        List<PuntoSpawnItem> sinGrupo = new List<PuntoSpawnItem>();
        Dictionary<string, List<PuntoSpawnItem>> grupos = new Dictionary<string, List<PuntoSpawnItem>>();
        foreach (PuntoSpawnItem p in puntos)
        {
            if (string.IsNullOrEmpty(p.grupoExclusivo))
            {
                sinGrupo.Add(p);
                continue;
            }
            string[] nombres = p.grupoExclusivo.Split(',');
            foreach (string nombre in nombres)
            {
                string g = nombre.Trim();
                if (string.IsNullOrEmpty(g)) continue;
                if (!grupos.TryGetValue(g, out List<PuntoSpawnItem> lista))
                {
                    lista = new List<PuntoSpawnItem>();
                    grupos[g] = lista;
                }
                if (!lista.Contains(p)) lista.Add(p);
            }
        }

        foreach (PuntoSpawnItem punto in sinGrupo) ResolverPuntoSpawnItem(punto, celda);

        HashSet<PuntoSpawnItem> evaluados = new HashSet<PuntoSpawnItem>();
        HashSet<PuntoSpawnItem> exitosos = new HashSet<PuntoSpawnItem>();
        foreach (List<PuntoSpawnItem> grupo in grupos.Values)
        {
            grupo.Sort((a, b) => b.prioridad.CompareTo(a.prioridad)); // mayor prioridad primero
            bool grupoResuelto = false;
            foreach (PuntoSpawnItem punto in grupo)
            {
                if (grupoResuelto)
                {
                    // Perdió el lugar: se apaga directo, NUNCA tira sus propios dados.
                    punto.gameObject.SetActive(false);
                    evaluados.Add(punto);
                    continue;
                }

                // Si este punto ya tuvo éxito en otro grupo anterior, este grupo también queda resuelto
                if (exitosos.Contains(punto))
                {
                    grupoResuelto = true;
                    continue;
                }

                // Si este punto ya fue evaluado en otro grupo anterior y falló (o fue desactivado), no re-evaluar
                if (evaluados.Contains(punto) || !punto.gameObject.activeSelf)
                {
                    continue;
                }

                evaluados.Add(punto);
                if (ResolverPuntoSpawnItem(punto, celda))
                {
                    grupoResuelto = true;
                    exitosos.Add(punto);
                }
            }
        }
    }

    /// <summary>
    /// Tira los dados de UN punto (probabilidad de spawn + elección de candidato) y, si gana,
    /// instancia el elegido — recursivo, así que si lo que spawnea tiene sus propios
    /// PuntoSpawnItem adentro, se resuelven en la misma pasada. Devuelve true si spawneó algo,
    /// para que el llamador (el grupo excluyente) sepa que ya no hace falta seguir probando.
    /// </summary>
    private bool ResolverPuntoSpawnItem(PuntoSpawnItem punto, Vector3Int celda)
    {
        if (punto == null || !punto.gameObject.activeSelf) return false;
        if (punto.itemsPosibles == null || punto.itemsPosibles.Length == 0) return false;

        // Roll 1: ¿este punto genera algo?
        if (Random.value > punto.probabilidadSpawn) { punto.gameObject.SetActive(false); return false; }

        // Roll 2: primer candidato que pase su probabilidad (mismo criterio que RoomData.LootEntry)
        GameObject elegido = null;
        foreach (PuntoSpawnItem.ItemPosible item in punto.itemsPosibles)
        {
            if (item.prefab != null && Random.value <= item.probabilidad) { elegido = item.prefab; break; }
        }

        punto.gameObject.SetActive(false); // el marcador nunca se ve en juego
        if (elegido == null) return false;

        GameObject go = Instantiate(elegido, punto.transform.position, punto.transform.rotation, transform);

        // RECURSIVO: mismo Random.State (ya sembrado por el caller original), así que la cadena
        // entera sigue siendo determinista por semilla sin que cada eslabón siembre el suyo.
        ProcesarPuntosSpawnItem(go, celda);

        go.SetActive(false);
        RegistrarEnCelda(celda, go);
        return true;
    }

    /// <summary>
    /// Instancia el prefab de sección (SeccionSala) que le toca a esta celda, centrado en la
    /// celda y a la altura del piso. El contenido va armado a mano dentro del prefab, así que
    /// acá no se genera nada más. Sin `rotacion` va sin girar (lo que necesita la sección de
    /// almacenamiento de la celda central, que tiene sus 4 lados libres y no hay a cuál apuntar).
    /// </summary>
    private void InstanciarSeccion(GameObject prefabSeccion, Vector3Int celda, Vector3 origin, int x, int y, int z, Quaternion? rotacion = null)
    {
        if (prefabSeccion == null) return;

        Vector3 pos = origin + new Vector3(
            x * cellSize + (cellSize / 2f),
            y * cellSize,
            z * cellSize + (cellSize / 2f));

        GameObject go = Instantiate(prefabSeccion, pos, rotacion ?? Quaternion.identity, transform);
        go.SetActive(false);
        RegistrarEnCelda(celda, go);
    }

    /// <summary>
    /// Secciones de vivienda en las 4 ESQUINAS del 3x3 (lx/lz ∈ {1,3}) — nunca en las celdas
    /// del medio de cada lado, que son las que dan a las puertas.
    /// El reparto sección↔esquina se baraja con el hash del MACRO CHUNK (no de la celda), así
    /// las 4 celdas esquina replican el MISMO barajado por separado y cada una sabe qué le toca
    /// sin comunicarse entre sí (mismo patrón de "predictores por hash" del resto del generador).
    /// </summary>
    private void IntentarGenerarSeccionVivienda(int mx, int my, int mz, Vector3Int celda, Vector3 origin, int x, int y, int z, int lx, int lz)
    {
        RoomData sala = ObtenerRoomDataParaMacro(mx, my, mz);
        if (sala == null) return;

        GameObject[] secciones = { sala.seccionAgua, sala.seccionComida, sala.seccionBateria, sala.seccionDormir };

        // Barajar el orden de las secciones (Fisher-Yates) con el hash del macro chunk
        int hashMacro = semillaDelMundo + mx * 55103 + my * 71317 + mz * 90379;
        Random.State prev = Random.state;
        Random.InitState(hashMacro);

        int[] orden = { 0, 1, 2, 3 };
        for (int i = orden.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (orden[i], orden[j]) = (orden[j], orden[i]);
        }
        Random.state = prev;

        // Índice de esta esquina en el orden fijo (1,1) (3,1) (1,3) (3,3)
        int idxEsquina = (lx == 1 ? 0 : 1) + (lz == 1 ? 0 : 2);
        GameObject prefabSeccion = secciones[orden[idxEsquina]];

        InstanciarSeccion(prefabSeccion, celda, origin, x, y, z, RotacionSeccionEsquina(x, y, z, lx, lz));
    }

    /// <summary>
    /// Rotación de una sección de esquina para que MIRE a uno de sus dos lados libres.
    ///
    /// En el 3x3 de la vivienda las esquinas son lx/lz ∈ {1,3}: de sus 4 caras, las dos que
    /// apuntan hacia AFUERA del 3x3 (a lx/lz 0 ó 4) dan contra la masa sólida que rodea la sala,
    /// y las dos que apuntan hacia ADENTRO dan a las celdas de paso — esas son las libres. O sea
    /// que la dirección libre en cada eje sale sola de en qué extremo está la esquina: si lx==1
    /// lo libre es +X, si lx==3 es -X, e igual en Z.
    ///
    /// Entre esas dos elige por hash de la CELDA, así no todas las viviendas quedan calcadas pero
    /// cada una es estable con la semilla. Como la elegida siempre es libre, la espalda de la
    /// sección queda contra una de las dos paredes, nunca al aire.
    /// </summary>
    private Quaternion RotacionSeccionEsquina(int x, int y, int z, int lx, int lz)
    {
        if (!orientarSeccionesALadoLibre) return Quaternion.identity;

        Vector3 libreX = lx == 1 ? Vector3.right : Vector3.left;
        Vector3 libreZ = lz == 1 ? Vector3.forward : Vector3.back;

        int hash = semillaDelMundo + x * 39119 + y * 51749 + z * 64007;
        Random.State prev = Random.state;
        Random.InitState(hash);
        Vector3 mirarHacia = Random.value < 0.5f ? libreX : libreZ;
        Random.state = prev;

        return Quaternion.LookRotation(mirarHacia, Vector3.up) * Quaternion.Euler(0f, yawBaseSeccion, 0f);
    }

    // ============================================================
    //  RED DE VIVIENDAS (conexión narrativa entre viviendas)
    // ============================================================

    /// <summary>
    /// Registra esta vivienda en la red (idempotente por macro chunk — el id natural) y elige
    /// de forma determinista hacia dónde "cree" que está la vecina que le toca buscar, entre
    /// viviendas REALES (puede no estar generada todavía).
    ///
    /// ⚠️ NO conecta ni acepta nada acá: esto corre cuando la celda se GENERA (streaming), y la
    /// burbuja genera hasta 8 celdas alrededor del jugador — el centro de un macro chunk vecino
    /// existe mucho antes de que el jugador lo pise, y ni hablar de que le hable al NPC. La
    /// conexión la dispara hablarle al NPC de cada vivienda (ver OnHablarConVivienda).
    /// </summary>
    private void RegistrarVivienda(int mx, int my, int mz, Vector3Int celda, Vector3 origin)
    {
        Vector3Int macroChunk = new Vector3Int(mx, my, mz);
        if (registroViviendas.ContainsKey(macroChunk)) return; // ya registrada

        var datos = new DatosVivienda { macroChunk = macroChunk };
        datos.habitante = ElegirHabitanteParaVivienda(macroChunk);
        registroViviendas[macroChunk] = datos;

        int hash = semillaDelMundo + mx * 50261 + my * 88439 + mz * 27061;
        if (BuscarViviendaCercana(macroChunk, hash, out Vector3Int objetivo))
        {
            datos.macroChunkBuscado = objetivo;
            datos.tieneObjetivo = true;

            if (!viviendaBuscadaPor.TryGetValue(objetivo, out List<Vector3Int> buscadores))
            {
                buscadores = new List<Vector3Int>();
                viviendaBuscadaPor[objetivo] = buscadores;
            }
            buscadores.Add(macroChunk);

            InstanciarCartelBusquedaVivienda(celda, origin, macroChunk, objetivo);
        }
    }

    /// <summary>
    /// Elige el habitante de una vivienda: determinista por semilla + macro chunk, ponderado por
    /// pesoAparicion (mismo criterio que ObtenerRoomDataParaMacro para las salas).
    /// </summary>
    private HabitanteData ElegirHabitanteParaVivienda(Vector3Int macroChunk)
    {
        if (habitantesPosibles == null || habitantesPosibles.Length == 0) return null;

        float pesoTotal = 0f;
        foreach (HabitanteData h in habitantesPosibles)
            if (h != null) pesoTotal += Mathf.Max(0f, h.pesoAparicion);

        if (pesoTotal <= 0f) return null;

        int hash = semillaDelMundo + macroChunk.x * 46021 + macroChunk.y * 12983 + macroChunk.z * 71993;
        Random.State prev = Random.state;
        Random.InitState(hash);
        float tirada = Random.value * pesoTotal;
        Random.state = prev;

        foreach (HabitanteData h in habitantesPosibles)
        {
            if (h == null) continue;
            tirada -= Mathf.Max(0f, h.pesoAparicion);
            if (tirada <= 0f) return h;
        }
        return null;
    }

    /// <summary>
    /// Diálogo completo que da el NPC de esta vivienda (saludo propio del habitante + la pista
    /// hacia la vecina que buscan, o el agradecimiento si ya están conectadas).
    /// El NPC lo PIDE (en vez de que se lo inyecten al instanciar), así funciona igual si el NPC
    /// viene embebido en el prefab de la sala en vez de spawnearlo el generador.
    /// </summary>
    public bool TryObtenerPistaVivienda(Vector3Int macroChunk, out string pista)
    {
        pista = null;
        if (!registroViviendas.TryGetValue(macroChunk, out DatosVivienda datos)) return false;

        // Hash propio para las frases: distinto al de la elección de habitante, así dos
        // viviendas con el MISMO habitante igual pueden usar frases distintas de su repertorio.
        int hashFrase = semillaDelMundo + macroChunk.x * 15413 + macroChunk.y * 33961 + macroChunk.z * 58171;
        HabitanteData hab = datos.habitante;
        string nombre = (hab != null && !string.IsNullOrWhiteSpace(hab.nombre)) ? hab.nombre : null;

        if (datos.conectada)
        {
            // Primera vez que le hablás después de conectar: el agradecimiento de verdad.
            // De ahí en más, si vuelve a hablarle, charla de relleno (o repite el agradecimiento
            // si no se cargó una frase específica para eso).
            string[] repertorio = datos.yaAgradecio ? hab?.charlaPosMision : hab?.agradecimientos;
            string frase = hab != null ? HabitanteData.ElegirFrase(repertorio, hashFrase) : null;
            if (string.IsNullOrWhiteSpace(frase))
            {
                // Antes acá había un "..." literal y se leía como un bug en pantalla, igual que el
                // de NPCHabitante. Si querés que un habitante se quede callado de verdad, cargale
                // los puntos a mano en su charlaPosMision — pero que sea una decisión, no un hueco.
                frase = datos.yaAgradecio
                    ? "Seguimos acá. Gracias de nuevo."
                    : "Gracias por conectarnos. Ahora sabemos que no estamos solos.";
            }
            datos.yaAgradecio = true;

            pista = nombre != null ? $"{nombre}: {frase}" : frase;
            return true;
        }

        // ⚠️ Sin objetivo NO es un error, y no puede devolver false: NPCHabitante interpreta el
        // false como "no hay nada que decir" y muestra "...", que en pantalla se lee como un bug.
        //
        // Pasa cuando BuscarViviendaCercana no encontró ninguna otra vivienda válida en el radio
        // (ej. una isla que salió con el cuartel y una sola casa — el cuartel ocupa un cupo de
        // salasPorIsla y los cuarteles están excluidos como objetivo). Una vivienda realmente
        // aislada es parte de la ficción, así que se responde con una frase de verdad.
        if (!datos.tieneObjetivo)
        {
            string fraseSola = hab != null ? HabitanteData.ElegirFrase(hab.frasesSinObjetivo, hashFrase) : null;
            if (string.IsNullOrWhiteSpace(fraseSola))
                fraseSola = "No sé de nadie más por acá. Hace mucho que no pasa nadie.";

            string saludoSolo = hab != null ? HabitanteData.ElegirFrase(hab.saludos, hashFrase) : null;
            string cuerpoSolo = string.IsNullOrWhiteSpace(saludoSolo) ? fraseSola : $"{saludoSolo}\n{fraseSola}";

            pista = nombre != null ? $"{nombre}: {cuerpoSolo}" : cuerpoSolo;
            return true;
        }

        string dir = DireccionCardinal(macroChunk, datos.macroChunkBuscado);

        string peticion = hab != null ? HabitanteData.ElegirFrase(hab.peticiones, hashFrase) : null;
        if (string.IsNullOrWhiteSpace(peticion))
            peticion = "Creemos que hay otra vivienda hacia el {DIR}...";
        peticion = peticion.Replace("{DIR}", dir);

        string saludo = hab != null ? HabitanteData.ElegirFrase(hab.saludos, hashFrase) : null;

        string cuerpo = string.IsNullOrWhiteSpace(saludo) ? peticion : $"{saludo}\n{peticion}";
        pista = nombre != null ? $"{nombre}: {cuerpo}" : cuerpo;
        return true;
    }

    /// <summary>
    /// Se llama cuando el jugador le habla a un NPC de vivienda (NPCHabitante.Hablar).
    /// Leer/escuchar al NPC = aceptar su pedido en el momento, sin popup de confirmación.
    ///
    /// Cada NPC cumple doble rol:
    ///   1. Acepta la propia misión de ESTA vivienda (que busca a otra).
    ///   2. Si UNA O MÁS viviendas ya te habían registrado a VOS como su objetivo (puede pasar
    ///      más de una por casualidad) Y ya aceptaron la suya, hablarle a este NPC entrega
    ///      TODAS esas misiones pendientes de una — no solo la primera que matchee.
    /// </summary>
    public void OnHablarConVivienda(Vector3Int macroChunk)
    {
        if (registroViviendas.TryGetValue(macroChunk, out DatosVivienda miVivienda))
        {
            miVivienda.misionAceptada = true;
        }

        if (viviendaBuscadaPor.TryGetValue(macroChunk, out List<Vector3Int> buscadores))
        {
            foreach (Vector3Int origen in buscadores)
            {
                if (registroViviendas.TryGetValue(origen, out DatosVivienda datosOrigen)
                    && datosOrigen.misionAceptada
                    && !datosOrigen.conectada)
                {
                    ConectarViviendas(origen, macroChunk);
                }
            }
        }
    }

    /// <summary>
    /// Busca, sin generar nada, una vivienda REAL (macro chunk tipo 1, no cuartel) dentro de
    /// radioBusquedaViviendaMacros alrededor del origen. ObtenerTipoMacroChunk es una función
    /// pura — funciona aunque esa región del mundo nunca se haya generado todavía.
    /// </summary>
    private bool BuscarViviendaCercana(Vector3Int origenMacro, int hash, out Vector3Int viviendaEncontrada)
    {
        viviendaEncontrada = Vector3Int.zero;
        int radioMax = Mathf.Max(3, radioBusquedaViviendaMacros);

        // ⚠️ El alcance VERTICAL estaba hardcodeado en ±2 y eso rompía el emparejamiento entero
        // dentro de la isla del spawn:
        //
        //   · La isla mide radioIslaChunks.y*2+1 macro chunks de lado (5 con radio 2), así que
        //     desde el chunk de spawn (0,0,0) la distancia Chebyshev MÁXIMA en XZ es 2.
        //   · separacionMinimaSalasChunks (3) se mide en Chebyshev INCLUYENDO Y, así que ninguna
        //     otra sala puede separarse lo suficiente usando solo X/Z — no hay lugar. El generador
        //     está OBLIGADO a empujarlas a my = 3 o 4.
        //   · Y ±2 excluye exactamente ese rango. Resultado: tieneObjetivo = false para siempre,
        //     por más que subieras salasPorIsla.
        //
        // Con el default (5) se cubre la altura completa de una isla desde cualquier punto de ella.
        int radioY = Mathf.Max(2, radioYBusquedaViviendaMacros);

        // Cáscaras Chebyshev crecientes: nos quedamos con la PRIMERA cáscara que tenga
        // viviendas (las más cercanas). Sortear entre todo el volumen del radio hacía que
        // casi siempre saliera una lejísimos — el volumen crece al cubo, así que la enorme
        // mayoría de candidatas están en el borde exterior.
        // Contadores solo para el diagnóstico de abajo.
        int tipo1Vistos = 0, cuartelesDescartados = 0, porGarantiaDeIsla = 0, muestras = 0;
        var detalle = new System.Text.StringBuilder();

        List<Vector3Int> candidatas = new List<Vector3Int>();
        for (int r = 1; r <= radioMax && candidatas.Count == 0; r++)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -radioY; dy <= radioY; dy++)
                    for (int dz = -r; dz <= r; dz++)
                    {
                        // Solo el borde de la cáscara actual (el interior ya se revisó).
                        // ⚠️ La cáscara se define SOLO en XZ, así que la columna dx==0 && dz==0
                        // nunca entra (daría Max=0, y r arranca en 1). En la práctica no importa:
                        // SalasDeIsla ya excluye la columna central de la isla, que es justo esa.
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;

                        int cmx = origenMacro.x + dx, cmy = origenMacro.y + dy, cmz = origenMacro.z + dz;
                        if (ObtenerTipoMacroChunk(cmx, cmy, cmz) != 1) continue;
                        tipo1Vistos++;

                        RoomData rd = ObtenerRoomDataParaMacro(cmx, cmy, cmz);
                        if (rd != null && rd.esCuartelCentinelas)
                        {
                            cuartelesDescartados++;
                            // ¿Vino de la garantía "un cuartel por isla" o de la ruleta ponderada?
                            // Son dos bugs distintos y el número los separa.
                            if (EsChunkCuartelDeIsla(cmx, cmy, cmz))
                            {
                                porGarantiaDeIsla++;
                                // Muestra de los 3 primeros: chunk, su región, y qué chunk dice esa
                                // región que es su cuartel. Si el "cuartel de la región" cambia en
                                // cada muestra, hay más regiones de las que debería (periodo mal), y
                                // si coincide con el chunk siempre, RegionDeChunk está devolviendo
                                // el chunk en vez de la región.
                                if (muestras < 3)
                                {
                                    muestras++;
                                    Vector3Int reg = RegionDeChunk(cmx, cmy, cmz);
                                    TryCuartelDeIsla(reg, out Vector3Int cu);
                                    detalle.Append($"\n   chunk ({cmx},{cmy},{cmz}) → región {reg} → cuartel de esa región {cu}");
                                }
                            }
                            continue;
                        }

                        candidatas.Add(new Vector3Int(cmx, cmy, cmz));
                    }
        }

        if (candidatas.Count == 0)
        {
            // Diagnóstico: separa las tres causas posibles de que una vivienda quede sin objetivo.
            //   tipo1Vistos == 0                      → no hay NINGUNA sala en el rango (¿radio corto?
            //                                            ¿isla sin viviendas? ¿porosidad muy alta?)
            //   tipo1Vistos > 0 y todas cuarteles     → hay salas pero todas son cuartel
            //   (no debería pasar otro caso)
            // Debug.Log y no LogWarning a propósito: el filtro de warnings de la consola puede
            // estar apagado y el diagnóstico se pierde justo cuando lo necesitás.
            if (logBusquedaVivienda)
            {
                Debug.Log($"[RedViviendas] {origenMacro} NO encontró pareja. " +
                    $"Salas tipo 1 vistas: {tipo1Vistos} · cuartel: {cuartelesDescartados} (de los cuales {porGarantiaDeIsla} por la garantía de isla, el resto salió de la RULETA). " +
                    $"Radio XZ: {radioMax}, radio Y: {radioY}, periodoIslas: {periodoIslas}, cuartelGarantizadoPorIsla: {cuartelGarantizadoPorIsla}.{detalle}");
            }
            return false;
        }

        if (logBusquedaVivienda)
            Debug.Log($"[RedViviendas] {origenMacro} → pareja en {candidatas[0]} y {candidatas.Count - 1} alternativa(s) más.");

        Random.State prev = Random.state;
        Random.InitState(hash);
        viviendaEncontrada = candidatas[Random.Range(0, candidatas.Count)];
        Random.state = prev;
        return true;
    }

    private void ConectarViviendas(Vector3Int a, Vector3Int b)
    {
        if (registroViviendas.TryGetValue(a, out DatosVivienda da)) { da.conectada = true; da.conectadaCon = b; }
        if (registroViviendas.TryGetValue(b, out DatosVivienda db)) { db.conectada = true; db.conectadaCon = a; }
        Debug.Log($"[WorldGenerator] Viviendas conectadas: {a} <-> {b}");
    }

    /// <summary>
    /// Instancia lo que le da la pista al jugador sobre la vivienda que busca. Es OPCIONAL: si
    /// el NPC ya viene embebido en el prefab de la sala (Centro.Vivienda.prefab), no hace falta
    /// asignar nada acá — ese NPC deduce su vivienda y pide su pista solo.
    /// Sirve para el cartel plano, o para spawnear un NPC en salas cuyo prefab no lo traiga.
    /// </summary>
    private void InstanciarCartelBusquedaVivienda(Vector3Int celda, Vector3 origin, Vector3Int origenMacro, Vector3Int objetivoMacro)
    {
        if (prefabNPCHabitante == null && prefabCartelBusquedaVivienda == null) return;

        Vector3 pos = CeldaACentroMundoPiso(celda) + Vector3.up * alturaPistaVivienda;

        GameObject prefabElegido = prefabNPCHabitante != null ? prefabNPCHabitante : prefabCartelBusquedaVivienda;
        GameObject go = Instantiate(prefabElegido, pos, Quaternion.identity, transform);

        // El NPC resuelve su vivienda y su pista solo (ver NPCHabitante). Al cartel
        // plano, en cambio, hay que escribirle el texto acá porque no tiene lógica propia.
        if (go.GetComponent<NPCHabitante>() == null)
        {
            TMP_Text texto = go.GetComponentInChildren<TMP_Text>();
            if (texto != null)
                texto.text = $"Creemos que hay otra vivienda hacia el {DireccionCardinal(origenMacro, objetivoMacro)}...";
        }

        go.SetActive(false);
        RegistrarEnCelda(celda, go);
    }

    /// <summary>Dirección cardinal aproximada de un macro chunk a otro (NORTE, SURESTE, etc).</summary>
    public string DireccionCardinal(Vector3Int desde, Vector3Int hacia)
    {
        int dx = hacia.x - desde.x;
        int dz = hacia.z - desde.z;
        bool norte = dz > 0, sur = dz < 0, este = dx > 0, oeste = dx < 0;

        if (norte && este) return "NORESTE";
        if (norte && oeste) return "NOROESTE";
        if (sur && este) return "SURESTE";
        if (sur && oeste) return "SUROESTE";
        if (norte) return "NORTE";
        if (sur) return "SUR";
        if (este) return "ESTE";
        if (oeste) return "OESTE";
        return "ALGÚN LUGAR CERCANO";
    }

    /// <summary>Datos de una vivienda ya registrada (petición/conexión), si existe.</summary>
    public bool TryObtenerDatosVivienda(Vector3Int macroChunk, out DatosVivienda datos)
    {
        return registroViviendas.TryGetValue(macroChunk, out datos);
    }

    /// <summary>Todas las viviendas registradas hasta ahora (para HUD/debug).</summary>
    public IEnumerable<DatosVivienda> ViviendasRegistradas => registroViviendas.Values;

    /// <summary>Debug: vuelca el estado completo de la red de viviendas a la consola.</summary>
    public void DebugImprimirRedViviendas()
    {
        Debug.Log($"[RedViviendas] {registroViviendas.Count} vivienda(s) registrada(s):");
        foreach (var kv in registroViviendas)
        {
            DatosVivienda d = kv.Value;
            string estado = d.conectada ? $"CONECTADA con {d.conectadaCon}" : $"buscando en {d.macroChunkBuscado} (no encontrada aún)";
            Debug.Log($"  · {d.macroChunk} → {estado}");
        }
    }

    private void GenerarLootDeSala(int mx, int my, int mz, Vector3Int celda, Vector3 origin, int x, int y, int z, GameObject roomObj)
    {
        if (salasGrandesPosibles == null || salasGrandesPosibles.Length == 0) return;

        // La RoomData se pide al único punto de decisión (ponderado por pesoAparicion), y
        // recién después se siembra el Random para el reparto del loot en sí.
        RoomData sala = ObtenerRoomDataParaMacro(mx, my, mz);

        if (sala == null || !sala.esCuartelCentinelas)
        {
            RegistrarVivienda(mx, my, mz, celda, origin);
        }

        int hash = semillaDelMundo + mx * 91827 + my * 12453 + mz * 36251;
        Random.State estadoViejo = Random.state;
        Random.InitState(hash);

        if (sala != null)
        {
            // Intentar buscar puntos de spawn en el objeto de la sala
            List<Transform> spawnPoints = new List<Transform>();
            if (roomObj != null)
            {
                Transform[] childs = roomObj.GetComponentsInChildren<Transform>(true);
                foreach (Transform child in childs)
                {
                    if (child != null && (child.name.ToLower().Contains("spawnpoint") || child.name.ToLower().Contains("lootspawn")))
                    {
                        spawnPoints.Add(child);
                    }
                }
            }

            // Generar Loot estándar en la sala
            if (sala.lootPosible != null && sala.lootPosible.Length > 0)
            {
                int cantidad = Random.Range(sala.cantidadLoot.x, sala.cantidadLoot.y + 1);

                // Si hay puntos de spawn definidos en el prefab, los usamos prioritariamente
                if (spawnPoints.Count > 0)
                {
                    // Barajar los puntos de spawn usando la semilla aleatoria
                    for (int i = 0; i < spawnPoints.Count; i++)
                    {
                        int tempIdx = Random.Range(i, spawnPoints.Count);
                        Transform temp = spawnPoints[i];
                        spawnPoints[i] = spawnPoints[tempIdx];
                        spawnPoints[tempIdx] = temp;
                    }

                    int spawnCount = Mathf.Min(cantidad, spawnPoints.Count);
                    for (int i = 0; i < spawnCount; i++)
                    {
                        List<GameObject> candidatos = new List<GameObject>();
                        foreach (RoomData.LootEntry entry in sala.lootPosible)
                        {
                            if (entry.prefab != null && Random.value <= entry.probabilidad)
                                candidatos.Add(entry.prefab);
                        }

                        if (candidatos.Count == 0) continue;

                        GameObject prefabElegido = candidatos[Random.Range(0, candidatos.Count)];
                        Transform pt = spawnPoints[i];

                        // Instanciar usando la posición y rotación exacta del punto de spawn
                        GameObject go = Instantiate(prefabElegido, pt.position, pt.rotation, transform);
                        go.SetActive(false);
                        RegistrarEnCelda(celda, go);
                    }
                }
                else
                {
                    // Fallback: spawn aleatorio por coordenadas (con un rango reducido a 0.7f para evitar paredes exteriores)
                    for (int i = 0; i < cantidad; i++)
                    {
                        List<GameObject> candidatos = new List<GameObject>();
                        foreach (RoomData.LootEntry entry in sala.lootPosible)
                        {
                            if (entry.prefab != null && Random.value <= entry.probabilidad)
                                candidatos.Add(entry.prefab);
                        }

                        if (candidatos.Count == 0) continue;

                        GameObject prefabElegido = candidatos[Random.Range(0, candidatos.Count)];

                        // Rango aleatorio reducido para evitar quedar demasiado pegado a las paredes
                        float offsetX = Random.Range(-1f, 1f) * cellSize * 0.7f;
                        float offsetZ = Random.Range(-1f, 1f) * cellSize * 0.7f;
                        Vector3 pos = origin + new Vector3(
                            x * cellSize + (cellSize / 2f) + offsetX,
                            y * cellSize + 0.1f,
                            z * cellSize + (cellSize / 2f) + offsetZ);

                        GameObject go = Instantiate(prefabElegido, pos, Quaternion.Euler(0f, Random.Range(0, 360), 0f), transform);
                        go.SetActive(false);
                        RegistrarEnCelda(celda, go);
                    }
                }
            }
        }

        Random.state = estadoViejo;
    }

    private void GenerarCelda(Vector3Int celda)
    {
        // IMPORTANTE: Siempre crear la entrada en el diccionario para evitar KeyNotFoundException
        mundoChunks[celda] = new List<GameObject>();

        Vector3 origin = transform.position;
        int x = celda.x;
        int y = celda.y;
        int z = celda.z;

        // 0. ESCALERAS (En el centro de la Sala Grande, para subir del piso 1 al balcón en el piso 2)
        int mx = Mathf.FloorToInt((float)x / macroSize);
        int my = Mathf.FloorToInt((float)y / macroSize);
        int mz = Mathf.FloorToInt((float)z / macroSize);

        int lx = (x % macroSize + macroSize) % macroSize;
        int ly = (y % macroSize + macroSize) % macroSize;
        int lz = (z % macroSize + macroSize) % macroSize;

        int tipo = ObtenerTipoMacroChunk(mx, my, mz);

        // Misión encadenada: si esta celda es un destino registrado, spawnear la recompensa
        // (cofre + materiales + siguiente nota). GenerarYActivarCelda activa todo al final.
        if (misionesEncadenadasPorCelda.TryGetValue(celda, out NotaData notaCadena))
            SpawnRecompensaMision(celda, notaCadena, false);

        // POZO VERTICAL (tipo 6): objeto central por celda. El pozo talla lx==2 && lz==2 a TODAS
        // las alturas, así que esta es la única posición hueca del chunk y el objeto va justo ahí,
        // apoyado en el piso de su propia celda. Al apilarse los pozos, la columna queda continua.
        //
        // Sorteo POR CELDA (no por chunk): tanto si hay objeto como cuál es. Por eso una misma
        // columna puede mezclar prefabs y tener huecos — es a propósito.
        if (tipo == 6 && lx == 2 && lz == 2
            && prefabsCentroPozo != null && prefabsCentroPozo.Length > 0
            && probabilidadObjetoPozo > 0f)
        {
            int hashPozo = semillaDelMundo + x * 42043 + y * 15683 + z * 70237;
            Random.State estadoPozo = Random.state;
            Random.InitState(hashPozo);
            bool hayObjeto = Random.value < probabilidadObjetoPozo;
            GameObject prefabPozo = hayObjeto
                ? prefabsCentroPozo[Random.Range(0, prefabsCentroPozo.Length)]
                : null;

            // Giro SOLO en Y (nunca se inclinan). Se tira dentro del mismo bloque sembrado, así
            // que la rotación también es determinista por semilla.
            float yawPozo = 0f;
            if (prefabPozo != null)
            {
                if (pasoRotacionPozoGrados > 0f)
                {
                    int pasos = Mathf.Max(1, Mathf.RoundToInt(360f / pasoRotacionPozoGrados));
                    yawPozo = Random.Range(0, pasos) * pasoRotacionPozoGrados;
                }
                else
                {
                    yawPozo = Random.Range(0f, 360f);
                }
            }
            Random.state = estadoPozo;

            if (prefabPozo != null)
            {
                Vector3 posPozo = origin + new Vector3(
                    x * cellSize + (cellSize / 2f),
                    y * cellSize,
                    z * cellSize + (cellSize / 2f));
                GameObject goPozo = Instantiate(prefabPozo, posPozo, Quaternion.Euler(0f, yawPozo, 0f), transform);
                goPozo.SetActive(false);
                RegistrarEnCelda(celda, goPozo);
            }
        }

        if (tipo == 1 && lx == 2 && ly == 1 && lz == 2)
        {
            RoomData sala = ObtenerRoomDataParaMacro(mx, my, mz);

            // Decidir qué estructura central poner (si la sala tiene prefabs centrales asignados, se usa uno; si no, y no hay sala, cae en las escaleras por defecto)
            GameObject goCentro = null;
            if (sala != null)
            {
                if (sala.prefabsCentrales != null && sala.prefabsCentrales.Length > 0)
                {
                    int hashCentro = semillaDelMundo + mx * 91827 + my * 12453 + mz * 36251;
                    Random.State estadoViejo = Random.state;
                    Random.InitState(hashCentro);
                    GameObject prefabCentro = sala.prefabsCentrales[Random.Range(0, sala.prefabsCentrales.Length)];
                    Random.state = estadoViejo;

                    if (prefabCentro != null)
                    {
                        Vector3 posCentro = origin + new Vector3(x * cellSize + (cellSize / 2f), y * cellSize, z * cellSize + (cellSize / 2f));
                        goCentro = Instantiate(prefabCentro, posCentro, Quaternion.identity, transform);
                        ProcesarPuntosSpawnItem(goCentro, celda); // mismo Random.State ya sembrado arriba (hashCentro)
                        goCentro.SetActive(false);
                        RegistrarEnCelda(celda, goCentro);
                    }
                }
                else
                {
                    Debug.LogWarning($"[WorldGenerator] Sala '{sala.nombreSala}' sin prefabsCentrales. Celda central queda vacía.");
                }
            }
            else if (prefabsEscaleras != null && prefabsEscaleras.Length > 0)
            {
                // Posicionar en el centro de la celda (escalera por defecto) solo si sala == null
                Vector3 posEscalera = origin + new Vector3(x * cellSize + (cellSize / 2f), y * cellSize, z * cellSize + (cellSize / 2f));
                GameObject prefabEscalera = prefabsEscaleras[Random.Range(0, prefabsEscaleras.Length)];
                GameObject go = Instantiate(prefabEscalera, posEscalera, Quaternion.identity, transform);
                go.SetActive(false);
                RegistrarEnCelda(celda, go);
            }

            // Carteles con el nombre de la sala en la celda central. Con cartelSalaSoloAlNorte
            // va UNO solo, en la cara norte: deja de ser decoración repetida y pasa a servir de
            // brújula — si lo ves, estás mirando al norte. Norte = +Z (Vector3.forward), misma
            // convención que DireccionCardinal, así concuerda con las pistas de los NPC.
            if (sala != null && prefabCartelCoordenadas != null && !string.IsNullOrEmpty(sala.nombreSala))
            {
                Vector3 cellCenter = origin + new Vector3(x * cellSize + (cellSize / 2f), y * cellSize + (cellSize / 2f), z * cellSize + (cellSize / 2f));
                Vector3[] direcciones = cartelSalaSoloAlNorte
                    ? new[] { Vector3.forward }
                    : new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
                foreach (Vector3 dir in direcciones)
                {
                    Vector3 posSign = cellCenter + dir * (cellSize * 0.5f - 0.1f);
                    posSign.y = origin.y + y * cellSize + (cellSize * 0.75f);

                    Quaternion rotSign = RotacionDesdeNormalInterior(dir) * Quaternion.Euler(0, 180f, 0);

                    GameObject goSign = Instantiate(prefabCartelCoordenadas, posSign, rotSign, transform);

                    // Destruir el script de coordenadas ANTES de desactivar para que no sobreescriba el texto al activarse luego
                    CartelCoordenadas comp = goSign.GetComponent<CartelCoordenadas>();
                    if (comp != null)
                    {
                        comp.enabled = false;
                        DestroyImmediate(comp);
                    }

                    // Asignar el texto del nombre de la sala (mientras el GO sigue activo, así GetComponentInChildren lo encuentra)
                    TMP_Text textMesh = goSign.GetComponentInChildren<TMP_Text>(true);
                    if (textMesh != null)
                    {
                        textMesh.text = sala.nombreSala.ToUpper();
                        textMesh.ForceMeshUpdate();
                    }
                    else
                    {
                        Debug.LogWarning($"[WorldGenerator] El prefab del cartel '{prefabCartelCoordenadas.name}' no tiene un componente TMP_Text. No se pudo asignar '{sala.nombreSala}'.");
                    }

                    goSign.SetActive(false); // La burbuja lo activará cuando corresponda
                    RegistrarEnCelda(celda, goSign);
                }
            }

            // Sección de ALMACENAMIENTO en la celda central (opcional, convive con el prefab central).
            if (sala != null && sala.seccionAlmacenamiento != null)
                InstanciarSeccion(sala.seccionAlmacenamiento, celda, origin, x, y, z);

            // Loot de la sala: se elige y genera una sola vez por Macro Chunk, aprovechando
            // que este bloque solo se ejecuta en la celda central (lx==2, ly==1, lz==2).
            GenerarLootDeSala(mx, my, mz, celda, origin, x, y, z, goCentro);

            // Comprobar si esta celda tiene una misión/tesoro asociado
            if (misionesPorCelda.TryGetValue(celda, out NotaData notaMision))
            {
                // Instanciar el cofre de la misión en el centro de la sala
                Vector3 posCofre = origin + new Vector3(
                    x * cellSize + (cellSize / 2f),
                    y * cellSize + 0.1f, // Justo sobre el suelo
                    z * cellSize + (cellSize / 2f)
                );
                
                // Si la sala tiene spawn points, podemos poner el cofre en uno de ellos
                if (goCentro != null)
                {
                    Transform[] childs = goCentro.GetComponentsInChildren<Transform>(true);
                    foreach (Transform child in childs)
                    {
                        if (child != null && (child.name.ToLower().Contains("treasure") || child.name.ToLower().Contains("spawnpoint")))
                        {
                            posCofre = child.position;
                            break;
                        }
                    }
                }

                GameObject cofreInstance = Instantiate(notaMision.prefabCofre, posCofre, Quaternion.identity, transform);
                cofreInstance.SetActive(false);
                RegistrarEnCelda(celda, cofreInstance);
            }
        }

        // Secciones de vivienda: SOLO las 4 esquinas del 3x3 (lx/lz ∈ {1,3}). Las celdas del
        // medio de cada lado (2,1)/(1,2)/(3,2)/(2,3) son las que dan a las puertas → se saltean.
        if (tipo == 1 && ly == 1 && (lx == 1 || lx == 3) && (lz == 1 || lz == 3))
        {
            IntentarGenerarSeccionVivienda(mx, my, mz, celda, origin, x, y, z, lx, lz);
        }

        // Luces de Sala: para cualquier celda del 3x3 del piso de la Sala Grande, EXCEPTO la central.
        // La configuración (prefabs, cantidad, offset) vive en la RoomData asignada a este macro chunk.
        if (tipo == 1 && ly == 1
            && lx >= 1 && lx <= 3 && lz >= 1 && lz <= 3
            && !(lx == 2 && lz == 2))
        {
            RoomData salaLuces = ObtenerRoomDataParaMacro(mx, my, mz);
            if (salaLuces != null && salaLuces.generarLuces
                && salaLuces.prefabsLuces != null && salaLuces.prefabsLuces.Length > 0)
            {
                GenerarLucesTechoSala(celda, x, y, z, origin, salaLuces);
            }
        }

        bool celdaActualVacia = IsCellEmpty(x, y, z);

        // 1. PILAR GIGANTE (esquinas de Sala Grande / tipo 7 — característica visual de ese tipo).
        // En el Abismo verdadero (tipo 9) no se generan: el abismo es void puro.
        // En la Sala Colosal (10) forman columnas continuas cada 100m que le dan escala al espacio,
        // pero son muchos objetos a la vez: se pueden apagar con pilaresEnSalaColosal.
        if (celdaActualVacia && lx == 0 && lz == 0 && tipo != 9 && (tipo != 10 || pilaresEnSalaColosal))
        {
            if (prefabsPilaresGigantes != null && prefabsPilaresGigantes.Length > 0)
            {
                // Instanciar en el centro de la celda
                Vector3 posPilar = origin + new Vector3(x * cellSize + (cellSize / 2f), y * cellSize, z * cellSize + (cellSize / 2f));
                GameObject prefabPilar = prefabsPilaresGigantes[Random.Range(0, prefabsPilaresGigantes.Length)];
                GameObject go = Instantiate(prefabPilar, posPilar, Quaternion.identity, transform);
                go.SetActive(false);
                RegistrarEnCelda(celda, go);
            }
        }

        // REGLA: Cada celda SOLO se encarga de crear su pared Izquierda, su pared Atrás y su Piso.
        // Las paredes Derecha, Adelante y Techo se crearán por las celdas vecinas. Esto evita que se dupliquen paredes.

        // 1. PARED X (Izquierda)
        bool celdaIzquierdaVacia = IsCellEmpty(x - 1, y, z);
        // Modo islas: caras al VOID no generan pared alguna (sin paredes contenedoras)
        if (celdaActualVacia != celdaIzquierdaVacia && !CaraDaAlVacio(celdaActualVacia, x, y, z, x - 1, y, z))
        {
            // Saltear esta pared (cara X-) solo si un balcón abre justo hacia ella:
            //  - esta celda vacía con balcón abriendo hacia X- (izquierda), o
            //  - la celda izquierda vacía con balcón abriendo hacia X+ (hacia esta celda)
            bool paredXSalteadaPorBalcon =
                DireccionBalcon(x, y, z) == new Vector3Int(-1, 0, 0) ||
                DireccionBalcon(x - 1, y, z) == new Vector3Int(1, 0, 0);

            // Cara externa del casco de una Sala Colosal: interior y exterior van al revés.
            bool cascoInvertidoX = CaraDeCascoColosalInvertida(celdaActualVacia, x, y, z, x - 1, y, z);

            if (prefabsPared.Length > 0 && !paredXSalteadaPorBalcon)
            {
                Vector3 pos = origin + new Vector3(x * cellSize, y * cellSize + (cellSize / 2f), z * cellSize + (cellSize / 2f));

                GameObject prefab = prefabsPared[Random.Range(0, prefabsPared.Length)];
                // La cara decorada mira hacia el interior (lado vacío) usando la normal de la cara.
                Vector3 normalInterior = celdaActualVacia ? Vector3.right : Vector3.left;
                if (cascoInvertidoX) normalInterior = -normalInterior;
                GameObject go = Instantiate(prefab, pos, RotacionDesdeNormalInterior(normalInterior) * Quaternion.Euler(0, 180f, 0), transform);
                ProcesarPuntosSpawnItemDePared(go, celda, pos, normalInterior);
                go.SetActive(false); // Estará apagado hasta que lo active la Burbuja
                RegistrarEnCelda(celda, go);

                // Intentar colocar cartel de coordenadas en esta pared
                IntentarInstanciarCartelCoordenadas(pos, normalInterior, celda);
            }

            if (!paredXSalteadaPorBalcon)
            {
                Vector3 posBase = origin + new Vector3(x * cellSize, y * cellSize + (cellSize / 2f), z * cellSize + (cellSize / 2f));
                Vector3 dirExterior = celdaActualVacia ? Vector3.left : Vector3.right;
                if (cascoInvertidoX) dirExterior = -dirExterior;
                InstanciarParedExterior(celda, posBase, dirExterior, x, y, z, 0, tipo,
                                        cascoInvertidoX && cascoColosalSiempreConPielExterior);
            }

            // Aristas continuas en pasillo Z (cara X) — sobre la pared exterior
            // Aristas en la cara X: cualquier pasillo interactuable (2=Z, 3=X, 4=L, 5=+, 8=T) puede tener arista exterior en esta cara
            if (generarAristasExteriores && !paredXSalteadaPorBalcon && celdaActualVacia
                && (tipo == 1 || tipo == 2 || tipo == 3 || tipo == 4 || tipo == 5 || tipo == 8)
                && !(tipo == 1 && ly == 2) // Sala Chica: NO aristas en ly=2 (columna central que une los pisos)
                && Random.value <= probabilidadAristaExterior)
            {
                // Arista izquierda (X-): la masa sólida está en (x-1,y,z). La pared corre derecha
                // solo si esa masa SE EXTIENDE a (x-1,y,z-1) y (x-1,y,z+1). Si alguno está vacío,
                // la pared dobla en ese extremo (esquina cóncava) y se salta la arista.
                if (!IsCellEmpty(x - 1, y, z)
                    && !IsCellEmpty(x - 1, y, z - 1) && !IsCellEmpty(x - 1, y, z + 1))
                {
                    Vector3 posBaseIzq = origin + new Vector3(x * cellSize, y * cellSize + (cellSize / 2f), z * cellSize + (cellSize / 2f));
                    posBaseIzq += Vector3.left * paredExteriorOffset;
                    InstanciarAristasParaParedExterior(celda, posBaseIzq, Vector3.left, x, y, z, forzar: true);
                }

                // Arista derecha (X+): mismo principio en el lado opuesto.
                if (!IsCellEmpty(x + 1, y, z)
                    && !IsCellEmpty(x + 1, y, z - 1) && !IsCellEmpty(x + 1, y, z + 1))
                {
                    Vector3 posBaseDer = origin + new Vector3((x + 1) * cellSize, y * cellSize + (cellSize / 2f), z * cellSize + (cellSize / 2f));
                    posBaseDer += Vector3.right * paredExteriorOffset;
                    InstanciarAristasParaParedExterior(celda, posBaseDer, Vector3.right, x, y, z, forzar: true);
                }
            }
        }

        // 2. PARED Z (Atrás)
        bool celdaAtrasVacia = IsCellEmpty(x, y, z - 1);

        // Caras al VOID no generan pared alguna (sin paredes contenedoras)
        if (celdaActualVacia != celdaAtrasVacia && !CaraDaAlVacio(celdaActualVacia, x, y, z, x, y, z - 1))
        {
            // Saltear esta pared (cara Z-) solo si un balcón abre justo hacia ella
            bool paredZSalteadaPorBalcon =
                DireccionBalcon(x, y, z) == new Vector3Int(0, 0, -1) ||
                DireccionBalcon(x, y, z - 1) == new Vector3Int(0, 0, 1) ||
                (tipo == 5 && (lz == 2 || lz == 3) && (lx == 1 || lx == 3));

            // Cara externa del casco de una Sala Colosal: interior y exterior van al revés.
            bool cascoInvertidoZ = CaraDeCascoColosalInvertida(celdaActualVacia, x, y, z, x, y, z - 1);

            if (prefabsPared.Length > 0 && !paredZSalteadaPorBalcon)
            {
                Vector3 pos = origin + new Vector3(x * cellSize + (cellSize / 2f), y * cellSize + (cellSize / 2f), z * cellSize);

                GameObject prefab = prefabsPared[Random.Range(0, prefabsPared.Length)];
                // La cara decorada mira hacia el lado vacío.
                Vector3 normalInterior = celdaActualVacia ? Vector3.forward : Vector3.back;
                if (cascoInvertidoZ) normalInterior = -normalInterior;
                GameObject go = Instantiate(prefab, pos, RotacionDesdeNormalInterior(normalInterior) * Quaternion.Euler(0, 180f, 0), transform);
                ProcesarPuntosSpawnItemDePared(go, celda, pos, normalInterior);
                go.SetActive(false);
                RegistrarEnCelda(celda, go);

                // Intentar colocar cartel de coordenadas en esta pared
                IntentarInstanciarCartelCoordenadas(pos, normalInterior, celda);
            }

            if (!paredZSalteadaPorBalcon)
            {
                Vector3 posBase = origin + new Vector3(x * cellSize + (cellSize / 2f), y * cellSize + (cellSize / 2f), z * cellSize);
                Vector3 dirExterior = celdaActualVacia ? Vector3.back : Vector3.forward;
                if (cascoInvertidoZ) dirExterior = -dirExterior;
                InstanciarParedExterior(celda, posBase, dirExterior, x, y, z, 1, tipo,
                                        cascoInvertidoZ && cascoColosalSiempreConPielExterior);
            }

            // Aristas continuas en pasillo X (cara Z) — sobre la pared exterior
            // Aristas en la cara Z: cualquier pasillo interactuable puede tener arista exterior
            if (generarAristasExteriores && !paredZSalteadaPorBalcon && celdaActualVacia
                && (tipo == 1 || tipo == 2 || tipo == 3 || tipo == 4 || tipo == 5 || tipo == 8)
                && !(tipo == 1 && ly == 2) // Sala Chica: NO aristas en ly=2 (columna central que une los pisos)
                && Random.value <= probabilidadAristaExterior)
            {
                // Arista atrás (Z-): la pared corre derecha solo si la masa sólida en (x,y,z-1)
                // se extiende a (x-1,y,z-1) y (x+1,y,z-1). Si alguno está vacío, esquina cóncava → skip.
                if (!IsCellEmpty(x, y, z - 1)
                    && !IsCellEmpty(x - 1, y, z - 1) && !IsCellEmpty(x + 1, y, z - 1))
                {
                    Vector3 posBaseAtras = origin + new Vector3(x * cellSize + (cellSize / 2f), y * cellSize + (cellSize / 2f), z * cellSize);
                    posBaseAtras += Vector3.back * paredExteriorOffset;
                    InstanciarAristasParaParedExterior(celda, posBaseAtras, Vector3.back, x, y, z, forzar: true);
                }

                // Arista adelante (Z+): mismo principio.
                if (!IsCellEmpty(x, y, z + 1)
                    && !IsCellEmpty(x - 1, y, z + 1) && !IsCellEmpty(x + 1, y, z + 1))
                {
                    Vector3 posBaseAdelante = origin + new Vector3(x * cellSize + (cellSize / 2f), y * cellSize + (cellSize / 2f), (z + 1) * cellSize);
                    posBaseAdelante += Vector3.forward * paredExteriorOffset;
                    InstanciarAristasParaParedExterior(celda, posBaseAdelante, Vector3.forward, x, y, z, forzar: true);
                }
            }
        }

        // 3. PISO Y (Abajo)
        bool celdaAbajoVacia = IsCellEmpty(x, y - 1, z);

        // Modo islas: caras al VOID tampoco generan piso/techo (ni interior ni exterior) —
        // solo las estructuras llevan sus propias superficies, NADA se genera hacia el vacío.
        if (celdaActualVacia != celdaAbajoVacia && !CaraDaAlVacio(celdaActualVacia, x, y, z, x, y - 1, z))
        {
            // Cara externa del casco de una Sala Colosal (panza o coronación): igual que en las
            // paredes, acá el lado vacío es el VACÍO DEL MUNDO, no un interior — se invierte a qué
            // lado mira la cara decorada y de qué lado va la piel exterior.
            bool cascoInvertidoY = CaraDeCascoColosalInvertida(celdaActualVacia, x, y, z, x, y - 1, z);

            if (prefabsPiso.Length > 0)
            {
                Vector3 pos = origin + new Vector3(x * cellSize + (cellSize / 2f), y * cellSize, z * cellSize + (cellSize / 2f));
                GameObject prefab = prefabsPiso[Random.Range(0, prefabsPiso.Length)];
                float rotY = Random.Range(0, 4) * 90f;
                Quaternion rot = Quaternion.Euler(0, rotY, 0);

                // Si la celda actual es sólida y la de abajo es vacía, actúa como techo
                bool caraDecoradaAbajo = !celdaActualVacia && celdaAbajoVacia;
                if (cascoInvertidoY) caraDecoradaAbajo = !caraDecoradaAbajo;
                if (caraDecoradaAbajo)
                {
                    // Rotar 180 grados en el eje X local para que la cara decorada mire hacia abajo (el pasillo)
                    rot = rot * Quaternion.Euler(180f, 0f, 0f);
                }

                GameObject go = Instantiate(prefab, pos, rot, transform);
                go.SetActive(false);
                RegistrarEnCelda(celda, go);
            }

            Vector3 posBasePiel = origin + new Vector3(x * cellSize + (cellSize / 2f), y * cellSize, z * cellSize + (cellSize / 2f));
            if (cascoInvertidoY)
            {
                // El vacío está de un solo lado: si cae ABAJO esta cara es la PANZA del casco
                // (piel de piso exterior, que se desplaza hacia abajo); si cae arriba es la
                // CORONACIÓN (piel de techo exterior, que se desplaza hacia arriba). En ambos
                // casos el offset de la piel sale solo hacia afuera.
                if (celdaAbajoVacia)
                    InstanciarPisoExterior(celda, posBasePiel, x, y, z, cascoColosalSiempreConPielExterior);
                else
                    InstanciarTechoExterior(celda, posBasePiel, x, y, z, cascoColosalSiempreConPielExterior);
            }
            else if (celdaActualVacia)
            {
                InstanciarPisoExterior(celda, posBasePiel, x, y, z);
            }
        }

        // 3b. TECHO EXTERIOR: cuando esta celda es pasillo (vacía) y la de arriba es sólida (hay techo encima),
        // generar la cara exterior del techo (visible desde el cielo / por encima de la mega-estructura).
        // Modo islas: si esta celda vacía es VOID (tipo 9), nada — el vacío no recibe superficies.
        // (Si esta cara es la PANZA de un casco colosal, el bloque de arriba ya le puso su piel
        // mirando al vacío — sin esta guarda se duplicaría la geometría en el mismo plano.)
        if (celdaActualVacia && !IsCellEmpty(x, y + 1, z)
            && !CaraDaAlVacio(true, x, y, z, x, y + 1, z)
            && !CaraDeCascoColosalInvertida(true, x, y, z, x, y + 1, z))
        {
            Vector3 posBase = origin + new Vector3(x * cellSize + (cellSize / 2f), (y + 1) * cellSize, z * cellSize + (cellSize / 2f));
            InstanciarTechoExterior(celda, posBase, x, y, z);
        }

        // 3.5. BALCÓN: si esta celda es un balcón, spawnear el prefab en el BORDE de la celda
        // hacia la cara abierta (donde se quitó la pared exterior), orientado hacia afuera.
        Vector3Int dirBalcon = DireccionBalcon(x, y, z);
        if (celdaActualVacia && dirBalcon != Vector3Int.zero)
        {
            Vector3 dirF = new Vector3(dirBalcon.x, 0f, dirBalcon.z);
            if (prefabBalcon != null)
            {
                Vector3 posBalcon = origin + new Vector3(
                    x * cellSize + (cellSize / 2f),
                    y * cellSize,
                    z * cellSize + (cellSize / 2f))
                    + dirF * (cellSize * 0.5f + balconOffset); // empujado al borde (+ ajuste fino hacia la pared exterior)
                Quaternion rotBalcon = Quaternion.LookRotation(dirF, Vector3.up);
                GameObject goBalcon = Instantiate(prefabBalcon, posBalcon, rotBalcon, transform);
                goBalcon.SetActive(false);
                RegistrarEnCelda(celda, goBalcon);
            }

        }

        // 4. LUCES DE TECHO EN PASILLOS
        if (generarLucesTecho)
        {
            IntentarGenerarTuberia(celda, x, y, z, tipo, lx, ly, lz, origin);
        }

        // 5. PASARELAS INDUSTRIALES EN PAREDES DE PASILLOS (probabilidadPasarela = 0 las apaga)
        IntentarGenerarPasarela(celda, x, y, z, tipo, lx, ly, lz, origin);

        // 5.5 OBJETOS DE BORDE (props a nivel de piso contra las paredes; array vacío = apagado)
        IntentarGenerarObjetosBorde(celda, x, y, z, origin);

        // 5.6 TUBERÍAS MODULARES (kit por conectores; probabilidadTuberia = 0 las apaga)
        IntentarGenerarTuberiaModular(celda, x, y, z, origin);

        // 6. CIERRES DE PASILLOS (puertas en cambios de sección; probabilidadCierre = 0 las apaga)
        IntentarGenerarCierrePasillo(celda, x, y, z, tipo, lx, ly, lz, origin);

        // 6.5 MARCOS EN TÉRMINOS DE PASILLO (boca del tubo abierta hacia Sala Grande / Abismo)
        if (generarAristasExteriores)
        {
            IntentarGenerarMarcoTerminoPasillo(celda, x, y, z, origin);
        }

        // 7. ARISTAS VERTICALES EN ESQUINAS DE SALA (arquitectura del mundo, vive en el WorldGenerator)
        if (generarAristasVerticales) IntentarGenerarAristasVerticales(celda, x, y, z, ly, origin);

        // 8. VÉRTICES CONVEXOS EN ESQUINAS EXTERIORES (cierra el espacio con las aristas horizontales)
        IntentarGenerarVerticesConvexos(celda, x, y, z, origin);
    }


    /// <summary>
    /// Genera aristas verticales en las 4 esquinas exteriores de cada Sala (tipo 1).
    /// Es parte de la arquitectura del mundo: la config (prefabs, niveles, offsets, yaw) está
    /// en el WorldGenerator (no en cada RoomData). Las salas no eligen sus aristas.
    /// </summary>
    private void IntentarGenerarAristasVerticales(Vector3Int celda, int x, int y, int z, int ly, Vector3 origin)
    {
        if (prefabsAristaExteriorVertical == null || prefabsAristaExteriorVertical.Length == 0) return;

        int mx = Mathf.FloorToInt((float)x / macroSize);
        int my = Mathf.FloorToInt((float)y / macroSize);
        int mz = Mathf.FloorToInt((float)z / macroSize);
        int tipoChunk = ObtenerTipoMacroChunk(mx, my, mz);

        // Tipo 1 (Sala Chica): aristas solo en niveles configurados (default ly=1,3).
        // Tipo 6 (Pozo Vertical): aristas en TODOS los ly del macro chunk (cubre toda la columna).
        if (tipoChunk == 1)
        {
            bool lyHabilitado = false;
            if (nivelesAristasVerticales != null)
            {
                for (int i = 0; i < nivelesAristasVerticales.Length; i++)
                    if (nivelesAristasVerticales[i] == ly) { lyHabilitado = true; break; }
            }
            if (!lyHabilitado) return;
        }
        else if (tipoChunk == 6)
        {
            // Pozo: todos los ly válidos (la columna se extiende toda la altura del macro chunk).
        }
        else
        {
            return; // otros tipos no generan aristas verticales
        }

        int lx = (x % macroSize + macroSize) % macroSize;
        int lz = (z % macroSize + macroSize) % macroSize;

        bool xExtremo = (lx == 0 || lx == macroSize - 1);
        bool zExtremo = (lz == 0 || lz == macroSize - 1);
        if (!xExtremo || !zExtremo) return;

        int dx = (lx == 0) ? -1 : 1;
        int dz = (lz == 0) ? -1 : 1;

        // Omitir aristas verticales en todo el nivel 'y' del pozo vertical (tipo 6) si hay 
        // una entrada de pasillo transitable (celda vacía) en cualquiera de las 4 direcciones cardinales.
        if (tipoChunk == 6)
        {
            int px = mx * macroSize + 2;
            int pz = mz * macroSize + 2;
            if (IsCellEmpty(px - 1, y, pz) || IsCellEmpty(px + 1, y, pz) ||
                IsCellEmpty(px, y, pz - 1) || IsCellEmpty(px, y, pz + 1))
            {
                return;
            }
        }

        string nombre = (dz > 0 ? "N" : "S") + (dx > 0 ? "E" : "W");

        int hash = semillaDelMundo + mx * 67213 + my * 35817 + mz * 91547 + (dx + 1) * 13 + (dz + 1) * 17;
        Random.State prev = Random.state;
        Random.InitState(hash);
        GameObject prefab = prefabsAristaExteriorVertical[Random.Range(0, prefabsAristaExteriorVertical.Length)];
        Random.state = prev;
        if (prefab == null) return;

        // Distancia (en celdas) desde la esquina del macro hasta donde empieza la zona vacía interior.
        // Sala Chica (tipo 1): empieza a 1 celda (lx=1 es interior). Pozo Vertical (tipo 6): empieza
        // a 2 celdas (solo lx=2 es interior). Esto afecta donde se cruzan las paredes exteriores y
        // por ende donde debe ir la arista vertical.
        int celdasAInterior = (tipoChunk == 6) ? 2 : 1;
        float dxInterior = (dx > 0) ? (x + 1 - celdasAInterior) : (x + celdasAInterior);
        float dzInterior = (dz > 0) ? (z + 1 - celdasAInterior) : (z + celdasAInterior);
        float bordeInteriorX = origin.x + dxInterior * cellSize;
        float bordeInteriorZ = origin.z + dzInterior * cellSize;
        float posX = bordeInteriorX + dx * paredExteriorOffset;
        float posZ = bordeInteriorZ + dz * paredExteriorOffset;
        float posY = origin.y + y * cellSize + offsetYAristaVertical;

        Vector3 outward = new Vector3(dx, 0, dz).normalized;
        Vector3 pos = new Vector3(posX, posY, posZ) + outward * offsetAristaVertical;

        float yawEsquina;
        if      (dx == -1 && dz == -1) yawEsquina = -90f;
        else if (dx == -1 && dz ==  1) yawEsquina =   0f;
        else if (dx ==  1 && dz ==  1) yawEsquina =  90f;
        else                            yawEsquina = 180f;
        Quaternion rot = Quaternion.Euler(0f, yawEsquina + yawBaseAristaVertical, 0f);

        GameObject go = Instantiate(prefab, pos, rot, transform);
        go.name = prefab.name + "_vert_sala_" + nombre;
        go.SetActive(false);
        RegistrarEnCelda(celda, go);
    }

    /// <summary>
    /// Marco para la BOCA de un pasillo que termina abriéndose a una Sala Grande (7) o Abismo (9):
    /// el corte del tubo quedaba sin trim. Coloca arista de techo, de piso y las dos aristas
    /// verticales en los cantos laterales, reusando los mismos prefabs y convenciones (yaw por
    /// esquina, offsets) del resto del sistema de aristas exteriores.
    /// </summary>
    private void IntentarGenerarMarcoTerminoPasillo(Vector3Int celda, int x, int y, int z, Vector3 origin)
    {
        if (!EsPasilloInteractuable(x, y, z)) return;

        Vector3Int[] dirsLat =
        {
            new Vector3Int( 1, 0,  0), new Vector3Int(-1, 0,  0),
            new Vector3Int( 0, 0,  1), new Vector3Int( 0, 0, -1),
        };
        float hs = cellSize * 0.5f;
        Vector3 cellCenter = origin + new Vector3(x * cellSize + hs, y * cellSize + hs, z * cellSize + hs);

        foreach (var d in dirsLat)
        {
            int nx = x + d.x, nz = z + d.z;
            if (!IsCellEmpty(nx, y, nz)) continue;         // hay pared → no es boca
            if (!EsSalaGrandeOAbismo(nx, y, nz)) continue; // el pasillo continúa → no es término

            Vector3 dirF = new Vector3(d.x, 0f, d.z);
            Quaternion rot = Quaternion.LookRotation(dirF, Vector3.up);
            // La boca se trata como si hubiera una pared exterior en esa cara: mismo offset de piel.
            Vector3 mouthCenter = cellCenter + dirF * (hs + paredExteriorOffset);

            int hash = semillaDelMundo + x * 52361 + y * 24683 + z * 71429 + (d.x * 3 + d.z) * 977;
            Random.State prev = Random.state;
            Random.InitState(hash);

            // PUERTA EN BOCA AL VACÍO: si la boca abre al Abismo (9) y hay prefabs asignados,
            // instalar una compuerta en el plano de la cara — salir del casco es deliberado.
            // (Las bocas a Sala Grande (7) quedan abiertas: son atrios interiores.)
            if (prefabsPuertaBocaVacio != null && prefabsPuertaBocaVacio.Length > 0)
            {
                int tmx = Mathf.FloorToInt((float)nx / macroSize);
                int tmy = Mathf.FloorToInt((float)y / macroSize);
                int tmz = Mathf.FloorToInt((float)nz / macroSize);
                if (ObtenerTipoMacroChunk(tmx, tmy, tmz) == 9)
                {
                    GameObject prefabPuerta = prefabsPuertaBocaVacio[Random.Range(0, prefabsPuertaBocaVacio.Length)];
                    if (prefabPuerta != null)
                    {
                        Vector3 posPuerta = cellCenter + dirF * hs;
                        GameObject puerta = Instantiate(prefabPuerta, posPuerta, rot, transform);
                        puerta.SetActive(false);
                        RegistrarEnCelda(celda, puerta);
                    }
                }
            }

            // Aristas horizontales del marco (mismas piezas que el trim de paredes exteriores)
            InstanciarArista(celda, prefabsAristaExteriorTecho, mouthCenter + Vector3.up * hs, rot, "_termino_techo");
            InstanciarArista(celda, prefabsAristaExteriorPiso,  mouthCenter - Vector3.up * hs, rot, "_termino_piso");

            // Aristas verticales en los dos cantos laterales de la boca. Misma convención de yaw
            // que IntentarGenerarAristasVerticales (prefab modelado para esquina NW a yaw 0).
            if (prefabsAristaExteriorVertical != null && prefabsAristaExteriorVertical.Length > 0)
            {
                Vector3 perpF = Vector3.Cross(Vector3.up, dirF); // eje paralelo a la boca
                for (int s = -1; s <= 1; s += 2)
                {
                    GameObject prefabVert = prefabsAristaExteriorVertical[Random.Range(0, prefabsAristaExteriorVertical.Length)];
                    if (prefabVert == null) continue;

                    Vector3 lateral = perpF * s;
                    // Normales exteriores del canto: dirF (boca) + lateral (piel del costado)
                    int dxC = Mathf.RoundToInt(dirF.x + lateral.x);
                    int dzC = Mathf.RoundToInt(dirF.z + lateral.z);
                    float yawEsquina;
                    if      (dxC == -1 && dzC == -1) yawEsquina = -90f;
                    else if (dxC == -1 && dzC ==  1) yawEsquina =   0f;
                    else if (dxC ==  1 && dzC ==  1) yawEsquina =  90f;
                    else                             yawEsquina = 180f;

                    Vector3 outward = new Vector3(dxC, 0f, dzC).normalized;
                    Vector3 posVert = cellCenter
                        + dirF * (hs + paredExteriorOffset)
                        + lateral * (hs + paredExteriorOffset)
                        + outward * offsetAristaVertical;
                    posVert.y = origin.y + y * cellSize + offsetYAristaVertical;

                    Quaternion rotVert = Quaternion.Euler(0f, yawEsquina + yawBaseAristaVertical, 0f);
                    GameObject goV = Instantiate(prefabVert, posVert, rotVert, transform);
                    goV.name = prefabVert.name + "_termino_vert";
                    goV.SetActive(false);
                    RegistrarEnCelda(celda, goV);
                }
            }

            Random.state = prev;
        }
    }


    private bool EsEsquinaConcava(int cx, int cy, int cz)
    {
        if (IsCellEmpty(cx, cy, cz)) return false;

        bool n = IsCellEmpty(cx, cy, cz + 1);
        bool s = IsCellEmpty(cx, cy, cz - 1);
        bool e = IsCellEmpty(cx + 1, cy, cz);
        bool w = IsCellEmpty(cx - 1, cy, cz);

        bool cornerNE = n && e;
        bool cornerES = e && s;
        bool cornerSW = s && w;
        bool cornerWN = w && n;

        return cornerNE || cornerES || cornerSW || cornerWN;
    }

    /// <summary>
    /// Para una pared End (esquina cóncava): devuelve hacia qué lado del eje paralelo a la cara
    /// continúa el TRAMO LARGO de la pared (+1/-1). 0 si no se puede determinar (masa aislada).
    /// Se usa para espejar la pared End (el hueco apunta a la esquina) y acotar sus pilares.
    /// </summary>
    private int SignoLadoLargoPared(int x, int y, int z, Vector3 dirExterior)
    {
        bool actualVacio = IsCellEmpty(x, y, z);
        int xs = x, zs = z;
        if (dirExterior == Vector3.left) xs = actualVacio ? x - 1 : x;
        else if (dirExterior == Vector3.right) xs = actualVacio ? x + 1 : x;
        else if (dirExterior == Vector3.back) zs = actualVacio ? z - 1 : z;
        else if (dirExterior == Vector3.forward) zs = actualVacio ? z + 1 : z;
        else return 0;

        // Eje paralelo a la cara (mismo que usa el reparto de pilares)
        Vector3 paralelo = Vector3.Cross(Vector3.up, dirExterior.normalized).normalized;
        Vector3Int pd = new Vector3Int(Mathf.RoundToInt(paralelo.x), 0, Mathf.RoundToInt(paralelo.z));
        bool vacioMas   = IsCellEmpty(xs + pd.x, y, zs + pd.z); // ¿la masa termina hacia +paralelo?
        bool vacioMenos = IsCellEmpty(xs - pd.x, y, zs - pd.z); // ¿termina hacia -paralelo?
        if (vacioMas && !vacioMenos) return -1; // esquina en +, tramo largo en -
        if (!vacioMas && vacioMenos) return 1;  // esquina en -, tramo largo en +
        return 0;
    }

    private bool EsParedAdyacenteEsquinaConcava(int x, int y, int z, Vector3 dirExterior)
    {
        // El guard "si es Sala Chica, nunca acortar" fue removido: en las celdas conectoras carved
        // (entradas de pasillo a sala) las paredes laterales SÍ son esquinas cóncavas y deben usar
        // el prefab End. La detección geométrica de EsEsquinaConcava funciona igual en sala que en pasillo.

        int x_solid = x;
        int z_solid = z;

        bool actualVacio = IsCellEmpty(x, y, z);

        if (dirExterior == Vector3.left)
        {
            x_solid = actualVacio ? x - 1 : x;
        }
        else if (dirExterior == Vector3.right)
        {
            x_solid = actualVacio ? x + 1 : x;
        }
        else if (dirExterior == Vector3.back)
        {
            z_solid = actualVacio ? z - 1 : z;
        }
        else if (dirExterior == Vector3.forward)
        {
            z_solid = actualVacio ? z + 1 : z;
        }
        else
        {
            return false;
        }

        return EsEsquinaConcava(x_solid, y, z_solid);
    }

    private bool EsCeldaSolidaParaVertices(int cx, int cy, int cz)
    {
        int mx = Mathf.FloorToInt((float)cx / macroSize);
        int my = Mathf.FloorToInt((float)cy / macroSize);
        int mz = Mathf.FloorToInt((float)cz / macroSize);

        int lcx = (cx % macroSize + macroSize) % macroSize;
        int lcy = (cy % macroSize + macroSize) % macroSize;
        int lcz = (cz % macroSize + macroSize) % macroSize;

        int tipo = ObtenerTipoMacroChunk(mx, my, mz);

        if (tipo == 1) // Sala Grande
        {
            // La sala ocupa lx = 1..3, lz = 1..3 de lcy = 1 y 3 (excluimos lcy = 2 porque no tiene paredes).
            // Consideramos estas celdas como la "estructura sólida" de la sala.
            if (lcy == 1 || lcy == 3)
            {
                return lcx >= 1 && lcx <= 3 && lcz >= 1 && lcz <= 3;
            }
            return false;
        }

        return !IsCellEmpty(cx, cy, cz);
    }


    private void IntentarGenerarVerticesConvexos(Vector3Int celda, int x, int y, int z, Vector3 origin)
    {
        if (!generarAristasExteriores) return;
        if (prefabsVerticeConvexo == null || prefabsVerticeConvexo.Length == 0) return;

        // Verificar las 4 celdas alrededor del vértice S-W de (x, z)
        bool s_SW = EsCeldaSolidaParaVertices(x - 1, y, z - 1);
        bool s_SE = EsCeldaSolidaParaVertices(x,     y, z - 1);
        bool s_NW = EsCeldaSolidaParaVertices(x - 1, y, z);
        bool s_NE = EsCeldaSolidaParaVertices(x,     y, z);

        int solidCount = (s_SW ? 1 : 0) + (s_SE ? 1 : 0) + (s_NW ? 1 : 0) + (s_NE ? 1 : 0);
        if (solidCount != 1) return;

        // Identificar la celda sólida y su dirección hacia afuera
        int sx = x, sz = z;
        int dx = -1, dz = -1;
        float yaw = 0f;

        if (s_NE)
        {
            sx = x; sz = z;
            dx = -1; dz = -1;
            yaw = 180f;

            // Las celdas vacías deben ser exteriores (no jugables)
            if (EsCeldaInteriorJugable(x - 1, y, z - 1) || 
                EsCeldaInteriorJugable(x,     y, z - 1) || 
                EsCeldaInteriorJugable(x - 1, y, z))
                return;
        }
        else if (s_SE)
        {
            sx = x; sz = z - 1;
            dx = -1; dz = 1;
            yaw = -90f;

            if (EsCeldaInteriorJugable(x - 1, y, z - 1) || 
                EsCeldaInteriorJugable(x,     y, z)     || 
                EsCeldaInteriorJugable(x - 1, y, z))
                return;
        }
        else if (s_SW)
        {
            sx = x - 1; sz = z - 1;
            dx = 1; dz = 1;
            yaw = 0f;

            if (EsCeldaInteriorJugable(x,     y, z)     || 
                EsCeldaInteriorJugable(x,     y, z - 1) || 
                EsCeldaInteriorJugable(x - 1, y, z))
                return;
        }
        else if (s_NW)
        {
            sx = x - 1; sz = z;
            dx = 1; dz = -1;
            yaw = 90f;

            if (EsCeldaInteriorJugable(x - 1, y, z - 1) || 
                EsCeldaInteriorJugable(x,     y, z - 1) || 
                EsCeldaInteriorJugable(x,     y, z))
                return;
        }

        // Verificar si la esquina continúa arriba o abajo
        // Una esquina continúa si ambos muros exteriores que se cruzan existen en el nivel vecino
        bool tieneParedXActual = EsCeldaSolidaParaVertices(sx, y, sz) != EsCeldaSolidaParaVertices(sx + dx, y, sz);
        bool tieneParedZActual = EsCeldaSolidaParaVertices(sx, y, sz) != EsCeldaSolidaParaVertices(sx, y, sz + dz);

        // Si por alguna razón no hay muros en este nivel, no tiene sentido poner esquina
        if (!tieneParedXActual || !tieneParedZActual) return;

        bool tieneParedXAbajo = EsCeldaSolidaParaVertices(sx, y - 1, sz) != EsCeldaSolidaParaVertices(sx + dx, y - 1, sz);
        bool tieneParedZAbajo = EsCeldaSolidaParaVertices(sx, y - 1, sz) != EsCeldaSolidaParaVertices(sx, y - 1, sz + dz);
        bool continuaAbajo = tieneParedXAbajo && tieneParedZAbajo;

        bool tieneParedXArriba = EsCeldaSolidaParaVertices(sx, y + 1, sz) != EsCeldaSolidaParaVertices(sx + dx, y + 1, sz);
        bool tieneParedZArriba = EsCeldaSolidaParaVertices(sx, y + 1, sz) != EsCeldaSolidaParaVertices(sx, y + 1, sz + dz);
        bool continuaArriba = tieneParedXArriba && tieneParedZArriba;

        float posX = origin.x + x * cellSize + dx * paredExteriorOffset;
        float posZ = origin.z + z * cellSize + dz * paredExteriorOffset;
        Quaternion rot = Quaternion.Euler(0f, yaw + yawBaseVerticeConvexo, 0f);

        // Generar en el piso (Y = y * cellSize)
        if (!continuaAbajo)
        {
            float posY = origin.y + y * cellSize;
            Vector3 pos = new Vector3(posX, posY, posZ);
            InstanciarVerticeConvexo(celda, pos, rot, x, y, z, dx, dz, true);
        }

        // Generar en el techo (Y = (y + 1) * cellSize)
        if (!continuaArriba)
        {
            float posY = origin.y + (y + 1) * cellSize;
            Vector3 pos = new Vector3(posX, posY, posZ);
            InstanciarVerticeConvexo(celda, pos, rot, x, y, z, dx, dz, false);
        }
    }

    private bool EsCeldaInteriorJugable(int cx, int cy, int cz)
    {
        int cmx = Mathf.FloorToInt((float)cx / macroSize);
        int cmy = Mathf.FloorToInt((float)cy / macroSize);
        int cmz = Mathf.FloorToInt((float)cz / macroSize);
        int ctipo = ObtenerTipoMacroChunk(cmx, cmy, cmz);
        return ctipo != 7 && IsCellEmpty(cx, cy, cz);
    }


    private void InstanciarVerticeConvexo(Vector3Int celda, Vector3 pos, Quaternion rot, int x, int y, int z, int dx, int dz, bool esPiso)
    {
        int hash = semillaDelMundo 
                   + x * 73856 
                   + y * 19349 
                   + z * 83492 
                   + (dx + 1) * 31 
                   + (dz + 1) * 37
                   + (esPiso ? 7 : 13);

        GameObject[] arrayPrefabs = (esPiso && prefabsVerticeConvexoPiso != null && prefabsVerticeConvexoPiso.Length > 0)
            ? prefabsVerticeConvexoPiso
            : prefabsVerticeConvexo;

        if (arrayPrefabs == null || arrayPrefabs.Length == 0) return;

        Random.State prev = Random.state;
        Random.InitState(hash);
        GameObject prefab = arrayPrefabs[Random.Range(0, arrayPrefabs.Length)];
        Random.state = prev;

        if (prefab == null) return;

        GameObject go = Instantiate(prefab, pos, rot, transform);
        go.name = prefab.name + (esPiso ? "_piso" : "_techo");
        go.SetActive(false);
        RegistrarEnCelda(celda, go);
    }



    private void IntentarGenerarTuberia(Vector3Int celda, int x, int y, int z, int tipo, int lx, int ly, int lz, Vector3 origin)
    {
        // Solo colocar en el nivel principal de pasillos (ly == 1)
        if (ly != 1) return;
        if (!EsPasilloInteractuable(x, y, z)) return;

        // No generar tuberías en finales de pasillo (0 o 1 conexiones vacías laterales)
        bool nN = IsCellEmpty(x, y, z + 1);
        bool nS = IsCellEmpty(x, y, z - 1);
        bool nE = IsCellEmpty(x + 1, y, z);
        bool nW = IsCellEmpty(x - 1, y, z);
        int totalConexiones = (nN ? 1 : 0) + (nS ? 1 : 0) + (nE ? 1 : 0) + (nW ? 1 : 0);
        if (totalConexiones <= 1) return;

        // Centro de la celda en espacio mundo
        Vector3 cellCenter = origin + new Vector3(
            x * cellSize + cellSize * 0.5f,
            y * cellSize + cellSize * 0.5f,
            z * cellSize + cellSize * 0.5f
        );

        // --- LUCES EN TECHO (en cualquier pasillo horizontal, fuera de salas/viviendas) ---
        bool esPasilloValido = EsPasilloInteractuable(x, y, z) && tipo != 9;
        if (esPasilloValido && generarLucesTecho && prefabsLucesTecho != null && prefabsLucesTecho.Length > 0)
        {
            int hashLuzTecho = semillaDelMundo + x * 37481 + y * 58321 + z * 19283;
            Random.State estadoViejo = Random.state;
            Random.InitState(hashLuzTecho);
            bool debeGenerarLuzTecho = Random.value <= probabilidadLuzTecho;
            Random.state = estadoViejo;

            if (debeGenerarLuzTecho)
            {
                // Seleccionar prefab aleatorio del array
                GameObject prefabLuzTecho = prefabsLucesTecho[Random.Range(0, prefabsLucesTecho.Length)];

                if (prefabLuzTecho != null)
                {
                    // Decidir si el pasillo en esta celda corre en Z o en X
                    bool spawnZ = false;
                    bool spawnX = false;

                    if (lx == 2 && lz != 2)
                    {
                        spawnZ = true;
                    }
                    else if (lz == 2 && lx != 2)
                    {
                        spawnX = true;
                    }
                    else if (lx == 2 && lz == 2)
                    {
                        // Centro de intersección: elegir según el tipo de pasillo
                        if (tipo == 3) // Pasillo X
                            spawnX = true;
                        else // Pasillo Z (tipo 2), L (tipo 4), T (tipo 8), + (tipo 5)
                            spawnZ = true;
                    }

                    if (spawnZ || spawnX)
                    {
                        // CARRILES: se piden los que NO estén reclamados por la tubería. Antes las
                        // dos luces iban fijas en ±luzTechoOffset y la tubería por el centro — con
                        // modelos anchos se tocaban, que es el bug que originó todo esto.
                        bool[] ocupados = CarrilesOcupadosTecho(x, y, z);
                        List<int> libres = new List<int>();
                        // Orden de preferencia: laterales primero (0 y 2), centro al final — así,
                        // mientras haya lugar a los costados, el par de luces queda simétrico como
                        // siempre y solo se mete al medio si no le queda otra.
                        foreach (int carril in new[] { 0, 2, 1 })
                            if (!ocupados[carril]) libres.Add(carril);

                        bool sentidoPositivo = Random.value > 0.5f; // 50/50 hacia qué lado mira la luz
                        float yawBase = spawnZ
                            ? (sentidoPositivo ? 0f : 180f)
                            : (sentidoPositivo ? 90f : -90f);
                        string sufijoEje = spawnZ ? "_Z" : "_X";

                        // Hasta 2 luces (el par de siempre), pero solo en carriles libres: si la
                        // tubería ocupó uno, sale una sola en vez de encimarse.
                        int cuantas = Mathf.Min(2, libres.Count);
                        for (int i = 0; i < cuantas; i++)
                        {
                            float offsetLateral = OffsetLateralCarril(libres[i]);

                            Vector3 posLuz = origin + new Vector3(
                                x * cellSize + (cellSize / 2f) + (spawnZ ? offsetLateral : 0f),
                                y * cellSize + cellSize - 0.1f,
                                z * cellSize + (cellSize / 2f) + (spawnZ ? 0f : offsetLateral)
                            );

                            GameObject luz = Instantiate(prefabLuzTecho, posLuz, Quaternion.Euler(0, yawBase, 0), transform);
                            luz.name = prefabLuzTecho.name + "_carril" + libres[i] + sufijoEje;
                            luz.SetActive(false);
                            RegistrarEnCelda(celda, luz);
                        }
                    }
                }
            }
        }
    }

    private Quaternion RotacionDesdeNormalInterior(Vector3 normalInterior)
    {
        return Quaternion.LookRotation(normalInterior.normalized, Vector3.up);
    }

    /// <summary>
    /// Predice si la celda (x,y,z) recibirá una pasarela y a qué altura LOCAL (0..cellSize), sin instanciarla.
    /// Replica la lógica de decisión de <see cref="IntentarGenerarPasarela"/> para que sistemas que corren
    /// antes (como los carteles) puedan evitar superponerse con la pasarela.
    /// </summary>
    private bool HabraPasarelaEn(int x, int y, int z, out float alturaLocal)
    {
        alturaLocal = 0f;
        if (!EsPasilloInteractuable(x, y, z)) return false;

        int mx = Mathf.FloorToInt((float)x / macroSize);
        int my = Mathf.FloorToInt((float)y / macroSize);
        int mz = Mathf.FloorToInt((float)z / macroSize);
        int tipo = ObtenerTipoMacroChunk(mx, my, mz);

        int hashPasarela = semillaDelMundo + x * 19283 + y * 48392 + z * 76234 + tipo * 12345;
        Random.State estadoViejo = Random.state;
        Random.InitState(hashPasarela);
        bool debeGenerar = Random.value <= probabilidadPasarela;
        Random.state = estadoViejo;
        if (!debeGenerar) return false;

        bool nN = IsCellEmpty(x, y, z + 1) && !EsSalaGrandeOAbismo(x, y, z + 1);
        bool nS = IsCellEmpty(x, y, z - 1) && !EsSalaGrandeOAbismo(x, y, z - 1);
        bool nE = IsCellEmpty(x + 1, y, z) && !EsSalaGrandeOAbismo(x + 1, y, z);
        bool nW = IsCellEmpty(x - 1, y, z) && !EsSalaGrandeOAbismo(x - 1, y, z);
        int totalConexiones = (nN ? 1 : 0) + (nS ? 1 : 0) + (nE ? 1 : 0) + (nW ? 1 : 0);
        if (totalConexiones <= 1) return false;

        // Mismo veto por cierres que IntentarGenerarPasarela. Faltaba acá, así que el predictor
        // decía "sí" en celdas donde la pasarela después NO se generaba.
        if (HabraCierreEn(x, y, z)
            || HabraCierreEn(x + 1, y, z) || HabraCierreEn(x - 1, y, z)
            || HabraCierreEn(x, y, z + 1) || HabraCierreEn(x, y, z - 1))
            return false;

        List<float> alturasDisponibles = new List<float> { cellSize * 0.25f, cellSize * 0.75f };
        alturasDisponibles.Add(cellSize * 0.5f); // altura media siempre disponible (sin tuberías/luces de pared que estorben)

        int hashAltura;
        if (tipo == 2)      hashAltura = semillaDelMundo + x * 91733 + y * 26417;
        else if (tipo == 3) hashAltura = semillaDelMundo + z * 91733 + y * 26417;
        else                hashAltura = semillaDelMundo
                                       + Mathf.FloorToInt((float)x / macroSize) * 91733
                                       + Mathf.FloorToInt((float)y / macroSize) * 26417
                                       + Mathf.FloorToInt((float)z / macroSize) * 53729;
        alturaLocal = alturasDisponibles[Mathf.Abs(hashAltura) % alturasDisponibles.Count];
        return true;
    }

    private void IntentarInstanciarCartelCoordenadas(Vector3 baseWallPos, Vector3 normalInterior, Vector3Int celda, bool forzar = false)
    {
        if (prefabCartelCoordenadas == null) return;

        // Determinar qué celda está en el lado vacío (interior) de la pared
        Vector3 emptyCellPos = baseWallPos + normalInterior * (cellSize * 0.5f);
        Vector3Int emptyCell = MundoACelda(emptyCellPos);

        // SOLO spawnear si la celda vacía/transitable es un pasillo interactuable (excluye salas grandes, pozos, abismos)
        if (!EsPasilloInteractuable(emptyCell.x, emptyCell.y, emptyCell.z)) return;

        // Evitar duplicados: si esta celda ya tiene un cartel de coordenadas, no crear otro
        if (YaTieneCartel(emptyCell)) return;

        // SEPARACIÓN MÍNIMA entre celdas distintas. YaTieneCartel solo cubre la MISMA celda, y la
        // probabilidad no garantiza distancia: dos celdas vecinas pueden sacar cara las dos y
        // quedan dos carteles pared con pared.
        //
        // Regla determinista y sin estado: el cartel solo se permite donde (x + z) cae en la fase
        // de la retícula. En un pasillo recto (X o Z fijos) eso da uno cada `separacion` celdas
        // exacto, y dos celdas vecinas nunca pueden calificar las dos.
        //
        // La fase sale de la semilla y es la MISMA en todo el mundo — a propósito. Si se derivara
        // por macro chunk, en los bordes entre chunks las fases distintas volverían a permitir dos
        // carteles pegados, que es justo lo que estamos arreglando.
        //
        // La Y no entra: dos carteles en la misma columna pero en pisos distintos están a un
        // cellSize de distancia vertical, que ya es separación de sobra.
        if (separacionCartelesCeldas > 1 && !forzar)
        {
            int sep = separacionCartelesCeldas;
            int fase = ((semillaDelMundo % sep) + sep) % sep;
            int resto = ((emptyCell.x + emptyCell.z) % sep + sep) % sep;
            if (resto != fase) return;
        }

        // Semilla determinista basada en la celda y la normal de la cara
        int seed = celda.x * 73856093 ^ celda.y * 19349663 ^ celda.z * 83492791 ^ Mathf.RoundToInt(normalInterior.x + normalInterior.z * 10f);
        Random.State prev = Random.state;
        Random.InitState(seed);

        float randValue = Random.value;
        Random.state = prev; // restaurar estado

        if (forzar || randValue <= probabilidadCartelCoordenadas)
        {
            // No colocar el cartel si esta celda de pasillo va a recibir una pasarela: quedaría
            // atravesado/debajo de ella. Se predice con el mismo hash que IntentarGenerarPasarela.
            // Los carteles forzados (Gran Muralla) se colocan igual porque van en la pared frontera.
            if (!forzar && HabraPasarelaEn(emptyCell.x, emptyCell.y, emptyCell.z, out _))
                return;

            // Altura: 75% de la pared (baseWallPos ya está en 50%, sumamos 25% más).
            Vector3 pos = baseWallPos + normalInterior * cartelCoordenadasOffset + Vector3.up * (cellSize * 0.25f);
            Quaternion rot = RotacionDesdeNormalInterior(normalInterior) * Quaternion.Euler(0, 180f, 0);

            GameObject go = Instantiate(prefabCartelCoordenadas, pos, rot, transform);

            // Inyectar la celda REAL (la transitable hacia la que mira el cartel): el cartel vive
            // en el plano de la cara — el límite exacto entre dos celdas — y si la derivara de su
            // posición, las caras X+/Z+ mostrarían la celda vecina (dos carteles del mismo pasillo
            // con números distintos).
            CartelCoordenadas cartel = go.GetComponentInChildren<CartelCoordenadas>(true);
            if (cartel != null)
            {
                cartel.usarCeldaForzada = true;
                cartel.celdaForzada = emptyCell;
            }

            go.SetActive(false); // La burbuja lo activará cuando corresponda
            RegistrarEnCelda(celda, go);
            
            // LOG DE DIAGNOSTICO DE CARTEL DE COORDENADAS
            int emx = Mathf.FloorToInt((float)emptyCell.x / macroSize);
            int emy = Mathf.FloorToInt((float)emptyCell.y / macroSize);
            int emz = Mathf.FloorToInt((float)emptyCell.z / macroSize);
        }
    }

    private bool YaTieneCartel(Vector3Int emptyCell)
    {
        // 5 celdas posibles que podrían tener el cartel físicamente en emptyCell:
        // emptyCell misma, y sus 4 vecinos laterales.
        Vector3Int[] posiblesDuas = new Vector3Int[]
        {
            emptyCell,
            emptyCell + new Vector3Int(1, 0, 0),
            emptyCell + new Vector3Int(-1, 0, 0),
            emptyCell + new Vector3Int(0, 0, 1),
            emptyCell + new Vector3Int(0, 0, -1)
        };

        foreach (Vector3Int cell in posiblesDuas)
        {
            if (mundoChunks.TryGetValue(cell, out List<GameObject> objs))
            {
                foreach (GameObject obj in objs)
                {
                    if (obj != null && obj.name.StartsWith(prefabCartelCoordenadas.name))
                    {
                        // Verificar si el cartel físicamente pertenece a la misma celda
                        Vector3Int cellOfObj = MundoACelda(obj.transform.position);
                        if (cellOfObj == emptyCell)
                        {
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    private float YawDesdeNormalInterior(Vector3 normalInterior)
    {
        normalInterior.Normalize();
        return Mathf.Atan2(normalInterior.x, normalInterior.z) * Mathf.Rad2Deg;
    }

    // Devuelve la dirección hacia la que abre el balcón de una celda (la cara cuya pila de muro se quita),
    // o Vector3Int.zero si la celda no es un balcón. Determinista por celda + semilla del mundo.
    // El balcón abre hacia UNA sola cara lateral que normalmente tendría pared (vecino sólido).
    /// <summary>
    /// ¿Es esta celda un tramo RECTO de pasillo? Exactamente 2 vecinos laterales abiertos
    /// y opuestos entre sí (corredor en X o en Z). Esquinas (vecinos perpendiculares),
    /// intersecciones (3-4 vecinos) y callejones (0-1) devuelven false.
    /// </summary>
    public bool EsTramoRectoDePasillo(int x, int y, int z, out Vector3Int ejeCorredor)
    {
        ejeCorredor = Vector3Int.zero;
        if (!EsPasilloInteractuable(x, y, z)) return false;

        bool nE = IsCellEmpty(x + 1, y, z);
        bool nW = IsCellEmpty(x - 1, y, z);
        bool nN = IsCellEmpty(x, y, z + 1);
        bool nS = IsCellEmpty(x, y, z - 1);
        int total = (nE ? 1 : 0) + (nW ? 1 : 0) + (nN ? 1 : 0) + (nS ? 1 : 0);
        if (total != 2) return false;

        if (nE && nW) { ejeCorredor = new Vector3Int(1, 0, 0); return true; } // corre en X
        if (nN && nS) { ejeCorredor = new Vector3Int(0, 0, 1); return true; } // corre en Z
        return false; // 2 vecinos perpendiculares = esquina
    }

    private Vector3Int DireccionBalcon(int x, int y, int z)
    {
        Vector3Int k = new Vector3Int(x, y, z);
        if (cacheDirBalcon.TryGetValue(k, out Vector3Int cached)) return cached;
        Vector3Int dir = CalcularDireccionBalcon(x, y, z);
        cacheDirBalcon[k] = dir;
        return dir;
    }

    private Vector3Int CalcularDireccionBalcon(int x, int y, int z)
    {
        if (!EsPasilloInteractuable(x, y, z)) return Vector3Int.zero;

        // Solo tramos rectos de pasillo, y NO pegados a una esquina/intersección:
        // la celda debe ser recta Y sus dos vecinas a lo largo del corredor también.
        // En una corrida de 5 celdas entre dos esquinas, solo la 2, 3 y 4 califican.
        if (!EsTramoRectoDePasillo(x, y, z, out Vector3Int eje)) return Vector3Int.zero;
        if (!EsTramoRectoDePasillo(x + eje.x, y, z + eje.z, out _)) return Vector3Int.zero;
        if (!EsTramoRectoDePasillo(x - eje.x, y, z - eje.z, out _)) return Vector3Int.zero;

        int hash = semillaDelMundo + x * 12347 + y * 56419 + z * 39127;
        Random.State estadoViejo = Random.state;
        Random.InitState(hash);

        bool esBalcon = Random.value <= probabilidadBalcon;
        if (!esBalcon)
        {
            Random.state = estadoViejo;
            return Vector3Int.zero;
        }

        // Candidatos: caras laterales que tienen pared (vecino sólido)
        Vector3Int[] dirs =
        {
            new Vector3Int( 1, 0,  0),
            new Vector3Int(-1, 0,  0),
            new Vector3Int( 0, 0,  1),
            new Vector3Int( 0, 0, -1),
        };
        List<Vector3Int> solidos = new List<Vector3Int>();
        foreach (var d in dirs)
        {
            if (!IsCellEmpty(x + d.x, y, z + d.z)) solidos.Add(d);
        }

        Vector3Int elegido = (solidos.Count > 0)
            ? solidos[Random.Range(0, solidos.Count)]
            : Vector3Int.zero;

        Random.state = estadoViejo;
        return elegido;
    }

    /// <summary>
    /// Devuelve true si la celda está en un macro chunk de Sala Grande (7), Abismo (9) o
    /// Sala Colosal (10). Útil para que decoraciones de pasillo (pasarelas, etc.) no se extiendan
    /// adentro, y para que el pasillo que desemboca ahí se trate como BOCA (recibe su marco).
    /// </summary>
    private bool EsSalaGrandeOAbismo(int x, int y, int z)
    {
        int mx = Mathf.FloorToInt((float)x / macroSize);
        int my = Mathf.FloorToInt((float)y / macroSize);
        int mz = Mathf.FloorToInt((float)z / macroSize);
        int t = ObtenerTipoMacroChunk(mx, my, mz);
        return t == 7 || t == 9 || t == 10;
    }

    private bool EsPasilloInteractuable(int x, int y, int z)
    {
        // 1. Coordenadas del Macro Chunk
        int mx = Mathf.FloorToInt((float)x / macroSize);
        int my = Mathf.FloorToInt((float)y / macroSize);
        int mz = Mathf.FloorToInt((float)z / macroSize);

        // 2. Coordenadas locales dentro del Macro Chunk
        int ly = (y % macroSize + macroSize) % macroSize;

        // Solo permitir a la altura principal de pasillos (ly == 1)
        if (ly != 1) return false;

        int tipo = ObtenerTipoMacroChunk(mx, my, mz);
        // Excluimos Sala Chica (1), Pozo (6), Sala Grande (7), Abismo (9) y Sala Colosal (10)
        if (tipo == 1 || tipo == 6 || tipo == 7 || tipo == 9 || tipo == 10) return false;

        // Debe ser una celda transitable (vacía)
        return IsCellEmpty(x, y, z);
    }

    private void IntentarGenerarPasarela(Vector3Int celda, int x, int y, int z, int tipo, int lx, int ly, int lz, Vector3 origin)
    {
        if (!EsPasilloInteractuable(x, y, z)) return;

        // Determinar si este pasillo debe generar pasarelas (probabilidad)
        int hashPasarela = semillaDelMundo + x * 19283 + y * 48392 + z * 76234 + tipo * 12345;
        Random.State estadoViejo = Random.state;
        Random.InitState(hashPasarela);
        bool debeGenerarPasarela = Random.value <= probabilidadPasarela;
        Random.state = estadoViejo;

        if (!debeGenerarPasarela) return;

        // Lista de alturas posibles
        List<float> alturasDisponibles = new List<float> { cellSize * 0.25f, cellSize * 0.75f };
        alturasDisponibles.Add(cellSize * 0.5f); // altura media siempre disponible (sin tuberías/luces de pared que estorben)

        // Altura compartida por "línea de pasillo" (hash sobre coordenada perpendicular al eje)
        int hashAltura;
        if (tipo == 2)      hashAltura = semillaDelMundo + x * 91733 + y * 26417;
        else if (tipo == 3) hashAltura = semillaDelMundo + z * 91733 + y * 26417;
        else                hashAltura = semillaDelMundo
                                       + Mathf.FloorToInt((float)x / macroSize) * 91733
                                       + Mathf.FloorToInt((float)y / macroSize) * 26417
                                       + Mathf.FloorToInt((float)z / macroSize) * 53729;
        float alturaElegida = alturasDisponibles[Mathf.Abs(hashAltura) % alturasDisponibles.Count];

        // Detectar vecinos transitables (abiertos): un vecino cuenta como "pasillo continuado"
        // SOLO si es vacío Y está dentro de un macro chunk de pasillo (no Sala Grande ni Abismo).
        // Así las pasarelas no se extienden hacia el interior de salas/abismos quedando flotando.
        bool nN = IsCellEmpty(x, y, z + 1) && !EsSalaGrandeOAbismo(x, y, z + 1);
        bool nS = IsCellEmpty(x, y, z - 1) && !EsSalaGrandeOAbismo(x, y, z - 1);
        bool nE = IsCellEmpty(x + 1, y, z) && !EsSalaGrandeOAbismo(x + 1, y, z);
        bool nW = IsCellEmpty(x - 1, y, z) && !EsSalaGrandeOAbismo(x - 1, y, z);
        int totalConexiones = (nN ? 1 : 0) + (nS ? 1 : 0) + (nE ? 1 : 0) + (nW ? 1 : 0);

        // No generar pasarelas en finales de pasillo (0 o 1 conexiones transitables laterales)
        if (totalConexiones <= 1) return;

        // No generar pasarelas dentro ni pegadas a una puerta interactiva (cierre): la pasarela
        // la atravesaría. Se chequea la celda actual y sus 4 vecinas laterales ("atrás y delante").
        if (HabraCierreEn(x, y, z)
            || HabraCierreEn(x + 1, y, z) || HabraCierreEn(x - 1, y, z)
            || HabraCierreEn(x, y, z + 1) || HabraCierreEn(x, y, z - 1))
            return;

        // Centro de la celda en espacio mundo
        Vector3 cellCenter = origin + new Vector3(
            x * cellSize + cellSize * 0.5f,
            y * cellSize + cellSize * 0.5f,
            z * cellSize + cellSize * 0.5f
        );

        // ¿El vecino CONTINÚA este tramo de pasarela? Hacen falta las tres cosas:
        //   a) que el vecino también vaya a generar pasarela (probabilidad, tipo, cierres, conexiones),
        //   b) que le toque LA MISMA altura — si no, no se tocan aunque estén pegados, y
        //   c) que la pared que la sostiene siga existiendo de su lado.
        // Antes esto era un IsCellEmpty pelado, que solo responde "¿hay aire al lado?". Por eso el
        // tramo se creía continuo contra celdas que nunca recibían pasarela: quedaba un _mid
        // apuntando al vacío y la pieza de FIN casi no llegaba a colocarse nunca.
        bool ContinuaTramo(int nx, int nz, Vector3Int caraDir)
        {
            if (!HabraPasarelaEn(nx, y, nz, out float alturaVecina)) return false;
            if (!Mathf.Approximately(alturaVecina, alturaElegida)) return false;
            return !IsCellEmpty(nx + caraDir.x, y, nz + caraDir.z);
        }

        // CORRIDAS DE 2 CELDAS (final+final, sin recta al medio) SE DESCARTAN ENTERAS. Las dos
        // tapas quedan pegadas al mismo punto de unión — se ven como una sola pieza huérfana
        // flotando en la pared, sin pasarela real a la vista. Regla resultante: toda corrida
        // colocada es final-recta(s)-final, así el fin SIEMPRE está unido a una recta. Ambas
        // celdas de la pareja evalúan este mismo filtro por su lado y descartan a la vez.
        (bool, bool) FiltrarCorridaSinRecta(bool cMas, bool cMenos, Vector3Int ejeCorr, Vector3Int caraDir)
        {
            if (cMas == cMenos) return (cMas, cMenos); // tramo medio o aislado: nada que filtrar
            int s = cMas ? 1 : -1;
            bool sigueMasAlla = ContinuaTramo(x + ejeCorr.x * 2 * s, z + ejeCorr.z * 2 * s, caraDir);
            return sigueMasAlla ? (cMas, cMenos) : (false, false);
        }

        // Caras laterales: dir = hacia el vecino, normal = normal interior (hacia pasillo), corrAxis = eje de continuidad, abierto = tiene corredor vecino
        var caras = new (Vector3Int dir, Vector3 normal, Vector3Int corrAxis, bool abierto)[]
        {
            (new Vector3Int(-1, 0,  0), Vector3.right,   new Vector3Int(0, 0, 1), nW), // Pared X- (Oeste)
            (new Vector3Int( 1, 0,  0), Vector3.left,    new Vector3Int(0, 0, 1), nE), // Pared X+ (Este)
            (new Vector3Int( 0, 0, -1), Vector3.forward, new Vector3Int(1, 0, 0), nS), // Pared Z- (Sur)
            (new Vector3Int( 0, 0,  1), Vector3.back,    new Vector3Int(1, 0, 0), nN), // Pared Z+ (Norte)
        };

        // --- CASO 1: INTERSECCIÓN EN T (3 conexiones) ---
        if (totalConexiones == 3)
        {
            foreach (var (dir, normal, corrAxis, abierto) in caras)
            {
                if (abierto) continue; // Buscamos la única cara sólida

                // Cara sólida: colocar segmento recta contra la pared
                Vector3 wallSolida = cellCenter + new Vector3(dir.x, 0, dir.z) * (cellSize * 0.5f);
                Vector3 posSeg = wallSolida + normal * pasarelaOffset;
                posSeg.y = origin.y + y * cellSize + alturaElegida;
                bool vm  = ContinuaTramo(x + corrAxis.x, z + corrAxis.z, dir);
                bool vmn = ContinuaTramo(x - corrAxis.x, z - corrAxis.z, dir);
                (vm, vmn) = FiltrarCorridaSinRecta(vm, vmn, corrAxis, dir);
                InstanciarPasarelaSegmento(celda, posSeg, normal, corrAxis, vm, vmn, true);

                // Cara opuesta (abierta hacia el T): la pieza de T va SOLO si (a) hay un prefab de
                // T DE VERDAD — antes esto caía en silencio a la recta, y como además no chequeaba
                // nada, CADA intersección plantaba una recta suelta cruzada en pleno cruce,
                // conectando nada — y (b) el segmento de la cara sólida realmente se colocó:
                // la T acompaña a una corrida existente, no flota sola en el cruce.
                if (prefabPasarelaT != null && (vm || vmn))
                {
                    Vector3 wallOpuesta = cellCenter + new Vector3(-dir.x, 0, -dir.z) * (cellSize * 0.5f);
                    Vector3 posT = wallOpuesta + (-normal) * pasarelaOffset;
                    posT.y = origin.y + y * cellSize + alturaElegida;
                    InstanciarTPasarela(celda, posT, -normal);
                }
                break;
            }
            return;
        }

        // --- CASO 1.5: ESQUINA EN L (2 conexiones perpendiculares) ---
        // La celda esquina se cubre con UNA pieza integral (mismo criterio que la T): los dos
        // brazos y el codo vienen juntos en el prefab, así que no hay solapes ni huecos que
        // rellenar con piezas sueltas. Solo va si AMBOS brazos continúan en sus vecinos — si no,
        // la esquina es en realidad un final de corrida y caen los segmentos normales del caso 2.
        if (prefabPasarelaL != null && totalConexiones == 2 && (nN || nS) && (nE || nW))
        {
            Vector3Int dirParedZ = nN ? new Vector3Int(0, 0, -1) : new Vector3Int(0, 0, 1); // pared opuesta al abierto en Z
            Vector3Int dirParedX = nE ? new Vector3Int(-1, 0, 0) : new Vector3Int(1, 0, 0); // pared opuesta al abierto en X

            // El brazo que corre pegado a la pared Z-sólida sigue hacia el vecino abierto en X,
            // y el brazo de la pared X-sólida hacia el abierto en Z.
            bool brazoX = ContinuaTramo(x + (nE ? 1 : -1), z, dirParedZ);
            bool brazoZ = ContinuaTramo(x, z + (nN ? 1 : -1), dirParedX);

            // REGLA DURA: sin conectores en el prefab, la L NO se coloca — la esquina cae a las
            // dos tapas de fin del caso 2, que ya calzan bien. Colocarla "por pivote" a ciegas es
            // lo que dejaba cuadrados flotando en el medio de la celda cada vez que el prefab no
            // era la pieza integral esperada. Mejor una esquina sin conectar que basura flotante.
            List<Transform> conectoresPrefabL = new List<Transform>();
            BuscarConectores(prefabPasarelaL.transform, conectoresPrefabL);
            if (conectoresPrefabL.Count == 0)
            {
                if (!avisoPasarelaLSinConector)
                {
                    avisoPasarelaLSinConector = true;
                    Debug.LogWarning("[Pasarelas] 'Prefab Pasarela L' no tiene ningún empty 'Conector' en las puntas de sus brazos — las esquinas usan tapas de fin hasta que se los agregues (o vaciá el slot para sacar este aviso).");
                }
            }
            else if (brazoX && brazoZ)
            {
                // Esquina sólida → yaw, asumiendo prefab modelado para paredes S+O (abiertos N+E).
                float yawEsquina =
                    (!nS && !nW) ? 0f :   // paredes S+O
                    (!nW && !nN) ? 90f :  // paredes O+N
                    (!nN && !nE) ? 180f : // paredes N+E
                    270f;                  // paredes E+S

                Vector3 posL = new Vector3(cellCenter.x, origin.y + y * cellSize + alturaElegida, cellCenter.z);
                GameObject goL = Instantiate(prefabPasarelaL, posL, Quaternion.Euler(0f, yawEsquina, 0f), transform);
                goL.name = prefabPasarelaL.name + "_L";
                goL.SetActive(false);
                RegistrarEnCelda(celda, goL);

                // Alineación por conector (opcional pero recomendada): si el prefab tiene empties
                // "Conector" en las puntas de sus brazos, se pega el brazo del lado X abierto al
                // punto EXACTO donde arranca la recta vecina — el borde de celda, sobre la línea
                // de pivote de la pared Z-sólida. Con esto el pivote del prefab puede estar en
                // cualquier lado; sin conectores, el prefab debe tener el pivote en el centro de
                // la celda (comportamiento anterior).
                List<Transform> conectoresL = new List<Transform>();
                BuscarConectores(goL.transform, conectoresL);
                if (conectoresL.Count > 0)
                {
                    Vector3 dirAbiertoX = nE ? Vector3.right : Vector3.left;
                    float xUnion = origin.x + (nE ? x + 1 : x) * cellSize;
                    float zLineaPivote = nN
                        ? origin.z + z * cellSize + pasarelaOffset
                        : origin.z + (z + 1) * cellSize - pasarelaOffset;
                    Vector3 puntoUnionL = new Vector3(xUnion, posL.y, zLineaPivote);

                    Transform conectorL = conectoresL[0];
                    float mejorL = Vector3.Dot(conectorL.position - goL.transform.position, dirAbiertoX);
                    for (int i = 1; i < conectoresL.Count; i++)
                    {
                        float dL = Vector3.Dot(conectoresL[i].position - goL.transform.position, dirAbiertoX);
                        if (dL > mejorL) { mejorL = dL; conectorL = conectoresL[i]; }
                    }
                    goL.transform.position += puntoUnionL - conectorL.position;
                }

                return;
            }
        }

        // --- CASO 2: PASILLOS ESTÁNDAR / EXTREMOS — colocar segmento en cada cara sólida ---
        foreach (var (dir, normal, corrAxis, abierto) in caras)
        {
            if (abierto) continue; // Si el vecino es pasillo abierto, no hay pared

            // Centro de la cara de la pared
            Vector3 wallFaceCenter = cellCenter + new Vector3(dir.x, 0, dir.z) * (cellSize * 0.5f);

            // Posición: desde la cara de la pared, hacia el interior
            Vector3 pos = wallFaceCenter + normal * pasarelaOffset;
            pos.y = origin.y + y * cellSize + alturaElegida;

            bool vecinoMas   = ContinuaTramo(x + corrAxis.x, z + corrAxis.z, dir);
            bool vecinoMenos = ContinuaTramo(x - corrAxis.x, z - corrAxis.z, dir);
            (vecinoMas, vecinoMenos) = FiltrarCorridaSinRecta(vecinoMas, vecinoMenos, corrAxis, dir);
            // ⚠️ De qué LADO de la corrida queda la pared. Antes era `(dir.x < 0 || dir.z < 0)`, y
            // estaba MAL: la izquierda depende de hacia dónde corre el pasillo, no del signo del eje.
            //   · pasillo en Z, pared en X−  → mirando a +Z, la pared está a la IZQUIERDA
            //   · pasillo en X, pared en Z−  → mirando a +X, la pared está a la DERECHA
            // La versión vieja metía las dos en la misma bolsa, así que en los pasillos de un eje
            // la paridad quedaba invertida respecto a los del otro. Síntoma: dentro de un mismo
            // grupo de piezas con nombre `_izq` idéntico, unas encastran y otras quedan despegadas,
            // y ningún valor del toggle global las arregla a todas (arregla un eje y rompe el otro).
            // El producto cruzado da la lateralidad de verdad.
            bool esIzquierda = Vector3.Cross(new Vector3(corrAxis.x, 0f, corrAxis.z),
                                             new Vector3(dir.x, 0f, dir.z)).y > 0f;

            InstanciarPasarelaSegmento(celda, pos, normal, corrAxis, vecinoMas, vecinoMenos, esIzquierda);
        }
    }

    private void InstanciarPasarelaSegmento(Vector3Int celda, Vector3 pos, Vector3 normalInterior, Vector3Int corrAxis, bool conectaMas, bool conectaMenos, bool esIzquierda)
    {
        // Pieza SUELTA (ningún vecino continúa el tramo): no se coloca nada. Una pasarela de una
        // sola celda flotando en la pared no conecta nada con nada — y es lo que hacía que
        // aparecieran finales/rectas huérfanas. Toda pieza colocada pertenece a una corrida de 2+
        // celdas: las puntas llevan el prefab de FIN (el indicativo de que ahí termina) y el medio
        // rectas/escaleras.
        if (!conectaMas && !conectaMenos)
        {
            return;
        }

        GameObject prefabElegido = prefabPasarelaRecta;
        string sufijo = "_isolated";
        Vector3 rotNormal = normalInterior;

        // Detección de tramo continuo: el MEDIO del tramo es SIEMPRE recta pelada. Antes acá había
        // un roll de escalera (20%) y las hileras largas quedaban picadas: una corrida que iba a
        // ser recta 5-6 celdas aparecía cortada en segmentos porque la pieza de escalera rompe la
        // línea de la baranda. La escalera es un ACCESO — vive en los extremos, no en el medio.
        if (conectaMas && conectaMenos)
        {
            sufijo = "_mid";
        }
        // Detección de tramo final (extremo)
        else if (conectaMas || conectaMenos)
        {
            // El extremo elige UNA de dos terminaciones, excluyentes entre sí:
            //   - ESCALERA: pieza de largo completo con escalera vertical/caracol — el punto de
            //     subida/bajada de la pasarela. No lleva tapa de fin (la escalera ES el final).
            //   - TAPA DE FIN: el cierre corto de siempre (con conector y prefab espejado).
            int hashEscalera = semillaDelMundo + Mathf.RoundToInt(pos.x) * 17 + Mathf.RoundToInt(pos.z) * 31;
            bool hayEscaleras = prefabsPasarelaConEscalera != null && prefabsPasarelaConEscalera.Length > 0;
            if (hayEscaleras && (Mathf.Abs(hashEscalera) % 1000) / 1000f < probabilidadEscaleraExtremo)
            {
                sufijo = "_end_escalera"; // distinto de "_end": sin conector, sin yaw extra de fin

                // Mismo índice en las dos listas: así la variante espejada que toca es el espejo de
                // la normal que hubiera tocado, y no una escalera distinta.
                GameObject[] listaEscaleras = (NecesitaEspejo(conectaMenos, esIzquierda)
                        && prefabsPasarelaConEscaleraInvertida != null
                        && prefabsPasarelaConEscaleraInvertida.Length > 0)
                    ? prefabsPasarelaConEscaleraInvertida
                    : prefabsPasarelaConEscalera;

                prefabElegido = listaEscaleras[Mathf.Abs(hashEscalera / 1000) % listaEscaleras.Length];
            }
            else
            {
            sufijo = "_end";
            if (prefabPasarelaFin != null)
            {
                prefabElegido = prefabPasarelaFin;
            }

            // OJO: acá antes se hacía `rotNormal = -normalInterior` para "alinear la conexión".
            // Con el pivote de estos prefabs (centro del largo, sobre el borde OPUESTO a las
            // barandas) eso no giraba la tapa de punta: giraba la pieza entera 180° sobre Y, así
            // que el tablón se metía DENTRO de la pared y las barandas quedaban del lado del muro.
            //
            // La variante espejada (`prefabPasarelaFinInvertida`) resuelve en qué punta queda la
            // tapa. ⚠️ La condición ANTES era solo `conectaMenos` — le faltaba la pared, así que
            // acertaba en 2 de las 4 combinaciones. Ver NecesitaEspejo.
            if (NecesitaEspejo(conectaMenos, esIzquierda) && prefabPasarelaFinInvertida != null)
            {
                prefabElegido = prefabPasarelaFinInvertida;
            }
            }
        }

        if (prefabElegido == null) return;

        string lado = esIzquierda ? "_izq" : "_der";
        string nombre = prefabElegido.name + lado + sufijo;
        Quaternion rotacion = RotacionDesdeNormalInterior(rotNormal);
        // El prefab de fin puede estar modelado con el largo sobre otro eje que la recta: este
        // yaw extra (configurable en el Inspector, pasos de 90) lo endereza sin re-modelar nada.
        if (sufijo == "_end") rotacion *= Quaternion.Euler(0f, yawExtraPasarelaFin, 0f);
        // La escalera tiene su propio yaw: en este proyecto va en 180 porque con 0 las barandas
        // salen para el lado equivocado. ⚠️ Ese giro TAMBIÉN invierte qué punta mira a la unión —
        // de eso se encarga `invertirEspejoPasarela`, no este yaw. Dos problemas, dos perillas.
        else if (sufijo == "_end_escalera") rotacion *= Quaternion.Euler(0f, yawExtraPasarelaEscalera, 0f);
        GameObject go = InstanciarYRegistrarPasarelaRotacion(celda, prefabElegido, pos, rotacion, nombre);

        // Piezas de terminación MÁS CORTAS que la celda (la tapa de fin, y las escaleras que miden
        // ~4-5m menos que la recta): instanciadas "centradas en la pared" quedan flotando con aire
        // a los lados, sin tocar a la recta vecina que termina justo en el borde. Se pegan
        // alineando un empty "Conector" del prefab con el borde de celda del lado conectado.
        //
        // Si el prefab tiene VARIOS conectores (uno por punta — lo recomendado para las
        // escaleras, que tienen sus dos puntas abiertas), se usa el que ya quedó mirando hacia la
        // unión tras la rotación: así la pieza siempre se extiende hacia su PROPIA celda y nunca
        // se solapa con la recta vecina. La tapa de fin debe tener UN solo conector (en su punta
        // abierta): con dos, este picker podría apoyar la tapa contra la recta.
        if (go != null && (sufijo == "_end" || sufijo == "_end_escalera"))
        {
            List<Transform> conectores = new List<Transform>();
            BuscarConectores(go.transform, conectores);
            if (conectores.Count > 0)
            {
                Vector3 dirConexion = new Vector3(corrAxis.x, 0f, corrAxis.z) * (conectaMas ? 1f : -1f);
                Vector3 puntoUnion = pos + dirConexion * (cellSize * 0.5f);

                Transform conector = ConectorHaciaLaUnion(conectores, go.transform.position, dirConexion);
                go.transform.position += puntoUnion - conector.position;
            }
            else
            {
                // Sin conector la pieza queda centrada en la celda en vez de pegada a la recta
                // vecina. Es un error de setup del prefab, no una situación normal — por eso avisa
                // siempre y no detrás de un flag de debug.
                Debug.LogWarning($"[Pasarelas] '{prefabElegido.name}' no tiene ningún hijo 'Conector' — la pieza queda centrada en la celda en vez de pegada a la recta vecina.");
            }
        }

        // ESCALERA DE BAJADA: tramos apilados desde el piso de la celda hasta la plataforma.
        // Va DESPUÉS del ajuste por conector a propósito: la escalera cuelga de la posición FINAL
        // de la plataforma, no de la previa al encastre, o quedaría corrida respecto a ella.
        //
        // Las alturas posibles son cellSize * 0.25/0.50/0.75 (relación 1:2:3), así que un tramo de
        // un cuarto de celda cubre las tres apilándose 1, 2 o 3 veces — sin estirar y sin modelar
        // tres largos.
        if (go != null && sufijo == "_end_escalera" && prefabTramoEscalera != null)
        {
            float alturaTramo = alturaTramoEscalera > 0f ? alturaTramoEscalera : cellSize * 0.25f;
            if (alturaTramo > 0.01f)
            {
                // ANCLAJE: si la plataforma trae un empty hijo llamado "PuntoEscalera", la escalera
                // cuelga EXACTAMENTE de ahí — o sea, del hueco que modelaste. Sin ese marcador cae
                // al pivote de la plataforma, que en estos prefabs está en el CENTRO del largo, y
                // la escalera aparece en la mitad de la pasarela en vez de en la punta.
                Transform anclaEscalera = BuscarHijoPorPrefijo(go.transform, "PuntoEscalera");
                Vector3 ancla = anclaEscalera != null ? anclaEscalera.position : go.transform.position;

                // Igual que el Conector: si falta el marcador es un prefab mal armado, así que
                // conviene que se vea sin tener que prender nada.
                if (anclaEscalera == null)
                    Debug.LogWarning($"[Pasarelas] '{prefabElegido.name}' no tiene un hijo 'PuntoEscalera' — la escalera cuelga del pivote (mitad del largo) en vez de la punta. Hijos que SÍ encontré: {ListarDescendientes(go.transform)}");

                float pisoY = transform.position.y + celda.y * cellSize;
                // Clamp defensivo: si alguien mete un alto de tramo minúsculo en el Inspector, esto
                // podría intentar apilar cientos de piezas en cada extremo de pasarela.
                int tramos = Mathf.Clamp(Mathf.RoundToInt((ancla.y - pisoY) / alturaTramo), 0, 8);

                for (int i = 0; i < tramos; i++)
                {
                    Vector3 posTramo = new Vector3(
                        ancla.x,
                        pisoY + i * alturaTramo,
                        ancla.z);

                    GameObject tramo = Instantiate(prefabTramoEscalera, posTramo, go.transform.rotation, transform);
                    tramo.name = prefabTramoEscalera.name + "_tramo" + i;
                    tramo.SetActive(false);
                    RegistrarEnCelda(celda, tramo);
                }

            }
        }

    }

    private void InstanciarTPasarela(Vector3Int celda, Vector3 pos, Vector3 normalInterior)
    {
        // SIN fallback a la recta: si no hay prefab de T, no se coloca nada. El fallback plantaba
        // una recta suelta atravesada en cada intersección — peor que no poner pieza.
        if (prefabPasarelaT == null) return;

        Quaternion rotacion = RotacionDesdeNormalInterior(normalInterior);
        InstanciarYRegistrarPasarelaRotacion(celda, prefabPasarelaT, pos, rotacion, prefabPasarelaT.name + "_T");
    }

    private void InstanciarYRegistrarPasarela(Vector3Int celda, GameObject prefab, Vector3 pos, float yaw, string nombre)
    {
        if (prefab == null) return;

        Quaternion rot = Quaternion.Euler(0, yaw, 0);
        GameObject go = Instantiate(prefab, pos, rot, transform);
        go.name = nombre;

        go.SetActive(false);
        RegistrarEnCelda(celda, go);
    }

    private GameObject InstanciarYRegistrarPasarelaRotacion(Vector3Int celda, GameObject prefab, Vector3 pos, Quaternion rotacion, string nombre)
    {
        if (prefab == null) return null;

        Quaternion rotacionAjustada = rotacion * Quaternion.Euler(0, 180f, 0);
        GameObject go = Instantiate(prefab, pos, rotacionAjustada, transform);
        go.name = nombre;

        // Único punto de instanciación de TODAS las piezas de pasarela (recta, fin, escalera, L,
        // T) — por eso es acá y no en cada llamador donde se resuelven los `PuntoSpawnItem` que
        // el prefab pueda tener adentro (ej: un palet en el medio de una recta). Antes NADIE
        // llamaba esto para pasarelas — el marcador podía estar perfecto y jamás se leía.
        // Hash propio por posición: ProcesarPuntosSpawnItem exige Random ya sembrado para ser
        // determinista, y acá no llega ninguno sembrado desde el caller (cada roll de la cadena
        // de decisión de la pasarela restaura su propio Random.state apenas termina).
        int hashSpawn = semillaDelMundo
                       + Mathf.RoundToInt(pos.x) * 92821
                       + Mathf.RoundToInt(pos.y) * 68917
                       + Mathf.RoundToInt(pos.z) * 50171;
        Random.State prevSpawn = Random.state;
        Random.InitState(hashSpawn);
        ProcesarPuntosSpawnItem(go, celda);
        Random.state = prevSpawn;

        go.SetActive(false);
        RegistrarEnCelda(celda, go);
        return go;
    }

    /// <summary>
    /// Junta recursivamente TODOS los hijos cuyo nombre empiece con "Conector" (los empties que
    /// marcan las puntas abiertas de una pieza de pasarela). Las piezas con una sola punta
    /// conectable (tapa de fin) llevan uno; las de dos puntas abiertas (escaleras) llevan uno por
    /// punta y el llamador elige cuál alinear.
    /// </summary>
    /// <summary>
    /// Deja solo letras y dígitos en minúscula. Así "Punto_Escalera", "Punto Escalera",
    /// "puntoEscalera.001" y "PUNTO-ESCALERA" son todos el mismo nombre — que es lo que uno espera
    /// al nombrar un empty en Blender/Unity, donde los separadores y los sufijos .001 aparecen solos.
    /// </summary>
    private static string NormalizarNombre(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    /// <summary>
    /// Primer descendiente cuyo nombre (normalizado) empieza con `prefijo`, o null.
    /// Mismos marcadores-empty-en-el-prefab que BuscarConectores: la posición exacta la decide el
    /// modelo y no un offset a ciegas en código.
    /// </summary>
    private static Transform BuscarHijoPorPrefijo(Transform raiz, string prefijo)
    {
        return BuscarHijoNormalizado(raiz, NormalizarNombre(prefijo));
    }

    private static Transform BuscarHijoNormalizado(Transform raiz, string objetivoNormalizado)
    {
        foreach (Transform hijo in raiz)
        {
            if (NormalizarNombre(hijo.name).StartsWith(objetivoNormalizado, System.StringComparison.Ordinal))
                return hijo;

            Transform enNieto = BuscarHijoNormalizado(hijo, objetivoNormalizado);
            if (enNieto != null) return enNieto;
        }
        return null;
    }

    /// <summary>
    /// ¿Esta pieza de extremo necesita el prefab ESPEJADO A LO LARGO?
    ///
    /// Hay dos cosas que invierten la pieza y son independientes entre sí:
    ///   1. contra qué PARED va → la rotación difiere 180° entre izquierda y derecha
    ///   2. qué PUNTA de la corrida es → la unión cae del otro lado (conectaMas / conectaMenos)
    ///
    /// Si se dan LAS DOS, se cancelan. Si se da UNA SOLA, hace falta el espejo. O sea: XOR.
    ///
    /// `invertirEspejoPasarela` lo da vuelta desde el Inspector. Hace falta porque el código sabe
    /// CUÁNDO espejar pero no cuál de los dos prefabs asignados es el espejado — y encima el
    /// `yawExtraPasarelaEscalera` (180 en este proyecto, para las barandas) también invierte la
    /// punta. Es más rápido que él lo tildee y vea, que deducirlo desde acá: ya se intentó dos
    /// veces adivinando la paridad y las dos veces salió mal.
    ///
    /// ⚠️ Diagnóstico por cantidad de fallos: si fallan TODAS es este toggle; si falla la MITAD,
    /// la condición dejó de mirar una de las dos variables (ver bug histórico en GDD §20.6).
    /// </summary>
    private bool NecesitaEspejo(bool conectaMenos, bool esIzquierda)
        => (conectaMenos != esIzquierda) != invertirEspejoPasarela;

    /// <summary>De los conectores dados, el que quedó MÁS ADELANTADO hacia la unión.</summary>
    private static Transform ConectorHaciaLaUnion(List<Transform> conectores, Vector3 centroPieza, Vector3 dirConexion)
    {
        Transform mejor = conectores[0];
        float mejorProy = Vector3.Dot(mejor.position - centroPieza, dirConexion);
        for (int i = 1; i < conectores.Count; i++)
        {
            float d = Vector3.Dot(conectores[i].position - centroPieza, dirConexion);
            if (d > mejorProy) { mejorProy = d; mejor = conectores[i]; }
        }
        return mejor;
    }

    /// <summary>Nombres de todos los descendientes, para diagnosticar marcadores que no se encuentran.</summary>
    private static string ListarDescendientes(Transform raiz)
    {
        var nombres = new List<string>();
        void Recorrer(Transform t)
        {
            foreach (Transform hijo in t) { nombres.Add(hijo.name); Recorrer(hijo); }
        }
        Recorrer(raiz);
        return nombres.Count == 0 ? "(ningún hijo)" : string.Join(", ", nombres);
    }

    private static void BuscarConectores(Transform raiz, List<Transform> resultado)
    {
        foreach (Transform hijo in raiz)
        {
            if (hijo.name.StartsWith("Conector", System.StringComparison.OrdinalIgnoreCase))
                resultado.Add(hijo);
            BuscarConectores(hijo, resultado);
        }
    }

    /// <summary>
    /// Riega props en la FRANJA pegada a cada pared de una celda de pasillo. La franja se define
    /// por cálculo (nada de colliders): desde cada cara lateral CON PARED, `anchoFranjaBorde`
    /// metros hacia el centro. Caras sin pared — pasillo abierto, balcón, o cara al void — no
    /// tienen franja, así que los objetos quedan SIEMPRE del lado de la pared por construcción.
    /// Determinista por semilla+celda+cara, mismo patrón que todos los decoradores.
    /// </summary>
    private void IntentarGenerarObjetosBorde(Vector3Int celda, int x, int y, int z, Vector3 origin)
    {
        if (prefabsObjetoBorde == null || prefabsObjetoBorde.Length == 0) return;
        if (!EsPasilloInteractuable(x, y, z)) return;
        // No amontonar cosas justo donde va una puerta de cierre.
        if (HabraCierreEn(x, y, z)) return;

        var carasBorde = new (Vector3Int dir, Vector3 normal)[]
        {
            (new Vector3Int(-1, 0,  0), Vector3.right),
            (new Vector3Int( 1, 0,  0), Vector3.left),
            (new Vector3Int( 0, 0, -1), Vector3.forward),
            (new Vector3Int( 0, 0,  1), Vector3.back),
        };

        Vector3 centroPiso = origin + new Vector3(
            x * cellSize + cellSize * 0.5f,
            y * cellSize,
            z * cellSize + cellSize * 0.5f);

        foreach (var (dir, normal) in carasBorde)
        {
            if (IsCellEmpty(x + dir.x, y, z + dir.z)) continue;                    // sin pared → sin franja
            if (DireccionBalcon(x, y, z) == dir) continue;                         // el balcón abrió esta cara
            if (CaraDaAlVacio(true, x, y, z, x + dir.x, y, z + dir.z)) continue;   // cara al void: no hay muro

            int hash = semillaDelMundo + x * 48271 + y * 16807 + z * 69621 + (dir.x * 3 + dir.z) * 7919;
            Random.State prev = Random.state;
            Random.InitState(hash);

            if (Random.value > probabilidadObjetoBorde) { Random.state = prev; continue; }

            int cantidad = Random.Range(Mathf.Max(1, objetosPorCara.x), Mathf.Max(1, objetosPorCara.y) + 1);
            Vector3 ejeLargo = Vector3.Cross(Vector3.up, normal);                  // eje a lo largo de la pared
            float mitadUtil = Mathf.Max(0.5f, cellSize * 0.5f - margenFranjaBorde);
            Vector3 centroPared = centroPiso + new Vector3(dir.x, 0f, dir.z) * (cellSize * 0.5f);

            for (int i = 0; i < cantidad; i++)
            {
                GameObject prefab = prefabsObjetoBorde[Random.Range(0, prefabsObjetoBorde.Length)];
                if (prefab == null) continue;

                float alLargo = Random.Range(-mitadUtil, mitadUtil);
                float haciaAdentro = Random.Range(0.25f, Mathf.Max(0.3f, anchoFranjaBorde));
                Vector3 pos = centroPared + normal * haciaAdentro + ejeLargo * alLargo;

                Quaternion rot = objetoBordeMiraAlPasillo
                    ? Quaternion.LookRotation(normal, Vector3.up)
                    : Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                GameObject go = Instantiate(prefab, pos, rot, transform);
                go.SetActive(false);
                RegistrarEnCelda(celda, go);
            }

            Random.state = prev;
        }
    }

    // ===================== TUBERÍAS MODULARES (kit por conectores) =====================
    // v1: solo celdas con exactamente 2 vecinos laterales abiertos (tramo recto u esquina).
    // Intersecciones, callejones y T/cruces quedan para una v2 con los 3 conectores por lado
    // del documento original — acá cada celda usa 1 solo conector (el del medio) por lado.

    /// <summary>
    /// Descompone un largo total en piezas de 5/2/1m (greedy, mismo criterio que el kit del
    /// documento: "7 = 5+2, 8 = 5+2+1"). Minimiza la cantidad de piezas.
    /// </summary>
    private List<float> DescomponerLongitudTuberia(float longitudTotal)
    {
        float[] piezas = { 5f, 2f, 1f };
        List<float> resultado = new List<float>();
        float restante = longitudTotal;
        foreach (float pieza in piezas)
        {
            while (restante >= pieza - 0.01f)
            {
                resultado.Add(pieza);
                restante -= pieza;
            }
        }
        return resultado;
    }

    private GameObject[] ArrayTuberiaParaLongitud(float longitud)
    {
        if (Mathf.Approximately(longitud, 5f)) return prefabsTuberiaRecta5m;
        if (Mathf.Approximately(longitud, 2f)) return prefabsTuberiaRecta2m;
        return prefabsTuberiaRecta1m;
    }

    private void InstanciarYRegistrarTuberia(Vector3Int celda, GameObject prefab, Vector3 pos, Quaternion rotacion, string nombre)
    {
        if (prefab == null) return;
        GameObject go = Instantiate(prefab, pos, rotacion, transform);
        go.name = nombre;
        go.SetActive(false);
        RegistrarEnCelda(celda, go);
    }

    /// <summary>
    /// Instancia una cadena de piezas rectas desde "inicio" avanzando por "ejeDir" (eje X o Z,
    /// con signo), una detrás de otra, según la lista de largos ya decompuesta.
    /// </summary>
    private void InstanciarCadenaTuberiaRecta(Vector3Int celda, Vector3 inicio, Vector3Int ejeDir, List<float> segmentos)
    {
        Vector3 eje = new Vector3(ejeDir.x, 0, ejeDir.z);
        Quaternion rot = Quaternion.LookRotation(eje, Vector3.up);
        Vector3 cursor = inicio;
        foreach (float largo in segmentos)
        {
            GameObject[] candidatos = ArrayTuberiaParaLongitud(largo);
            if (candidatos != null && candidatos.Length > 0)
            {
                GameObject prefab = candidatos[Random.Range(0, candidatos.Length)];
                Vector3 pos = cursor + eje * (largo * 0.5f);
                InstanciarYRegistrarTuberia(celda, prefab, pos, rot, prefab.name);
            }
            cursor += eje * largo;
        }
    }

    /// <summary>
    /// Tubería modular por conectores: si la celda es un pasillo con exactamente 2 vecinos
    /// laterales abiertos (recto o esquina, mismo criterio que EsTramoRectoDePasillo), corre
    /// una tubería de un lado al otro combinando piezas de 1/2/5m, con curva 90° en esquinas.
    /// El conector queda fijo en el punto medio de cada lado (v1: 1 conector por lado, no 3).
    /// </summary>
    /// <summary>
    /// Predice si la celda (x,y,z) generaría tubería (sin instanciar nada) — misma elegibilidad
    /// y mismo roll de probabilidad que IntentarGenerarTuberiaModular. La usan: (1) la propia
    /// función para no duplicar el hash, y (2) cada extremo de un recorrido, para saber si el
    /// vecino continúa la tubería o si hay que taponar (ver TaponarSiHaceFalta).
    /// </summary>
    private bool HabraTuberiaEn(int x, int y, int z)
    {
        if (!EsPasilloInteractuable(x, y, z)) return false;
        if (HabraCierreEn(x, y, z)) return false;

        bool nE = IsCellEmpty(x + 1, y, z);
        bool nW = IsCellEmpty(x - 1, y, z);
        bool nN = IsCellEmpty(x, y, z + 1);
        bool nS = IsCellEmpty(x, y, z - 1);
        int total = (nE ? 1 : 0) + (nW ? 1 : 0) + (nN ? 1 : 0) + (nS ? 1 : 0);
        if (total != 2) return false;

        int hash = semillaDelMundo + x * 28657 + y * 46471 + z * 15629;
        Random.State estadoViejo = Random.state;
        Random.InitState(hash);
        bool debeGenerar = Random.value <= probabilidadTuberia;
        Random.state = estadoViejo;
        return debeGenerar;
    }

    // ===================== CARRILES DE TECHO =====================
    // El techo de un pasillo se reparte en 3 carriles a lo ancho: 0=izquierda, 1=centro,
    // 2=derecha. Cada sistema que cuelga algo del techo (tuberías, luces, y lo que venga) reclama
    // carril acá en vez de calcular su offset por su cuenta — así dejan de pisarse.
    //
    // ⚠️ TODO ESTO ES PURO POR SEMILLA, a propósito. La tentación es preguntar "¿qué hay ya
    // instanciado en esta celda?", pero NO SIRVE: las celdas se generan en orden arbitrario según
    // por dónde camina el jugador, y WorkerDescarga además las destruye y regenera. Un chequeo
    // contra objetos vivos daría un resultado distinto según el orden → el mismo seed generaría
    // mundos distintos y las cosas parpadearían entre visitas. Mismo patrón que el resto de
    // predictores del generador (HabraPasarelaEn, HabraCierreEn, HabraTuberiaEn).
    //
    // PRIORIDAD: la tubería reclama primero y la luz cede. No es arbitrario — la tubería es un
    // RECORRIDO CONTINUO entre celdas: si esquivara celda por celda quedaría en zigzag. La luz es
    // un objeto suelto por celda, se puede correr sin que se note.

    private const int CarrilesTecho = 3;

    /// <summary>Desplazamiento lateral (desde el centro del pasillo) del carril dado.</summary>
    private float OffsetLateralCarril(int carril)
    {
        float sep = Mathf.Min(separacionCarrilesTecho, cellSize * 0.5f - luzTechoMargenPared);
        return (carril - 1) * sep; // 0 → -sep, 1 → 0, 2 → +sep
    }

    /// <summary>
    /// Carril que usa la tubería que pasa por esta celda. El hash va sobre la coordenada
    /// PERPENDICULAR al recorrido (mismo truco que la altura de las pasarelas): así todas las
    /// celdas de una misma corrida sacan el mismo carril y la tubería sale derecha, no en zigzag.
    /// </summary>
    private int CarrilTuberiaEn(int x, int y, int z)
    {
        if (!tuberiaUsaCarrilAleatorio) return 1; // centro — comportamiento de siempre

        bool nE = IsCellEmpty(x + 1, y, z);
        bool nW = IsCellEmpty(x - 1, y, z);
        bool corridaEnX = nE && nW;

        // Perpendicular: si corre en X, lo constante a lo largo es Z (y viceversa).
        int perpendicular = corridaEnX ? z : x;
        int hash = semillaDelMundo + perpendicular * 39119 + y * 71413;
        return ((hash % CarrilesTecho) + CarrilesTecho) % CarrilesTecho;
    }

    /// <summary>
    /// Qué carriles del techo ya están reclamados en esta celda por sistemas de mayor prioridad.
    /// Hoy solo la tubería; si mañana se agrega otra cosa que cuelgue del techo, se suma acá y
    /// todos los sistemas de abajo la respetan automáticamente.
    /// </summary>
    private bool[] CarrilesOcupadosTecho(int x, int y, int z)
    {
        bool[] ocupados = new bool[CarrilesTecho];

        if (HabraTuberiaEn(x, y, z))
            ocupados[CarrilTuberiaEn(x, y, z)] = true;

        return ocupados;
    }

    /// <summary>
    /// Si la celda vecina (nx,ny,nz) NO va a continuar la tubería (HabraTuberiaEn = false),
    /// tapa el extremo en "puntoBorde" con un prefab de Tapón — orientado hacia afuera, hacia
    /// donde el recorrido se corta. Así solo aparece en los extremos reales, nunca en el medio
    /// de un tramo donde sigue conectando con la celda de al lado.
    /// </summary>
    private void TaponarSiHaceFalta(Vector3Int celda, Vector3 puntoBorde, Vector3Int direccionHaciaFuera, int nx, int ny, int nz)
    {
        if (HabraTuberiaEn(nx, ny, nz)) return; // el vecino sigue el recorrido, no es un extremo

        GameObject[] candidatos = prefabsTuberiaTapon;
        if (candidatos == null || candidatos.Length == 0) return;

        GameObject prefab = candidatos[Random.Range(0, candidatos.Length)];
        Vector3 dirF = new Vector3(direccionHaciaFuera.x, 0, direccionHaciaFuera.z);
        Quaternion rot = Quaternion.LookRotation(dirF, Vector3.up);
        InstanciarYRegistrarTuberia(celda, prefab, puntoBorde, rot, prefab.name + "_tapon");
    }

    private void IntentarGenerarTuberiaModular(Vector3Int celda, int x, int y, int z, Vector3 origin)
    {
        if (!HabraTuberiaEn(x, y, z)) return;

        bool nE = IsCellEmpty(x + 1, y, z);
        bool nW = IsCellEmpty(x - 1, y, z);
        bool nN = IsCellEmpty(x, y, z + 1);
        bool nS = IsCellEmpty(x, y, z - 1);

        Vector3 cellCenter = origin + new Vector3(
            x * cellSize + cellSize * 0.5f,
            y * cellSize + alturaTuberia,
            z * cellSize + cellSize * 0.5f
        );

        // El desplazamiento lateral sale del CARRIL que le tocó a esta corrida (ver
        // CarrilTuberiaEn) — es lo que después respetan las luces para no encimarse.
        float offsetCarril = OffsetLateralCarril(CarrilTuberiaEn(x, y, z));

        if (nE && nW) // tramo recto en X
        {
            Vector3 lateral = new Vector3(0, 0, offsetCarril);
            Vector3 edgeOeste = cellCenter - new Vector3(cellSize * 0.5f, 0, 0) + lateral;
            Vector3 edgeEste = cellCenter + new Vector3(cellSize * 0.5f, 0, 0) + lateral;
            List<float> segmentos = DescomponerLongitudTuberia(cellSize);
            InstanciarCadenaTuberiaRecta(celda, edgeOeste, new Vector3Int(1, 0, 0), segmentos);

            TaponarSiHaceFalta(celda, edgeOeste, new Vector3Int(-1, 0, 0), x - 1, y, z);
            TaponarSiHaceFalta(celda, edgeEste, new Vector3Int(1, 0, 0), x + 1, y, z);
        }
        else if (nN && nS) // tramo recto en Z
        {
            Vector3 lateral = new Vector3(offsetCarril, 0, 0);
            Vector3 edgeSur = cellCenter - new Vector3(0, 0, cellSize * 0.5f) + lateral;
            Vector3 edgeNorte = cellCenter + new Vector3(0, 0, cellSize * 0.5f) + lateral;
            List<float> segmentos = DescomponerLongitudTuberia(cellSize);
            InstanciarCadenaTuberiaRecta(celda, edgeSur, new Vector3Int(0, 0, 1), segmentos);

            TaponarSiHaceFalta(celda, edgeSur, new Vector3Int(0, 0, -1), x, y, z - 1);
            TaponarSiHaceFalta(celda, edgeNorte, new Vector3Int(0, 0, 1), x, y, z + 1);
        }
        else // esquina: 2 vecinos perpendiculares
        {
            float largoLibre = Mathf.Max(0f, cellSize * 0.5f - longitudCurvaTuberia * 0.5f);
            List<float> segmentos = DescomponerLongitudTuberia(largoLibre);

            Vector3Int dirA = nE ? new Vector3Int(1, 0, 0) : new Vector3Int(-1, 0, 0);
            Vector3Int dirB = nN ? new Vector3Int(0, 0, 1) : new Vector3Int(0, 0, -1);

            Vector3 edgeA = cellCenter + new Vector3(dirA.x, 0, dirA.z) * (cellSize * 0.5f);
            Vector3 edgeB = cellCenter + new Vector3(dirB.x, 0, dirB.z) * (cellSize * 0.5f);

            InstanciarCadenaTuberiaRecta(celda, edgeA, -dirA, segmentos);
            InstanciarCadenaTuberiaRecta(celda, edgeB, -dirB, segmentos);

            TaponarSiHaceFalta(celda, edgeA, dirA, x + dirA.x, y, z + dirA.z);
            TaponarSiHaceFalta(celda, edgeB, dirB, x + dirB.x, y, z + dirB.z);

            // Tabla de yaw cardinal (mismo criterio que Vértices Convexos, §5.5): prefab
            // modelado por defecto para la esquina NE.
            float yaw;
            if (nE && nN) yaw = 0f;
            else if (nE && nS) yaw = 90f;
            else if (nW && nS) yaw = 180f;
            else yaw = -90f; // nW && nN

            GameObject[] curvas = prefabsTuberiaCurva90;
            if (curvas != null && curvas.Length > 0)
            {
                GameObject prefabCurva = curvas[Random.Range(0, curvas.Length)];
                Quaternion rotCurva = Quaternion.Euler(0, yaw + yawBaseTuberiaCurva, 0);
                InstanciarYRegistrarTuberia(celda, prefabCurva, cellCenter, rotCurva, prefabCurva.name + "_curva");
            }
        }
    }

    /// <summary>
    /// Predice si la celda (x,y,z) generará un cierre/puerta interactiva, sin instanciarlo.
    /// Replica la decisión de <see cref="IntentarGenerarCierrePasillo"/> para que las pasarelas
    /// (que corren antes en el pipeline) puedan evitar cruzarse con las puertas.
    /// </summary>
    private bool HabraCierreEn(int x, int y, int z)
    {
        Vector3Int k = new Vector3Int(x, y, z);
        if (cacheCierre.TryGetValue(k, out bool cached)) return cached;
        bool habra = CalcularHabraCierre(x, y, z);
        cacheCierre[k] = habra;
        return habra;
    }

    private bool CalcularHabraCierre(int x, int y, int z)
    {
        // Delegado a la regla única de transición (misma función que usa la generación →
        // predictor y generador quedan sincronizados por construcción).
        return TransicionConPuerta(x, y, z, caraX: false) || TransicionConPuerta(x, y, z, caraX: true);
    }

    /// <summary>
    /// REGLA ÚNICA de los cierres/puertas: solo en CAMBIOS DE SECCIÓN. La cara Z- (o X-) de
    /// esta celda lleva puerta si: (1) es un límite de macro chunk (lz==0 / lx==0), (2) ambos
    /// lados son caminables (hay conexión real), (3) los dos chunks son de TIPO DISTINTO
    /// (recto→T, pasillo→vivienda, entrada a sala grande, etc. — dentro de la misma sección
    /// nunca hay puerta), y (4) pasa el roll determinista de probabilidadCierre. Las bocas al
    /// void (9) se excluyen: esas las maneja prefabsPuertaBocaVacio.
    /// </summary>
    private bool TransicionConPuerta(int x, int y, int z, bool caraX)
    {
        int ly = (y % macroSize + macroSize) % macroSize;
        if (ly != 1) return false;

        // (1) La cara debe cruzar un límite de macro chunk
        int lFace = caraX ? ((x % macroSize + macroSize) % macroSize)
                          : ((z % macroSize + macroSize) % macroSize);
        if (lFace != 0) return false;

        int nx = caraX ? x - 1 : x;
        int nz = caraX ? z : z - 1;

        // (2) Ambos lados caminables — la puerta va sobre una conexión real
        if (!IsCellEmpty(x, y, z) || !IsCellEmpty(nx, y, nz)) return false;

        // (3) Cambio de tipo entre los dos chunks (y ninguno es void)
        int mx = Mathf.FloorToInt((float)x / macroSize);
        int my = Mathf.FloorToInt((float)y / macroSize);
        int mz = Mathf.FloorToInt((float)z / macroSize);
        int nmx = Mathf.FloorToInt((float)nx / macroSize);
        int nmz = Mathf.FloorToInt((float)nz / macroSize);
        int tipoA = ObtenerTipoMacroChunk(mx, my, mz);
        int tipoB = ObtenerTipoMacroChunk(nmx, my, nmz);
        if (tipoA == tipoB) return false;
        if (tipoA == 9 || tipoB == 9) return false;

        // (4) Roll determinista por cara
        int hashCierre = semillaDelMundo + x * 31456 + y * 78923 + z * 12789 + (caraX ? 7 : 913);
        Random.State estadoViejo = Random.state;
        Random.InitState(hashCierre);
        bool debeGenerar = Random.value <= probabilidadCierre;
        Random.state = estadoViejo;
        return debeGenerar;
    }

    private void IntentarGenerarCierrePasillo(Vector3Int celda, int x, int y, int z, int tipo, int lx, int ly, int lz, Vector3 origin)
    {
        // Solo a nivel de pasillos/salas
        if (ly != 1) return;

        // REGLA ÚNICA: puertas solo en CAMBIOS DE SECCIÓN (límite de chunk + tipos distintos
        // + conexión caminable + roll). Ver TransicionConPuerta — misma función que el predictor.
        bool puertaZ = TransicionConPuerta(x, y, z, caraX: false);
        bool puertaX = TransicionConPuerta(x, y, z, caraX: true);
        if (!puertaZ && !puertaX) return;

        Vector3 centerCell = origin + new Vector3(x * cellSize + (cellSize / 2f), y * cellSize + (cellSize / 2f), z * cellSize + (cellSize / 2f));

        // Helper function para instanciar cierres
        System.Action<Vector3, float> InstanciarCierre = (Vector3 pos, float yaw) =>
        {
            Quaternion rot = Quaternion.Euler(0, yaw, 0);
            if (prefabsCierrePasillo != null && prefabsCierrePasillo.Length > 0)
            {
                GameObject prefab = prefabsCierrePasillo[Random.Range(0, prefabsCierrePasillo.Length)];
                GameObject go = Instantiate(prefab, pos, rot, transform);
                go.name = prefab.name + "_cierre";
                go.SetActive(false);
                RegistrarEnCelda(celda, go);
            }
            else
            {
                GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                placeholder.transform.SetParent(transform);
                placeholder.transform.position = pos;
                placeholder.transform.rotation = rot;
                placeholder.transform.localScale = new Vector3(cellSize * 0.9f, cellSize * 0.9f, 0.2f);
                placeholder.name = "CierrePlaceholder_cierre";
                Renderer r = placeholder.GetComponent<Renderer>();
                if (r != null)
                {
                    Material mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.5f, 0.3f, 0.7f);
                    r.sharedMaterial = mat;
                }
                placeholder.SetActive(false);
                RegistrarEnCelda(celda, placeholder);
            }
        };

        // Puerta en la cara Z- (transición de sección hacia atrás)
        if (puertaZ)
        {
            Vector3 pos = centerCell + Vector3.back * (cellSize / 2f - cierreOffset);
            InstanciarCierre(pos, 180f);
        }
        // Puerta en la cara X- (transición de sección hacia la izquierda)
        if (puertaX)
        {
            Vector3 pos = centerCell + Vector3.left * (cellSize / 2f - cierreOffset);
            InstanciarCierre(pos, 270f);
        }
    }

    /// <param name="forzar">Saltea el roll de probabilidadParedExterior. Lo usa el casco de las
    /// Salas Colosales: una piel con agujeros al 18% no es un casco, se ve como parches.</param>
    private void InstanciarParedExterior(Vector3Int celda, Vector3 posBase, Vector3 dirExterior, int x, int y, int z, int faceSeed, int tipo, bool forzar = false)
    {
        if (prefabsParedExterior == null || prefabsParedExterior.Length == 0) return;

        // Si es adyacente a una esquina cóncava, usar el set "End" (paredes más angostas que no se solapan).
        // Si no hay prefabs End asignados, se salta (compatibilidad con setup anterior).
        bool esConcava = EsParedAdyacenteEsquinaConcava(x, y, z, dirExterior);

        // Esquina cóncava VERTICAL: la masa sólida que respalda la pared termina arriba o abajo
        // (ej. cara alta expuesta a Sala Grande/Abismo, borde superior de la mega-estructura).
        // La pared completa se solaparía con el trim del techo/piso → usar End girada 90°.
        // Prioridad: si ya es End horizontal, se respeta esa (no se combinan).
        bool esConcavaV = false;
        int signoLadoLargoV = 0;
        if (!esConcava)
        {
            bool actualVacioV = IsCellEmpty(x, y, z);
            int xsV = x, zsV = z;
            if (dirExterior == Vector3.left) xsV = actualVacioV ? x - 1 : x;
            else if (dirExterior == Vector3.right) xsV = actualVacioV ? x + 1 : x;
            else if (dirExterior == Vector3.back) zsV = actualVacioV ? z - 1 : z;
            else if (dirExterior == Vector3.forward) zsV = actualVacioV ? z + 1 : z;

            bool vacioArriba = IsCellEmpty(xsV, y + 1, zsV);
            bool vacioAbajo  = IsCellEmpty(xsV, y - 1, zsV);
            if (vacioArriba != vacioAbajo) // XOR: si termina en AMBOS lados es pared aislada de 1 celda → completa
            {
                esConcavaV = true;
                signoLadoLargoV = vacioArriba ? -1 : 1; // masa termina arriba → tramo largo hacia abajo
            }
        }

        bool usaEnd = esConcava || esConcavaV;
        if (usaEnd && (prefabsParedExteriorEnd == null || prefabsParedExteriorEnd.Length == 0))
        {
            return;
        }

        // Si esta cara toca un balcón (en esta celda o en la vecina que comparte la pared), no colocar
        // la pared End: el balcón ya abrió ese lado y la End cliparía con su baranda. Solo aplica a los
        // tramos cóncavos (End); los tramos rectos normales no chocan con balcones.
        if (usaEnd)
        {
            Vector3Int vecino = (faceSeed == 0) ? new Vector3Int(x - 1, y, z) : new Vector3Int(x, y, z - 1);
            if (DireccionBalcon(x, y, z) != Vector3Int.zero
                || DireccionBalcon(vecino.x, vecino.y, vecino.z) != Vector3Int.zero)
                return;
        }

        int hash = semillaDelMundo
                   + x * 91823
                   + y * 13441
                   + z * 44123
                   + faceSeed * 773
                   + Mathf.RoundToInt(paredExteriorOffset * 100f);

        Random.State estadoViejo = Random.state;
        Random.InitState(hash);

        bool debeGenerar = forzar || Random.value <= probabilidadParedExterior;
        if (!debeGenerar)
        {
            Random.state = estadoViejo;
            return;
        }

        // Elegir array según si es esquina cóncava (End, horizontal o vertical) o tramo recto (normal)
        GameObject[] arrayParedExt = usaEnd ? prefabsParedExteriorEnd : prefabsParedExterior;

        if (arrayParedExt == null || arrayParedExt.Length == 0)
        {
            Random.state = estadoViejo;
            return;
        }

        GameObject prefab = arrayParedExt[Random.Range(0, arrayParedExt.Length)];
        if (prefab == null)
        {
            Random.state = estadoViejo;
            return;
        }

        Vector3 pos = posBase + dirExterior.normalized * paredExteriorOffset;
        bool esEjeX = dirExterior == Vector3.left || dirExterior == Vector3.right;
        // La pared exterior mira hacia afuera usando la normal de la cara de la celda.
        Quaternion rot = Quaternion.LookRotation(dirExterior.normalized, Vector3.up);

        // Pared End: el prefab es una pared normal con un tramo removido (mismo pivote), así que va
        // EXACTAMENTE donde la pared completa — sin offsets. Solo se decide el ESPEJO: el hueco debe
        // apuntar hacia la esquina. Como el prefab es simétrico verticalmente, el espejo se logra con
        // un roll local de 180° (voltea izquierda-derecha; el volteo vertical no se nota).
        int signoLadoLargo = esConcava ? SignoLadoLargoPared(x, y, z, dirExterior) : 0;
        if (esConcava && signoLadoLargo != 0)
        {
            // End horizontal: espejo con roll 180° cuando la esquina cae del lado modelado.
            if (signoLadoLargo < 0) rot = rot * Quaternion.Euler(0f, 0f, 180f);
        }
        else if (esConcavaV && signoLadoLargoV != 0)
        {
            // End VERTICAL: mismo prefab girado 90° sobre su normal — el hueco lateral del modelo
            // pasa a apuntar arriba/abajo, hacia donde termina la masa. Como el panel es cuadrado
            // (cellSize×cellSize) el giro no cambia su huella.
            bool huecoArriba = signoLadoLargoV > 0;
            rot = rot * Quaternion.Euler(0f, 0f, huecoArriba ? 90f : -90f);
        }

        GameObject go = Instantiate(prefab, pos, rot, transform);
        go.name = prefab.name + "_externo";
        go.SetActive(false);
        RegistrarEnCelda(celda, go);

        // Pilares: para paredes End se restringen a la mitad del tramo largo (horizontal o vertical).
        InstanciarPilarParaParedExterior(celda, pos, hash, dirExterior, usaEnd, signoLadoLargo, signoLadoLargoV);
        // Aristas exteriores: pasillos rectos según su eje, y también L (4), + (5) y T (8) en
        // cualquier eje — sus tramos mezclan ambas orientaciones. Los checks internos de
        // continuidad de InstanciarAristasParaParedExterior evitan colocar en esquinas/quiebres.
        bool aristaValida = (tipo == 2 && esEjeX) || (tipo == 3 && !esEjeX)
                          || tipo == 4 || tipo == 5 || tipo == 8;
        if (generarAristasExteriores)
        {
            if (aristaValida && IsCellEmpty(x, y, z))
            {
                InstanciarAristasParaParedExterior(celda, pos, dirExterior, x, y, z);
            }
            else
            {
                // Verificar si la celda vacía (interior) pertenece a una sala (tipo 1) en nivel habitable (1 o 3)
                bool actualVacio = IsCellEmpty(x, y, z);
                int x_empty = actualVacio ? x : x - Mathf.RoundToInt(dirExterior.x);
                int y_empty = y;
                int z_empty = actualVacio ? z : z - Mathf.RoundToInt(dirExterior.z);

                int emx = Mathf.FloorToInt((float)x_empty / macroSize);
                int emy = Mathf.FloorToInt((float)y_empty / macroSize);
                int emz = Mathf.FloorToInt((float)z_empty / macroSize);

                int tipoEmpty = ObtenerTipoMacroChunk(emx, emy, emz);
                int ely = (y_empty % macroSize + macroSize) % macroSize;

                // Sala Chica: spawnear aristas en ambos pisos (ly=1 y ly=3) que son los niveles habitables.
                if (tipoEmpty == 1 && (ely == 1 || ely == 3) && !esConcava)
                {
                    float hs = cellSize * 0.5f;
                    Vector3 posTecho = pos + Vector3.up * hs;
                    InstanciarArista(celda, prefabsAristaExteriorTecho, posTecho, rot, "_techo");

                    Vector3 posPiso = pos - Vector3.up * hs;
                    InstanciarArista(celda, prefabsAristaExteriorPiso, posPiso, rot, "_piso");
                }
            }
        }


        Random.state = estadoViejo;
    }

    private void InstanciarPilarParaParedExterior(Vector3Int celda, Vector3 paredPos, int hash, Vector3 normalExterior, bool esEnd = false, int signoLadoLargo = 0, int signoLadoLargoV = 0)
    {
        if (prefabsPilaresParedExterior == null || prefabsPilaresParedExterior.Length == 0) return;

        // Base de ejes robusta para CUALQUIER normal (paredes: horizontal; techo/piso: vertical).
        // Con normal vertical, LookRotation(normal, up) degenera → usar forward como up-hint.
        Vector3 n = normalExterior.normalized;
        Vector3 upHint = Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up;
        Quaternion rot = Quaternion.LookRotation(n, upHint);
        // Ejes paralelos a la cara: para paredes, paralelo = horizontal y paraleloB = up (idéntico
        // al comportamiento original); para techo/piso ambos barren el plano XZ.
        Vector3 paralelo = Vector3.Cross(upHint, n).normalized;
        Vector3 paraleloB = Vector3.Cross(n, paralelo).normalized;

        // Entre 3 y 6 pilares repartidos al azar a lo ancho Y alto de la cara.
        // Paredes End (más cortas): solo 3 pilares, y solo en la mitad que apunta al tramo
        // largo del pasillo (lejos de la esquina), para que no queden desfasados de la pared.
        int cantidad = esEnd ? 3 : Random.Range(3, 7);
        float minH = -cellSize * 0.45f, maxH = cellSize * 0.45f;
        if (esEnd && signoLadoLargo != 0)
        {
            if (signoLadoLargo > 0) minH = 0f; // tramo largo hacia +paralelo → solo esa mitad
            else maxH = 0f;                    // tramo largo hacia -paralelo → solo esa mitad
        }
        // End vertical: restringir el reparto en el eje vertical de la cara (paraleloB = up en paredes)
        float minV = -cellSize * 0.45f, maxV = cellSize * 0.45f;
        if (esEnd && signoLadoLargoV != 0)
        {
            if (signoLadoLargoV > 0) minV = 0f; // tramo largo hacia arriba → solo mitad superior
            else maxV = 0f;                     // tramo largo hacia abajo → solo mitad inferior
        }

        for (int i = 0; i < cantidad; i++)
        {
            GameObject prefabPilar = prefabsPilaresParedExterior[Random.Range(0, prefabsPilaresParedExterior.Length)];
            if (prefabPilar == null) continue;

            float offsetH = Random.Range(minH, maxH); // a lo ancho (acotado si es End horizontal)
            float offsetV = Random.Range(minV, maxV); // a lo alto (acotado si es End vertical; 2do eje XZ en techo/piso)
            Vector3 posPilar = paredPos + paralelo * offsetH + paraleloB * offsetV;

            GameObject pilar = Instantiate(prefabPilar, posPilar, rot, transform);
            pilar.name = prefabPilar.name + "_soporte_externo";
            pilar.SetActive(false);
            RegistrarEnCelda(celda, pilar);
        }
    }

    private void InstanciarAristasParaParedExterior(Vector3Int celda, Vector3 paredPos, Vector3 dirExterior, int x, int y, int z, bool forzar = false)
    {
        // Si es esquina cóncava, usar set "End" (aristas más angostas). Si no hay End asignados, skip.
        bool esConcava = EsParedAdyacenteEsquinaConcava(x, y, z, dirExterior);
        if (esConcava)
        {
            bool hayTechoEnd = prefabsAristaExteriorTechoEnd != null && prefabsAristaExteriorTechoEnd.Length > 0;
            bool hayPisoEnd  = prefabsAristaExteriorPisoEnd  != null && prefabsAristaExteriorPisoEnd.Length > 0;
            if (!hayTechoEnd && !hayPisoEnd) return; // ningún End disponible
        }

        bool esEjeX = dirExterior == Vector3.left || dirExterior == Vector3.right;

        Quaternion rotArista = Quaternion.LookRotation(dirExterior.normalized, Vector3.up);

        Vector3Int nd = new Vector3Int(Mathf.RoundToInt(dirExterior.x), 0, Mathf.RoundToInt(dirExterior.z));

        if (!forzar)
        {
            // Detectar vecinos: si tiene cualquier vecino, no generar aristas
            bool paredArriba = IsCellEmpty(x, y + 1, z) != IsCellEmpty(x + nd.x, y + 1, z + nd.z);
            bool paredAbajo  = IsCellEmpty(x, y - 1, z) != IsCellEmpty(x + nd.x, y - 1, z + nd.z);
            Vector3Int perp  = esEjeX ? new Vector3Int(0, 0, 1) : new Vector3Int(1, 0, 0);
            bool paredIzq    = IsCellEmpty(x + perp.x, y, z + perp.z) != IsCellEmpty(x + perp.x + nd.x, y, z + perp.z + nd.z);
            bool paredDer    = IsCellEmpty(x - perp.x, y, z - perp.z) != IsCellEmpty(x - perp.x + nd.x, y, z - perp.z + nd.z);
            if (paredArriba || paredAbajo || paredIzq || paredDer) return;
        }

        // La arista de techo va en la CARA SUPERIOR de la pared exterior (+hs en Y),
        // la de piso en la CARA INFERIOR (-hs). Más el offset fino del inspector (relativo a la cara).
        float hs = cellSize * 0.5f;

        // Elegir array según si es esquina cóncava (End) o tramo recto (normal)
        GameObject[] arrayAristaTecho = esConcava ? prefabsAristaExteriorTechoEnd : prefabsAristaExteriorTecho;
        GameObject[] arrayAristaPiso  = esConcava ? prefabsAristaExteriorPisoEnd  : prefabsAristaExteriorPiso;

        int mx = Mathf.FloorToInt((float)x / macroSize);
        int my = Mathf.FloorToInt((float)y / macroSize);
        int mz = Mathf.FloorToInt((float)z / macroSize);
        int tipo = ObtenerTipoMacroChunk(mx, my, mz);



        Vector3 posTecho = paredPos + Vector3.up * hs;
        InstanciarArista(celda, arrayAristaTecho, posTecho, rotArista, "_techo");

        // Si esta cara es la apertura de un balcón, NO colocar la arista inferior:
        // la plataforma/baranda del balcón ocupa ese borde y la arista la atraviesa.
        // (La celda vacía del par puede ser la actual o la vecina, según desde qué lado se llamó.)
        int exB = x, ezB = z;
        if (!IsCellEmpty(x, y, z)) { exB = x - nd.x; ezB = z - nd.z; }
        bool balconEnCara = DireccionBalcon(exB, y, ezB) == nd;

        if (!balconEnCara)
        {
            Vector3 posPiso = paredPos - Vector3.up * hs;
            InstanciarArista(celda, arrayAristaPiso, posPiso, rotArista, "_piso");
        }
    }

    private void InstanciarArista(Vector3Int celda, GameObject[] prefabs, Vector3 pos, Quaternion rot, string sufijo)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        if (prefab == null) return;

        GameObject go = Instantiate(prefab, pos, rot, transform);
        go.name = prefab.name + sufijo;
        go.SetActive(false);
        RegistrarEnCelda(celda, go);
    }


    /// <summary>
    /// Spawnea 1-N luces colgando del techo de una celda del 3x3 de Sala Grande.
    /// La configuración (prefabs, rango de cantidad, offset) viene del RoomData de la sala.
    /// Cantidad y posiciones son deterministas por celda + semilla del mundo.
    /// </summary>
    private void GenerarLucesTechoSala(Vector3Int celda, int x, int y, int z, Vector3 origin, RoomData sala)
    {
        int hash = semillaDelMundo + x * 84621 + y * 49317 + z * 13267;
        Random.State prev = Random.state;
        Random.InitState(hash);

        int minC = Mathf.Max(0, sala.cantidadLuces.x);
        int maxC = Mathf.Max(minC, sala.cantidadLuces.y);
        int cantidad = Random.Range(minC, maxC + 1);

        float techoY = origin.y + (y + 1) * cellSize - sala.luzOffsetTecho;
        float baseX = origin.x + x * cellSize;
        float baseZ = origin.z + z * cellSize;

        for (int i = 0; i < cantidad; i++)
        {
            GameObject prefab = sala.prefabsLuces[Random.Range(0, sala.prefabsLuces.Length)];
            if (prefab == null) continue;

            // Posición dentro de la celda, con un margen para evitar pegarse a las paredes
            float lx = Random.Range(cellSize * 0.25f, cellSize * 0.75f);
            float lz = Random.Range(cellSize * 0.25f, cellSize * 0.75f);
            Vector3 pos = new Vector3(baseX + lx, techoY, baseZ + lz);

            GameObject go = Instantiate(prefab, pos, Quaternion.identity, transform);
            go.name = prefab.name + $"_luz_sala_{i}";
            go.SetActive(false);
            RegistrarEnCelda(celda, go);
        }

        Random.state = prev;
    }

    /// <param name="forzar">Saltea el roll de probabilidadPisoExterior (casco de Sala Colosal).</param>
    private void InstanciarPisoExterior(Vector3Int celda, Vector3 posBase, int x, int y, int z, bool forzar = false)
    {
        if (prefabsPisoExterior == null || prefabsPisoExterior.Length == 0) return;

        int hash = semillaDelMundo + x * 55291 + y * 33847 + z * 71923;
        Random.State estadoViejo = Random.state;
        Random.InitState(hash);

        if (!forzar && Random.value > probabilidadPisoExterior)
        {
            Random.state = estadoViejo;
            return;
        }

        GameObject prefab = prefabsPisoExterior[Random.Range(0, prefabsPisoExterior.Length)];
        if (prefab == null)
        {
            Random.state = estadoViejo;
            return;
        }

        Vector3 pos = posBase + Vector3.down * paredExteriorOffset;
        float rotY = Random.Range(0, 4) * 90f;
        GameObject go = Instantiate(prefab, pos, Quaternion.Euler(0, rotY, 0), transform);
        go.name = prefab.name + "_piso_externo";
        go.SetActive(false);
        RegistrarEnCelda(celda, go);

        // Pilares entre el piso del pasillo y la placa exterior (mismo prefab de pared, girado)
        if (pilaresEnTechoPisoExterior)
            InstanciarPilarParaParedExterior(celda, pos, hash, Vector3.down);

        Random.state = estadoViejo;
    }

    /// <param name="forzar">Saltea el roll de probabilidadTechoExterior (casco de Sala Colosal).</param>
    private void InstanciarTechoExterior(Vector3Int celda, Vector3 posBase, int x, int y, int z, bool forzar = false)
    {
        if (prefabsTechoExterior == null || prefabsTechoExterior.Length == 0) return;

        int hash = semillaDelMundo + x * 41879 + y * 92837 + z * 26431;
        Random.State estadoViejo = Random.state;
        Random.InitState(hash);

        if (!forzar && Random.value > probabilidadTechoExterior)
        {
            Random.state = estadoViejo;
            return;
        }

        GameObject prefab = prefabsTechoExterior[Random.Range(0, prefabsTechoExterior.Length)];
        if (prefab == null)
        {
            Random.state = estadoViejo;
            return;
        }

        Vector3 pos = posBase + Vector3.up * paredExteriorOffset;
        float rotY = 0f;
        GameObject go = Instantiate(prefab, pos, Quaternion.Euler(0, rotY, 0), transform);
        go.name = prefab.name + "_techo_externo";
        go.SetActive(false);
        RegistrarEnCelda(celda, go);

        // Pilares entre el techo del pasillo y la placa exterior (mismo prefab de pared, girado)
        if (pilaresEnTechoPisoExterior)
            InstanciarPilarParaParedExterior(celda, pos, hash, Vector3.up);

        Random.state = estadoViejo;
    }

    /// <summary>Dibuja cada CELDA activa (no macro chunk) como wire cube — solo en Play, con Gizmos on en la Scene view.</summary>
    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            Vector3 origin = transform.position;
            foreach(Vector3Int celda in celdasActivas)
            {
                Vector3 cellCenter = origin + new Vector3(
                    celda.x * cellSize + (cellSize / 2f),
                    celda.y * cellSize + (cellSize / 2f),
                    celda.z * cellSize + (cellSize / 2f)
                );

                Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f); 
                Gizmos.DrawWireCube(cellCenter, Vector3.one * cellSize);

                if (celda == celdaSpawn)
                {
                    Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
                    Gizmos.DrawCube(cellCenter, Vector3.one * (cellSize * 0.95f)); 
                }
            }
        }
    }

    /// <summary>
    /// Busca una sala (tipo 1) aleatoria dentro de un rango de macro chunks respecto a una posición del mundo,
    /// de manera determinista usando una semilla provista, y devuelve su nombre de sala y sus coordenadas formateadas.
    /// </summary>
    public bool ObtenerSalaAleatoriaDeterminista(Vector3 posicionCentro, float radioMetros, int semillaNota, out string nombreSala, out string coordsFormateadas)
    {
        nombreSala = "Zona Desconocida";
        coordsFormateadas = "0 / PISO 0 / 00";

        if (salasGrandesPosibles == null || salasGrandesPosibles.Length == 0) return false;

        // Convertir posición centro a coordenadas de macro chunk
        int centroMX = Mathf.FloorToInt(posicionCentro.x / (cellSize * macroSize));
        int centroMY = Mathf.FloorToInt(posicionCentro.y / (cellSize * macroSize));
        int centroMZ = Mathf.FloorToInt(posicionCentro.z / (cellSize * macroSize));

        int rangoMacro = Mathf.CeilToInt(radioMetros / (cellSize * macroSize));
        if (rangoMacro < 1) rangoMacro = 1;

        List<Vector3Int> macrosSala = new List<Vector3Int>();
        for (int mx = centroMX - rangoMacro; mx <= centroMX + rangoMacro; mx++)
        {
            for (int my = centroMY - rangoMacro; my <= centroMY + rangoMacro; my++)
            {
                for (int mz = centroMZ - rangoMacro; mz <= centroMZ + rangoMacro; mz++)
                {
                    // Si el generador considera que este macro chunk es de tipo 1 (Sala)
                    if (ObtenerTipoMacroChunk(mx, my, mz) == 1)
                    {
                        macrosSala.Add(new Vector3Int(mx, my, mz));
                    }
                }
            }
        }

        if (macrosSala.Count == 0) return false;

        // Inicializar estado aleatorio determinista
        Random.State prev = Random.state;
        Random.InitState(semillaNota);

        // Elegir una macro chunk de forma determinista
        Vector3Int elegido = macrosSala[Random.Range(0, macrosSala.Count)];

        // Obtener la sala asignada deterministamente
        RoomData dataSala = ObtenerRoomDataParaMacro(elegido.x, elegido.y, elegido.z);
        if (dataSala != null)
        {
            nombreSala = dataSala.nombreSala;
        }

        // Restaurar estado aleatorio anterior
        Random.state = prev;

        // Formatear coordenadas de la celda central de esa sala (lx=2, ly=1, lz=2)
        int cellX = elegido.x * macroSize + 2;
        int cellY = elegido.y * macroSize + 1;
        int cellZ = elegido.z * macroSize + 2;

        string parteX = cellX.ToString();
        // Usar formato de letra para X (copiado de CartelCoordenadas para coherencia)
        char letra = (char)('A' + Mathf.Abs(cellX) % 26);
        parteX = letra.ToString();

        string parteY = $"PISO {cellY}";
        string parteZ = cellZ.ToString("D2");

        coordsFormateadas = $"{parteX} / {parteY} / {parteZ}";
        return true;
    }

    private bool ObtenerCeldaDestinoMision(int semillaNota, out Vector3Int celdaDestino)
    {
        celdaDestino = Vector3Int.zero;
        if (salasGrandesPosibles == null || salasGrandesPosibles.Length == 0) return false;

        // Buscamos en un rango amplio de macro chunks alrededor del spawn (0,0,0)
        int rangoMacro = 15; 
        List<Vector3Int> macrosSala = new List<Vector3Int>();
        for (int mx = -rangoMacro; mx <= rangoMacro; mx++)
        {
            for (int my = -rangoMacro; my <= rangoMacro; my++)
            {
                for (int mz = -rangoMacro; mz <= rangoMacro; mz++)
                {
                    if (ObtenerTipoMacroChunk(mx, my, mz) == 1)
                    {
                        macrosSala.Add(new Vector3Int(mx, my, mz));
                    }
                }
            }
        }

        if (macrosSala.Count == 0) return false;

        Random.State prev = Random.state;
        Random.InitState(semillaNota);
        Vector3Int elegido = macrosSala[Random.Range(0, macrosSala.Count)];
        Random.state = prev;

        // Celda central de la sala (lx=2, ly=1, lz=2)
        celdaDestino = new Vector3Int(elegido.x * macroSize + 2, elegido.y * macroSize + 1, elegido.z * macroSize + 2);
        return true;
    }

    // ============================================================
    //  MISIONES ENCADENADAS (cadenas de notas)
    // ============================================================

    /// <summary>
    /// Registra la misión de una nota encadenada. La celda donde spawneó la nota es el punto
    /// inicial; el destino se calcula determinista por semilla a ~distanciaMisionCeldas en
    /// cualquier dirección, ajustado a la celda caminable (vacía con piso) más cercana.
    /// Idempotente por NotaData: si otra copia de la misma nota ya registró, se ignora.
    /// </summary>
    public void RegistrarMisionEncadenada(NotaData nota, Vector3Int celdaInicial)
    {
        if (nota == null || !nota.esMisionEncadenada) return;
        if (destinoPorNota.ContainsKey(nota)) return; // ya registrada por otra copia

        int hash = semillaDelMundo + nota.titulo.GetHashCode() * 31
                 + celdaInicial.x * 73856 + celdaInicial.y * 19349 + celdaInicial.z * 83492;
        Random.State prev = Random.state;
        Random.InitState(hash);
        float ang = Random.Range(0f, Mathf.PI * 2f);
        int dist = Mathf.Max(5, nota.distanciaMisionCeldas);
        int dx = Mathf.RoundToInt(Mathf.Cos(ang) * dist);
        int dz = Mathf.RoundToInt(Mathf.Sin(ang) * dist);
        int dy = Random.Range(-5, 6); // algo de variación vertical
        Random.state = prev;

        Vector3Int objetivo = celdaInicial + new Vector3Int(dx, dy, dz);

        // El objetivo probablemente cae en el vacío → ajustarlo a la isla de la región
        // del objetivo (siempre existe una) antes de buscar piso.
        {
            int p = Mathf.Max(3, periodoIslas);
            int cmx = Mathf.FloorToInt((float)objetivo.x / macroSize);
            int cmy = Mathf.FloorToInt((float)objetivo.y / macroSize);
            int cmz = Mathf.FloorToInt((float)objetivo.z / macroSize);
            Vector3Int centro = CentroIsla(Mathf.FloorToInt((float)cmx / p), Mathf.FloorToInt((float)cmy / p), Mathf.FloorToInt((float)cmz / p), out _, out _);
            objetivo = new Vector3Int(centro.x * macroSize + 2, centro.y * macroSize + 1, centro.z * macroSize + 2);
        }

        Vector3Int destino = BuscarCeldaConPiso(objetivo, 15);
        destinoPorNota[nota] = destino;
        misionesEncadenadasPorCelda[destino] = nota;

        // Caso raro: la celda destino ya estaba generada (jugador ya pasó por ahí) → spawnear ya
        if (mundoChunks.ContainsKey(destino))
            SpawnRecompensaMision(destino, nota, celdasActivasSet.Contains(destino));
    }

    /// <summary>Celda destino de la misión de una nota (para que la UI muestre coordenadas).</summary>
    public bool ObtenerDestinoMision(NotaData nota, out Vector3Int destino)
    {
        if (nota != null && destinoPorNota.TryGetValue(nota, out destino)) return true;
        destino = Vector3Int.zero;
        return false;
    }

    /// <summary>
    /// Busca determinísticamente la celda caminable (vacía y con piso debajo) más cercana al
    /// objetivo, en cáscaras Chebyshev crecientes. Fallback: el objetivo mismo.
    /// </summary>
    private Vector3Int BuscarCeldaConPiso(Vector3Int objetivo, int radioMax)
    {
        for (int r = 0; r <= radioMax; r++)
        {
            for (int ox = -r; ox <= r; ox++)
                for (int oy = -r; oy <= r; oy++)
                    for (int oz = -r; oz <= r; oz++)
                    {
                        // Solo la cáscara del radio actual (el interior ya se revisó)
                        if (Mathf.Max(Mathf.Abs(ox), Mathf.Abs(oy), Mathf.Abs(oz)) != r) continue;

                        int cx = objetivo.x + ox, cy = objetivo.y + oy, cz = objetivo.z + oz;
                        if (IsCellEmpty(cx, cy, cz) && !IsCellEmpty(cx, cy - 1, cz))
                            return new Vector3Int(cx, cy, cz);
                    }
        }
        return objetivo; // sin piso cerca: la nota igual da las coordenadas (rapel mediante)
    }

    /// <summary>
    /// Spawnea la recompensa de una misión encadenada en su celda destino: cofre (si hay),
    /// materiales sueltos y la SIGUIENTE nota de la cadena (que al activarse registra su
    /// propia misión → la cadena continúa sola). Determinista por semilla+celda.
    /// </summary>
    private void SpawnRecompensaMision(Vector3Int celda, NotaData nota, bool activar)
    {
        if (!mundoChunks.ContainsKey(celda)) return;

        Vector3 origin = transform.position;
        Vector3 basePos = origin + new Vector3(
            celda.x * cellSize + cellSize / 2f,
            celda.y * cellSize + 0.15f,
            celda.z * cellSize + cellSize / 2f);

        int hash = semillaDelMundo + celda.x * 52361 + celda.y * 88301 + celda.z * 15683;
        Random.State prev = Random.state;
        Random.InitState(hash);

        // 1. Cofre
        if (nota.prefabCofre != null)
        {
            GameObject cofre = Instantiate(nota.prefabCofre, basePos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            cofre.SetActive(activar);
            RegistrarEnCelda(celda, cofre);
        }

        // 2. Materiales sueltos alrededor
        if (nota.materialesRecompensa != null && nota.materialesRecompensa.Length > 0)
        {
            int cant = Random.Range(nota.cantidadMateriales.x, nota.cantidadMateriales.y + 1);
            for (int i = 0; i < cant; i++)
            {
                GameObject prefabMat = nota.materialesRecompensa[Random.Range(0, nota.materialesRecompensa.Length)];
                if (prefabMat == null) continue;
                float a = Random.Range(0f, Mathf.PI * 2f);
                float radio = Random.Range(0.8f, 2.2f);
                Vector3 pos = basePos + new Vector3(Mathf.Cos(a) * radio, 0.3f, Mathf.Sin(a) * radio);
                GameObject mat = Instantiate(prefabMat, pos, Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)), transform);
                mat.SetActive(activar);
                RegistrarEnCelda(celda, mat);
            }
        }

        // 3. Siguiente nota de la cadena
        if (nota.notaSiguiente != null && nota.prefabNotaFisica != null)
        {
            Vector3 posNota = basePos + new Vector3(0.6f, 1.0f, 0.6f); // cae con física junto al cofre
            GameObject goNota = Instantiate(nota.prefabNotaFisica, posNota, Quaternion.Euler(0f, Random.Range(0f, 360f), 90f), transform);
            NotaMundo nm = goNota.GetComponent<NotaMundo>();
            if (nm != null) nm.data = nota.notaSiguiente;
            goNota.SetActive(activar);
            RegistrarEnCelda(celda, goNota);
        }

        Random.state = prev;
    }

    // ============================================================
    //  MODO BLAME! — ISLAS + PUENTES
    // ============================================================

    /// <summary>
    /// Centro (en macro chunks) de la isla de una región, con su radio y forma.
    /// Cada región de periodoIslas³ chunks tiene SIEMPRE una isla, jitteada en X/Z con margen
    /// para quedar contenida (así EnIsla solo chequea la región propia). En Y la isla apoya su
    /// base en el nivel inferior de la región (cy = ry*p + radio) → los puentes, que corren en
    /// my = ry*p, siempre tocan la base de cada isla sin importar su radio.
    /// La región (0,0,0) es especial: isla caja de radio máximo centrada sobre el chunk de
    /// spawn (0,0,0 queda en su cara inferior, al nivel de los puentes).
    /// </summary>
    private Vector3Int CentroIsla(int rx, int ry, int rz, out int radio, out bool esCaja)
    {
        int p = Mathf.Max(3, periodoIslas);
        int rMax = Mathf.Min(radioIslaChunks.y, (p - 1) / 2 - 1);
        rMax = Mathf.Max(1, rMax);

        if (rx == 0 && ry == 0 && rz == 0)
        {
            radio = rMax;
            esCaja = true;
            return new Vector3Int(0, rMax, 0); // spawn (0,0,0) en la cara inferior
        }

        int hash = semillaDelMundo + rx * 68111 + ry * 24851 + rz * 47657;
        Random.State prev = Random.state;
        Random.InitState(hash);
        radio = Random.Range(Mathf.Min(radioIslaChunks.x, rMax), rMax + 1);
        esCaja = Random.value < 0.5f;
        // Las salas colosales van siempre en CAJA y al radio máximo: una nave rectangular limpia
        // y lo más grande que permita el período. En esfera quedaría un hueco abombado con la
        // cáscara escalonada, y con radio chico dejaría de ser "colosal".
        if (IslaEsColosal(rx, ry, rz)) { esCaja = true; radio = rMax; }
        int margen = radio + 1;
        int cx = rx * p + Random.Range(margen, p - margen + 1);
        int cz = rz * p + Random.Range(margen, p - margen + 1);
        Random.state = prev;

        return new Vector3Int(cx, ry * p + radio, cz);
    }

    /// <summary>
    /// ¿El lado VACÍO de esta cara pertenece al VOID (tipo 9)? En modo islas, esas caras NO
    /// generan pared alguna (ni interior, ni exterior, ni aristas): el vacío queda abierto y
    /// la estructura se muestra tal cual — sin paredes contenedoras que encapsulen nada.
    /// Las estructuras conservan su piel propia (sus caras dan a pasillos/salas, no al void).
    /// </summary>
    private bool CaraDaAlVacio(bool actualVacia, int x, int y, int z, int nx, int ny, int nz)
    {
        // EXCEPCIÓN: la cáscara de una Sala Colosal sí se recubre contra el vacío. Es el casco de
        // la nave y tiene que verse desde afuera — sin esto la isla entera sería una caja
        // invisible, porque la geometría solo nace en los bordes sólido↔vacío y todos los de
        // afuera quedarían saltados. El lado SÓLIDO de la cara es el de la cáscara.
        int sx = actualVacia ? nx : x;
        int sy = actualVacia ? ny : y;
        int sz = actualVacia ? nz : z;
        if (CeldaEnIslaColosal(sx, sy, sz)) return false;

        int ex = actualVacia ? x : nx;
        int ey = actualVacia ? y : ny;
        int ez = actualVacia ? z : nz;
        return ObtenerTipoMacroChunk(
            Mathf.FloorToInt((float)ex / macroSize),
            Mathf.FloorToInt((float)ey / macroSize),
            Mathf.FloorToInt((float)ez / macroSize)) == 9;
    }

    /// <summary>
    /// Chunks de estructura de una isla CONECTADOS a su esqueleto (columna-pozo + lanes de
    /// puente), vía BFS LATERAL (ver nota sobre los dirs abajo: lo vertical solo pasa por la
    /// columna). Los candidatos que quedan aislados NO están en el set → se descartan (vuelven
    /// void). Nada de pedazos flotando sueltos en el vacío.
    /// Cacheado por región (el BFS corre una sola vez por isla, ≤343 chunks).
    /// </summary>
    private HashSet<Vector3Int> ChunksConectadosDeIsla(Vector3Int region)
    {
        if (cacheIslaConectada.TryGetValue(region, out HashSet<Vector3Int> set)) return set;

        set = new HashSet<Vector3Int>();
        Vector3Int centro = CentroIsla(region.x, region.y, region.z, out int radio, out _);

        // Semillas del BFS: la columna central (pozo) de la isla
        Queue<Vector3Int> frontera = new Queue<Vector3Int>();
        for (int dy = -radio; dy <= radio; dy++)
        {
            Vector3Int c = new Vector3Int(centro.x, centro.y + dy, centro.z);
            if (EnIsla(c.x, c.y, c.z)) { set.Add(c); frontera.Enqueue(c); }
        }

        // SOLO vecinos LATERALES. La conectividad vertical entre chunks comunes NO existe en el
        // mundo real: los conectores tallados entre chunks vecinos son laterales (los pasos a
        // lz==2 / lx==2), y el único vínculo vertical es la columna-pozo — cuyos niveles ya son
        // todos semillas del BFS. Con arriba/abajo en este BFS, un chunk podía darse por
        // "conectado" a través del chunk de encima: un enlace que no talla ningún paso ni genera
        // geometría — y quedaba un pasillo FLOTANDO solo en el void, imposible de alcanzar.
        // Regla resultante: cada chunk necesita una cadena lateral hasta la columna EN SU NIVEL.
        Vector3Int[] dirs =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };
        while (frontera.Count > 0)
        {
            Vector3Int c = frontera.Dequeue();
            foreach (Vector3Int d in dirs)
            {
                Vector3Int n = c + d;
                if (set.Contains(n)) continue;
                if (!EnIsla(n.x, n.y, n.z)) continue;
                if (!EsCandidatoEstructura(n)) continue;
                set.Add(n);
                frontera.Enqueue(n);
            }
        }

        cacheIslaConectada[region] = set;
        return set;
    }

    /// <summary>
    /// ¿Este chunk sería estructura según el branch de isla? Réplica LIGERA de esa decisión
    /// (columna/lane/porosidad) sin pasar por ObtenerTipoMacroChunk — evita recursión con el
    /// chequeo de conectividad.
    /// </summary>
    private bool EsCandidatoEstructura(Vector3Int chunk)
    {
        int p = Mathf.Max(3, periodoIslas);
        // Sala colosal: la isla entera es estructura maciza (ni porosidad ni columna-pozo aplican).
        if (EsIslaColosalDeChunk(chunk.x, chunk.y, chunk.z)) return true;
        Vector3Int centro = CentroIsla(
            Mathf.FloorToInt((float)chunk.x / p),
            Mathf.FloorToInt((float)chunk.y / p),
            Mathf.FloorToInt((float)chunk.z / p), out _, out _);
        if (chunk.x == centro.x && chunk.z == centro.z) return true;   // columna-pozo
        if (TipoPuente(chunk.x, chunk.y, chunk.z) != 0) return true;    // lane de puente
        return ObtenerDensidad(chunk.x, chunk.y, chunk.z) >= umbralVacioIsla; // porosidad
    }

    /// <summary>
    /// ¿Este chunk de isla tiene algún vecino (6 direcciones) que terminará siendo VOID?
    /// Replica la decisión del vecino con la cadena candidato+conectividad+lane (funciones
    /// puras, sin consultar su tipo final — eso recursaría entre vecinos). El remapeo de
    /// borde no afecta la void-ness del vecino (solo cambia sala→pasillo), así que es exacto.
    /// </summary>
    private bool ChunkTocaVoid(int mx, int my, int mz)
    {
        int p = Mathf.Max(3, periodoIslas);
        Vector3Int[] dirs =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };
        foreach (Vector3Int d in dirs)
        {
            int nx = mx + d.x, ny = my + d.y, nz = mz + d.z;
            bool vecinoEstructura;
            if (EnIsla(nx, ny, nz))
            {
                Vector3Int regionVecino = new Vector3Int(
                    Mathf.FloorToInt((float)nx / p),
                    Mathf.FloorToInt((float)ny / p),
                    Mathf.FloorToInt((float)nz / p));
                vecinoEstructura = EsCandidatoEstructura(new Vector3Int(nx, ny, nz))
                                && ChunksConectadosDeIsla(regionVecino).Contains(new Vector3Int(nx, ny, nz));
            }
            else
            {
                vecinoEstructura = TipoPuente(nx, ny, nz) != 0; // fuera de isla solo las lanes son estructura
            }
            if (!vecinoEstructura) return true;
        }
        return false;
    }

    /// <summary>
    /// ¿La isla de esta REGIÓN es una Sala Colosal (todo su volumen vaciado en un solo espacio)?
    /// Decisión PURA por hash: no consulta CentroIsla ni EnIsla, así CentroIsla puede llamarla
    /// para forzar la forma sin recursión. La región (0,0,0) nunca lo es — la isla del spawn
    /// tiene que ser el mundo normal, con viviendas y pasillos.
    /// </summary>
    private bool IslaEsColosal(int rx, int ry, int rz)
    {
        if (rx == 0 && ry == 0 && rz == 0) return false;
        if (probabilidadIslaColosal <= 0f) return false;

        Vector3Int region = new Vector3Int(rx, ry, rz);
        if (cacheIslaColosal.TryGetValue(region, out bool cached)) return cached;

        int hash = semillaDelMundo + rx * 15487 + ry * 32452 + rz * 49999;
        Random.State prev = Random.state;
        Random.InitState(hash);
        bool colosal = Random.value < probabilidadIslaColosal;
        Random.state = prev;

        cacheIslaColosal[region] = colosal;
        return colosal;
    }

    /// <summary>
    /// ¿La isla de esta región se generó SIN viviendas? Hash puro, mismo patrón que IslaEsColosal.
    /// La región (0,0,0) queda excluida: es donde arranca el jugador y `spawnEnVivienda` necesita
    /// que haya al menos una casa ahí.
    /// </summary>
    private bool IslaSinViviendas(int rx, int ry, int rz)
    {
        if (rx == 0 && ry == 0 && rz == 0) return false;
        if (probabilidadIslaSinViviendas <= 0f) return false;

        Vector3Int region = new Vector3Int(rx, ry, rz);
        if (cacheIslaSinViviendas.TryGetValue(region, out bool cached)) return cached;

        int hash = semillaDelMundo + rx * 26417 + ry * 60013 + rz * 78241;
        Random.State prev = Random.state;
        Random.InitState(hash);
        bool sinViviendas = Random.value < probabilidadIslaSinViviendas;
        Random.state = prev;

        cacheIslaSinViviendas[region] = sinViviendas;
        return sinViviendas;
    }

    /// <summary>
    /// Chunk elegido como CUARTEL de esta isla, o null si la isla no tiene dónde ponerlo.
    ///
    /// Escanea el volumen de la isla y se queda con los chunks de NÚCLEO — los que sobreviven a
    /// todos los forzados previos del tipado: no son la columna-pozo, no son una lane de puente,
    /// pasan el umbral de porosidad, están conectados al esqueleto y NO tocan el vacío (una sala
    /// en el borde quedaría destapada en corte contra el void, que es justo lo que evita el paso
    /// de "borde de isla → solo pasillos"). De esos, elige uno por hash de la región.
    ///
    /// Se apoya solo en las réplicas puras (EsCandidatoEstructura / ChunksConectadosDeIsla /
    /// ChunkTocaVoid), nunca en ObtenerTipoMacroChunk: si preguntara el tipo final entraría en
    /// recursión con el propio tipado que lo consulta. Cacheado por región — el escaneo corre una
    /// sola vez por isla.
    /// </summary>
    private bool TryCuartelDeIsla(Vector3Int region, out Vector3Int chunkCuartel)
    {
        if (cacheCuartelIsla.TryGetValue(region, out Vector3Int? cached))
        {
            chunkCuartel = cached ?? default;
            return cached.HasValue;
        }

        chunkCuartel = default;
        Vector3Int? resultado = null;

        // Una isla colosal es UNA sala: no hay chunks sueltos donde meter un cuartel.
        if (!IslaEsColosal(region.x, region.y, region.z))
        {
            Vector3Int centro = CentroIsla(region.x, region.y, region.z, out int radio, out _);

            // CASCADA de candidatos, del mejor lugar al último recurso. La garantía es DURA: si el
            // nivel ideal no tiene dónde, se relajan las condiciones hasta que quepa — el cuartel
            // desplaza a lo que sea (viviendas incluidas: la ruleta simplemente no corre en ese
            // chunk). Preferimos un cuartel feo a centinelas sin base.
            //   nucleo: conectado al esqueleto y sin tocar el vacío (el cuartel "perfecto").
            //   borde:  conectado pero expuesto al void — la sala queda en el casco, se aguanta.
            //   loQueSea: cualquier chunk de la isla que no sea pozo/lane/spawn, hasta poroso o
            //             desconectado. Solo se llega acá en islas degeneradas (radio 1).
            List<Vector3Int> nucleo = new List<Vector3Int>();
            List<Vector3Int> borde = new List<Vector3Int>();
            List<Vector3Int> loQueSea = new List<Vector3Int>();

            for (int mx = centro.x - radio; mx <= centro.x + radio; mx++)
                for (int my = centro.y - radio; my <= centro.y + radio; my++)
                    for (int mz = centro.z - radio; mz <= centro.z + radio; mz++)
                    {
                        // El chunk del spawn está forzado a Sala Chica normal; no puede ser cuartel
                        // o el jugador arrancaría dentro de la sala de los vigías.
                        if (mx == 0 && my == 0 && mz == 0) continue;
                        if (!EnIsla(mx, my, mz)) continue;
                        if (mx == centro.x && mz == centro.z) continue;      // columna-pozo
                        if (TipoPuente(mx, my, mz) != 0) continue;           // lane de puente

                        Vector3Int c = new Vector3Int(mx, my, mz);
                        loQueSea.Add(c);
                        if (!EsCandidatoEstructura(c)) continue;             // porosidad
                        if (!ChunksConectadosDeIsla(region).Contains(c)) continue;
                        if (ChunkTocaVoid(mx, my, mz)) borde.Add(c);
                        else nucleo.Add(c);
                    }

            List<Vector3Int> candidatos = nucleo.Count > 0 ? nucleo
                                        : borde.Count > 0 ? borde
                                        : loQueSea;

            if (candidatos.Count > 0)
            {
                int hash = semillaDelMundo + region.x * 84131 + region.y * 22079 + region.z * 53411;
                Random.State prev = Random.state;
                Random.InitState(hash);
                resultado = candidatos[Random.Range(0, candidatos.Count)];
                Random.state = prev;
            }
            else
            {
                // Solo alcanzable si la isla ENTERA es columna-pozo + lanes (más el chunk de spawn).
                // Con periodoIslas >= 3 y radio >= 1 no debería pasar nunca — si aparece, la
                // configuración de islas está rota de raíz.
                Debug.LogWarning($"[WorldGenerator] La isla de la región {region} quedó SIN cuartel: no tiene NI UN chunk disponible fuera de la columna-pozo y las lanes de puente. Revisá periodoIslas/radioIslaChunks — esto no debería ser posible en una configuración sana.");
            }
        }

        cacheCuartelIsla[region] = resultado;
        chunkCuartel = resultado ?? default;
        return resultado.HasValue;
    }

    /// <summary>¿Este macro chunk es el cuartel garantizado de su isla?</summary>
    private bool EsChunkCuartelDeIsla(int mx, int my, int mz)
    {
        if (!cuartelGarantizadoPorIsla) return false;
        if (!EnIsla(mx, my, mz)) return false;
        Vector3Int region = RegionDeChunk(mx, my, mz);
        return TryCuartelDeIsla(region, out Vector3Int chunk) && chunk == new Vector3Int(mx, my, mz);
    }

    /// <summary>
    /// Los chunks que serán Sala Chica en esta isla — CUARTEL INCLUIDO. Se decide una sola vez por
    /// isla (cacheado) en vez de chunk por chunk, que es lo que permite un tope duro: antes cada
    /// chunk del núcleo tiraba sus propios dados contra `umbralDensidadAlta`, así que la cantidad
    /// de viviendas dependía del tamaño de la isla y podía dispararse a decenas.
    ///
    /// Solo se eligen chunks del NÚCLEO (conectados, no porosos, sin tocar el vacío): una sala en
    /// el borde quedaría destapada contra el void — misma razón que la regla de borde del tipado.
    /// El cuartel es la excepción: puede caer en borde si no hay lugar mejor, pero eso ya lo
    /// resuelve TryCuartelDeIsla con su propia cascada.
    ///
    /// No hay recursión: igual que TryCuartelDeIsla, esto usa la cadena candidato/conectividad/lane
    /// y NUNCA ObtenerTipoMacroChunk.
    /// </summary>
    private HashSet<Vector3Int> SalasDeIsla(Vector3Int region)
    {
        if (cacheSalasIsla.TryGetValue(region, out HashSet<Vector3Int> cached)) return cached;

        HashSet<Vector3Int> set = new HashSet<Vector3Int>();

        // Una isla colosal es UNA sola sala: no hay chunks sueltos donde repartir viviendas.
        if (IslaEsColosal(region.x, region.y, region.z))
        {
            cacheSalasIsla[region] = set;
            return set;
        }

        // El cuartel entra siempre y ocupa uno de los cupos.
        if (TryCuartelDeIsla(region, out Vector3Int cuartel)) set.Add(cuartel);

        int minSalas = Mathf.Max(0, Mathf.Min(salasPorIsla.x, salasPorIsla.y));
        int maxSalas = Mathf.Max(minSalas, Mathf.Max(salasPorIsla.x, salasPorIsla.y));

        // Isla deshabitada: se queda solo con el cuartel, sin viviendas.
        if (IslaSinViviendas(region.x, region.y, region.z))
        {
            cacheSalasIsla[region] = set;
            return set;
        }

        Vector3Int centro = CentroIsla(region.x, region.y, region.z, out int radio, out _);

        List<Vector3Int> candidatos = new List<Vector3Int>();
        List<Vector3Int> borde = new List<Vector3Int>();   // casco de la isla: respaldo, no primera opción
        for (int mx = centro.x - radio; mx <= centro.x + radio; mx++)
            for (int my = centro.y - radio; my <= centro.y + radio; my++)
                for (int mz = centro.z - radio; mz <= centro.z + radio; mz++)
                {
                    // El chunk de spawn ya está forzado a Sala Chica desde el tipado.
                    if (mx == 0 && my == 0 && mz == 0) continue;
                    if (!EnIsla(mx, my, mz)) continue;
                    if (mx == centro.x && mz == centro.z) continue;   // columna-pozo
                    if (TipoPuente(mx, my, mz) != 0) continue;        // lane de puente

                    Vector3Int c = new Vector3Int(mx, my, mz);
                    if (set.Contains(c)) continue;                    // ya es el cuartel
                    if (!EsCandidatoEstructura(c)) continue;          // porosidad
                    if (!ChunksConectadosDeIsla(region).Contains(c)) continue;

                    // ⚠️ Antes acá había un `if (ChunkTocaVoid(...)) continue;` que descartaba todo
                    // lo que no fuera núcleo. En islas CHICAS eso vacía la lista entera: una isla de
                    // radio 1 es 3x3x3, o sea que TODOS sus chunks tocan el vacío y el único interior
                    // sería el centro, ya excluido por ser la columna-pozo. Resultado: cero viviendas
                    // en la isla, solo el cuartel (que sí tenía su propia cascada de respaldo) — y
                    // como las viviendas son las únicas que pueden emparejarse, ningún NPC daba
                    // misión en todo el mundo.
                    //
                    // Ahora el borde es respaldo, igual que en TryCuartelDeIsla: se prefiere núcleo,
                    // pero si no alcanza se usa el casco antes que dejar la isla sin gente.
                    if (ChunkTocaVoid(mx, my, mz)) borde.Add(c);
                    else candidatos.Add(c);
                }

        int hash = semillaDelMundo + region.x * 27653 + region.y * 51749 + region.z * 83231;
        Random.State prev = Random.state;
        Random.InitState(hash);

        int objetivo = Random.Range(minSalas, maxSalas + 1);

        // Barajado COMPLETO (Fisher-Yates): ya no alcanza con tomar los primeros N, porque hay que
        // ir descartando los que caen demasiado cerca de los ya elegidos.
        for (int i = candidatos.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidatos[i], candidatos[j]) = (candidatos[j], candidatos[i]);
        }
        for (int i = borde.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (borde[i], borde[j]) = (borde[j], borde[i]);
        }

        // El borde va DESPUÉS del núcleo en la misma lista: como la cascada recorre en orden y corta
        // al llegar al objetivo, el núcleo se usa primero por construcción y el casco solo entra
        // cuando falta. No hace falta una segunda pasada.
        candidatos.AddRange(borde);

        // Cascada de separación, mismo espíritu que la del cuartel: se intenta con la separación
        // pedida y se relaja de a uno SOLO si no se llegó al mínimo de salas. Preferimos salas un
        // poco más juntas antes que una isla con menos viviendas de las pedidas.
        //
        // El cuartel ya está en `set` antes de esta vuelta, así que las viviendas se mantienen
        // lejos de él por construcción — que era el caso feo: cuartel y vivienda pared con pared.
        for (int sep = Mathf.Max(1, separacionMinimaSalasChunks); sep >= 1; sep--)
        {
            foreach (Vector3Int c in candidatos)
            {
                if (set.Count >= objetivo) break;
                if (set.Contains(c)) continue;
                if (!RespetaSeparacion(c, set, sep)) continue;
                set.Add(c);
            }
            if (set.Count >= minSalas) break;
        }

        Random.state = prev;

        cacheSalasIsla[region] = set;
        return set;
    }

    /// <summary>
    /// ¿Este chunk está lo bastante lejos de TODAS las salas ya elegidas? Distancia Chebyshev en
    /// macro chunks, así que cuenta la vertical igual que la horizontal: dos salas una encima de
    /// la otra están a distancia 1, exactamente igual que dos pared con pared.
    /// </summary>
    private static bool RespetaSeparacion(Vector3Int candidato, HashSet<Vector3Int> yaElegidas, int separacion)
    {
        foreach (Vector3Int otra in yaElegidas)
        {
            int d = Mathf.Max(Mathf.Abs(candidato.x - otra.x),
                    Mathf.Max(Mathf.Abs(candidato.y - otra.y), Mathf.Abs(candidato.z - otra.z)));
            if (d < separacion) return false;
        }
        return true;
    }

    /// <summary>¿A este macro chunk le toca ser Sala Chica según el cupo de su isla?</summary>
    private bool EsChunkSalaDeIsla(int mx, int my, int mz)
    {
        if (!EnIsla(mx, my, mz)) return false;
        return SalasDeIsla(RegionDeChunk(mx, my, mz)).Contains(new Vector3Int(mx, my, mz));
    }

    /// <summary>
    /// Primera RoomData marcada como cuartel de centinelas (elegida por hash si hay varias), o
    /// null si el proyecto no tiene ninguna configurada.
    /// </summary>
    private RoomData ElegirRoomDataCuartel(int mx, int my, int mz)
    {
        if (salasGrandesPosibles == null) return null;

        List<RoomData> cuarteles = null;
        foreach (RoomData s in salasGrandesPosibles)
        {
            if (s == null || !s.esCuartelCentinelas) continue;
            (cuarteles ??= new List<RoomData>()).Add(s);
        }
        if (cuarteles == null) return null;
        if (cuarteles.Count == 1) return cuarteles[0];

        int hash = semillaDelMundo + mx * 30013 + my * 46589 + mz * 61717;
        Random.State prev = Random.state;
        Random.InitState(hash);
        RoomData elegida = cuarteles[Random.Range(0, cuarteles.Count)];
        Random.state = prev;
        return elegida;
    }

    /// <summary>Región (y por lo tanto isla) a la que pertenece un macro chunk.</summary>
    private Vector3Int RegionDeChunk(int mx, int my, int mz)
    {
        int p = Mathf.Max(3, periodoIslas);
        return new Vector3Int(
            Mathf.FloorToInt((float)mx / p),
            Mathf.FloorToInt((float)my / p),
            Mathf.FloorToInt((float)mz / p));
    }

    /// <summary>¿Este macro chunk cae en una isla que es Sala Colosal?</summary>
    private bool EsIslaColosalDeChunk(int mx, int my, int mz)
    {
        Vector3Int r = RegionDeChunk(mx, my, mz);
        return IslaEsColosal(r.x, r.y, r.z);
    }

    /// <summary>
    /// ¿En esta cara están INVERTIDOS los roles interior/exterior respecto a lo que asume el
    /// generador? La regla general es "lado vacío = interior de la estructura, lado sólido = su
    /// masa", y por eso la piel exterior se aplica hacia adentro del sólido. En la cáscara de una
    /// Sala Colosal eso vale para su cara de ADENTRO (la que mira a la nave), pero en la de AFUERA
    /// el lado vacío es el vacío del MUNDO: ahí el interior queda del lado SÓLIDO y la piel
    /// exterior tiene que mirar al vacío. Sin esta corrección el casco sale del revés — la pared
    /// interior por fuera y la exterior metida para adentro.
    /// </summary>
    private bool CaraDeCascoColosalInvertida(bool actualVacia, int x, int y, int z, int nx, int ny, int nz)
    {
        int sx = actualVacia ? nx : x, sy = actualVacia ? ny : y, sz = actualVacia ? nz : z; // lado sólido
        int ex = actualVacia ? x : nx, ey = actualVacia ? y : ny, ez = actualVacia ? z : nz; // lado vacío
        return CeldaEnIslaColosal(sx, sy, sz) && !CeldaEnIslaColosal(ex, ey, ez);
    }

    /// <summary>¿Esta CELDA cae dentro del volumen de una sala colosal?</summary>
    private bool CeldaEnIslaColosal(int x, int y, int z)
    {
        int mx = Mathf.FloorToInt((float)x / macroSize);
        int my = Mathf.FloorToInt((float)y / macroSize);
        int mz = Mathf.FloorToInt((float)z / macroSize);
        return EnIsla(mx, my, mz) && EsIslaColosalDeChunk(mx, my, mz);
    }

    /// <summary>
    /// ¿Esta celda es parte de la CÁSCARA sólida de la sala colosal? Lo es si a grosorCascaraColosal
    /// celdas de distancia, sobre cualquiera de los 6 ejes, ya se sale del volumen de la isla.
    /// Se mide sobre el volumen real y no sobre el macro chunk: así la cáscara sigue la forma de
    /// la isla (caja o esfera) sin casos especiales, y el piso queda abajo de todo — no un piso
    /// flotante por macro chunk como haría el tipo 7 apilado.
    /// </summary>
    private bool EsCascaraSalaColosal(int x, int y, int z)
    {
        int g = Mathf.Max(1, grosorCascaraColosal);
        return !CeldaEnIslaColosal(x + g, y, z) || !CeldaEnIslaColosal(x - g, y, z)
            || !CeldaEnIslaColosal(x, y + g, z) || !CeldaEnIslaColosal(x, y - g, z)
            || !CeldaEnIslaColosal(x, y, z + g) || !CeldaEnIslaColosal(x, y, z - g);
    }

    private bool EnIsla(int mx, int my, int mz)
    {
        int p = Mathf.Max(3, periodoIslas);
        int rx = Mathf.FloorToInt((float)mx / p);
        int ry = Mathf.FloorToInt((float)my / p);
        int rz = Mathf.FloorToInt((float)mz / p);
        Vector3Int c = CentroIsla(rx, ry, rz, out int radio, out bool esCaja);
        int dx = mx - c.x, dy = my - c.y, dz = mz - c.z;
        if (esCaja)
            return Mathf.Abs(dx) <= radio && Mathf.Abs(dy) <= radio && Mathf.Abs(dz) <= radio;
        return dx * dx + dy * dy + dz * dz <= radio * radio;
    }

    /// <summary>
    /// ¿Este chunk (fuera de isla) es parte de un puente entre islas vecinas?
    /// Devuelve el tipo de chunk a forzar: 0 = no hay puente, 2 = pasillo Z, 3 = pasillo X,
    /// 5 = cruce (esquina de camino en L o superposición de puentes).
    /// Cada región decide por hash si tiene puente hacia +X y hacia +Z (probabilidadPuente).
    /// El camino es una L: desde el centro de la isla A a lo largo de X hasta la x del centro
    /// de B, luego a lo largo de Z. Corre en my = ry*p (base de las islas de esa capa).
    /// </summary>
    private int TipoPuente(int mx, int my, int mz)
    {
        int p = Mathf.Max(3, periodoIslas);
        int ry = Mathf.FloorToInt((float)my / p);
        if (my != ry * p) return 0; // puentes solo en el nivel base de cada capa

        int rx = Mathf.FloorToInt((float)mx / p);
        int rz = Mathf.FloorToInt((float)mz / p);

        bool enX = false, enZ = false;
        // 4 puentes candidatos pueden pasar por esta región: los 2 propios (hacia +X / +Z)
        // y los entrantes del vecino -X y del vecino -Z.
        ChequearSegmentoPuente(rx, ry, rz, true, mx, mz, ref enX, ref enZ);
        ChequearSegmentoPuente(rx, ry, rz, false, mx, mz, ref enX, ref enZ);
        ChequearSegmentoPuente(rx - 1, ry, rz, true, mx, mz, ref enX, ref enZ);
        ChequearSegmentoPuente(rx, ry, rz - 1, false, mx, mz, ref enX, ref enZ);

        if (enX && enZ) return 5;
        if (enX) return 3;
        if (enZ) return 2;
        return 0;
    }

    /// <summary>
    /// ¿Qué puentes salientes (+X / +Z) tiene la isla de esta región? Se deciden juntos por
    /// hash, y si AMBOS rolls fallan se fuerza el de +X: toda isla tiene SIEMPRE al menos una
    /// conexión saliente (más las entrantes de sus vecinas) — sin islas huérfanas.
    /// </summary>
    private void PuentesDeRegion(int rx, int ry, int rz, out bool hayX, out bool hayZ)
    {
        int hash = semillaDelMundo + rx * 92821 + ry * 31469 + rz * 57943;
        Random.State prev = Random.state;
        Random.InitState(hash);
        hayX = Random.value <= probabilidadPuente;
        hayZ = Random.value <= probabilidadPuente;
        Random.state = prev;
        if (!hayX && !hayZ) hayX = true; // garantía: nunca una isla sin salida
    }

    private void ChequearSegmentoPuente(int rx, int ry, int rz, bool haciaX, int mx, int mz, ref bool enX, ref bool enZ)
    {
        // ¿Existe el puente de esta región hacia +X (o +Z)?
        PuentesDeRegion(rx, ry, rz, out bool hayPuenteX, out bool hayPuenteZ);
        bool hay = haciaX ? hayPuenteX : hayPuenteZ;
        if (!hay) return;

        Vector3Int a = CentroIsla(rx, ry, rz, out _, out _);
        Vector3Int b = haciaX ? CentroIsla(rx + 1, ry, rz, out _, out _)
                              : CentroIsla(rx, ry, rz + 1, out _, out _);

        // Camino en L: X primero (en z = a.z), luego Z (en x = b.x)
        if (mz == a.z && mx >= Mathf.Min(a.x, b.x) && mx <= Mathf.Max(a.x, b.x)) enX = true;
        if (mx == b.x && mz >= Mathf.Min(a.z, b.z) && mz <= Mathf.Max(a.z, b.z)) enZ = true;
    }
}
