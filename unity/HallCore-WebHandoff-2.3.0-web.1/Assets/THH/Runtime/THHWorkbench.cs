using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace HallLab
{
    public sealed partial class THHWorkbench : MonoBehaviour
    {
        public Camera view;
        public InspectionCamera orbit;
        public Transform sampleCarriage;
        public Transform[] switchBlades;
        public GameObject externalLeads;
        public TextMesh currentDisplay, voltageDisplay;
        public Material positiveLead, negativeLead;
        public THHCircuit Circuit { get; private set; } = new THHCircuit();
        public HallExperimentModel ExperimentModel { get; private set; } = new HallExperimentModel();
        public bool PhysicalModelEnabled { get; private set; }
        public bool PreviewInitialized => initialized;
        public Pin? SelectedPin { get; private set; }
        private Vector3 carriageHome;
        private CircuitTerminal[] terminals;
        private Text selectedTitle, selectedBody, selectedEvidence, stateText, pathText, selectionText, powerText;
        private readonly Text[] switchTexts = new Text[3], pinTexts = new Text[12], cableTexts = new Text[12];
        private Text currentCaption, magnetCaption, meterText, modelButton;
        private Text recordButton, dataTableText, fitText;
        private RawImage curveImage;
        private Texture2D curveTexture;
        private RectTransform dataPanel;
        private bool dataVisible;
        private Slider isSlider, imSlider, xSlider, ySlider;
        private RectTransform wirePanel, detailPanel;
        private Font font;
        private bool initialized, showIm;
        private float stageX, stageY;
        private string recentMessage;
        private float recentMessageUntil;
        private float nextReadoutRefresh;
        // Shared presentation tokens; equipment materials are independent.
        private static readonly Color Ink = new Color(.13f,.17f,.20f), Muted = new Color(.40f,.46f,.49f);
        private static readonly Color PanelColor = new Color(.985f,.99f,.995f);
        private static readonly Color PanelAlt = new Color(.94f,.96f,.97f);
        private static readonly Color ButtonColor = new Color(.93f,.95f,.96f);
        private static readonly Color AccentColor = new Color(.035f,.43f,.38f);
        private static readonly Color Cyan = new Color(.02f,.40f,.36f);
        private static readonly Color LineColor = new Color(.87f,.90f,.91f);
        private static readonly Color WarningColor = new Color(.68f,.22f,.12f);

        private void Start() { InitializePreview(); }
        public void InitializePreview()
        {
            if (initialized) return;
            initialized = true; carriageHome = sampleCarriage.localPosition;
            terminals = GetComponentsInChildren<CircuitTerminal>();
#if UNITY_WEBGL && !UNITY_EDITOR
            font = Resources.Load<Font>("Fonts/NotoSansSC-Regular");
            foreach(var label in GetComponentsInChildren<TextMesh>(true))label.font=font;
#else
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" }, 20);
#endif
            if (EventSystem.current == null) {var events=new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));if(!Application.isPlaying)events.hideFlags=HideFlags.DontSave;}
            BuildInterface(); ResetControls(); Select(GetComponentInChildren<ReferencePart>());
            Canvas.ForceUpdateCanvases();
            InitializeTeaching();
            InitializeRecovery();
        }
        private void Update()
        {
            if (!initialized) return;
            if (Application.isPlaying)
            {
                if (Time.unscaledDeltaTime > .5f) ExperimentModel.InvalidateSampling();
                if (PhysicalModelEnabled) ExperimentModel.Tick(Circuit, Time.deltaTime);
                if (PhysicalModelEnabled && Time.unscaledTime>=nextReadoutRefresh) {nextReadoutRefresh=Time.unscaledTime+.08f;Refresh();}
            }
            if (Input.GetKeyDown(KeyCode.Escape)) { HandleEscape(); return; }
            if (HasOpenDialog) return;
            if(LessonActive)return;
            if (HandleInstrumentInput()) return;
            if (!Input.GetMouseButtonDown(0) || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;
            if (!Physics.Raycast(view.ScreenPointToRay(Input.mousePosition), out RaycastHit hit, 6)) return;
            var terminal = hit.collider.GetComponentInParent<CircuitTerminal>();
            if (terminal != null) { ChooseTerminal(terminal.pin); return; }
            var part = hit.collider.GetComponentInParent<ReferencePart>();
            if (part == null) return;
            Select(part);
            if(part.kind!=PartKind.IsSwitch && part.kind!=PartKind.VoltageSwitch && part.kind!=PartKind.ImSwitch)ShowTab(2);
            if (part.kind == PartKind.IsSwitch) ToggleSwitch(0);
            if (part.kind == PartKind.VoltageSwitch) ToggleSwitch(1);
            if (part.kind == PartKind.ImSwitch) ToggleSwitch(2);
        }
        public void Select(ReferencePart part)
        {
            if (part == null || selectedTitle == null) return;
            selectedTitle.text = part.title; selectedBody.text = part.description;
            selectedEvidence.text = "参照与边界\n" + part.evidence;
        }
        public void FocusView(int index)
        {
            index = Mathf.Clamp(index,0,3);
            orbit.Preset(index);
            for(int i=0;i<viewButtons.Length;i++)if(viewButtons[i]!=null)SetSelected(viewButtons[i],i==index);
            if(viewTitle != null) viewTitle.text = new[]{ "装置全景", "测试仪 · 面板细节", "实验箱 · 接线与开关", "样品 · 磁极间隙" }[index];
            PartKind kind = index == 2 ? PartKind.Case : index == 3 ? PartKind.Sample : PartKind.Tester;
            foreach (var part in GetComponentsInChildren<ReferencePart>()) if (part.kind == kind) { Select(part); break; }
        }
        public void ChooseTerminal(Pin pin)
        {
            if (RejectAutomaticEdit()) return;
            if (Circuit.Powered) { WebBridge.Instance?.Abnormal("LIVE_WIRING_CHANGE","接线");Refresh("请先归零断电，禁止带电改线"); return; }
            if (!THHCircuit.IsExternal(pin)) { Refresh("厂家固定端子不可拆接"); return; }
            if (!SelectedPin.HasValue) { SelectedPin = pin; Refresh("已选择起点，再点一个端子完成接线"); return; }
            if (SelectedPin.Value == pin) { CancelConnection(); return; }
            bool changed = Circuit.Add(SelectedPin.Value, pin, out string message);
            if(!changed)WebBridge.Instance?.Abnormal("WIRING_REJECTED",message);
            else if(Circuit.Evaluate().Hazard || Circuit.Evaluate().MeterShort)WebBridge.Instance?.CircuitFailure(Circuit.Evaluate());
            ExperimentModel.TrackOperation("接线",SelectedPin.Value+" -> "+pin+" / "+message,changed);
            SelectedPin = null; Refresh(message, changed);
        }
        public void CancelConnection() { SelectedPin = null; Refresh("已取消端子选择"); }
        public void StandardWiring()
        {
            if (RejectAutomaticEdit()) return;
            if(!ValidationLaunch){Refresh("请逐根连接端子，或观看分步演示");return;}
            bool changed = Circuit.ConnectStandard();
            ExperimentModel.TrackOperation("标准接线",changed?"成功":"带电拦截");
            if (changed) SelectedPin = null;
            Refresh(changed ? "已连接三对标准外接线；开关保持当前档位" : "请先归零断电，禁止带电自动接线", changed);
        }
        public void ClearWiring()
        {
            if (RejectAutomaticEdit()) return;
            bool changed = Circuit.Clear(); if (changed) SelectedPin = null;
            if(!changed)WebBridge.Instance?.Abnormal("LIVE_WIRING_CHANGE","清空接线");
            ExperimentModel.TrackOperation("清空接线",changed?"成功":"带电拦截",changed);
            Refresh(changed ? "学生外接线已清空，厂家固定线保留" : "请先归零断电，禁止带电拆线", changed);
        }
        public void RemoveCable(int id)
        {
            if (RejectAutomaticEdit()) return;
            bool changed = Circuit.Remove(id);
            if(!changed)WebBridge.Instance?.Abnormal(Circuit.Powered?"LIVE_WIRING_CHANGE":"CABLE_NOT_FOUND",id.ToString());
            ExperimentModel.TrackOperation("拆线",id+" / "+changed,changed);
            Refresh(changed ? "已移除外接线" : "无法拆线：请确认已断电且接线存在", changed);
        }
        public void UndoLastCable()
        {
            if (RejectAutomaticEdit()) return;
            if (Circuit.Cables.Count==0) { Refresh("当前没有可撤销的外接线"); return; }
            RemoveCable(Circuit.Cables[Circuit.Cables.Count-1].Id);
        }
        public void ToggleSwitch(int index)
        {
            int next = Circuit.Position(index) == 1 ? 0 : Circuit.Position(index) == 0 ? -1 : 1;
            SetSwitch(index, next);
        }
        public void SetSwitch(int index, int position)
        {
            if (RejectAutomaticEdit()) return;
            bool changed = Circuit.SetSwitch(index, position);
            if(!changed)WebBridge.Instance?.Abnormal("LIVE_SWITCH_CHANGE",index.ToString());
            if (changed) ExperimentModel.InvalidateSampling();
            ExperimentModel.TrackOperation("开关",index+" = "+position+" / "+changed,changed);
            Refresh(changed ? "开关已切换，回路状态已重新检查" : "请先归零断电，再操作开关");
        }
        public void TogglePower()
        {
            if (RejectAutomaticEdit()) return;
            string message;
            bool accepted=true;
            if (Circuit.Powered) { Circuit.TurnOff(); message = "两路设定已归零，输出已关闭"; }
            else if(!Circuit.TurnOn(out message)){accepted=false;WebBridge.Instance?.CircuitFailure(Circuit.Evaluate());}
            ExperimentModel.InvalidateSampling();
            ExperimentModel.TrackOperation("输出",message,accepted);
            SelectedPin = null; Refresh(message);
        }
        public void TogglePhysicalModel()
        {
            if (RejectAutomaticEdit()) return;
            PhysicalModelEnabled = !PhysicalModelEnabled;
            if (!PhysicalModelEnabled) ExperimentModel.Reset();
            ExperimentModel.TrackOperation("物理模型",PhysicalModelEnabled.ToString());
            Refresh(PhysicalModelEnabled ? "物理模型已启用：电压显示为计算读数" : "物理模型已关闭：电压显示 N/A");
        }
        public void RecordCurrentReading()
        {
            if(WebBridge.Instance!=null && WebBridge.Instance.Finished){WebBridge.Instance.Abnormal("SESSION_FINISHED","ResetExperiment is required.");return;}
            if (RejectAutomaticEdit()) return;
            if (!PhysicalModelEnabled) { Refresh("请先开启物理模型"); return; }
            ExperimentModel.RecordCurrentReading(Circuit);
            Refresh(ExperimentModel.LastRecordMessage);
        }
        public void SetCurrents(float isValue, float imValue) {
            if (RejectAutomaticEdit()) return;
            if(WebBridge.Instance!=null && !WebBridge.Instance.ValidateCurrents(isValue,imValue))return;
            float oldIs = Circuit.IsSetpoint, oldIm = Circuit.ImSetpoint;
            Circuit.SetCurrent(isValue, imValue);
            if (oldIs != Circuit.IsSetpoint || oldIm != Circuit.ImSetpoint) {ExperimentModel.InvalidateSampling();ExperimentModel.TrackOperation("设定电流","IS="+Circuit.IsSetpoint+"mA IM="+Circuit.ImSetpoint+"A");}
            Refresh();
        }
        public void MoveStage(float x, float y)
        {
            if (RejectAutomaticEdit()) return;
            stageX = Mathf.Clamp(x,-1,1); stageY = Mathf.Clamp(y,-1,1);
            sampleCarriage.localPosition = carriageHome + new Vector3(stageX*.018f,0,stageY*.006f);
            var mechanics=sampleCarriage.parent.GetComponent<StageMechanics>();
            if(mechanics!=null)mechanics.SetPose(stageX,stageY);
            if (xSlider != null) { xSlider.SetValueWithoutNotify(stageX); ySlider.SetValueWithoutNotify(stageY); }
            foreach (var lead in GetComponentsInChildren<LeadPath>()) lead.Refresh();
            UpdateStageCaption();
        }
        public void ResetControls()
        {
            if(LessonActive){EndLesson();return;}
            if (automaticExperimentRunning) StopAutomaticExperiment("重置装置，自动实验已停止");
            Circuit.TurnOff(); Circuit = new THHCircuit(); ExperimentModel.Reset(); PhysicalModelEnabled = false; SelectedPin = null; showIm = false;
            ExperimentModel.TrackOperation("重置","装置已重置，完整数据保留");
            if(!ValidationLaunch)PhysicalModelEnabled=true;
            MoveStage(0,0); Refresh("先接三对外接线 → 检查回路 → 电流归零 → 接通输出", true);
        }
        private void Refresh(string message = null, bool rebuild = false)
        {
            if (!initialized || stateText == null) return;
            var report = Circuit.Evaluate();
            pathText.text = (Circuit.Powered ? "● 输出已接通" : "○ 已断电") + "\n连接方向（非实测）\n" + report.Detail;
            pathText.color = report.Hazard ? WarningColor : Ink;
            string summary = Circuit.Powered ? "回路有效，输出已接通；接线与开关已锁定" : report.Summary;
            if (message != null) { recentMessage = message; recentMessageUntil = Time.realtimeSinceStartup + 7f; }
            else if (Time.realtimeSinceStartup < recentMessageUntil) message = recentMessage;
            stateText.text = string.IsNullOrEmpty(message) || message == summary ? summary : message + "\n" + summary;
            progressText.text = Circuit.Powered ? "输出 · 已接通" : report.Ready ? "接线就绪 · 已断电" : "接线检查 · "+Circuit.Cables.Count+" 根外线";
            progressText.color = report.Hazard ? WarningColor : report.Ready ? AccentColor : Muted;
            powerText.text = Circuit.Powered ? "归零并关闭输出" : "检查接线并接通输出";
            selectionText.text = Circuit.Powered ? "输出已接通，接线已锁定" : SelectedPin.HasValue ? "当前起点：" + THHCircuit.Name(SelectedPin.Value) + "\n终点：待选择" : "当前连接：待选择端子";
            foreach (var terminal in terminals) terminal.Highlight(SelectedPin == terminal.pin || (LessonWaiting && (lessonFrom==terminal.pin || lessonTo==terminal.pin)));
            for (int i=0;i<12;i++) {
                pinTexts[i].color = SelectedPin == (Pin)i ? Cyan : Ink;
                bool has = i < Circuit.Cables.Count;
                cableTexts[i].transform.parent.gameObject.SetActive(has);
                if (has) { var c = Circuit.Cables[i]; cableTexts[i].text = Short(c.A) + "   →   " + Short(c.B); }
            }
            for (int i=0;i<3;i++) {
                int position = Circuit.Position(i);
                // Pivot about the transverse axis; middle is open, opposite throw goes over the top.
                switchBlades[i].localRotation = Quaternion.Euler(position == 1 ? 0 : position == 0 ? -90 : -180,0,0);
                switchTexts[i].text = i==0 ? "IS 电流方向" : i==1 ? "测量模式" : "IM 电流方向";
            }
            isSlider.SetValueWithoutNotify(Circuit.IsSetpoint); imSlider.SetValueWithoutNotify(Circuit.ImSetpoint);
            currentCaption.text = "工作电流 IS";
            magnetCaption.text = "励磁电流 IM";
            currentDisplay.text = Circuit.Powered ? (showIm ? (PhysicalModelEnabled ? ExperimentModel.ActualImAmps.ToString("0.000") : Circuit.ImSetpoint.ToString("0.000")) : (PhysicalModelEnabled ? ExperimentModel.ActualIsMilliamps.ToString("0.00") : Circuit.IsSetpoint.ToString("0.00"))) : "----";
            voltageDisplay.text = Circuit.Powered ? (PhysicalModelEnabled ? HallInstrument.Display(true,report.Ready,ExperimentModel.LiveReading.totalMillivolts).Replace(" mV","") : "N/A") : "----";
            meterText.text = "电流屏：" + (showIm ? "IM / A" : "IS / mA");
            if (modelButton != null) modelButton.text = PhysicalModelEnabled ? "物理模型：开启" : "物理模型：关闭";
            if (recordButton != null) recordButton.text = "记录当前方向 · " + ExperimentModel.MeasurementCount + "/4";
            RefreshSamplingControls();
            RefreshPresentation();
            RefreshTeaching();
            if (dataVisible) RefreshDataPanel();
            if (rebuild) RebuildStudentLeads();
        }
        private static string Short(Pin pin) => ((int)pin<6 ? "仪 " : "箱 ") + new[]{"IS+","IS−","V+","V−","IM+","IM−"}[(int)pin%6];
        private void RebuildStudentLeads()
        {
            for (int k=externalLeads.transform.childCount-1;k>=0;k--) {
                var child=externalLeads.transform.GetChild(k);child.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(child.gameObject); else DestroyImmediate(child.gameObject);
            }
            for (int i=0;i<Circuit.Cables.Count;i++) {
                var cable = Circuit.Cables[i]; var a = terminals.First(t=>t.pin==cable.A); var b = terminals.First(t=>t.pin==cable.B);
                var go = new GameObject("Cable_"+cable.Id); go.transform.SetParent(externalLeads.transform,false);
                Material mat = (int)cable.A%2==0 ? positiveLead : negativeLead;
                var line = LeadPath.Configure(go,mat,.0033f);
                Vector3 aExit=a.Exit, bExit=b.Exit;
                // Loose leads go around the front of the apparatus, above the bench.
                float lane = -.33f - i*.010f;
                float deskHeight=.012f+i*.0015f;
                var route=new List<Vector3>{a.transform.position,aExit};
                // Tester leads descend directly; case leads first clear the front rim.
                if((int)cable.A>=6)route.Add(new Vector3(aExit.x,.122f,-.285f));
                route.Add(new Vector3(aExit.x,deskHeight,lane));
                route.Add(new Vector3(bExit.x,deskHeight,lane));
                if((int)cable.B>=6)route.Add(new Vector3(bExit.x,.122f,-.285f));
                route.Add(bExit);route.Add(b.transform.position);
                var points=RoundedRoute(route);
                line.positionCount=points.Length; line.SetPositions(points);
                go.GetComponent<RoundCable>().Refresh();
                Plug(go.transform,a,mat); Plug(go.transform,b,mat);
            }
        }
        private static Vector3[] RoundedRoute(List<Vector3> route)
        {
            var result=new List<Vector3>{route[0]};
            for(int i=1;i<route.Count-1;i++) {
                Vector3 center=route[i], incoming=route[i-1]-center, outgoing=route[i+1]-center;
                float radius=Mathf.Min(.035f,incoming.magnitude*.4f,outgoing.magnitude*.4f);
                Vector3 a=center+incoming.normalized*radius,b=center+outgoing.normalized*radius;
                for(int j=0;j<=8;j++){float t=j/8f;result.Add((1-t)*(1-t)*a+2*(1-t)*t*center+t*t*b);}
            }
            result.Add(route[route.Count-1]);return result.ToArray();
        }
        private static void Plug(Transform parent, CircuitTerminal terminal, Material mat)
        {
            var plug = GameObject.CreatePrimitive(PrimitiveType.Cylinder); plug.name="InsulatedPlug";
            plug.transform.SetParent(parent,false);
            Vector3 direction = terminal.transform.TransformDirection(terminal.outward).normalized;
            plug.transform.position=terminal.transform.position+direction*.009f;
            plug.transform.rotation=Quaternion.FromToRotation(Vector3.up,direction);
            plug.transform.localScale=new Vector3(.008f,.009f,.008f);
            plug.GetComponent<Renderer>().sharedMaterial=mat;
            // Plugs remain selectable as the same electrical terminal.
            var target=plug.AddComponent<CircuitTerminal>(); target.pin=terminal.pin;
            // Moulded grip ribs and a flexible cable exit, all attached to the same selectable terminal.
            for(int i=0;i<5;i++) {
                var rib=GameObject.CreatePrimitive(PrimitiveType.Cylinder);rib.name="PlugGripRing";
                rib.transform.SetParent(plug.transform,false);
                rib.transform.localPosition=new Vector3(0,-.55f+i*.26f,0);
                rib.transform.localScale=new Vector3(1.15f,.07f,1.15f);
                rib.GetComponent<Renderer>().sharedMaterial=mat;
            }
            var boot=GameObject.CreatePrimitive(PrimitiveType.Cylinder);boot.name="CableStrainRelief";
            boot.transform.SetParent(plug.transform,false);boot.transform.localPosition=new Vector3(0,1.14f,0);
            boot.transform.localScale=new Vector3(.60f,.28f,.60f);boot.GetComponent<Renderer>().sharedMaterial=mat;
        }
        private RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 low,Vector2 high)
        {
            var go=new GameObject(name,typeof(RectTransform)); var rt=go.GetComponent<RectTransform>(); rt.SetParent(parent,false);
            rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=low;rt.offsetMax=high;return rt;
        }
        private RectTransform Panel(string name,Transform parent,Vector2 min,Vector2 max,Vector2 low,Vector2 high,Color color)
        {var rt=Rect(name,parent,min,max,low,high);rt.gameObject.AddComponent<Image>().color=color;return rt;}
        private Text Label(Transform parent,string content,int size,Color color,float x,float top,float width,float height)
        {
            var rt=Rect("Label",parent,new Vector2(0,1),new Vector2(0,1),new Vector2(x,-top-height),new Vector2(x+width,-top));
            var t=rt.gameObject.AddComponent<Text>();t.font=font;t.fontSize=size;t.text=content;t.color=color;t.raycastTarget=false;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Noto's ascent/descent differs from Windows fonts; fit within the existing fixed tracks.
            t.resizeTextForBestFit=true;t.resizeTextMinSize=Math.Min(size,10);t.resizeTextMaxSize=size;
#endif
            t.verticalOverflow=VerticalWrapMode.Truncate;t.horizontalOverflow=HorizontalWrapMode.Wrap;return t;
        }
        private Text Button(Transform parent,string title,float x,float top,float width,UnityAction action,bool accent=false,float height=32)
        {
            var rt=Panel(title,parent,new Vector2(0,1),new Vector2(0,1),new Vector2(x,-top-height),new Vector2(x+width,-top),accent?AccentColor:ButtonColor);
            var btn=rt.gameObject.AddComponent<Button>();btn.onClick.AddListener(action);
            Round(rt,6);
            var colors=btn.colors;colors.highlightedColor=new Color(.90f,.95f,.92f);colors.pressedColor=new Color(.72f,.82f,.76f);colors.selectedColor=new Color(.83f,.92f,.88f);colors.disabledColor=new Color(.76f,.78f,.77f,.65f);btn.colors=colors;
            var t=Label(rt,title,14,accent?Color.white:Ink,8,0,width-16,height);t.alignment=TextAnchor.MiddleCenter;return t;
        }
        private Slider Slider(Transform parent,string name,float top,float min,float max,UnityAction<float> change)
        {
            var rt=Rect(name,parent,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-top-22),new Vector2(378,-top));
            var hitArea=rt.gameObject.AddComponent<Image>();hitArea.color=Color.clear;
            var track=Panel("Track",rt,new Vector2(0,.43f),new Vector2(1,.57f),Vector2.zero,Vector2.zero,LineColor);Round(track,2);
            var area=Rect("HandleArea",rt,Vector2.zero,Vector2.one,new Vector2(6,0),new Vector2(-6,0));
            var handle=Panel("Handle",area,Vector2.zero,Vector2.one,new Vector2(-10,0),new Vector2(10,0),Cyan);Round(handle,10);
            var slider=rt.gameObject.AddComponent<Slider>();slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();
            slider.minValue=min;slider.maxValue=max;slider.value=0;slider.onValueChanged.AddListener(change);return slider;
        }
    }
}

