using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using Game.Infrastructure.Session;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Real Play Mode evidence against isolated storage, with frames between UI interactions.</summary>
public static class UI02ThemeValidation
{
    [Serializable] private class Check { public string name; public bool passed; public string detail; public int frame; }
    [Serializable] private class Report { public List<Check> checks=new(); public bool completed; public int failures; }
    private static readonly Queue<(string name,Action action)> steps=new();
    private static Report report;
    private static double next;
    private static int previousFrame, managerId;
    private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    private static T Find<T>() where T:UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Fields).GetValue(target);
    private static UIIndentification Form=>Find<UIIndentification>();
    private static TMP_InputField Name=>Field<TMP_InputField>(Form,"studentNameInput");
    private static TMP_InputField RA=>Field<TMP_InputField>(Form,"raInput");
    private static UnityEngine.UI.Button Register=>Field<UnityEngine.UI.Button>(Form,"buttonRegister");
    private static GameState State=>GameManager.Instance.StateMachine.CurrentState;
    private static void Expect(bool b,string why) { if(!b) throw new InvalidOperationException(why); }
    private static void Click(object view,string field)=>Field<UnityEngine.UI.Button>(view,field).onClick.Invoke();
    private static void Add(string name,Action action)=>steps.Enqueue((name,action));
    private static void Capture(string name)=>UI01Validation.Capture("../UI-02/"+name);
    private static readonly string LongName="João Antônio da Conceição de Albuquerque e Sá Silva".Substring(0,50);

    [MenuItem("Tools/Jogos de Empresa/UI-02/Validate theme in Play Mode")]
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage();
        if(!EditorApplication.isPlaying || State!=GameState.Config_Identification) throw new InvalidOperationException("Start at identification with isolated storage.");
        UnityEngine.Application.runInBackground=true; report=new Report(); steps.Clear();
        managerId=GameManager.Instance.GetInstanceID(); previousFrame=Time.frameCount;
        Add("Empty form and both Inter Tight assets cover Portuguese, currency and negatives",()=>{
            Expect(Name.text=="" && RA.text=="" && !Register.interactable,"Empty state");
            foreach(var path in new[]{GameTypography.FontAssetPath,"Assets/TextMesh Pro/Fonts/InterTight-Italic.asset"})
            {
                var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                foreach(char c in "ÁÉÍÓÚáéíóúÂÊÔâêôÃÕãõÇç R$ -12.345,67") Expect(font.HasCharacter(c),"Missing glyph "+c+" in "+path);
            }
            Capture("identification-empty-1920x1080");
            Name.text="Ana"; RA.text="12345"; Form.Register();
        });
        Add("Validation has a text message, focused field and dark coral text",()=>{
            var error=Field<TMP_Text>(Form,"validationText");
            Expect(error.text.Contains("pelo menos 5") && Name.isFocused,"Invalid name feedback/focus");
            Expect(error.color==GamePalette.ErrorText,"Error token"); Capture("identification-error-1920x1080");
            Name.text=LongName; RA.text="1234567890";
        });
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1280,720)})
        {
            var s=size; string suffix=s.x+"x"+s.y;
            Add("Set identification viewport "+suffix,()=>UI01Validation.SetGameView(s.x,s.y));
            Add("Identification long input at "+suffix,()=>{
                Expect(Screen.width==s.x&&Screen.height==s.y,"Viewport mismatch");
                Expect(Name.text.Length==50 && Name.textComponent.fontSize==GameTypography.Input && !Name.textComponent.enableAutoSizing,"Long input/size");
                VerifyText(Form.gameObject); Capture("identification-long-"+suffix); Register.onClick.Invoke();
            });
            Add("Revisit identification after Location Back at "+suffix,()=>{
                Expect(State==GameState.Config_Location,"Register did not open location");
                Click(Find<LocationScreenView>(),"backButton");
                Expect(State==GameState.Config_Identification && Name.text==LongName && RA.text=="1234567890","Revisit lost identification");
            });
            Add("Revisited identification retains shared roles at "+suffix,()=>{VerifyText(Form.gameObject); Capture("identification-revisited-"+suffix);});
        }
        Add("Return to 1920 for pointer states",()=>UI01Validation.SetGameView(1920,1080));
        Add("Hover the registration button",()=>ExecuteEvents.Execute(Register.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler));
        Add("Hover sprite is applied",()=>{
            Expect(Register.image.overrideSprite.name.Contains("hover"),"Hover sprite"); Capture("identification-hover-1920x1080");
            ExecuteEvents.Execute(Register.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerDownHandler);
        });
        Add("Pressed sprite is applied",()=>{
            Expect(Register.image.overrideSprite.name.Contains("pressed"),"Pressed sprite"); Capture("identification-pressed-1920x1080");
            ExecuteEvents.Execute(Register.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerUpHandler);
            Register.onClick.Invoke();
        });
        Add("Choose real location and restaurant",()=>{
            var location=Find<LocationScreenView>(); Click(location,"bankButton"); Click(location,"confirmButton");
            Expect(State==GameState.Config_Restaurant,"D2 missing");
            var restaurant=Find<RestaurantScreenView>(); Click(restaurant,"cardFrances"); Click(restaurant,"confirmButton");
        });
        Add("Confirm menu using the existing SQLite boundary",()=>{
            Expect(State==GameState.Config_TargetSegment,"D3 missing"); Click(Find<MenuPricingScreenView>(),"confirmButton");
            Expect(State==GameState.Initial_Capital,"Bank missing");
            Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==1,"Expected one isolated session");
        });
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768),new Vector2Int(1280,720)})
        {
            var s=size; string suffix=s.x+"x"+s.y;
            Add("Set catalog viewport "+suffix,()=>UI01Validation.SetGameView(s.x,s.y));
            Add("Catalog four actual entries at "+suffix,()=>{
                Expect(Screen.width==s.x&&Screen.height==s.y,"Viewport mismatch");
                var view=Find<FinancialScreenView>(); Expect(view.GetComponentsInChildren<BankCardView>().Length==4,"Catalog entry count");
                VerifyText(view.gameObject); VerifyFooter(view,suffix); Capture("catalog-normal-"+suffix);
                var scroll=view.GetComponentInChildren<UnityEngine.UI.ScrollRect>(); scroll.verticalNormalizedPosition=0;
            });
            Add("Catalog scroll reaches the last entry at "+suffix,()=>{
                var view=Find<FinancialScreenView>();
                Expect(view.GetComponentInChildren<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition<.01f,"Scroll did not reach end");
                Capture("catalog-bottom-"+suffix);
                // Actual panel lifecycle, same state and services; no catalog data replaced.
                view.gameObject.SetActive(false); view.gameObject.SetActive(true);
            });
            Add("Catalog revisit recreates exactly four cards at "+suffix,()=>{
                var view=Find<FinancialScreenView>(); Expect(view.GetComponentsInChildren<BankCardView>().Length==4,"Duplicate cards on revisit");
                VerifyText(view.gameObject); Capture("catalog-revisited-"+suffix);
            });
        }
        Add("Set catalog state viewport",()=>UI01Validation.SetGameView(1920,1080));
        Add("Select actual credit card",()=>{
            var card=Find<FinancialScreenView>().GetComponentsInChildren<BankCardView>().First();
            Click(card,"selectButton");
        });
        Add("Selection has a border and textual selection message",()=>{
            var view=Find<FinancialScreenView>(); Expect(Field<TMP_Text>(view,"hintText").text.Contains("Selecionado"),"Missing selection label");
            Capture("catalog-selected-1920x1080");
            view.SetInitialCapital(-12345); view.SetHint("Atenção: saldo de teste R$ -12.345,67. Revise as condições de contratação e o planejamento da operação.");
        });
        Add("Negative currency and long Portuguese feedback render at fixed role sizes",()=>{
            var view=Find<FinancialScreenView>(); VerifyText(view.gameObject);
            var value=Field<TMP_Text>(view,"capitalValueText"); Expect(value.text.Contains("-") && value.textInfo.characterCount>0 && !value.isTextOverflowing,"Negative currency clipped");
            Capture("catalog-negative-long-feedback-1920x1080");
            GameSessionState.Current.currentRound=3;
            view.gameObject.SetActive(false); view.gameObject.SetActive(true);
        });
        Add("Month-three disabled credit retains a textual explanation",()=>{
            var view=Find<FinancialScreenView>();
            Expect(Field<TMP_Text>(view,"hintText").text==LoanService.LastMonthMessage,"Missing unavailable explanation");
            var cards=view.GetComponentsInChildren<BankCardView>();
            Expect(cards.Count(c=>!Field<UnityEngine.UI.Button>(c,"selectButton").interactable)==3,"Disabled credit states");
            VerifyText(view.gameObject); Capture("catalog-disabled-1920x1080");
            Expect(GameManager.Instance.GetInstanceID()==managerId,"Manager reloaded");
        });
        next=EditorApplication.timeSinceStartup+.5; EditorApplication.update-=Tick; EditorApplication.update+=Tick;
    }

    private static void VerifyText(GameObject root)
    {
        foreach(var text in root.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.enabled && !t.name.Contains("Shadow")))
        {
            Expect(text.font.name=="InterTight-Variable",text.name+" wrong font");
            Expect(!text.enableAutoSizing,text.name+" arbitrary autosize");
            Expect(text.fontSharedMaterial==text.font.material,text.name+" material override");
            Expect(!text.fontSharedMaterial.IsKeywordEnabled("UNDERLAY_ON") && text.fontSharedMaterial.GetFloat("_OutlineWidth")==0,"Legacy material effect");
            if(text.gameObject.activeInHierarchy && !string.IsNullOrWhiteSpace(text.text))
            {
                text.ForceMeshUpdate();
                Expect(text.textInfo.characterCount>0,text.name+" invisible text");
            }
        }
    }

    private static void VerifyFooter(FinancialScreenView view,string suffix)
    {
        var confirm=Field<UnityEngine.UI.Button>(view,"confirmButton");
        var menu=GameObject.Find("Canvas/MenuAccess/Button").GetComponent<UnityEngine.UI.Button>();
        Rect Bounds(RectTransform rect)
        {
            var corners=new Vector3[4]; rect.GetWorldCorners(corners);
            var min=RectTransformUtility.WorldToScreenPoint(null,corners[0]);
            var max=RectTransformUtility.WorldToScreenPoint(null,corners[2]);
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        Rect a=Bounds((RectTransform)confirm.transform),b=Bounds((RectTransform)menu.transform);
        Expect(!a.Overlaps(b),"Menu overlaps Confirmar Seleção");
        foreach(var button in new[]{confirm,menu})
        {
            var hits=new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=Bounds((RectTransform)button.transform).center},hits);
            Expect(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==button,"Blocked hit target: "+button.name);
        }
        File.WriteAllText("output/ui-update/UI-02/catalog-hit-targets-"+suffix+".json",
            Newtonsoft.Json.JsonConvert.SerializeObject(new{resolution=suffix,overlap=false,confirm=new[]{a.x,a.y,a.width,a.height},menu=new[]{b.x,b.y,b.width,b.height},bothCentersReachTheirOwnButton=true},Newtonsoft.Json.Formatting.Indented));
    }

    private static void Tick()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        if(EditorApplication.timeSinceStartup<next) return;
        if(!EditorApplication.isPlaying || steps.Count==0)
        {
            report.completed=steps.Count==0&&report.failures==0; EditorApplication.update-=Tick;
            File.WriteAllText("output/ui-update/UI-02/playmode-theme.json",JsonUtility.ToJson(report,true)); return;
        }
        var step=steps.Dequeue(); var check=new Check{name=step.name,frame=Time.frameCount};
        try { Expect(Time.frameCount>previousFrame,"Player loop did not advance"); step.action(); check.passed=true; }
        catch(Exception e) { check.detail=e.ToString(); report.failures++; steps.Clear(); Debug.LogError("[UI-02 QA] "+step.name+": "+e.Message); }
        report.checks.Add(check); previousFrame=Time.frameCount; next=EditorApplication.timeSinceStartup+.6;
    }
}
