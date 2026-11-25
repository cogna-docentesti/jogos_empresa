using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    public static class CashLine
    {
        public const string Sales = "SALES";
        public const string OtherReceipts = "OTHER_RECEIPTS";
        public const string Suppliers = "SUPPLIERS";
        public const string Payroll = "PAYROLL";
        public const string Rent = "RENT";
        public const string Utilities = "UTILITIES";
        public const string Taxes = "TAXES";
        public const string Marketing = "MARKETING";
        public const string Maintenance = "MAINTENANCE";
        public const string OtherOperating = "OTHER_OPERATING";
        public const string Events = "EVENTS";
        public const string LoanInterest = "LOAN_INTEREST";
        public const string LoanPrincipal = "LOAN_PRINCIPAL";
        public const string Overdraft = "OVERDRAFT";
    }

    /// <summary>Conta recorrente do mes: reconhecida por dia (DRE) e paga no vencimento (caixa).</summary>
    public sealed class RecurringBill
    {
        public string Id;
        public string Label;
        public string CashLine;
        public string Icon;
        public int DueDay;
        public double Accrued;
        public double Paid;
        public double LastDailyAccrual;
    }

    /// <summary>
    /// Caixa do mes. Separa competencia (o que entra na DRE) de caixa (quando o dinheiro sai):
    ///  - contas recorrentes acumulam por dia e sao pagas no vencimento pelo valor estimado do mes;
    ///    no fechamento, a diferenca e acertada (SAME_MONTH) ou vira conta a pagar no dia 5 do mes
    ///    seguinte (NEXT_MONTH_DAY5);
    ///  - compras de insumos aumentam estoque e saem do caixa a vista (com desconto) ou em duas parcelas;
    ///  - a venda entra no caixa no mesmo dia (Bloco 5).
    /// Todo movimento passa por Receive/Pay, entao saldo final = saldo inicial + entradas - saidas.
    /// </summary>
    public sealed class Ledger
    {
        private readonly List<RecurringBill> _bills = new List<RecurringBill>();
        public double Cash { get; private set; }
        public CashFlowStatement Flow { get; } = new CashFlowStatement();
        public int OverdraftDays { get; private set; }
        public int OverdueBills { get; private set; }
        public double OverdraftLimit { get; }

        public IReadOnlyList<RecurringBill> Bills => _bills;

        public Ledger(double openingCash, double overdraftLimit)
        {
            Cash = openingCash;
            Flow.openingCash = openingCash;
            OverdraftLimit = Math.Max(0d, overdraftLimit);
        }

        public RecurringBill Bill(string id, string label, string cashLine, int dueDay, string icon)
        {
            var bill = _bills.Find(b => b.Id == id);
            if (bill != null) return bill;
            bill = new RecurringBill { Id = id, Label = label, CashLine = cashLine, DueDay = dueDay, Icon = icon };
            _bills.Add(bill);
            return bill;
        }

        public void Accrue(string billId, double amount)
        {
            var bill = _bills.Find(b => b.Id == billId) ?? throw new InvalidOperationException("Conta desconhecida: " + billId);
            if (amount < 0d) throw new InvalidOperationException("Acumulo negativo na conta " + billId);
            bill.Accrued += amount;
            bill.LastDailyAccrual = amount;
        }

        public void Receive(string line, double amount)
        {
            if (amount <= 0d) return;
            SimMath.RequireFinite(amount, "Entrada de caixa");
            Cash += amount;
            if (line == CashLine.Sales) Flow.salesReceipts += amount;
            else Flow.otherReceipts += amount;
        }

        /// <summary>Saida de caixa. Pode deixar o caixa negativo (cheque especial).</summary>
        public void Pay(string line, double amount)
        {
            if (amount <= 0d) return;
            SimMath.RequireFinite(amount, "Saida de caixa");
            if (Cash - amount < -OverdraftLimit) OverdueBills++;
            Cash -= amount;
            switch (line)
            {
                case CashLine.Suppliers: Flow.suppliers += amount; break;
                case CashLine.Payroll: Flow.payroll += amount; break;
                case CashLine.Rent: Flow.rent += amount; break;
                case CashLine.Utilities: Flow.utilities += amount; break;
                case CashLine.Taxes: Flow.taxes += amount; break;
                case CashLine.Marketing: Flow.marketing += amount; break;
                case CashLine.Maintenance: Flow.maintenance += amount; break;
                case CashLine.Events: Flow.eventPayments += amount; break;
                case CashLine.LoanInterest: Flow.loanInterest += amount; break;
                case CashLine.LoanPrincipal: Flow.loanPrincipal += amount; break;
                case CashLine.Overdraft: Flow.overdraftInterest += amount; break;
                default: Flow.otherOperating += amount; break;
            }
        }

        /// <summary>Recursos disponiveis para uma reacao: caixa + limite do cheque especial (se financiavel).</summary>
        public double Available(bool financeable) => financeable ? Cash + OverdraftLimit : Math.Max(0d, Cash);

        /// <summary>Paga as contas que vencem hoje pelo valor estimado do mes. Retorna as linhas do calendario.</summary>
        public List<CalendarEntry> PayDueBills(int day, int operatingDays)
        {
            var entries = new List<CalendarEntry>();
            foreach (var bill in _bills.Where(b => b.DueDay == day))
            {
                double projected = bill.Accrued + bill.LastDailyAccrual * Math.Max(0, operatingDays - day);
                double due = Math.Max(0d, projected - bill.Paid);
                if (due <= 0.005d) continue;
                Pay(bill.CashLine, due);
                bill.Paid += due;
                entries.Add(new CalendarEntry { kind = EntryKind.Expense, label = bill.Label, amount = -due, icon = bill.Icon });
            }
            return entries;
        }

        /// <summary>Acerto do fechamento: paga a diferenca no proprio mes ou devolve contas a pagar.</summary>
        public List<PayableState> CloseBills(string timing, int nextMonthDueAbsDay, List<CalendarEntry> entries)
        {
            var payables = new List<PayableState>();
            foreach (var bill in _bills)
            {
                double remaining = bill.Accrued - bill.Paid;
                if (Math.Abs(remaining) <= 0.005d) continue;
                if (remaining < 0d)
                {
                    // Pagou a mais pela estimativa: o fornecedor devolve no fechamento.
                    Receive(CashLine.OtherReceipts, -remaining);
                    CorrectOutflow(bill.CashLine, -remaining);
                    bill.Paid = bill.Accrued;
                    continue;
                }
                if (timing == BillTiming.NextMonthDay5)
                {
                    payables.Add(new PayableState { kind = PayableKind.Bill, label = bill.Label, cashLine = bill.CashLine, amount = remaining, dueAbsDay = nextMonthDueAbsDay });
                }
                else
                {
                    Pay(bill.CashLine, remaining);
                    bill.Paid += remaining;
                    entries?.Add(new CalendarEntry { kind = EntryKind.Expense, label = "Acerto: " + bill.Label, amount = -remaining, icon = bill.Icon });
                }
            }
            return payables;
        }

        private void CorrectOutflow(string line, double refund)
        {
            // A devolucao reduz a saida da propria linha, para o fluxo nao inflar entradas e saidas.
            Flow.otherReceipts -= refund;
            switch (line)
            {
                case CashLine.Payroll: Flow.payroll -= refund; break;
                case CashLine.Rent: Flow.rent -= refund; break;
                case CashLine.Utilities: Flow.utilities -= refund; break;
                case CashLine.Taxes: Flow.taxes -= refund; break;
                case CashLine.Marketing: Flow.marketing -= refund; break;
                case CashLine.Maintenance: Flow.maintenance -= refund; break;
                default: Flow.otherOperating -= refund; break;
            }
        }

        /// <summary>Juros do cheque especial sobre o saldo negativo do fim do dia.</summary>
        public double ChargeOverdraftInterest(double monthlyRate, int operatingDays)
        {
            if (Cash >= 0d) return 0d;
            OverdraftDays++;
            double interest = -Cash * monthlyRate / Math.Max(1, operatingDays);
            Pay(CashLine.Overdraft, interest);
            return interest;
        }

        public void Finish()
        {
            Flow.closingCash = Cash;
        }
    }
}
