using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HallLab
{
    public enum InstrumentAction { IsKnob, ImKnob, Power, Meter }
    public sealed class InstrumentControl : MonoBehaviour
    {
        public InstrumentAction action;
        public Transform pointer;
    }

    public sealed partial class THHWorkbench
    {
        public bool LessonActive { get; private set; }
        public bool LessonWaiting { get; private set; }
        public int LessonStep { get; private set; }
        public bool LessonPlaying { get; private set; }
        public string LessonInstruction { get; private set; }
        private Pin? lessonFrom, lessonTo;
        private HallExperimentModel studentModel;
        private THHCircuit studentCircuit;
        private bool studentPhysical;
        private float studentStageX, studentStageY;
        private RectTransform lessonControls;
        private Text lessonPlayButton, lessonNextButton, qualityButton;
        private InstrumentControl activeKnob;
        private Vector3 knobScreen;
        private float knobLastAngle, knobAccumDeg, knobStartValue;
        private bool draggingKnob;
        private bool lowQuality;
        private TextMesh isKnobReadout, imKnobReadout;
        private TextMesh isKnobRange, imKnobRange, outputMeterCaption;
        public bool InstrumentPointerCaptured { get; private set; }
        public static bool ValidationLaunch => Environment.GetCommandLineArgs().Any(a =>
            a == "-hall-smoke" || a == "-hall-auto-check");

        private void BuildTeachingControls(Transform root)
        {
            lessonControls = Panel("Lesson controls",root,new Vector2(0,1),new Vector2(0,1),
                new Vector2(478,-224),new Vector2(974,-178),Color.white);
            lessonNextButton=Button(lessonControls,"下一步",8,4,108,NextLessonStep,true,36);
            lessonPlayButton=Button(lessonControls,"连续播放",122,4,108,ToggleLessonPlayback,false,36);
            Button(lessonControls,"重播",236,4,108,ReplayLesson,false,36);
            Button(lessonControls,"返回实验",350,4,138,EndLesson,false,36);
            lessonControls.gameObject.SetActive(false);
            qualityButton=Button(detailPanel,"画质：节能",24,696,170,ToggleQuality,false,36);
#if !UNITY_WEBGL || UNITY_EDITOR
            Button(detailPanel,"恢复未保存数据",202,696,176,ShowRecoveryBrowser,false,36);
#endif
            Label(detailPanel,"X/Y 仅演示机构移动，不改变模型磁场。",12,Muted,24,744,354,42);
            detailPanel.sizeDelta=new Vector2(detailPanel.sizeDelta.x,800);
        }

        private void InitializeTeaching()
        {
            var tester=GetComponentsInChildren<Transform>().First(t=>t.name=="THH_TestInstrument");
            // Runtime migration also handles the serialized reference scene. The
            // scene builder is not re-run when an existing scene is built for WebGL.
            foreach(var old in tester.GetComponentsInChildren<TextMesh>(true).Where(t=>t.name=="KnobCaption"))
                old.gameObject.SetActive(false);
            outputMeterCaption=tester.GetComponentsInChildren<TextMesh>()
                .First(t=>t.name=="DisplayCaption"&&t.transform.localPosition.x>0);
            AddInstrumentControl(tester,"IS 调节旋钮",InstrumentAction.IsKnob,new Vector3(.035f,.070f,-.184f),new Vector3(.05f,.05f,.03f));
            AddInstrumentControl(tester,"IM 调节旋钮",InstrumentAction.ImKnob,new Vector3(.091f,.070f,-.184f),new Vector3(.05f,.05f,.03f));
            AddInstrumentControl(tester,"电流表选择",InstrumentAction.Meter,new Vector3(.164f,.13f,-.174f),new Vector3(.024f,.024f,.010f));
            var power=AddInstrumentControl(tester,"输出电源",InstrumentAction.Power,new Vector3(-.170f,.107f,-.179f),new Vector3(.026f,.018f,.012f));
            // Keep the live setpoint labels on the faceplate, between the display
            // caption and the knobs. The old labels floated .05 m in front of the
            // panel, so perspective made them cover the meter caption
            // in the close-up preset.  The large meter above remains the measured
            // output; these compact labels deliberately say SET to distinguish the
            // control value from that output.
            isKnobReadout=AddKnobLegend(tester,"Knob setpoint", "IS 0.00 mA");
            imKnobReadout=AddKnobLegend(tester,"Knob setpoint", "IM 0.000 A");
            isKnobRange=AddKnobLegend(tester,"Knob range", "0-10 mA");
            imKnobRange=AddKnobLegend(tester,"Knob range", "0-1 A");
            var cap=GameObject.CreatePrimitive(PrimitiveType.Cube);cap.name="Output power key";
            cap.transform.SetParent(power.transform,false);cap.transform.localScale=new Vector3(.024f,.016f,.008f);
            Destroy(cap.GetComponent<Collider>());
            cap.GetComponent<Renderer>().sharedMaterial=positiveLead;
            var label=new GameObject("Power label",typeof(TextMesh));label.transform.SetParent(power.transform,false);
            label.transform.localPosition=new Vector3(0,.016f,-.006f);
            var text=label.GetComponent<TextMesh>();text.text="POWER";text.characterSize=.0015f;text.fontSize=32;
            text.anchor=TextAnchor.MiddleCenter;text.color=Color.black;
            foreach(var knob in GetComponentsInChildren<InstrumentControl>().Where(c=>c.action<=InstrumentAction.ImKnob)) {
                var marker=tester.GetComponentsInChildren<Transform>().Where(t=>t.name=="KnobIndex")
                    .OrderBy(t=>(t.position-knob.transform.position).sqrMagnitude).First();
                var pivot=new GameObject("Knob pointer pivot").transform;pivot.SetParent(tester,false);
                pivot.localPosition=new Vector3(knob.transform.localPosition.x,.070f,-.1882f);
                marker.SetParent(pivot,true);knob.pointer=pivot;
            }
            lowQuality=PlayerPrefs.GetInt("LowQuality",1)==1;ApplyQuality();
            RefreshKnobLegends();
            if(!ValidationLaunch) {PhysicalModelEnabled=true;Refresh();}
        }

        private InstrumentControl AddInstrumentControl(Transform parent,string name,InstrumentAction action,Vector3 position,Vector3 size)
        {
            var go=new GameObject(name,typeof(BoxCollider),typeof(InstrumentControl));go.transform.SetParent(parent,false);
            go.transform.localPosition=position;go.GetComponent<BoxCollider>().size=size;
            var control=go.GetComponent<InstrumentControl>();control.action=action;return control;
        }

        private static TextMesh AddKnobLegend(Transform parent,string name,string caption)
        {
            var template=parent.GetComponentsInChildren<TextMesh>().FirstOrDefault(t=>t.font!=null);
            var label=new GameObject(name,typeof(TextMesh));label.transform.SetParent(parent,false);
            var text=label.GetComponent<TextMesh>();text.text=caption;text.characterSize=.00072f;text.fontSize=32;
            text.font=template?.font ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.characterSize=.00072f;text.fontSize=32;
            text.fontStyle=FontStyle.Normal;text.alignment=TextAlignment.Center;text.anchor=TextAnchor.MiddleCenter;
            text.color=Color.black;
            var targetRenderer=label.GetComponent<MeshRenderer>();
            var templateRenderer=template?.GetComponent<MeshRenderer>();
            if(templateRenderer!=null&&templateRenderer.sharedMaterial!=null)targetRenderer.sharedMaterial=templateRenderer.sharedMaterial;
            label.AddComponent<DepthLabel>();
            return text;
        }

        private void RefreshKnobLegends()
        {
            if(isKnobReadout==null)return;
            FitFaceplateLabel(isKnobReadout,"IS "+Circuit.IsSetpoint.ToString("0.00")+" mA",.035f,.098f,.051f,.006f);
            FitFaceplateLabel(imKnobReadout,"IM "+Circuit.ImSetpoint.ToString("0.000")+" A",.091f,.098f,.051f,.006f);
            FitFaceplateLabel(isKnobRange,"0-10 mA",.035f,.043f,.047f,.0045f);
            FitFaceplateLabel(imKnobRange,"0-1 A",.091f,.043f,.047f,.0045f);
            FitFaceplateLabel(outputMeterCaption,"IS / mA   |   IM / A",.068f,.105f,.100f,.006f);
        }

        private static void FitFaceplateLabel(TextMesh label,string value,float x,float y,float width,float height)
        {
            label.text=value;
            // Fit actual glyph bounds, since the WebGL Noto font differs from the
            // editor font. The rectangles leave a gap above the 93 mm dial ticks
            // and below the 103 mm lower edge of the meter's unit caption.
            var bounds=label.GetComponent<MeshRenderer>().localBounds;
            if(bounds.size.x<=.000001f||bounds.size.y<=.000001f) {
                label.transform.localScale=Vector3.one;
                label.transform.localPosition=new Vector3(x,y,-.1595f);
                return; // A dynamic font can rebuild its atlas on its first frame.
            }
            float scale=Mathf.Min(width/bounds.size.x,height/bounds.size.y);
            label.transform.localScale=Vector3.one*scale;
            // All captions occupy the same faceplate plane: no floating text,
            // no perspective parallax between the label and its dial.
            label.transform.localPosition=new Vector3(x,y,-.1595f)-bounds.center*scale;
        }

        public void OperateInstrument(InstrumentAction action,float delta=0)
        {
            if(RejectAutomaticEdit()||LessonActive)return;
            if(action==InstrumentAction.Power)TogglePower();
            else if(action==InstrumentAction.Meter){showIm=!showIm;Refresh();}
            else {
                bool primary=action==InstrumentAction.IsKnob;
                SetCurrents(primary?Circuit.IsSetpoint+delta:Circuit.IsSetpoint,primary?Circuit.ImSetpoint:Circuit.ImSetpoint+delta);
            }
        }

        private InstrumentControl HoveredControl()
        {
            var hits = Physics.RaycastAll(view.ScreenPointToRay(Input.mousePosition), 6);
            if (hits.Length == 0) return null;
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            // 优先命中仪器控件（旋钮 / POWER / Meter），即使被仪器整体碰撞体遮挡
            InstrumentControl ctrl = null;
            foreach (var h in hits)
            {
                ctrl = h.collider.GetComponentInParent<InstrumentControl>();
                if (ctrl != null) break;
            }
            return ctrl;
        }

        private bool HandleInstrumentInput()
        {
            InstrumentPointerCaptured=false;
            if(HasOpenDialog||dataVisible||LessonActive){draggingKnob=false;return false;}
            InstrumentControl hovered=HoveredControl();
            bool overUi=hovered==null&&EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject();
            InstrumentPointerCaptured=hovered!=null||draggingKnob;
            if(hovered!=null && Input.GetMouseButtonDown(0)) {
                if(hovered.action<=InstrumentAction.ImKnob) {
                    activeKnob=hovered;draggingKnob=true;
                    knobScreen=view.WorldToScreenPoint(hovered.transform.position);
                    var mp0=Input.mousePosition;
                    knobLastAngle=Mathf.Atan2(mp0.y-knobScreen.y,mp0.x-knobScreen.x)*Mathf.Rad2Deg;
                    knobStartValue=hovered.action==InstrumentAction.IsKnob?Circuit.IsSetpoint:Circuit.ImSetpoint;
                    knobAccumDeg=0f;
                    Refresh("按住旋钮做旋转拖动（像转真旋钮），也可滚轮微调。IS 每格 0.05 mA，IM 每格 0.005 A");
                } else OperateInstrument(hovered.action);
                return true;
            }
            if(draggingKnob) {
                if(!Input.GetMouseButton(0)){draggingKnob=false;return true;}
                bool primary=activeKnob.action==InstrumentAction.IsKnob;
                var mp=Input.mousePosition;
                float ang=Mathf.Atan2(mp.y-knobScreen.y,mp.x-knobScreen.x)*Mathf.Rad2Deg;
                float d=ang-knobLastAngle;
                if(d>180f)d-=360f;else if(d<-180f)d+=360f;
                knobLastAngle=ang;knobAccumDeg+=d;
                float step=primary?.05f:.005f;
                float desired=knobStartValue-knobAccumDeg/(primary?25f:250f);
                desired=Mathf.Round(desired/step)*step;
                desired=Mathf.Clamp(desired,0f,primary?10f:1f);
                OperateInstrument(activeKnob.action,desired-(primary?Circuit.IsSetpoint:Circuit.ImSetpoint));return true;
            }
            if(hovered!=null&&hovered.action<=InstrumentAction.ImKnob&&Input.mouseScrollDelta.y!=0)
                OperateInstrument(hovered.action,Input.mouseScrollDelta.y*(hovered.action==InstrumentAction.IsKnob?.05f:.005f));
            return hovered!=null;
        }

        private void RefreshTeaching()
        {
            UpdateLessonLayout();
            foreach(var knob in GetComponentsInChildren<InstrumentControl>())if(knob.pointer!=null)
                knob.pointer.localRotation=Quaternion.Euler(0,0,125-250*(knob.action==InstrumentAction.IsKnob?Circuit.IsSetpoint/10:Circuit.ImSetpoint));
            RefreshKnobLegends();
            if(LessonActive||automaticExperimentRunning)return;
            if(!ValidationLaunch)automaticExperimentButton.text="观看演示";
            string hint;
            var report=Circuit.Evaluate();
            if(!report.Ready)hint="学生实验 · 先接线\n点击仪器端子，再点击实验箱端子；逐根连接。可接错、拆线后重试。";
            else if(!Circuit.Powered)hint="学生实验 · 检查并通电\n两路电流归零，点击测试仪红色 POWER 键。点击刀闸可换向。";
            else if(!ExperimentModel.SamplingStable)hint="学生实验 · 调节并观察\n按住 IS / IM 旋钮做旋转拖动，或滚轮微调；等待显示稳定，再记录。";
            else hint="学生实验 · 四方向采样 "+ExperimentModel.MeasurementCount+"/4\n记录后归零断电、操作刀闸换向，再恢复相同电流；重复四个方向。";
            automaticProgressText.text=hint;
        }

        public void BeginLesson()
        {
            if(WebBridge.Instance!=null && WebBridge.Instance.Finished){WebBridge.Instance.Abnormal("SESSION_FINISHED","ResetExperiment is required.");return;}
            if(LessonActive||HasOpenDialog)return;
            studentModel=ExperimentModel;studentCircuit=Circuit;studentPhysical=PhysicalModelEnabled;
            studentStageX=stageX;studentStageY=stageY;
            string demoRoot=Environment.GetCommandLineArgs().Contains("-hall-teaching-check")?Path.GetDirectoryName(studentModel.StorageFolder):ExperimentStorage.Root;
            ExperimentModel=new HallExperimentModel {
                StorageFolder=Path.Combine(demoRoot,"演示数据",Guid.NewGuid().ToString("N")),
                RecoveryFolder=Path.Combine(demoRoot,"演示恢复")};
            Circuit=new THHCircuit();PhysicalModelEnabled=true;MoveStage(0,0);LessonActive=true;LessonPlaying=false;LessonStep=0;
            SelectedPin=null;lessonControls.gameObject.SetActive(true);automaticExperimentButton.text="返回学生实验";
            ShowTab(0);FocusView(0);renderedVersion=-1;Refresh(null,true);ToggleAutomaticExperiment();
        }
        public void NextLessonStep(){if(LessonWaiting)LessonWaiting=false;}
        public void ToggleLessonPlayback(){LessonPlaying=!LessonPlaying;lessonPlayButton.text=LessonPlaying?"暂停播放":"连续播放";}
        public void ReplayLesson(){if(!LessonActive)return;EndLesson();BeginLesson();}
        public void EndLesson()
        {
            if(!LessonActive)return;
            if(automaticExperimentRunning)StopAutomaticExperiment("演示结束");
            ExperimentModel=studentModel;Circuit=studentCircuit;PhysicalModelEnabled=studentPhysical;
            LessonActive=false;LessonWaiting=false;LessonPlaying=false;lessonFrom=lessonTo=null;lessonPlayButton.text="连续播放";
            lessonControls.gameObject.SetActive(false);MoveStage(studentStageX,studentStageY);SelectedPin=null;
            ShowTab(0);FocusView(0);renderedVersion=-1;Refresh("已返回学生实验；原接线、参数、未完成采样和数据均已保留",true);
        }

        private IEnumerator ExplainStep(string message,int focus=-1,Pin? first=null,Pin? second=null)
        {
            if(!LessonActive)yield break;
            LessonStep++;LessonWaiting=true;LessonInstruction=message;lessonFrom=first;lessonTo=second;
            if(focus>=0)FocusView(focus);
            SetAutomaticStatus("演示 · 第 "+LessonStep+" 步\n"+message);
            foreach(var t in terminals)t.Highlight(t.pin==first||t.pin==second);
            float elapsed=0;
            while(LessonWaiting) {
                if(LessonPlaying){elapsed+=Time.unscaledDeltaTime;if(elapsed>=2.5f)LessonWaiting=false;}
                yield return null;
            }
            lessonFrom=lessonTo=null;
            foreach(var t in terminals)t.Highlight(false);
        }

        private void UpdateLessonLayout()
        {
            if(lessonControls==null)return;
            lessonControls.offsetMin=dataVisible?new Vector2(880,-148):new Vector2(478,-224);
            lessonControls.offsetMax=dataVisible?new Vector2(1376,-102):new Vector2(974,-178);
            lessonNextButton.transform.parent.GetComponent<Button>().interactable=LessonWaiting;
            lessonPlayButton.transform.parent.GetComponent<Button>().interactable=automaticExperimentRunning;
        }

        private IEnumerator DemonstrateCurrentAdjustment(float targetIs,float targetIm)
        {
            float elapsed=0;
            while(elapsed<.7f) {
                elapsed+=Time.deltaTime;
                float t=Mathf.Clamp01(elapsed/.7f);
                Circuit.SetCurrent(targetIs*t,targetIm*t);Refresh();
                yield return null;
            }
            Circuit.SetCurrent(targetIs,targetIm);
        }

        private void ToggleQuality(){lowQuality=!lowQuality;PlayerPrefs.SetInt("LowQuality",lowQuality?1:0);ApplyQuality();}
        private void ApplyQuality()
        {
            QualitySettings.shadows=lowQuality?ShadowQuality.Disable:ShadowQuality.All;
            QualitySettings.antiAliasing=lowQuality?0:2;QualitySettings.vSyncCount=0;
            Application.targetFrameRate=lowQuality?30:60;
            if(qualityButton!=null)qualityButton.text=lowQuality?"画质：节能 30 FPS":"画质：标准 60 FPS";
        }
    }
}
