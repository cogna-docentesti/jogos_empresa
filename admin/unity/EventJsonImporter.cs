using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Ponte entre o painel admin e os ScriptableObjects EventData.
    ///
    ///   admin (navegador) -> admin-data/events.json -> [Importar] -> Assets/Resources/Events/*.asset
    ///   Assets/Resources/Events/*.asset -> [Exportar] -> admin-data/events.json
    ///
    /// Onde colocar: Assets/Editor/Events/EventJsonImporter.cs
    /// (qualquer pasta chamada "Editor" funciona; o script nao entra no build do jogo).
    /// </summary>
    public static class EventJsonImporter
    {
        private const string AssetDirectory = "Assets/Resources/Events";
        private const string DefaultJsonRelativePath = "admin-data/events.json";
        private const string LogPrefix = "[EventJsonImporter]";

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string DefaultJsonPath => Path.Combine(ProjectRoot, DefaultJsonRelativePath);

        // ------------------------------------------------------------------
        // Menus
        // ------------------------------------------------------------------

        [MenuItem("Tools/Jogo/Eventos/Importar JSON do Admin", priority = 100)]
        public static void ImportDefaultMenu()
        {
            if (!File.Exists(DefaultJsonPath))
            {
                EditorUtility.DisplayDialog(
                    "Arquivo não encontrado",
                    $"Não encontrei {DefaultJsonRelativePath} na raiz do projeto.\n\n" +
                    "Se o admin está publicado, faça git pull. Ou use \"Importar JSON de outro arquivo...\".",
                    "OK");
                return;
            }

            Import(DefaultJsonPath);
        }

        [MenuItem("Tools/Jogo/Eventos/Importar JSON de outro arquivo...", priority = 101)]
        public static void ImportFromFileMenu()
        {
            string path = EditorUtility.OpenFilePanel("Selecione o events.json exportado pelo admin", ProjectRoot, "json");
            if (!string.IsNullOrEmpty(path))
                Import(path);
        }

        [MenuItem("Tools/Jogo/Eventos/Exportar assets para JSON do Admin", priority = 102)]
        public static void ExportMenu()
        {
            if (File.Exists(DefaultJsonPath) &&
                !EditorUtility.DisplayDialog(
                    "Sobrescrever JSON?",
                    $"{DefaultJsonRelativePath} já existe e será substituído pelos dados dos assets atuais.",
                    "Sobrescrever", "Cancelar"))
            {
                return;
            }

            Export(DefaultJsonPath);
        }

        // ------------------------------------------------------------------
        // Importacao
        // ------------------------------------------------------------------

        public static void Import(string jsonPath)
        {
            EventCatalogDto catalog;
            try
            {
                catalog = JsonUtility.FromJson<EventCatalogDto>(File.ReadAllText(jsonPath, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                Fail($"Não foi possível ler o JSON: {ex.Message}");
                return;
            }

            if (catalog?.items == null)
            {
                Fail("O JSON não possui a lista \"items\".");
                return;
            }

            var errors = new List<string>();
            var converted = new List<EventValues>();

            foreach (var dto in catalog.items)
            {
                var values = Convert(dto, errors);
                if (values != null)
                    converted.Add(values);
            }

            Validate(converted, errors);
            ValidateAssetPaths(converted, errors);

            if (errors.Count > 0)
            {
                Debug.LogError($"{LogPrefix} Importação cancelada. Nenhum asset foi alterado:\n- {string.Join("\n- ", errors)}");
                EditorUtility.DisplayDialog("Importação cancelada",
                    $"{errors.Count} problema(s) encontrado(s). Veja os detalhes no Console.", "OK");
                return;
            }

            EnsureFolder(AssetDirectory);

            int created = 0;
            int updated = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var values in converted)
                {
                    string path = GetAssetPath(values.id);
                    var asset = AssetDatabase.LoadAssetAtPath<EventData>(path);

                    if (asset == null)
                    {
                        asset = ScriptableObject.CreateInstance<EventData>();
                        values.ApplyTo(asset);
                        AssetDatabase.CreateAsset(asset, path);
                        created++;
                    }
                    else
                    {
                        values.ApplyTo(asset);
                        EditorUtility.SetDirty(asset);
                        updated++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            int removed = RemoveOrphans(new HashSet<string>(converted.Select(x => x.id), StringComparer.Ordinal));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string summary = $"{created} criado(s), {updated} atualizado(s), {removed} removido(s).";
            Debug.Log($"{LogPrefix} Importação concluída a partir de {jsonPath}: {summary}");
            EditorUtility.DisplayDialog("Eventos importados", summary, "OK");
        }

        /// <summary>Assets de evento que existem na pasta, mas nao estao mais no JSON.</summary>
        private static int RemoveOrphans(HashSet<string> idsInJson)
        {
            var orphans = new List<(string id, string path)>();

            foreach (string guid in AssetDatabase.FindAssets("t:EventData", new[] { AssetDirectory }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<EventData>(path);
                if (asset != null && !idsInJson.Contains(asset.id ?? string.Empty))
                    orphans.Add((asset.id, path));
            }

            if (orphans.Count == 0)
                return 0;

            string list = string.Join("\n", orphans.Take(15).Select(o => $"• {o.id}"));
            if (orphans.Count > 15)
                list += $"\n• ... e mais {orphans.Count - 15}";

            bool remove = EditorUtility.DisplayDialog(
                "Eventos removidos no admin",
                $"{orphans.Count} asset(s) não existem mais no JSON:\n\n{list}\n\nDeseja apagar esses assets?",
                "Apagar assets", "Manter");

            if (!remove)
                return 0;

            foreach (var orphan in orphans)
                AssetDatabase.DeleteAsset(orphan.path);

            return orphans.Count;
        }

        private static EventValues Convert(EventDto dto, List<string> errors)
        {
            string label = string.IsNullOrWhiteSpace(dto?.id) ? "(sem ID)" : dto.id;
            if (dto == null)
            {
                errors.Add("Existe um item nulo na lista.");
                return null;
            }

            bool ok = true;

            EventPolarity polarity = ParseEnum<EventPolarity>(dto.polarity, $"{label}.polarity", errors, ref ok);
            EventTriggerType trigger = ParseEnum<EventTriggerType>(dto.triggerType, $"{label}.triggerType", errors, ref ok);

            var restaurantTypes = (dto.applicableRestaurantTypes ?? Array.Empty<string>())
                .Select((value, i) => ParseEnum<RestaurantType>(value, $"{label}.applicableRestaurantTypes[{i}]", errors, ref ok))
                .ToArray();

            var conditions = (dto.conditions ?? Array.Empty<EventConditionDto>())
                .Select((c, i) => new EventConditionData
                {
                    type = ParseEnum<EventConditionType>(c.type, $"{label}.conditions[{i}].type", errors, ref ok),
                    comparison = ParseEnum<EventConditionOperator>(c.comparison, $"{label}.conditions[{i}].comparison", errors, ref ok),
                    value = c.value
                })
                .ToArray();

            var options = (dto.options ?? Array.Empty<EventOptionDto>())
                .Select((o, i) =>
                {
                    var fx = o.effects ?? new EventEffectDto();
                    return new EventOption
                    {
                        id = o.id,
                        title = o.title,
                        description = o.description,
                        effects = new EventEffectData
                        {
                            cashDelta = fx.cashDelta,
                            demandMultiplier = fx.demandMultiplier,
                            capacityMultiplier = fx.capacityMultiplier,
                            stockMultiplier = fx.stockMultiplier,
                            ingredientCostMultiplier = fx.ingredientCostMultiplier,
                            averageTicketMultiplier = fx.averageTicketMultiplier,
                            reputationDelta = fx.reputationDelta,
                            duration = ParseEnum<EventEffectDuration>(fx.duration, $"{label}.options[{i}].effects.duration", errors, ref ok)
                        }
                    };
                })
                .ToArray();

            if (!ok)
                return null;

            return new EventValues
            {
                id = dto.id,
                title = dto.title,
                description = dto.description,
                educationalConcept = dto.educationalConcept ?? string.Empty,
                polarity = polarity,
                triggerType = trigger,
                baseWeight = dto.baseWeight,
                canRepeat = dto.canRepeat,
                applicableRestaurantTypes = restaurantTypes,
                minRound = dto.minRound,
                maxRound = dto.maxRound,
                conditions = conditions,
                options = options
            };
        }

        private static TEnum ParseEnum<TEnum>(string value, string where, List<string> errors, ref bool ok)
            where TEnum : struct
        {
            if (!string.IsNullOrEmpty(value) &&
                Enum.TryParse(value, false, out TEnum parsed) &&
                Enum.IsDefined(typeof(TEnum), parsed) &&
                !int.TryParse(value, out _))
            {
                return parsed;
            }

            errors.Add($"{where}: \"{value}\" não é um valor válido de {typeof(TEnum).Name}.");
            ok = false;
            return default(TEnum);
        }

        /// <summary>Mesmas regras de erro do EventAssetGenerator (sem as regras de quantidade do catalogo).</summary>
        private static void Validate(List<EventValues> events, List<string> errors)
        {
            foreach (var duplicate in events.GroupBy(x => x.id).Where(g => g.Count() > 1))
                errors.Add($"ID de evento duplicado: '{duplicate.Key}'.");

            foreach (var e in events)
            {
                if (string.IsNullOrWhiteSpace(e.id))
                    errors.Add("Existe um evento sem ID.");
                else if (!IsSnakeCase(e.id))
                    errors.Add($"O ID de evento '{e.id}' deve estar em snake_case.");

                if (string.IsNullOrWhiteSpace(e.title))
                    errors.Add($"O evento '{e.id}' deve possuir title.");

                if (string.IsNullOrWhiteSpace(e.description))
                    errors.Add($"O evento '{e.id}' deve possuir description.");

                if (e.options == null || e.options.Length != 3)
                    errors.Add($"O evento '{e.id}' deve possuir exatamente 3 opções.");
                else
                {
                    foreach (var duplicate in e.options.GroupBy(x => x.id).Where(g => g.Count() > 1))
                        errors.Add($"O evento '{e.id}' possui o ID de opção duplicado '{duplicate.Key}'.");

                    if (e.options.Any(x => string.IsNullOrWhiteSpace(x.id)))
                        errors.Add($"O evento '{e.id}' possui uma opção sem ID.");
                }

                if (e.baseWeight <= 0f)
                    errors.Add($"O evento '{e.id}' deve possuir baseWeight positivo.");

                if (e.minRound < 1 || e.maxRound < e.minRound)
                    errors.Add($"O evento '{e.id}' possui intervalo de rodadas inválido ({e.minRound}-{e.maxRound}).");

                int conditionCount = e.conditions?.Length ?? 0;
                if (e.triggerType == EventTriggerType.CONDITIONAL && conditionCount == 0)
                    errors.Add($"O evento CONDITIONAL '{e.id}' deve possuir ao menos uma condição.");

                if (e.triggerType == EventTriggerType.RANDOM && conditionCount != 0)
                    errors.Add($"O evento RANDOM '{e.id}' não pode possuir condições.");
            }
        }

        private static void ValidateAssetPaths(List<EventValues> events, List<string> errors)
        {
            if (!AssetDatabase.IsValidFolder(AssetDirectory))
                return;

            foreach (var e in events.Where(x => !string.IsNullOrWhiteSpace(x.id)))
            {
                string expectedPath = GetAssetPath(e.id);
                UnityEngine.Object objectAtPath = AssetDatabase.LoadMainAssetAtPath(expectedPath);

                if (objectAtPath != null && !(objectAtPath is EventData))
                    errors.Add($"O caminho '{expectedPath}' já está ocupado por um asset que não é EventData.");
                else if (objectAtPath is EventData existing && !string.Equals(existing.id, e.id, StringComparison.Ordinal))
                    errors.Add($"O caminho '{expectedPath}' contém EventData com ID '{existing.id}'.");
            }
        }

        // ------------------------------------------------------------------
        // Exportacao (JSON no mesmo formato gravado pelo admin)
        // ------------------------------------------------------------------

        public static void Export(string jsonPath)
        {
            var assets = AssetDatabase.FindAssets("t:EventData", new[] { AssetDirectory })
                .Select(guid => AssetDatabase.LoadAssetAtPath<EventData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(a => a != null)
                .ToList();

            // Mantem a ordem do JSON atual (evita diffs enormes no Git); novos IDs vao para o final.
            var currentOrder = ReadIdOrder(jsonPath);
            assets = assets
                .OrderBy(a => currentOrder.TryGetValue(a.id ?? string.Empty, out int index) ? index : int.MaxValue)
                .ThenBy(a => a.id, StringComparer.Ordinal)
                .ToList();

            var json = new JsonWriter();
            json.BeginObject();
            json.Property("schemaVersion", 1);
            json.Property("collection", "events");
            json.Property("updatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
            json.BeginArray("items");

            foreach (var e in assets)
            {
                json.BeginObject();
                json.Property("id", e.id);
                json.Property("title", e.title);
                json.Property("description", e.description);
                json.Property("educationalConcept", e.educationalConcept);
                json.Property("polarity", e.polarity.ToString());
                json.Property("triggerType", e.triggerType.ToString());
                json.Property("baseWeight", e.baseWeight);
                json.Property("canRepeat", e.canRepeat);
                json.StringArray("applicableRestaurantTypes", (e.applicableRestaurantTypes ?? Array.Empty<RestaurantType>()).Select(x => x.ToString()));
                json.Property("minRound", e.minRound);
                json.Property("maxRound", e.maxRound);

                json.BeginArray("conditions");
                foreach (var c in e.conditions ?? Array.Empty<EventConditionData>())
                {
                    json.BeginObject();
                    json.Property("type", c.type.ToString());
                    json.Property("comparison", c.comparison.ToString());
                    json.Property("value", c.value);
                    json.EndObject();
                }
                json.EndArray();

                json.BeginArray("options");
                foreach (var o in e.options ?? Array.Empty<EventOption>())
                {
                    var fx = o.effects ?? new EventEffectData();
                    json.BeginObject();
                    json.Property("id", o.id);
                    json.Property("title", o.title);
                    json.Property("description", o.description);
                    json.BeginObject("effects");
                    json.Property("cashDelta", fx.cashDelta);
                    json.Property("demandMultiplier", fx.demandMultiplier);
                    json.Property("capacityMultiplier", fx.capacityMultiplier);
                    json.Property("stockMultiplier", fx.stockMultiplier);
                    json.Property("ingredientCostMultiplier", fx.ingredientCostMultiplier);
                    json.Property("averageTicketMultiplier", fx.averageTicketMultiplier);
                    json.Property("reputationDelta", fx.reputationDelta);
                    json.Property("duration", fx.duration.ToString());
                    json.EndObject();
                    json.EndObject();
                }
                json.EndArray();

                json.EndObject();
            }

            json.EndArray();
            json.EndObject();

            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath));
            File.WriteAllText(jsonPath, json.ToString(), new UTF8Encoding(false));

            Debug.Log($"{LogPrefix} {assets.Count} evento(s) exportado(s) para {jsonPath}.");
            EditorUtility.DisplayDialog("Eventos exportados", $"{assets.Count} evento(s) gravado(s) em\n{jsonPath}", "OK");
        }

        // ------------------------------------------------------------------
        // Utilitarios
        // ------------------------------------------------------------------

        private static Dictionary<string, int> ReadIdOrder(string jsonPath)
        {
            var order = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!File.Exists(jsonPath))
                return order;

            try
            {
                var catalog = JsonUtility.FromJson<EventCatalogDto>(File.ReadAllText(jsonPath, Encoding.UTF8));
                var items = catalog?.items ?? Array.Empty<EventDto>();
                for (int i = 0; i < items.Length; i++)
                {
                    if (!string.IsNullOrEmpty(items[i]?.id) && !order.ContainsKey(items[i].id))
                        order.Add(items[i].id, i);
                }
            }
            catch (Exception)
            {
                // JSON invalido: exporta em ordem alfabetica
            }

            return order;
        }

        private static void Fail(string message)
        {
            Debug.LogError($"{LogPrefix} {message}");
            EditorUtility.DisplayDialog("Importação cancelada", message, "OK");
        }

        private static string GetAssetPath(string id) => $"{AssetDirectory}/{id}.asset";

        private static bool IsSnakeCase(string value)
        {
            if (value.Length == 0 || value[0] == '_' || value[value.Length - 1] == '_')
                return false;

            bool previousWasUnderscore = false;
            foreach (char character in value)
            {
                bool isUnderscore = character == '_';
                if (!(character >= 'a' && character <= 'z') &&
                    !(character >= '0' && character <= '9') &&
                    !isUnderscore)
                    return false;

                if (isUnderscore && previousWasUnderscore)
                    return false;

                previousWasUnderscore = isUnderscore;
            }

            return true;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // ------------------------------------------------------------------
        // Valores convertidos (ja com enums do jogo)
        // ------------------------------------------------------------------

        private sealed class EventValues
        {
            public string id;
            public string title;
            public string description;
            public string educationalConcept;
            public EventPolarity polarity;
            public EventTriggerType triggerType;
            public float baseWeight;
            public bool canRepeat;
            public RestaurantType[] applicableRestaurantTypes;
            public int minRound;
            public int maxRound;
            public EventConditionData[] conditions;
            public EventOption[] options;

            public void ApplyTo(EventData asset)
            {
                asset.id = id;
                asset.title = title;
                asset.description = description;
                asset.educationalConcept = educationalConcept;
                asset.polarity = polarity;
                asset.triggerType = triggerType;
                asset.baseWeight = baseWeight;
                asset.canRepeat = canRepeat;
                asset.applicableRestaurantTypes = applicableRestaurantTypes;
                asset.minRound = minRound;
                asset.maxRound = maxRound;
                asset.conditions = conditions;
                asset.options = options;
            }
        }

        // ------------------------------------------------------------------
        // DTOs do JSON (enums chegam como texto, ex.: "NEGATIVE")
        // ------------------------------------------------------------------

#pragma warning disable 0649 // campos preenchidos pelo JsonUtility

        [Serializable]
        private class EventCatalogDto
        {
            public int schemaVersion;
            public string collection;
            public string updatedAt;
            public EventDto[] items;
        }

        [Serializable]
        private class EventDto
        {
            public string id;
            public string title;
            public string description;
            public string educationalConcept;
            public string polarity;
            public string triggerType;
            public float baseWeight = 1f;
            public bool canRepeat;
            public string[] applicableRestaurantTypes;
            public int minRound = 1;
            public int maxRound = 3;
            public EventConditionDto[] conditions;
            public EventOptionDto[] options;
        }

        [Serializable]
        private class EventConditionDto
        {
            public string type;
            public string comparison;
            public float value;
        }

        [Serializable]
        private class EventOptionDto
        {
            public string id;
            public string title;
            public string description;
            public EventEffectDto effects;
        }

        [Serializable]
        private class EventEffectDto
        {
            public float cashDelta = 0f;
            public float demandMultiplier = 1f;
            public float capacityMultiplier = 1f;
            public float stockMultiplier = 1f;
            public float ingredientCostMultiplier = 1f;
            public float averageTicketMultiplier = 1f;
            public int reputationDelta = 0;
            public string duration = "IMMEDIATE";
        }

#pragma warning restore 0649

        // ------------------------------------------------------------------
        // Escritor JSON minimo: 2 espacos de indentacao, igual ao admin,
        // para que o diff no Git mostre apenas o que realmente mudou.
        // ------------------------------------------------------------------

        private sealed class JsonWriter
        {
            private readonly StringBuilder sb = new StringBuilder();
            private readonly Stack<bool> hasItems = new Stack<bool>();

            private void Indent() => sb.Append(' ', hasItems.Count * 2);

            private void Separator()
            {
                if (hasItems.Count == 0) return;
                if (hasItems.Peek()) sb.Append(',');
                sb.Append('\n');
                hasItems.Pop();
                hasItems.Push(true);
                Indent();
            }

            private void Key(string name)
            {
                Separator();
                if (name != null) sb.Append(Quote(name)).Append(": ");
            }

            public void BeginObject(string name = null) { Key(name); sb.Append('{'); hasItems.Push(false); }
            public void EndObject() { Close('}'); }
            public void BeginArray(string name) { Key(name); sb.Append('['); hasItems.Push(false); }
            public void EndArray() { Close(']'); }

            private void Close(char bracket)
            {
                bool any = hasItems.Pop();
                if (any) { sb.Append('\n'); Indent(); }
                sb.Append(bracket);
            }

            public void Property(string name, string value) { Key(name); sb.Append(Quote(value ?? string.Empty)); }
            public void Property(string name, int value) { Key(name); sb.Append(value.ToString(CultureInfo.InvariantCulture)); }
            public void Property(string name, bool value) { Key(name); sb.Append(value ? "true" : "false"); }
            public void Property(string name, float value) { Key(name); sb.Append(FormatFloat(value)); }

            public void StringArray(string name, IEnumerable<string> values)
            {
                var list = values.ToList();
                Key(name);
                if (list.Count == 0) { sb.Append("[]"); return; }
                sb.Append('[');
                hasItems.Push(false);
                foreach (var v in list) { Separator(); sb.Append(Quote(v)); }
                Close(']');
            }

            private static string FormatFloat(float value)
            {
                // "R" gera a menor representacao que volta ao mesmo float (0.9f -> "0.9")
                return value.ToString("R", CultureInfo.InvariantCulture);
            }

            private static string Quote(string value)
            {
                var q = new StringBuilder("\"");
                foreach (char c in value)
                {
                    switch (c)
                    {
                        case '"': q.Append("\\\""); break;
                        case '\\': q.Append("\\\\"); break;
                        case '\n': q.Append("\\n"); break;
                        case '\r': q.Append("\\r"); break;
                        case '\t': q.Append("\\t"); break;
                        default:
                            if (c < 0x20) q.Append("\\u").Append(((int)c).ToString("x4"));
                            else q.Append(c);
                            break;
                    }
                }
                return q.Append('"').ToString();
            }

            public override string ToString() => sb.ToString() + "\n";
        }
    }
}
