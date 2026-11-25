using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>Runs against temporary objects and an in-memory database, preserving the player's save.</summary>
    public static class EquipmentTutorialValidation
    {
        [MenuItem("Tools/Jogos de Empresa/Validar tutorial dos equipamentos")]
        public static void Run()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results(api));
            api.Execute(new ExecutionSettings(new Filter {
                testMode = TestMode.EditMode,
                testNames = new[] { "EquipmentTutorialTests", "UI19NavigationTests.EquipmentExplanationProgressDoesNotChangePurchasesOrCash",
                    "UI19NavigationTests.EquipmentTutorialBuysOnceAndWaitsForTheLastExplanationBeforeTeam" }
            }));
        }
        private sealed class Results : ICallbacks
        {
            private readonly TestRunnerApi api;
            public Results(TestRunnerApi api) => this.api = api;
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                System.IO.Directory.CreateDirectory("output/equipment-tutorial");
                TestRunnerApi.SaveResultToFile(result, "output/equipment-tutorial/tests.xml");
                Debug.Log($"[EquipmentTutorial] Passed: {result.PassCount}; failed: {result.FailCount}. Results: output/equipment-tutorial/tests.xml");
                Object.DestroyImmediate(api);
            }
        }
    }
}
