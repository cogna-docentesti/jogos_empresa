using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Adapter.In.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace Game.Adapter.In.UI
{
    public sealed class MonthlyCalendarView : MonoBehaviour
    {
        private const int Columns = 6;
        private const int DetailRows = 10;
        private static readonly CultureInfo Currency = CultureInfo.GetCultureInfo("pt-BR");

        private sealed class Tile
        {
            public Button Button;
            public SharedControlAppearance Appearance;
            public Image Pin;
            public readonly Image[] Icons = new Image[2];
            public readonly TMP_Text[] Captions = new TMP_Text[2];
        }

        private readonly struct Commitment
        {
            public readonly string Name, State, Icon;
            public readonly Color Color;
            public readonly bool IsEvent, IsMilestone;

            public Commitment(string name, string state, string icon, Color color, bool isEvent = false, bool isMilestone = false)
            { Name = name; State = state; Icon = icon; Color = color; IsEvent = isEvent; IsMilestone = isMilestone; }
        }

        private readonly Tile[] tiles = new Tile[MonthlyCalendarService.Days];
        private readonly List<Commitment>[] agenda = new List<Commitment>[MonthlyCalendarService.Days];
        private readonly TMP_Text[] detailNames = new TMP_Text[DetailRows], detailStates = new TMP_Text[DetailRows];
        private readonly Image[] detailIcons = new Image[DetailRows];
        private TMP_Text period, position, cash, detailsHeading, empty, status, advanceLabel;
        private Button advance, back;
        private RectTransform modalSurface, review;
        private SharedModalFocus modal;
        private TMP_Text reviewPeriod, reviewTeam, reviewMenu, reviewResources, reviewStatus, eventDay;
        private Button reviewConfirm, reviewBack, reviewClose;
        private Button policiesButton;
        private RectTransform policiesPanel;
        public Game.Domain.Simulation.MonthPolicies ChosenPolicies { get; private set; }
        private int shownDay, selectedDay;
        private float pinShownAt;
        private Action onAdvance, onBack;

        public void Bind(Action advanceAction, Action backAction)
        {
            onAdvance = advanceAction;
            onBack = backAction;
            Build();
        }

        public void Render(MonthlyCalendarService.View plan, IReadOnlyList<EventData> catalog)
        {
            period.text = $"MÊS {plan.Month:D2}";
            cash.text = Money(plan.CashCents);
            for (int i = 0; i < agenda.Length; i++) agenda[i].Clear();
            foreach (var bill in plan.Bills)
            {
                bool paid = plan.Receipts.Any(r => r.type == bill.type && r.day == bill.day);
                var (name, icon) = BillPresentation(bill.type);
                var state = paid ? $"Pago · {Money(bill.amountCents)}"
                    : bill.estimated ? "Previsto · valor calculado no dia" : $"A pagar · {Money(bill.amountCents)}";
                agenda[bill.day - 1].Add(new Commitment(name, state, paid ? "ICON_CHECK" : icon,
                    paid ? GamePalette.PositiveReportText : GamePalette.Ink));
            }
            foreach (var occurrence in plan.Events)
            {
                var data = catalog.FirstOrDefault(e => e.id == occurrence.EventId);
                string state = occurrence.Decided ? "Decisão registrada"
                    : plan.Pending == occurrence ? "Decisão pendente" : "Evento anunciado";
                agenda[occurrence.Day - 1].Add(new Commitment(occurrence.Title ?? (data != null ? data.title : "Evento"),
                    state, occurrence.Decided ? "ICON_CHECK" : occurrence.Announced ? "ICON_STAR" : "ICON_ALERT",
                    occurrence.Decided ? GamePalette.PositiveReportText : GamePalette.Primary, isEvent: true));
            }
            agenda[MonthlyCalendarService.Days - 1].Add(new Commitment("Fechamento",
                plan.ReadyToClose ? "Pronto para fechar" : "Após os compromissos", "ICON_DOCUMENT", GamePalette.Primary, isMilestone: true));
            for (int i = 0; i < tiles.Length; i++)
            {
                var items = agenda[i];
                var entries = items.OrderByDescending(item => item.IsMilestone).ThenByDescending(item => item.IsEvent).ToArray();
                RenderTile(tiles[i], entries);
            }

            ShowPosition(plan.Day);
            SelectDay(plan.Day);
            long debited = plan.Receipts.Where(r => r.day == plan.Day).Sum(r => r.amountCents);
            Status(debited > 0 ? $"Dia {plan.Day:D2}  ·  {Money(debited)} debitados  ·  Caixa {Money(plan.CashCents)}"
                : plan.Pending != null ? "Resolva o evento deste dia para continuar."
                : plan.ReadyToClose ? "Dia 30 concluído. Feche o mês para ver a DRE."
                : plan.Day == 0 ? "Avance até o primeiro compromisso." : "Avance até o próximo compromisso.");
        }

        public void RenderPreparation(GameSessionEntity session)
        {
            period.text = $"MÊS {session.currentRound:D2}";
            cash.text = session.currentCash.ToString("C", Currency);
            for (int i = 0; i < agenda.Length; i++)
            {
                agenda[i].Clear();
                RenderTile(tiles[i], agenda[i]);
            }
            ShowPosition(0);
            SelectDay(0);
            empty.text = "A agenda aparece após a confirmação.";
            Status("Revise as decisões para iniciar o mês.");
            SetControls(false, false, "Avançar");
        }

        public void ShowReview(GameSessionEntity session, Action confirm, Action cancel)
        {
            if (policiesButton != null) policiesButton.gameObject.SetActive(false);
            if (policiesPanel != null) policiesPanel.gameObject.SetActive(false);
            ChosenPolicies = null;
            var restaurant = Resources.LoadAll<RestaurantData>("Restaurants").FirstOrDefault(r => r.type == session.restaurantType);
            var roles = Resources.LoadAll<RoleData>("Roles");
            bool valid = GameSessionService.HasMinimumTeam(session);
            var team = TeamSelectionHelper.FromJson(session.teamJson);
            var minimums = restaurant != null ? TeamSelectionHelper.GetMinimums(restaurant) : new Dictionary<string, int>();
            var actual = team.members.Where(m => m != null).ToArray();
            var roleIds = minimums.Keys.Concat(actual.Select(m => m.roleId)).Distinct();
            reviewTeam.text = string.Join("\n", roleIds.Select(id =>
            {
                var role = roles.FirstOrDefault(r => r.id == id);
                string name = role != null ? TeamRoleCardView.Label(role) : id;
                int count = actual.FirstOrDefault(m => m.roleId == id)?.quantity ?? 0;
                if (!minimums.TryGetValue(id, out int minimum)) return $"{name}  ·  {count}";
                string missing = count < minimum ? $"  ·  Falta: {minimum - count}" : "";
                return $"{name}  ·  {count}/{minimum}{missing}";
            }));
            if (string.IsNullOrEmpty(reviewTeam.text)) reviewTeam.text = "Equipe indisponível.";

            var pricing = MenuPricingHelper.FromJson(session.menuPricingJson);
            reviewMenu.text = restaurant?.products == null ? "Cardápio indisponível."
                : string.Join("\n", restaurant.products.Where(p => p != null).Select(p =>
                    $"{p.displayName}  ·  {(MenuPricingHelper.TryGetPrice(pricing, p.id, out float price) ? price.ToString("C", Currency) : "—")}"));
            int equipment = EquipmentSelectionHelper.FromJson(session.equipmentJson).equipmentIds.Count;
            reviewResources.text = $"Equipamentos adquiridos  ·  {equipment}\nCaixa  ·  {session.currentCash.ToString("C", Currency)}\n"
                + $"Saldo devedor  ·  {session.loanBalance.ToString("C", Currency)}";
            reviewPeriod.text = $"MÊS {session.currentRound:D2} DE 3";
            reviewStatus.text = valid ? "Ao confirmar, as escolhas ficam fixas até o fechamento deste mês."
                : "Complete os mínimos da equipe no hub para abrir o mês.";
            reviewStatus.color = valid ? GamePalette.Ink : GamePalette.ErrorText;
            reviewConfirm.interactable = valid;
            reviewConfirm.onClick.RemoveAllListeners();
            reviewConfirm.onClick.AddListener(() => confirm());
            foreach (var button in new[] { reviewBack, reviewClose })
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => cancel());
            }
            modalSurface.sizeDelta = review.sizeDelta;
            eventDay.gameObject.SetActive(false);
            review.gameObject.SetActive(true);
            modal.Configure(valid ? reviewConfirm : reviewBack, reviewBack);
            modal.gameObject.SetActive(true);
        }

        public void ReviewError(string message)
        {
            reviewStatus.text = message;
            reviewStatus.color = GamePalette.ErrorText;
        }

        public SharedModalFocus ShowEventModal(int month, int day, out RectTransform surface)
        {
            review.gameObject.SetActive(false);
            modalSurface.sizeDelta = new Vector2(1624, 930);
            eventDay.text = $"MÊS {month:D2}  ·  DIA {day:D2}";
            eventDay.gameObject.SetActive(true);
            modal.Configure(null, null, false);
            modal.gameObject.SetActive(true);
            surface = modalSurface;
            return modal;
        }

        public bool Reviewing => modal.isActiveAndEnabled && review.gameObject.activeSelf;
        public void CloseModal() => modal.gameObject.SetActive(false);

        public void ConfigurePolicies(Game.Application.UseCases.Simulation.PlanningData data)
        {
            if (data == null) return;
            ChosenPolicies = data.Current.Clone();
            if (policiesButton == null)
                policiesButton = ActionButton(review, "Policies", "Políticas do mês", 48, 480, 535,
                    SharedControlKind.Secondary, null, out _);
            policiesButton.gameObject.SetActive(true);
            policiesButton.onClick.RemoveAllListeners();
            policiesButton.onClick.AddListener(() => ShowPolicies(data));
            if (!data.CanStart)
            {
                reviewConfirm.interactable = false;
                ReviewError(string.Join(" ", data.Errors));
            }
        }

        private void ShowPolicies(Game.Application.UseCases.Simulation.PlanningData data)
        {
            if (policiesPanel != null) Destroy(policiesPanel.gameObject);
            policiesPanel = Node(modalSurface, "MonthlyPolicies", 0, 0, 1680, 950);
            modalSurface.sizeDelta = policiesPanel.sizeDelta;
            review.gameObject.SetActive(false);
            Label(policiesPanel, "Title", "Políticas do mês", 48, 32, 1500, 64, TypographyRole.ScreenTitle, 44, GamePalette.Plum);
            Label(policiesPanel, "Hint", "Escolha as opções antes de abrir o mês. Os custos entram nos cálculos do restaurante.",
                48, 100, 1584, 40, TypographyRole.Body, 25, GamePalette.Ink);
            string[] names = { "Marketing", "Fidelização", "Fornecedor", "Pagamento", "Manutenção", "Estoque", "Salários", "Treinamento" };
            var ids = Game.Domain.Simulation.MonthPolicies.PolicyIds;
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                if (!data.Options.TryGetValue(id, out var options) || options.Count == 0) continue;
                float y = 160 + i * 80;
                Label(policiesPanel, "Caption_" + id, names[i], 48, y, 288, 72, TypographyRole.Label, 27, GamePalette.Ink);
                float width = (1260 - (options.Count - 1) * 12) / options.Count;
                var buttons = new List<Button>();
                foreach (var option in options)
                {
                    var choice = option;
                    string cost = option.FixedMonthlyCost > 0
                        ? option.FixedMonthlyCost.ToString("C", Currency) + "/mês"
                        : option.AssetValueRate > 0 ? option.AssetValueRate.ToString("P0", Currency) + " dos equipamentos"
                        : "Sem custo fixo";
                    var button = ActionButton(policiesPanel, id + "_" + option.OptionId,
                        option.Label + "\n<size=22>" + cost + "</size>", 350 + buttons.Count * (width + 12), y, width,
                        SharedControlKind.Secondary, null, out var optionLabel);
                    optionLabel.textWrappingMode = TextWrappingModes.Normal;
                    buttons.Add(button);
                    button.onClick.AddListener(() =>
                    {
                        ChosenPolicies.Set(id, choice.OptionId);
                        for (int j = 0; j < buttons.Count; j++)
                            buttons[j].GetComponent<SharedControlAppearance>().SetSelected(options[j].OptionId == choice.OptionId);
                    });
                    button.GetComponent<SharedControlAppearance>().SetSelected(option.OptionId == ChosenPolicies.Get(id));
                }
            }
            var done = ActionButton(policiesPanel, "Done", "Voltar à revisão", 1220, 842, 400,
                SharedControlKind.Primary, () =>
                {
                    policiesPanel.gameObject.SetActive(false); review.gameObject.SetActive(true);
                    modalSurface.sizeDelta = review.sizeDelta; modal.Configure(reviewConfirm, reviewBack);
                }, out _);
            modal.Configure(done, done);
        }

        private static void RenderTile(Tile tile, IReadOnlyList<Commitment> commitments)
        {
            for (int i = 0; i < tile.Icons.Length; i++)
            {
                bool visible = i < commitments.Count;
                tile.Icons[i].enabled = visible;
                tile.Captions[i].text = "";
                if (!visible) continue;
                var entry = commitments[i];
                tile.Icons[i].sprite = SharedUiSkin.Current.Get(entry.Icon);
                tile.Icons[i].color = tile.Captions[i].color = entry.Color;
                tile.Captions[i].text = entry.Name;
            }
        }

        public void ShowPosition(int day)
        {
            if (shownDay > 0) tiles[shownDay - 1].Pin.enabled = false;
            shownDay = day;
            if (day > 0)
            {
                tiles[day - 1].Pin.rectTransform.localScale = Vector3.one;
                tiles[day - 1].Pin.enabled = true;
                pinShownAt = Time.unscaledTime;
            }
            position.text = day == 0 ? "Antes do dia 1" : $"Dia {day:D2} de {MonthlyCalendarService.Days}";
        }

        private void Update()
        {
            if (shownDay == 0) return;
            float elapsed = Mathf.Clamp01((Time.unscaledTime - pinShownAt) / .13f);
            tiles[shownDay - 1].Pin.rectTransform.localScale = Vector3.one * (1 + .25f * Mathf.Sin(elapsed * Mathf.PI));
        }

        private void SelectDay(int day)
        {
            if (selectedDay > 0) tiles[selectedDay - 1].Appearance.SetSelected(false);
            selectedDay = day;
            if (day > 0) tiles[day - 1].Appearance.SetSelected(true);
            detailsHeading.text = day == 0 ? "Agenda do mês" : $"Dia {day:D2}";
            var items = day == 0
                ? agenda.SelectMany((entries, index) => entries.Select(entry => (entry, day: index + 1))).ToArray()
                : agenda[day - 1].Select(entry => (entry, day)).ToArray();
            empty.text = items.Length == 0 ? "Nenhum compromisso registrado neste dia." : "";
            for (int i = 0; i < DetailRows; i++)
            {
                bool active = i < items.Length;
                detailIcons[i].enabled = active;
                detailNames[i].text = active ? items[i].entry.Name : "";
                detailStates[i].text = "";
                if (!active) continue;
                var (entry, itemDay) = items[i];
                detailStates[i].text = day == 0 ? $"Dia {itemDay:D2}  ·  {entry.State}" : entry.State;
                detailIcons[i].sprite = SharedUiSkin.Current.Get(entry.Icon);
                detailIcons[i].color = detailStates[i].color = entry.Color;
            }
        }

        public void SetControls(bool canAdvance, bool canReturn, string label)
        {
            advance.interactable = canAdvance;
            advanceLabel.text = label;
            back.interactable = canReturn;
            foreach (var tile in tiles) tile.Button.interactable = canReturn;
        }

        public void Status(string message) => status.text = message;
        public void FocusAdvance() { if (advance.IsInteractable()) advance.Select(); }

        private void Build()
        {
            var frame = (RectTransform)transform;
            var backdrop = gameObject.AddComponent<Image>();
            backdrop.color = GamePalette.SurfaceAlt;
            var top = Surface(frame, "Header", "UI_TOPBAR_VIOLET", 0, 0, 1920, 88);
            Icon(top, "CalendarIcon", "ICON_CALENDAR", 40, 22, 44, GamePalette.OnDark);
            Label(top, "Title", "Calendário mensal", 102, 0, 600, 88, TypographyRole.ScreenTitle, 44, GamePalette.OnDark);
            period = Label(top, "Period", "", 1340, 0, 340, 88, TypographyRole.Label, 26, GamePalette.OnDark);
            period.alignment = TextAlignmentOptions.MidlineRight;

            var board = Surface(frame, "Board", "UI_PANEL_IVORY", 36, 116, 1304, 838);
            Label(board, "Heading", "Dias do mês", 24, 14, 420, 60, TypographyRole.SectionTitle, 34, GamePalette.Ink);
            position = Label(board, "Position", "Antes do dia 1", 900, 14, 380, 60, TypographyRole.SectionTitle, 28, GamePalette.Primary);
            position.alignment = TextAlignmentOptions.MidlineRight;

            for (int i = 0; i < tiles.Length; i++)
            {
                int day = i + 1;
                agenda[i] = new List<Commitment>();
                var root = Node(board, $"Day{day:D2}", 24 + i % Columns * 211, 84 + i / Columns * 134, 201, 122);
                var button = root.gameObject.AddComponent<Button>();
                var appearance = SharedUiStyles.Bind(button, SharedControlKind.Card);
                Label(root, "DayNumber", day.ToString("D2"), 14, 4, 104, 54, TypographyRole.ScreenTitle, 38, GamePalette.Ink);
                var pin = Icon(root, "CurrentDay", "ICON_PIN", 145, 10, 40, GamePalette.Primary);
                pin.enabled = false;
                var tile = new Tile { Button = button, Appearance = appearance, Pin = pin };
                for (int line = 0; line < tile.Icons.Length; line++)
                {
                    int y = 60 + line * 27;
                    tile.Icons[line] = Icon(root, "CommitmentIcon" + line, null, 14, y, 23, GamePalette.Ink);
                    tile.Icons[line].enabled = false;
                    tile.Captions[line] = Label(root, "Commitment" + line, "", 42, y - 3, 152, 28, TypographyRole.Label, 20, GamePalette.Ink);
                }
                tiles[i] = tile;
                button.onClick.AddListener(() => { if (button.isActiveAndEnabled && button.IsInteractable() && SharedModalFocus.Active == null) SelectDay(day); });
            }
            Icon(board, "PinKey", "ICON_PIN", 28, 770, 28, GamePalette.Primary);
            Label(board, "PinLegend", "Posição atual", 63, 759, 207, 53, TypographyRole.Label, 22, GamePalette.Ink);
            Icon(board, "PaidKey", "ICON_CHECK", 264, 770, 28, GamePalette.PositiveReportText);
            Label(board, "PaidLegend", "Pago ou decidido", 300, 759, 350, 53, TypographyRole.Label, 22, GamePalette.Ink);
            Icon(board, "AnnouncedKey", "ICON_STAR", 557, 770, 28, GamePalette.Primary);
            Label(board, "AnnouncedLegend", "Evento anunciado", 593, 759, 350, 53, TypographyRole.Label, 22, GamePalette.Ink);

            var details = Surface(frame, "Details", "UI_PANEL_IVORY", 1360, 116, 524, 838);
            var wallet = Surface(details, "Wallet", "UI_PANEL_PLUM", 20, 20, 484, 112);
            Icon(wallet, "CashIcon", "ICON_CASH", 18, 33, 42, GamePalette.OnDark);
            Label(wallet, "CashLabel", "CAIXA DISPONÍVEL", 72, 12, 390, 34, TypographyRole.Label, 24, GamePalette.OnDark);
            cash = Label(wallet, "Cash", "—", 72, 46, 390, 57, TypographyRole.SectionTitle, 40, GamePalette.OnDark);
            detailsHeading = Label(details, "Heading", "Compromissos do mês", 28, 153, 465, 59, TypographyRole.SectionTitle, 32, GamePalette.Ink);
            empty = Label(details, "Empty", "", 28, 220, 463, 106, TypographyRole.Body, 27, GamePalette.Muted);
            empty.textWrappingMode = TextWrappingModes.Normal;
            empty.alignment = TextAlignmentOptions.TopLeft;
            for (int i = 0; i < DetailRows; i++)
            {
                float y = 220 + 59 * i;
                detailIcons[i] = Icon(details, "Icon" + i, null, 28, y + 12, 32, GamePalette.Ink);
                detailIcons[i].enabled = false;
                detailNames[i] = Label(details, "Name" + i, "", 75, y, 419, 32, TypographyRole.SectionTitle, 24, GamePalette.Ink);
                detailStates[i] = Label(details, "State" + i, "", 75, y + 30, 419, 28, TypographyRole.Body, 22, GamePalette.Muted);
            }

            var footer = Surface(frame, "Footer", "UI_NAVBAR_PLUM", 0, 978, 1920, 102);
            back = ActionButton(footer, "ReturnToHub", "Voltar ao hub", 36, 14, 250, SharedControlKind.Secondary, onBack, out _);
            status = Label(footer, "Status", "", 310, 10, 1190, 82, TypographyRole.Body, 28, GamePalette.OnDark);
            advance = ActionButton(footer, "Advance", "Avançar", 1528, 14, 356, SharedControlKind.Primary, onAdvance, out advanceLabel);
            SetControls(false, false, "Avançar");
            BuildModal(frame);
        }

        private void BuildModal(RectTransform frame)
        {
            var layer = Node(frame, "CalendarDecisionModal", 0, 0, 0, 0);
            layer.gameObject.SetActive(false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;
            var scrim = SharedUiStyles.Surface(layer.gameObject, "UI_SCRIM", true);
            scrim.preserveAspect = false;
            layer.gameObject.AddComponent<Canvas>();
            layer.gameObject.AddComponent<GraphicRaycaster>();
            modal = layer.gameObject.AddComponent<SharedModalFocus>();

            modalSurface = Surface(layer, "Surface", "UI_MODAL_SURFACE", 0, 0, 0, 0);
            modalSurface.anchorMin = modalSurface.anchorMax = modalSurface.pivot = new Vector2(.5f, .5f);
            modalSurface.anchoredPosition = Vector2.zero;
            eventDay = Label(modalSurface, "EventDay", "", 74, 66, 500, 40, TypographyRole.Label, 28, GamePalette.Primary);
            review = Node(modalSurface, "MonthlyReview", 0, 0, 1256, 744);
            reviewPeriod = Label(review, "Period", "", 48, 32, 500, 36, TypographyRole.Label, 24, GamePalette.Primary);
            Label(review, "Title", "Decisões do mês", 48, 72, 1030, 64, TypographyRole.ScreenTitle, 48, GamePalette.Plum);
            Label(review, "TeamHeading", "Equipe", 48, 169, 535, 52, TypographyRole.SectionTitle, 34, GamePalette.Plum);
            reviewTeam = ReviewText("Team", 48, 225, 535, 215);
            Label(review, "MenuHeading", "Cardápio", 630, 169, 578, 52, TypographyRole.SectionTitle, 34, GamePalette.Plum);
            reviewMenu = ReviewText("Menu", 630, 225, 578, 145);
            Label(review, "ResourcesHeading", "Equipamentos e crédito", 630, 385, 578, 52, TypographyRole.SectionTitle, 34, GamePalette.Plum);
            reviewResources = ReviewText("Resources", 630, 441, 578, 110);
            reviewStatus = ReviewText("Status", 48, 583, 1160, 49);
            reviewBack = ActionButton(review, "Back", "Voltar ao hub", 48, 634, 310, SharedControlKind.Secondary, null, out _);
            reviewConfirm = ActionButton(review, "Confirm", "Confirmar e abrir mês", 828, 634, 380, SharedControlKind.Primary, null, out _);
            var close = Node(review, "Close", 1140, 32, 68, 68);
            reviewClose = close.gameObject.AddComponent<Button>();
            Icon(close, "Icon", "ICON_CLOSE", 18, 18, 32, GamePalette.Ink);
            SharedUiStyles.Bind(reviewClose, SharedControlKind.Icon);
        }

        private TMP_Text ReviewText(string name, float x, float y, float width, float height)
        {
            var text = Label(review, name, "", x, y, width, height, TypographyRole.Body, 28, GamePalette.Ink);
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }

        private static Button ActionButton(RectTransform parent, string name, string value, float x, float y, float width,
            SharedControlKind kind, Action action, out TMP_Text label)
        {
            var root = Node(parent, name, x, y, width, 74);
            var button = root.gameObject.AddComponent<Button>();
            label = Label(root, "Label", value, 14, 0, width - 28, 74, TypographyRole.Button, 32, GamePalette.Ink);
            label.alignment = TextAlignmentOptions.Center;
            SharedUiStyles.Bind(button, kind);
            if (action != null) button.onClick.AddListener(() => action());
            return button;
        }

        private static RectTransform Node(RectTransform parent, string name, float x, float y, float width, float height)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static RectTransform Surface(RectTransform parent, string name, string id, float x, float y, float width, float height)
        {
            var rect = Node(parent, name, x, y, width, height);
            SharedUiStyles.Surface(rect.gameObject, id);
            return rect;
        }

        private static Image Icon(RectTransform parent, string name, string id, float x, float y, float size, Color color)
        {
            var icon = Node(parent, name, x, y, size, size).gameObject.AddComponent<Image>();
            icon.sprite = id != null ? SharedUiSkin.Current.Get(id) : null;
            icon.color = color;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return icon;
        }

        private static TMP_Text Label(RectTransform parent, string name, string value, float x, float y, float width, float height,
            TypographyRole role, float size, Color color)
        {
            var label = Node(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            GameTypography.Apply(label, new TypographyToken(role, size));
            label.text = value;
            label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.richText = false;
            return label;
        }

        private static string Money(long cents) => (cents / 100m).ToString("C", Currency);
        private static (string name, string icon) BillPresentation(string type) => type switch
        {
            "rent" => ("Aluguel", "ICON_RENT"),
            "utilities" => ("Custos fixos", "ICON_UTILITIES"),
            "salaries" => ("Folha", "ICON_PAYROLL"),
            "loanPayment" => ("Empréstimo", "ICON_LOAN_PRINCIPAL"),
            "marketing" => ("Políticas do mês", "ICON_DOCUMENT"),
            "maintenance" => ("Manutenção", "ICON_EQUIPMENT"),
            "water" => ("Água", "ICON_UTILITIES"),
            "taxes" => ("Impostos", "ICON_DOCUMENT"),
            _ => ("Conta", "ICON_DOCUMENT")
        };
    }
}
