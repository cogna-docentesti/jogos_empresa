# Skin do redesign — tela de localização (V2)

Como ligar os 112 PNGs desta pasta na tela que já existe, sem refazer a cena.

## O que já é automático

**Import dos PNGs** — `Editor/UI/RedesignUiTexturePostprocessor.cs` roda sozinho
em tudo que entra nesta pasta: Sprite, sem compressão, sem mipmap, Bilinear,
100 pixels/unit, e o **9-slice** já com a borda certa para cada arte
(painel 30px, botões 20/16px, linhas 18px…).

Se você copiar PNG por cima de um existente, o Unity não reaplica o import.
Nesse caso rode: **Tools > Redesign UI > Reimportar sprites do redesign**.

## Passo a passo

### 1. Gerar a skin

**Tools > Redesign UI > Criar-atualizar skin da tela de localizacao**

Cria `Assets/Resources/LocationSkinV2.asset` achando os 56 sprites pelo nome.
O Console avisa se algum faltar. Rodar de novo é seguro — reaproveita o asset,
então não quebra quem já aponta pra ele.

### 2. Ligar na cena

Em `LocationSelectionScene`, no objeto com o `LocationScreenV2View`,
preencha o bloco **Skin - opcional**:

| Campo | Aponte para |
|---|---|
| `Skin` | `LocationSkinV2.asset` |
| `Panel Background` | a `Image` de fundo do `SidePanel` |
| `Highlight Background` | a `Image` de fundo da caixa de destaque |
| `Stat Row Backgrounds` | as 3 `Image` de fundo das linhas de indicador |
| `Stat Tiles` | as 3 pastilhas, **nesta ordem**: Investimento, Movimento, Concorrência |
| `Top Bar Background` | a `Image` do `TopBar` |
| `Brand Badge` | a `Image` do `Crest` |
| `Ornament Left/Right` | as `Image` de `OrnamentLeft` / `OrnamentRight` |

Deixar qualquer um vazio é permitido — aquele pedaço só continua com o visual antigo.

### 3. Atenção: as caixas são Frame + Fill

O builder procedural monta cada caixa com **duas** Images sobrepostas
(`Frame` para a borda, `Fill` para o miolo) — está explicado no comentário do
`LocationMockupTokens.cs`: com um Image sliced só, raio e espessura são a mesma
variável, então não dava pra ter raio 13 e borda 2.

**Os PNGs não têm esse problema** — borda e raio já vêm desenhados no mesmo
sprite. Então, onde você ligar um sprite da skin:

- aponte o campo para o **Fill** (o de baixo), e
- **desative o `Frame`** correspondente.

Se deixar os dois, você vê a borda nova por baixo da borda velha.

## O que troca sozinho durante o jogo

Nada disso precisa de código novo — entra na lógica que já existe:

| Interação | O que acontece |
|---|---|
| Clique num marcador | `SelectMarker` troca **fundo do painel** e **caixa de destaque** para a cor da zona |
| Marcador escolhido | Pino troca de `pin-<zona>-idle` para `pin-<zona>-active` (PNG preenchido) |
| Brilho no mapa | `glow-<zona>-idle` ↔ `glow-<zona>-active`; a pulsação que já existia continua, mexendo só no alfa |
| Hover / clique nos botões | `SpriteSwap` com `btn-continuar-hover` / `-pressed` e `btn-menu-hover` |
| Estado vazio | Painel volta para `panel-bg.png` (neutro) |

## Cores

Com a skin ativa, o accent vem **dela**, não do `colorHex` do `LocationData` —
senão o texto ficaria num azul (`#4A90D9`) e o PNG em outro (`#3B82F6`).

Os `LocationData` continuam intactos. Se você tirar a skin, tudo volta ao
comportamento anterior.

Paleta completa: `PALETA.md` / `PALETA.html` na pasta do projeto web.

## Variantes `-halo`

Cada `panel-bg-<cor>` tem um par `panel-bg-<cor>-halo` (504×724) com o brilho
vazando pra fora. O cartão de 464×684 fica em x=20, y=20. Use só se o painel
tiver folga no layout — senão o halo é cortado.
