using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace HallLab
{
    // Keep original tab ids for existing workbench integrations.
    public sealed partial class THHWorkbench
    {
        private RectTransform controlPanel, inspector, samplingPanel, checkPanel, sceneTools, readingDock, resetDialog;
        private Text viewTitle, progressText, pageTitle, pageDescription, wireCount, emptyWires, checkSummary;
        private Text liveIs, liveIm, liveVoltage, liveSource, focusButton, samplePower, stageCaption;
        private readonly Text[] navigation=new Text[6], viewButtons=new Text[4], checkValues=new Text[4], sampleValues=new Text[4];
        private readonly Text[,] switchOptions=new Text[3,3];
        private readonly Text[] meterOptions=new Text[2], sweepOptions=new Text[2];
        private readonly int[] navigationTabs={0,4,1,3,-1,2};
        private InputField isInput, imInput;
        private Toggle modelToggle;
        public int ActiveTab { get; private set; }
        public bool InspectorVisible => inspector!=null && inspector.gameObject.activeSelf;

        public void ShowTab(int index)
        {
            if(dataPanel!=null)dataPanel.gameObject.SetActive(false);
            dataVisible=false;ActiveTab=Mathf.Clamp(index,0,4);
            wirePanel.parent.parent.gameObject.SetActive(ActiveTab==0);
            controlPanel.parent.parent.gameObject.SetActive(ActiveTab==1);
            detailPanel.parent.parent.gameObject.SetActive(ActiveTab==2);
            samplingPanel.parent.parent.gameObject.SetActive(ActiveTab==3);
            checkPanel.parent.parent.gameObject.SetActive(ActiveTab==4);
            inspector.gameObject.SetActive(true);sceneTools.gameObject.SetActive(true);readingDock.gameObject.SetActive(true);
            orbit.compositionShift=.13f;orbit.Apply();
            pageTitle.text=new[]{"连接实验回路","实验控制","器材详情","四方向采样","回路检查"}[ActiveTab];
            pageDescription.text=new[]{"工作电流 / 测量电压 / 励磁电流","电源与参数","所选器材的结构与依据","记录稳定读数，合成霍尔电压","接通输出前的回路状态"}[ActiveTab];
            RefreshNavigation();UpdateLessonLayout();
        }
        private void RefreshNavigation()
        {
            for(int i=0;i<navigation.Length;i++)SetSelected(navigation[i],dataVisible?navigationTabs[i]==-1:navigationTabs[i]==ActiveTab);
            if(focusButton!=null)focusButton.transform.parent.GetComponent<UIHint>().message=InspectorVisible?"专注装置：收起右侧面板":"显示右侧面板";
        }
        private void SetSelected(Text text,bool selected)
        {
            var style=text.transform.parent.GetComponent<UIChoiceStyle>();
            if(style!=null){style.Apply(text,selected);return;}
            text.transform.parent.GetComponent<Image>().color=selected?AccentColor:ButtonColor;text.color=selected?Color.white:Ink;
        }
        public void ToggleInspector()
        {
            if(dataVisible)ToggleDataPanel();
            inspector.gameObject.SetActive(!InspectorVisible);orbit.compositionShift=InspectorVisible?.13f:0;
            orbit.Apply();RefreshNavigation();
        }
        private RectTransform ScrollPanel(string name,Transform parent,float height)
        {
            var area=Rect(name,parent,Vector2.zero,Vector2.one,new Vector2(0,16),new Vector2(0,-116));
            var scroll=area.gameObject.AddComponent<ScrollRect>();
            var viewport=Panel("Viewport",area,Vector2.zero,Vector2.one,Vector2.zero,new Vector2(-12,0),Color.clear);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Content",viewport,new Vector2(0,1),Vector2.one,new Vector2(0,-height),Vector2.zero);
            content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;
            var rail=Panel("Scroll rail",area,new Vector2(1,0),Vector2.one,new Vector2(-5,0),Vector2.zero,PanelAlt);
            var handle=Panel("Scroll thumb",rail,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.63f,.68f,.69f));
            var bar=rail.gameObject.AddComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=handle.GetComponent<Image>();bar.direction=Scrollbar.Direction.BottomToTop;
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity=32;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }
        private void Rule(Transform parent,float y,float width=354)
        {Panel("Divider",parent,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-y-1),new Vector2(24+width,-y),LineColor);}
        private InputField CurrentInput(Transform parent,string name,float top,string unit,UnityEngine.Events.UnityAction<string> change)
        {
            var box=Panel(name,parent,new Vector2(0,1),new Vector2(0,1),new Vector2(244,-top-34),new Vector2(378,-top),ButtonColor);
            Round(box,6);
            var field=box.gameObject.AddComponent<InputField>();var text=Label(box,"0",16,Ink,10,0,74,34);text.alignment=TextAnchor.MiddleRight;
            field.textComponent=text;field.targetGraphic=box.GetComponent<Image>();field.contentType=InputField.ContentType.DecimalNumber;field.characterLimit=8;field.onEndEdit.AddListener(change);
            Label(box,unit,12,Muted,92,0,40,34).alignment=TextAnchor.MiddleLeft;return field;
        }
        private void ParseCurrent(string text,bool primary)
        {
            if(float.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out float value)&&!float.IsNaN(value)&&!float.IsInfinity(value))SetCurrents(primary?value:Circuit.IsSetpoint,primary?Circuit.ImSetpoint:value);
            else Refresh("请输入有效的电流数值");
            RefreshPresentation();
        }
        private void BuildInterface()
        {
            foreach(var old in Resources.FindObjectsOfTypeAll<WorkbenchUIRoot>())if(old.owner==this){old.gameObject.SetActive(false);if(Application.isPlaying)Destroy(old.gameObject);else DestroyImmediate(old.gameObject);}
            var root=new GameObject("Workbench UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            uiRoot=root;root.AddComponent<WorkbenchUIRoot>().owner=this;if(!Application.isPlaying)root.hideFlags=HideFlags.DontSave;
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=view;canvas.planeDistance=.08f;
            // Keep the workbench controls above the 3-D scene in WebGL and
            // after browser/fullscreen resize.  Without an explicit sorting
            // order a camera overlay can swallow the right-side hit targets.
            canvas.overrideSorting=true;canvas.sortingOrder=500;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var header=Panel("Header",root.transform,new Vector2(0,1),Vector2.one,new Vector2(0,-72),Vector2.zero,Color.white);
            Circle(header,"Brand",24,19,34,AccentColor);Icon(header,"activity",31,26,20,Color.white);
            Label(header,"霍尔效应实验台",21,Ink,72,12,240,30).fontStyle=FontStyle.Bold;
            Label(header,"独立教学仿真 · 非原厂软件",10,Muted,73,42,230,18);
            string[] tabs={"接线","检查","控制","采样","数据","器材"};
            string[] icons={"cable","shield-check","sliders-horizontal","activity","chart-no-axes-combined","info"};
            for(int i=0;i<6;i++){int tab=navigationTabs[i];navigation[i]=Choice(header,tabs[i],350+i*108,18,100,()=>{if(tab<0){if(!dataVisible)ToggleDataPanel();}else ShowTab(tab);},icons[i],true);}
            automaticExperimentButton=TextAction(header,ValidationLaunch?"自动实验":"观看演示","activity",1030,18,210,()=>{if(ValidationLaunch)ToggleAutomaticExperiment();else if(LessonActive)EndLesson();else BeginLesson();},true);
            var tools=Rect("Header tools",header,Vector2.one,Vector2.one,new Vector2(-156,-56),new Vector2(-20,-16));
            // This is a layout action, not browser fullscreen. Use a panel-close
            // glyph so it cannot be confused with the host's fullscreen control.
            focusButton=IconButton(tools,"专注装置","panel-right-close",0,0,ToggleInspector);
            IconButton(tools,"重置装置","rotate-ccw",48,0,()=>resetDialog.gameObject.SetActive(true));
            IconButton(tools,"退出","log-out",96,0,RequestQuit);
            Panel("Header divider",header,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,1),LineColor);
            sceneTools=Rect("Scene tools",root.transform,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-175),new Vector2(670,-96));
            var titleBack=Panel("View title background",sceneTools,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-32),new Vector2(432,0),new Color(.12f,.16f,.18f,.94f));
            viewTitle=Label(titleBack,"装置全景",19,Color.white,10,1,410,30);
            var autoBack=Panel("Automatic progress",sceneTools,new Vector2(0,1),new Vector2(0,1),new Vector2(450,-78),new Vector2(946,0),new Color(.12f,.16f,.18f,.94f));
            automaticProgressText=Label(autoBack,AutomaticExperimentStatus,14,Color.white,12,6,472,70);
            var rail=Panel("View rail",sceneTools,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-78),new Vector2(432,-36),new Color(1,1,1,.94f));Round(rail,6);
            string[] views={"装置全景","测试仪","实验箱","霍尔样品"};
            for(int i=0;i<4;i++){int index=i;viewButtons[i]=Choice(rail,views[i],4+i*107,2,103,()=>FocusView(index));}
            SetSelected(viewButtons[0],true);
            inspector=Panel("Inspector",root.transform,new Vector2(1,0),Vector2.one,new Vector2(-420,52),new Vector2(0,-72),Color.white);
            Panel("Inspector border",inspector,Vector2.zero,new Vector2(0,1),Vector2.zero,new Vector2(1,0),LineColor);
            pageTitle=Label(inspector,"",22,Ink,24,20,360,34);pageTitle.fontStyle=FontStyle.Bold;
            pageDescription=Label(inspector,"",12,Muted,24,60,360,22);progressText=Label(inspector,"",12,AccentColor,24,86,360,22);Rule(inspector,112,364);
            wirePanel=ScrollPanel("Wiring",inspector,750);checkPanel=ScrollPanel("Checks",inspector,590);controlPanel=ScrollPanel("Controls",inspector,630);
            samplingPanel=ScrollPanel("Sampling",inspector,610);detailPanel=ScrollPanel("Details",inspector,670);
            BuildWiringPage();BuildCheckPage();BuildControlPage();BuildSamplingPage();BuildDetailPage();
            readingDock=Panel("Live readings",root.transform,Vector2.zero,new Vector2(1,0),new Vector2(0,52),new Vector2(-420,150),Color.white);
            Icon(readingDock,"zap",28,20,17,new Color(.24f,.43f,.63f));Icon(readingDock,"magnet",246,20,17,new Color(.63f,.39f,.23f));Icon(readingDock,"activity",464,20,17,AccentColor);
            Label(readingDock,"工作电流 IS",12,Muted,52,18,155,22);Label(readingDock,"励磁电流 IM",12,Muted,270,18,155,22);Label(readingDock,"霍尔 / 纵向电压",12,Muted,488,18,180,22);
            liveIs=Label(readingDock,"",27,Ink,28,46,196,37);liveIm=Label(readingDock,"",27,Ink,246,46,196,37);liveVoltage=Label(readingDock,"",27,AccentColor,464,46,216,37);
            liveSource=Label(readingDock,"",12,Muted,738,24,240,52);
            foreach(float x in new[]{222f,440f,708f})Panel("Readout separator",readingDock,new Vector2(0,1),new Vector2(0,1),new Vector2(x,-76),new Vector2(x+1,-22),LineColor);
            BuildDataInterface(root.transform);
            var footer=Panel("Status dock",root.transform,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,52),PanelAlt);
            Panel("Status divider",footer,new Vector2(0,1),Vector2.one,new Vector2(0,-1),Vector2.zero,LineColor);
            Circle(footer,"Status dot",26,23,6,AccentColor);stateText=Label(footer,"",12,Ink,46,6,1110,40);
            var stamp=Rect("Data source",footer,new Vector2(1,0),Vector2.one,new Vector2(-244,0),new Vector2(-24,0));
            Label(stamp,"教学仿真  ·  "+Application.version,11,Muted,0,15,220,24).alignment=TextAnchor.MiddleRight;
            BuildResetDialog(root.transform);BuildStorageDialogs(root.transform);BuildTeachingControls(root.transform);ShowTab(0);
        }
        private void BuildWiringPage()
        {
            selectionText=Label(wirePanel,"",13,Muted,24,8,354,44);
            Label(wirePanel,"测试仪",12,Muted,74,62,110,22).alignment=TextAnchor.MiddleCenter;
            Label(wirePanel,"实验箱",12,Muted,256,62,110,22).alignment=TextAnchor.MiddleCenter;
            string[] groups={"IS","V","IM"};string[] subtitles={"工作电流","测量电压","励磁电流"};
            for(int group=0;group<3;group++) {
                float top=96+group*88;Label(wirePanel,groups[group],18,Ink,24,top+7,54,26).fontStyle=FontStyle.Bold;
                Label(wirePanel,subtitles[group],10,Muted,24,top+37,72,22);
                for(int side=0;side<2;side++)for(int polarity=0;polarity<2;polarity++) {
                    int pin=group*2+polarity+side*6;float x=92+side*182+polarity*48;
                    var text=Button(wirePanel,THHCircuit.Name((Pin)pin),x,top,38,()=>ChooseTerminal((Pin)pin),false,38);text.text="";
                    var image=text.transform.parent.GetComponent<Image>();image.color=Color.clear;
                    var choice=text.transform.parent.gameObject.AddComponent<UIChoiceStyle>();choice.idle=Color.clear;choice.selected=new Color(.79f,.94f,.91f);choice.idleText=Ink;choice.selectedText=AccentColor;
                    var color=polarity==0?new Color(.77f,.30f,.30f):new Color(.26f,.34f,.40f);
                    Circle(text.transform.parent,"Socket ring",3,3,32,color,false,3);
                    Circle(text.transform.parent,"Socket core",11,11,16,color);
                    Label(text.transform.parent,polarity==0?"+":"−",10,Color.white,8,8,22,22).alignment=TextAnchor.MiddleCenter;
                    pinTexts[pin]=text;text.transform.parent.gameObject.AddComponent<UIHint>().message=THHCircuit.Name((Pin)pin);
                }
                Icon(wirePanel,"arrow-right",207,top+10,18,new Color(.66f,.73f,.75f));Rule(wirePanel,top+73);
            }
            TextAction(wirePanel,ValidationLaunch?"标准接线":"观看逐根接线演示","cable",24,368,244,()=>{if(ValidationLaunch)StandardWiring();else BeginLesson();},true);IconButton(wirePanel,"清空外接线","x",286,370,ClearWiring);
            TextAction(wirePanel,"撤销上一根接线","rotate-ccw",24,410,244,UndoLastCable,true);
            Label(wirePanel,"接错后可撤销上一根，或点击下方导线移除；每个端子一根线，最多 6 根。",11,Muted,24,448,354,28);
            wireCount=Label(wirePanel,"",13,Ink,24,482,300,24);emptyWires=Label(wirePanel,"尚未连接外接线",13,Muted,24,518,330,30);
            for(int i=0;i<12;i++) {
                int index=i;cableTexts[i]=Choice(wirePanel,"移除接线 "+i,24,548+i*36,354,()=>{if(index<Circuit.Cables.Count)RemoveCable(Circuit.Cables[index].Id);});
                cableTexts[i].alignment=TextAnchor.MiddleLeft;Icon(cableTexts[i].transform.parent,"x",324,12,14,Muted);
                cableTexts[i].transform.parent.gameObject.AddComponent<UIHint>().message="移除此导线";
            }
        }
        private void BuildCheckPage()
        {
            string[] labels={"电流源隔离","测量回路","回路连通","启动电流"};
            for(int i=0;i<4;i++){Circle(checkPanel,"Check marker",24,25+i*62,26,PanelAlt);Icon(checkPanel,"shield-check",29,30+i*62,16,AccentColor);Label(checkPanel,labels[i],15,Ink,64,24+i*62,180,28);checkValues[i]=Label(checkPanel,"",14,Muted,266,24+i*62,112,28);checkValues[i].alignment=TextAnchor.MiddleRight;Rule(checkPanel,70+i*62);}
            checkSummary=Label(checkPanel,"",14,Ink,24,270,354,76);pathText=Label(checkPanel,"",14,Muted,24,354,354,108);
            TextAction(checkPanel,"检查回路","shield-check",24,482,354,()=>Refresh("检查完成："+Circuit.Evaluate().Summary),true);TextAction(checkPanel,"进入实验控制","arrow-right",24,536,354,()=>ShowTab(1));
        }
        private void BuildControlPage()
        {
            powerText=TextAction(controlPanel,"检查接线并接通输出","power",24,10,354,TogglePower,true);
            var toggleRect=Rect("Physical model",controlPanel,new Vector2(0,1),new Vector2(0,1),new Vector2(24,-108),new Vector2(378,-68));
            var box=Panel("Toggle track",toggleRect,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-44,-12),new Vector2(0,12),LineColor);Round(box,12);toggleTrack=box.GetComponent<Image>();
            toggleThumb=Panel("Toggle thumb",box,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(3,-9),new Vector2(21,9),Color.white);Round(toggleThumb,9);
            modelToggle=toggleRect.gameObject.AddComponent<Toggle>();modelToggle.targetGraphic=box.GetComponent<Image>();modelToggle.onValueChanged.AddListener(v=>{if(v!=PhysicalModelEnabled)TogglePhysicalModel();});
            modelButton=Label(toggleRect,"物理模型：关闭",14,Ink,0,7,280,26);
            if(!ValidationLaunch)toggleRect.gameObject.SetActive(false);
            Label(controlPanel,"可直接拖动仪器旋钮，点击电源与刀闸。\n旋钮步进：IS 0.05 mA / 格，IM 0.005 A / 格。",12,Muted,24,74,354,34);Rule(controlPanel,118);
            currentCaption=Label(controlPanel,"工作电流 IS（0–10 mA）",14,Ink,24,139,270,28);isInput=CurrentInput(controlPanel,"IS numeric",132,"mA",s=>ParseCurrent(s,true));isSlider=Slider(controlPanel,"IS setpoint",180,0,10,v=>SetCurrents(v,Circuit.ImSetpoint));
            magnetCaption=Label(controlPanel,"励磁电流 IM（0–1 A）",14,Ink,24,227,270,28);imInput=CurrentInput(controlPanel,"IM numeric",220,"A",s=>ParseCurrent(s,false));imSlider=Slider(controlPanel,"IM setpoint",268,0,1,v=>SetCurrents(Circuit.IsSetpoint,v));Rule(controlPanel,306);
            for(int i=0;i<3;i++){int index=i;float top=322+i*58;switchTexts[i]=Label(controlPanel,"",13,Ink,24,top,130,36);string[] choices=i==1?new[]{"VH","断开","Vσ"}:new[]{"正向","断开","反向"};var rail=Panel("Switch rail",controlPanel,new Vector2(0,1),new Vector2(0,1),new Vector2(154,-top-40),new Vector2(380,-top),PanelAlt);Round(rail,6);for(int j=0;j<3;j++){int position=1-j;switchOptions[i,j]=Choice(rail,choices[j],2+j*74,1,72,()=>SetSwitch(index,position));}}
            Label(controlPanel,"电流表显示",13,Muted,24,500,125,30);meterOptions[0]=Choice(controlPanel,"IS",156,500,108,()=>{showIm=false;Refresh();});meterOptions[1]=Choice(controlPanel,"IM",274,500,104,()=>{showIm=true;Refresh();});meterOptions[1].transform.parent.name="电流屏：IM";
            meterText=Label(controlPanel,"",12,Muted,24,542,354,22);TextAction(controlPanel,"进入四方向采样","arrow-right",24,578,354,()=>ShowTab(3));
        }
        private void BuildSamplingPage()
        {
            sweepOptions[0]=Choice(samplingPanel,"VH–IS",24,14,170,()=>SelectDataSweep(true),null,true);sweepOptions[1]=Choice(samplingPanel,"VH–IM",204,14,174,()=>SelectDataSweep(false),null,true);
            sweepButton=Label(samplingPanel,"",13,Muted,24,62,354,32);samplingText=Label(samplingPanel,"",14,AccentColor,24,104,354,42);
            samplingCount=Label(samplingPanel,"0 / 4",14,Ink,24,148,354,26);
            string[] directions={"+IS / +IM","+IS / −IM","−IS / −IM","−IS / +IM"};
            for(int i=0;i<4;i++) {
                float x=24+(i%2)*184,top=188+(i/2)*104;
                samplingRings[i]=Circle(samplingPanel,"V"+(i+1)+" progress",x,top,44,LineColor,false,3);samplingRings[i].ProgressColor=AccentColor;
                Label(samplingPanel,"V"+(i+1),14,Ink,x,top+8,44,26).alignment=TextAnchor.MiddleCenter;
                Label(samplingPanel,directions[i],12,Muted,x+54,top,116,23);
                sampleValues[i]=Label(samplingPanel,"待记录",15,Ink,x+54,top+24,124,30);
            }
            Label(samplingPanel,"VH = (V1 − V2 + V3 − V4) / 4",13,Muted,24,404,354,28);recordButton=TextAction(samplingPanel,"记录当前读数","circle-plus",24,450,354,RecordCurrentReading,true);
            TextAction(samplingPanel,"清除本组","rotate-ccw",24,502,164,ClearPendingReadings);TextAction(samplingPanel,"数据分析","chart-no-axes-combined",202,502,176,ToggleDataPanel);
            samplePower=TextAction(samplingPanel,"接通输出","power",24,560,170,TogglePower);TextAction(samplingPanel,"电流与方向","settings-2",204,560,174,()=>ShowTab(1));
        }
        private void BuildDetailPage()
        {
            selectedTitle=Label(detailPanel,"",20,Ink,24,12,354,62);selectedTitle.fontStyle=FontStyle.Bold;
            selectedBody=Label(detailPanel,"",14,Ink,24,84,354,124);Rule(detailPanel,220);selectedEvidence=Label(detailPanel,"",13,Muted,24,236,354,126);Rule(detailPanel,378);
            Label(detailPanel,"样品电极",16,Ink,24,394,354,28).fontStyle=FontStyle.Bold;
            Label(detailPanel,"D / E        工作电流\nA / A′       霍尔电压 VH\nA′ / C′      纵向电压 Vσ\nC              悬空",14,Ink,24,436,354,96);
            stageCaption=Label(detailPanel,"样品位置",14,Ink,24,544,354,26);xSlider=Slider(detailPanel,"Stage X",586,-1,1,v=>MoveStage(v,stageY));ySlider=Slider(detailPanel,"Stage Y",636,-1,1,v=>MoveStage(stageX,v));
            Label(detailPanel,"X",12,Muted,24,568,32,18);Label(detailPanel,"Y",12,Muted,24,618,32,18);
        }
        private void BuildResetDialog(Transform parent)
        {
            resetDialog=Panel("Reset confirmation",parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(0,0,0,.35f));
            var dialog=Panel("Dialog",resetDialog,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-240,-122),new Vector2(240,122),PanelColor);
            Round(dialog,8);
            Label(dialog,"重置实验装置？",22,Ink,28,24,424,36).fontStyle=FontStyle.Bold;
#if UNITY_WEBGL && !UNITY_EDITOR
            Label(dialog,"将关闭输出并清空当前接线与测量数据。\n请先在网页导出需要保留的结果。",14,Muted,28,82,424,64);
            Button(dialog,"取消",216,174,104,()=>resetDialog.gameObject.SetActive(false),false,40);Button(dialog,"重新开始",332,174,120,()=>{WebBridge.Instance.ResetExperiment();resetDialog.gameObject.SetActive(false);},true,40);
#else
            Label(dialog,"将关闭输出、清空接线并归零参数。\n本组未完成读数将清除，已保存的测量组保留。",14,Muted,28,82,424,64);
            Button(dialog,"取消",216,174,104,()=>resetDialog.gameObject.SetActive(false),false,40);Button(dialog,"重置装置",332,174,120,()=>{ResetControls();resetDialog.gameObject.SetActive(false);ShowTab(0);},true,40);
#endif
            resetDialog.gameObject.SetActive(false);
        }
        private void RefreshPresentation()
        {
            if(liveIs==null)return;var report=Circuit.Evaluate();
            wireCount.text="已连接导线  /  "+Circuit.Cables.Count+"（最多 6 根）";emptyWires.gameObject.SetActive(Circuit.Cables.Count==0);wirePanel.sizeDelta=new Vector2(wirePanel.sizeDelta.x,Mathf.Max(620,560+Circuit.Cables.Count*36));
            for(int i=0;i<12;i++){pinTexts[i].transform.parent.GetComponent<Button>().interactable=!Circuit.Powered && !automaticExperimentRunning;cableTexts[i].transform.parent.GetComponent<Button>().interactable=!Circuit.Powered && !automaticExperimentRunning;SetSelected(pinTexts[i],SelectedPin==(Pin)i || (LessonWaiting&&(lessonFrom==(Pin)i||lessonTo==(Pin)i)));}
            bool[] results={!report.SourceShort&&!report.CrossCircuit,!report.MeterShort,report.Ready,Circuit.IsSetpoint==0&&Circuit.ImSetpoint==0};
            for(int i=0;i<4;i++){checkValues[i].text=results[i]?"通过":i==3?"未归零":"需检查";checkValues[i].color=results[i]?AccentColor:WarningColor;}
            checkSummary.text=Circuit.Powered?"输出已接通；归零断电后才能改线或换向":report.Summary;checkSummary.color=report.Ready?AccentColor:WarningColor;
            if(!isInput.isFocused)isInput.SetTextWithoutNotify(Circuit.IsSetpoint.ToString("0.00",CultureInfo.InvariantCulture));if(!imInput.isFocused)imInput.SetTextWithoutNotify(Circuit.ImSetpoint.ToString("0.000",CultureInfo.InvariantCulture));modelToggle.SetIsOnWithoutNotify(PhysicalModelEnabled);
            toggleTrack.color=PhysicalModelEnabled?AccentColor:LineColor;toggleThumb.anchoredPosition=new Vector2(PhysicalModelEnabled?32:12,0);
            for(int i=0;i<3;i++)for(int j=0;j<3;j++){SetSelected(switchOptions[i,j],Circuit.Position(i)==1-j);switchOptions[i,j].transform.parent.GetComponent<Button>().interactable=!Circuit.Powered && !automaticExperimentRunning;}
            SetSelected(meterOptions[0],!showIm);SetSelected(meterOptions[1],showIm);SetSelected(sweepOptions[0],ExperimentModel.SweepIs);SetSelected(sweepOptions[1],!ExperimentModel.SweepIs);
            liveIs.text=Circuit.Powered?(PhysicalModelEnabled?ExperimentModel.ActualIsMilliamps:Circuit.IsSetpoint).ToString("0.00")+" mA":"— mA";
            liveIm.text=Circuit.Powered?(PhysicalModelEnabled?ExperimentModel.ActualImAmps:Circuit.ImSetpoint).ToString("0.000")+" A":"— A";
            liveVoltage.text=Circuit.Powered&&PhysicalModelEnabled?HallInstrument.Display(true,report.Ready,ExperimentModel.LiveReading.totalMillivolts):"— mV";
            liveSource.text=!Circuit.Powered?"输出已关闭":PhysicalModelEnabled?"模型计算读数\n"+(!report.HallMode?"纵向电压 Vσ":ExperimentModel.SamplingStable?"读数稳定":"等待稳定"):"设定电流\n物理模型已关闭";
            samplePower.text=Circuit.Powered?"归零断电":"接通输出";UpdateStageCaption();
            recordButton.transform.parent.GetComponent<Button>().interactable=!automaticExperimentRunning&&PhysicalModelEnabled&&Circuit.Powered&&ExperimentModel.SamplingStable&&report.HallMode&&Circuit.IsSetpoint>=.15f&&Circuit.ImSetpoint>=.05f;
            isInput.interactable=imInput.interactable=isSlider.interactable=imSlider.interactable=modelToggle.interactable=xSlider.interactable=ySlider.interactable=!automaticExperimentRunning;
            powerText.transform.parent.GetComponent<Button>().interactable=samplePower.transform.parent.GetComponent<Button>().interactable=!automaticExperimentRunning;
        }
        private void UpdateStageCaption(){if(stageCaption!=null)stageCaption.text="样品位置    X "+stageX.ToString("0.00")+" / Y "+stageY.ToString("0.00");}
    }
}
