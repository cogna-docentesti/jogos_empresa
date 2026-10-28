#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Infrastructure.Session;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Explicit, isolated development-player smoke. Absent from release builds.</summary>
public sealed class UI20StandaloneProbe : MonoBehaviour
{
    string directory,mode;readonly List<object> observations=new();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void StartProbe()
    {
        if(Application.isEditor)return;
        string output=Environment.GetEnvironmentVariable("COGNA_UI20_PLAYER_EVIDENCE");
        if(string.IsNullOrEmpty(output))return;
        string db=Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH"),key=Environment.GetEnvironmentVariable("COGNA_TEST_IDENTIFICATION_KEY");
        if(string.IsNullOrEmpty(db)||string.IsNullOrEmpty(key)||key=="PlayerSession.Identification"||Path.GetFullPath(db)==Path.Combine(Application.persistentDataPath,"game.db"))throw new InvalidOperationException("Standalone QA requires isolated database and identification key");
        var host=new GameObject("UI20StandaloneProbe");DontDestroyOnLoad(host);var p=host.AddComponent<UI20StandaloneProbe>();p.directory=output;p.mode=Environment.GetEnvironmentVariable("COGNA_UI20_PLAYER_MODE")??"first";Directory.CreateDirectory(output);p.StartCoroutine(p.Run());
    }
    static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static T View<T>(string n)=>GameObject.Find("Canvas").transform.Find(n).GetComponent<T>();
    static void Click(UnityEngine.UI.Button b)
    {
        Canvas.ForceUpdateCanvases();var r=(RectTransform)b.transform;var p=new PointerEventData(EventSystem.current){pointerId=-1,button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center))};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        if(!b.IsActive()||!b.IsInteractable()||hits.Count==0||hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()!=b)throw new InvalidOperationException("Standalone blocked control "+b.name);
        ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,mode+"-"+name+".png"));
        observations.Add(new{name,frame=Time.frameCount,width=Screen.width,height=Screen.height,state=GameManager.Instance.StateMachine.CurrentState.ToString(),panel=UIStateListener.Instance.CurrentPanel?.name,sessionId=GameSessionState.Current?.sessionId,cash=GameSessionState.Current?.currentCash,month=GameSessionState.Current?.currentRound});
        yield return null;
    }
    IEnumerator Run()
    {
        Application.runInBackground=true;for(int i=0;i<12;i++)yield return null;
        if(DatabaseInitializer.DatabaseService.Connection.DatabasePath!=Environment.GetEnvironmentVariable("COGNA_TEST_DATABASE_PATH"))throw new InvalidOperationException("Player isolation failed");
        yield return Shot("startup");
        if(mode=="first")
        {
            if(GameManager.Instance.StateMachine.CurrentState!=GameState.Config_Identification)throw new InvalidOperationException("Player entry must be identification in GameScene");
            var f=UnityEngine.Object.FindFirstObjectByType<UIIndentification>();Field<TMP_InputField>(f,"studentNameInput").text="Participante QA do executável Windows";Field<TMP_InputField>(f,"raInput").text="202020";Click(Field<UnityEngine.UI.Button>(f,"buttonRegister"));
            yield return Shot("location");var location=View<LocationScreenView>("Panel_Location");ExecuteEvents.Execute(location.Zones.Single(z=>z.id=="bank").marker.gameObject,new PointerEventData(EventSystem.current){pointerId=4},ExecuteEvents.pointerClickHandler);Click(Field<UnityEngine.UI.Button>(location,"confirmButton"));
            yield return Shot("restaurant");var restaurant=View<RestaurantScreenView>("Panel_Restaurant");Click(Field<UnityEngine.UI.Button>(restaurant,"cardPodrao"));yield return null;Click(Field<UnityEngine.UI.Button>(restaurant,"confirmButton"));
            yield return Shot("menu");Click(Field<UnityEngine.UI.Button>(View<MenuPricingScreenView>("Panel_MenuPricing"),"confirmButton"));
            yield return Shot("bank");
        }
        else
        {
            if(GameManager.Instance.StateMachine.CurrentState!=GameState.Initial_Capital)throw new InvalidOperationException("Player did not resume the saved bank step");
            yield return Shot("resumed-bank");
        }
        var s=GameSessionState.Current;

        var exit=View<ExitDialogController>("ExitUI");Click(Field<UnityEngine.UI.Button>(exit,"openButton"));yield return Shot("exit");
        File.WriteAllText(Path.Combine(directory,mode+"-execution.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=true,mode,unity=Application.unityVersion,platform=Application.platform.ToString(),scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,frame=Time.frameCount,observations,database=DatabaseInitializer.DatabaseService.Connection.DatabasePath,sessionId=s.sessionId,cash=s.currentCash,month=s.currentRound,results=new RoundResultRepository(DatabaseInitializer.DatabaseService.Connection).GetBySessionId(s.sessionId).Count},Newtonsoft.Json.Formatting.Indented));
        Click(Field<UnityEngine.UI.Button>(exit,"saveExitButton").gameObject.activeSelf?Field<UnityEngine.UI.Button>(exit,"saveExitButton"):Field<UnityEngine.UI.Button>(exit,"discardButton"));
    }
}
#endif
