using System.Collections.Generic;
using System.IO;
using Game.Adapter.In.UI.Theme;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Monta o asset LocationSkinV2 achando os PNGs pelo nome do arquivo.
    ///
    /// Escrito porque a skin tem ~50 campos de Sprite. Arrastar um por um no
    /// Inspector e onde erro de digitacao vira bug silencioso (sprite errado
    /// numa zona so, que so aparece quando o jogador clica naquela zona).
    ///
    /// Rodar de novo e seguro: reaproveita o asset existente e so reescreve os
    /// campos, entao nao perde referencia de quem ja aponta para ele.
    ///
    /// Menu: Tools > Redesign UI > Criar/atualizar skin da tela de localizacao
    /// </summary>
    public static class LocationSkinV2Builder
    {
        private const string SpriteDir = "Assets/Art/Sprites/REDESIGN-UI";
        private const string SkinPath  = "Assets/Resources/LocationSkinV2.asset";

        /// <summary>id do LocationData -> (sufixo de cor, sufixo de zona, accent, accentSoft).</summary>
        private static readonly (string id, string hue, string zone, string accent, string soft)[] Map =
        {
            ("bank",        "blue",   "financeira",  "#3B82F6", "#8AB6FF"),
            ("university",  "violet", "educacional", "#A855F7", "#CD9BFF"),
            ("store",       "amber",  "comercial",   "#EF9A2C", "#FFC272"),
            ("condominium", "green",  "residencial", "#22C55E", "#7EE6A4"),
            ("marketing",   "cyan",   "corporativa", "#22D3EE", "#86E9F8"),
        };

        [MenuItem("Tools/Redesign UI/Criar-atualizar skin da tela de localizacao")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(SpriteDir))
            {
                EditorUtility.DisplayDialog("Skin nao criada",
                    $"Nao achei a pasta {SpriteDir}.\n\n" +
                    "Copie os PNGs exportados para la e rode de novo.", "Ok");
                return;
            }

            var skin = AssetDatabase.LoadAssetAtPath<LocationSkinV2>(SkinPath);
            bool isNew = skin == null;

            if (isNew)
            {
                skin = ScriptableObject.CreateInstance<LocationSkinV2>();
                Directory.CreateDirectory(Path.GetDirectoryName(SkinPath) ?? "Assets/Resources");
                AssetDatabase.CreateAsset(skin, SkinPath);
            }

            var missing = new List<string>();

            // ---- partes fixas ----
            skin.panelBackgroundNeutral = Load("panel-bg",              missing);
            skin.statRowBackground      = Load("stat-row-bg",           missing);
            skin.statTileInvestment     = Load("stat-tile-blue-icon",   missing);
            skin.statTileTraffic        = Load("stat-tile-violet-icon", missing);
            skin.statTileCompetition    = Load("stat-tile-green-icon",  missing);
            skin.starIcon               = Load("star-blue",             missing);

            skin.continueNormal  = Load("btn-continuar",         missing);
            skin.continueHover   = Load("btn-continuar-hover",   missing);
            skin.continuePressed = Load("btn-continuar-pressed", missing);
            skin.menuNormal      = Load("btn-menu",              missing);
            skin.menuHover       = Load("btn-menu-hover",        missing);
            skin.menuDropdown    = Load("menu-dropdown-bg",      missing);
            skin.menuItemHover   = Load("menu-item-hover",       missing);

            skin.brandBadge       = Load("brand-badge",     missing);
            skin.ornamentLeft     = Load("ornament-left",   missing);
            skin.ornamentRight    = Load("ornament-right",  missing);
            skin.topbarBackground = Load("topbar-bg",       missing);
            skin.mapBase          = Load("map-base",        missing);
            skin.mapVignette      = Load("map-vignette",    missing);
            skin.labelPill        = Load("label-pill-bg",   missing);
            skin.toastBackground  = Load("toast-bg",        missing);

            // ---- zonas ----
            var zones = new List<LocationSkinV2.ZoneSkin>(Map.Length);

            foreach (var m in Map)
            {
                var zone = new LocationSkinV2.ZoneSkin
                {
                    id              = m.id,
                    accent          = Parse(m.accent),
                    accentSoft      = Parse(m.soft),
                    panelBackground = Load($"panel-bg-{m.hue}",        missing),
                    highlightBox    = Load($"highlight-{m.hue}",       missing),
                    panelIcon       = Load($"panel-icon-{m.zone}",     missing),
                    pinIdle         = Load($"pin-{m.zone}-idle",       missing),
                    pinActive       = Load($"pin-{m.zone}-active",     missing),
                    glowIdle        = Load($"glow-{m.zone}-idle",      missing),
                    glowActive      = Load($"glow-{m.zone}-active",    missing),
                };

                zones.Add(zone);
            }

            skin.EditorSetZones(zones);

            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = skin;
            EditorGUIUtility.PingObject(skin);

            string verb = isNew ? "criada" : "atualizada";

            if (missing.Count == 0)
            {
                Debug.Log($"[RedesignUI] Skin {verb} em {SkinPath} - todos os sprites encontrados.");
            }
            else
            {
                Debug.LogWarning(
                    $"[RedesignUI] Skin {verb} em {SkinPath}, mas {missing.Count} sprite(s) nao " +
                    $"foram encontrados:\n  - {string.Join("\n  - ", missing)}\n" +
                    "Os campos correspondentes ficaram vazios e a tela cai no visual antigo neles.");
            }
        }

        // =====================================================
        //  APOIO
        // =====================================================

        /// <summary>
        /// Carrega por nome EXATO de arquivo. FindAssets faz busca por prefixo,
        /// entao "panel-bg" tambem casaria com "panel-bg-blue" - por isso o
        /// filtro final compara o nome sem extensao.
        /// </summary>
        private static Sprite Load(string fileName, List<string> missing)
        {
            var guids = AssetDatabase.FindAssets($"{fileName} t:Sprite", new[] { SpriteDir });

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.Equals(Path.GetFileNameWithoutExtension(path), fileName,
                        System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

                if (sprite != null)
                    return sprite;
            }

            missing.Add(fileName + ".png");
            return null;
        }

        private static Color Parse(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }
    }
}
