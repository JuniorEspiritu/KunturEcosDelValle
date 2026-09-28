using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

// v55: fuente fina y elegante para el menú principal (estilo Horizon):
// Josefin Sans (licencia SIL OFL), en Art/Fuentes. El atlas SDF se arma una
// sola vez con todos sus caracteres (incluye tildes, ñ, ¡ ¿) y queda estático,
// así se ve igual en el editor y en el build.
public static partial class KunturSceneBuilder
{
    private static TMP_FontAsset GetMenuFont(string weight)
    {
        string dir = ArtDir + "/Fuentes";
        string ttfPath = $"{dir}/JosefinSans-{weight}.ttf";
        string assetPath = $"{dir}/JosefinSans-{weight} SDF.asset";

        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null && existing.characterTable != null && existing.characterTable.Count > 60) return existing;
        if (existing != null) AssetDatabase.DeleteAsset(assetPath);

        Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (font == null)
        {
            Debug.LogWarning($"[Kuntur] Falta la fuente {ttfPath}: el menú usa la fuente por defecto.");
            return defaultFont;
        }

        TMP_FontAsset asset = null;
        try
        {
            asset = TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Kuntur] No se pudo crear el atlas de {ttfPath}: {e.Message}");
        }
        if (asset == null) return defaultFont;

        asset.name = $"JosefinSans-{weight} SDF";
        const string chars =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
            "¡¿·«»°ºª ÁÉÍÓÚÜÑáéíóúüñ—–…‘’“”";
        asset.TryAddCharacters(chars, out string missing);
        if (!string.IsNullOrEmpty(missing)) Debug.Log($"[Kuntur] Josefin {weight}: sin glifo para '{missing}'.");

        AssetDatabase.CreateAsset(asset, assetPath);
        foreach (Texture2D atlas in asset.atlasTextures)
        {
            if (atlas == null) continue;
            atlas.name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
        }
        if (asset.material != null)
        {
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }
        // Ya tiene todo lo que el menú escribe: queda fijo.
        asset.atlasPopulationMode = AtlasPopulationMode.Static;
        // Si falta un carácter, que lo ponga la fuente de siempre.
        if (defaultFont != null)
        {
            asset.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { defaultFont };
        }
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) ?? asset;
    }
}
