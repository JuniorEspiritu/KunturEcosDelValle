using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Prueba automática (solo editor): entra a Play desde el menú, le da
// CONTINUAR y va sacando capturas de pantalla (menú, HUD, cartel, mochila,
// pausa, heladería) a Logs/PlayTest. Se dispara con el menú Kuntur o creando
// el archivo Logs/playtest.request. No toca la partida guardada.
[InitializeOnLoad]
public static class KunturPlayTest
{
    private const string Request = "Logs/playtest.request";
    private const string OutDir = "Logs/PlayTest";
    private const string StateKey = "Kuntur_PlayTest_Step";

    private static double stepTime;

    static KunturPlayTest()
    {
        EditorApplication.update += Tick;
    }

    [MenuItem("Kuntur/Prueba automática con capturas")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) return;
        Directory.CreateDirectory(OutDir);
        SessionState.SetInt(StateKey, 1);
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MenuPrincipal.unity");
        EditorApplication.isPlaying = true;
    }

    private static DrivableVehicle TukTuk()
    {
        foreach (DrivableVehicle v in DrivableVehicle.All) if (v != null && v.IsTukTuk) return v;
        return null;
    }

    private static void Where(string label)
    {
        DrivableVehicle tuk = TukTuk();
        var p = SimpleThirdPersonController.Instance;
        if (tuk == null || p == null) return;
        Transform hips = null;
        foreach (Transform t in p.GetComponentsInChildren<Transform>()) if (t.name == "mixamorig:Hips") { hips = t; break; }
        Debug.Log($"[Kuntur] Prueba ({label}): asiento {tuk.Seat.position}, cadera {(hips != null ? hips.position.ToString() : "-")}, rot tuk {tuk.transform.eulerAngles}, rot Kuntur {p.transform.eulerAngles}");
        Debug.Log($"[Kuntur] Prueba ({label}): tuk tuk en {tuk.transform.position}, Kuntur en {p.transform.position}, " +
                  $"distancia {tuk.DistanceTo(p.transform.position):0.0} m, piloto automático {tuk.Autopilot}, largo {tuk.HalfLength * 2f:0.0} m");
    }

    private static void Shot(string name)
    {
        ScreenCapture.CaptureScreenshot($"{OutDir}/{name}.png");
        Debug.Log($"[Kuntur] Prueba: captura {name}");
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Request))
        {
            try { File.Delete(Request); } catch { }
            if (!EditorApplication.isCompiling) Begin();
            return;
        }

        int step = SessionState.GetInt(StateKey, 0);
        if (step == 0) return;
        if (!EditorApplication.isPlaying)
        {
            // Ya salió de Play: fin de la prueba.
            if (step >= 99) SessionState.SetInt(StateKey, 0);
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (stepTime == 0) stepTime = now;
        double t = now - stepTime;

        void Next(int s) { SessionState.SetInt(StateKey, s); stepTime = now; }

        switch (step)
        {
            case 1: if (t > 4) { Shot("01_menu"); Next(2); } break;
            case 2: if (t > 2) { Shot("02_menu_hover"); Object.FindAnyObjectByType<MenuController>()?.ContinueGame(); Next(3); } break;
            case 3: if (t > 9) { Shot("03_hud"); Next(4); } break;
            case 4: if (t > 5) { Shot("04_hud_banner"); Next(5); } break;
            case 5: if (t > 1) { InventoryUI.Instance?.Open(); Next(6); } break;
            case 6: if (t > 1.2) { Shot("05_mochila"); InventoryUI.Instance?.Close(); Next(7); } break;
            case 7: if (t > 1) { Object.FindAnyObjectByType<PauseMenu>()?.Open(); Next(8); } break;
            case 8: if (t > 1.2) { Shot("06_pausa"); Object.FindAnyObjectByType<PauseMenu>()?.Close(); Next(9); } break;
            // v56: el tuk tuk (subir, manejar, bajar, pedirlo) y el gatito.
            case 9:
                if (t > 1)
                {
                    var p = SimpleThirdPersonController.Instance;
                    if (p != null) p.Teleport(new Vector3(-79.2f, 0.1f, -112.3f), 180f);
                    Next(10);
                }
                break;
            case 10: if (t > 2) { Shot("07_tuktuk_cartel"); VehicleSystem.Instance?.EnterVehicle(TukTuk()); Next(11); } break;
            case 11: if (t > 1.0) { Shot("08_tuktuk_subiendo"); Next(12); } break;
            case 12:
                if (t > 2.5)
                {
                    Shot("09_tuktuk_sentado");
                    // Vista de costado para ver a Kuntur sentado en el tuk tuk.
                    var p = SimpleThirdPersonController.Instance;
                    var f = typeof(SimpleThirdPersonController).GetField("vehicleYaw", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (p != null && f != null) f.SetValue(p, 150f);
                    Where("sentado");
                    Next(30);
                }
                break;
            case 30: if (t > 0.4) { Shot("09b_tuktuk_costado"); VehicleSystem.DebugThrottle = 0.8f; Next(13); } break;
            case 13: if (t > 3.5) { VehicleSystem.DebugSteer = 0.6f; Shot("10_tuktuk_manejando"); Where("manejando"); Next(14); } break;
            case 14: if (t > 2.0) { VehicleSystem.DebugSteer = 0f; VehicleSystem.DebugThrottle = -1f; Next(15); } break;
            case 15: if (t > 1.5) { VehicleSystem.DebugThrottle = 0f; Shot("11_tuktuk_frenado"); VehicleSystem.Instance?.ForceExit(); Next(16); } break;
            case 16: if (t > 1.5) { Shot("12_tuktuk_bajado"); Where("bajado"); Next(17); } break;
            case 17:
                if (t > 0.5)
                {
                    var p = SimpleThirdPersonController.Instance;
                    if (p != null) p.Teleport(new Vector3(-5.6f, 0.2f, -30f), 180f);
                    Next(18);
                }
                break;
            case 18: if (t > 1) { VehicleSystem.Instance?.RequestTukTuk(); Next(19); } break;
            case 19: if (t > 2) { Shot("13_tuktuk_pedido"); Where("pedido"); Next(20); } break;
            case 20: if (t > 7) { Shot("14_tuktuk_viniendo"); Where("viniendo"); Next(21); } break;
            case 21: if (t > 12) { Shot("15_tuktuk_llego"); Where("llego"); Next(22); } break;
            case 22:
                if (t > 0.5)
                {
                    var p = SimpleThirdPersonController.Instance;
                    if (p != null) p.Teleport(new Vector3(-84.9f, 0.2f, -105.6f), 180f);
                    Next(23);
                }
                break;
            case 23:
                if (t > 1.5)
                {
                    foreach (var a in Object.FindObjectsByType<AnimalWander>(FindObjectsSortMode.None))
                        if (a.GetPrompt() == "Llevar al gatito") { a.Interact(); break; }
                    Next(24);
                }
                break;
            case 24: if (t > 1.5) { Shot("16_gatito_brazos"); Next(25); } break;
            case 25:
                if (t > 0.5)
                {
                    var p = SimpleThirdPersonController.Instance;
                    if (p != null) p.Teleport(new Vector3(20f, 17.5f, -205f), -90f);
                    Next(26);
                }
                break;
            case 26: if (t > 3) { Shot("17_calle_mirador"); Next(99); } break;
            case 99: if (t > 1.5) { EditorApplication.isPlaying = false; } break;
        }
    }
}
