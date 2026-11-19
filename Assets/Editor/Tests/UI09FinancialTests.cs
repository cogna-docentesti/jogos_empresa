using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class UI09FinancialTests
{
    private GameObject card;
    private static T Field<T>(object o, string field) => (T)o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
    private BankCardView Present(string id, int round, string saved = "", float balance = 0, GameSessionStatus status = GameSessionStatus.IN_PROGRESS)
    {
        GameSessionState.Set(new GameSessionEntity { currentRound = round, creditLineId = saved, loanBalance = balance, status = status });
        card = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Card_loan.prefab"));
        var view = card.GetComponent<BankCardView>(); view.Setup(LoanService.FindLine(id)); return view;
    }
    [TearDown] public void Cleanup() { if (card != null) Object.DestroyImmediate(card); GameSessionState.Clear(); }
    [Test] public void FourArtworksAreBoundByExistingCatalogIdAndEconomyIsUnchanged()
    {
        var expected = new[] { ("sem_credito", 0f, 0f), ("microcredito_empresarial", 50000f, .02f), ("credito_empresarial_padrao", 120000f, .03f), ("credito_de_investimento", 250000f, .04f) };
        Assert.AreEqual(4, Resources.LoadAll<CreditLineData>("CreditLines").Length);
        foreach (var (id, amount, rate) in expected)
        {
            var line = LoanService.FindLine(id); Assert.NotNull(line); Assert.AreEqual(amount, line.maxAmount); Assert.AreEqual(rate, line.monthlyInterestRate); Assert.AreEqual(0, line.termRounds);
            Assert.AreEqual("Assets/Art/new/financial/credit/" + id + ".png", AssetDatabase.GetAssetPath(line.icon));
        }
    }
    [TestCase(1, true, 3)][TestCase(2, true, 2)][TestCase(3, false, 0)]
    public void NewLoanAvailabilityAndDisplayedTermUseRealService(int month, bool available, int months)
    {
        var view = Present("microcredito_empresarial", month);
        Assert.AreEqual(available, Field<UnityEngine.UI.Button>(view, "selectButton").interactable);
        Assert.AreEqual(available, LoanService.CanContract); Assert.AreEqual(months, LoanService.ContractTerm(month));
        var text = Field<TMP_Text>(view, "installmentText").text;
        if (available) Assert.That(text, Does.Contain(FinancialScreenView.Money(LoanService.Installment(50000, view.CreditLine, month))));
        else Assert.That(text, Does.Contain("indisponíveis"));
    }
    [TestCase(1)][TestCase(2)][TestCase(3)] public void NoCreditIsAvailableWithoutInventedInterestOrPayment(int month)
    {
        var view = Present("sem_credito", month); Assert.IsTrue(Field<UnityEngine.UI.Button>(view, "selectButton").interactable);
        Assert.AreEqual("Sem juros", Field<TMP_Text>(view, "interestRateText").text); Assert.AreEqual("Sem parcelas", Field<TMP_Text>(view, "termText").text);
    }
    [Test] public void MonthThreeExistingContractShowsItsRealBalancePaymentAndCannotBeReplaced()
    {
        var view = Present("microcredito_empresarial", 3, "microcredito_empresarial", 16666.666f);
        Assert.IsFalse(Field<UnityEngine.UI.Button>(view, "selectButton").interactable);
        Assert.That(Field<TMP_Text>(view, "availabilityText").text, Does.Contain("Contratado"));
        Assert.That(Field<TMP_Text>(view, "installmentText").text, Does.Contain(FinancialScreenView.Money(LoanService.Installment(16666.666f, view.CreditLine, 3))));
        Assert.That(Field<TMP_Text>(view, "termText").text, Does.Contain("1 parcela"));
        Assert.AreEqual(16666.666f, GameSessionState.Current.loanBalance); Assert.IsFalse(LoanService.TryContract(view.CreditLine));
    }
    [Test] public void NoCreditCannotReplaceAnExistingLoanEvenAfterBalanceIsPaid()
    {
        var view = Present("sem_credito", 2, "credito_empresarial_padrao", 0);
        Assert.IsTrue(LoanService.HasLoan(GameSessionState.Current)); Assert.IsFalse(Field<UnityEngine.UI.Button>(view, "selectButton").interactable);
    }
    [Test] public void UnavailableSessionHasNoActionAndCurrencyKeepsCents()
    {
        var view = Present("sem_credito", 3); GameSessionState.Clear(); view.Setup(view.CreditLine);
        Assert.IsFalse(Field<UnityEngine.UI.Button>(view, "selectButton").interactable); Assert.AreEqual("Sessão indisponível", Field<TMP_Text>(view, "availabilityText").text);
        Assert.AreEqual("R$ -1.234,56", FinancialScreenView.Money(-1234.56f));
    }
}
