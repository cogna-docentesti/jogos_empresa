"""Regenerate the feedback batch documents from their canonical JSON backlog."""
from __future__ import annotations

import argparse
from collections import Counter
from html import escape
import json
from pathlib import Path
import re


def numbered(items):
    return "\n".join(f"{i}. {value}" for i, value in enumerate(items, 1))


def bullets(items):
    return "<ul>" + "".join(f"<li>{escape(str(x))}</li>" for x in items) + "</ul>"


def links(ids):
    return " · ".join(f'<a href="#{escape(i)}">{escape(i)}</a>' for i in ids) or "Nenhuma"


def render(root):
    relative = Path("docs/ui-update/feedback-2026-11-19")
    base = root / relative
    data = json.loads((base / "tasks.json").read_text(encoding="utf-8"))
    tasks = data["tasks"]
    statuses = Counter(t["status"] for t in tasks)
    completed = sum(t["status"] == "concluída" for t in tasks)
    active = [t for t in tasks if t["status"] != "adiada"]
    by_id = {t["id"]: t for t in tasks}
    order = data["execution_order"]
    calendar = data.get("calendar_requirements", [])
    prefix = relative.as_posix()

    def write(path, content):
        target = base / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content.rstrip() + "\n", encoding="utf-8")

    for t in tasks:
        cfg = t["execution"]
        header = (
            f'{t["id"]} — {t["title"]}\n'
            f'Status: {t["status"]} | Prioridade: {t["priority"]} | Tipo: {t["kind"]}\n'
            f'Área: {t["area"]}\n'
            f'Dependências: {", ".join(t["depends_on"]) or "nenhuma"}\n'
            f'Modelo: {cfg["model"]} | Raciocínio: {cfg["thinking"]}\n'
            f'Conversa: {cfg["chat_title"]}\n'
            f'Escolha do modelo: {cfg["rationale"]}\n'
            f'Commit local em inglês: {cfg["commit_message"]}\n'
            f'Próxima tarefa: {cfg["next_task_id"] or "nenhuma"}\n'
            f'Feedbacks: {", ".join(t["feedback_ids"]) or "validação transversal"}\n'
            f'Esclarecimentos do calendário: {", ".join(t.get("calendar_requirement_ids", [])) or "não se aplica"}\n'
        )
        sections = [
            ("OBJETIVO", t["objective"]),
            ("LEITURAS COMUNS", "\n".join([
                f"{prefix}/COMECE-AQUI.txt", f"{prefix}/EXECUCAO.txt",
                f"{prefix}/ANALISE.txt", f"{prefix}/tasks.json",
                "docs/ui-update/visual-contract.json", "docs/art/manifest.json"])),
            ("REFERÊNCIAS ESPECÍFICAS", "\n".join(t["references"])),
            ("ESCOPO", numbered(t["scope"])),
            ("CRITÉRIOS DE ACEITE", numbered(t["acceptance_criteria"])),
            ("VALIDAÇÃO PREVISTA", numbered(t["validation"])),
        ]
        if t["notes"]:
            sections.append(("OBSERVAÇÕES E LIMITES", numbered(t["notes"])))
        sections.append(("ENTREGÁVEIS", (
            f'Alterações limitadas à tarefa e QA em {t["qa_record"]}.\n'
            f'Evidências reais em {t["evidence_directory"]}/.\n'
            'Atualizar tasks.json e regenerar documentos com:\n'
            'python tools/ui/render_feedback_plan.py\n'
            'Concluir a validação e fazer o commit local antes de iniciar a próxima conversa.')))
        sections.append(("EXECUÇÃO AUTORIZADA", (
            "Tarefa adiada. Não implementar nem criar conversa de venda de usados neste lote."
            if t["status"] == "adiada" else
            f'Execute somente {t["id"]} no projeto D:/Cogna/jogos_empresa. '
            f'Leia esta especificação e {prefix}/EXECUCAO.txt. '
            'A implementação e o commit local estão autorizados. Verifique as dependências, '
            'conclua e valide a tarefa, atualize o backlog e faça commit em inglês. '
            'Depois crie a conversa sucessora conforme o procedimento de EXECUCAO.txt; '
            'não implemente a tarefa seguinte nesta mesma conversa.')))
        write(f'tasks/{t["id"]}.txt', header + "\n" + "\n\n".join(a + "\n" + b for a, b in sections))

    trace = (
        f'RASTREABILIDADE — {len(data["feedback"])} FEEDBACKS ORIGINAIS\n\n' +
        "\n\n".join(f'{f["id"]} | {f["screen"]}\n{f["original_text"]}\nTarefas: {", ".join(f["task_ids"])}' for f in data["feedback"]) +
        "\n\nESCLARECIMENTOS DO CALENDÁRIO — 19/11/2026\n\n" +
        "\n\n".join(f'{c["id"]}\n{c["rule"]}\nTarefas: {", ".join(c["tasks"])}' for c in calendar)
    )
    write("RASTREABILIDADE.txt", trace)
    analysis = (
        f'ANÁLISE — FEEDBACKS E CALENDÁRIO MENSAL\nBase estática inicial: {data["baseline"]["git_head"]}.\n'
        'Planejamento autorizado para execução sequencial pelo usuário em 19/11/2026.\n'
        'Somente tarefas com aceite e evidências reais podem ser marcadas concluídas.\n\n' +
        "\n\n".join(f'{x["finding"]}\nImplicação: {x["implication"]}\nFontes: {"; ".join(x["evidence"])}' for x in data["analysis"]) +
        "\n\nREQUISITOS CONFIRMADOS DO CALENDÁRIO\n" + numbered([c["rule"] for c in calendar]) +
        "\n\nDECISÕES E INTERPRETAÇÕES\n" +
        "\n\n".join(f'{a["id"]} — {a["topic"]}\n{a["proposal"]}\nEstado: {a["status"]}; responsável: {a["owner_task"]}' for a in data["assumptions_and_decisions"]) +
        "\n\nFLUXO\nHub → Calendário com 30 dias visíveis → Avançar ao próximo compromisso → "
        "Conta: debitar e registrar / Evento: abrir decisão → Continuar → Fim do mês → "
        "Fechamento conciliado sem dupla cobrança → DRE aguardando Continuar.\n"
        "Shows e demais eventos previsíveis são anunciados antes. Quebras e demais imprevistos "
        "não têm qualquer marcador antecipado. As regras atuais de incidência permanecem iguais.\n\n"
        "ORDEM\n" + " → ".join(order)
    )
    write("ANALISE.txt", analysis)
    start = (
        'FEEDBACKS DE INTERFACE E CALENDÁRIO — COGNA\nAtualizado em 19/11/2026\n\n'
        'Índice: INDICE.html. Fonte canônica: tasks.json. Execução: EXECUCAO.txt.\n'
        'Análise: ANALISE.txt. Origem: RASTREABILIDADE.txt. Tarefas: tasks/FB-XX.txt.\n\n'
        f'{len(tasks)} tarefas: {len(active)} no lote e {statuses["adiada"]} adiada; {completed} {"concluída" if completed == 1 else "concluídas"}.\n'
        f'{len(data["feedback"])} feedbacks originais e {len(calendar)} esclarecimentos do calendário.\n'
        'O usuário autorizou uma conversa por tarefa, commit local em inglês e continuação sequencial.\n'
        'FB-28 (venda de usados) permanece fora da execução.\n\n'
        'CALENDÁRIO CONFIRMADO\n' + numbered([c["rule"] for c in calendar]) +
        '\n\nREGRAS COMUNS\n' + numbered(data["common_contract"]) +
        '\n\nORDEM\n' + ' → '.join(order) + '\n\nTAREFAS E CONFIGURAÇÕES\n' +
        '\n'.join(f'{t["id"]} | {t["status"]} | {t["execution"]["model"]} / {t["execution"]["thinking"]} | {t["title"]}' for t in tasks)
    )
    write("COMECE-AQUI.txt", start)

    old_html = (base / "INDICE.html").read_text(encoding="utf-8")
    css = re.search(r"<style>(.*?)</style>", old_html, re.S).group(1)
    js = re.search(r"<script>(.*?)</script>", old_html, re.S).group(1)
    cards = []
    for t in tasks:
        cfg = t["execution"]
        body = f'<p class="objective">{escape(t["objective"])}</p><p><strong>Dependências:</strong> {links(t["depends_on"])}</p>'
        body += f'<p><strong>Modelo:</strong> {escape(cfg["model"])} · <strong>Raciocínio:</strong> {escape(cfg["thinking"])}</p><p>{escape(cfg["rationale"])}</p>'
        body += f'<p><strong>Commit:</strong> <code>{escape(cfg["commit_message"])}</code> · <strong>Próxima:</strong> {links([cfg["next_task_id"]] if cfg["next_task_id"] else [])}</p>'
        for heading, field in [("Escopo", "scope"), ("Critérios de aceite", "acceptance_criteria"), ("Validação prevista", "validation"), ("Observações", "notes")]:
            if t[field]:
                body += f'<h3>{heading}</h3>' + bullets(t[field])
        body += '<details class="references"><summary>Arquivos de referência</summary><ul>' + ''.join(f'<li><a href="../../../{escape(r)}">{escape(r)}</a></li>' for r in t["references"]) + '</ul></details>'
        body += f'<p class="task-foot"><a href="tasks/{t["id"]}.txt">Especificação individual</a> · Origem: {links(t["feedback_ids"])} · Calendário: {links(t.get("calendar_requirement_ids", []))}</p>'
        opened = ' open' if t["id"] == data["first_task"] else ''
        badge = 'badge deferred' if t["status"] == 'adiada' else 'badge'
        cards.append(f'<details class="task" id="{t["id"]}" data-priority="{t["priority"]}" data-status="{escape(t["status"])}"{opened}><summary><span class="task-id">{t["id"]}</span><span class="summary-text"><strong>{escape(t["title"])}</strong><small>{escape(t["area"])} · {escape(t["kind"])}</small></span><span class="{badge}">{t["priority"]} · {escape(t["status"])}</span></summary><div class="task-body">{body}</div></details>')
    analysis_html = ''.join(f'<div class="analysis-item"><strong>{escape(x["finding"])}</strong><p>{escape(x["implication"])}</p><details class="references"><summary>Fontes</summary>{bullets(x["evidence"])}</details></div>' for x in data["analysis"])
    calendar_html = ''.join(f'<div class="analysis-item" id="{c["id"]}"><strong>{c["id"]}</strong><p>{escape(c["rule"])}</p><p>{links(c["tasks"])}</p></div>' for c in calendar)
    waves_html = ''.join(f'<div class="wave"><strong>{w["wave"]}. {escape(w["title"])}</strong><p>{links(w["tasks"])}</p></div>' for w in data["execution_waves"])
    rows_html = ''.join(f'<tr id="{f["id"]}"><td>{f["id"]}</td><td><strong>{escape(f["screen"])}</strong><br>{escape(f["original_text"])}</td><td>{links(f["task_ids"])}</td></tr>' for f in data["feedback"])
    decisions_html = ''.join(f'<div class="analysis-item"><strong>{escape(a["topic"])}</strong><p>{escape(a["proposal"])}</p><p>{escape(a["status"])} · {links([a["owner_task"]])}</p></div>' for a in data["assumptions_and_decisions"])
    stat_options = ''.join(f'<option value="{escape(s)}">{escape(s.capitalize())}</option>' for s in sorted(statuses))
    html = f'''<!doctype html>
<html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Cogna · Feedbacks e calendário · 19/11/2026</title><style>{css}</style></head><body>
<header><div class="eyebrow">Jogos de Empresa · revisão de 19 de novembro de 2026</div>
<h1>Feedbacks de interface e calendário de 30 dias</h1><p>Avançar de compromisso em compromisso. Contas debitam no dia; eventos abrem decisões. Previsíveis anunciados antes, imprevistos revelados somente ao avançar. Incidência existente preservada.</p>
<p><strong>Execução sequencial autorizada: uma conversa e um commit local em inglês por tarefa.</strong></p>
<nav aria-label="Seções"><a href="#tarefas">Tarefas</a> · <a href="#calendario">Calendário</a> · <a href="#analise">Análise</a> · <a href="#rastreabilidade">Feedbacks</a> · <a href="EXECUCAO.txt">Execução</a></nav></header><main>
<div class="stats"><div class="stat"><strong>{len(data["feedback"])}</strong><span>feedbacks originais</span></div><div class="stat"><strong>{len(tasks)}</strong><span>tarefas especificadas</span></div><div class="stat"><strong>{completed}/{len(active)}</strong><span>tarefas ativas concluídas</span></div><div class="stat"><strong>{len(calendar)}</strong><span>esclarecimentos do calendário</span></div></div>
<div class="callout"><strong>Começar por {links([data["first_task"]])}: contrato do calendário.</strong><p>As contas debitam efetivamente durante o mês. O fechamento reconcilia pagamentos e caixa para não cobrar novamente. Os 30 dias ficam visíveis, mas imprevistos não deixam pistas antecipadas.</p><p>Venda de usados ({links(["FB-28"])}) permanece adiada.</p></div>
<h2 id="calendario">Regras confirmadas do calendário</h2>{calendar_html}
<h2>Ordem de execução</h2>{waves_html}<p>P1: regra, fluxo ou integridade. P2: apresentação. P3: futuro adiado. As dependências de cada tarefa prevalecem.</p>
<h2 id="tarefas">Tarefas</h2><div class="toolbar" role="search"><label class="field search">Buscar tarefa<input id="search" type="search" placeholder="Ex.: calendário, conta, equipe, borda"></label><label class="field">Prioridade<select id="priority"><option value="">Todas</option><option>P1</option><option>P2</option><option>P3</option></select></label><label class="field">Status<select id="status"><option value="">Todos</option>{stat_options}</select></label><button type="button" id="expand">Expandir visíveis</button></div><p id="count" aria-live="polite">{len(tasks)} tarefas exibidas.</p><div id="task-list">{"".join(cards)}</div>
<h2 id="analise">Análise do projeto</h2><p>Base estática inicial: <code>{data["baseline"]["git_head"]}</code>. Evidência de implementação e QA deve ser registrada por tarefa durante a execução.</p>{analysis_html}
<h2>Decisões e interpretações</h2>{decisions_html}<h2>Regras comuns</h2>{bullets(data["common_contract"])}
<h2 id="rastreabilidade">Rastreabilidade dos feedbacks originais</h2><div class="table-wrap"><table><thead><tr><th>Origem</th><th>Tela e feedback</th><th>Tarefas</th></tr></thead><tbody>{rows_html}</tbody></table></div>
<footer><a href="tasks.json">Backlog JSON</a> · <a href="COMECE-AQUI.txt">Como usar</a> · <a href="EXECUCAO.txt">Procedimento de execução</a> · <a href="ANALISE.txt">Análise</a> · <a href="RASTREABILIDADE.txt">Rastreabilidade</a><br>Atualizar a fonte JSON e executar python tools/ui/render_feedback_plan.py após cada aceite. UI/FONT anteriores permanecem históricos.</footer></main><script>{js}</script></body></html>
'''
    write("INDICE.html", html)
    print(f"Rendered {len(tasks)} task specifications, index, analysis, and traceability.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[2])
    render(parser.parse_args().project)
