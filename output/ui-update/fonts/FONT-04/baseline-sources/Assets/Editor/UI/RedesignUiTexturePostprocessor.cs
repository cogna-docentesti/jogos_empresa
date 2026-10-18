using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Ajusta o import de tudo que cai em Art/Sprites/REDESIGN-UI.
    ///
    /// Dois problemas que isto resolve:
    ///
    /// 1. COMPRESSAO. O padrao do Unity comprime a textura; num degrade dourado
    ///    e num halo suave isso vira faixa (banding) bem visivel. UI fica sem
    ///    compressao.
    ///
    /// 2. ESTICAMENTO. Os PNGs tem canto arredondado e borda desenhados. Se o
    ///    Image esticar o sprite inteiro, o raio vira oval e a borda engorda de
    ///    um lado. O spriteBorder abaixo marca a moldura de cada arte para o
    ///    Unity usar 9-slice e esticar so o miolo.
    ///
    /// Os numeros de borda sao sempre MAIORES que o raio do canto do PNG, senao
    /// o corte passaria dentro da curva.
    /// </summary>
    public sealed class RedesignUiTexturePostprocessor : AssetPostprocessor
    {
        private const string Folder = "/Art/Sprites/REDESIGN-UI/";

        /// <summary>prefixo do arquivo -> borda de 9-slice (px).</summary>
        private static readonly Dictionary<string, float> Borders = new Dictionary<string, float>
        {
            { "panel-bg",          30f },  // raio 20
            { "stat-row-bg",       18f },  // raio 12
            { "highlight-",        18f },  // raio 12
            { "btn-continuar",     20f },  // raio 12
            { "btn-menu",          16f },  // raio 10
            { "menu-dropdown-bg",  20f },  // raio 14
            { "menu-item-hover",   14f },  // raio 9
            { "toast-bg",          18f },  // raio 12
            { "label-pill-bg",     16f },  // raio 10
        };

        /// <summary>
        /// Arte que NAO pode ser fatiada: o conteudo e o desenho inteiro, nao
        /// uma moldura. Esticar o miolo destruiria o icone / o pino / o mapa.
        /// </summary>
        private static readonly string[] NeverSliced =
        {
            "pin-", "glow-", "panel-icon-", "stat-tile-", "brand-badge",
            "ornament-", "map-", "device-frame", "topbar-bg", "screen-bg",
            "label-financeira", "label-educacional", "label-comercial",
            "label-residencial", "label-corporativa"
        };

        private void OnPreprocessTexture()
        {
            if (assetPath.Replace('\\', '/').IndexOf(Folder, System.StringComparison.OrdinalIgnoreCase) < 0)
                return;

            var importer = (TextureImporter)assetImporter;

            importer.textureType         = TextureImporterType.Sprite;
            importer.spriteImportMode    = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled       = false;
            importer.filterMode          = FilterMode.Bilinear;
            importer.wrapMode            = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 100f;   // casa com referencePixelsPerUnit do Canvas
            importer.maxTextureSize      = 2048;

            var settings = importer.GetDefaultPlatformTextureSettings();
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            settings.format             = TextureImporterFormat.Automatic;
            importer.SetPlatformTextureSettings(settings);

            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();

            foreach (var skip in NeverSliced)
            {
                if (file.StartsWith(skip))
                {
                    importer.spriteBorder = Vector4.zero;
                    return;
                }
            }

            foreach (var pair in Borders)
            {
                if (!file.StartsWith(pair.Key))
                    continue;

                float b = pair.Value;
                importer.spriteBorder = new Vector4(b, b, b, b);
                return;
            }

            importer.spriteBorder = Vector4.zero;
        }

        /// <summary>
        /// Forca a reimportacao da pasta. Util depois de trocar os PNGs por cima,
        /// porque o Unity so reaplica o preprocessor quando o arquivo muda.
        /// </summary>
        [MenuItem("Tools/Redesign UI/Reimportar sprites do redesign")]
        private static void Reimport()
        {
            const string dir = "Assets/Art/Sprites/REDESIGN-UI";

            if (!AssetDatabase.IsValidFolder(dir))
            {
                Debug.LogWarning($"[RedesignUI] Pasta nao encontrada: {dir}");
                return;
            }

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { dir });

            foreach (var guid in guids)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);

            AssetDatabase.Refresh();
            Debug.Log($"[RedesignUI] {guids.Length} sprites reimportados.");
        }
    }
}
