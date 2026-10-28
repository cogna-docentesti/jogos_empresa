using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Adapter.In.UI.Theme;
using Game.Adapter.In.Controllers;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>Final gap checks: wide signed money, reentry, safe-area geometry and finite glyph corpus.</summary>
public static class Font10StressValidation
{
    static readonly Queue<(string,Action)> steps=new();
    static readonly List<object> checks=new();
    static int frame;static double due;static string resolution;
    static GameObject sheet;
    static TMP_InputField nameField;static int nameWrites;static float nameStartX;
    const string LongName="José Ítalo Gonçalves de Albuquerque e Vasconcelos Júnior — João Luiz, açúcares e gestão, descendentes gjpqy";
    static Transform Root=>GameObject.Find("Canvas").transform;
    static GameStateMachine States=>GameManager.Instance.StateMachine;
    static int Writes=>DatabaseInitializer.DatabaseService.Connection.ExecuteScalar<int>("SELECT total_changes()");
    static Vector2Int[] Sizes=>UiValidationContext.Resolutions;
    static void Add(string name,Action a)=>steps.Enqueue((name,a));
    static void Expect(bool b,string why){if(!b)throw new InvalidOperationException(why);}
    static string PathOf(Transform t)=>Game.EditorTools.Font09Migration.PathOf(t);
    static void Role(TMP_Text t,TypographyRole r){var s=GameTypography.Resolve(r);Expect(t.font==s.Font&&t.fontSharedMaterial==s.Material,"Face/material "+PathOf(t.transform));Expect((t.fontStyle&FontStyles.Bold)==0&&!t.enableAutoSizing,"Style "+PathOf(t.transform));}
    static void Capture(string name)
    {
        foreach(var t in Root.GetComponentsInChildren<TMP_Text>().Where(t=>t.enabled&&!string.IsNullOrEmpty(t.text))){var b=t.GetComponent<TypographyRoleBinding>();Expect(b!=null,"Missing binding");Role(t,b.Role);t.ForceMeshUpdate();Expect(t.GetComponentInParent<TMP_InputField>()!=null||!t.isTextOverflowing,"Overflow "+PathOf(t.transform));Expect(t.rectTransform.localScale==Vector3.one,"Scaled text");}
        string path=UI01Validation.Capture(UiValidationContext.CaptureName("FONT-10",name+"-"+resolution));UiValidationContext.OnCapture?.Invoke(name,path);
        if(Screen.width==2340){var p=(RectTransform)UIStateListener.Instance.CurrentPanel.transform;var corners=new Vector3[4];p.GetWorldCorners(corners);var safe=new Rect(90,24,2160,1056);Expect(corners[0].x>=safe.xMin-.5f&&corners[0].y>=safe.yMin-.5f&&corners[2].x<=safe.xMax+.5f&&corners[2].y<=safe.yMax+.5f,"Production frame outside simulated safe area");}
    }
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage();steps.Clear();checks.Clear();frame=-1;
        Plan();
        foreach(var size in Sizes)
        {
            var s=size;
            Add("Long identification viewport "+s,()=>{resolution=s.x+"x"+s.y;UI01Validation.SetGameView(s.x,s.y);States.ForceState(GameState.Config_Identification);nameWrites=Writes;var form=Root.Find("Panel_Identification").GetComponent<UIIndentification>();nameField=(TMP_InputField)typeof(UIIndentification).GetField("studentNameInput",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);nameField.text=LongName;nameField.Select();nameField.ActivateInputField();});
            Add("Long identification focus and start "+s,()=>{Expect(nameField.isFocused,"Long input did not receive focus");nameField.MoveTextStart(false);});
            Add("Long identification start "+s,()=>{Expect(nameField.text==LongName,"Long name changed");Role(nameField.textComponent,TypographyRole.Input);Expect(Writes==nameWrites,"Typing wrote SQLite");nameStartX=nameField.textComponent.rectTransform.anchoredPosition.x;Capture("long-name-start");nameField.MoveTextEnd(false);});
            Add("Long identification end and rebind "+s,()=>{Capture("long-name-end");Expect(nameField.stringPosition==LongName.Length,"Caret did not reach the end");Expect(nameField.textComponent.rectTransform.anchoredPosition.x<nameStartX-100,"Native input did not scroll horizontally");var info=nameField.textComponent.textInfo;var last=info.characterInfo.Take(info.characterCount).Last(c=>c.isVisible&&c.index<LongName.Length);Expect(last.character=='y',"Final typed descender not present");var metrics=last.textElement.glyph.metrics;var inkRight=new Vector3(last.origin+(metrics.horizontalBearingX+metrics.width)*last.scale,last.baseLine,0);var point=nameField.textViewport.InverseTransformPoint(nameField.textComponent.transform.TransformPoint(inkRight));Expect(point.x<=nameField.textViewport.rect.xMax+1&&point.x>=nameField.textViewport.rect.xMin,"Final descender "+last.character+" at "+point+" outside input viewport "+nameField.textViewport.rect+"; offset "+nameField.textComponent.rectTransform.anchoredPosition);nameField.DeactivateInputField();nameField.gameObject.SetActive(false);nameField.gameObject.SetActive(true);Role(nameField.textComponent,TypographyRole.Input);Expect(nameField.text==LongName&&Writes==nameWrites,"Name reentry changed text/storage");});
        }
        Add("Finite corpus on all six primary faces",()=>{
            var corpus=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("output/ui-update/fonts/FONT-01/glyph-corpus.json"))["primary_codepoints"].Select(x=>(int)x).ToArray();
            var set=Resources.Load<ConceptFontSet>("UI/ConceptFontSet");var fonts=new[]{set.robotoRegular,set.robotoBold,set.condensedRegular,set.condensedMedium,set.condensedSemiBold,set.condensedBold};
            Expect(corpus.All(code=>code<=char.MaxValue),"Corpus needs a supplementary-plane API");
            foreach(var font in fonts)foreach(var code in corpus)Expect(font.HasCharacter((char)code,true,false),font.name+" missing U+"+code.ToString("X4"));
            foreach(var child in Root.Cast<Transform>().Where(t=>t.gameObject.activeSelf))child.gameObject.SetActive(false);
            UI01Validation.SetGameView(1920,1080);resolution="1920x1080";
            sheet=new GameObject("FONT10Corpus",typeof(RectTransform),typeof(UnityEngine.UI.Image));sheet.transform.SetParent(Root,false);var rect=(RectTransform)sheet.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(1920,1080);sheet.GetComponent<UnityEngine.UI.Image>().color=GamePalette.Surface;
            var roles=new[]{TypographyRole.Body,TypographyRole.ScreenTitle,TypographyRole.Caption,TypographyRole.Label,TypographyRole.Button,TypographyRole.SectionTitle};
            for(int i=0;i<fonts.Length;i++){var go=new GameObject(fonts[i].name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(sheet.transform,false);var text=go.GetComponent<TMP_Text>();var r=text.rectTransform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(20+i%2*960,-20-i/2*350);r.sizeDelta=new Vector2(920,340);GameTypography.Apply(text,roles[i],24);text.color=GamePalette.Ink;text.textWrappingMode=TextWrappingModes.Normal;text.text=fonts[i].name+"\n"+string.Concat(corpus.Select(char.ConvertFromUtf32));}
        });
        Add("Rendered corpus proof",()=>{foreach(var t in sheet.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();Expect(!t.isTextOverflowing,"Corpus sheet overflow");}string path=UI01Validation.Capture(UiValidationContext.CaptureName("FONT-10","corpus-six-faces-1920x1080"));UiValidationContext.OnCapture?.Invoke("corpus-six-faces",path);UnityEngine.Object.Destroy(sheet);});
        due=EditorApplication.timeSinceStartup+.3;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    // PLAN_BODY is copied verbatim from the accepted FONT-08 wide-value fixture.
    static void Plan(){
        steps.Clear();
        foreach(var size in Sizes)
        {
            var s=size;
            Add("Focused viewport "+s,()=>{resolution=s.x+"x"+s.y;UI01Validation.SetGameView(s.x,s.y);});
            Add("Persisted wide monetary fixture",()=>{
                typeof(UI14QuarterlyResultValidation).GetMethod("SavedCase",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{3,"positive"});
                foreach(var result in new RoundResultRepository(DatabaseInitializer.DatabaseService.Connection).GetBySessionId(GameSessionState.Current.sessionId))
                {
                    result.grossRevenue=1234567.75f; result.netResult=result.round==2?1234567.75f:-1234567.75f;
                    result.openingCash=1234567.75f; result.closingCash=-2469135.50f;
                    result.alignmentClassification="Classificação histórica extensa para conferência de linhas e colunas";
                    DatabaseInitializer.DatabaseService.Connection.Update(result);
                }
                Root.Find("Panel_QuarterlyResult").GetComponent<QuarterlyResultController>().Refresh();
            });
            Add("Wide quarterly columns and read only refresh",()=>{
                var controller=Root.Find("Panel_QuarterlyResult").GetComponent<QuarterlyResultController>();
                int before=Writes;controller.Refresh();Expect(before==Writes,"Quarterly consultation wrote SQLite");Capture("wide-quarterly-top");
                controller.GetComponent<QuarterlyResultView>().TableScroll.verticalNormalizedPosition=.4f;
            });
            Add("Wide quarterly details and reentry",()=>{
                Capture("wide-quarterly-details");
                var q=Root.Find("Panel_QuarterlyResult");int before=Writes;q.gameObject.SetActive(false);q.gameObject.SetActive(true);
                q.GetComponent<QuarterlyResultController>().Refresh();Expect(before==Writes,"Quarterly reentry wrote SQLite");
                UIStateListener.PresentScreen(Root.Find("Panel_MonthlyResult").gameObject);
            });
            Add("Wide monthly persisted values",()=>{
                var controller=Root.Find("Panel_MonthlyResult").GetComponent<MonthlyResultController>();
                int before=Writes;controller.Refresh();Expect(before==Writes,"Monthly consultation wrote SQLite");Capture("wide-monthly-top");
                controller.GetComponent<MonthlyResultView>().TableScroll.verticalNormalizedPosition=0;
            });
            Add("Wide monthly chart and reconciliation",()=>{Capture("wide-monthly-details");});
            Add("Wide final report query",()=>{
                States.ForceState(GameState.FinalReport);
                var c=Root.Find("Panel_FinalReport").GetComponent<FinalReportController>();
                int before=Writes;c.Refresh();Expect(before==Writes,"Final consultation wrote SQLite");
            });
            Add("Wide final report capture and reentry",()=>{
                Capture("wide-final");var p=Root.Find("Panel_FinalReport");int before=Writes;
                p.gameObject.SetActive(false);p.gameObject.SetActive(true);
                Expect(before==Writes,"Final reentry wrote SQLite");
            });
            Add("Recorded bankrupt wide fixture",()=>{
                typeof(UI16BankruptcyValidation).GetMethod("Complete",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{false});
                var db=DatabaseInitializer.DatabaseService.Connection;
                var result=new RoundResultRepository(db).GetBySessionAndRound(GameSessionState.Current.sessionId,3);
                result.netResult=result.closingCash=-1234567.75f;db.Update(result);
                States.ForceState(GameState.GameOver_Bankruptcy);
                var c=Root.Find("Panel_Bankruptcy").GetComponent<BankruptcyController>();int before=Writes;
                c.Refresh();Expect(before==Writes,"Bankruptcy consultation wrote SQLite");
            });
            Add("Wide bankruptcy capture and reentry",()=>{
                Capture("wide-bankruptcy");var p=Root.Find("Panel_Bankruptcy");int before=Writes;
                p.gameObject.SetActive(false);p.gameObject.SetActive(true);
                Expect(before==Writes,"Bankruptcy reentry wrote SQLite");
            });
            Add("Final and bankruptcy enable cycles preserve writes",()=>{
                int before=Writes;
                foreach(var panel in new[]{"Panel_MonthlyResult","Panel_QuarterlyResult","Panel_FinalReport","Panel_Bankruptcy"})
                {
                    var p=Root.Find(panel);bool enabled=p.gameObject.activeSelf;
                    foreach(var t in p.GetComponentsInChildren<TMP_Text>(true))
                    {
                        var binding=t.GetComponent<TypographyRoleBinding>();Expect(binding!=null,"Unbound text "+PathOf(t.transform));
                        Role(t,binding.Role);binding.Reapply();Role(t,binding.Role);
                    }
                    p.gameObject.SetActive(false);p.gameObject.SetActive(true);p.gameObject.SetActive(enabled);
                }
                Expect(before==Writes,"Typography/enable caused a SQLite write");
            });
        }
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||Time.frameCount==frame||EditorApplication.timeSinceStartup<due)return;frame=Time.frameCount;due=EditorApplication.timeSinceStartup+.3;
        if(steps.Count==0){Finish(null);return;}var step=steps.Dequeue();try{step.Item2();checks.Add(new{name=step.Item1,passed=true,frame});}catch(Exception e){checks.Add(new{name=step.Item1,passed=false,error=e.ToString(),frame});Finish(e.ToString());}
    }
    static void Finish(string error){EditorApplication.update-=Tick;File.WriteAllText(UiValidationContext.OutputRoot+"/playmode.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{completed=error==null,error,checks},Newtonsoft.Json.Formatting.Indented));}
}
