# Reformulação dos tutoriais — proposta em discussão

## Direção acordada

- Explicar as consequências das decisões no jogo antes de ensinar a interface.
- Usar passos curtos, com linguagem natural e uma ideia por passo.
- Alternar explicações e ações quando fizer sentido, sem transformar toda interação em tutorial.
- Explicar o que é obrigatório, o que é opcional e o que pode ser alterado depois.
- Informar que loja e RH podem ser revisitados entre as rodadas.
- Seguir a identidade visual de new-screens.
- Planejar e revisar cada tela com a usuária antes de implementar o novo tutorial.

## Situação atual

As introduções longas de banco, equipamentos e equipe e as dicas antigas de clique, rolagem e teclado foram removidas da cena e do prefab da loja. As regras de compra do básico e equipe mínima continuam. Os novos tutoriais de equipamentos e equipe estão implementados. O de banco segue em planejamento.

## Loja de equipamentos — sequência implementada

1. **Efeito no negócio:** explicar como equipamentos contribuem para capacidade e qualidade, sem prometer aumento automático de vendas.
2. **Primeiro, o essencial:** destacar o botão real de adicionar no cartão do kit. Só avançar após adicioná-lo à seleção da loja.
3. **Confira antes de comprar:** destacar o botão real de revisar carrinho. Só avançar após abri-lo.
4. **Você pode voltar depois:** explicar que não é preciso comprar tudo agora e que a loja pode ser revisitada entre rodadas.
5. **Hora de concluir a compra:** destacar a confirmação real no carrinho. Só avançar após a compra efetivamente salva.
6. **Explore a loja:** explicar a diferença entre equipamentos básicos e específicos, sem mencionar premium. Liberar a exploração; o avanço para a equipe usa o botão normal da loja.

O texto e a apresentação estão em `Assets/Scripts/Game/Adapter/In/UI/EquipmentTutorial.cs`, usando as fontes, superfícies e botões de new-screens. As condições de avanço são:

- A introdução avança por “Continuar”.
- Nos passos 2, 3 e 5 não existe botão de avanço no tutorial: apenas o controle real destacado permite continuar. A máscara visual deixa esse controle acessível e posiciona o texto sem cobri-lo.
- O quarto passo avança por “Continuar”, dentro do carrinho.
- Após a compra, “Explorar loja” fecha a sexta explicação sem abrir automaticamente a equipe.

As explicações ficam em uma faixa reservada abaixo da área de produtos (ou abaixo dos itens do carrinho), sem cobri-los. A área de rolagem é reduzida apenas enquanto a explicação aparece e volta ao tamanho original ao terminar. Nos passos de interação 2, 3 e 5, as máscaras de entrada são transparentes: a interface permanece iluminada, com apenas o botão destacado. O escurecimento fica restrito às explicações sem ação na loja.

O progresso é salvo por partida em `equipmentTutorialStep`, com `equipmentTutorialVersion` para compatibilidade com o roteiro anterior. Ao recarregar sem o kit comprado, uma seleção de carrinho perdida retorna ao passo do kit; com o kit comprado, aparece apenas a explicação final. O tutorial concluído não reaparece nas visitas pelo hub. “Revisável” significa novas compras, não venda de equipamentos nem reembolso.

Validação: `Tools > Jogos de Empresa > Validar tutorial dos equipamentos` executa testes de retomada, layout e compra usando objetos temporários e banco em memória. O resultado é gravado em `output/equipment-tutorial/tests.xml`.

O roteiro de seis etapas teve 10 testes aprovados no Unity. A validação cobre as ações reais da loja, as travas contra avanço pelo tutorial nos passos de ação, a área clicável livre no destaque, a sobreposição sobre o carrinho, a persistência e a compra sem débito duplicado. As imagens `step-2.png` a `step-6.png` mostram o roteiro sobre a loja e o carrinho reais em um cenário de teste isolado.

## Equipe

1. Explicar como a equipe contribui para a capacidade e a qualidade do restaurante.
2. Destacar o botão **+** das funções que faltam e avançar após a primeira contratação salva.
3. Destacar a folha mensal e explicar a cobrança no fechamento e a possibilidade de voltar ao RH.
4. Destacar as quantidades atuais e mínimas no resumo até a equipe atingir os mínimos do restaurante.
5. Destacar **Confirmar equipe e abrir hub** e deixar o jogador conferir o resumo.

O tutorial aparece durante a preparação inicial e guarda o progresso por partida. A explicação ocupa uma faixa abaixo dos cartões e os destaques deixam a rolagem disponível.

## Próxima tela

- **Banco.** Capital próprio, necessidade de crédito, juros, parcelas e efeito no caixa.

## Entrada no jogo

O carregamento atual retoma automaticamente uma partida salva. Apertar Play no Editor não equivale a iniciar uma partida nova. Uma partida nova deve seguir identificação → localização → restaurante e público → cardápio → banco → equipamentos → equipe.

Foi removida a inferência adicionada no merge que podia redirecionar saves antigos sem marcador de preparação para banco, equipamentos ou equipe. A retomada de etapas explicitamente salvas continua funcionando. Nenhum save foi apagado.

Decisão da usuária: manter a retomada automática, sem tela de “Novo jogo / Continuar”. Com progresso salvo, continuar da etapa pendente; sem progresso, começar pela identificação e seguir as escolhas iniciais.
