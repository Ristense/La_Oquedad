using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Ventana de Editor en Unity para combinar mapas PBR individuales (Metallic, AO, Roughness)
/// en una única textura de máscara (Mask Map) y configurar el material automáticamente.
/// </summary>
public class CreadorMaterialesPBR : EditorWindow
{
    private Texture2D texturaColor;
    private Texture2D texturaDiffuse;   // alias del Albedo: si se asigna y Color está vacío, se usa éste
    private Texture2D texturaNormal;
    private Texture2D texturaMetalness;
    private Texture2D texturaRoughness;
    private Texture2D texturaAO;
    private Texture2D texturaDisplacement;
    private Texture2D texturaOpacity;   // si tiene, se mergea al alpha del BaseColor
    private Texture2D texturaSpecular;  // si tiene, se usa workflow Specular Color en vez de Metallic

    [Tooltip("Si la opacidad debe funcionar como Alpha Clip (cutout) en lugar de transparencia completa.")]
    private bool usarAlphaClip = true;
    private float umbralAlphaClip = 0.5f;

    private string nombreMaterial = "NuevoMaterialPBR";
    private string pathCarpeta = "Assets";

    [MenuItem("Herramientas/Creador de Materiales PBR")]
    public static void ShowWindow()
    {
        GetWindow<CreadorMaterialesPBR>("Creador PBR");
    }

    void OnEnable()
    {
        ObtenerCarpetaSeleccionada();
    }

    void OnFocus()
    {
        ObtenerCarpetaSeleccionada();
    }

    void OnSelectionChange()
    {
        ObtenerCarpetaSeleccionada();
        Repaint();
    }

    private void ObtenerCarpetaSeleccionada()
    {
        string nuevaCarpeta = "Assets";
        foreach (UnityEngine.Object obj in Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets))
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path))
            {
                if (Directory.Exists(path))
                {
                    nuevaCarpeta = path;
                    break;
                }
                else if (File.Exists(path))
                {
                    nuevaCarpeta = Path.GetDirectoryName(path);
                    break;
                }
            }
        }
        if (!string.IsNullOrEmpty(nuevaCarpeta))
        {
            pathCarpeta = nuevaCarpeta;
        }
    }

    void OnGUI()
    {
        GUILayout.Label("Configura las texturas para generar el Material", EditorStyles.boldLabel);

        texturaColor = (Texture2D)EditorGUILayout.ObjectField("Color / Albedo", texturaColor, typeof(Texture2D), false);
        texturaDiffuse = (Texture2D)EditorGUILayout.ObjectField("Diffuse (alias de Albedo)", texturaDiffuse, typeof(Texture2D), false);
        texturaNormal = (Texture2D)EditorGUILayout.ObjectField("Normal Map (DX)", texturaNormal, typeof(Texture2D), false);
        texturaMetalness = (Texture2D)EditorGUILayout.ObjectField("Metalness", texturaMetalness, typeof(Texture2D), false);
        texturaRoughness = (Texture2D)EditorGUILayout.ObjectField("Roughness", texturaRoughness, typeof(Texture2D), false);
        texturaAO = (Texture2D)EditorGUILayout.ObjectField("Ambient Occlusion (AO)", texturaAO, typeof(Texture2D), false);
        texturaDisplacement = (Texture2D)EditorGUILayout.ObjectField("Displacement / Height", texturaDisplacement, typeof(Texture2D), false);

        GUILayout.Space(6);
        GUILayout.Label("Opcional: Transparencia", EditorStyles.boldLabel);
        texturaOpacity = (Texture2D)EditorGUILayout.ObjectField("Opacity / Alpha", texturaOpacity, typeof(Texture2D), false);
        if (texturaOpacity != null)
        {
            usarAlphaClip = EditorGUILayout.Toggle("Usar Alpha Clip (cutout)", usarAlphaClip);
            if (usarAlphaClip)
                umbralAlphaClip = EditorGUILayout.Slider("Umbral Alpha Clip", umbralAlphaClip, 0f, 1f);
        }

        GUILayout.Space(6);
        GUILayout.Label("Opcional: Specular Color (sustituye Metallic workflow)", EditorStyles.boldLabel);
        texturaSpecular = (Texture2D)EditorGUILayout.ObjectField("Specular", texturaSpecular, typeof(Texture2D), false);

        GUILayout.Space(6);
        nombreMaterial = EditorGUILayout.TextField("Nombre del Material", nombreMaterial);

        GUILayout.Space(10);

        EditorGUILayout.LabelField("Carpeta de Exportación (Seleccionada en Proyecto):", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(pathCarpeta, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));

        GUILayout.Space(10);

        if (GUILayout.Button("Generar Mask Map y Material", GUILayout.Height(40)))
        {
            GenerarTodo();
        }
    }

    private void GenerarTodo()
    {
        // Diffuse es alias de Albedo: si Color/Albedo está vacío y Diffuse no, lo usamos como BaseColor
        if (texturaColor == null && texturaDiffuse != null)
        {
            texturaColor = texturaDiffuse;
        }

        if (texturaColor == null)
        {
            EditorUtility.DisplayDialog("Error", "Debes asignar al menos la textura de Color o Diffuse.", "OK");
            return;
        }

        string destino = pathCarpeta;
        if (string.IsNullOrEmpty(destino) || destino == "Assets")
        {
            string pathColor = AssetDatabase.GetAssetPath(texturaColor);
            if (!string.IsNullOrEmpty(pathColor))
            {
                destino = Path.GetDirectoryName(pathColor);
            }
        }

        if (string.IsNullOrEmpty(destino))
        {
            destino = "Assets";
        }

        // 1. Crear el Mask Map (Combinando Metallic, AO y Roughness)
        string maskMapPath = CombinarTexturas(destino);

        // 1.5. Si hay opacity, mergearla al alpha del color para que HDRP la use como transparencia/alpha-clip
        string colorConAlphaPath = null;
        if (texturaOpacity != null)
        {
            colorConAlphaPath = CombinarColorYOpacity(destino);
        }

        // 2. Crear y configurar el Material
        CrearMaterialPBR(destino, maskMapPath, colorConAlphaPath);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Éxito", "Material y Mask Map creados con éxito.", "OK");
    }

    private string CombinarTexturas(string carpeta)
    {
        // Encontrar dimensiones de referencia (usar la de color o roughness)
        int width = texturaColor.width;
        int height = texturaColor.height;

        Texture2D maskMap = new Texture2D(width, height, TextureFormat.RGBA32, true);

        // Asegurar que las texturas de entrada sean legibles temporalmente
        HabilitarLecturaEscritura(texturaMetalness, true);
        HabilitarLecturaEscritura(texturaAO, true);
        HabilitarLecturaEscritura(texturaRoughness, true);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // HDRP Mask Map Estándar:
                // R: Metallic (Metalness)
                // G: Ambient Occlusion
                // B: Detalle (lo dejamos vacío/negro o 0)
                // A: Smoothness (1.0 - Roughness)

                float metallic = (texturaMetalness != null) ? texturaMetalness.GetPixel(x, y).r : 0f;
                float ao = (texturaAO != null) ? texturaAO.GetPixel(x, y).r : 1f;
                float smoothness = (texturaRoughness != null) ? (1f - texturaRoughness.GetPixel(x, y).r) : 0.5f;

                maskMap.SetPixel(x, y, new Color(metallic, ao, 0f, smoothness));
            }
        }

        maskMap.Apply();

        // Restaurar configuraciones de lectura
        HabilitarLecturaEscritura(texturaMetalness, false);
        HabilitarLecturaEscritura(texturaAO, false);
        HabilitarLecturaEscritura(texturaRoughness, false);

        // Guardar la textura en disco
        byte[] bytes = maskMap.EncodeToPNG();
        string fullPath = Path.Combine(carpeta, nombreMaterial + "_MaskMap.png");
        File.WriteAllBytes(fullPath, bytes);
        AssetDatabase.ImportAsset(fullPath);

        return fullPath;
    }

    /// <summary>
    /// Combina el albedo + opacity en una sola textura (RGB = color, A = opacity) y la guarda en disco.
    /// HDRP/Lit lee la transparencia del canal alpha del BaseColorMap.
    /// </summary>
    private string CombinarColorYOpacity(string carpeta)
    {
        int w = texturaColor.width;
        int h = texturaColor.height;
        Texture2D combinada = new Texture2D(w, h, TextureFormat.RGBA32, true);

        HabilitarLecturaEscritura(texturaColor, true);
        HabilitarLecturaEscritura(texturaOpacity, true);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c = texturaColor.GetPixel(x, y);
                float a = texturaOpacity.GetPixel(x, y).r; // usamos canal rojo (asumimos grayscale)
                combinada.SetPixel(x, y, new Color(c.r, c.g, c.b, a));
            }
        }
        combinada.Apply();

        HabilitarLecturaEscritura(texturaColor, false);
        HabilitarLecturaEscritura(texturaOpacity, false);

        byte[] bytes = combinada.EncodeToPNG();
        string fullPath = Path.Combine(carpeta, nombreMaterial + "_BaseColorAlpha.png");
        File.WriteAllBytes(fullPath, bytes);
        AssetDatabase.ImportAsset(fullPath);

        // Asegurarnos de que el alpha NO sea tratado como transparencia para preview, sino solo como dato
        TextureImporter ti = AssetImporter.GetAtPath(fullPath) as TextureImporter;
        if (ti != null)
        {
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
        }

        return fullPath;
    }

    private void CrearMaterialPBR(string carpeta, string maskMapPath, string colorConAlphaPath)
    {
        string materialPath = Path.Combine(carpeta, nombreMaterial + ".mat");
        Material mat = new Material(Shader.Find("HDRP/Lit"));

        if (mat.shader == null)
        {
            // Caída por si es URP en vez de HDRP
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        // Asignar BaseColor: si hubo merge con opacity, usar esa textura combinada
        Texture2D colorFinal = texturaColor;
        if (!string.IsNullOrEmpty(colorConAlphaPath))
        {
            colorFinal = AssetDatabase.LoadAssetAtPath<Texture2D>(colorConAlphaPath);
        }
        mat.SetTexture("_BaseColorMap", colorFinal); // HDRP
        mat.SetTexture("_BaseMap", colorFinal);      // URP

        if (texturaNormal != null)
        {
            ConfigurarComoNormalMap(texturaNormal);
            mat.SetTexture("_NormalMap", texturaNormal);
        }

        Texture2D maskMap = AssetDatabase.LoadAssetAtPath<Texture2D>(maskMapPath);
        if (maskMap != null)
        {
            mat.SetTexture("_MaskMap", maskMap);      // HDRP
            mat.SetTexture("_MetallicGlossMap", maskMap); // URP (usa el canal Alpha como Smoothness)
        }

        if (texturaDisplacement != null)
        {
            mat.SetTexture("_HeightMap", texturaDisplacement);      // HDRP
            mat.SetTexture("_ParallaxMap", texturaDisplacement);    // URP
            
            // Configurar desplazamiento por defecto en Pixel Displacement (Parallax Occlusion)
            mat.SetFloat("_DisplacementMode", 2f); // 2 = Pixel displacement en HDRP
            mat.SetFloat("_HeightAmplitude", 0.02f); // 2cm de amplitud
            mat.SetFloat("_HeightCenter", 0.5f);
            mat.SetFloat("_Parallax", 0.02f); // URP
            
            mat.EnableKeyword("_HEIGHTMAP");
            mat.EnableKeyword("_HEIGHTMAP_AS_DISPLACEMENT");
        }

        // --- TRANSPARENCIA / ALPHA CLIP (si hay opacity) ---
        if (texturaOpacity != null)
        {
            if (usarAlphaClip)
            {
                // Cutout: queda Opaque pero con alpha test
                mat.SetFloat("_AlphaCutoffEnable", 1f);
                mat.SetFloat("_AlphaCutoff", umbralAlphaClip);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            else
            {
                // Transparente real
                mat.SetFloat("_SurfaceType", 1f);
                mat.SetFloat("_BlendMode", 0f); // 0 = Alpha blend
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_ZTestDepthEqualForOpaque", 4f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }

        // --- SPECULAR COLOR (sustituye Metallic workflow) ---
        if (texturaSpecular != null)
        {
            // HDRP: switch a Specular Color workflow
            mat.SetTexture("_SpecularColorMap", texturaSpecular);
            mat.SetColor("_SpecularColor", Color.white);
            mat.SetFloat("_MaterialID", 4f); // 4 = SpecularColor
            mat.EnableKeyword("_MATERIAL_FEATURE_SPECULAR_COLOR");
        }

        // Guardar Material
        AssetDatabase.CreateAsset(mat, materialPath);
    }

    private void HabilitarLecturaEscritura(Texture2D textura, bool habilitar)
    {
        if (textura == null) return;
        string path = AssetDatabase.GetAssetPath(textura);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.isReadable != habilitar)
        {
            importer.isReadable = habilitar;
            importer.SaveAndReimport();
        }
    }

    private void ConfigurarComoNormalMap(Texture2D textura)
    {
        string path = AssetDatabase.GetAssetPath(textura);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }
}
