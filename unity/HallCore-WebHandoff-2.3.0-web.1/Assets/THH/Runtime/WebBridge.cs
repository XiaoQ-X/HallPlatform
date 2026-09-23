using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace HallLab
{
    // Public JSON boundary. The host owns persistence and grading.
    [Preserve]
    public sealed class WebBridge : MonoBehaviour
    {
        public static WebBridge Instance { get; private set; }
        private THHWorkbench app;
        private string sessionId, caseId = "default";
        private int step = 1, sequence;
        private float started;
        private bool finished;
        private string previousState;
        private float maxIs = 10, maxIm = 1;
        private readonly List<string> events = new List<string>();
        public event Action<WebEvent> Emitted;
        public bool Finished => finished;
        public string SessionId => sessionId;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void HallWebReport(string name, string json);
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
#if !UNITY_WEBGL
            if(!Environment.GetCommandLineArgs().Contains("-hall-web-check"))return;
#endif
            if (Instance == null && FindObjectOfType<THHWorkbench>() != null)
                new GameObject("WebBridge").AddComponent<WebBridge>();
        }
        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
        }
        private void Start()
        {
            app = FindObjectOfType<THHWorkbench>();
            app.InitializePreview(); NewSession();
            Emit("OnReady", "ready", "", true);
        }
        private void NewSession()
        {
            sessionId = Guid.NewGuid().ToString("N"); started = Time.realtimeSinceStartup;
            sequence = 0; step = 1; finished = false; events.Clear();
            previousState=null;
        }
        private void Update()
        {
            if(app==null||sessionId==null||app.LessonActive)return;
            var r=app.Circuit.Evaluate();
            string value=r.Ready+":"+app.Circuit.Powered+":"+app.ExperimentModel.SamplingStable+":"+app.ExperimentModel.MeasurementCount+":"+app.ExperimentModel.Rows.Count+":"+finished;
            if(value!=previousState){previousState=value;Emit("OnStepState","stateChanged","",true);}
        }
        private bool Available()
        {
            if(app == null || sessionId == null) return false;
            if(finished) { Abnormal("SESSION_FINISHED", "ResetExperiment is required."); return false; }
            if(app.LessonActive) { Abnormal("DEMO_ACTIVE", "Return to the student session first."); return false; }
            return true;
        }
        private T Parse<T>(string json) where T : class
        {
            if(string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{") || json.Length > 32768)
                throw new ArgumentException("Expected a JSON object (max 32 KiB).");
            var value=Activator.CreateInstance<T>();
            JsonUtility.FromJsonOverwrite(json,value);
            return value;
        }
        [Preserve] public void InitExperiment(string json)
        {
            try {
                var c = Parse<WebInit>(json);
                if(c.schemaVersion != 1 || string.IsNullOrWhiteSpace(c.caseId) || c.caseId.Length > 128 ||
                    !Finite(c.maxIs_mA) || !Finite(c.maxIm_A) || c.maxIs_mA < .15f || c.maxIs_mA > 10 || c.maxIm_A < .05f || c.maxIm_A > 1 ||
                    c.material != "n-silicon" || c.thickness_mm != .5f)
                    throw new ArgumentException("Unsupported configuration. Fixed n-silicon, 0.5 mm; IS <= 10 mA, IM <= 1 A.");
                // Initialization is deliberately non-destructive for an active experiment.
                if(app.LessonActive || app.Circuit.Powered || app.ExperimentModel.Rows.Count > 0 || app.ExperimentModel.MeasurementCount > 0)
                    throw new InvalidOperationException("ResetExperiment before initializing an active session.");
                caseId=c.caseId;maxIs=c.maxIs_mA;maxIm=c.maxIm_A;
                ResetExperiment(); Emit("OnCommandResult", "InitExperiment", "", true);
            } catch(Exception e) { Abnormal("INVALID_INIT", e.Message); }
        }
        [Preserve] public void SetParameter(string json)
        {
            if(!Available()) return;
            try {
                var p=Parse<WebParameter>(json);
                if(!Finite(p.value)) throw new ArgumentException("Non-finite value.");
                switch(p.name) {
                    case "IS_mA": if(p.value<0||p.value>maxIs)throw new ArgumentOutOfRangeException(p.name); app.SetCurrents(p.value,app.Circuit.ImSetpoint); break;
                    case "IM_A": if(p.value<0||p.value>maxIm)throw new ArgumentOutOfRangeException(p.name); app.SetCurrents(app.Circuit.IsSetpoint,p.value); break;
                    case "isSwitch": case "imSwitch": case "measurementSwitch":
                        if(p.value != -1 && p.value != 0 && p.value != 1)throw new ArgumentOutOfRangeException(p.name);
                        if(app.Circuit.Powered) {Abnormal("LIVE_SWITCH_CHANGE",p.name);return;}
                        app.SetSwitch(p.name=="isSwitch"?0:p.name=="imSwitch"?2:1,(int)p.value);break;
                    case "stageX": case "stageY":
                        if(p.value < -1 || p.value > 1)throw new ArgumentOutOfRangeException(p.name);
                        app.WebMoveStage(p.name,p.value);break;
                    case "sweep":
                        if(p.value != 0 && p.value != 1)throw new ArgumentOutOfRangeException(p.name);
                        if(!app.ExperimentModel.SelectSweep(p.value==0))throw new InvalidOperationException(app.ExperimentModel.LastRecordMessage);break;
                    case "power":
                        if(p.value!=0 && p.value!=1)throw new ArgumentOutOfRangeException(p.name);
                        if(app.Circuit.Powered != (p.value==1))app.TogglePower();
                        if(app.Circuit.Powered != (p.value==1))return;break;
                    default: throw new ArgumentException("Unsupported parameter: "+p.name);
                }
                Emit("OnCommandResult","SetParameter",json,true);
            } catch(Exception e) {Abnormal("INVALID_PARAMETER",e.Message);}
        }
        [Preserve] public void GotoStep(string json)
        {
            if(!Available())return;
            try {
                int target=Parse<WebStep>(json).step;
                if(target<1||target>6)throw new ArgumentOutOfRangeException("step");
                // Navigation selects a panel; it never marks a task complete or changes wiring.
                step=target;app.ShowTab(new[]{2,0,4,1,3,3}[target-1]);
                Emit("OnStepState","navigate","",true);
            } catch(Exception e){Abnormal("INVALID_STEP",e.Message);}
        }
        [Preserve] public void ResetExperiment()
        {
            if(app==null)return;
            if(app.LessonActive)app.EndLesson();
            NewSession();app.WebNewExperiment();
            Emit("OnStepState","reset","",true);
        }
        [Preserve] public void FinishFromWeb()
        {
            if(!Available())return;
            if(app.ExperimentModel.Rows.Count==0 || app.ExperimentModel.MeasurementCount>0) {
                Abnormal("EXPERIMENT_INCOMPLETE","At least one complete group and no pending directions required.");return;
            }
            app.Circuit.TurnOff();finished=true;step=6;
            Emit("OnExperimentComplete","finish","",true);
        }
        [Preserve] public void RequestExit()
        {
            if(!Available())return;
            if(app.LessonActive)app.EndLesson();
            app.Circuit.TurnOff();
            Emit("OnExitRequested","exit","",true);
        }
        [Preserve] public void RequestSnapshot() { if(app!=null)Emit("OnSnapshot","snapshot","",true); }
        [Preserve] public void RecordMeasurement() {if(Available())app.RecordCurrentReading();}
        [Preserve] public void ConnectTerminals(string json)
        {
            if(!Available())return;
            try {
                var c=Parse<WebCable>(json);
                if(c.a<0||c.a>=12||c.b<0||c.b>=12||c.a==c.b)throw new ArgumentException("External terminal IDs must be distinct and within 0..11.");
                app.CancelConnection();app.ChooseTerminal((Pin)c.a);app.ChooseTerminal((Pin)c.b);
            }catch(Exception e){Abnormal("INVALID_CABLE",e.Message);}
        }
        [Preserve] public void UndoLastCable()
        {
            if(!Available())return;
            app.UndoLastCable();
        }
        public bool ValidateCurrents(float isValue,float imValue)
        {
            if(app==null||app.LessonActive)return true;
            if(finished){Abnormal("SESSION_FINISHED", "ResetExperiment is required.");return false;}
            if(!Finite(isValue)||!Finite(imValue)||isValue<0||isValue>maxIs||imValue<0||imValue>maxIm) {
                Abnormal("PARAMETER_OUT_OF_RANGE", "IS/IM outside configured range.");return false;
            }
            return true;
        }
        public void Operation(HallExperimentModel model,string name,string detail,bool success)
        {
            if(app!=null && model==app.ExperimentModel)Emit("OnOperationLog",name,detail,success);
        }
        public void Measure(HallExperimentModel model, WebMeasurement value)
        {
            if(app==null||model!=app.ExperimentModel)return;
            var e=Create("OnMeasureData","record","",true);e.measurement=value;Send(e);
        }
        public void Abnormal(string code,string detail)
        {
            if(app==null||sessionId==null)return;
            Emit("OnAbnormalEvent",code,detail,false);
        }
        public void CircuitFailure(CircuitReport r)
        {
            Abnormal(r.SourceShort?"CIRCUIT_SOURCE_SHORT":r.CrossCircuit?"CIRCUIT_CROSS_CONNECTED":r.MeterShort?"CIRCUIT_METER_SHORT":!r.Ready?"CIRCUIT_INCOMPLETE":"POWER_ON_NONZERO_CURRENT",r.Summary);
        }
        private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        private WebEvent Create(string name,string code,string detail,bool success)
        {
            return new WebEvent {type=name,eventId=sessionId+":"+(++sequence),sessionId=sessionId,caseId=caseId,
                timestamp=DateTime.UtcNow.ToString("O"),elapsedMs=(long)((Time.realtimeSinceStartup-started)*1000),
                step=step,code=code,detail=detail,success=success,isDemo=app.LessonActive,
                state=WebState.From(app),maxIs_mA=maxIs,maxIm_A=maxIm};
        }
        private void Emit(string name,string code,string detail,bool success)
        {
            if(app==null||sessionId==null)return;
            var e=Create(name,code,detail,success);
            if(name=="OnSnapshot"||name=="OnExperimentComplete") {
                e.rows=app.ExperimentModel.Rows.ToArray(); e.history=events.ToArray();
            }
            Send(e);
        }
        private void Send(WebEvent e)
        {
            // Snapshots must not recursively contain earlier snapshots.
            if(e.history==null && !e.isDemo)events.Add(JsonUtility.ToJson(e));
            Emitted?.Invoke(e);
#if UNITY_WEBGL && !UNITY_EDITOR
            HallWebReport(e.type,JsonUtility.ToJson(e));
#endif
        }
        private void OnDestroy(){if(Instance==this)Instance=null;}
    }
    [Serializable] public sealed class WebInit {public int schemaVersion;public string caseId,material;public float thickness_mm=float.NaN,maxIs_mA=float.NaN,maxIm_A=float.NaN;}
    [Serializable] public sealed class WebParameter {public string name;public float value=float.NaN;}
    [Serializable] public sealed class WebStep {public int step;}
    [Serializable] public sealed class WebCable {public int a=-1,b=-1;}
    [Serializable] public sealed class WebMeasurement {
        public string groupId,timestamp; public int slot,isDirection,imDirection,voltageDirection;
        public float IS_mA,IM_A,isSetpoint_mA,imSetpoint_A,B_T,rawVoltage_mV,VH_mV,normalizedVH_mV;
        // Explicit model components keep the four transverse effects visible
        // in exported events: VH, VE, VN (Nernst) and VRL (Righi–Leduc).
        public float V0_mV,VE_mV,VN_mV,VRL_mV,noise_mV;
        public bool groupComplete; public ExperimentRow row;
    }
    [Serializable] public sealed class WebState {
        public bool powered,circuitValid,stable,finished;public int pendingDirections,completeGroups;
        public float IS_mA,IM_A,B_T,rawVoltage_mV;
        public static WebState From(THHWorkbench a)=>new WebState{powered=a.Circuit.Powered,circuitValid=a.Circuit.Evaluate().Ready,
            stable=a.ExperimentModel.SamplingStable,finished=WebBridge.Instance!=null&&WebBridge.Instance.Finished,
            pendingDirections=a.ExperimentModel.MeasurementCount,completeGroups=a.ExperimentModel.Rows.Count,
            IS_mA=a.ExperimentModel.ActualIsMilliamps,IM_A=a.ExperimentModel.ActualImAmps,
            B_T=a.ExperimentModel.MagneticFieldTesla,rawVoltage_mV=a.ExperimentModel.LiveReading.totalMillivolts};
    }
    [Serializable] public sealed class WebEvent {
        public int schemaVersion=1,step;public string type,eventId,sessionId,caseId,timestamp,code,detail;
        public long elapsedMs;public bool success,isDemo;public float maxIs_mA,maxIm_A;
        public WebState state;public WebMeasurement measurement;public ExperimentRow[] rows;public string[] history;
    }
}
