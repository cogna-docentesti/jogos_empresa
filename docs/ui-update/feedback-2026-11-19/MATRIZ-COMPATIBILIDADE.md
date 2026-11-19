# Compatibilidade de restaurante, público e localização

A tela de restaurante permite 25 das 45 combinações da abertura. Cada uma das cinco áreas aceita os três restaurantes. A classe de público determina se a escolha pode ser confirmada.

## Matriz conferida na GameScene

A coluna **Público** traz a contribuição da classe, de 0 a 20 pontos. Cada célula de área mostra **pontos da localização / subtotal de localização e público**. O subtotal pode chegar a 40. Nas linhas bloqueadas, os valores documentam o que o motor calcula se receber essa combinação diretamente. A tela mantém a seleção válida anterior quando o jogador tenta escolher uma classe bloqueada.

| Restaurante | Classe | Escolha | Público | Financeira | Educacional | Comercial | Residencial | Corporativa |
| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Lanches | C | Permitida | 20 | 10 / 30 | 18 / 38 | 20 / 40 | 8 / 28 | 14 / 34 |
| Lanches | B | Permitida | 10 | 10 / 20 | 18 / 28 | 20 / 30 | 8 / 18 | 14 / 24 |
| Lanches | A | Bloqueada | 0 | 10 / 10 | 18 / 18 | 20 / 20 | 8 / 8 | 14 / 14 |
| Japonês | C | Bloqueada | 0 | 20 / 20 | 15 / 15 | 14 / 14 | 10 / 10 | 12 / 12 |
| Japonês | B | Permitida | 20 | 20 / 40 | 15 / 35 | 14 / 34 | 10 / 30 | 12 / 32 |
| Japonês | A | Permitida | 15 | 20 / 35 | 15 / 30 | 14 / 29 | 10 / 25 | 12 / 27 |
| Francês | C | Bloqueada | 0 | 20 / 20 | 6 / 6 | 10 / 10 | 16 / 16 | 12 / 12 |
| Francês | B | Bloqueada | 5 | 20 / 25 | 6 / 11 | 10 / 15 | 16 / 21 | 12 / 17 |
| Francês | A | Permitida | 20 | 20 / 40 | 6 / 26 | 10 / 30 | 16 / 36 | 12 / 32 |

Lanches com a classe B na Área Residencial pode ser confirmado com 8 pontos de localização e 10 de público. O Francês com a classe B recebe 5 pontos de público no motor, mas o catálogo só permite selecionar a classe A. A exibição usa os pontos de cada critério separadamente. Preço e equipe completam o alinhamento mensal de até 80 pontos.

## Fontes e divergências

- **Elegibilidade.** Os assets `REST_Lanches`, `REST_Japones` e `REST_Frances` declaram classes permitidas e bloqueadas. Nos três, as listas são complementares. O método `AllowsSegment` consulta apenas `allowedSegments`. A D2 usa esse método ao habilitar o botão, aceitar a seleção e gravar o rascunho. A pontuação da área não participa dessas verificações. [Catálogos](../../../Assets/Resources/Restaurants) · [RestaurantData](../../../Assets/Scripts/Data/ScriptableObjects/Restaurants/RestaurantData.cs) · [Seleção e confirmação](../../../Assets/Scripts/Game/Adapter/In/Controllers/RestaurantScreenController.cs)
- **Pontuação.** A D2 lê os mesmos dois critérios que `AlignmentEngine.Calculate` soma no fechamento. Antes, a tela estimava a compatibilidade da área pela distância entre duas classes e tinha uma segunda tabela de percentuais para o público. Lanches com classe C no Comércio exibia 60% e 95%, respectivamente. O motor atribui 20 de 20 aos dois critérios. [AlignmentEngine](../../../Assets/Scripts/Game/Domain/Aligment/AligmentEngine.cs) · [Simulação mensal](../../../Assets/Scripts/Game/Domain/Service/MonthlySimulationEngine.cs) · [Descrição do cálculo de demanda](../../monthly-simulation.md)
- **Nomes.** A interface lê os nomes de restaurante e área nos catálogos. O ID `marketing` corresponde ao enum `Servicos` e ao nome “Área Corporativa” no mapa. A antiga explicação de compatibilidade dizia “Área de serviços”. [Catálogo de locais](../../../Assets/Resources/Locations) · [Mapa de IDs](../../../Assets/Scripts/Game/Domain/Mappers/LocationZoneMap.cs)
- **Homologação econômica pendente.** O projeto descreve os parâmetros iniciais e menciona uma recalibração da matriz na tarefa T-06. A busca no repositório não localizou uma planilha ou especificação separada que homologue os pesos ou altere os públicos permitidos. A FB-04 preserva os valores em vigor. Uma decisão de liberar a classe B para o Francês ou mudar os pesos depende de validação de balanceamento. [Configuração de balanceamento](../../monthly-simulation.md#catalog-and-balance-configuration) · [Referência à T-06](../../../Assets/Scripts/Game/Domain/Service/GamesSessionService.cs)

O registro de QA descreve a reprodução e a conferência das 45 tentativas. As evidências contêm a classe tentada, a seleção efetiva após cada tentativa, o estado do botão e os valores lidos na GameScene. [QA da FB-04](qa/FB-04.json) · [Capturas e medições](../../../output/ui-update/feedback-2026-11-19/FB-04)
