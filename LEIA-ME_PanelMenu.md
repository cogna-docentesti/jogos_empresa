# Panel Menu — o que foi feito e como rodar

## 0-D. O bug dos nodes que não clicam

**Causa:** ao reestilizar os nodes na mão, o `Image` do `Card` foi apagado. O `Card` ficou com `Button` + `MapMenuNode` + `PanelNavButton` e **nenhum `Graphic` com `raycastTarget`** — nem nele, nem em nenhum filho (todos ficaram em `raycast=0`).

Um `Button` do uGUI só recebe clique se existir algum `Graphic` com raycast ligado nele ou em algum descendente; o evento sobe na hierarquia a partir do gráfico atingido. Sem nenhum, o botão fica mudo — e **não gera erro nenhum no Console**.

A única exceção era o `MeTitle` do `Node_Establishment`, que ficou com `raycast=1`. Por isso só aquele node respondia, e só quando o clique acertava o texto.

**Correção:** cada `Card` agora ganha um filho `HitArea` — um `Image` transparente (`alpha 0`), esticado no card inteiro, com `raycastTarget` ligado. O clique deixa de depender de decisão visual: você pode trocar, apagar ou repintar o fundo do card à vontade que o botão continua funcionando.

Além disso, no fim de cada build o `UiFactory.WarnAboutDeadButtons` percorre a árvore e **avisa em amarelo no Console** qualquer `Button` sem alvo de raycast, com o caminho completo do objeto. Esse tipo de falha não se denuncia sozinha.

Rode **Tools > Jogo > 2 - Corrigir areas de clique** para aplicar só isso, sem tocar em mais nada.

---

## 0-E. O builder não sobrescreve mais seus ajustes

Você reestilizou os nodes na mão. Rodar o builder de novo apagaria tudo. Agora ele tem dois modos:

| Menu | O que faz |
|---|---|
| **1 - Construir Tudo (preserva ajustes manuais)** | Cria o que falta, religa referências e garante áreas de clique. **Não escreve cor, fonte nem posição em nada que já existia.** Seguro rodar sempre. |
| **2 - Corrigir areas de clique** | Só o `HitArea` + auditoria de botões mortos. |
| **3 - Reaplicar estilo: Meu Estabelecimento** | Sobrescreve só esse painel, com a escala tipográfica nova. |
| **4 - Reaplicar estilo: TUDO** | Sobrescreve tudo, inclusive os nodes. Pede confirmação antes. |

Por dentro: o `UiFactory` marca quais objetos ele acabou de criar. Em modo preservação, `Panel`, `Picture`, `Solid`, `Text`, `MakeButton` e todos os helpers de âncora saem cedo quando o objeto já existia. O que **sempre** é escrito nos dois modos: hierarquia, referências (`SetPrivate`) e áreas de clique — isso é correção, não estilo.

---

## 0-F. Padrão visual: agora vem da cena, não do meu chute

Levantei os valores reais das suas telas prontas (`Panel_Financial`, `Panel_MenuPricing`) e do `Panel_Menu` depois do seu ajuste. Eu estava usando uma escala **muito menor** que a sua — era essa a raiz da ilegibilidade.

`Assets/Scripts/Game/Adapter/In/UI/Theme/GameTypography.cs`:

| Papel | Tamanho | Origem |
|---|---|---|
| Título de tela | **70** | `Panel_Financial > TitleText` |
| Hint do cabeçalho | **32** | `Panel_Financial > HintText` |
| Título de seção / card | **42** | `CapitalLabel` |
| Valor de destaque | **50** | `CapitalValue` |
| Corpo e texto de botão | **30** | `ConfirmButton > Text` |
| Texto secundário / Voltar | **28** | `BackButton > Text` |
| Título da barra do menu | **32** | seu ajuste manual |
| Subtítulo da barra | **28** | seu ajuste manual |
| Rótulo / valor de pill | **23 / 27** | seu ajuste manual |
| Título / subtítulo de node | **28 / 21** | seu ajuste manual |

Duas escalas convivem de propósito: **tela** (telas cheias) e **mapa** (marcadores, que precisam caber em cards pequenos).

**Cor de texto:** branco puro em tudo, como você fez. O único texto realmente apagado é `GamePalette.HexMuted` (`#7A9CC0`), usado no botão Voltar e nas notas de rodapé — que é exatamente o tom do Voltar das suas telas antigas. Quem separa hierarquia agora é o **tamanho**, não o tom.

### Cores que estavam escritas dentro das telas

Estavam hard coded dentro dos scripts. Foram para o `GamePalette` **sem alterar um único tom** — a mudança é só de lugar:

| Antes (no script) | Agora | Onde estava |
|---|---|---|
| `new Color(0.08f,0.12f,0.20f)` | `HexCardNormal` `#141F33` | RestaurantScreenView |
| `new Color(0.45f,1f,0.10f,0.18f)` | `HexCardSelected` `#73FF1A` | RestaurantScreenView |
| `new Color(0.16f,0.24f,0.36f)` | `HexBorderNormal` `#293D5C` | RestaurantScreenView |
| `new Color(0.70f,1f,0f)` | `HexBorderSelected` `#B3FF00` | RestaurantScreenView |
| `new Color(0.18f,0.62f,0.18f)` | `HexSegmentSelected` `#2E9E2E` | RestaurantScreenView |
| `new Color(0.086f,0.639f,0.29f)` | `HexScoreGood` `#16A34A` | Restaurant **e** MenuPricing |
| `new Color(0.851f,0.604f,0.043f)` | `HexScoreAverage` `#D99A0B` | Restaurant **e** MenuPricing |
| `new Color(0.882f,0.114f,0.282f)` | `HexScoreBad` `#E11D48` | Restaurant **e** MenuPricing |
| `new Color(0.35f,0.35f,0.35f,0.85f)` | `HexDisabledBank` `#595959` | BankCardView |
| `"#2563EB"` | `GamePalette.HexPrimary` | MapMenuNode (default) |

As faixas de pontuação estavam **duplicadas** em `RestaurantScreenView` e `MenuPricingScreenView`. Agora as duas chamam `GamePalette.ScoreColor(valor)` — não têm mais como divergir.

Também entraram os tokens das telas antigas: `HexLegacyHeader #13557B`, `HexLegacyBackground #0D1B2D`, `HexLegacyCard #0F1E30`, `HexLegacyFooter #0D1525`, `HexConfirm #2E7D5B`.

### Uma coisa que eu não mexi, de propósito

`Panel_Location` e `Panel_Restaurant` **não estão no `PanelRegistry`**. Se o menu for aberto a partir delas, o `MenuNavigator` não vai saber devolver o jogador para lá. Hoje isso não acontece porque o botão fica oculto durante D1–D3. Se você ligar `Visible During Initial Setup`, esse caminho precisa ser resolvido antes.

---

## 0-C. Redesign mobile do Meu Estabelecimento

**A causa raiz não era cor.** O `CanvasScaler` está com *Match = Height*. Num P40 Pro landscape (2640×1200) a escala vira `1200/1080 = 1.11`, e o canvas passa a ter **~2376 unidades de largura**, não 1920. Os cards esticavam nessa largura toda e o rótulo ficava a ~800px do valor — o olho não conseguia ligar um ao outro. Somado a texto de 22px, que no aparelho vira ~1,4 mm de altura.

O que mudou:

1. **`MaxWidthFitter`** no `ContentFrame`: estica até no máximo 1800 e centraliza. Resolve para qualquer aparelho, em qualquer orientação.
2. **Tipografia mobile**: rótulo 22→26 (caixa alta, com espaçamento), valor 22→34, KPI 30→46, título de card 25→30, título de header 28→34, botão Voltar 21→26.
3. **Resumo virou grade 4×2 de chips**: ícone à esquerda, rótulo pequeno em cima, valor grande logo abaixo — a 4px um do outro. As células usam âncoras fracionárias também na vertical, então a grade preenche a altura sozinha. Fim dos 40% de espaço morto.
4. **Indicadores viraram 4 tiles** no topo, com faixa de cor lateral e número grande. A barra de score vive dentro do tile de Score.
5. **`SafeAreaFitter`** no botão flutuante, no conteúdo dos painéis novos e no conteúdo das barras superiores.

### Sobre o SafeAreaFitter

A implementação comum sobrescreve `anchorMin`/`anchorMax` com as frações do `Screen.safeArea`. Isso **só funciona se o objeto for filho direto de algo que cobre a tela inteira**. Num objeto aninhado — dentro de uma barra de 112px, por exemplo — as frações passariam a ser calculadas sobre a barra e o resultado sai errado.

Aqui a margem é convertida para **pixels de canvas** e somada aos offsets originais, sem tocar nas âncoras. Funciona em qualquer nível da hierarquia e não quebra barras ancoradas no topo. Ele guarda os offsets originais na primeira ativação, então girar o aparelho várias vezes não acumula margem.

As barras (`Header`, `TopBar`) continuam sangrando até a borda — quem recua é só o conteúdo, nos filhos `HeaderSafe` e `Safe`.

### Botão Menu do Jogo

Agora **âmbar sólido** (`#F0A02E`) com texto escuro (`#1B1305`), 268×64.

---

## 0-B. Paleta: voltou ao dark navy (mais claro) + botão de acesso ao menu

A paleta clara foi descartada — o véu branco lavava o mapa. Agora é **o mesmo dark navy das telas que já existem, um degrau acima em cada nível**:

| Uso | Antes | Agora |
|---|---|---|
| Fundo geral | `#0D1B2D` | **`#122540`** |
| Topbar / header | `#0A1220` | **`#0F1E33`** |
| Card / node | `#1A2A3E` | **`#1E3352`** |
| Hairline | `#1E3A5F` | **`#2B4C79`** |
| Texto corpo | `#7A9CC0` | **`#9CBADB`** |
| Primária | `#2563EB` | **`#3D82F7`** |
| Positivo | `#16A34A` | **`#2ECC71`** |
| Score | `#7C3AED` | **`#A78BFA`** |

O véu sobre o mapa agora é **escuro** (`#0B1A2E` a 42%), não branco. Ele existe só para rebaixar a saturação da arte o suficiente para os cards lerem por cima. Se ainda quiser mais ou menos mapa, mexa em `GamePalette.MapScrimAlpha` em passos de `0.05` e rode o builder de novo.

Os badges dos ícones usam o accent com alpha baixo (`#3D82F733`) em vez de pastel — acendem sobre o card escuro sem virar bloco claro.

**Botão "Menu do Jogo":** agora existe um botão flutuante em `Canvas > MenuAccess > Button`, no canto superior direito. Ele é irmão dos painéis, não filho de nenhum — então aparece por cima de **qualquer** tela, incluindo as antigas (`Panel_Location`, `Panel_Restaurant`, `Panel_MenuPricing`, `Panel_Financial`) sem precisar editar nenhuma delas. Ele se esconde sozinho:

- quando o próprio `Panel_Menu` está aberto;
- em `Bootstrap`, `MainMenu`, `FinalReport` e `GameOver_Bankruptcy`;
- durante as decisões iniciais (`Config_Location`, `Config_Restaurant`, `Config_TargetSegment`, `Config_Review`) — controlado pelo campo `Visible During Initial Setup` no Inspector, hoje **desligado**.

O antigo botão "Menu do Jogo" que ficava no header dos painéis novos foi aposentado, para não ter dois no mesmo canto.

---

## 0. O que deu errado na primeira tentativa (corrigido)

`CashPill` e `ScorePill` **não eram caixas** — eram objetos `TextMeshProUGUI` soltos na TopBar. O builder tentou adicionar um `Image` neles para virarem pills, e a Unity **proíbe dois componentes `Graphic` no mesmo GameObject**. Isso lançou uma exceção no meio da execução: a TopBar foi construída, e daí para frente **nada mais rodou** — sem os 5 nodes, sem os 3 painéis novos, sem a navegação. O que você viu na Scene foi esse meio-caminho sobreposto às telas antigas.

Três correções entraram:

1. **Objetos incompatíveis agora são aposentados, não reaproveitados.** `CashPill`, `ScorePill` e o `Node` antigo são renomeados com sufixo `_ANTIGO` e **desativados** (nada é apagado — estão na Hierarchy, é só reativar se quiser algo de volta). Os novos são construídos do zero.
2. **Cada etapa é isolada.** Se uma falhar, o Console diz `FALHOU na etapa 'X'` com o motivo, e as outras continuam. Nunca mais para no meio em silêncio.
3. **Play Mode é bloqueado.** Rodar o builder durante o Play não adianta: a Unity descarta alterações de cena ao sair do Play. Agora ele avisa e recusa.

---

## 1. Rode isto na Unity

1. **Saia do Play Mode** se estiver rodando (Ctrl+P).
2. Espere a recompilação terminar (o ícone de progresso no canto inferior direito some).
3. Confira o **Console**. Erro vermelho de compilação? Me manda o print — com erro de compilação o menu `Tools` nem aparece.
4. Abra a cena `Assets/Scenes/GameScene.unity`.
5. Menu **Tools > Jogo > 3 - Construir Tudo**.
6. **Ctrl+S** para salvar a cena.

No fim ele **isola o `Panel_Menu`**: desliga os outros painéis e enquadra o mapa na Scene. É só para você conseguir enxergar — na Scene todos os painéis ocupam o mesmo retângulo, então com vários ativos ao mesmo tempo vira uma pilha ilegível. Em Play quem manda continua sendo o `UIStateListener` + `MenuNavigator`.

Se quiser trocar o que está visível depois, use **Tools > Jogo > Ver apenas o Panel Menu** ou **Ver apenas o Meu Estabelecimento**.

O builder é idempotente: pode rodar quantas vezes quiser que nada é duplicado, e `Ctrl+Z` desfaz tudo.

---

## 2. O que o builder monta

### `Panel_Menu` (reaproveitando o que já existia)

```
Panel_Menu
├── Backdrop            fundo #F2F5FA
├── MapBackground       mapa-menu.png esticado na tela inteira
├── MapScrim            véu #F2F5FA a 62% (deixa os nodes legíveis)
└── MapLayer
    ├── TopBar          + MenuHudView
    │   ├── Mark / MenuTitle / MenuSubtitle
    │   ├── CashPill    ← renomeado de "CashPìll" (tinha um í acentuado)
    │   ├── ScorePill
    │   └── RoundPill
    ├── Node_Bank            → Panel_Financial
    ├── Node_Store           → Panel_EquipmentStore
    ├── Node_Establishment   ← renomeado de "Node"
    ├── Node_Rh              → Panel_Team
    └── Node_MenuPricing     → Panel_MenuPricing
```

Cada node é `GlowOverlay` (halo que acende no hover, via o seu `MapMenuNode`) + `Card` (Button + PanelNavButton) com `IconBadge`, `Title` e `Subtitle`.

Os nodes ficam **ancorados em coordenada normalizada** sobre o mapa, cada um em cima do prédio que representa. Como a âncora é normalizada, eles acompanham o stretch da imagem em qualquer resolução.

| Node | Prédio no mapa | u, v |
|---|---|---|
| Banco | prédio azul, topo-centro | 0.498, 0.681 |
| Loja de Equipamentos | galpão laranja, direita | 0.800, 0.591 |
| Meu Estabelecimento | restaurante central | 0.519, 0.453 |
| RH | prédio roxo, esquerda | 0.210, 0.603 |
| Cardápio | escritórios, inferior-direita | 0.761, 0.207 |

Se algum node ficar fora de lugar, é só mexer no `AnchorNormalized` da tabela `Nodes` em `MenuPanelBuilder.cs` — ou arrastar na Scene e me dizer os valores finais.

### `Panel_Establishment`

Header com **Voltar** + **Menu do Jogo**, e dois cards:

- **Resumo da empresa** — Localização, Tipo de restaurante, Público alvo, Cardápio, Estratégia de preço, Equipamentos, Equipe, Coerência.
- **Indicadores** — Score total, Caixa disponível, Receita estimada, Receita mensal, barra de score, rodada e um aviso de contexto.

### `Panel_Team` e `Panel_EquipmentStore`

Placeholders navegáveis: header completo, botão Voltar funcional e área de conteúdo marcada com o texto do que vai entrar ali.

---

## 3. Arquitetura — o que entrou e por quê

Nada do que já existia foi alterado. Dez arquivos novos:

| Arquivo | Camada | Papel |
|---|---|---|
| `Adapter/In/UI/Theme/GamePalette.cs` | Adapter | Fonte única das cores. Trocar paleta = trocar os HEX aqui. |
| `Adapter/In/UI/Navigation/PanelId.cs` | Adapter | Identificador estável de cada tela. |
| `Adapter/In/UI/Navigation/PanelRegistry.cs` | Adapter | Tabela `PanelId → GameObject`. |
| `Adapter/In/UI/Navigation/MenuNavigator.cs` | Adapter | Pilha de histórico. `Open`, `Back`, `OpenRoot`. |
| `Adapter/In/UI/Navigation/PanelNavButton.cs` | Adapter | Cola um Button na navegação sem UnityEvent. |
| `Adapter/In/UI/EstablishmentSummaryView.cs` | Adapter | Escreve nos TMP. Não calcula nada. |
| `Adapter/In/UI/MenuHudView.cs` | Adapter | Pills de Score/Caixa/Rodada. |
| `Adapter/In/Controllers/EstablishmentSummaryController.cs` | Adapter | Liga serviço → view no `OnEnable`. |
| `Domain/Service/EstablishmentSummary.cs` | Domain | DTO somente-leitura. |
| `Domain/Service/EstablishmentSummaryService.cs` | Domain | Calcula os indicadores. Não grava nada. |

### Por que a navegação não briga com o `UIStateListener`

O `UIStateListener` continua dono do fluxo por `GameState`. A única ponte que o builder cria é apontar `UIStateListener.managementHubPanel` para o `Panel_Menu` — ou seja, entrar em `Management_Hub` mostra o mapa. Daí em diante o `MenuNavigator` assume, e ele **só liga e desliga os GameObjects que estão no `PanelRegistry`**. Nada fora dessa lista é tocado.

Por isso o `openRootOnStart` do `MenuNavigator` vem **desligado**: se os dois tentassem abrir tela no `Start`, disputariam o controle. Ligue só se quiser testar o mapa isolado.

### Como o "Voltar" funciona

`MenuNavigator` mantém uma pilha. Toda tela aberta empilha a anterior. O botão Voltar do header desempilha; se a pilha esvaziar, cai no mapa. Então dá para o jogador abrir o Cardápio pelo mapa, mudar a escolha, voltar, abrir o Banco, voltar — sempre chegando onde estava.

---

## 4. Números dos indicadores — de onde saem

Tudo vem dos ScriptableObjects que já existem, nada foi inventado:

- **Ticket médio** — média dos preços salvos em `menuPricingJson`, com fallback em `ProductData.price`.
- **Demanda** — `RestaurantData.estimatedMonthlyCustomers × alignmentFactor`, limitada pela capacidade real (`RoleData.maxClientsSupported` + `EquipmentData.capacityBonus`).
- **Receita estimada** — ticket médio × demanda.
- **Receita mensal** — receita − insumos (`ProductData.inputCostRatio`) − folha (`RoleData.salary`) − `RestaurantData.baseMonthlyCost` − parcela (`CreditLineData.termRounds` e `monthlyInterestRate`).

> **Atenção:** `RoleData` e `EquipmentData` não estão em `Resources/`. Só existem os quatro assets de teste em `Scripts/Game/TESTES/`. Enquanto isso, folha salarial e bônus de capacidade entram como **zero**, e a tela mostra um aviso dizendo exatamente isso — em vez de exibir um número inventado. Para ativar: crie `Assets/Resources/Roles/` e `Assets/Resources/Equipment/` e coloque os assets lá.

---

## 5. Uma alteração de import que o builder faz

`Art/Sprites/UI/botao.png` ganha borda 9-slice de 48px. Sem isso, `Image.Type.Sliced` não funciona e os cantos arredondados esticam. Quem já usa a sprite como `Simple` não é afetado.

O raio aparente é controlado por `pixelsPerUnitMultiplier`, então a mesma sprite serve para node (raio ~22px), card (~16px), botão (~11px) e pill (totalmente arredondada). Os valores estão em `GamePalette.Ppu*`.

---

## 6. Depois de rodar, me conte

- O Console ficou limpo?
- Os 5 nodes caíram em cima dos prédios certos?
- O véu sobre o mapa está no ponto, ou o mapa ficou lavado demais / visível demais? (`GamePalette.MapScrimAlpha`)
- Play → os cliques navegam e o Voltar devolve certo?

Aí partimos para o item **#3**, o sistema de eventualidades — que vai entrar em classes separadas, sem tocar em nada disto.
