// Game/Domain/Simulation/Ledger.cs
// Livro contábil do mês. Toda operação passa por aqui e entra nas duas pontas:
// DRE pela competência e fluxo de caixa no dia do pagamento. O balanço do fechamento
// confere que as duas pontas batem (diferença zero).
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Simulation
{
    public enum DreLine
    {
        GrossRevenue, Taxes, Cmv, Payroll, Rent, Marketing, Utilities,
        MaintenanceAndLosses, OtherExpenses, OtherIncome, Depreciation, Interest
    }

    public enum CashLine
    {
        Sales, OtherInflows, Suppliers, Salaries, Rent, Bills, Taxes,
        Marketing, Maintenance, Events, Interest, Amortization
    }

    [Serializable]
    public sealed class LedgerEntry
    {
        public int Day;
        public bool IsCash;          // false = DRE (competência), true = caixa
        public int Line;             // (int)DreLine ou (int)CashLine
        public double Amount;        // DRE: receita positiva, despesa positiva na sua linha. Caixa: + entra, - sai
        public string Memo;
    }

    /// <summary>
    /// Faz os lançamentos e atualiza o estado (caixa, estoque, contas a pagar) junto.
    /// Nenhuma outra classe mexe em state.Cash diretamente: assim o balanço sempre fecha.
    /// </summary>
    public sealed class Accounting
    {
        private readonly SimulationState _s;
        private readonly SimulationConstants _k;
        public List<LedgerEntry> Entries { get; } = new List<LedgerEntry>();

        public Accounting(SimulationState state, SimulationConstants constants)
        {
            _s = state;
            _k = constants;
        }

        // ------------------------------------------------------------ primitivas
        private void Dre(int day, DreLine line, double amount, string memo)
            => Entries.Add(new LedgerEntry { Day = day, IsCash = false, Line = (int)line, Amount = amount, Memo = memo });

        private void Cash(int day, CashLine line, double amount, string memo)
        {
            Entries.Add(new LedgerEntry { Day = day, IsCash = true, Line = (int)line, Amount = amount, Memo = memo });
            _s.Cash += amount;
        }

        // ------------------------------------------------------------ operações do restaurante
        /// Receita fora das vendas recebida no mesmo dia (ex.: evento com ganho de caixa)
        public void ReceiveOtherIncome(int day, double amount, string memo)
        {
            if (amount <= 0) return;
            Dre(day, DreLine.OtherIncome, amount, memo);
            Cash(day, CashLine.OtherInflows, amount, memo);
        }

        /// Despesa paga no mesmo dia (aluguel, salários, marketing, custo de reação...)
        public void PayExpense(int day, DreLine dre, CashLine cash, double amount, string memo)
        {
            if (amount <= 0) return;
            Dre(day, dre, amount, memo);
            Cash(day, cash, -amount, memo);
        }

        /// Venda: receita entra no caixa; imposto vira dívida (paga no dia 29); CMV sai do estoque.
        public void Sell(int day, double revenue, double costOfGoods)
        {
            Dre(day, DreLine.GrossRevenue, revenue, "vendas");
            Cash(day, CashLine.Sales, revenue, "vendas");
            double tax = revenue * _k.TaxRate;
            Dre(day, DreLine.Taxes, tax, "impostos sobre vendas");
            _s.TaxesPayable += tax;
            double cmv = Math.Min(costOfGoods, _s.InventoryValue);
            Dre(day, DreLine.Cmv, cmv, "CMV das vendas");
            _s.InventoryValue -= cmv;
        }

        /// Compra de insumos: vira ESTOQUE (não é despesa). À vista com desconto, ou duas parcelas.
        public void BuySupplies(int day, int absDay, double value, bool installments)
        {
            if (value <= 0) return;
            if (installments)
            {
                _s.InventoryValue += value;
                _s.SupplierPayables += value;
                int nextMonthStart = ((absDay - 1) / _k.DaysPerMonth + 1) * _k.DaysPerMonth;
                _s.Installments.Add(new SupplierInstallment { DueAbsDay = nextMonthStart + _k.SupplierInstallmentDay, Amount = value / 2 });
                _s.Installments.Add(new SupplierInstallment { DueAbsDay = nextMonthStart + _k.DaysPerMonth + _k.SupplierInstallmentDay, Amount = value / 2 });
            }
            else
            {
                double paid = value * (1 - _k.CashDiscountRate);
                _s.InventoryValue += paid;
                Cash(day, CashLine.Suppliers, -paid, "insumos à vista");
            }
        }

        public void PaySupplierInstallment(int day, SupplierInstallment installment)
        {
            Cash(day, CashLine.Suppliers, -installment.Amount, "parcela de fornecedor");
            _s.SupplierPayables -= installment.Amount;
        }

        /// Perda de estoque (validade, freezer parado): o CMV também nasce na perda.
        public void Spoil(int day, double value, string memo)
        {
            value = Math.Min(value, _s.InventoryValue);
            if (value <= 0) return;
            Dre(day, DreLine.Cmv, value, memo);
            _s.InventoryValue -= value;
        }

        /// Conta de consumo que cresce com os pedidos: despesa hoje, pagamento depois.
        public void AccrueBill(int day, double amount, string memo)
        {
            if (amount <= 0) return;
            Dre(day, DreLine.Utilities, amount, memo);
            _s.BillsPayable += amount;
        }

        public void PayBills(int day, double amount, string memo)
        {
            if (Math.Abs(amount) < 1e-9) return;
            Cash(day, CashLine.Bills, -amount, memo);
            _s.BillsPayable -= amount;
        }

        public void PayTaxes(int day)
        {
            if (_s.TaxesPayable <= 0) return;
            Cash(day, CashLine.Taxes, -_s.TaxesPayable, "impostos");
            _s.TaxesPayable = 0;
        }

        /// Parcela do empréstimo: juros vão para a DRE; a amortização só sai do caixa e reduz a dívida.
        public void PayLoan(int day, double interest, double amortization)
        {
            PayExpense(day, DreLine.Interest, CashLine.Interest, interest, "juros do empréstimo");
            if (amortization > 0)
            {
                Cash(day, CashLine.Amortization, -amortization, "amortização do empréstimo");
                _s.LoanBalance -= amortization;
            }
        }

        /// Juros do cheque especial: 8% ao mês, proporcional por dia.
        public void ChargeOverdraftInterest(int day)
        {
            if (_s.Cash >= 0) return;
            double interest = -_s.Cash * _k.OverdraftMonthlyRate / _k.DaysPerMonth;
            PayExpense(day, DreLine.Interest, CashLine.Interest, interest, "juros do cheque especial");
        }

        public void Depreciate(int day, EquipmentUnit unit, double amount)
        {
            amount = Math.Min(amount, unit.BookValue);
            if (amount <= 0) return;
            Dre(day, DreLine.Depreciation, amount, "depreciação " + unit.ProfileId);
            unit.BookValue -= amount;
        }

        // ------------------------------------------------------------ totais
        public double Total(DreLine line) => Entries.Where(e => !e.IsCash && e.Line == (int)line).Sum(e => e.Amount);
        public double Total(CashLine line) => Entries.Where(e => e.IsCash && e.Line == (int)line).Sum(e => e.Amount);

        public double NetIncome =>
            Total(DreLine.GrossRevenue) + Total(DreLine.OtherIncome)
            - Total(DreLine.Taxes) - Total(DreLine.Cmv) - Total(DreLine.Payroll) - Total(DreLine.Rent)
            - Total(DreLine.Marketing) - Total(DreLine.Utilities) - Total(DreLine.MaintenanceAndLosses)
            - Total(DreLine.OtherExpenses) - Total(DreLine.Depreciation) - Total(DreLine.Interest);

        public double CashResult => Entries.Where(e => e.IsCash).Sum(e => e.Amount);
    }
}
