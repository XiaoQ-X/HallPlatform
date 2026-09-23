using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HallLab
{
    public static class ExperimentChecks
    {
        public static string Run(THHWorkbench app, string folder)
        {
            int checks=0;
            void Check(bool ok,string name){checks++;if(!ok)throw new Exception("Experiment check failed: "+name);}
            void Settle(HallExperimentModel model,THHCircuit circuit){for(int i=0;i<240;i++)model.Tick(circuit,.05f);}
            var fitRows=new List<ExperimentRow> {
                new ExperimentRow{label="VH-IS",primaryCurrent=1,secondaryCurrent=.6f,vh=-1},
                new ExperimentRow{label="VH-IS",primaryCurrent=2,secondaryCurrent=.6f,vh=-3},
                new ExperimentRow{label="VH-IS",primaryCurrent=3,secondaryCurrent=.6f,vh=-5}
            };
            Check(ExperimentFit.TryFit(fitRows,true,out float slope,out float offset,out float r2,out _) &&
                Mathf.Abs(slope+2)<1e-6f && Mathf.Abs(offset-1)<1e-6f && Mathf.Abs(r2-1)<1e-6f,"known negative slope, intercept and R squared");
            fitRows[1].primaryCurrent=fitRows[2].primaryCurrent=1;
            Check(!ExperimentFit.TryFit(fitRows,true,out _,out _,out _,out _),"duplicate x rejected");
            fitRows[1].primaryCurrent=2;fitRows[2].primaryCurrent=3;fitRows[1].secondaryCurrent=.8f;
            Check(!ExperimentFit.TryFit(fitRows,true,out _,out _,out _,out _),"mixed fixed current rejected");
            fitRows[1].secondaryCurrent=.6f;fitRows[1].vh=float.NaN;
            Check(!ExperimentFit.TryFit(fitRows,true,out _,out _,out _,out _),"nonfinite fit rejected");
            foreach(var row in fitRows){row.label="VH-IM";row.secondaryCurrent=row.primaryCurrent*.1f;row.primaryCurrent=2;row.vh=3*row.secondaryCurrent+1;}
            Check(ExperimentFit.TryFit(fitRows,false,out slope,out offset,out r2,out _) && Mathf.Abs(slope-3)<.0001f,"IM sweep uses IM as x");

            app.ResetControls();app.ExperimentModel.StorageFolder=Path.Combine(folder,"实验验证数据");
            app.ExperimentModel.RecoveryFolder=Path.Combine(folder,"实验验证数据","recovery-cache");
            app.StandardWiring();app.TogglePhysicalModel();app.ShowTab(1);app.TogglePower();app.SetCurrents(2,.6f);
            var model=app.ExperimentModel;
            Check(!model.RecordCurrentReading(app.Circuit),"unsettled reading rejected");
            Settle(model,app.Circuit);
            Check(model.SamplingStable && model.RecordCurrentReading(app.Circuit) && model.MeasurementCount==1,"stable first direction");
            Check(model.RecordCurrentReading(app.Circuit) && model.MeasurementCount==1,"same direction updates slot without duplicate row");
            Check(!model.SelectSweep(false) && model.SweepIs,"pending group cannot silently change table");
            app.TogglePower();
            Check(!model.RecordCurrentReading(app.Circuit) && model.MeasurementCount==1,"immediate power off blocks stale sample");
            app.SetSwitch(0,-1);app.TogglePower();app.SetCurrents(3,.6f);Settle(model,app.Circuit);
            Check(!model.RecordCurrentReading(app.Circuit) && model.MeasurementCount==1,"mismatched magnitude preserves pending set");
            app.ClearPendingReadings();Check(model.MeasurementCount==0,"pending reset permits recovery");

            void Group(float isValue,float imValue) {
                int[] isSigns={1,1,-1,-1},imSigns={1,-1,-1,1};
                int before=model.Rows.Count;
                for(int slot=0;slot<4;slot++) {
                    if(app.Circuit.Powered)app.TogglePower();
                    app.SetSwitch(0,isSigns[slot]);app.SetSwitch(2,imSigns[slot]);app.SetSwitch(1,1);
                    app.TogglePower();app.SetCurrents(isValue,imValue);Settle(model,app.Circuit);
                    app.RecordCurrentReading();
                    Check(slot==3?model.MeasurementCount==0:model.MeasurementCount==slot+1,"four-direction slot progress");
                }
                Check(model.Rows.Count==before+1,"exactly one row for four directions");
                var row=model.Rows.Last();
                Check(Mathf.Abs(row.vh-(row.v1-row.v2+row.v3-row.v4)/4)<1e-6f,"four-direction composition");
            }
            // More than one page exercises actual recorded rows rather than injected table data.
            for(int i=0;i<11;i++)Group(1+i*.2f,.6f);
            Check(File.Exists(model.CsvFilePath) && File.ReadAllLines(model.CsvFilePath).Length==12,"CSV header and all completed rows");
            Check(!model.HasUnsavedRows,"automatic disk save acknowledged");
            app.ToggleInspector();app.ToggleDataPanel();
            Check(app.DataPanelVisible && GameObject.Find("Experiment data")!=null,"data opens even when inspector hidden");
            Check(app.DisplayedFit.Contains("R²"),"fit displayed for real collected IS sweep");
            var next=UnityEngine.Object.FindObjectsOfType<Button>().First(b=>b.name=="下一页");next.onClick.Invoke();
            Check(UnityEngine.Object.FindObjectsOfType<Text>().Any(t=>t.text.Contains("第 2 / 2 页")),"table pagination reaches eleventh row");
            app.ToggleDataPanel();Check(!app.DataPanelVisible,"close data panel");app.ToggleInspector();
            app.SelectDataSweep(false);
            for(int i=0;i<3;i++)Group(2,.2f+i*.2f);
            app.ToggleDataPanel();Check(app.DisplayedFit.Contains("IM") && app.DisplayedFit.Contains("R²"),"IM table and fit");
            app.ToggleDataPanel();
            Check(File.ReadAllLines(model.CsvFilePath).Length==15,"CSV contains both tables with original column meaning");
            app.TogglePower();app.SetSwitch(1,-1);app.TogglePower();app.SetCurrents(2,.6f);Settle(model,app.Circuit);
            Check(!model.RecordCurrentReading(app.Circuit),"conductivity mode excluded from Hall sampling");
            app.ResetControls();Check(model.Rows.Count==14,"reset device preserves completed experiment records");
            app.SelectDataSweep(true);app.FocusView(0);

            // A path whose parent is a file reliably fails without changing OS permissions.
            string blocked=Path.Combine(folder,"实验验证数据","occupied-file");File.WriteAllText(blocked,"test fixture");
            var failing=new HallExperimentModel{StorageFolder=Path.Combine(blocked,"child"),RecoveryFolder=Path.Combine(folder,"实验验证数据","recovery-cache")};
            var circuit=new THHCircuit();circuit.ConnectStandard();
            for(int slot=0;slot<4;slot++) {
                circuit.TurnOff();circuit.SetSwitch(0,slot<2?1:-1);circuit.SetSwitch(2,slot==0||slot==3?1:-1);
                circuit.TurnOn(out _);circuit.SetCurrent(2,.6f);Settle(failing,circuit);
                Check(failing.RecordCurrentReading(circuit),"sample retained despite unavailable storage");
            }
            Check(failing.Rows.Count==1 && failing.HasUnsavedRows && failing.LastStorageError.Length>0,"failed save keeps row and error");
            failing.StorageFolder=Path.Combine(folder,"实验验证数据","recovered");
            Check(failing.SaveData() && !failing.HasUnsavedRows && File.ReadAllLines(failing.CsvFilePath).Length==2,"retry persists retained row");
            return "PASS: "+checks+" experiment assertions. Stability, four polarities, two tables, pagination, fits, CSV, storage failure/retry and UI visibility.\n";
        }
    }
}
