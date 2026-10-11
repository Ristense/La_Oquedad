# La_Oquedad — respaldo de In Side Us

Respaldo **liviano** del proyecto Unity *In Side Us*: código, configuración y datos en texto.
No es el proyecto completo. El proyecto original (con FBX, texturas y audio, ~6 GB) vive en
`Documents/In Side Us` con su propio repo y Git LFS.

## Qué hay

| Carpeta | Contenido |
|---|---|
| `Assets/Scripts` | Todo el código C# (incluye `Editor/`) |
| `Assets/Prefabs` | Todos los prefabs |
| `Assets/Escenas` | Escenas del juego |
| `Assets/_Recovery` | Escenas que Unity guardó tras crashes (pueden tener cambios sin guardar) |
| `Assets/Data` | ScriptableObjects (salas, habitantes, módulos…) |
| `Assets/Settings` | Assets de HDRP y volúmenes |
| `Assets/Plugins` | DOTween |
| `Assets/Fonts` | Fuentes |
| `Assets/Resources`, `Imports`, `Animation` | **Solo** materiales, shaders, shader graphs, animator controllers, `.anim` y `.asset` |
| `ProjectSettings`, `Packages` | Configuración del proyecto y versiones de paquetes |

Cada archivo va con su `.meta`, así los GUIDs (las referencias entre assets) se conservan.

## Qué NO hay

- Modelos (`.fbx`), texturas (`.png`, `.jpg`), audio (`.mp3`, `.wav`).
- `Library/`, `Temp/`, `Logs/` (Unity los regenera).
- TextMesh Pro y TutorialInfo (se reimportan desde Unity).

Por eso los prefabs y escenas, abiertos solos, muestran mallas y texturas faltantes: apuntan por GUID
a binarios que están en el proyecto original. Para restaurar, copiar estas carpetas **encima** del
proyecto original.

## Versión

Unity **6000.6.4f1** · HDRP 17.6.0
