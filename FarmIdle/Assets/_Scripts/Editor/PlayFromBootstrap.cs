using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace FarmIdle.EditorTools
{
    // Hace que el botón Play arranque siempre desde la escena Bootstrap, aunque tengas abierta otra:
    // los servicios (GameManager, economía, pools) los crea el Bootstrapper y el resto de las escenas los necesita.
    // Se puede apagar desde el menú FarmIdle. En batchmode no se aplica, y mientras corren los tests de
    // PlayMode se suelta, porque traen su propia escena.
    [InitializeOnLoad]
    public static class PlayFromBootstrap
    {
        private const string MenuPath = "FarmIdle/Play siempre desde Bootstrap";
        private const string PrefKey = "FarmIdle.PlayFromBootstrap";

        private static bool Enabled => EditorPrefs.GetBool(PrefKey, true);

        static PlayFromBootstrap()
        {
            var testRunner = ScriptableObject.CreateInstance<TestRunnerApi>();
            testRunner.RegisterCallbacks(new TestRunCallbacks());

            EditorApplication.delayCall += Apply;
        }

        [MenuItem(MenuPath, false, 100)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(PrefKey, !Enabled);
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void Apply()
        {
            if (Application.isBatchMode) return;

            var bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePaths.Bootstrap);
            EditorSceneManager.playModeStartScene = Enabled ? bootstrap : null;
        }

        private static void Release()
        {
            EditorSceneManager.playModeStartScene = null;
        }

        private sealed class TestRunCallbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { Release(); }
            public void RunFinished(ITestResultAdaptor result) { Apply(); }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
        }
    }
}
