/* Original ART-08 geometry. Coordinates and white primitives are the editable masters. */
const stroke=b=>`<g fill="none" stroke="#FFFFFF" stroke-width="16" stroke-linecap="round" stroke-linejoin="round">${b}</g>`;
const path=d=>`<path d="${d}"/>`;
const rect=(x,y,w,h,r=0)=>`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}"/>`;
const circle=(x,y,r)=>`<circle cx="${x}" cy="${y}" r="${r}"/>`;
const back=stroke(path('M216 128 H40 M112 56 L40 128 L112 200'));
const up=stroke(path('M64 160 L128 96 L192 160'));
const speaker=path('M40 96 H80 L128 56 V200 L80 160 H40 Z');
module.exports={
  ICON_BACK:{name:'Voltar',semantic:'Seta completa para a esquerda, com haste; separada do botão que a recebe.',body:back},
  ICON_NEXT:{name:'Avançar',semantic:'Par de avançar espelhado horizontalmente do voltar, no mesmo canvas e pivot.',body:`<g transform="translate(256 0) scale(-1 1)">${back}</g>`},
  ICON_CHEVRON_UP:{name:'Chevron para cima',semantic:'Chevron vertical para recolher ou navegar para cima, sem haste.',body:up},
  ICON_CHEVRON_DOWN:{name:'Chevron para baixo',semantic:'Par de expandir espelhado verticalmente do chevron para cima.',body:`<g transform="translate(0 256) scale(1 -1)">${up}</g>`},
  ICON_CLOSE:{name:'Fechar',semantic:'Cruz diagonal de fechamento, isolada de botão e painel.',body:stroke(path('M64 64 L192 192 M192 64 L64 192'))},
  ICON_MENU:{name:'Menu',semantic:'Três barras horizontais de menu, sem recipiente ou moldura.',body:stroke(path('M48 64 H208 M48 128 H208 M48 192 H208'))},
  ICON_PLUS:{name:'Adicionar',semantic:'Sinal geométrico de adição, para receber tint e compor o stepper separado.',body:stroke(path('M48 128 H208 M128 48 V208'))},
  ICON_MINUS:{name:'Remover',semantic:'Sinal geométrico de subtração com o mesmo comprimento e espessura do sinal de adição.',body:stroke(path('M48 128 H208'))},
  ICON_CHECK:{name:'Confirmar',semantic:'Marca de confirmação sem círculo ou badge; aplicável às bases de seleção do ART-05.',body:stroke(path('M56 128 L104 176 L200 80'))},
  ICON_LOCK:{name:'Bloqueado',semantic:'Cadeado com arco e corpo sólido; furo de fechadura em recorte alfa verdadeiro.',body:stroke(path('M80 112 V88 A48 48 0 0 1 176 88 V112'))+'<path fill-rule="evenodd" d="M68 112 H188 Q200 112 200 124 V204 Q200 216 188 216 H68 Q56 216 56 204 V124 Q56 112 68 112 Z M128 144 C112 144 108 160 120 166 V184 H136 V166 C148 160 144 144 128 144 Z"/>'},
  ICON_ALERT:{name:'Alerta',semantic:'Triângulo vazado de atenção com sinal geométrico central; sem preenchimento de badge.',body:stroke(path('M128 40 L216 200 Q220 208 208 208 H48 Q36 208 40 200 Z M128 104 V144'))+circle(128,176,8)},
  ICON_INFO:{name:'Informação',semantic:'Símbolo geométrico de informação em anel vazado; o anel faz parte do glifo, sem disco de fundo.',body:stroke(circle(128,128,88)+path('M128 120 V176'))+circle(128,88,9)},
  ICON_HELP:{name:'Ajuda',semantic:'Interrogação desenhada em curvas dentro de anel vazado; sem fonte tipográfica ou fundo.',body:stroke(circle(128,128,88)+path('M100 96 C100 64 156 64 156 96 C156 120 128 120 128 144'))+circle(128,176,9)},
  ICON_SAVE:{name:'Salvar',semantic:'Disquete simplificado com encaixe superior e área inferior vazados, sem letras ou rótulo.',body:stroke(path('M40 40 H184 L216 72 V216 H40 Z')+rect(80,40,80,64)+rect(80,144,96,72))},
  ICON_EXIT:{name:'Sair',semantic:'Porta aberta com seta saindo para a direita; distinta da seta simples de avançar.',body:stroke(path('M128 80 V40 H40 V216 H128 V176 M96 128 H216 M184 96 L216 128 L184 160'))},
  ICON_RESET:{name:'Reiniciar',semantic:'Seta única no sentido anti-horário em arco aberto, distinta do par de sincronização.',body:stroke(path('M56 88 C72 56 96 40 128 40 C176 40 216 80 216 128 C216 176 176 216 128 216 C88 216 48 184 40 144 M56 40 V88 H104'))},
  ICON_DOWNLOAD:{name:'Baixar',semantic:'Seta vertical descendo para bandeja aberta, sem recipiente de botão.',body:stroke(path('M128 40 V152 M96 120 L128 152 L160 120 M48 168 V216 H208 V168'))},
  ICON_PLAY:{name:'Reproduzir',semantic:'Triângulo sólido de reprodução com centro óptico ajustado, sem círculo de fundo.',body:path('M80 48 Q80 40 88 44 L208 120 Q224 128 208 136 L88 212 Q80 216 80 208 Z')},
  ICON_PAUSE:{name:'Pausar',semantic:'Duas barras verticais sólidas, em caixa e pivot compartilhados com reprodução.',body:rect(64,48,40,160,6)+rect(152,48,40,160,6)},
  ICON_FILTER:{name:'Filtrar',semantic:'Funil vazado com haste inferior inclinada, sem linhas ou dados de filtragem.',body:stroke(path('M40 48 H216 L152 128 V192 L104 216 V128 Z'))},
  ICON_EDIT:{name:'Editar',semantic:'Lápis diagonal independente, com tampa separada por espaço alfa; sem folha ou contrato.',body:path('M48 168 L152 64 L184 96 L80 200 L40 216 Z M164 52 L176 40 Q184 32 192 40 L208 56 Q216 64 208 72 L196 84 Z')},
  ICON_TRASH:{name:'Excluir',semantic:'Lixeira vazada com tampa, alça e duas ranhuras, sem papel ou fundo.',body:stroke(path('M96 72 V48 H160 V72 M48 72 H208 M64 104 L76 216 H180 L192 104 M104 128 V184 M152 128 V184'))},
  ICON_SYNC:{name:'Sincronizar',semantic:'Duas setas curvas opostas, distintas do arco único de reiniciar e das setas retas de concorrência.',body:stroke(path('M48 112 C56 64 96 40 136 40 C168 40 192 56 208 80 M208 40 V88 H160 M208 144 C200 192 160 216 120 216 C88 216 64 200 48 176 M48 216 V168 H96'))},
  ICON_OFFLINE:{name:'Sem conexão',semantic:'Sinal Wi-Fi com diagonal de indisponibilidade e arcos interrompidos, sem badge de status.',body:stroke(path('M40 80 Q52 69 64 62 M112 44 Q168 40 216 80 M72 120 L88 108 M152 100 Q168 104 184 120 M104 160 Q128 140 152 160 M48 40 L208 216'))+circle(128,192,10)},
  ICON_CLOCK:{name:'Relógio',semantic:'Relógio analógico vazado com dois ponteiros, sem escala, números ou data.',body:stroke(circle(128,128,88)+path('M128 72 V128 L168 152'))},
  ICON_SEARCH:{name:'Buscar',semantic:'Lupa de aro circular e cabo diagonal; centro transparente.',body:stroke(circle(112,112,72)+path('M168 168 L216 216'))},
  ICON_SOUND_ON:{name:'Som ativo',semantic:'Alto-falante sólido com duas ondas curvas; corpo idêntico ao símbolo de som desativado.',body:speaker+stroke(path('M160 88 C184 108 184 148 160 168 M184 64 C224 100 224 156 184 192'))},
  ICON_SOUND_OFF:{name:'Som desativado',semantic:'Mesmo alto-falante do som ativo acompanhado de cruz diagonal; sem ondas.',body:speaker+stroke(path('M168 104 L208 152 M208 104 L168 152'))},
  ICON_FULLSCREEN:{name:'Tela cheia',semantic:'Quatro cantos abertos de expansão, sem moldura central ou screenshot.',body:stroke(path('M40 104 V40 H104 M152 40 H216 V104 M216 152 V216 H152 M104 216 H40 V152'))},
  ICON_KITCHEN:{name:'Cozinha',semantic:'Panela com tampa e duas alças, para categoria de equipamentos de cozinha; sem duplicar talheres.',body:stroke(path('M56 88 H200 M104 72 V56 H152 V72 M64 112 V180 Q64 208 100 208 H156 Q192 208 192 180 V112 Z M64 128 H40 V160 H64 M192 128 H216 V160 H192'))},
  ICON_REFRIGERATION:{name:'Refrigeração',semantic:'Geladeira vertical de duas portas, com puxadores geométricos e divisória; sem textura ou marca.',body:stroke(rect(64,40,128,176,12)+path('M64 112 H192 M88 72 V88 M88 144 V168'))},
  ICON_SERVICE:{name:'Atendimento',semantic:'Cloche de serviço com puxador e bandeja separados; categoria de atendimento distinta de cardápio e equipe.',body:stroke(path('M48 184 A80 80 0 0 1 208 184 Z M128 96 V104 M40 216 H216'))+circle(128,84,12)},
  ICON_FURNITURE:{name:'Móveis',semantic:'Cadeira frontal com encosto, assento e pés, para mobiliário do restaurante.',body:stroke(rect(80,40,96,88,12)+path('M80 128 V160 M176 128 V160 M64 160 H192 V184 H64 Z M80 184 V216 M176 184 V216'))}
};
