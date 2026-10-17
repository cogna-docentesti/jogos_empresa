using Game.EditorTools;
using UnityEditor;

/// <summary>The legacy menu entry now updates the existing hierarchy without replacing its references.</summary>
public static class ExitDialogBuilder
{
    [MenuItem("Tools/Jogos de Empresa/Criar botao Sair e dialogo (E-05)")]
    public static void Build() => DialogUiMigration.Apply();
}
