"""Monta Conferencia_Motor.xlsx a partir dos CSVs do exportador (Game > Simulação > Exportar conferência).

Uso: python build_xlsx.py <pasta_com_csvs> <saida.xlsx>

Para cada cenário: aba "Cn Dados" (o CSV como veio do Unity) e aba "Cn Conferência" (fórmulas que
recalculam cada número a partir das entradas e mostram Motor | Planilha | Diferença).
"""
import csv
import glob
import os
import sys

from openpyxl import Workbook
from openpyxl.formatting.rule import FormulaRule
from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
from openpyxl.utils import get_column_letter as L

BLOCK, DAY_HDR, MONTH_VALS = 210, 100, 135        # layout do ConferenciaExport.cs
CONF_TOP, CONF_BLOCK = 3, 112                       # aba de conferência: mês m começa em 3 + 112*(m-1)
FONT = "Arial"
BLUE = Font(name=FONT, color="0000FF", size=10)
BLACK = Font(name=FONT, color="000000", size=10)
GREEN = Font(name=FONT, color="008000", size=10)
BOLD = Font(name=FONT, bold=True, size=10)
TITLE = Font(name=FONT, bold=True, size=14)
HEAD_FILL = PatternFill("solid", fgColor="1F3A5F")
HEAD_FONT = Font(name=FONT, bold=True, color="FFFFFF", size=10)
SUB_FILL = PatternFill("solid", fgColor="DCE6F1")
HELP_FILL = PatternFill("solid", fgColor="F2F2F2")
RED_FILL = PatternFill("solid", fgColor="F8CBAD")
GREEN_FILL = PatternFill("solid", fgColor="C6EFCE")
THIN = Side(style="thin", color="BFBFBF")
SCREEN_EXTRA = 0.02                                 # a tela grava em float: até 2 centavos de arredondamento


def read_csv(path):
    with open(path, encoding="utf-8-sig", newline="") as f:
        rows = list(csv.reader(f, delimiter=";"))
    grid = {}
    for r, row in enumerate(rows, start=1):
        for c, v in enumerate(row, start=1):
            if v == "":
                continue
            grid[(r, c)] = to_number(v)
    return grid


def to_number(v):
    t = v.strip()
    try:
        if t and (t[0].isdigit() or t[0] in "-+") and "/" not in t:
            return float(t.replace(".", "").replace(",", ".")) if "," in t else float(t)
    except ValueError:
        pass
    return v


# ---------------------------------------------------------------------- conferência: definição das contas
# Cada conta: (chave, rótulo, coluna do motor na aba Dados, fórmula, formato)
# Na fórmula: {P:x} parâmetro; {I:x} entrada do dia (aba Dados); {C:x} valor da planilha nesta linha;
# {Cp:x} valor da planilha no dia anterior; {H:x} auxiliar nesta linha; {Hp:x} auxiliar no dia anterior;
# {DAY} célula do dia; {EQ:col} faixa da tabela de equipamentos (aba Dados).
CHECKS = [
    ("rep_antes", "Reputação antes da operação", "m_reputacao_antes", "IF({DAY}=1,{P:reputacao_inicio},{Cp:rep_fim})", "0.0000"),
    ("sat_antes", "Satisfação antes da operação", "m_satisfacao_antes", "IF({DAY}=1,{P:satisfacao_inicio},{Cp:sat_fim})", "0.0000"),
    ("mem_dem", "Memória de demanda", "m_memoria_demanda",
     "IF({DAY}=1,{P:memoria_demanda_inicio},IF({Cp:mem_dem}<=0,{Cp:demanda},0.7*{Cp:mem_dem}+0.3*{Cp:demanda}))", "0.0000"),
    ("cap_rec", "Capacidade recente", "m_capacidade_recente", "IF({DAY}=1,{P:capacidade_recente_inicio},{Hp:cap_max})", "0.0000"),
    ("est_antes", "Estoque antes da compra", "m_estoque_antes", "IF({DAY}=1,{P:estoque_inicio},{Cp:est_fim})-{I:perda_falha_equipamento}", "#,##0.00"),
    ("q_equip", "Qualidade dos equipamentos", "m_qualidade_equip", "IFERROR(AVERAGE({EQ:N}),0)", "0.0000"),
    ("qualidade", "Qualidade", "m_qualidade", "MIN(1,MAX(0,0.5+{C:q_equip}+{I:qualidade_politicas}))", "0.0000"),
    ("encaixe", "Encaixe de mercado", "m_encaixe",
     "({H:w_C}*({H:acc_C1}+{H:acc_C2}+{H:acc_C3})/3+{H:w_B}*({H:acc_B1}+{H:acc_B2}+{H:acc_B3})/3+{H:w_A}*({H:acc_A1}+{H:acc_A2}+{H:acc_A3})/3)/{P:encaixe_referencia}", "0.0000"),
    ("ticket", "Ticket médio", "m_ticket", "{H:share_1}*{P:preco_1}+{H:share_2}*{P:preco_2}+{H:share_3}*{P:preco_3}", "#,##0.00"),
    ("f_conc", "Fator concorrência", "m_f_concorrencia", "SQRT({H:attr}/{I:pressao_concorrencia})", "0.0000"),
    ("f_rep", "Fator reputação", "m_f_reputacao", "0.6+0.8*MIN(100,MAX(0,{C:rep_antes}))/100", "0.0000"),
    ("f_fid", "Fator fidelização", "m_f_fidelizacao", "1+{I:forca_fidelizacao}*({C:sat_antes}-50)/100", "0.0000"),
    ("f_mkt", "Fator marketing", "m_f_marketing", "1+{I:ganho_max_marketing}*{I:lembranca_marca}", "0.0000"),
    ("f_cal", "Fator calendário", "m_f_calendario", "INDEX({PROFILE},MOD({DAY}-1,7)+1)*{P:sazonalidade_mes}*{P:indice_regional}", "0.0000"),
    ("demanda", "Demanda potencial", "m_demanda",
     "{P:demanda_base}*{C:encaixe}*{C:f_conc}*{C:f_rep}*{C:f_fid}*{C:f_mkt}*{C:f_cal}*{I:fator_eventos}*{I:ruido}", "0.0000"),
    ("produt", "Produtividade", "m_produtividade",
     "0.85+0.3*MIN(100,MAX(0,{I:moral}))/100+{I:experiencia}+IF({I:tem_gerente}=1,{P:gerente_produtividade},0)+{I:treinamento_prod}", "0.0000"),
    ("cap_at", "Capacidade do atendimento", "m_cap_atendimento",
     "{I:atendentes_presentes}*{P:cap_atendente}*{C:produt}*{I:disp_atendimento}*{I:fator_cap_atendimento}", "0.0000"),
    ("cap_coz", "Capacidade da cozinha", "m_cap_cozinha",
     "({I:especialistas_presentes}*{P:cap_especialista}*{C:produt}+{I:bonus_cozinha})*{I:disp_cozinha}*{I:fator_cap_cozinha}", "0.0000"),
    ("cap_sal", "Capacidade do salão", "m_cap_salao", "({P:capacidade_fisica}+{I:bonus_salao})*{I:disp_salao}", "0.0000"),
    ("custo_unit", "Custo unitário do estoque", "m_custo_unit_estoque",
     "{H:unit_cost}*{I:fator_custo_insumos}*IF({I:parcelado}=1,1,1-{P:desconto_a_vista})", "#,##0.0000"),
    ("compra", "Compra do dia (tabela)", "m_compra",
     "MAX(0,{I:dias_cobertura}*{H:esperada}*{C:custo_unit}-{C:est_antes})/IF({I:parcelado}=1,1,1-{P:desconto_a_vista})*{I:fator_entrega}", "#,##0.00"),
    ("entrou", "Estoque que entrou", "m_estoque_entrou", "IF({I:parcelado}=1,{C:compra},{C:compra}*(1-{P:desconto_a_vista}))", "#,##0.00"),
    ("atendidos", "Pedidos atendidos", "m_atendidos", "MIN({H:apos_cap},MAX(0,{H:estoque_pedidos}))", "0.0000"),
    ("perd_cap", "Perdidos por capacidade", "m_perdidos_capacidade", "{C:demanda}-{H:apos_cap}", "0.0000"),
    ("perd_est", "Perdidos por estoque", "m_perdidos_estoque", "{H:apos_cap}-{C:atendidos}", "0.0000"),
    ("receita", "Receita do dia", "m_receita", "{C:atendidos}*{C:ticket}", "#,##0.00"),
    ("cmv", "CMV das vendas", "m_cmv_vendas", "MIN({C:atendidos}*{C:custo_unit},{H:estoque_apos_compra})", "#,##0.00"),
    ("perda_val", "Perda por validade", "m_perda_validade",
     "MIN({H:apos_venda},{H:apos_venda}*{P:perecibilidade}*IF({I:refrigeracao_parada}=1,4,1))", "#,##0.00"),
    ("est_fim", "Estoque no fim do dia", "m_estoque_fim", "{H:apos_venda}-{C:perda_val}", "#,##0.00"),
    ("ocup", "Ocupação", "m_ocupacao", "IF({H:cap_max}>0,{C:demanda}/{H:cap_max},2)", "0.0000"),
    ("tempo", "Fator de tempo", "m_fator_tempo",
     "IF({C:ocup}<=0.75,1,IF({C:ocup}<=1,1+({C:ocup}-0.75)/0.25*0.8,IF({C:ocup}<=1.25,1.8+({C:ocup}-1)/0.25*0.8,2.6)))", "0.0000"),
    ("erros", "Taxa de erros", "m_taxa_erros", "0.04*{I:fator_erros_treinamento}+0.12*MAX(0,MIN({C:ocup},1.25)-1)", "0.0000"),
    ("nps", "NPS do dia", "m_nps",
     "MIN(100,MAX(-100,50+IF({C:tempo}<=1,10,-15*MIN(1,({C:tempo}-1)/1.6))+IF({H:ruptura}<=0,5,-15*MIN(1,{H:ruptura}/0.2))"
     "+IF({H:nivel_preco}<=1,5,IF({C:qualidade}<0.55,-10*MIN(1,({H:nivel_preco}-1)/0.2),0))+MIN(10,MAX(-10,20*({C:qualidade}-0.5)))"
     "-120*{C:erros}-IF({I:evento_negativo_aberto}=1,10,0)+IF({I:bonus_premium_gerente}=1,5,0)))", "0.0000"),
    ("rep_fim", "Reputação no fim do dia", "m_reputacao_fim", "{C:rep_antes}+{P:atraso_reputacao}*({H:satisfacao_hoje}-{C:rep_antes})", "0.0000"),
    ("sat_fim", "Satisfação acumulada", "m_satisfacao_fim", "{C:sat_antes}+{P:memoria_satisfacao}*({H:satisfacao_hoje}-{C:sat_antes})", "0.0000"),
    ("caixa_aj", "Caixa antes dos juros", "m_caixa_antes_juros",
     "{I:caixa_antes_operacao}+{C:receita}-IF({I:parcelado}=1,0,{C:entrou})-{P:fidelizacao_por_pedido}*{C:atendidos}", "#,##0.00"),
    ("juros", "Juros do cheque especial", "m_juros_cheque", "MAX(0,-{C:caixa_aj})*{P:juros_cheque_mes}/{P:dias_mes}", "#,##0.00"),
    ("caixa_fim", "Caixa no fim do dia", "m_caixa_fim", "{C:caixa_aj}-{C:juros}", "#,##0.00"),
]

HELPERS = [("pdiv", "1+0.6*({C:qualidade}-0.5)")]
for s in ("C", "B", "A"):
    for i in (1, 2, 3):
        HELPERS.append((f"acc_{s}{i}", f"1/(1+POWER({{P:preco_{i}}}/{{H:pdiv}}/({{P:T_{s}}}*{{P:fator_preco_regiao}}),3*{{P:sens_{s}}}))"))
for s in ("C", "B", "A"):
    HELPERS.append((f"w_{s}", f"({{P:mix_{s}}}+{{I:alcance_marketing}}*{{P:cidade_{s}}}*{{P:fluxo_cidade}})*{{P:atrat_{s}}}*{{P:ajuste_{s}}}"))
for i in (1, 2, 3):
    HELPERS.append((f"pw_{i}", f"{{H:w_C}}*{{H:acc_C{i}}}+{{H:w_B}}*{{H:acc_B{i}}}+{{H:w_A}}*{{H:acc_A{i}}}"))
for i in (1, 2, 3):
    HELPERS.append((f"share_{i}", f"IF(({{H:pw_1}}+{{H:pw_2}}+{{H:pw_3}})>0,{{H:pw_{i}}}/({{H:pw_1}}+{{H:pw_2}}+{{H:pw_3}}),1/3)"))
HELPERS += [
    ("attr", "{P:mix_C}*{P:atrat_C}*{P:ajuste_C}+{P:mix_B}*{P:atrat_B}*{P:ajuste_B}+{P:mix_A}*{P:atrat_A}*{P:ajuste_A}"),
    ("ref_ticket", "{H:share_1}*{P:preco_ref_1}+{H:share_2}*{P:preco_ref_2}+{H:share_3}*{P:preco_ref_3}"),
    ("unit_cost", "{H:share_1}*{P:custo_1}+{H:share_2}*{P:custo_2}+{H:share_3}*{P:custo_3}"),
    ("cap_max", "MIN({C:cap_at},{C:cap_coz},{C:cap_sal})"),
    ("esperada", "MIN({C:mem_dem},IF({C:cap_rec}>0,{C:cap_rec},{H:cap_max}))"),
    ("estoque_apos_compra", "{C:est_antes}+{C:entrou}"),
    ("estoque_pedidos", "{H:estoque_apos_compra}/{C:custo_unit}"),
    ("apos_cap", "MIN({C:demanda},{H:cap_max})"),
    ("apos_venda", "{H:estoque_apos_compra}-{C:cmv}"),
    ("ruptura", "IF({C:demanda}>0,{C:perd_est}/{C:demanda},0)"),
    ("nivel_preco", "IF({H:ref_ticket}>0,{C:ticket}/{H:ref_ticket},1)"),
    ("satisfacao_hoje", "MIN(100,MAX(0,{C:nps}))"),
]
HELPER_LABELS = {
    "pdiv": "1 + 0,6·(qualidade − 0,5)", "attr": "Σ mix·atrat·ajuste", "ref_ticket": "ticket de referência", "unit_cost": "custo médio do mix",
    "cap_max": "capacidade máxima (menor frente)", "esperada": "demanda esperada para a compra", "estoque_apos_compra": "estoque após a compra",
    "estoque_pedidos": "estoque em pedidos", "apos_cap": "pedidos após o corte de capacidade", "apos_venda": "estoque após a venda",
    "ruptura": "perdidos por estoque / demanda", "nivel_preco": "ticket / ticket de referência", "satisfacao_hoje": "NPS limitado a 0..100",
}


def build(csv_folder, out_path):
    files = sorted(glob.glob(os.path.join(csv_folder, "C*_*.csv"))) + sorted(glob.glob(os.path.join(csv_folder, "P*_*.csv")))
    wb = Workbook()
    how = wb.active
    how.title = "Como usar"
    summary = wb.create_sheet("Resumo")
    scenarios = []
    for path in files:
        grid = read_csv(path)
        n = os.path.basename(path).split("_")[0]            # C1..C4 = cenários; P1 = partida jogada no Play
        name = str(grid.get((1, 5), os.path.basename(path)))
        dados = wb.create_sheet(f"{n} Dados")
        conf = wb.create_sheet(f"{n} Conferência")
        months = sorted({int(grid[(r, 2)]) for (r, c) in grid if c == 1 and grid[(r, c)] == "MÊS"})
        write_dados(dados, grid)
        info = write_conferencia(conf, dados.title, grid, months)
        scenarios.append((n, name, conf.title, dados.title, info))
    write_summary(summary, scenarios)
    write_how(how, scenarios)
    wb.save(out_path)


def write_dados(ws, grid):
    for (r, c), v in grid.items():
        cell = ws.cell(row=r, column=c, value=v)
        cell.font = BLUE if isinstance(v, float) else BLACK
    for col, w in {1: 26, 2: 16, 3: 34, 5: 22, 6: 44, 8: 30, 16: 14}.items():
        ws.column_dimensions[L(col)].width = w
    for c in range(9, 15):
        ws.column_dimensions[L(c)].width = 14
    for m in range(3):
        r0 = 1 + BLOCK * m
        if (r0, 1) not in grid:
            continue
        ws.cell(row=r0, column=1).font = TITLE
        for c in range(1, 80):
            cell = ws.cell(row=r0 + DAY_HDR, column=c)
            if cell.value is not None:
                cell.font = HEAD_FONT
                cell.fill = HEAD_FILL
    ws.freeze_panes = "B2"


def write_conferencia(ws, dados, grid, months):
    q = f"'{dados}'"
    # mapas do layout a partir do mês 1
    params = {grid[(r, 1)]: r - 1 for (r, c) in grid if c == 1 and 1 < r < 1 + 96 and isinstance(grid[(r, 1)], str)}
    daycols = {grid[(1 + DAY_HDR, c)]: c for (r, c) in grid if r == 1 + DAY_HDR}
    month_keys = {grid[(r, 1)]: r - 1 for (r, c) in grid if c == 1 and 1 + MONTH_VALS <= r < 1 + BLOCK}

    check_col = {}
    col = 3
    for key, *_ in CHECKS:
        check_col[key] = col          # motor = col, planilha = col + 1, diferença = col + 2
        col += 3
    helper_start = col + 1
    helper_col = {key: helper_start + i for i, (key, _) in enumerate(HELPERS)}

    ws["A1"] = "Conferência das fórmulas do motor"
    ws["A1"].font = TITLE
    ws["G1"] = "Tolerância"
    ws["G1"].font = BOLD
    ws["H1"] = "=Resumo!$C$4"
    ws["H1"].font = GREEN
    tol = "$H$1"

    info = []
    for m in months:
        r0 = 1 + BLOCK * (m - 1)
        top = CONF_TOP + CONF_BLOCK * (m - 1)
        flag = f"$B${top}"
        first, last = top + 3, top + 32

        def P(key):
            return f"{q}!$B${r0 + params[key]}"

        def I(key, row):
            return f"{q}!${L(daycols[key])}${r0 + DAY_HDR + (row - first + 1)}"

        def expand(template, row):
            out = template
            out = out.replace("{DAY}", f"$A{row}")
            out = out.replace("{PROFILE}", f"{q}!$B${r0 + params['perfil_seg']}:$B${r0 + params['perfil_dom']}")
            for key in params:
                out = out.replace("{P:" + key + "}", P(key))
            for key in daycols:
                out = out.replace("{I:" + key + "}", I(key, row))
            for key, c in check_col.items():
                out = out.replace("{C:" + key + "}", f"{L(c + 1)}{row}").replace("{Cp:" + key + "}", f"{L(c + 1)}{row - 1}")
            for key, c in helper_col.items():
                out = out.replace("{H:" + key + "}", f"{L(c)}{row}").replace("{Hp:" + key + "}", f"{L(c)}{row - 1}")
            out = out.replace("{EQ:N}", f"{q}!$N${r0 + 2}:$N${r0 + 31}")
            assert "{" not in out, out
            return out

        # cabeçalho do bloco
        ws.cell(row=top, column=1, value=f"Mês {m}").font = TITLE
        ws.cell(row=top, column=2, value=f'=IF(ISNUMBER({q}!$B${r0}),1,0)').font = GREEN
        ws.cell(row=top, column=3, value="Contas conferidas").font = BOLD
        ws.cell(row=top, column=5, value="Divergências").font = BOLD
        ws.cell(row=top, column=7, value="Situação").font = BOLD
        ws.cell(row=top + 1, column=1, value="Dia").font = HEAD_FONT
        ws.cell(row=top + 1, column=2, value="Divergências do dia").font = HEAD_FONT
        for c in (1, 2):
            ws.cell(row=top + 1, column=c).fill = HEAD_FILL
            ws.cell(row=top + 2, column=c).fill = HEAD_FILL
        for key, label, motor_key, formula, fmt in CHECKS:
            c = check_col[key]
            ws.merge_cells(start_row=top + 1, start_column=c, end_row=top + 1, end_column=c + 2)
            h = ws.cell(row=top + 1, column=c, value=label)
            h.font, h.fill, h.alignment = HEAD_FONT, HEAD_FILL, Alignment(horizontal="center", wrap_text=True)
            for j, t in enumerate(("Motor", "Planilha", "Diferença")):
                s = ws.cell(row=top + 2, column=c + j, value=t)
                s.font, s.fill, s.alignment = BOLD, SUB_FILL, Alignment(horizontal="center")
        hh = ws.cell(row=top + 1, column=helper_start - 1, value="Cálculos auxiliares →")
        hh.font = BOLD
        for key, _ in HELPERS:
            h = ws.cell(row=top + 1, column=helper_col[key], value=key)
            h.font, h.fill = BOLD, HELP_FILL
            s = ws.cell(row=top + 2, column=helper_col[key], value=HELPER_LABELS.get(key, ""))
            s.font, s.fill, s.alignment = Font(name=FONT, size=8, italic=True), HELP_FILL, Alignment(wrap_text=True)
        ws.row_dimensions[top + 1].height = 30
        ws.row_dimensions[top + 2].height = 26

        # dias
        for day in range(1, 31):
            row = first + day - 1
            ws.cell(row=row, column=1, value=day).font = BOLD
            diffs = []
            for key, label, motor_key, formula, fmt in CHECKS:
                c = check_col[key]
                mc = ws.cell(row=row, column=c, value=f"={I(motor_key, row)}")
                mc.font, mc.number_format = GREEN, fmt
                pc = ws.cell(row=row, column=c + 1, value=f"=IF({flag}=1,{expand(formula, row)},\"\")")
                pc.font, pc.number_format = BLACK, fmt
                dc = ws.cell(row=row, column=c + 2, value=f"=IF({flag}=1,{L(c + 1)}{row}-{L(c)}{row},\"\")")
                dc.font, dc.number_format = BLACK, "0.000000"
                diffs.append(f"(ABS({L(c + 2)}{row})>{tol})")
            for key, formula in HELPERS:
                hc = ws.cell(row=row, column=helper_col[key], value=f"=IF({flag}=1,{expand(formula, row)},\"\")")
                hc.font, hc.number_format, hc.fill = BLACK, "0.000000", HELP_FILL
            ws.cell(row=row, column=2, value=f"=IF({flag}=1,{'+'.join(diffs)},0)").font = BLACK
        # destaque das diferenças
        rng = f"C{first}:{L(check_col[CHECKS[-1][0]] + 2)}{last}"
        ws.conditional_formatting.add(f"B{first}:B{last}", FormulaRule(formula=[f"B{first}>0"], fill=RED_FILL))
        for key, *_ in CHECKS:
            dcol = L(check_col[key] + 2)
            ws.conditional_formatting.add(f"{dcol}{first}:{dcol}{last}",
                                          FormulaRule(formula=[f'AND(ISNUMBER({dcol}{first}),ABS({dcol}{first})>{tol})'], fill=RED_FILL))

        # fechamento do mês
        mt = top + 35
        ws.cell(row=mt, column=1, value="Fechamento do mês").font = TITLE
        for j, t in enumerate(("Conta", "Motor", "Planilha", "Diferença", "Como a planilha calcula")):
            h = ws.cell(row=mt + 1, column=1 + j, value=t)
            h.font, h.fill = HEAD_FONT, HEAD_FILL

        def DR(key):                       # faixa da coluna Planilha de uma conta do dia
            c = check_col[key] + 1
            return f"{L(c)}{first}:{L(c)}{last}"

        def DI(key):                       # faixa de uma entrada do dia na aba Dados
            return f"{q}!${L(daycols[key])}${r0 + DAY_HDR + 1}:${L(daycols[key])}${r0 + DAY_HDR + 30}"

        eq = lambda col: f"{q}!${col}${r0 + 2}:${col}${r0 + 31}"
        staff = lambda col: f"{q}!${col}${r0 + 2}:${col}${r0 + 7}"
        items = []

        def item(key, label, motor_key, formula, explain, fmt="#,##0.00", text=False):
            items.append((key, label, motor_key, formula, explain, fmt, text))

        R = {}
        item("receita", "DRE · Receita bruta", "dre_GrossRevenue", f"SUM({DR('receita')})", "soma da receita dos 30 dias")
        item("impostos", "DRE · Impostos", "dre_Taxes", "{receita}*" + P("imposto"), "receita × 8%")
        item("cmv", "DRE · CMV (vendas + perdas)", "dre_Cmv", f"SUM({DR('cmv')})+SUM({DR('perda_val')})+SUM({DI('perda_falha_equipamento')})",
             "CMV das vendas + perda por validade + perda nas falhas")
        item("folha", "DRE · Folha e encargos", "dre_Payroll", f"SUMPRODUCT({staff('Q')},{staff('R')})*{P('encargos')}*{P('fator_folha')}",
             "Σ pessoas × salário no dia 27 × 1,35 × fator da remuneração")
        item("aluguel", "DRE · Aluguel", "dre_Rent", P("aluguel"), "aluguel da região")
        item("marketing", "DRE · Marketing e fidelização", "dre_Marketing",
             f"{P('marketing_fixo')}+{P('fidelizacao_fixo')}+{P('fidelizacao_por_pedido')}*SUM({DR('atendidos')})", "custo fixo das políticas + R$ por pedido")
        item("utilidades", "DRE · Energia, água e taxas", "dre_Utilities",
             f"{P('energia_fixa')}+SUMPRODUCT({eq('J')},{eq('K')})*{P('energia_kwh')}+{P('agua_fixa')}+{P('limpeza_fixa')}+{P('taxas_adm')}"
             f"+SUM({DR('atendidos')})*({P('energia_pedido')}+{P('agua_pedido')}+{P('limpeza_pedido')})", "fixos + kWh dos equipamentos + R$ por pedido")
        item("manutencao", "DRE · Manutenção e reparos", "dre_MaintenanceAndLosses", f"{P('manutencao_taxa')}*SUM({eq('I')})+{P('reacoes_reparo')}",
             "taxa da política × valor de compra + reparos")
        item("outras", "DRE · Outras despesas", "dre_OtherExpenses", f"{P('treinamento_fixo')}+{P('reacoes_outras')}", "treinamento + reações a eventos")
        item("outras_rec", "DRE · Outras receitas", "dre_OtherIncome", P("reacoes_ganho"), "reações com ganho")
        item("depreciacao", "DRE · Depreciação", "dre_Depreciation", "SUM({DEP})", "Σ mínimo(valor contábil, preço ÷ vida útil)")
        item("juros", "DRE · Juros", "dre_Interest", f"{P('juros_emprestimo')}+SUM({DR('juros')})+{P('reacoes_juros')}",
             "parcela do empréstimo + cheque especial + tarifa")
        item("lucro", "DRE · Lucro líquido", "lucro_liquido",
             "{receita}+{outras_rec}-{impostos}-{cmv}-{folha}-{aluguel}-{marketing}-{utilidades}-{manutencao}-{outras}-{depreciacao}-{juros}",
             "receitas − despesas")
        item("c_vendas", "Caixa · Vendas", "caixa_Sales", "{receita}", "recebidas no dia")
        item("c_outras", "Caixa · Outras entradas", "caixa_OtherInflows", "{outras_rec}", "")
        item("c_forn", "Caixa · Fornecedores", "caixa_Suppliers",
             f"-(SUMPRODUCT(1-{DI('parcelado')},{DR('entrou')})+{P('parcelas_fornecedor_pagas')})", "compras à vista + parcelas vencidas")
        item("c_sal", "Caixa · Salários", "caixa_Salaries", "-{folha}", "")
        item("c_alug", "Caixa · Aluguel", "caixa_Rent", "-{aluguel}", "")
        item("c_contas", "Caixa · Contas de consumo", "caixa_Bills", "-{utilidades}", "projeção + acerto no dia 30 = despesa do mês")
        item("c_imp", "Caixa · Impostos", "caixa_Taxes", "-{impostos}", "dia 29 + acerto no dia 30")
        item("c_mkt", "Caixa · Marketing, fidelização e treinamento", "caixa_Marketing", f"-({{marketing}}+{P('treinamento_fixo')})", "")
        item("c_man", "Caixa · Manutenção", "caixa_Maintenance", "-{manutencao}", "")
        item("c_ev", "Caixa · Reações a eventos", "caixa_Events", f"-{P('reacoes_outras')}", "")
        item("c_jur", "Caixa · Juros", "caixa_Interest", "-{juros}", "")
        item("c_amort", "Caixa · Amortização", "caixa_Amortization", f"-{P('amortizacao')}", "saldo ÷ meses restantes")
        item("c_res", "Caixa · Resultado do mês", "resultado_caixa",
             "{c_vendas}+{c_outras}+{c_forn}+{c_sal}+{c_alug}+{c_contas}+{c_imp}+{c_mkt}+{c_man}+{c_ev}+{c_jur}+{c_amort}", "soma das linhas")
        item("c_fim", "Caixa no fim do mês", "caixa_fim_mes", f"{P('caixa_inicio')}+{{c_res}}", "caixa do dia 1 + resultado")
        item("b_caixa", "Balanço · Caixa", "balanco_Caixa", "MAX(0,{c_fim})", "")
        item("b_est", "Balanço · Estoque", "balanco_Estoque", f"{L(check_col['est_fim'] + 1)}{last}", "estoque no fim do dia 30")
        item("b_eq", "Balanço · Equipamentos líquidos", "balanco_Equipamentos líquidos", f"SUM({eq('M')})-{{depreciacao}}", "valor contábil − depreciação")
        item("b_cheque", "Balanço · Cheque especial", "balanco_Cheque especial", "MAX(0,-{c_fim})", "")
        item("b_forn", "Balanço · Fornecedores", "balanco_Fornecedores",
             f"{P('fornecedores_inicio')}+SUMPRODUCT({DI('parcelado')},{DR('entrou')})-{P('parcelas_fornecedor_pagas')}", "a pagar")
        item("b_contas", "Balanço · Contas a pagar", "balanco_Contas a pagar", "0", "impostos e contas acertados no dia 30")
        item("b_emp", "Balanço · Empréstimos", "balanco_Empréstimos", f"{P('emprestimo_inicio')}-{P('amortizacao')}", "")
        item("b_cap", "Balanço · Capital", "balanco_Capital", P("capital"), "")
        item("b_lucros", "Balanço · Lucros acumulados", "balanco_Lucros acumulados", f"{P('lucros_acumulados_inicio')}+{{lucro}}", "")
        item("b_dif", "Balanço · Ativo − passivo − patrimônio", "diferenca_balanco",
             "({b_caixa}+{b_est}+{b_eq})-({b_cheque}+{b_forn}+{b_contas}+{b_emp})-({b_cap}+{b_lucros})", "tem que ser zero")
        item("i_ped", "Indicador · Pedidos atendidos", "pedidos", f"SUM({DR('atendidos')})", "", "#,##0.0000")
        item("i_perd", "Indicador · Pedidos perdidos", "perdidos_fracao",
             f"IF(SUM({DR('demanda')})>0,(SUM({DR('perd_cap')})+SUM({DR('perd_est')}))/SUM({DR('demanda')}),0)", "perdidos ÷ demanda", "0.0000%")
        item("i_nps", "Indicador · NPS médio", "nps_medio", f"SUM({DR('nps')})/{P('dias_mes')}", "", "0.0000")
        item("i_cmv", "Indicador · CMV / receita", "cmv_receita", "IF({receita}>0,{cmv}/{receita},0)", "", "0.0000%")
        item("i_ticket", "Indicador · Ticket médio", "ticket_medio", "IF({i_ped}>0,{receita}/{i_ped},0)", "")
        item("i_liq", "Indicador · Liquidez corrente", "liquidez",
             "IF(({b_cheque}+{b_forn}+{b_contas}+{b_emp})>0,({b_caixa}+{b_est})/({b_cheque}+{b_forn}+{b_contas}+{b_emp}),99)", "(caixa + estoque) ÷ passivo", "0.0000")
        item("score", "Pontuação do mês", "pontuacao",
             "IF({lucro}>0,20,0)+IF({c_res}>0,20,0)+IF({i_nps}>70,15,0)+IF(AND({i_cmv}>=" + P("cmv_min") + ",{i_cmv}<=" + P("cmv_max") + "),15,0)"
             "+IF({i_liq}>1.2,10,0)+IF({i_perd}<0.05,10,0)+IF(" + P("conta_atrasada") + "=0,10,0)-IF({lucro}<0,20,0)-IF({c_fim}<0,25,0)-IF({i_nps}<30,15,0)",
             "regras do Bloco 10", "0")
        item("classe", "Classificação", "classificacao",
             'IF({score}>=80,"Gestão excelente",IF({score}>=60,"Adequada com pontos de atenção",IF({score}>=40,"Instável",IF({score}>=0,"Em risco","Crise financeira e operacional"))))',
             "faixas da pontuação", "@", True)

        # tabela auxiliar de depreciação por equipamento (colunas G..I do fechamento)
        ws.cell(row=mt + 1, column=7, value="Equipamento").font = HEAD_FONT
        ws.cell(row=mt + 1, column=8, value="Depreciação do mês").font = HEAD_FONT
        ws.cell(row=mt + 1, column=7).fill = HEAD_FILL
        ws.cell(row=mt + 1, column=8).fill = HEAD_FILL
        for i in range(30):
            dr = r0 + 2 + i
            ws.cell(row=mt + 2 + i, column=7, value=f'=IF({q}!$H${dr}="","",{q}!$H${dr})').font = GREEN
            dep = ws.cell(row=mt + 2 + i, column=8, value=f'=IF({q}!$L${dr}="",0,MIN({q}!$M${dr},{q}!$I${dr}/{q}!$L${dr}))')
            dep.font, dep.number_format = BLACK, "#,##0.00"
        dep_range = f"H{mt + 2}:H{mt + 31}"

        for idx, it in enumerate(items):
            R[it[0]] = f"C{mt + 2 + idx}"
        for idx, (key, label, motor_key, formula, explain, fmt, text) in enumerate(items):
            row = mt + 2 + idx
            f = formula.replace("{DEP}", dep_range)
            for k2, ref in R.items():
                f = f.replace("{" + k2 + "}", ref)
            assert "{" not in f, f
            ws.cell(row=row, column=1, value=label).font = BLACK
            mc = ws.cell(row=row, column=2, value=f"={q}!$B${r0 + month_keys[motor_key]}")
            mc.font, mc.number_format = GREEN, fmt
            pc = ws.cell(row=row, column=3, value=f'=IF({flag}=1,{f},"")')
            pc.font, pc.number_format = BLACK, fmt
            if text:
                dif = f'=IF({flag}=1,IF(C{row}=B{row},0,1),"")'
            else:
                dif = f'=IF({flag}=1,C{row}-B{row},"")'
            dc = ws.cell(row=row, column=4, value=dif)
            dc.font, dc.number_format = BLACK, "0.000000"
            ws.cell(row=row, column=5, value=explain).font = Font(name=FONT, size=9, italic=True)
        last_item = mt + 1 + len(items)
        ws.conditional_formatting.add(f"D{mt + 2}:D{last_item}",
                                      FormulaRule(formula=[f'AND(ISNUMBER(D{mt + 2}),ABS(D{mt + 2})>{tol})'], fill=RED_FILL))

        # tela "DRE mensal • resultado de caixa" do layout novo (RoundResultEntity), recalculada do fluxo de caixa
        st = last_item + 2
        sh = ws.cell(row=st, column=1, value="Tela de resultado do mês (layout novo) · modelo de caixa, gravada em float")
        sh.font, sh.fill = HEAD_FONT, HEAD_FILL
        for j in range(2, 6):
            ws.cell(row=st, column=j).fill = HEAD_FILL
        ws.cell(row=st, column=4, value=f"tolerância + {SCREEN_EXTRA}").font = HEAD_FONT
        screen = [
            ("t_receita", "Tela · Receita de vendas", "tela_receita", "{c_vendas}", "vendas recebidas no mês"),
            ("t_insumos", "Tela · Insumos", "tela_insumos", "-{c_forn}", "pago a fornecedores (à vista e parcelas vencidas)"),
            ("t_aluguel", "Tela · Aluguel", "tela_aluguel", "-{c_alug}", ""),
            ("t_folha", "Tela · Folha de pagamento", "tela_folha", "-{c_sal}", ""),
            ("t_desp", "Tela · Despesas operacionais", "tela_despesas_operacionais",
             "-({c_contas}+{c_imp}+{c_mkt}+{c_man}+{c_jur})-" + P("juros_emprestimo"),
             "contas + impostos + marketing/fidelização/treinamento + manutenção + juros do cheque e tarifas"),
            ("t_13", "Tela · 13º salário", "tela_13", "0", "o motor não tem 13º no trimestre"),
            ("t_parcela", "Tela · Parcela de empréstimo", "tela_parcela", P("juros_emprestimo") + "+" + P("amortizacao"), "juros + amortização pagos"),
            ("t_principal", "Tela · Principal da parcela", "tela_principal", P("amortizacao"), ""),
            ("t_saidas", "Tela · Saídas do mês", "tela_saidas", "{t_insumos}+{t_aluguel}+{t_folha}+{t_desp}+{t_13}+{t_parcela}", "soma das saídas"),
            ("t_eventos", "Tela · Impacto dos eventos", "tela_eventos", "{c_ev}+{c_outras}", "reações (saída) e ganhos (entrada)"),
            ("t_resultado", "Tela · Resultado do mês", "tela_resultado", "{t_receita}-{t_saidas}+{t_eventos}", "receita − saídas + eventos"),
            ("t_inicio", "Tela · Caixa no início do mês", "tela_caixa_inicio", P("caixa_inicio"), ""),
            ("t_fim", "Tela · Caixa no fechamento", "tela_caixa_fim", "{t_inicio}+{t_resultado}", "início + resultado"),
        ]
        for idx, it in enumerate(screen):
            R[it[0]] = f"C{st + 1 + idx}"
        for idx, (key, label, motor_key, formula, explain) in enumerate(screen):
            row = st + 1 + idx
            f = formula
            for k2, ref in sorted(R.items(), key=lambda kv: -len(kv[0])):
                f = f.replace("{" + k2 + "}", ref)
            assert "{" not in f, f
            ws.cell(row=row, column=1, value=label).font = BLACK
            mc = ws.cell(row=row, column=2, value=f"={q}!$B${r0 + month_keys[motor_key]}")
            mc.font, mc.number_format = GREEN, "#,##0.00"
            pc = ws.cell(row=row, column=3, value=f'=IF({flag}=1,{f},"")')
            pc.font, pc.number_format = BLACK, "#,##0.00"
            dc = ws.cell(row=row, column=4, value=f'=IF({flag}=1,C{row}-B{row},"")')
            dc.font, dc.number_format = BLACK, "0.000000"
            ws.cell(row=row, column=5, value=explain).font = Font(name=FONT, size=9, italic=True)
        last_screen = st + len(screen)
        stol = f"({tol}+{SCREEN_EXTRA})"
        ws.conditional_formatting.add(f"D{st + 1}:D{last_screen}",
                                      FormulaRule(formula=[f'AND(ISNUMBER(D{st + 1}),ABS(D{st + 1})>{stol})'], fill=RED_FILL))

        checks_count = f'IF({flag}=1,{len(CHECKS) * 30 + len(items) + len(screen)},0)'
        div_count = (f'SUM(B{first}:B{last})+COUNTIF(D{mt + 2}:D{last_item},">"&{tol})+COUNTIF(D{mt + 2}:D{last_item},"<"&-{tol})'
                     f'+COUNTIF(D{st + 1}:D{last_screen},">"&{stol})+COUNTIF(D{st + 1}:D{last_screen},"<"&-{stol})')
        ws.cell(row=top, column=4, value=f"={checks_count}").font = BLACK
        ws.cell(row=top, column=6, value=f"={div_count}").font = BLACK
        ws.cell(row=top, column=8, value=f'=IF({flag}=0,"sem dados",IF(F{top}=0,"OK","DIVERGE"))').font = BOLD
        ws.conditional_formatting.add(f"H{top}", FormulaRule(formula=[f'H{top}="OK"'], fill=GREEN_FILL))
        ws.conditional_formatting.add(f"H{top}", FormulaRule(formula=[f'H{top}="DIVERGE"'], fill=RED_FILL))
        info.append((m, top, f"{q}!$B${r0 + month_keys['lucro_liquido']}", f"{q}!$B${r0 + month_keys['pontuacao']}"))

    ws.column_dimensions["A"].width = 44
    ws.column_dimensions["B"].width = 14
    for c in range(3, helper_start + len(HELPERS) + 1):
        ws.column_dimensions[L(c)].width = 13
    ws.column_dimensions["E"].width = 16
    ws.freeze_panes = ws.cell(row=CONF_TOP + 3, column=3)
    return info


def write_summary(ws, scenarios):
    ws["A1"] = "Resumo da conferência do motor"
    ws["A1"].font = TITLE
    ws["A2"] = "Cada linha soma as contas da aba de conferência do cenário. OK = nenhuma diferença acima da tolerância."
    ws["A2"].font = Font(name=FONT, size=10, italic=True)
    ws["A4"] = "Tolerância (R$ ou unidade)"
    ws["A4"].font = BOLD
    ws["C4"] = 0.005
    ws["C4"].font = BLUE
    ws["C4"].fill = PatternFill("solid", fgColor="FFFF00")
    ws["D4"] = "diferença máxima aceita entre motor e planilha (célula editável)"
    ws["D4"].font = Font(name=FONT, size=9, italic=True)
    head = ["Cenário", "Nome", "Mês", "Contas conferidas", "Divergências", "Situação", "Lucro líquido (motor)", "Pontuação (motor)", "Aba"]
    for j, t in enumerate(head):
        c = ws.cell(row=6, column=1 + j, value=t)
        c.font, c.fill = HEAD_FONT, HEAD_FILL
    row = 7
    for n, name, conf, dados, info in scenarios:
        for m, top, lucro, score in info:
            ws.cell(row=row, column=1, value=n).font = BLACK
            ws.cell(row=row, column=2, value=name).font = BLACK
            ws.cell(row=row, column=3, value=m).font = BLACK
            ws.cell(row=row, column=4, value=f"='{conf}'!D{top}").font = GREEN
            ws.cell(row=row, column=5, value=f"='{conf}'!F{top}").font = GREEN
            ws.cell(row=row, column=6, value=f"='{conf}'!H{top}").font = GREEN
            lc = ws.cell(row=row, column=7, value=f"={lucro}")
            lc.font, lc.number_format = GREEN, "#,##0.00;(#,##0.00);-"
            ws.cell(row=row, column=8, value=f"={score}").font = GREEN
            ws.cell(row=row, column=9, value=conf).font = BLACK
            row += 1
    ws.cell(row=row, column=1, value="Total").font = BOLD
    ws.cell(row=row, column=4, value=f"=SUM(D7:D{row - 1})").font = BOLD
    ws.cell(row=row, column=5, value=f"=SUM(E7:E{row - 1})").font = BOLD
    ws.cell(row=row, column=6, value=f'=IF(E{row}=0,"OK","DIVERGE")').font = BOLD
    ws.conditional_formatting.add(f"F7:F{row}", FormulaRule(formula=['F7="OK"'], fill=GREEN_FILL))
    ws.conditional_formatting.add(f"F7:F{row}", FormulaRule(formula=['F7="DIVERGE"'], fill=RED_FILL))
    for col, w in {"A": 26, "B": 34, "C": 6, "D": 18, "E": 14, "F": 12, "G": 20, "H": 18, "I": 26}.items():
        ws.column_dimensions[col].width = w


def write_how(ws, scenarios):
    lines = [
        ("Conferência do motor do jogo", TITLE),
        ("", None),
        ("O que é", BOLD),
        ("Cada cenário é um conjunto de decisões (D1 a D6 e as 8 políticas do mês) jogado pelo motor durante 3 meses.", None),
        ("A aba \"Cn Dados\" traz o que o motor registrou: as entradas de cada fórmula e os números que ele calculou.", None),
        ("A aba \"Cn Conferência\" recalcula cada número com as fórmulas do GDD e mostra Motor | Planilha | Diferença.", None),
        ("A aba Resumo soma as divergências. OK = todas as diferenças abaixo da tolerância.", None),
        ("", None),
        ("Cores", BOLD),
        ("Azul: valores vindos do motor (aba Dados).  Verde: referência a outra aba.  Preto: fórmula da planilha.", None),
        ("Vermelho no fundo: diferença acima da tolerância.", None),
        ("", None),
        ("Como conferir outras decisões", BOLD),
        ("1. No projeto, edite Conferencia/cenarios.json (região, restaurante, público, preços, equipe, equipamentos, crédito, políticas, semente).", None),
        ("2. No Unity: Game > Simulação > Exportar conferência (CSV para a planilha). A pasta Conferencia abre com um CSV por cenário.", None),
        ("3. Abra o CSV no Excel (ponto e vírgula, vírgula decimal), selecione tudo (Ctrl+A), copie e cole na célula A1 da aba \"Cn Dados\" correspondente.", None),
        ("4. A aba de conferência e o Resumo recalculam sozinhos. Nada nas abas de conferência precisa ser editado.", None),
        ("", None),
        ("Como conferir uma partida jogada no Play (aba P1)", BOLD),
        ("1. Jogue um ou mais meses no Play.  2. No Unity: Game > Simulação > Conferir partida jogada (game.db) e exportar CSV.", None),
        ("3. O menu reproduz os meses com as mesmas decisões e reações, confere tudo e compara com o que ficou gravado.", None),
        ("4. Cole o arquivo P1_Partida_jogada_no_Play.csv na aba \"P1 Dados\", como nos cenários.", None),
        ("", None),
        ("O que é entrada e o que é conferido", BOLD),
        ("Entradas (não recalculadas): sorteios do dia (ruído da demanda, eventos), equipe presente após faltas, moral, efeitos de eventos ativos,", None),
        ("disponibilidade dos equipamentos (falhas), caixa antes da operação e custos das reações escolhidas.", None),
        ("Conferidas: encaixe de mercado, os 8 fatores da demanda, demanda, produtividade, as 3 frentes de capacidade, compra e estoque,", None),
        ("pedidos atendidos e perdidos, receita, CMV, perdas, ocupação, tempo, erros, NPS, reputação, satisfação, juros e caixa do dia,", None),
        ("e no fechamento: DRE, fluxo de caixa, balanço (diferença zero), indicadores, pontuação e classificação.", None),
        ("Também a tela \"DRE mensal • resultado de caixa\" do layout novo: cada linha que o jogador vê, recalculada do fluxo de caixa.", None),
        ("Essa tela grava valores em float (precisão de centavos), por isso as linhas \"Tela\" aceitam 2 centavos a mais de diferença.", None),
        ("As cadeias entre dias (reputação, satisfação, memória de demanda, estoque) usam os valores da própria planilha, não os do motor.", None),
        ("", None),
        ("Cenários desta planilha", BOLD),
    ]
    for n, name, conf, dados, info in scenarios:
        lines.append((f"{n}: {name}  ({len(info)} {'mês' if len(info) == 1 else 'meses'})", None))
    for i, (text, font) in enumerate(lines, start=1):
        c = ws.cell(row=i, column=1, value=text)
        c.font = font or Font(name=FONT, size=10)
    ws.column_dimensions["A"].width = 140


if __name__ == "__main__":
    build(sys.argv[1], sys.argv[2])
    print("ok", sys.argv[2])
