using Game.Adapter.In.UI;
using UnityEngine;

namespace Game.Adapter.In.Controllers
{
    public sealed class RestaurantScreenController : MonoBehaviour
    {
        [SerializeField] private RestaurantScreenView view;

        private RestaurantType? _selectedRestaurant;
        private Segment?       _selectedSegment;

        // ── Ciclo de vida ─────────────────────────────────────────────────
        private void OnEnable()
        {
            // OnEnable é chamado quando o Panel_Restaurant é ativado pelo UIStateListener
            BindActions();
            ResetFromSession();
            UpdateCoherencePanel();
        }

        private void OnDisable()
        {
            // Remove os listeners quando o painel é desativado (evita duplicação)
            view.BindCardPodrao(null);
            view.BindCardJapones(null);
            view.BindCardFrances(null);
        }

        private void BindActions()
        {
            view.BindCardPodrao(() => OnRestaurantSelected(RestaurantType.PODRAO));
            view.BindCardJapones(() => OnRestaurantSelected(RestaurantType.JAPONES));
            view.BindCardFrances(() => OnRestaurantSelected(RestaurantType.FRANCES));
            view.BindBtnLow(() => OnSegmentSelected(Segment.LOW));
            view.BindBtnMedium(() => OnSegmentSelected(Segment.MEDIUM));
            view.BindBtnHigh(() => OnSegmentSelected(Segment.HIGH));
            view.BindConfirm(OnConfirm);
            view.BindBack(OnBack);
        }

        // ── Restaurar estado da sessão ao exibir o painel ─────────────────
        private void ResetFromSession()
        {
            // Se o jogador voltou para esta tela, restaura a seleção anterior
            if (GameSessionState.HasSession)
            {
                var session = GameSessionState.Current;
                // restaurantType é um enum com valor default 0 (PODRAO)
                // Precisamos verificar se foi explicitamente definido
                // Dica: adicione um bool "restaurantChosen" na entidade, OU
                // use um valor sentinela no enum (-1 ou "NONE")
            }

            view.SelectRestaurantCard(_selectedRestaurant);
            view.SelectSegmentButton(_selectedSegment);
            view.SetConfirmEnabled(_selectedRestaurant.HasValue && _selectedSegment.HasValue);

            UpdateHint();
        }

        // ── Seleção de restaurante ───────────────────────────────────────
        private void OnRestaurantSelected(RestaurantType type)
        {
            _selectedRestaurant = type;
            view.SelectRestaurantCard(type);
            view.SetConfirmEnabled(_selectedSegment.HasValue);
            UpdateHint();
            UpdateCoherencePanel();
        }

        // ── Seleção de segmento ──────────────────────────────────────────
        private void OnSegmentSelected(Segment seg)
        {
            _selectedSegment = seg;
            view.SelectSegmentButton(seg);
            view.SetConfirmEnabled(_selectedRestaurant.HasValue);
            UpdateHint();
            UpdateCoherencePanel();
        }

        // ── Atualiza hint do header ──────────────────────────────────────
        private void UpdateHint()
        {
            if (_selectedRestaurant == null && _selectedSegment == null)
            {
                view.SetHint("Selecione o tipo de restaurante e o público-alvo.");
                return;
            }

            string restName = _selectedRestaurant switch
            {
                RestaurantType.PODRAO  => "Podrão",
                RestaurantType.JAPONES => "Japonês",
                RestaurantType.FRANCES => "Francês",
                _                       => "—"
            };

            string segName = _selectedSegment switch
            {
                Segment.LOW    => "Público Baixo",
                Segment.MEDIUM => "Público Médio",
                Segment.HIGH   => "Público Alto",
                _               => "—"
            };

            string hint = _selectedRestaurant.HasValue && _selectedSegment.HasValue
                ? $"Você selecionou: {restName} · {segName}"
                : _selectedRestaurant.HasValue
                    ? $"Restaurante: {restName} · Selecione o segmento"
                    : $"Segmento: {segName} · Selecione o restaurante";

            view.SetHint(hint);
        }

        // ── Calcula e atualiza o painel de coerência ─────────────────────
        private void UpdateCoherencePanel()
        {
            if (_selectedRestaurant == null)
            {
                view.UpdateCoherence(0f, "—", 0f, "—");
                return;
            }

            LocationZone zone = GameSessionState.HasSession
                ? GameSessionState.Current.locationZone
                : LocationZone.Financas;

            float coh1 = CalcRestaurantLocationCoherence(_selectedRestaurant!.Value, zone);
            string tip1 = GetCoherenceTip1(_selectedRestaurant.Value, zone);

            float coh2 = _selectedSegment.HasValue
                ? CalcRestaurantSegmentCoherence(_selectedRestaurant.Value, _selectedSegment!.Value)
                : 0f;
            string tip2 = _selectedSegment.HasValue
                ? GetCoherenceTip2(_selectedRestaurant.Value, _selectedSegment.Value)
                : "Selecione um segmento.";

            view.UpdateCoherence(coh1, tip1, coh2, tip2);
        }

        // ── Lógica de coerência Restaurante + Localização ───────────────
        private static float CalcRestaurantLocationCoherence(RestaurantType r, LocationZone z) => (r, z) switch
        {
            (RestaurantType.PODRAO, LocationZone.Comercio)    => 0.9f,
            (RestaurantType.PODRAO, LocationZone.Educacao)   => 0.8f,
            (RestaurantType.PODRAO, LocationZone.Financas)   => 0.6f,
            (RestaurantType.PODRAO, LocationZone.Residencial) => 0.5f,
            (RestaurantType.PODRAO, LocationZone.Servicos)   => 0.7f,
            (RestaurantType.JAPONES, LocationZone.Financas)  => 0.85f,
            (RestaurantType.JAPONES, LocationZone.Educacao)  => 0.75f,
            (RestaurantType.JAPONES, LocationZone.Comercio)   => 0.7f,
            (RestaurantType.JAPONES, LocationZone.Residencial)=> 0.6f,
            (RestaurantType.JAPONES, LocationZone.Servicos)  => 0.65f,
            (RestaurantType.FRANCES, LocationZone.Financas)  => 0.95f,
            (RestaurantType.FRANCES, LocationZone.Residencial)=> 0.8f,
            (RestaurantType.FRANCES, LocationZone.Comercio)   => 0.65f,
            (RestaurantType.FRANCES, LocationZone.Educacao)  => 0.55f,
            (RestaurantType.FRANCES, LocationZone.Servicos)  => 0.7f,
            _ => 0.5f
        };

        private static float CalcRestaurantSegmentCoherence(RestaurantType r, Segment s) => (r, s) switch
        {
            (RestaurantType.PODRAO, Segment.LOW)    => 0.95f,
            (RestaurantType.PODRAO, Segment.MEDIUM) => 0.7f,
            (RestaurantType.PODRAO, Segment.HIGH)   => 0.3f,
            (RestaurantType.JAPONES, Segment.LOW)   => 0.4f,
            (RestaurantType.JAPONES, Segment.MEDIUM)=> 0.8f,
            (RestaurantType.JAPONES, Segment.HIGH)  => 0.75f,
            (RestaurantType.FRANCES, Segment.LOW)   => 0.1f,
            (RestaurantType.FRANCES, Segment.MEDIUM)=> 0.5f,
            (RestaurantType.FRANCES, Segment.HIGH)  => 0.95f,
            _ => 0.5f
        };

        private static string GetCoherenceTip1(RestaurantType r, LocationZone z) => (r, z) switch
        {
            (RestaurantType.FRANCES, LocationZone.Financas) => "Ótimo! Francês se destaca no centro financeiro.",
            (RestaurantType.PODRAO,  LocationZone.Comercio)  => "Ótimo! Podrão domina o comércio popular.",
            (RestaurantType.JAPONES, LocationZone.Financas) => "Bom! Executivos apreciam a culinária oriental.",
            _ => "Combinação viável. Avalie o segmento de público."
        };

        private static string GetCoherenceTip2(RestaurantType r, Segment s) => (r, s) switch
        {
            (RestaurantType.PODRAO,  Segment.HIGH)   => "Atenção: público alto raramente busca podrão.",
            (RestaurantType.FRANCES, Segment.LOW)    => "Atenção: público baixo não tem ticket para culinária francesa.",
            (RestaurantType.JAPONES, Segment.MEDIUM) => "Boa escolha. Japonês atrai bem o público médio.",
            _ => "Compatível."
        };

        // ── Confirmar ────────────────────────────────────────────────────
        private void OnConfirm()
        {
            if (!_selectedRestaurant.HasValue || !_selectedSegment.HasValue) return;

            // Salva em MEMÓRIA — NÃO no SQLite ainda
            GameSessionState.SetRestaurant(_selectedRestaurant.Value);
            GameSessionState.SetTargetSegment(_selectedSegment.Value);

            // Avança o estado — UIStateListener mostrará o próximo painel
            GameManager.Instance.StateMachine
                .TryChangeState(GameState.Config_TargetSegment);
        }

        // ── Voltar ────────────────────────────────────────────────────────
        private void OnBack()
        {
            // Limpa seleção local (não foi confirmada)
            _selectedRestaurant = null;
            _selectedSegment    = null;

            // Volta ao estado anterior
            GameManager.Instance.StateMachine
                .TryChangeState(GameState.Config_Location);
        }
    }
}