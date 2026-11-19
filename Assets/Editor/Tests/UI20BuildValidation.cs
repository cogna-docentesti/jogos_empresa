using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class UI20BuildValidation
{
    public static void Windows()=>Build(false);
    public static void Android()=>Build(true);
    static void Build(bool android)
    {
        if(Application.dataPath.Replace('\\','/')!="D:/Cogna/jogos_empresa/Assets")throw new InvalidOperationException("Wrong project");
        string name=android?"android":"windows",root="output/ui-update/UI-20",path=android?"Builds/UI-20/Android/Cogna.apk":"Builds/UI-20/Windows/Cogna.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var profile=AssetDatabase.LoadAssetAtPath<BuildProfile>(android?"Assets/Settings/Build Profiles/Android™.asset":"Assets/Settings/Build Profiles/New Windows Profile.asset");
        if(profile==null)throw new InvalidOperationException("Existing build profile missing");
        int originalSubtarget=new SerializedObject(profile).FindProperty("m_Subtarget").intValue;
        if(!android)UI20ReferenceAudit.Run();
        if(File.ReadAllText("Packages/manifest.json").Contains("com.unity.pipeline"))throw new InvalidOperationException("Temporary Pipeline must be absent");
        // Keep the saved Windows profile intact; use explicit graphical Player options.
        // Android is launched with the matching active target and uses its saved profile.
        if(android)BuildProfile.SetActiveBuildProfile(profile);
        var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
        var report=android
            ? BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions { buildProfile=profile,locationPathName=path,options=BuildOptions.Development })
            : BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=scenes,target=BuildTarget.StandaloneWindows64,subtarget=(int)StandaloneBuildSubtarget.Player,locationPathName=path,options=BuildOptions.Development });
        var data=new {passed=report.summary.result==BuildResult.Succeeded,target=report.summary.platform.ToString(),originalSubtarget,effectiveSubtarget=android?originalSubtarget:(int)StandaloneBuildSubtarget.Player,subtargets=Enum.GetValues(typeof(StandaloneBuildSubtarget)).Cast<StandaloneBuildSubtarget>().Select(x=>new{name=x.ToString(),value=(int)x}),referenceProfile=AssetDatabase.GetAssetPath(profile),profileUsed=android,options=report.summary.options.ToString(),output=Path.GetFullPath(path),result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,bytes=report.summary.totalSize,seconds=report.summary.totalTime.TotalSeconds,scenes,steps=report.steps.Select(s=>new{name=s.name,messages=s.messages.Select(m=>new{type=m.type.ToString(),m.content})})};
        File.WriteAllText(root+"/build-"+name+".json",Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented));
        if(!data.passed)throw new InvalidOperationException("Build failed: "+name);
    }
}
