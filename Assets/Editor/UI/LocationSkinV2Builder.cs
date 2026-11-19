using UnityEditor;

namespace Game.EditorTools
{
    /// <summary>Preserves the asset and zone IDs; shared surfaces come from the approved theme.</summary>
    public static class LocationSkinV2Builder
    {
        [MenuItem("Tools/Redesign UI/Criar-atualizar skin da tela de localizacao")]
        public static void Build()
        {
            GlobalThemeMigration.UpdateLocationSkin();
            AssetDatabase.SaveAssets();
        }
    }
}
