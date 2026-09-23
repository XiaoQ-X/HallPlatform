using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HallLab
{
    // Explicit opt-in; data and recovery are isolated from the user's session.
    public sealed class AutomaticExperimentChecks : MonoBehaviour
    {
        private int assertions;
        private string folder;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Enable()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-hall-auto-check")>=0)
                new GameObject("Automatic experiment checks").AddComponent<AutomaticExperimentChecks>();
        }
        private void Check(bool ok,string label)
        {assertions++;if(!ok)throw new Exception(label);}
        private IEnumerator Start()
        {
            yield return null;
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-hall-output");
            if(index<0||index+1>=args.Length){Application.Quit(1);yield break;}
            folder=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(folder);
            QualitySettings.vSyncCount=0;Application.targetFrameRate=120;
            Time.timeScale=4f;Time.maximumDeltaTime=.05f;
            var routine=Run();
            while(true)
            {
                object step=null;bool more=false;Exception failure=null;
                try{more=routine.MoveNext();if(more)step=routine.Current;}
                catch(Exception error){failure=error;}
                if(failure!=null){File.WriteAllText(Path.Combine(folder,"result.txt"),failure.ToString());Debug.LogException(failure);Application.Quit(1);yield break;}
                if(!more)break;
                yield return step;
            }
            File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: "+assertions+" automatic experiment and UI assertions; 4x simulation clock.\n");
            Debug.Log("THH_AUTOMATIC_CHECK_PASS");Application.Quit(0);
        }
        private IEnumerator Run()
        {
            var app=FindObjectOfType<THHWorkbench>();
            var model=app.ExperimentModel;
            model.StorageFolder=Path.Combine(folder,"data");model.RecoveryFolder=Path.Combine(folder,"recovery");
            app.ShowSaveAs();Check(app.HasOpenDialog,"save-as modal visible");app.HandleEscape();Check(!app.HasOpenDialog,"Escape closes modal");
            app.ToggleDataPanel();
            Button Named(string name)=>Resources.FindObjectsOfTypeAll<Button>().First(b=>b.name==name&&b.gameObject.scene.IsValid());
            Check(!Named("上一页").interactable&&!Named("下一页").interactable,"empty pagination disabled");
            Check(!Named("撤销删除").interactable,"empty undo disabled");
            Check(!Resources.FindObjectsOfTypeAll<Dropdown>().First(d=>d.name=="Series selector").interactable,"empty series disabled");
            app.ShowTab(0);
            app.ToggleAutomaticExperiment();
            Check(app.AutomaticExperimentRunning,"automatic start");
            float deadline=Time.realtimeSinceStartup+300f;
            int previousIs=app.Circuit.Position(0),previousIm=app.Circuit.Position(2);
            while(app.AutomaticExperimentRunning)
            {
                Check(Time.realtimeSinceStartup<deadline,"automatic timeout");
                if(previousIs!=app.Circuit.Position(0)||previousIm!=app.Circuit.Position(2))
                    Check(model.ActualIsMilliamps<=.1f&&model.ActualImAmps<=.03f,"direction changed only after zero (one frame tolerance)");
                previousIs=app.Circuit.Position(0);previousIm=app.Circuit.Position(2);
                app.SetCurrents(9,.9f);app.TogglePhysicalModel();app.ClearWiring();app.ClearPendingReadings();
                Check(app.PhysicalModelEnabled,"manual model toggle blocked");
                yield return null;
            }
            Check(app.AutomaticExperimentSucceeded,app.AutomaticExperimentStatus);
            Check(model.Rows.Count==12&&app.AutomaticCompletedGroups==12,"12 complete groups");
            Check(model.Rows.Select(r=>r.seriesId).Distinct().Count()==2,"two independent batches");
            foreach(bool sweep in new[]{true,false})
            {
                var rows=model.Rows.Where(r=>r.label==(sweep?"VH-IS":"VH-IM")).ToArray();
                Check(rows.Length==6,"six rows per sweep");
                Check(rows.Select(r=>r.SweepCurrent).Distinct().Count()==6,"six distinct current points");
                Check(ExperimentFit.TryFit(rows,sweep,out _,out _,out _,out _),"fit each sweep");
                foreach(var row in rows)
                {
                    Check(Mathf.Abs(row.vh-(row.v1-row.v2+row.v3-row.v4)/4)<.00001f,"four-direction formula");
                    Check(row.b1>0&&row.b2<0&&row.b3<0&&row.b4>0,"four magnetic polarities");
                }
            }
            Check(File.ReadAllLines(model.CsvFilePath).Length==13&&!model.HasUnsavedRows,"CSV contains 12 rows");
            Check(!app.Circuit.Powered&&model.MeasurementCount==0,"complete shuts down");
            app.ToggleAutomaticExperiment();
            deadline=Time.realtimeSinceStartup+30f;
            while(model.MeasurementCount==0&&app.AutomaticExperimentRunning&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(model.MeasurementCount>0,"restart reaches partial group");
            app.ToggleAutomaticExperiment();
            int pending=model.MeasurementCount;
            Check(!app.AutomaticExperimentRunning&&!app.Circuit.Powered&&pending>0,"stop preserves partial group");
            Check(Named("自动实验").GetComponentInChildren<Text>().text=="自动实验","stop resets button text");
            app.ToggleAutomaticExperiment();Check(!app.AutomaticExperimentRunning&&model.MeasurementCount==pending,"pending group blocks restart");
            app.ClearPendingReadings();
            app.ToggleAutomaticExperiment();
            deadline=Time.realtimeSinceStartup+30f;
            while(model.MeasurementCount==0&&app.AutomaticExperimentRunning&&Time.realtimeSinceStartup<deadline)yield return null;
            string blocked=Path.Combine(folder,"not-a-directory");File.WriteAllText(blocked,"storage failure test");
            string previousFolder=model.StorageFolder;model.StorageFolder=blocked;
            deadline=Time.realtimeSinceStartup+60f;
            while(app.AutomaticExperimentRunning&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!app.AutomaticExperimentRunning&&!app.AutomaticExperimentSucceeded&&!app.Circuit.Powered,"storage failure stops output");
            Check(model.Rows.Count==13&&model.HasUnsavedRows&&File.Exists(model.RecoveryPath),"failed save retains complete row and recovery");
            Check(model.Rows.Last().seriesId!=model.Rows[0].seriesId,"repeat experiment does not mix earlier batch");
            model.StorageFolder=previousFolder;Check(model.SaveData(),"retry save");
            app.ToggleAutomaticExperiment();app.ResetControls();
            Check(!app.AutomaticExperimentRunning&&!app.Circuit.Powered,"reset cancels automatic run");
            Check(model.Rows.Count==13,"reset preserves complete rows");
        }
    }
}
