using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HallLab
{
    public static class RemediationChecks
    {
        public static string Run(THHWorkbench app,string folder)
        {
            int count=0;void Check(bool value,string title){count++;if(!value)throw new Exception("Remediation: "+title);}
            Directory.CreateDirectory(folder);
            HallExperimentModel Model(string name)=>new HallExperimentModel{StorageFolder=Path.Combine(folder,name),RecoveryFolder=Path.Combine(folder,"recovery")};
            void Settle(HallExperimentModel m,THHCircuit c){for(int i=0;i<240;i++)m.Tick(c,.05f);}
            THHCircuit Circuit(bool reverse=false){var c=new THHCircuit();c.ConnectStandard();if(reverse){foreach(var cable in c.Cables.Where(x=>x.A==Pin.VPlus||x.A==Pin.VMinus).ToArray())c.Remove(cable.Id);c.Add(Pin.VPlus,Pin.CaseVMinus,out _);c.Add(Pin.VMinus,Pin.CaseVPlus,out _);}return c;}
            void Direction(HallExperimentModel m,THHCircuit c,int slot,float isValue,float imValue){c.TurnOff();c.SetSwitch(0,slot<2?1:-1);c.SetSwitch(2,slot==0||slot==3?1:-1);c.TurnOn(out _);c.SetCurrent(isValue,imValue);Settle(m,c);}
            void Group(HallExperimentModel m,THHCircuit c,float isValue,float imValue){for(int i=0;i<4;i++){Direction(m,c,i,isValue,imValue);Check(m.RecordCurrentReading(c),"record stable direction");}}

            var model=Model("polarities");Group(model,Circuit(),1,.6f);Group(model,Circuit(true),2,.6f);
            Check(model.Rows[0].vh<0&&model.Rows[1].vh>0,"raw polarities retained");
            Check(model.Rows[0].voltageDirection==1&&model.Rows[1].voltageDirection==-1,"voltage direction persisted");
            Check(ExperimentFit.TryFit(model.Rows,true,out float slope,out _,out _,out _)&&slope<0,"mixed polarity normalized before fitting");
            Check(model.Rows[1].is4==2&&model.Rows[1].im4==.6f,"all actual currents retained");
            Check(File.ReadAllText(model.OperationLogPath).Contains("方向采样"),"real operation events logged");
            Check(File.ReadAllLines(model.CsvFilePath)[0].Split(',').Length==24,"extended CSV schema");

            var tolerance=Model("tolerance");var tc=Circuit();Direction(tolerance,tc,0,.15f,.6f);Check(tolerance.RecordCurrentReading(tc),"minimum current first sample");
            Direction(tolerance,tc,1,.229f,.6f);Check(!tolerance.RecordCurrentReading(tc)&&tolerance.MeasurementCount==1,"38 percent error counterexample blocked");
            Direction(tolerance,tc,1,.15f,.6f);Check(tolerance.RecordCurrentReading(tc),"original setpoint accepted");

            Group(model,Circuit(),1,.4f);Group(model,Circuit(),2,.4f);
            Check(model.Rows.Select(r=>r.seriesId).Distinct().Count()==2,"fixed-current changes create separate series");
            foreach(var group in model.Rows.GroupBy(r=>r.seriesId))Check(ExperimentFit.TryFit(group.ToList(),true,out _,out _,out _,out _),"each series independently fits");
            Check(!ExperimentFit.TryFit(model.Rows,true,out _,out _,out _,out _),"cross-series fit blocked");
            string deleted=model.Rows[0].id;Check(model.DeleteRow(deleted)&&model.Rows.Count==3,"delete row");Check(model.UndoDelete()&&model.Rows[0].id==deleted&&model.Rows.Count==4,"undo restores ordering");

            string oldPath=model.CsvFilePath;
            using(var held=new FileStream(oldPath,FileMode.Open,FileAccess.Read,FileShare.None)) {
                Check(!model.SaveData()&&model.HasUnsavedRows&&File.Exists(model.RecoveryPath),"locked CSV retains independent recovery");
                var restored=Model("restored");Check(restored.Restore(model.RecoveryPath)&&restored.Rows.Count==4,"new session recovers failed save");
                Check(restored.Restore(model.RecoveryPath)&&restored.Rows.Count==4,"repeated recovery deduplicates IDs");
                Check(restored.SaveData()&&!restored.HasUnsavedRows,"recovered data export");
                Check(model.SaveAs(Path.Combine(folder,"migrated"))&&model.CsvFilePath!=oldPath,"save-as bypasses locked original CSV");
            }
            Check(File.ReadAllLines(oldPath).Length==5,"old CSV preserved after migration");
            var logger=new ExperimentLogger();Check(!logger.SaveRows(model.Rows)&&!logger.Log("TEST","event"),"uninitialized logger reports failure");
            string corrupt=Path.Combine(folder,"corrupt.json");File.WriteAllText(corrupt,"{}");Check(!Model("invalid").Restore(corrupt),"invalid recovery rejected");
            Check(Path.GetDirectoryName(ExperimentLogger.DefaultFolder)==ExperimentStorage.Root &&
                Path.GetDirectoryName(ExperimentStorage.Root)==Path.GetDirectoryName(Application.persistentDataPath),"default storage preserves per-user company directory after rename");
            Check(Path.GetDirectoryName(ExperimentRecovery.DefaultFolder)==ExperimentStorage.Root,"recovery and CSV share compatible storage root");

            app.ResetControls();app.StandardWiring();app.SelectDataSweep(true);app.TogglePhysicalModel();app.TogglePower();app.SetCurrents(2,.6f);Settle(app.ExperimentModel,app.Circuit);app.RecordCurrentReading();
            app.ToggleDataPanel();app.BrowseDataSweep(false);Check(app.ExperimentModel.MeasurementCount==1&&app.ExperimentModel.SweepIs,"table browsing preserves pending sampling target");
            app.ShowTab(1);app.TogglePower();app.ClearPendingReadings();app.SetSwitch(1,-1);app.TogglePower();app.SetCurrents(2,.6f);Settle(app.ExperimentModel,app.Circuit);app.SetCurrents(2,.6f);app.ShowTab(3);
            Check(UnityEngine.Object.FindObjectsOfType<Text>().Any(t=>t.text=="当前为 Vσ，请切换到 VH"),"conductivity mode gives actionable hint");
            app.ResetControls();app.ShowTab(2);app.MoveStage(1,1);
            Check(UnityEngine.Object.FindObjectsOfType<Text>().Any(t=>t.text.Contains("X 1.00 / Y 1.00")),"stage caption updates without power");
            var renderers=app.GetComponentsInChildren<Renderer>();
            Bounds upper=renderers.First(x=>x.name=="UpperPole_Approximate").bounds, lower=renderers.First(x=>x.name=="LowerPole_Approximate").bounds;
            Bounds upperFace=renderers.First(x=>x.name=="PoleFaceUpper").bounds,lowerFace=renderers.First(x=>x.name=="PoleFaceLower").bounds;
            Check(Mathf.Abs(upper.min.y-upperFace.max.y)<.00001f&&Mathf.Abs(lowerFace.min.y-lower.max.y)<.00001f,"pole faces attached to bodies");
            Check(Resources.FindObjectsOfTypeAll<WorkbenchUIRoot>().Count(x=>x.owner==app)==1,"one owned UI root");
            Check(app.GetComponentsInChildren<LeadPath>().All(l=>l.GetComponent<MeshFilter>().sharedMesh!=null),"cached cables keep valid geometry");
            app.ResetControls();app.ShowTab(0);
            return "PASS: "+count+" remediation assertions. Polarity, strict current, series, recovery, migration, deletion, logs, mode hints, geometry and UI ownership.\n";
        }
    }
}
