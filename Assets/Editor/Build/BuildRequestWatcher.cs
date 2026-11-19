#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>
    /// Pedido de build por arquivo: crie BuildRequests/windows.request ou BuildRequests/android.request
    /// na raiz do projeto e o editor aberto inicia a build no próximo ciclo (útil para automação sem clicar no menu).
    /// O arquivo é renomeado para .done ao começar, para não repetir.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildRequestWatcher
    {
        private const string Folder = "BuildRequests";
        private static double _next;

        static BuildRequestWatcher() => EditorApplication.update += Poll;

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _next || EditorApplication.isCompiling
                || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            _next = EditorApplication.timeSinceStartup + 2;
            if (!Directory.Exists(Folder)) return;
            if (Take("windows")) EditorApplication.delayCall += WindowsBuilder.BuildFromMenu;
            else if (Take("android")) EditorApplication.delayCall += AndroidBuilder.BuildFromMenu;
        }

        private static bool Take(string name)
        {
            string path = Path.Combine(Folder, name + ".request");
            if (!File.Exists(path)) return false;
            string done = Path.Combine(Folder, name + ".done");
            if (File.Exists(done)) File.Delete(done);
            File.Move(path, done);
            return true;
        }
    }
}
#endif
