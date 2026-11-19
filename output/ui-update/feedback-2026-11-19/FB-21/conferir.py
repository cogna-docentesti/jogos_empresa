"""Confere a matriz documental e os exemplos monetários contra os arquivos do projeto."""
import json
import re
from decimal import Decimal
from pathlib import Path

import yaml


ROOT = Path(__file__).resolve().parents[4]
RESOURCES = ROOT / "Assets/Resources"
CONTRACT = ROOT / "docs/ui-update/feedback-2026-11-19/CONTRATO-CALENDARIO.md"


def asset(path):
    body = path.read_text(encoding="utf-8").split("MonoBehaviour:", 1)[1]
    return yaml.safe_load(body)


def options(event):
    return {option["id"]: option["effects"] for option in event["options"]}


def main():
    rows = re.findall(r"^\| `(\w+)` \| ([^|]+) \| (Anunciado|Surpresa) \| ([^|]+) \|$",
                      CONTRACT.read_text(encoding="utf-8"), re.MULTILINE)
    paths = sorted((RESOURCES / "Events").glob("*.asset"))
    catalog = {event["id"]: event for event in map(asset, paths)}
    assert len(rows) == len(catalog) == len(paths) == 20
    assert {row[0] for row in rows} == catalog.keys()
    for event_id, title, visibility, reason in rows:
        assert title == catalog[event_id]["title"] and reason.strip()
    announced = sorted(row[0] for row in rows if row[2] == "Anunciado")
    assert "local_festival" in announced and "freezer_breakdown" not in announced
    option_count = sum(len(options(event)) for event in catalog.values())
    assert option_count == 60
    assert asset(RESOURCES / "MonthlySimulation/DefaultSettings.asset")["operatingDays"] == 30

    # Plano datado de exemplo do contrato. Conferência de compatibilidade, sem sortear.
    choices = {"bulk_purchase_opportunity": "decline_purchase", "local_festival": "normal_operation",
               "freezer_breakdown": "repair_now", "payment_system_failure": "emergency_support"}
    groups = [catalog[key]["exclusionGroup"] for key in choices if catalog[key]["exclusionGroup"]]
    assert len(groups) == len(set(groups))
    assert all(catalog[key]["minRound"] <= 1 <= catalog[key]["maxRound"] for key in choices)
    assert all(not catalog[key]["applicableRestaurantTypes"] for key in choices)
    assert catalog["freezer_breakdown"]["conditions"] == [{"type": 0, "comparison": 5, "value": 1}]
    assert set(options(catalog["freezer_breakdown"])) == {"repair_now", "temporary_repair", "postpone_repair"}
    effects = {key: options(catalog[key])[value] for key, value in choices.items()}
    assert effects["local_festival"]["demandMultiplier"] == 1.3 and effects["local_festival"]["duration"] == 1
    event_cash = sum(effect["cashDelta"] for effect in effects.values())

    opening = 12000
    location = asset(RESOURCES / "Locations/LOC_University.asset")
    restaurant = asset(RESOURCES / "Restaurants/REST_Lanches.asset")
    roles = {role["id"]: role for role in map(asset, (RESOURCES / "Roles").glob("*.asset"))}
    payroll = sum(roles[member["roleId"]]["salary"] * member["quantity"] for member in restaurant["requiredRoles"])
    paid = location["rent"] + restaurant["baseMonthlyCost"] + payroll
    day10 = opening - restaurant["baseMonthlyCost"]
    day20 = day10 - location["rent"]
    day25 = day20 - payroll
    revenue, supplies = 30000, 10000  # Entradas ilustrativas do contrato.
    net = revenue - supplies - paid + event_cash
    remaining = net + paid
    assert (day10, day20, day25) == (10500, 4500, -2400)
    assert (paid, event_cash, net, remaining, day25 + remaining) == (14400, -4000, 1600, 16000, 13600)
    assert opening + net == day25 + remaining

    credit = asset(RESOURCES / "CreditLines/CREDIT_MicrocreditoEmpresarial.asset")
    balance = Decimal(credit["maxAmount"])
    rate = Decimal(str(credit["monthlyInterestRate"]))
    principal = balance / 2
    month2 = principal + balance * rate
    month3 = principal + principal * rate
    assert (month2, month3) == (26000, 25500)

    # Conferência independente com valores esperados de uma fixture preexistente.
    tests = (ROOT / "Assets/Editor/Tests/MonthlySimulationTests.cs").read_text(encoding="utf-8")
    fixture = tests.split("void KnownMonthlyStatementUsesSelectedPricesAndCatalogCosts()", 1)[1].split("[Test]", 1)[0]
    expected = {field: int(value) for value, field in re.findall(r"Assert.AreEqual\((\d+)f, result\.(\w+)\);", fixture)}
    opening_fixture = int(re.search(r"session.currentCash = (\d+)f;", tests).group(1))
    prepaid = expected["salaries"] + expected["rent"] + expected["utilities"]
    closing = opening_fixture - prepaid + expected["grossRevenue"] - expected["supplyCost"]
    assert closing == opening_fixture + expected["netResult"] == expected["closingCash"]

    result = {
        "natureza": "Comparação de arquivos e aritmética. Sem execução do jogo.",
        "matriz": {"assets": len(paths), "ids_titulos_justificativas_conferidos": len(rows),
                   "opcoes_do_catalogo": option_count, "anunciados": announced, "surpresas": len(rows) - len(announced)},
        "cenario_em_reais": {"abertura": opening, "opcoes": choices, "caixa_apos_dias_10_20_25": [day10, day20, day25],
                             "pagamentos": paid, "impacto_direto_eventos": event_cash, "receita_ilustrativa": revenue,
                             "insumos_ilustrativos": supplies, "resultado": net, "ajuste_final": remaining, "saldo_final": opening + net},
        "microcredito_em_reais": {"parcelas_meses_2_e_3": [int(month2), int(month3)], "principal_por_parcela": int(principal)},
        "fixture_existente_em_reais": {"abertura": opening_fixture, "pagamentos": prepaid, "saldo_final": closing}
    }
    print(json.dumps(result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
