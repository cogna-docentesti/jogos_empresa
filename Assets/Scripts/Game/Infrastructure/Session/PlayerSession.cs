using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Infrastructure.Session
{
    /// <summary>
    /// Rascunho do jogador, so em memoria.
    ///
    /// Guarda duas coisas:
    ///  1. A identificacao (nome, RA, nome do restaurante) da cena 0_Identification.
    ///     Essa parte o Renan ja salvava em PlayerPrefs, e continua igual.
    ///  2. As tres decisoes obrigatorias: D1 Localizacao, D2 Restaurante e D3 Cardapio.
    ///
    /// Nada das decisoes toca o SQLite enquanto o jogador navega entre D1, D2 e D3.
    /// A gravacao acontece uma unica vez, no confirm da D3, em
    /// GameSessionService.CommitInitialDecisions(), que le daqui e copia para a
    /// GameSessionEntity.
    ///
    /// Por ser uma classe static, este rascunho sobrevive a troca de painel e
    /// tambem a troca de cena (por exemplo, voltar da D1 para a identificacao).
    /// E isso que permite "reidratar" as telas: quando um painel reaparece, ele
    /// le daqui o que o jogador tinha escolhido.
    /// </summary>
    public static class PlayerSession
    {
        // =============================================================
        //  IDENTIFICACAO (cena 0_Identification, do Renan)
        // =============================================================

        private const string IdentificationKey = "PlayerSession.Identification";

        [Serializable]
        private sealed class IdentificationData
        {
            public string studentName;
            public string studentRA;
            public string restaurantName;
        }

        public static string StudentName { get; private set; }
        public static string StudentRA { get; private set; }
        public static string RestaurantName { get; private set; }

        public static bool HasIdentification =>
            !string.IsNullOrWhiteSpace(StudentName)
            && !string.IsNullOrWhiteSpace(StudentRA)
            && !string.IsNullOrWhiteSpace(RestaurantName);

        /// <summary>
        /// Ligado quando o jogador aperta Voltar na D1. A tela de identificacao
        /// normalmente pula sozinha para o jogo se ja houver cadastro salvo; com
        /// este pedido ela fica aberta para o jogador poder corrigir os dados.
        /// </summary>
        public static bool IdentificationEditRequested { get; private set; }

        public static void RequestIdentificationEdit()
        {
            IdentificationEditRequested = true;
        }

        /// <summary>Le e apaga o pedido de edicao (vale para uma abertura so).</summary>
        public static bool ConsumeIdentificationEditRequest()
        {
            bool requested = IdentificationEditRequested;
            IdentificationEditRequested = false;
            return requested;
        }

        /// <summary>
        /// Apaga o cadastro salvo (PlayerPrefs) e o que estiver em memoria.
        /// Usado pelo botao Resetar: depois dele a tela de identificacao abre
        /// vazia, como num jogo novo.
        /// </summary>
        public static void ClearSavedIdentification()
        {
            PlayerPrefs.DeleteKey(IdentificationKey);
            PlayerPrefs.Save();

            StudentName = null;
            StudentRA = null;
            RestaurantName = null;
            IdentificationEditRequested = false;
        }

        public static void SaveIdentification(string studentName, string studentRA, string restaurantName)
        {
            var data = new IdentificationData
            {
                studentName = studentName?.Trim(),
                studentRA = studentRA?.Trim(),
                restaurantName = restaurantName?.Trim()
            };

            if (!IsIdentificationComplete(data))
                throw new ArgumentException("All identification fields must be filled in.");

            PlayerPrefs.SetString(IdentificationKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            ApplyIdentification(data);
        }

        public static bool TryLoadIdentification()
        {
            if (!PlayerPrefs.HasKey(IdentificationKey))
                return false;

            try
            {
                var data = JsonUtility.FromJson<IdentificationData>(PlayerPrefs.GetString(IdentificationKey));
                if (!IsIdentificationComplete(data))
                {
                    Debug.LogWarning("[PlayerSession] Saved identification is incomplete. Registration is required.");
                    return false;
                }

                ApplyIdentification(data);
                return true;
            }
            catch (ArgumentException exception)
            {
                Debug.LogWarning("[PlayerSession] Failed to read saved identification: " + exception.Message);
                return false;
            }
        }

        private static bool IsIdentificationComplete(IdentificationData data)
        {
            return data != null
                && !string.IsNullOrWhiteSpace(data.studentName)
                && !string.IsNullOrWhiteSpace(data.studentRA)
                && !string.IsNullOrWhiteSpace(data.restaurantName);
        }

        private static void ApplyIdentification(IdentificationData data)
        {
            StudentName = data.studentName.Trim();
            StudentRA = data.studentRA.Trim();
            RestaurantName = data.restaurantName.Trim();
        }

        // =============================================================
        //  D1 LOCALIZACAO
        // =============================================================

        /// <summary>Id da area no mapa: "bank", "university", "store", "condominium", "marketing".</summary>
        public static string SelectedEstablishmentId { get; private set; }

        public static string SelectedEstablishmentName { get; private set; }

        /// <summary>
        /// Zona escolhida. O "?" torna o enum anulavel: sem ele, "nao escolheu"
        /// e "escolheu Financas" teriam o mesmo valor (0) e a tela nao saberia
        /// se deve reidratar ou mostrar o estado vazio.
        /// </summary>
        public static LocationZone? SelectedZone { get; private set; }

        public static bool HasLocation =>
            SelectedZone.HasValue && !string.IsNullOrWhiteSpace(SelectedEstablishmentId);

        public static void SaveLocation(string establishmentId, string establishmentName, LocationZone zone)
        {
            SelectedEstablishmentId = establishmentId;
            SelectedEstablishmentName = establishmentName;
            SelectedZone = zone;
        }

        // =============================================================
        //  D2 RESTAURANTE (tipo + classe social atendida)
        // =============================================================

        public static RestaurantType? SelectedRestaurantType { get; private set; }

        public static Segment? SelectedTargetSegment { get; private set; }

        public static bool HasRestaurant =>
            SelectedRestaurantType.HasValue && SelectedTargetSegment.HasValue;

        public static void SaveRestaurant(RestaurantType restaurantType, Segment targetSegment)
        {
            // Cada restaurante tem pratos diferentes. Se o jogador trocou de
            // restaurante, o cardapio montado para o anterior deixa de valer.
            if (MenuRestaurantType.HasValue && MenuRestaurantType.Value != restaurantType)
                ClearMenu();

            SelectedRestaurantType = restaurantType;
            SelectedTargetSegment = targetSegment;
        }

        // =============================================================
        //  D3 CARDAPIO (pratos e preco escolhido de cada um)
        // =============================================================

        // Reaproveita MenuPricingData/MenuPricingItem, que ja existem e ja sao o
        // formato da coluna menuPricingJson. Nao ha um segundo modelo de cardapio.
        private static readonly List<MenuPricingItem> _menuItems = new List<MenuPricingItem>();

        /// <summary>Restaurante para o qual o cardapio do rascunho foi montado.</summary>
        public static RestaurantType? MenuRestaurantType { get; private set; }

        public static bool HasMenu => MenuRestaurantType.HasValue && _menuItems.Count > 0;

        /// <summary>Copia defensiva: quem le nao consegue alterar o rascunho por fora.</summary>
        public static MenuPricingData GetMenu()
        {
            var copy = new MenuPricingData();

            foreach (var item in _menuItems)
                copy.items.Add(new MenuPricingItem { productId = item.productId, selectedPrice = item.selectedPrice });

            return copy;
        }

        public static void SetMenu(RestaurantType restaurantType, MenuPricingData menu)
        {
            _menuItems.Clear();
            MenuRestaurantType = restaurantType;

            if (menu?.items == null)
                return;

            foreach (var item in menu.items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.productId))
                    continue;

                _menuItems.Add(new MenuPricingItem { productId = item.productId, selectedPrice = item.selectedPrice });
            }
        }

        private static void ClearMenu()
        {
            _menuItems.Clear();
            MenuRestaurantType = null;
        }

        // =============================================================
        //  ESTADO GERAL
        // =============================================================

        /// <summary>As tres decisoes obrigatorias estao preenchidas e o cardapio e do restaurante escolhido.</summary>
        public static bool IsComplete =>
            HasLocation
            && HasRestaurant
            && HasMenu
            && MenuRestaurantType == SelectedRestaurantType;

        /// <summary>
        /// Preenche o rascunho a partir de uma sessao que veio do banco.
        /// Usado ao abrir o jogo com partida salva: assim as telas que leem do
        /// rascunho (por exemplo o Cardapio aberto pelo hub) mostram o que esta gravado.
        /// </summary>
        public static void LoadDecisionsFrom(GameSessionEntity session)
        {
            ClearDecisions();

            if (session == null)
                return;

            string establishmentId = LocationZoneMap.ToId(session.locationZone);
            SaveLocation(establishmentId, FindLocationName(establishmentId), session.locationZone);
            SaveRestaurant(session.restaurantType, session.targetSegment);

            var menu = MenuPricingHelper.FromJson(session.menuPricingJson);
            if (menu.items.Count > 0)
                SetMenu(session.restaurantType, menu);
        }

        private static string FindLocationName(string establishmentId)
        {
            if (string.IsNullOrWhiteSpace(establishmentId))
                return null;

            foreach (var location in Resources.LoadAll<LocationData>("Locations"))
            {
                if (location != null && location.id == establishmentId)
                    return location.displayName;
            }

            return establishmentId;
        }

        /// <summary>Apaga so as decisoes D1, D2 e D3. A identificacao fica.</summary>
        public static void ClearDecisions()
        {
            SelectedEstablishmentId = null;
            SelectedEstablishmentName = null;
            SelectedZone = null;

            SelectedRestaurantType = null;
            SelectedTargetSegment = null;

            ClearMenu();
        }

        // Clears runtime state only (the PlayerPrefs registration stays).
        // To also erase the saved registration, use ClearSavedIdentification().
        public static void Clear()
        {
            StudentName = null;
            StudentRA = null;
            RestaurantName = null;
            IdentificationEditRequested = false;

            ClearDecisions();
        }
    }
}
