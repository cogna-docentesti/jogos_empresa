#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Repinta as telas antigas com a paleta roxo Cogna.
    ///
    /// POR QUE NAO E UM "BUILDER":
    /// Panel_Location, Panel_Restaurant, Panel_MenuPricing e Panel_Financial
    /// foram montadas a mao, tem centenas de objetos e FUNCIONAM. Reconstruir
    /// essas telas por codigo seria destruir trabalho que ja esta certo para
    /// depois tentar reproduzi-lo - risco enorme, ganho zero.
    ///
    /// Esta ferramenta nao cria, nao apaga e nao move nada. Ela percorre a
    /// hierarquia existente e troca APENAS a cor de objetos cuja cor atual
    /// esta na tabela de correspondencia abaixo.
    ///
    /// TRES REGRAS DE SEGURANCA:
    ///  1. Lista branca. Cor que nao esta na tabela nao e tocada. Isso protege
    ///     sombras de texto (#000000), sprites tintados de branco e qualquer
    ///     cor autoral que voce tenha escolhido.
    ///  2. O ALPHA original e sempre preservado. Varios paineis usam a mesma
    ///     cor com transparencias diferentes, e isso precisa continuar.
    ///  3. Os paineis construidos pelo MenuPanelBuilder ficam de fora, porque
    ///     quem manda neles e o builder. Sem isso as duas ferramentas
    ///     brigariam pelo mesmo objeto.
    /// </summary>
    public static class LegacyScreenRestyler
    {
        private const string ScenePath = "Assets/Scenes/GameScene.unity";

        /// <summary>Telas montadas a mao, que esta ferramenta repinta.</summary>
        private static readonly string[] TargetRoots =
        {
            "Panel_Location",
            "Panel_Restaurant",
            "Panel_MenuPricing",
            "Panel_Financial",
            "Panel_Review"
        };

        /// <summary>Diferenca maxima por canal para considerar duas cores iguais (0..1).</summary>
        private const float MatchTolerance = 3f / 255f;

        // =====================================================
        //  TABELA DE CORRESPONDENCIA
        //  cor encontrada hoje  ->  token da paleta nova
        // =====================================================

        private sealed class ColorRule
        {
            public string From;
            public string To;
            public string Reason;
        }

        private static readonly ColorRule[] Rules =
        {
            // ---- superficies das telas antigas ----
            new ColorRule { From = "#13557B", To = GamePalette.HexLegacyHeader,     Reason = "barra de cabecalho" },
            new ColorRule { From = "#0D1B2D", To = GamePalette.HexLegacyBackground, Reason = "fundo" },
            new ColorRule { From = "#0D1B2A", To = GamePalette.HexLegacyBackground, Reason = "fundo" },
            new ColorRule { From = "#071630", To = GamePalette.HexLegacyBackground, Reason = "fundo" },
            new ColorRule { From = "#0D1622", To = GamePalette.HexLegacyBackground, Reason = "fundo" },
            new ColorRule { From = "#0F1E30", To = GamePalette.HexLegacyCard,       Reason = "card" },
            new ColorRule { From = "#1A2A3E", To = GamePalette.HexLegacyCard,       Reason = "card" },
            new ColorRule { From = "#0D1525", To = GamePalette.HexLegacyFooter,     Reason = "rodape" },
            new ColorRule { From = "#0A1220", To = GamePalette.HexLegacyFooter,     Reason = "rodape" },
            new ColorRule { From = "#1E3A5F", To = GamePalette.HexHairline,         Reason = "borda" },
            new ColorRule { From = "#2E7D5B", To = GamePalette.HexConfirm,          Reason = "botao confirmar" },
            new ColorRule { From = "#0068A4", To = GamePalette.HexNodeBank,         Reason = "botao de segmento" },
            new ColorRule { From = "#4A90D9", To = GamePalette.HexNodeBank,         Reason = "azul de apoio" },
            new ColorRule { From = "#2C63F5", To = GamePalette.HexPrimary,          Reason = "primaria antiga" },
            new ColorRule { From = "#2563EB", To = GamePalette.HexPrimary,          Reason = "primaria antiga" },

            // ---- texto ----
            new ColorRule { From = "#F0F6FF", To = GamePalette.HexInk,   Reason = "texto claro" },
            new ColorRule { From = "#EAF2FD", To = GamePalette.HexInk,   Reason = "texto claro" },
            new ColorRule { From = "#7A9CC0", To = GamePalette.HexMuted, Reason = "texto secundario" },
            new ColorRule { From = "#9CBADB", To = GamePalette.HexMuted, Reason = "texto secundario" },
            new ColorRule { From = "#7793B5", To = GamePalette.HexMuted, Reason = "texto secundario" },

            // ---- valores positivos ----
            new ColorRule { From = "#3ACF2C", To = GamePalette.HexMoney, Reason = "valor positivo" },
            new ColorRule { From = "#0ACA33", To = GamePalette.HexMoney, Reason = "valor positivo" },
            new ColorRule { From = "#72CF73", To = GamePalette.HexMoney, Reason = "valor positivo" },
            new ColorRule { From = "#16A34A", To = GamePalette.HexMoney, Reason = "valor positivo" },

            // ---- faixas de pontuacao ----
            new ColorRule { From = "#D99A0B", To = GamePalette.HexScoreAverage, Reason = "pontuacao media" },
            new ColorRule { From = "#E11D48", To = GamePalette.HexScoreBad,     Reason = "pontuacao ruim" },
            new ColorRule { From = "#DC2626", To = GamePalette.HexDanger,       Reason = "negativo" },
            new ColorRule { From = "#D97706", To = GamePalette.HexWarn,         Reason = "atencao" },

            // ---- fase roxo Cogna ----
            // Estes tons so existem se voce chegou a rodar a repintura roxa
            // antes de fecharmos o navy. Se nao rodou, estas regras nao
            // encontram nada e simplesmente nao fazem efeito.
            new ColorRule { From = "#150A20", To = GamePalette.HexBackground,   Reason = "fundo (fase roxa)" },
            new ColorRule { From = "#24103A", To = GamePalette.HexChrome,       Reason = "barra (fase roxa)" },
            new ColorRule { From = "#2E1547", To = GamePalette.HexSurface,      Reason = "card (fase roxa)" },
            new ColorRule { From = "#251139", To = GamePalette.HexSurfaceAlt,   Reason = "chip (fase roxa)" },
            new ColorRule { From = "#43265E", To = GamePalette.HexHairline,     Reason = "borda (fase roxa)" },
            new ColorRule { From = "#6B448C", To = GamePalette.HexBorderStrong, Reason = "borda forte (fase roxa)" },
            new ColorRule { From = "#1B0D2A", To = GamePalette.HexLegacyFooter, Reason = "rodape (fase roxa)" },
            new ColorRule { From = "#5F1890", To = GamePalette.HexLegacyHeader, Reason = "cabecalho (fase roxa)" },
            new ColorRule { From = "#B9AFC7", To = GamePalette.HexMuted,        Reason = "texto secundario (fase roxa)" },
            new ColorRule { From = "#3DD68C", To = GamePalette.HexMoney,        Reason = "positivo (fase roxa)" },
            new ColorRule { From = "#2E8B63", To = GamePalette.HexConfirm,      Reason = "confirmar (fase roxa)" },
            new ColorRule { From = "#F2C230", To = GamePalette.HexGold,         Reason = "score (fase roxa)" },

            // ---- selecao verde-limao do RestaurantScreenView ----
            new ColorRule { From = "#B3FF00", To = GamePalette.HexBorderSelected,  Reason = "borda selecionada" },
            new ColorRule { From = "#73FF1A", To = GamePalette.HexCardSelected,    Reason = "card selecionado" },
            new ColorRule { From = "#2E9E2E", To = GamePalette.HexSegmentSelected, Reason = "segmento selecionado" },
            new ColorRule { From = "#141F33", To = GamePalette.HexCardNormal,      Reason = "card normal" },
            new ColorRule { From = "#293D5C", To = GamePalette.HexBorderNormal,    Reason = "borda normal" }
        };

        // =====================================================
        //  MENUS
        // =====================================================

        [MenuItem("Tools/Jogo/Telas antigas/1 - Simular repintura (nao altera nada)", false, 60)]
        public static void DryRun()
        {
            Run(apply: false, scaleFonts: false);
        }

        [MenuItem("Tools/Jogo/Telas antigas/2 - Repintar com a paleta roxa", false, 61)]
        public static void Apply()
        {
            Run(apply: true, scaleFonts: false);
        }

        [MenuItem("Tools/Jogo/Telas antigas/3 - Simular aumento de fonte (+25%)", false, 80)]
        public static void DryRunFonts()
        {
            Run(apply: false, scaleFonts: true, onlyFonts: true);
        }

        [MenuItem("Tools/Jogo/Telas antigas/4 - Aumentar fonte em +25%", false, 81)]
        public static void ApplyFonts()
        {
            if (!EditorUtility.DisplayDialog(
                    "Aumentar as fontes?",
                    "As caixas de texto destas telas foram dimensionadas a mao para a fonte atual.\n\n"
                    + "Aumentar a fonte em 25% pode fazer algum texto estourar ou quebrar linha "
                    + "onde antes cabia.\n\nRode antes a simulacao (item 3) e confira a lista no Console.\n\n"
                    + "Ctrl+Z desfaz.",
                    "Aumentar mesmo assim", "Cancelar"))
                return;

            Run(apply: true, scaleFonts: true, onlyFonts: true);
        }

        // =====================================================
        //  EXECUCAO
        // =====================================================

        private static void Run(bool apply, bool scaleFonts, bool onlyFonts = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Saia do Play Mode",
                    "Alteracoes de cena feitas durante o Play sao descartadas ao parar o jogo.",
                    "Entendi");
                return;
            }

            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                if (!EditorUtility.DisplayDialog("Abrir a GameScene?",
                        "Esta ferramenta trabalha na Assets/Scenes/GameScene.unity.",
                        "Abrir", "Cancelar"))
                    return;

                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                EditorSceneManager.OpenScene(ScenePath);
            }

            var lookup = BuildLookup();
            var report = new List<string>();

            int colorChanges = 0;
            int fontChanges  = 0;
            int missingRoots = 0;

            foreach (var rootName in TargetRoots)
            {
                var root = FindInScene(rootName);

                if (root == null)
                {
                    report.Add($"  (ausente) {rootName} nao existe nesta cena.");
                    missingRoots++;
                    continue;
                }

                report.Add($"  == {rootName} ==");

                if (!onlyFonts)
                    colorChanges += RestyleColors(root.transform, lookup, apply, report);

                if (scaleFonts)
                    fontChanges += ScaleFonts(root.transform, apply, report);
            }

            string header = apply ? "REPINTURA APLICADA" : "SIMULACAO (nada foi alterado)";
            string body = string.Join("\n", report);

            Debug.Log($"[LegacyScreenRestyler] {header}\n"
                    + $"Cores trocadas: {colorChanges} | Fontes ajustadas: {fontChanges}"
                    + (missingRoots > 0 ? $" | Paineis ausentes: {missingRoots}" : "") + "\n\n"
                    + body);

            if (!apply)
                return;

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[LegacyScreenRestyler] Lembre de salvar a cena (Ctrl+S). Ctrl+Z desfaz tudo.");
        }

        private static int RestyleColors(Transform root, List<KeyValuePair<Color, ColorRule>> lookup,
                                         bool apply, List<string> report)
        {
            int changed = 0;

            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                // Paineis do builder tem dono proprio.
                if (IsBuilderOwned(graphic.transform))
                    continue;

                var current = graphic.color;

                if (!TryMatch(current, lookup, out var rule))
                    continue;

                var target = GamePalette.Parse(rule.To);
                target.a = current.a; // o alpha original e sempre respeitado

                if (Approximately(current, target))
                    continue;

                report.Add($"     {Hex(current)} -> {Hex(target)}  [{rule.Reason}]  {Path(graphic.transform)}");

                if (apply)
                {
                    Undo.RecordObject(graphic, "Repintar tela antiga");
                    graphic.color = target;
                    EditorUtility.SetDirty(graphic);
                }

                changed++;
            }

            return changed;
        }

        private static int ScaleFonts(Transform root, bool apply, List<string> report)
        {
            const float factor = 1.25f;
            int changed = 0;

            foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (IsBuilderOwned(label.transform))
                    continue;

                float before = label.fontSize;

                if (before <= 0f)
                    continue;

                float after = Mathf.Round(before * factor);

                if (Mathf.Approximately(before, after))
                    continue;

                report.Add($"     fonte {before:0} -> {after:0}  {Path(label.transform)}");

                if (apply)
                {
                    Undo.RecordObject(label, "Aumentar fonte");
                    label.fontSize = after;

                    // Auto size desligado com tamanho maior pode cortar texto.
                    // Avisar e melhor do que mudar silenciosamente o comportamento.
                    EditorUtility.SetDirty(label);
                }

                changed++;
            }

            return changed;
        }

        // =====================================================
        //  APOIO
        // =====================================================

        private static List<KeyValuePair<Color, ColorRule>> BuildLookup()
        {
            return Rules
                .Select(r => new KeyValuePair<Color, ColorRule>(GamePalette.Parse(r.From), r))
                .ToList();
        }

        private static bool TryMatch(Color color, List<KeyValuePair<Color, ColorRule>> lookup, out ColorRule rule)
        {
            foreach (var pair in lookup)
            {
                if (Mathf.Abs(pair.Key.r - color.r) <= MatchTolerance &&
                    Mathf.Abs(pair.Key.g - color.g) <= MatchTolerance &&
                    Mathf.Abs(pair.Key.b - color.b) <= MatchTolerance)
                {
                    rule = pair.Value;
                    return true;
                }
            }

            rule = null;
            return false;
        }

        private static bool Approximately(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.002f
                && Mathf.Abs(a.g - b.g) < 0.002f
                && Mathf.Abs(a.b - b.b) < 0.002f
                && Mathf.Abs(a.a - b.a) < 0.002f;
        }

        /// <summary>
        /// Os paineis do MenuPanelBuilder sao repintados por ele, nao por aqui.
        /// Deixar as duas ferramentas mexerem no mesmo objeto so geraria
        /// resultado dependente da ordem em que voce roda cada uma.
        /// </summary>
        private static bool IsBuilderOwned(Transform t)
        {
            while (t != null)
            {
                switch (t.name)
                {
                    case "Panel_Menu":
                    case "Panel_Establishment":
                    case "Panel_Team":
                    case "Panel_EquipmentStore":
                    case "MenuAccess":
                        return true;
                }

                t = t.parent;
            }

            return false;
        }

        private static string Hex(Color c)
        {
            string hex = "#" + ColorUtility.ToHtmlStringRGB(c);
            return c.a < 0.999f ? $"{hex}@{c.a:0.00}" : hex;
        }

        private static string Path(Transform t)
        {
            string path = t.name;

            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }

        private static GameObject FindInScene(string name)
        {
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name == name)
                    return go;
            }

            return null;
        }
    }
}
#endif
