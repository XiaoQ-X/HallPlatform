using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HallLab
{
    public sealed class TeachingChecks : MonoBehaviour
    {
        private int count;
        private string folder;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Enable()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-hall-teaching-check")>=0)
                new GameObject("Teaching checks").AddComponent<TeachingChecks>();
        }
        private void Check(bool ok,string title){count++;if(!ok)throw new Exception(title);}
        private IEnumerator Start()
        {
            yield return null;yield return null;
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-hall-output");
            if(index<0||index+1>=args.Length){Application.Quit(1);yield break;}
            folder=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(folder);
            Time.timeScale=4;Time.maximumDeltaTime=.05f;Application.targetFrameRate=120;
            var routine=Run();
            while(true) {
                bool more=false;object next=null;Exception failure=null;
                try{more=routine.MoveNext();if(more)next=routine.Current;}catch(Exception ex){failure=ex;}
                if(failure!=null){File.WriteAllText(Path.Combine(folder,"result.txt"),failure.ToString());Debug.LogException(failure);Application.Quit(1);yield break;}
                if(!more)break;yield return next;
            }
            File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: "+count+" teaching assertions. Student wiring, 3D controls, paused steps, replay, full guided demonstration, session isolation, empty recovery, damaged recovery fallback.\n");
            Debug.Log("THH_TEACHING_CHECK_PASS");Application.Quit(0);
        }
        private IEnumerator Run()
        {
            var app=FindObjectOfType<THHWorkbench>();var model=app.ExperimentModel;
            model.StorageFolder=Path.Combine(folder,"student");model.RecoveryFolder=Path.Combine(folder,"recovery");
            Check(app.PhysicalModelEnabled&&!app.LessonActive,"student mode by default");
            Check(!Resources.FindObjectsOfTypeAll<Button>().Any(b=>b.gameObject.scene.IsValid()&&(b.name=="标准接线"||b.name=="自动实验")),"no instant-complete UI");
            RuntimeSmoke.Capture(app.view,Path.Combine(folder,"01-student-wiring.png"));
            RuntimeSmoke.Capture(app.view,Path.Combine(folder,"01-student-1280x800.png"),1280,800);
            app.StandardWiring();Check(app.Circuit.Cables.Count==0,"student standard helper blocked");
            app.ChooseTerminal(Pin.IsPlus);app.ChooseTerminal(Pin.ImPlus);
            Check(app.Circuit.Cables.Count==1&&!app.Circuit.Evaluate().Ready,"wrong wiring retained");
            app.TogglePower();Check(!app.Circuit.Powered,"wrong wiring cannot energize");app.ClearWiring();
            for(int i=0;i<6;i++){app.ChooseTerminal((Pin)i);app.ChooseTerminal((Pin)(i+6));Check(app.Circuit.Cables.Count==i+1,"one wire per gesture");}
            app.FocusView(1);Physics.SyncTransforms();
            foreach(var control in app.GetComponentsInChildren<InstrumentControl>()) {
                var start=control.transform.position+Vector3.back*.08f;
                Check(Physics.Raycast(start,Vector3.forward,out var hit,.1f)&&hit.collider.GetComponentInParent<InstrumentControl>()==control,"instrument control reachable: "+control.action+" hit="+(hit.collider==null?"none":hit.collider.name)+" control="+control.transform.position);
                var ray=app.view.ScreenPointToRay(app.view.WorldToScreenPoint(control.transform.position));
                Check(Physics.Raycast(ray,out var cameraHit,6)&&cameraHit.collider.GetComponentInParent<InstrumentControl>()==control,"camera can select instrument control: "+control.action);
            }
            app.OperateInstrument(InstrumentAction.Power);Check(app.Circuit.Powered,"physical power button");
            app.OperateInstrument(InstrumentAction.IsKnob,2);app.OperateInstrument(InstrumentAction.ImKnob,.6f);
            Check(app.Circuit.IsSetpoint==2&&app.Circuit.ImSetpoint==.6f,"physical knobs drive setpoints");
            for(int i=0;i<240;i++)model.Tick(app.Circuit,.05f);
            app.RecordCurrentReading();Check(model.MeasurementCount==1,"student pending reading");
            app.MoveStage(.2f,.3f);var originalCircuit=app.Circuit;
            app.BeginLesson();
            Check(app.LessonActive&&app.ExperimentModel!=model&&app.Circuit!=originalCircuit,"demo owns separate state");
            Check(app.LessonWaiting&&app.Circuit.Cables.Count==0,"demo waits before action");
            int step=app.LessonStep;
            for(int i=0;i<5;i++)yield return null;
            Check(app.LessonStep==step&&app.Circuit.Cables.Count==0,"paused demonstration does not advance");
            app.SetCurrents(9,.9f);app.ChooseTerminal(Pin.IsPlus);Check(app.Circuit.IsSetpoint==0&&!app.SelectedPin.HasValue,"manual demo edits blocked");
            app.NextLessonStep();float deadline=Time.realtimeSinceStartup+10;
            while(!app.LessonWaiting&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(app.LessonWaiting&&app.Circuit.Cables.Count==0,"explain first wire before connecting");
            RuntimeSmoke.Capture(app.view,Path.Combine(folder,"02-demo-wire-step.png"));
            app.NextLessonStep();deadline=Time.realtimeSinceStartup+10;
            while(app.Circuit.Cables.Count==0&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(app.Circuit.Cables.Count==1,"demo connects first wire only");
            app.ReplayLesson();Check(app.Circuit.Cables.Count==0&&app.LessonStep==1,"replay starts isolated demo again");
            app.ToggleLessonPlayback();Check(app.LessonPlaying,"continuous playback");app.ToggleLessonPlayback();Check(!app.LessonPlaying,"pause playback");
            deadline=Time.realtimeSinceStartup+300;bool captured=false;
            while(app.AutomaticExperimentRunning&&Time.realtimeSinceStartup<deadline) {
                if(app.LessonWaiting) {
                    if(!captured&&app.ExperimentModel.MeasurementCount==1) {
                        RuntimeSmoke.Capture(app.view,Path.Combine(folder,"03-demo-four-directions.png"));captured=true;
                    }
                    app.NextLessonStep();
                }
                yield return null;
            }
            Check(app.AutomaticExperimentSucceeded&&app.ExperimentModel.Rows.Count==12,"full guided two-sweep demonstration");
            RuntimeSmoke.Capture(app.view,Path.Combine(folder,"04-demo-results.png"));
            app.EndLesson();
            Check(app.ExperimentModel==model&&app.Circuit==originalCircuit&&model.MeasurementCount==1,"return restores student pending state");
            Check(app.Circuit.Powered&&app.Circuit.IsSetpoint==2&&model.Rows.Count==0,"demo does not leak rows or setpoints");
            app.FocusView(1);app.ShowTab(1);RuntimeSmoke.Capture(app.view,Path.Combine(folder,"05-student-instrument.png"));
            app.TogglePower();model.ClearMeasurementSet();
            // Construct a real four-direction row, then force a CSV failure while deleting the last row.
            for(int slot=0;slot<4;slot++) {
                app.Circuit.TurnOff();app.Circuit.SetSwitch(0,slot<2?1:-1);app.Circuit.SetSwitch(2,slot==0||slot==3?1:-1);
                app.Circuit.TurnOn(out _);app.Circuit.SetCurrent(2,.6f);
                for(int i=0;i<240;i++)model.Tick(app.Circuit,.05f);
                Check(model.RecordCurrentReading(app.Circuit),"recovery setup sampling");
            }
            using(var held=new FileStream(model.CsvFilePath,FileMode.Open,FileAccess.Read,FileShare.None)) {
                Check(!model.SaveData(),"locked CSV save fails");
                model.DeleteRow(model.Rows[0].id);
                Check(model.HasUnsavedRows&&ExperimentRecovery.Read(model.RecoveryPath,out var snap,out _)&&snap.rows.Count==0,"empty snapshot replaces deleted row before crash");
            }
            Check(model.SaveData(),"save empty CSV after unlocking");
            var source=new HallExperimentModel {RecoveryFolder=model.RecoveryFolder};
            Check(source.PersistRecovery(),"valid candidate created");
            string bad=Path.Combine(model.RecoveryFolder,"latest-broken.json");File.WriteAllText(bad,"{}");File.SetLastWriteTimeUtc(bad,DateTime.UtcNow.AddMinutes(1));
            app.ShowRecoveryBrowser();Check(app.HasOpenDialog,"older valid recovery offered");
            var recovery=Resources.FindObjectsOfTypeAll<Button>().First(b=>b.gameObject.scene.IsValid()&&b.name=="恢复数据");recovery.onClick.Invoke();
            Check(!app.HasOpenDialog&&model.HasUnsavedRows,"valid recovery selected despite corrupt latest");
            Check(File.Exists(bad),"corrupt file retained");
            model.SaveData();app.Circuit.TurnOff();
        }
    }
}
