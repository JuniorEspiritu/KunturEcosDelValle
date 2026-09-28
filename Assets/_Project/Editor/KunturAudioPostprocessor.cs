using UnityEditor;
using UnityEngine;

// Ajustes de importación de los audios. Esto NO es un detalle: es lo que
// decide si un sonido que da vueltas suena continuo o con un hueco en cada
// vuelta.
//
// Unity vuelve a comprimir el audio al importarlo. Si un ambiente corto (el
// río, los grillos, el motor) se guarda en Vorbis, el codificador le agrega
// unas milésimas de relleno al principio y al final, y esas milésimas se oyen
// como un clic cada vez que el clip vuelve a empezar. Por eso todo lo que da
// vueltas y es corto entra en PCM, sin comprimir: son unos MB más, pero el
// loop queda limpio.
//
// La música larga sí va comprimida y en streaming: en PCM, cuatro minutos de
// estéreo son ~50 MB cargados en memoria.
public class KunturAudioPostprocessor : AssetPostprocessor
{
    // Subir este número cuando cambien las reglas de abajo. Unity guarda los
    // ajustes del PRIMER import y no vuelve a importar solo porque el script
    // haya cambiado; ya pasó con el modelo de Kuntur.
    public override uint GetVersion() => 4;

    private void OnPreprocessAudio()
    {
        if (!assetPath.Contains("/Art/Audio/")) return;

        AudioImporter importer = (AudioImporter)assetImporter;
        string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;

        bool isLongMusic = file == "Musica_Juego" || file == "Musica_Aventura";
        bool loops = file.StartsWith("Amb_") || file.StartsWith("Musica_") || file == "SFX_Motor" || file == "SFX_TukTuk_Motor";

        if (isLongMusic)
        {
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.preloadAudioData = false;
        }
        else if (loops)
        {
            // Corto y en bucle: sin comprimir, para que no le metan relleno.
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.preloadAudioData = true;
        }
        else
        {
            // Efecto corto de una sola vez: ADPCM pesa poco y se descomprime
            // al vuelo sin el retardo que tendría un Vorbis.
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.ADPCM;
            settings.preloadAudioData = true;
        }

        importer.defaultSampleSettings = settings;
        importer.forceToMono = false;
        importer.loadInBackground = isLongMusic;
        // Los ambientes 3D (río, grillos, motor) no necesitan estéreo: Unity
        // los espacializa igual, y en mono la mezcla queda más limpia.
        importer.ambisonic = false;
    }
}
