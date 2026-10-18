using System;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>UI-01 evidence helpers. Storage opt-in is mandatory for all destructive QA.</summary>
public static class UI01Validation
{
    public static void SetGameView(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = sizesType.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
        var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var kind = assembly.GetType("UnityEditor.GameViewSizeType");
        var size = Activator.CreateInstance(sizeType,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,
            new object[]{Enum.Parse(kind,"FixedResolution"),width,height,"UI01 "+width+"x"+height},null);
        int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
        var viewType = assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(viewType);
        viewType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,index);
        view.Repaint(); EditorApplication.QueuePlayerLoopUpdate();
    }

    public static void RequireIsolatedStorage()
    {
        string testPath=Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH");
        string testKey=Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY");
        if (string.IsNullOrWhiteSpace(testPath) || string.IsNullOrWhiteSpace(testKey)
            || testKey == "PlayerSession.Identification"
            || Path.GetFullPath(testPath) == Path.Combine(UnityEngine.Application.persistentDataPath,"game.db")
            || DatabaseInitializer.DatabaseService?.Connection?.DatabasePath != testPath)
            throw new InvalidOperationException("UI-01 requires an isolated database and PlayerPrefs key.");
    }

    public static string Capture(string name)
    {
        RequireIsolatedStorage();
        var type=typeof(Editor).Assembly.GetType("UnityEditor.PlayModeView");
        var view=type.GetMethod("GetMainPlayModeView",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,null);
        var source=(RenderTexture)type.GetField("m_TargetTexture",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
        if (source == null || source.width != Screen.width || source.height != Screen.height)
            throw new InvalidOperationException("The Game View must render at the evidence resolution.");
        var output=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32);
        var previous=RenderTexture.active;
        var texture=new Texture2D(source.width,source.height,TextureFormat.RGB24,false);
        try
        {
            if (SystemInfo.graphicsUVStartsAtTop) Graphics.Blit(source,output,new Vector2(1,-1),new Vector2(0,1));
            else Graphics.Blit(source,output);
            RenderTexture.active=output;
            texture.ReadPixels(new Rect(0,0,source.width,source.height),0,0); texture.Apply();
            string path=Path.GetFullPath("output/ui-update/UI-01/"+name+".png");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path,texture.EncodeToPNG());
            return path;
        }
        finally { RenderTexture.active=previous; RenderTexture.ReleaseTemporary(output); UnityEngine.Object.DestroyImmediate(texture); }
    }
}
