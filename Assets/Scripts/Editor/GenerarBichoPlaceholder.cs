using UnityEngine;
using UnityEditor;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Genera un bicho de prueba (cuerpo + 6 patas de 2 segmentos c/u, capsulas primitivas) con
/// Two Bone IK Constraint ya conectado, para probar la logica de "estirar y buscar nuevo punto"
/// sin depender de un modelo/rig hecho en Blender.
///
/// Cada pata tiene un GameObject "_Target" (arrastrable en Scene) que es donde el pie intenta
/// llegar, y un "_Hint" que define hacia que lado dobla la rodilla/codo. El script de stepping
/// que se arme despues solo tiene que mover el Target — el IK Constraint hace el resto.
/// </summary>
public static class GenerarBichoPlaceholder
{
    private const int NumPares = 3;
    private const float LargoSegmento = 0.8f;
    private const float Grosor = 0.15f;

    [MenuItem("Tools/In Side Us/Generar Bicho IK Placeholder")]
    public static void Generar()
    {
        GameObject cuerpo = new GameObject("BichoPlaceholder");

        GameObject cuerpoVisual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cuerpoVisual.name = "CuerpoVisual";
        cuerpoVisual.transform.SetParent(cuerpo.transform, false);
        cuerpoVisual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // acostado, como un torso
        cuerpoVisual.transform.localScale = new Vector3(1.2f, NumPares * 0.6f, 0.8f);
        Object.DestroyImmediate(cuerpoVisual.GetComponent<Collider>());

        // Animation Rigging necesita un Animator en la raiz para evaluar los constraints,
        // aunque no tengas ningun AnimatorController asignado.
        cuerpo.AddComponent<Animator>();
        RigBuilder rigBuilder = cuerpo.AddComponent<RigBuilder>();

        GameObject rigGO = new GameObject("Rig");
        rigGO.transform.SetParent(cuerpo.transform, false);
        Rig rig = rigGO.AddComponent<Rig>();
        rigBuilder.layers.Add(new RigLayer(rig, true));

        GameObject targetsGO = new GameObject("Targets");
        targetsGO.transform.SetParent(cuerpo.transform, false);

        for (int i = 0; i < NumPares; i++)
        {
            float z = (i - (NumPares - 1) / 2f) * 1.0f;
            CrearPata(cuerpo.transform, rigGO.transform, targetsGO.transform, $"Pata_Izq_{i}", new Vector3(-0.6f, 0f, z), -1);
            CrearPata(cuerpo.transform, rigGO.transform, targetsGO.transform, $"Pata_Der_{i}", new Vector3(0.6f, 0f, z), 1);
        }

        rigBuilder.Build();

        Selection.activeGameObject = cuerpo;
        Debug.Log($"[GenerarBichoPlaceholder] Listo: {NumPares * 2} patas. Cada '_Target' es lo que hay que mover para que el pie siga (arrastralo a mano para probar).");
    }

    private static void CrearPata(Transform cuerpo, Transform rigParent, Transform targetsParent, string nombre, Vector3 offsetLocal, int lado)
    {
        GameObject raiz = new GameObject(nombre + "_Raiz");
        raiz.transform.SetParent(cuerpo, false);
        raiz.transform.localPosition = offsetLocal;

        // Segmento superior: apunta hacia afuera y un poco hacia abajo (pose de reposo).
        GameObject sup = new GameObject(nombre + "_Sup");
        sup.transform.SetParent(raiz.transform, false);
        sup.transform.localRotation = Quaternion.LookRotation(new Vector3(lado, -0.4f, 0.05f).normalized);
        AgregarVisualSegmento(sup.transform, Grosor);

        // Segmento inferior: hijo del extremo del superior.
        GameObject inf = new GameObject(nombre + "_Inf");
        inf.transform.SetParent(sup.transform, false);
        inf.transform.localPosition = new Vector3(0f, 0f, LargoSegmento);
        inf.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
        AgregarVisualSegmento(inf.transform, Grosor * 0.8f);

        // Punta (pie): donde termina la pata de verdad.
        GameObject pie = new GameObject(nombre + "_Pie");
        pie.transform.SetParent(inf.transform, false);
        pie.transform.localPosition = new Vector3(0f, 0f, LargoSegmento);

        // Target del IK, en espacio de MUNDO, arrancando justo donde esta el pie ahora
        // (asi no hay un salto feo apenas entrás a Play).
        GameObject target = new GameObject(nombre + "_Target");
        target.transform.SetParent(targetsParent, false);
        target.transform.position = pie.transform.position;
        MarcadorGizmo marcadorTarget = target.AddComponent<MarcadorGizmo>();
        marcadorTarget.color = Color.yellow;

        // Hint: hacia donde dobla el "codo"/"rodilla" — sin esto el IK puede doblar para
        // adentro del cuerpo, que se ve horrible.
        GameObject hint = new GameObject(nombre + "_Hint");
        hint.transform.SetParent(targetsParent, false);
        hint.transform.position = sup.transform.position + raiz.transform.right * lado * 0.6f + Vector3.down * 0.4f;
        MarcadorGizmo marcadorHint = hint.AddComponent<MarcadorGizmo>();
        marcadorHint.color = Color.cyan;
        marcadorHint.radio = 0.05f;

        GameObject constraintGO = new GameObject(nombre + "_IK");
        constraintGO.transform.SetParent(rigParent, false);
        TwoBoneIKConstraint ik = constraintGO.AddComponent<TwoBoneIKConstraint>();
        ik.data.root = sup.transform;
        ik.data.mid = inf.transform;
        ik.data.tip = pie.transform;
        ik.data.target = target.transform;
        ik.data.hint = hint.transform;
        ik.data.targetPositionWeight = 1f;
        ik.data.targetRotationWeight = 0f;
        ik.data.hintWeight = 1f;

        // La logica de "cuando y donde pisar" vive en el propio Raiz de la pata (que sigue
        // al cuerpo), apuntando al Target que el IK ya usa arriba.
        PataIK pataIK = raiz.AddComponent<PataIK>();
        pataIK.target = target.transform;
        pataIK.puntoReposo = raiz.transform;
    }

    private static void AgregarVisualSegmento(Transform hueso, float grosor)
    {
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(hueso, false);
        visual.transform.localPosition = new Vector3(0f, 0f, LargoSegmento * 0.5f);
        visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        visual.transform.localScale = new Vector3(grosor, LargoSegmento * 0.5f, grosor);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
    }
}
