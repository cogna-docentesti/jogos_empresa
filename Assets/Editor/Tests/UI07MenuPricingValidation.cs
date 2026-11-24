using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Adapter.In.UI;
using Game.Adapter.In.Controllers;
using Game.Adapter.In.UI.Navigation;
using Game.Infrastructure.Session;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Button = UnityEngine.UI.Button;
using ScrollRect = UnityEngine.UI.ScrollRect;

public static class UI07MenuPricingValidation
{
    [Serializable] private class Check {public string name;public bool passed;public string detail;public int frame;}
    [Serializable] private class Report {public List<Check> checks=new();public bool completed;public int failures;}
    private static Report report;private static readonly Queue<(string,Action)> steps=new();private static double next;
    private static int managerId,databaseId,sceneHandle;private static string confirmedJson,sessionId;private static GameObject hubOrigin;private static ProductData longProduct;
    private static MenuPricingScreenView View=>UnityEngine.Object.FindFirstObjectByType<MenuPricingScreenView>(FindObjectsInactive.Include);
    private static MenuPricingScreenController Controller=>View.GetComponent<MenuPricingScreenController>();
    private static ProductPricingItemView[] Items=>View.GetComponentsInChildren<ProductPricingItemView>().ToArray();
    private static GameState State=>GameManager.Instance.StateMachine.CurrentState;
    private static ScrollRect ProductScroll => Field<ScrollRect>(View, "productsScrollRect");
    private static Touchscreen touchDevice;
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    private static void Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);
    private static UnityEngine.UI.Button Action(string n)=>Field<UnityEngine.UI.Button>(View,n);
    private static Button[] Adjustments(ProductPricingItemView item) => new[] {
        Field<Button>(item, "decreaseButton"), Field<Button>(item, "decreaseHalfButton"),
        Field<Button>(item, "increaseHalfButton"), Field<Button>(item, "increaseButton")
    };
    private static void Expect(bool v,string message){if(!v)throw new InvalidOperationException(message);}
    private static void Add(string n,Action a)=>steps.Enqueue((n,a));
    private static void Capture(string n)
    { var path=UI01Validation.Capture(UiValidationContext.CaptureName("UI-07",n)); UiValidationContext.OnCapture?.Invoke(n,path); }
    private static PointerEventData Pointer(UnityEngine.UI.Selectable b,int id=-1)
    {var target=b.transform.Find("MarkerTouchTarget");var r=(RectTransform)(target!=null?target:b.transform);return new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center)),pointerId=id,button=PointerEventData.InputButton.Left};}
    private static GameObject Hit(PointerEventData pointer)
    {
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        return results.Count == 0 ? null : results[0].gameObject;
    }
    private static void Click(UnityEngine.UI.Button b,int id=-1)
    {
        Expect(b.IsActive() && b.interactable,"Inactive control "+b.name);var p=Pointer(b,id);var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(p,hits);
        Expect(hits.Count>0 && hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==b,"Blocked hit "+b.name);
        ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,p,ExecuteEvents.pointerClickHandler);
    }
    private static void NoPersistence()
    {
        Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==0,"Persisted before confirmation");
        Expect(GameManager.Instance.GetInstanceID()==managerId && DatabaseInitializer.DatabaseService.GetInstanceID()==databaseId,"Services replaced");
        Expect(UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle==sceneHandle,"Scene reloaded");
    }
    private static void Fits()
    {
        Canvas.ForceUpdateCanvases();
        foreach(var t in View.GetComponentsInChildren<TMP_Text>())
        {
            t.ForceMeshUpdate();Expect(t.GetComponent<Game.Adapter.In.UI.Theme.TypographyRoleBinding>() != null && t.font == Game.Adapter.In.UI.Theme.GameTypography.Resolve(t.GetComponent<Game.Adapter.In.UI.Theme.TypographyRoleBinding>().Role).Font && !t.enableAutoSizing,"Native typography "+t.name);
            if(t.GetComponentInParent<TMP_InputField>()==null)Expect(!t.isTextOverflowing,"Overflow "+t.name+" "+t.text);
        }
        foreach(var scroll in View.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())
        {
            Expect(scroll == ProductScroll
                ? scroll.viewport.GetComponent<UnityEngine.UI.RectMask2D>() != null
                : scroll.viewport.GetComponent<UnityEngine.UI.Mask>() != null, "Missing mask");
            Expect(scroll.verticalScrollbar.handleRect.sizeDelta==Vector2.zero,"Handle size");
            Expect(!Action("backButton").transform.IsChildOf(scroll.transform),"Back inside scroll");
        }
    }
    private static void Matches(RestaurantType type)
    {
        var data=Resources.LoadAll<RestaurantData>("Restaurants").Single(r=>r.type==type);
        Expect(State==InitialDecisionFlow.Menu && Items.Length==3,"Wrong screen/products");
        Expect(Items.Select(i=>i.Product.id).SequenceEqual(data.products.Select(p=>p.id)),"Wrong product order/IDs");
        var labels = new[] { "−1", "−0,5", "+0,5", "+1" };
        foreach(var i in Items)
        {
            Expect(i.IsPriceValid && i.Product.IsPriceInRange(i.SelectedPrice),"Invalid price");
            var image=Field<UnityEngine.UI.Image>(i,"productImage");Expect(image.sprite==i.Product.productSprite && image.sprite.name==i.Product.id.ToLowerInvariant(),"Wrong product image");
            Expect(image.color==Color.white && image.preserveAspect && image.type==UnityEngine.UI.Image.Type.Simple,"Distorted/tinted image");
            Expect(Field<TMP_Text>(i,"productNameText").text==i.Product.displayName,"Wrong product name");
            Expect(Field<TMP_InputField>(i,"priceInput").text.EndsWith(",00") || Field<TMP_InputField>(i,"priceInput").text.Contains(","),"Decimal format");
            Expect(i.GetComponentsInChildren<UnityEngine.UI.Slider>(true).Length == 0, "Continuous price control");
            var adjust = Adjustments(i);
            Expect(adjust.Select(b => b.GetComponentInChildren<TMP_Text>().text).SequenceEqual(labels), "Adjustment labels");
            var price = (RectTransform)Field<TMP_InputField>(i, "priceInput").transform;
            var card = (RectTransform)i.transform;
            var previousRight = card.rect.xMin;
            foreach (var button in adjust)
            {
                var corners = new Vector3[4];
                ((RectTransform)button.transform).GetWorldCorners(corners);
                var lower = card.InverseTransformPoint(corners[0]);
                var upper = card.InverseTransformPoint(corners[2]);
                Expect(lower.x >= previousRight && lower.y >= card.rect.yMin && upper.x <= card.rect.xMax &&
                    upper.y < card.InverseTransformPoint(price.TransformPoint(price.rect.min)).y, "Adjustment layout");
                previousRight = upper.x;
            }
        }
        Fits();NoPersistence();
    }
    private static void Choose(RestaurantType type)
    {
        var restaurant=UnityEngine.Object.FindFirstObjectByType<RestaurantScreenView>();
        Click(Field<UnityEngine.UI.Button>(restaurant,type==RestaurantType.PODRAO?"cardPodrao":type==RestaurantType.JAPONES?"cardJapones":"cardFrances"));
        Click(Field<UnityEngine.UI.Button>(restaurant,"confirmButton"));
    }
    public static void Run()
    {
        UI01Validation.RequireIsolatedStorage();Expect(EditorApplication.isPlaying,"Use isolated Play Mode");
        report=new Report();steps.Clear();managerId=GameManager.Instance.GetInstanceID();databaseId=DatabaseInitializer.DatabaseService.GetInstanceID();sceneHandle=UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
        Add("Real identification form starts the decisions",()=>{
            Expect(State==InitialDecisionFlow.Identification,"Clean identification required");var form=UnityEngine.Object.FindFirstObjectByType<UIIndentification>();
            Field<TMP_InputField>(form,"studentNameInput").text="Aluna Teste UI07";Field<TMP_InputField>(form,"raInput").text="123456";form.Register();NoPersistence();
        });
        Add("Location choice stays in memory",()=>{var v=UnityEngine.Object.FindFirstObjectByType<LocationScreenView>();var marker=v.Zones.Single(z=>z.id=="bank").marker;ExecuteEvents.Execute(marker.gameObject,new PointerEventData(EventSystem.current){pointerId=4},ExecuteEvents.pointerClickHandler);Click(Field<UnityEngine.UI.Button>(v,"confirmButton"));NoPersistence();});
        Add("Restaurant choice opens pricing",()=>Choose(RestaurantType.PODRAO));
        foreach(var sizeValue in UiValidationContext.Resolutions)
        {
            var size=sizeValue;var suffix=size.x+"x"+size.y;
            Add("Set viewport "+suffix,()=>UI01Validation.SetGameView(size.x,size.y));
            foreach(var typeValue in new[]{RestaurantType.PODRAO,RestaurantType.JAPONES,RestaurantType.FRANCES})
            {
                var type=typeValue;
                Add("Back from D3 to D2 "+type+" "+suffix,()=>Click(Action("backButton")));
                Add("Select matching restaurant "+type+" "+suffix,()=>Choose(type));
                Add("Rendered products, prices and adjustment controls "+type+" "+suffix,()=>{
                    Expect(Screen.width==size.x && Screen.height==size.y,"Wrong evidence resolution");Matches(type);
                    Expect(Field<TMP_Text>(View,"stepText").text == "CARDÁPIO","Wrong menu heading");
                    EventSystem.current.SetSelectedGameObject(null);Capture(type.ToString().ToLowerInvariant()+"-"+suffix);
                });
                foreach(int indexValue in new[]{0,1,2})
                {
                    int index=indexValue;
                    Add("Inclusive limits, decimals and increments "+type+" product "+index+" "+suffix,()=>{
                        var i=Items[index];var input=Field<TMP_InputField>(i,"priceInput");
                        var adjust = Adjustments(i);
                        input.text=i.Product.minPrice.ToString("F2",System.Globalization.CultureInfo.InvariantCulture);Expect(i.IsPriceValid && Mathf.Approximately(i.SelectedPrice,i.Product.minPrice),"Minimum");Expect(!Field<UnityEngine.UI.Button>(i,"decreaseButton").interactable,"Minimum decrement enabled");
                        Expect(!adjust[1].interactable, "Minimum half decrement enabled");
                        adjust[2].onClick.Invoke();Expect(i.SelectedPrice==i.Product.minPrice+.5f && !adjust[0].interactable && adjust[1].interactable,"Half increment at minimum");
                        adjust[1].onClick.Invoke();Expect(i.SelectedPrice==i.Product.minPrice,"Half decrement to minimum");
                        Field<UnityEngine.UI.Button>(i,"increaseButton").onClick.Invoke();Expect(Mathf.Approximately(i.SelectedPrice,i.Product.minPrice+1),"Increment");
                        input.text=i.Product.maxPrice.ToString("F2",System.Globalization.CultureInfo.InvariantCulture).Replace('.',',');Expect(i.IsPriceValid && Mathf.Approximately(i.SelectedPrice,i.Product.maxPrice),"Maximum");Expect(!Field<UnityEngine.UI.Button>(i,"increaseButton").interactable,"Maximum increment enabled");
                        Expect(!adjust[2].interactable, "Maximum half increment enabled");
                        adjust[1].onClick.Invoke();Expect(i.SelectedPrice==i.Product.maxPrice-.5f && !adjust[3].interactable && adjust[2].interactable,"Half decrement at maximum");
                        adjust[2].onClick.Invoke();Expect(i.SelectedPrice==i.Product.maxPrice,"Half increment to maximum");
                        Field<UnityEngine.UI.Button>(i,"decreaseButton").onClick.Invoke();Expect(Mathf.Approximately(i.SelectedPrice,i.Product.maxPrice-1),"Decrement");
                        input.text = i.Product.price + ",37";
                        adjust[2].onClick.Invoke();
                        Expect(i.SelectedPrice == (float)(i.Product.price + .87m), $"Off-grid increment: {i.SelectedPrice:R}");
                        adjust[1].onClick.Invoke();
                        Expect(i.SelectedPrice == (float)(i.Product.price + .37m), $"Off-grid round trip: {i.SelectedPrice:R}");
                        NoPersistence();
                    });
                }
            }
        }
        Add("Set scroll evidence viewport",()=>UI01Validation.SetGameView(1280,720));
        Vector2 touchStart = default;
        float[] touchPrices = null;
        Add("Start touch drag over an enabled price adjustment", () => {
            var button = Adjustments(Items[1])[2];
            var pointer = Pointer(button);
            Expect(button.interactable && Hit(pointer)?.GetComponentInParent<Button>() == button, "Touch start hit");
            touchStart = pointer.position;
            touchPrices = Items.Select(i => i.SelectedPrice).ToArray();
            touchDevice = InputSystem.AddDevice<Touchscreen>();
            QueueTouch(UnityEngine.InputSystem.TouchPhase.Began, touchStart);
        });
        Add("Move touch past the drag threshold", () =>
            QueueTouch(UnityEngine.InputSystem.TouchPhase.Moved, touchStart + Vector2.up * 20));
        Add("Drag touch up to reveal the last product", () =>
            QueueTouch(UnityEngine.InputSystem.TouchPhase.Moved, touchStart + Vector2.up * 160));
        Add("Release the touch", () =>
            QueueTouch(UnityEngine.InputSystem.TouchPhase.Ended, touchStart + Vector2.up * 160));
        Add("Touch drag scrolls the list without firing the adjustment", () => {
            Expect(ProductScroll.verticalNormalizedPosition <= .01f, "Touch did not scroll to the last product");
            Expect(Items.Select(i => i.SelectedPrice).SequenceEqual(touchPrices), "Touch drag changed a price");
            Capture("price-touch-scroll-1280x720");
            NoPersistence();
        });
        Add("Wheel on the price controls scrolls the list without changing prices",()=>{
            Canvas.ForceUpdateCanvases();
            var scroll = ProductScroll;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1;
            var prices = Items.Select(i => i.SelectedPrice).ToArray();
            var lastButton = Adjustments(Items[2])[3];
            Expect(Hit(Pointer(lastButton))?.GetComponentInParent<Button>() != lastButton, "Clipped control receives clicks");
            foreach (var control in new UnityEngine.UI.Selectable[] {
                Adjustments(Items[0])[2], Field<TMP_InputField>(Items[0], "priceInput")
            })
            {
                var pointer = Pointer(control);
                pointer.scrollDelta = Vector2.down;
                var position = scroll.verticalNormalizedPosition;
                var hit = Hit(pointer);
                Expect(hit != null && hit.GetComponentInParent<UnityEngine.UI.Selectable>() == control, "Wheel start hit");
                ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.scrollHandler);
                Expect(scroll.verticalNormalizedPosition < position, "Wheel did not scroll over " + control.name);
            }
            Expect(Items.Select(i => i.SelectedPrice).SequenceEqual(prices), "Wheel changed a price");
            NoPersistence();
        });
        Add("Bottom of scrolled list offers all four buttons on the last card",()=>{
            Expect(ProductScroll.verticalNormalizedPosition <= .001f, "Wheel did not reach last product");
            EventSystem.current.SetSelectedGameObject(null);
            Capture("price-scroll-bottom-1280x720");
            var item = Items[2];
            var price = item.SelectedPrice;
            foreach (var button in Adjustments(item)) Click(button);
            Expect(price == item.SelectedPrice, "Bottom buttons changed the expected total");
            NoPersistence();
            ProductScroll.verticalNormalizedPosition = 1;
        });
        Add("Set interaction viewport",()=>UI01Validation.SetGameView(1920,1080));
        foreach(var amountValue in new[]{0f,.35f,1f})
        {
            var amount=amountValue;string label=Mathf.RoundToInt(amount*100).ToString();
            Add("Prepare visual coherence fixture "+label,()=>View.UpdatePriceCoherence(amount,"Conferência visual da barra, sem alterar a fórmula."));
            Add("Rendered coherence fill "+label,()=>{var fill=Field<UnityEngine.UI.Image>(View,"priceCoherenceBarFill");Expect(Mathf.Approximately(fill.rectTransform.anchorMax.x,amount),"Coherence fill");Fits();Capture("coherence-"+label+"-1920x1080");});
        }
        Add("Restore baseline price coherence",()=>Call(Controller,"UpdateCoherencePanel"));
        Add("Prepare input focus and edit selection",()=>{var input=Field<TMP_InputField>(Items[0],"priceInput");input.Select();input.ActivateInputField();});
        Add("Rendered focus, selection and mask",()=>{
            var i=Items[0];Expect(Field<TMP_InputField>(i,"priceInput").isFocused,"Input not focused");Expect(Field<UnityEngine.UI.Image>(i,"editingOutline").enabled,"Missing editing selection");Fits();Capture("input-focus-selected-1920x1080");
            Field<TMP_InputField>(i,"priceInput").text="9999999999999999";
        });
        Add("Long invalid input blocks confirmation and preserves last valid amount",()=>{
            var i=Items[0];Expect(!i.IsPriceValid && i.Product.IsPriceInRange(i.SelectedPrice),"Invalid lost valid amount");Expect(!Action("confirmButton").interactable,"Invalid enabled confirmation");Expect(Field<TMP_InputField>(i,"priceInput").GetComponent<SharedControlAppearance>().IsInvalid,"Invalid appearance");
            Action("confirmButton").onClick.Invoke();NoPersistence();Fits();Capture("invalid-long-input-1920x1080");Click(Action("backButton"));
        });
        Add("Reopen same restaurant with invalid text pending",()=>Choose(RestaurantType.FRANCES));
        Add("Both draft text and last valid price survive back/reentry",()=>{
            var i=Items[0];Expect(i.DraftInput=="9999999999999999" && !i.IsPriceValid,"Pending input lost");Expect(Mathf.Approximately(i.SelectedPrice,i.Product.price+.37f),"Valid draft lost");Expect(!Action("confirmButton").interactable,"Pending invalid accepted");NoPersistence();Capture("rehydrated-invalid-1920x1080");
            foreach(var item in Items)Field<TMP_InputField>(item,"priceInput").text=item.Product.minPrice.ToString(System.Globalization.CultureInfo.InvariantCulture);
            EventSystem.current.SetSelectedGameObject(null);
        });
        Add("Minimum limits disable decrement and remain valid",()=>{Fits();Capture("minimum-disabled-1920x1080");foreach(var i in Items)Expect(!Field<UnityEngine.UI.Button>(i,"decreaseButton").interactable,"Min button");
            var b=Field<UnityEngine.UI.Button>(Items[0],"increaseButton");ExecuteEvents.Execute(b.gameObject,Pointer(b),ExecuteEvents.pointerEnterHandler);});
        Add("Rendered hover",()=>{var b=Field<UnityEngine.UI.Button>(Items[0],"increaseButton");Expect(b.GetComponent<SharedControlAppearance>().CurrentState=="hover","Hover");Capture("stepper-hover-1920x1080");ExecuteEvents.Execute(b.gameObject,Pointer(b),ExecuteEvents.pointerDownHandler);});
        Add("Rendered pressed",()=>{var b=Field<UnityEngine.UI.Button>(Items[0],"increaseButton");Expect(b.GetComponent<SharedControlAppearance>().CurrentState=="pressed","Pressed");Capture("stepper-pressed-1920x1080");ExecuteEvents.Execute(b.gameObject,Pointer(b),ExecuteEvents.pointerUpHandler);ExecuteEvents.Execute(b.gameObject,Pointer(b),ExecuteEvents.pointerExitHandler);Click(b,12);b.Select();});
        Add("Touch increments and keyboard focus",()=>{var i=Items[0];Expect(Mathf.Approximately(i.SelectedPrice,i.Product.minPrice+1),"Touch increment");Fits();Capture("stepper-focus-1920x1080");InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Enter));});
        Add("Enter activates selected increment",()=>{InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());Expect(Mathf.Approximately(Items[0].SelectedPrice,Items[0].Product.minPrice+2),"Keyboard increment");Field<TMP_InputField>(Items[1],"priceInput").Select();InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Tab));});
        Add("Tab leaves input for the next enabled control",()=>{InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());Expect(EventSystem.current.currentSelectedGameObject!=Field<TMP_InputField>(Items[1],"priceInput").gameObject,"Tab trapped in input");Click(Action("backButton"));});
        Add("Switching restaurant resets previous draft",()=>Choose(RestaurantType.PODRAO));
        Add("New restaurant has only its original defaults",()=>{Matches(RestaurantType.PODRAO);Expect(Items.All(i=>Mathf.Approximately(i.SelectedPrice,i.Product.price)),"Old draft leaked");});
        foreach(var sizeValue in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)})
        {
            var size=sizeValue;var suffix=size.x+"x"+size.y;
            Add("Set long-copy viewport "+suffix,()=>UI01Validation.SetGameView(size.x,size.y));
            Add("Prepare isolated long name and price "+suffix,()=>{
                longProduct=UnityEngine.Object.Instantiate(Items[0].Product);longProduct.displayName=string.Join(" ",Enumerable.Repeat("Combo completo com nome extenso para conferir a leitura e a rolagem",4));longProduct.maxPrice=9999999;longProduct.minPrice=1;
                Items[0].Setup(longProduct,1234567.5f);Call(Controller,"UpdateCoherencePanel");foreach(var s in View.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())s.verticalNormalizedPosition=1;EventSystem.current.SetSelectedGameObject(null);
            });
            Add("Long name expands card inside mask; long price retains decimals "+suffix,()=>{
                Fits();Expect(Field<TMP_InputField>(Items[0],"priceInput").text=="1234567,50","Long price");Expect(((RectTransform)Items[0].transform).rect.height>228,"Name not reflowed");Capture("long-copy-top-"+suffix);
                foreach(var s in View.GetComponentsInChildren<UnityEngine.UI.ScrollRect>())s.verticalNormalizedPosition=0;
            });
            Add("Bottom scroll exposes every product and summary "+suffix,()=>{
                Fits();Capture("long-copy-bottom-"+suffix);var scroll=Field<UnityEngine.UI.ScrollRect>(View,"productsScrollRect");var before=scroll.verticalNormalizedPosition;
                ExecuteEvents.Execute(scroll.gameObject,new PointerEventData(EventSystem.current){scrollDelta=Vector2.up},ExecuteEvents.scrollHandler);Expect(scroll.verticalNormalizedPosition>before,"Wheel scroll");
                var lastInput=Field<TMP_InputField>(Items[2],"priceInput");lastInput.Select();
            });
            Add("Keyboard focus reveals last card and footer stays reachable "+suffix,()=>{Fits();Capture("last-product-focus-"+suffix);Call(Controller,"PopulateProducts");UnityEngine.Object.Destroy(longProduct);});
        }
        Add("Prepare unavailable menu fixture",()=>{Call(Controller,"ClearItems");Call(Controller,"ValidatePrices");Call(Controller,"UpdateCoherencePanel");});
        Add("Unavailable menu has honest message and cannot persist",()=>{
            Expect(!Action("confirmButton").interactable && Field<TMP_Text>(View,"hintText").text.Contains("indisponível"),"Empty feedback");Action("confirmButton").onClick.Invoke();NoPersistence();Fits();Capture("unavailable-1280x720");Click(Action("backButton"));
        });
        Add("Final real restaurant selection",()=>Choose(RestaurantType.JAPONES));
        Add("Final price entry before unique confirmation",()=>{
            Matches(RestaurantType.JAPONES);
            foreach (var i in Items)
            {
                Field<TMP_InputField>(i,"priceInput").text=i.Product.price+",25";
                Adjustments(i)[2].onClick.Invoke();
            }
            Click(Action("backButton"));
        });
        Add("Reopen restaurant with valid off-grid adjustments",()=>Choose(RestaurantType.JAPONES));
        Add("Adjusted draft prices survive Back and reentry",()=>{
            Expect(Items.All(i=>i.SelectedPrice==i.Product.price+.75f),"Adjusted draft price lost");
            NoPersistence();
        });
        Add("Confirm D3 is first session write",()=>{NoPersistence();Click(Action("confirmButton"));});
        Add("One session, same scene and next panel Banco",()=>{
            var rows=DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().ToList();Expect(rows.Count==1 && GameSessionState.IsPersisted,"Unique persistence");Expect(State==InitialDecisionFlow.AfterCommit,"Next state is not Banco");
            Expect(rows[0].restaurantType==RestaurantType.JAPONES && rows[0].locationZone==LocationZone.Financas && rows[0].studentName=="Aluna Teste UI07","Persisted decisions");
            Expect(MenuPricingHelper.FromJson(rows[0].menuPricingJson).items.All(p=>Resources.LoadAll<ProductData>("Products").Any(d=>d.id==p.productId && p.selectedPrice==d.price+.75f)),"Persisted prices");
            Expect(UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle==sceneHandle,"Scene changed");confirmedJson=rows[0].menuPricingJson;sessionId=rows[0].sessionId;Capture("confirmed-next-bank-1280x720");
            Action("confirmButton").onClick.Invoke();Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==1,"Duplicate callback");
            GameManager.Instance.StateMachine.TryChangeState(GameState.Initial_Equipment);GameManager.Instance.StateMachine.TryChangeState(GameState.Initial_Team);GameManager.Instance.StateMachine.TryChangeState(GameState.Management_Hub);
        });
        Add("Hub root renders before map interaction",()=>{Expect(State==GameState.Management_Hub,"Hub state");MenuNavigator.Instance.OpenRoot();});
        Add("Hub opens cardapio through navigator",()=>{var origin=UnityEngine.Object.FindObjectsByType<MapMenuNode>(FindObjectsSortMode.None).Single(n=>n.Destination==PanelId.MenuPricing);hubOrigin=origin.gameObject;origin.GetComponent<UnityEngine.UI.Button>().Select();Click(origin.GetComponent<UnityEngine.UI.Button>());});
        foreach(var sizeValue in UiValidationContext.Resolutions)
        {
            var size=sizeValue;var suffix=size.x+"x"+size.y;
            Add("Set review viewport "+suffix,()=>UI01Validation.SetGameView(size.x,size.y));
            Add("Review keeps saved prices, context and disabled editors "+suffix,()=>{
                Fits();Expect(State==GameState.Management_Hub && MenuNavigator.Instance.Current==PanelId.MenuPricing,"Review context");Expect(!Action("confirmButton").gameObject.activeSelf && !Action("backButton").gameObject.activeSelf && UnityEngine.GameObject.Find("Canvas/HubHistory/Back")!=null,"Review actions");
                Expect(Field<TMP_Text>(View,"stepText").text.Contains("REVISÃO"),"Review title");Expect(Field<TMP_Text>(View,"strategyHelpText").text.Contains("confirmados") && Field<TMP_Text>(View,"inputHelpText").text.Contains("Consulte") && !Field<TMP_Text>(View,"inputHelpText").text.Contains("Digite"),"Review instructions");
                foreach(var i in Items){Expect(!Field<TMP_InputField>(i,"priceInput").interactable && Adjustments(i).All(b => !b.interactable),"Editable review");Expect(Mathf.Approximately(i.SelectedPrice,i.Product.price+.75f),"Saved price changed");}
                Capture("hub-review-"+suffix);
            });
        }
        Add("Review back preserves hub history",()=>Click(UnityEngine.GameObject.Find("Canvas/HubHistory/Back").GetComponent<UnityEngine.UI.Button>()));
        Add("Return to hub without another session or mutations",()=>{
            Expect(MenuNavigator.Instance.Current==PanelId.Menu && State==GameState.Management_Hub,"Hub return");Expect(EventSystem.current.currentSelectedGameObject==hubOrigin,"Hub focus not restored");var row=DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Single();Expect(row.sessionId==sessionId && row.menuPricingJson==confirmedJson,"Review mutated session");
            MenuNavigator.Instance.Open(PanelId.MenuPricing);
        });
        Add("Review reentry is stable",()=>{Expect(Items.Length==3 && Items.All(i=>Mathf.Approximately(i.SelectedPrice,i.Product.price+.75f)),"Reentry");Expect(DatabaseInitializer.DatabaseService.Connection.Table<GameSessionEntity>().Count()==1,"Review insert");
            File.WriteAllText(UiValidationContext.FileFor("UI-07","runtime-scene.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new{project=UnityEngine.Application.dataPath,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,frame=Time.frameCount,managerId,databaseId,sessionRows=1,database=DatabaseInitializer.DatabaseService.Connection.DatabasePath,menuJson=confirmedJson,state=State.ToString(),panel=MenuNavigator.Instance.Current.ToString()},Newtonsoft.Json.Formatting.Indented));
        });
        next=EditorApplication.timeSinceStartup+.5;EditorApplication.update-=Tick;EditorApplication.update+=Tick;
    }
    private static void QueueTouch(UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
        => InputSystem.QueueStateEvent(touchDevice, new TouchState { touchId = 1, phase = phase, position = position });

    private static void Tick()
    {
        if(EditorApplication.timeSinceStartup<next)return;
        if(!EditorApplication.isPlaying || steps.Count==0){if(touchDevice != null){InputSystem.RemoveDevice(touchDevice);touchDevice=null;}report.completed=steps.Count==0 && report.failures==0;EditorApplication.update-=Tick;File.WriteAllText(UiValidationContext.FileFor("UI-07","playmode-menu-pricing.json"),JsonUtility.ToJson(report,true));return;}
        var step=steps.Dequeue();var check=new Check{name=step.Item1,frame=Time.frameCount};
        try{step.Item2();check.passed=true;}catch(Exception e){check.detail=e.ToString();report.failures++;steps.Clear();Debug.LogError("[UI-07 QA] "+check.name+": "+e.Message);}
        report.checks.Add(check);EditorApplication.QueuePlayerLoopUpdate();EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Repaint();next=EditorApplication.timeSinceStartup+.5;
    }
}
