using UnityEngine;

// Lo que se guarda de una partida: en qué día va Kuntur, cuántas misiones
// cumplió, sus estrellas y cómo está el valle. Se guarda al dormir (fin del
// día) y al terminar cada misión, así "Continuar" retoma desde ahí.
[System.Serializable]
public class SaveData
{
    public int day = 1;
    public int missionsDone;
    public int stars;
    public float health = 35f;
    public bool rosaConvinced;
    public bool samplesDone;
    public string lastZones = "";
    // v55: zonas "de vecino" ya limpiadas (chacra, mirador): la primera vez
    // te las encarga su vecino antes que cualquier otra.
    public string specialZonesDone = "";
}

// Guardado simple con PlayerPrefs + JSON: alcanza para un juego de curso y
// funciona igual en el editor y en el build de Windows, sin rutas de archivo.
public static class SaveSystem
{
    private const string Key = "Kuntur_Partida_v1";

    public static bool HasSave => PlayerPrefs.HasKey(Key);

    // La partida en curso (en memoria). Se lee del disco al entrar a la escena
    // del juego (ver DayManager) y se escribe con SaveCurrent().
    private static SaveData current;
    public static SaveData Current => current ??= Load();
    public static void Reload() => current = Load();
    public static void SaveCurrent() => Save(Current);

    public static SaveData Load()
    {
        if (!HasSave) return new SaveData();
        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
            return data ?? new SaveData();
        }
        catch
        {
            return new SaveData();
        }
    }

    public static void Save(SaveData data)
    {
        if (data == null) return;
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
        current = null;
    }
}
