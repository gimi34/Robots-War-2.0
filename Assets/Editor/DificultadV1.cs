using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Deja la DIFICULTAD de la V2 igual a la de la V1 (original).
/// Deshace lo que hizo ModoHardcore.cs en las escenas y en la bala enemiga:
///  - Mason: 5 vidas, 100 balas, cadencia 0.22
///  - Robots: 2 de vida, cadencia 2.4, deteccion 7
///  - Jefe: 26 de vida y sus valores originales
///  - Quita los robots que agrego el modo hardcore (RobotExtra5 en adelante)
///  - Devuelve los 5 corazones al HUD
///  - Bala enemiga a velocidad 7
/// NO toca lo visual (robots rojos, mapa sombrio, balas de colores, Jeep).
/// Menu: Aura -> 6.
/// </summary>
public static class DificultadV1
{
    const string PREFAB_BALA_ENEMIGA = "Assets/Prefabs/BalaEnemiga.prefab";

    // ---------- Valores originales de la V1 ----------
    const int VIDA_JUGADOR = 5;
    const int MUNICION_JUGADOR = 100;
    const float CADENCIA_JUGADOR = 0.22f;

    const int VIDA_ROBOT = 2;
    const float CADENCIA_ROBOT = 2.4f;
    const float DETECCION_ROBOT = 7f;

    const int VIDA_JEFE = 26;
    const float CADENCIA_JEFE = 1.8f;
    const float APERTURA_JEFE = 10f;
    const float DETECCION_JEFE = 14f;
    const float AMPLITUD_JEFE = 1.2f;
    const float BALANCEO_JEFE = 1.1f;

    const float VELOCIDAD_BALA_ENEMIGA = 7f;

    // La V1 ya traia RobotExtra1..4. Los que creo el modo hardcore son del 5 en adelante.
    const int PRIMER_ROBOT_HARDCORE = 5;

    [MenuItem("Aura/6. Dificultad de la V1 (quitar Hardcore)")]
    public static void Aplicar()
    {
        // Pregunta si hay algo sin guardar en la escena abierta
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        BalaEnemigaNormal();

        AplicarNivel("Assets/Scenes/Nivel1.unity");
        AplicarNivel("Assets/Scenes/Nivel2.unity");
        AplicarNivel("Assets/Scenes/Nivel3.unity");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[DificultadV1] Listo: Mason " + VIDA_JUGADOR + " vidas, robots con " + VIDA_ROBOT +
                  " de vida, jefe con " + VIDA_JEFE + " de vida, bala enemiga a " + VELOCIDAD_BALA_ENEMIGA + ".");
    }

    // =====================================================================
    //  UN NIVEL
    // =====================================================================
    static void AplicarNivel(string ruta)
    {
        Scene escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);

        Transform raiz = null;
        foreach (GameObject go in escena.GetRootGameObjects())
            if (go.name == "Nivel") raiz = go.transform;

        if (raiz == null)
        {
            Debug.LogWarning("[DificultadV1] No encontre la raiz 'Nivel' en " + ruta);
            return;
        }

        // 1. Quitar los robots que agrego el modo hardcore
        int quitados = 0;
        foreach (RobotEnemigo robot in raiz.GetComponentsInChildren<RobotEnemigo>(true))
        {
            if (EsRobotHardcore(robot.gameObject.name))
            {
                Object.DestroyImmediate(robot.gameObject);
                quitados++;
            }
        }

        // 2. Mason y su HUD de vidas
        JugadorMason jugador = Object.FindAnyObjectByType<JugadorMason>();
        if (jugador != null)
        {
            jugador.vidaMaxima = VIDA_JUGADOR;
            jugador.municionMaxima = MUNICION_JUGADOR;
            jugador.cadencia = CADENCIA_JUGADOR;
            EditorUtility.SetDirty(jugador);

            RestaurarIconosVida(raiz, jugador);
        }

        // 3. Robots restantes
        foreach (RobotEnemigo robot in raiz.GetComponentsInChildren<RobotEnemigo>(true))
        {
            robot.vidaMaxima = VIDA_ROBOT;
            robot.cadencia = CADENCIA_ROBOT;
            robot.distanciaDeteccion = DETECCION_ROBOT;
            EditorUtility.SetDirty(robot);
        }

        // 4. Jefe (solo existe en el nivel 3)
        foreach (JefeNucleo jefe in raiz.GetComponentsInChildren<JefeNucleo>(true))
        {
            jefe.vidaMaxima = VIDA_JEFE;
            jefe.cadencia = CADENCIA_JEFE;
            jefe.aperturaAbanico = APERTURA_JEFE;
            jefe.distanciaDeteccion = DETECCION_JEFE;
            jefe.amplitud = AMPLITUD_JEFE;
            jefe.velocidadBalanceo = BALANCEO_JEFE;
            EditorUtility.SetDirty(jefe);
        }

        Debug.Log("[DificultadV1] " + ruta + ": " + quitados + " robots quitados.");

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }

    // RobotExtra5, RobotExtra6... = los que puso el modo hardcore
    static bool EsRobotHardcore(string nombre)
    {
        const string prefijo = "RobotExtra";
        if (!nombre.StartsWith(prefijo)) return false;

        int numero;
        if (!int.TryParse(nombre.Substring(prefijo.Length), out numero)) return false;

        return numero >= PRIMER_ROBOT_HARDCORE;
    }

    // =====================================================================
    //  HUD: el modo hardcore dejo solo 3 corazones. Se copian hasta tener 5.
    // =====================================================================
    static void RestaurarIconosVida(Transform raiz, JugadorMason jugador)
    {
        HudVidas hud = raiz.GetComponentInChildren<HudVidas>(true);
        if (hud == null) hud = Object.FindAnyObjectByType<HudVidas>();
        if (hud == null || hud.iconos == null || hud.iconos.Length == 0) return;

        hud.jugador = jugador;

        int deseados = jugador.vidaMaxima;
        int actuales = hud.iconos.Length;
        if (actuales >= deseados) return;

        Image modelo = hud.iconos[actuales - 1];
        if (modelo == null) return;

        // Separacion entre corazones (en la V1 son 7 de ancho)
        float separacion = 7f;
        if (actuales >= 2 && hud.iconos[actuales - 2] != null)
            separacion = modelo.rectTransform.anchoredPosition.x - hud.iconos[actuales - 2].rectTransform.anchoredPosition.x;

        Image[] nuevos = new Image[deseados];
        for (int i = 0; i < nuevos.Length; i++)
        {
            if (i < actuales)
            {
                nuevos[i] = hud.iconos[i];
                continue;
            }

            Image copia = Object.Instantiate(modelo, modelo.transform.parent, false);
            copia.gameObject.name = "Vida" + i;

            Vector2 pos = modelo.rectTransform.anchoredPosition;
            pos.x += separacion * (i - (actuales - 1));
            copia.rectTransform.anchoredPosition = pos;

            nuevos[i] = copia;
        }

        hud.iconos = nuevos;
        EditorUtility.SetDirty(hud);
    }

    // =====================================================================
    //  BALA ENEMIGA: velocidad original
    // =====================================================================
    static void BalaEnemigaNormal()
    {
        GameObject contenido = PrefabUtility.LoadPrefabContents(PREFAB_BALA_ENEMIGA);
        if (contenido == null)
        {
            Debug.LogWarning("[DificultadV1] No encontre " + PREFAB_BALA_ENEMIGA);
            return;
        }

        Bala bala = contenido.GetComponent<Bala>();
        if (bala != null) bala.velocidad = VELOCIDAD_BALA_ENEMIGA;

        PrefabUtility.SaveAsPrefabAsset(contenido, PREFAB_BALA_ENEMIGA);
        PrefabUtility.UnloadPrefabContents(contenido);
    }
}