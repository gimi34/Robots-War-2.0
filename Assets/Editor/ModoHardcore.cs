using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Version 2.0 "HARDCORE" del juego. Se aplica UNA vez sobre las escenas que ya existen:
///  - dificultad mucho mas alta (menos vida de Mason, robots con mas vida y mas rapidos, jefe mas duro)
///  - mas robots repartidos por los tres niveles
///  - robots y jefe en rojo agresivo
///  - balas recoloreadas (jugador cian, enemigos rojo fuego) y mas rapidas
///  - mapa mucho mas sombrio: luz global baja, azulada, y fondo de camara casi negro
///  - la camioneta (Ranger) se cambia por el Jeep rojo de "Pixel Cars"
/// Menu: Aura -> 5.
/// </summary>
public static class ModoHardcore
{
    // ---------- Rutas ----------
    const string JEEP = "Assets/VOiD1 Gaming - Pixel Cars/Pixel Cars/Jeep.png";
    const string CARPETA_ARTE = "Assets/ArteHardcore";

    const string MAT_LIT = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
    const string MAT_UNLIT = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    const string PREFAB_BALA_JUGADOR = "Assets/Prefabs/BalaJugador.prefab";
    const string PREFAB_BALA_ENEMIGA = "Assets/Prefabs/BalaEnemiga.prefab";
    const string TEX_BALA_JUGADOR = "Assets/SpriteSheets/player/weapon_bullet.png";
    const string TEX_BALA_ENEMIGA = "Assets/SpriteSheets/enemy/enemy_weapon_bullet.png";

    // ---------- Colores ----------
    static readonly Color ROJO_ENEMIGO = new Color(1f, 0.46f, 0.42f, 1f);
    static readonly Color ROJO_JEFE = new Color(1f, 0.36f, 0.40f, 1f);
    static readonly Color CIAN_BALA = new Color(0.35f, 0.95f, 1f, 1f);
    static readonly Color ROJO_BALA = new Color(1f, 0.38f, 0.14f, 1f);
    static readonly Color COLOR_LUZ = new Color(0.58f, 0.62f, 0.86f, 1f);
    static readonly Color FONDO_CAMARA = new Color(0.012f, 0.014f, 0.028f, 1f);
    static readonly Color FILTRO_POST = new Color(0.78f, 0.85f, 1f, 1f);
    const float INTENSIDAD_LUZ = 0.72f;
    const float EXPOSICION_POST = -0.35f;
    const float SATURACION_POST = -25f;
    const float INTENSIDAD_VINETA = 0.40f;
    const string PERFIL_SOMBRIO = "Assets/PerfilSombrio.asset";

    // ---------- Dificultad ----------
    const int VIDA_JUGADOR = 3;
    const int MUNICION_JUGADOR = 80;
    const float CADENCIA_JUGADOR = 0.18f;

    const int VIDA_ROBOT = 4;
    const float CADENCIA_ROBOT = 1.1f;
    const float DETECCION_ROBOT = 12f;

    const int VIDA_JEFE = 50;
    const float CADENCIA_JEFE = 1.05f;
    const float APERTURA_JEFE = 14f;
    const float DETECCION_JEFE = 20f;
    const float AMPLITUD_JEFE = 2.2f;
    const float BALANCEO_JEFE = 1.6f;

    const float VELOCIDAD_BALA_ENEMIGA = 11f;

    // Posiciones de los robots nuevos (x). Se apoyan en el suelo real de la escena.
    static readonly float[] EXTRA_NIVEL1 = { 8f, 28f, 44f, 92f };
    static readonly float[] EXTRA_NIVEL2 = { 3f, 16f, 30f, 69f, 84f, 110f };
    static readonly float[] EXTRA_NIVEL3 = { 10f, 26f, 44f };

    // =====================================================================
    //  ENTRADA
    // =====================================================================
    [MenuItem("Aura/5. Modo Hardcore 2.0 (aplicar todo)")]
    public static void Aplicar()
    {
        Sprite jeep = PrepararJeep();
        if (jeep == null) Debug.LogWarning("[Hardcore] No pude preparar el Jeep.");

        RecolorearBalas();

        VolumeProfile perfil = CrearPerfilSombrio();

        AplicarNivel("Assets/Scenes/Nivel1.unity", jeep, perfil, EXTRA_NIVEL1);
        AplicarNivel("Assets/Scenes/Nivel2.unity", jeep, perfil, EXTRA_NIVEL2);
        AplicarNivel("Assets/Scenes/Nivel3.unity", jeep, perfil, EXTRA_NIVEL3);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Hardcore] Version 2.0 aplicada: Mason " + VIDA_JUGADOR + " vidas, robots rojos con " + VIDA_ROBOT +
                  " de vida, jefe " + VIDA_JEFE + " de vida, balas recoloreadas, luz global " + INTENSIDAD_LUZ +
                  " y Jeep en lugar de la camioneta.");
    }

    // =====================================================================
    //  UN NIVEL
    // =====================================================================
    static void AplicarNivel(string ruta, Sprite jeep, VolumeProfile perfil, float[] extraX)
    {
        Scene escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);

        Transform raiz = null;
        foreach (GameObject go in escena.GetRootGameObjects())
            if (go.name == "Nivel") raiz = go.transform;

        if (raiz == null)
        {
            Debug.LogWarning("[Hardcore] No encontre la raiz 'Nivel' en " + ruta);
            return;
        }

        // 1. Mas robots (clonando uno que ya existe para heredar sprites y bala)
        AgregarRobots(raiz, extraX);

        // Nunca un robot pegado al punto de salida de Mason: seria una muerte injusta
        JugadorMason inicio = Object.FindAnyObjectByType<JugadorMason>();
        if (inicio != null)
        {
            foreach (RobotEnemigo robot in raiz.GetComponentsInChildren<RobotEnemigo>(true))
                if (Vector3.Distance(robot.transform.position, inicio.transform.position) < 5f)
                    Object.DestroyImmediate(robot.gameObject);
        }

        // 2. Dificultad
        JugadorMason jugador = Object.FindAnyObjectByType<JugadorMason>();
        if (jugador != null)
        {
            jugador.vidaMaxima = VIDA_JUGADOR;
            jugador.municionMaxima = MUNICION_JUGADOR;
            jugador.cadencia = CADENCIA_JUGADOR;
            AjustarHud(raiz, jugador);
        }

        foreach (RobotEnemigo robot in raiz.GetComponentsInChildren<RobotEnemigo>(true))
        {
            robot.vidaMaxima = VIDA_ROBOT;
            robot.cadencia = CADENCIA_ROBOT;
            robot.distanciaDeteccion = DETECCION_ROBOT;
        }

        foreach (JefeNucleo jefe in raiz.GetComponentsInChildren<JefeNucleo>(true))
        {
            jefe.vidaMaxima = VIDA_JEFE;
            jefe.cadencia = CADENCIA_JEFE;
            jefe.aperturaAbanico = APERTURA_JEFE;
            jefe.distanciaDeteccion = DETECCION_JEFE;
            jefe.amplitud = AMPLITUD_JEFE;
            jefe.velocidadBalanceo = BALANCEO_JEFE;
        }

        // 3. Robots y jefe en rojo
        Material unlit = AssetDatabase.LoadAssetAtPath<Material>(MAT_UNLIT);

        foreach (RobotEnemigo robot in raiz.GetComponentsInChildren<RobotEnemigo>(true))
            Pintar(robot.gameObject, ROJO_ENEMIGO, unlit);

        foreach (JefeNucleo jefe in raiz.GetComponentsInChildren<JefeNucleo>(true))
        {
            foreach (SpriteRenderer dibujo in jefe.GetComponentsInChildren<SpriteRenderer>(true))
            {
                dibujo.color = ROJO_JEFE;
                if (unlit != null) dibujo.sharedMaterial = unlit;
            }
        }

        // 4. Ambiente sombrio: luz global baja y azulada + fondo de camara casi negro
        foreach (Light2D luz in Object.FindObjectsByType<Light2D>())
        {
            if (luz.lightType != Light2D.LightType.Global) continue;
            luz.intensity = INTENSIDAD_LUZ;
            luz.color = COLOR_LUZ;
        }

        foreach (Camera cam in Object.FindObjectsByType<Camera>())
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = FONDO_CAMARA;

            UniversalAdditionalCameraData datos = cam.GetUniversalAdditionalCameraData();
            if (datos != null) datos.renderPostProcessing = true;
        }

        // Post-proceso: menos exposicion, menos saturacion y viñeta para el tono tenebroso
        if (perfil != null)
        {
            GameObject volumen = Buscar(raiz, "VolumenSombrio");
            if (volumen == null) volumen = new GameObject("VolumenSombrio");
            if (volumen.transform.parent != raiz) volumen.transform.SetParent(raiz, false);

            Volume componente = volumen.GetComponent<Volume>();
            if (componente == null) componente = volumen.AddComponent<Volume>();
            componente.isGlobal = true;
            componente.priority = 1f;
            componente.weight = 1f;
            componente.sharedProfile = perfil;
        }

        // Todo el decorado usa material iluminado: asi la luz baja lo oscurece de verdad
        Material lit = AssetDatabase.LoadAssetAtPath<Material>(MAT_LIT);
        if (lit != null)
        {
            foreach (SpriteRenderer dibujo in raiz.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (dibujo.GetComponent<RobotEnemigo>() != null) continue;
                if (dibujo.GetComponent<JefeNucleo>() != null) continue;
                if (dibujo.GetComponentInParent<JefeNucleo>() != null) continue;
                if (dibujo.GetComponent<JugadorMason>() != null) continue;
                dibujo.sharedMaterial = lit;
            }
        }

        // 5. La camioneta pasa a ser el Jeep
        if (jeep != null)
        {
            GameObject camioneta = Buscar(raiz, "Camioneta");
            if (camioneta != null)
            {
                SpriteRenderer dibujo = camioneta.GetComponent<SpriteRenderer>();
                if (dibujo == null) dibujo = camioneta.AddComponent<SpriteRenderer>();
                dibujo.sprite = jeep;
                dibujo.sortingOrder = 4;
            }
        }

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
    }

    // =====================================================================
    //  HUD: los iconos de vida tienen que ser tantos como la vida nueva
    // =====================================================================
    static void AjustarHud(Transform raiz, JugadorMason jugador)
    {
        HudVidas hud = raiz.GetComponentInChildren<HudVidas>(true);
        if (hud == null) return;

        hud.jugador = jugador;

        if (hud.iconos != null)
        {
            for (int i = jugador.vidaMaxima; i < hud.iconos.Length; i++)
                if (hud.iconos[i] != null) Object.DestroyImmediate(hud.iconos[i].gameObject);

            Image[] nuevos = new Image[jugador.vidaMaxima];
            for (int i = 0; i < nuevos.Length; i++)
                if (i < hud.iconos.Length) nuevos[i] = hud.iconos[i];

            hud.iconos = nuevos;
        }
    }

    // =====================================================================
    //  MAS ROBOTS
    // =====================================================================
    static void AgregarRobots(Transform raiz, float[] posiciones)
    {
        if (raiz == null || posiciones == null) return;

        RobotEnemigo modelo = null;
        foreach (RobotEnemigo robot in raiz.GetComponentsInChildren<RobotEnemigo>(true))
        {
            modelo = robot;
            break;
        }

        if (modelo == null)
        {
            Debug.LogWarning("[Hardcore] La escena no tiene ningun robot del que copiar.");
            return;
        }

        // El numero sigue al ultimo RobotExtra que ya haya en la escena
        int siguiente = 1;
        foreach (Transform hijo in raiz)
            if (hijo.name.StartsWith("RobotExtra")) siguiente++;

        int creados = 0;

        for (int i = 0; i < posiciones.Length; i++)
        {
            float suelo;
            if (!AlturaDelSuelo(raiz, posiciones[i], out suelo)) continue;   // ahi hay un precipicio
            if (YaHayRobotEn(raiz, posiciones[i])) continue;               // ya se aplico antes

            GameObject go = Object.Instantiate(modelo.gameObject);
            go.name = "RobotExtra" + siguiente;
            siguiente++;
            go.transform.SetParent(raiz, true);
            go.transform.position = new Vector3(posiciones[i], suelo + 0.6f, 0f);
            go.transform.localScale = Vector3.one;

            creados++;
        }

        if (creados > 0) Debug.Log("[Hardcore] " + creados + " robots nuevos anadidos.");
    }

    // Evita duplicar robots si la herramienta se ejecuta mas de una vez
    static bool YaHayRobotEn(Transform raiz, float x)
    {
        foreach (RobotEnemigo robot in raiz.GetComponentsInChildren<RobotEnemigo>(true))
            if (Mathf.Abs(robot.transform.position.x - x) < 1.5f) return true;

        return false;
    }

    // Altura de la superficie mas alta que hay en esa x (suelo y plataformas, no cajas ni adornos)
    static bool AlturaDelSuelo(Transform raiz, float x, out float altura)
    {
        altura = 0f;
        bool encontrado = false;

        foreach (Collider2D choque in raiz.GetComponentsInChildren<Collider2D>(true))
        {
            if (choque.isTrigger) continue;
            if (choque.GetComponent<JugadorMason>() != null) continue;
            if (choque.GetComponent<RobotEnemigo>() != null) continue;
            if (choque.GetComponent<JefeNucleo>() != null) continue;
            if (choque.GetComponent<SalidaNivel>() != null) continue;

            Bounds caja = choque.bounds;
            if (caja.size.x < 3f) continue;                                 // cajas, barriles y adornos no son suelo
            if (x < caja.min.x + 0.4f || x > caja.max.x - 0.4f) continue;   // no pegado al borde
            if (caja.max.y > -0.2f) continue;                               // solo suelo y plataformas bajas

            if (!encontrado || caja.max.y > altura)
            {
                altura = caja.max.y;
                encontrado = true;
            }
        }

        return encontrado;
    }

    static void Pintar(GameObject go, Color color, Material material)
    {
        SpriteRenderer dibujo = go.GetComponent<SpriteRenderer>();
        if (dibujo == null) return;

        dibujo.color = color;
        if (material != null) dibujo.sharedMaterial = material;
    }

    // =====================================================================
    //  BALAS: se generan sprites recoloreados (el tinte solo puede oscurecer,
    //  asi que para cambiar de verdad el color se reescriben los pixeles)
    // =====================================================================
    static void RecolorearBalas()
    {
        Sprite balaJugador = Recolorear(TEX_BALA_JUGADOR, "weapon_bullet_0", "BalaJugadorCian", CIAN_BALA);
        Sprite balaEnemiga = Recolorear(TEX_BALA_ENEMIGA, "enemy_weapon_bullet_0", "BalaEnemigaRoja", ROJO_BALA);

        if (balaJugador != null) ConfigurarBala(PREFAB_BALA_JUGADOR, balaJugador, 16f, 7);
        if (balaEnemiga != null) ConfigurarBala(PREFAB_BALA_ENEMIGA, balaEnemiga, VELOCIDAD_BALA_ENEMIGA, 6);
    }

    static void ConfigurarBala(string rutaPrefab, Sprite sprite, float velocidad, int orden)
    {
        GameObject contenido = PrefabUtility.LoadPrefabContents(rutaPrefab);
        if (contenido == null) return;

        SpriteRenderer dibujo = contenido.GetComponent<SpriteRenderer>();
        if (dibujo != null)
        {
            dibujo.sprite = sprite;
            dibujo.color = Color.white;
            dibujo.sortingOrder = orden;

            Material unlit = AssetDatabase.LoadAssetAtPath<Material>(MAT_UNLIT);
            if (unlit != null) dibujo.sharedMaterial = unlit;
        }

        Bala bala = contenido.GetComponent<Bala>();
        if (bala != null) bala.velocidad = velocidad;

        PrefabUtility.SaveAsPrefabAsset(contenido, rutaPrefab);
        PrefabUtility.UnloadPrefabContents(contenido);
    }

    // Copia el primer cuadro del sprite, lo repinta con el color pedido y lo guarda como PNG nuevo
    static Sprite Recolorear(string rutaTextura, string nombreSprite, string nombreNuevo, Color color)
    {
        Sprite original = BuscarSprite(rutaTextura, nombreSprite);
        if (original == null)
        {
            Debug.LogWarning("[Hardcore] No encontre el sprite " + nombreSprite + " en " + rutaTextura);
            return null;
        }

        TextureImporter imp = AssetImporter.GetAtPath(rutaTextura) as TextureImporter;
        if (imp != null && !imp.isReadable)
        {
            imp.isReadable = true;
            imp.SaveAndReimport();
        }

        Texture2D hoja = original.texture;
        Rect recorte = original.rect;
        int ancho = Mathf.RoundToInt(recorte.width);
        int alto = Mathf.RoundToInt(recorte.height);

        Color[] origen = hoja.GetPixels((int)recorte.x, (int)recorte.y, ancho, alto);
        Color[] destino = new Color[origen.Length];

        for (int i = 0; i < origen.Length; i++)
        {
            Color pixel = origen[i];

            // Se mide el brillo original y se repinta con el color pedido:
            // el nucleo claro de la bala se queda casi blanco (efecto "caliente").
            float brillo = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
            Color nuevo = color * Mathf.Clamp01(brillo * 1.25f);
            nuevo = Color.Lerp(nuevo, Color.white, Mathf.Clamp01((brillo - 0.8f) * 2.5f) * 0.55f);
            nuevo.a = pixel.a;
            destino[i] = nuevo;
        }

        if (!AssetDatabase.IsValidFolder(CARPETA_ARTE))
            AssetDatabase.CreateFolder("Assets", "ArteHardcore");

        string rutaAsset = CARPETA_ARTE + "/" + nombreNuevo + ".png";

        Texture2D nueva = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
        nueva.SetPixels(destino);
        nueva.Apply();

        File.WriteAllBytes(rutaAsset, nueva.EncodeToPNG());
        Object.DestroyImmediate(nueva);

        AssetDatabase.ImportAsset(rutaAsset, ImportAssetOptions.ForceUpdate);

        TextureImporter nuevoImp = AssetImporter.GetAtPath(rutaAsset) as TextureImporter;
        if (nuevoImp != null)
        {
            nuevoImp.textureType = TextureImporterType.Sprite;
            nuevoImp.spriteImportMode = SpriteImportMode.Single;
            nuevoImp.spritePixelsPerUnit = 16f;
            nuevoImp.filterMode = FilterMode.Point;
            nuevoImp.textureCompression = TextureImporterCompression.Uncompressed;
            nuevoImp.alphaIsTransparency = true;
            nuevoImp.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(rutaAsset);
    }

    static Sprite BuscarSprite(string rutaTextura, string nombre)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(rutaTextura))
        {
            Sprite s = o as Sprite;
            if (s != null && s.name == nombre) return s;
        }
        return null;
    }

    // =====================================================================
    //  PERFIL DE POST-PROCESO (sombrio / tenebroso)
    // =====================================================================
    static VolumeProfile CrearPerfilSombrio()
    {
        VolumeProfile perfil = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PERFIL_SOMBRIO);

        if (perfil == null)
        {
            perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(perfil, PERFIL_SOMBRIO);
        }

        ColorAdjustments color = null;
        Vignette vineta = null;

        foreach (VolumeComponent componente in perfil.components)
        {
            if (componente is ColorAdjustments) color = componente as ColorAdjustments;
            else if (componente is Vignette) vineta = componente as Vignette;
        }

        if (color == null)
        {
            color = perfil.Add<ColorAdjustments>(true);
            AssetDatabase.AddObjectToAsset(color, perfil);
        }

        if (vineta == null)
        {
            vineta = perfil.Add<Vignette>(true);
            AssetDatabase.AddObjectToAsset(vineta, perfil);
        }

        color.postExposure.Override(EXPOSICION_POST);
        color.saturation.Override(SATURACION_POST);
        color.contrast.Override(12f);
        color.colorFilter.Override(FILTRO_POST);

        vineta.intensity.Override(INTENSIDAD_VINETA);
        vineta.smoothness.Override(0.45f);
        vineta.color.Override(Color.black);

        EditorUtility.SetDirty(color);
        EditorUtility.SetDirty(vineta);
        EditorUtility.SetDirty(perfil);

        return perfil;
    }

    // =====================================================================
    //  JEEP (sustituye a la camioneta Ranger)
    // =====================================================================
    static Sprite PrepararJeep()
    {
        TextureImporter imp = AssetImporter.GetAtPath(JEEP) as TextureImporter;
        if (imp == null)
        {
            Debug.LogWarning("[Hardcore] No encontre " + JEEP);
            return null;
        }

        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.isReadable = true;
        imp.SaveAndReimport();

        // El dibujo del coche no ocupa toda la imagen: el pivote se pone en el centro
        // de la base del dibujo para que quede apoyado en el suelo.
        Texture2D textura = AssetDatabase.LoadAssetAtPath<Texture2D>(JEEP);
        if (textura == null) return null;

        Color32[] pixeles = textura.GetPixels32();
        int ancho = textura.width;
        int alto = textura.height;

        int minX = ancho, maxX = -1, minY = alto, maxY = -1;
        for (int y = 0; y < alto; y++)
        {
            for (int x = 0; x < ancho; x++)
            {
                if (pixeles[y * ancho + x].a <= 16) continue;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX >= 0)
        {
            float pivoteX = ((minX + maxX) * 0.5f + 0.5f) / ancho;
            float pivoteY = minY / (float)alto;

            TextureImporterSettings ajustes = new TextureImporterSettings();
            imp.ReadTextureSettings(ajustes);
            ajustes.spriteAlignment = (int)SpriteAlignment.Custom;
            ajustes.spritePivot = new Vector2(pivoteX, pivoteY);
            imp.SetTextureSettings(ajustes);
            imp.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(JEEP);
    }

    static GameObject Buscar(Transform raiz, string nombre)
    {
        if (raiz == null) return null;

        Transform hijo = raiz.Find(nombre);
        return hijo != null ? hijo.gameObject : null;
    }
}
