# Admin do Jogo de Empresas

Painel web para cadastrar os dados do jogo. Hoje cadastra **eventos** (`EventData`).
A estrutura foi feita para receber os próximos cadastros (ingredientes, equipamentos, produtos etc.) sem reescrever telas.

## Como os dados chegam no Unity

```
Navegador (admin)  ->  admin-data/events.json  ->  Unity: Tools > Jogo > Eventos > Importar JSON do Admin  ->  Assets/Resources/Events/*.asset
```

- O admin grava `admin-data/events.json` na raiz do projeto Unity (fora de `Assets/`, então o Unity não importa esse arquivo sozinho).
- O importador (`unity/EventJsonImporter.cs`) valida o JSON com as mesmas regras do `EventAssetGenerator` e cria/atualiza os assets.
- O caminho inverso também existe: `Tools > Jogo > Eventos > Exportar assets para JSON do Admin`.

### Instalar o importador no Unity (uma vez)

Copie `admin/unity/EventJsonImporter.cs` para `Assets/Editor/Events/EventJsonImporter.cs`.
Ele fica ao lado do `EventAssetGenerator.cs` e aparece no mesmo menu.

> Atenção: depois de adotar o admin, **não use** `Tools > Jogo > Eventos > Gerar ou atualizar eventos`.
> Esse menu reescreve os 20 eventos com os valores fixos do código e desfaz o que foi cadastrado no admin.

## Rodar no seu computador

Pré-requisito: Node.js 20.12 ou mais novo (`node -v`).

```powershell
cd C:\Users\elisa\Projects\jogos_empresa\admin
npm install
Copy-Item .env.example .env     # no Linux/macOS: cp .env.example .env
npm run dev
```

Abra http://localhost:3000. Com `STORAGE=file`, as alterações vão direto para `..\admin-data\events.json`.

Testes das regras de validação: `npm test`.

## Publicar em um servidor gratuito

Veja `TUTORIAL_DEPLOY.html` (Render + armazenamento no próprio GitHub).

## Estrutura

```
admin/
├─ server/                      Backend Node.js (Express)
│  ├─ index.js                  composição: storage -> serviço -> rotas
│  ├─ config.js                 variáveis de ambiente
│  ├─ auth.js                   login (HTTP Basic Auth)
│  ├─ application/
│  │  └─ collectionService.js   casos de uso: listar, criar, editar, excluir, importar
│  ├─ http/
│  │  └─ collectionRoutes.js    rotas REST /api/...
│  └─ storage/                  adaptadores de saída
│     ├─ fileStorage.js         JSON em disco (uso local)
│     └─ githubStorage.js       JSON dentro do repositório GitHub (servidor gratuito)
├─ shared/                      Código usado pelo navegador E pelo servidor
│  ├─ enums.js                  espelho dos enums C#
│  ├─ validation.js             motor genérico de schema e validação
│  └─ collections/
│     ├─ index.js               registro das coleções
│     └─ events.js              schema de EventData + regras
├─ public/                      Frontend (HTML, CSS, JS sem framework)
├─ unity/EventJsonImporter.cs   importador/exportador para o Editor do Unity
└─ test/                        testes (node --test)
```

## API

| Método | Rota | Uso |
|---|---|---|
| GET | `/api/meta` | coleções e onde os dados estão salvos |
| GET | `/api/collections/events` | lista + relatório de validação |
| GET | `/api/collections/events/items/:id` | um item |
| POST | `/api/collections/events/items` | cria |
| PUT | `/api/collections/events/items/:id` | atualiza (permite trocar o ID) |
| DELETE | `/api/collections/events/items/:id` | exclui |
| GET | `/api/collections/events/export` | baixa o JSON |
| POST | `/api/collections/events/import` | substitui tudo pelo JSON enviado |
| GET | `/healthz` | health check (sem senha) |

## Adicionar um novo cadastro (ex.: equipamentos)

1. Crie `shared/collections/equipments.js` copiando a estrutura de `events.js`.
   Descreva os campos com os mesmos nomes do ScriptableObject (`EquipmentData.cs`):
   ```js
   { key: 'cost', label: 'Custo (R$)', type: 'integer', min: 0, width: 'third' },
   { key: 'category', label: 'Categoria', type: 'enum', enum: 'EquipmentCategory', width: 'third' },
   { key: 'applicableTypes', label: 'Tipos de restaurante', type: 'enumList', enum: 'RestaurantType' },
   { key: 'qualityBonus', label: 'Bônus de qualidade', type: 'number', min: 0, max: 1, step: 0.01 },
   ```
2. Registre em `shared/collections/index.js` (adicione em `COLLECTIONS` e remova de `PLANNED`).
3. Pronto: menu, listagem, formulário, validação, API e arquivo `admin-data/equipments.json` passam a existir.
4. No Unity, crie um importador equivalente (use `EventJsonImporter.cs` como modelo).

Tipos de campo disponíveis: `string`, `text`, `number`, `integer`, `boolean`, `enum`, `enumList`, `object` (campos agrupados) e `list` (lista de objetos, com `minItems`, `maxItems`, `uniqueBy`, `reorderable`).

Campos do tipo `Sprite` (ícones) não são editáveis pelo admin: continuam sendo configurados no Inspector.
O importador deve atualizar apenas os campos que o admin controla, preservando os demais.

## Observações encontradas no projeto

- Os 20 assets em `Assets/Resources/Events` estão com `description` vazia, embora o `EventAssetGenerator` tenha os textos no dicionário `Descriptions`.
  O `admin-data/events.json` inicial já traz as descrições do gerador; ao importar, os assets passam a tê-las.
- `EventData.maxRound` tem valor padrão 12 no C#, mas o ciclo atual tem 3 rodadas. O admin avisa quando `maxRound > 3`.
- As regras de quantidade do gerador (20 eventos, 10 positivos/10 negativos, 15 aleatórios/5 condicionais) aparecem como **avisos** no admin, para permitir cadastrar eventos novos.
