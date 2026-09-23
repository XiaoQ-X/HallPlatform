using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HallLab
{
    public static class WorkbenchChecks
    {
        public static string Run(THHWorkbench app)
        {
            int checks=0;
            void Check(bool ok,string label) { checks++;if(!ok)throw new Exception("Workbench check failed: "+label); }
            void Click(string name) {
                app.ShowTab(name=="标准接线" || name=="清空外接线" ? 0 : 1);
                var button=UnityEngine.Object.FindObjectsOfType<Button>().First(b=>b.name==name);
                button.onClick.Invoke();
            }
            int Wires()=>app.externalLeads.GetComponentsInChildren<LineRenderer>().Length;
            app.InitializePreview();app.ResetControls();
            var knobReadouts=app.GetComponentsInChildren<TextMesh>(true).Where(t=>t.name=="Knob setpoint").OrderBy(t=>t.transform.localPosition.x).ToArray();
            var knobRanges=app.GetComponentsInChildren<TextMesh>(true).Where(t=>t.name=="Knob range").ToArray();
            Check(knobReadouts.Length==2 && knobReadouts.All(t=>!t.text.Contains("\n")),"tester knobs have single-line setpoint readouts");
            Check(knobRanges.Length==2 && knobRanges.Any(t=>t.text=="0-10 mA") && knobRanges.Any(t=>t.text=="0-1 A"),"both knob ranges and units remain visible");
            Check(!app.GetComponentsInChildren<TextMesh>().Any(t=>t.name=="KnobCaption"),"old saved-scene knob captions are inactive");
            app.SetCurrents(10,1);
            Check(!app.Circuit.Powered && knobReadouts[0].text=="IS 10.00 mA" && knobReadouts[1].text=="IM 1.000 A","power-off labels show knob setpoints, not fictitious output");
            var tester=app.GetComponentsInChildren<Transform>().First(t=>t.name=="THH_TestInstrument");
            var caption=tester.GetComponentsInChildren<TextMesh>().First(t=>t.name=="DisplayCaption"&&t.transform.localPosition.x>0).GetComponent<MeshRenderer>().bounds;
            Check(caption.size.y>.0001f && caption.size.y<=.0061f,"meter caption remains readable without growing into display");
            var tickTop=tester.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="DialTick").Max(r=>r.bounds.max.y);
            foreach(var readout in knobReadouts) {
                var bounds=readout.GetComponent<MeshRenderer>().bounds;
                Check(bounds.size.x<=.0511f && bounds.size.y<=.0061f && bounds.size.y>.0001f,"setpoint glyph bounds fit readable faceplate row");
                Check(bounds.min.y>tickTop && bounds.max.y<caption.min.y,"setpoint row clears dial ticks and meter units");
                Check(Mathf.Abs(tester.InverseTransformPoint(bounds.center).z+.1595f)<.0001f,"setpoint labels are flush with faceplate");
            }
            app.ResetControls();
            for(int tab=0;tab<5;tab++){app.ShowTab(tab);Check(app.ActiveTab==tab,"inspector tab state");}
            app.ToggleInspector();Check(!app.InspectorVisible && app.orbit.compositionShift==0,"focus view hides inspector");
            Check(UnityEngine.Object.FindObjectsOfType<Image>(true).Any(i=>i.gameObject.name=="Icon panel-right-close"),"focus control uses panel-close icon, not fullscreen glyph");
            app.ToggleInspector();Check(app.InspectorVisible && app.orbit.compositionShift>0,"inspector restores camera composition");
            var renderers=app.GetComponentsInChildren<Renderer>();
            Bounds silicon=renderers.First(r=>r.name=="SiliconReference_0p5mm").bounds;
            Check(Mathf.Abs(silicon.size.y-.0005f)<.000001f && Mathf.Abs(silicon.size.z-.004f)<.000001f,"silicon thickness and width must not change for visual emphasis");
            var housing=renderers.First(r=>r.name=="Enclosure").GetComponent<MeshFilter>().sharedMesh;
            Check(housing.vertexCount>24,"housing is a rounded mesh, not a primitive cube");
            Bounds upper=renderers.First(r=>r.name=="UpperPole_Approximate").bounds;
            Bounds lower=renderers.First(r=>r.name=="LowerPole_Approximate").bounds;
            Check(silicon.min.y>lower.max.y && silicon.max.y<upper.min.y,"silicon between pole faces");
            Check(silicon.min.x>Mathf.Max(lower.min.x,upper.min.x) && silicon.max.x<Mathf.Min(lower.max.x,upper.max.x),"silicon inside pole X footprint at home");
            Check(silicon.min.z>Mathf.Max(lower.min.z,upper.min.z) && silicon.max.z<Mathf.Min(lower.max.z,upper.max.z),"silicon inside pole Z footprint at home");
            var terminals=app.GetComponentsInChildren<CircuitTerminal>().Where(t=>THHCircuit.IsExternal(t.pin)).ToArray();
            Check(terminals.Length==12 && terminals.Select(t=>t.pin).Distinct().Count()==12,"12 unique student sockets");
            Physics.SyncTransforms();
            foreach(var terminal in terminals) {
                Vector3 direction=terminal.transform.TransformDirection(terminal.outward).normalized;
                bool hit=Physics.Raycast(terminal.transform.position+direction*.06f,-direction,out RaycastHit contact,.065f);
                Check(hit && contact.collider.GetComponentInParent<CircuitTerminal>()==terminal,"socket collider reachable: "+terminal.pin);
            }
            Click("检查接线并接通输出");Check(!app.Circuit.Powered,"empty wiring cannot energize via UI");
            app.ChooseTerminal(Pin.SampleD);Check(!app.SelectedPin.HasValue,"factory terminal locked in UI");
            app.ChooseTerminal(Pin.IsPlus);app.CancelConnection();Check(!app.SelectedPin.HasValue,"cancel selection");
            for(int i=0;i<6;i++){app.ChooseTerminal((Pin)i);app.ChooseTerminal((Pin)(i+6));}
            Check(app.Circuit.Evaluate().Ready && Wires()==6,"two-click wiring and six visual cables");
            app.ChooseTerminal(Pin.IsPlus);app.ChooseTerminal(Pin.CaseIsPlus);Check(Wires()==6,"duplicate produces no phantom lead");
            app.RemoveCable(app.Circuit.Cables[0].Id);Check(Wires()==5 && !app.Circuit.Evaluate().Ready,"remove cable updates graph and visual");
            Click("标准接线");Check(Wires()==6 && app.Circuit.Evaluate().Ready,"standard helper uses graph");
            app.ShowTab(1);
            foreach(var slider in UnityEngine.Object.FindObjectsOfType<Slider>())if(slider.name=="IS setpoint")slider.value=3.25f;
            app.TogglePower();Check(!app.Circuit.Powered,"nonzero startup is blocked through workbench");
            app.SetCurrents(0,0);Click("检查接线并接通输出");Check(app.Circuit.Powered,"valid zero-start via button");
            app.SetCurrents(3.25f,.6f);Check(app.currentDisplay.text=="3.25" && app.voltageDisplay.text=="N/A","setpoint screen and no invented voltage");
            app.TogglePhysicalModel();
            app.ExperimentModel.Tick(app.Circuit,1f);
            Check(app.PhysicalModelEnabled && app.ExperimentModel.Ready && float.IsFinite(app.ExperimentModel.MagneticFieldTesla) && float.IsFinite(app.ExperimentModel.LiveReading.totalMillivolts),"ported huoer physical model produces finite reading");
            app.TogglePhysicalModel();
            Check(app.voltageDisplay.text=="N/A","physical model can be disabled without changing wiring mode");
            Click("电流屏：IM");Check(app.currentDisplay.text=="0.600","IM current display selection");
            var pose=app.switchBlades[0].localRotation;
            app.ToggleSwitch(0);app.ClearWiring();app.StandardWiring();app.RemoveCable(app.Circuit.Cables[0].Id);
            app.ChooseTerminal(Pin.IsPlus);app.ChooseTerminal(Pin.ImPlus);
            Check(app.Circuit.Cables.Count==6 && Wires()==6 && !app.SelectedPin.HasValue && Quaternion.Angle(pose,app.switchBlades[0].localRotation)<.1f,"all powered workbench edits blocked");
            app.TogglePower();Check(!app.Circuit.Powered && app.Circuit.IsSetpoint==0 && app.Circuit.ImSetpoint==0,"off zeroes graph");
            foreach(var slider in UnityEngine.Object.FindObjectsOfType<Slider>())if(slider.name.EndsWith("setpoint"))Check(slider.value==0,"off zeroes UI sliders");
            for(int i=0;i<3;i++) {
                app.SetSwitch(i,0);Check(!app.Circuit.Evaluate().Ready,"open switch blocks readiness");
                Check(Quaternion.Angle(Quaternion.identity,app.switchBlades[i].localRotation)>89,"open knife pose");
                app.SetSwitch(i,-1);var report=app.Circuit.Evaluate();
                Check(i==1 ? !report.HallMode && report.VoltageDirection==1 : (i==0?report.IsDirection:report.ImDirection)==-1,"electrode selection and source reversal");
                app.SetSwitch(i,1);
            }
            app.RemoveCable(app.Circuit.Cables[0].Id);app.RemoveCable(app.Circuit.Cables[0].Id);
            app.ChooseTerminal(Pin.IsPlus);app.ChooseTerminal(Pin.IsMinus);app.TogglePower();
            Check(app.Circuit.Evaluate().SourceShort && !app.Circuit.Powered && Wires()==5,"incorrect short retained but not energized");
            app.StandardWiring();app.RemoveCable(app.Circuit.Cables[4].Id);app.RemoveCable(app.Circuit.Cables[2].Id);
            app.ChooseTerminal(Pin.ImPlus);app.ChooseTerminal(Pin.VPlus);app.TogglePower();
            Check(app.Circuit.Evaluate().CrossCircuit && !app.Circuit.Powered,"cross-domain interlock");
            Vector3 home=app.sampleCarriage.localPosition;app.MoveStage(100,-100);
            Check(Vector3.Distance(app.sampleCarriage.localPosition,home+new Vector3(.018f,0,-.006f))<.00001f,"stage clamped");
            var leads=app.GetComponentsInChildren<LeadPath>();Check(leads.Length==THHCircuit.FactoryEdges.Count,"every fixed graph edge has a visual path");
            foreach(var edge in THHCircuit.FactoryEdges)Check(leads.Count(l=>l.from==edge.A && l.to==edge.B)==1,"fixed edge mapped once");
            foreach(var lead in leads) {
                var line=lead.GetComponent<LineRenderer>();
                Check(Vector3.Distance(line.GetPosition(0),lead.anchors[0].position)<.00001f && Vector3.Distance(line.GetPosition(line.positionCount-1),lead.anchors.Last().position)<.00001f,"wire endpoints track moving specimen");
            }
            app.ResetControls();
            Check(Wires()==0 && !app.SelectedPin.HasValue && app.Circuit.Cables.Count==0 && !app.Circuit.Powered,"reset graph selection and visuals");
            foreach(var slider in UnityEngine.Object.FindObjectsOfType<Slider>(true))Check(slider.value==0,"reset all sliders");
            for(int i=0;i<4;i++){app.FocusView(i);Check(float.IsFinite(app.view.transform.position.x),"finite camera preset");}
            app.FocusView(0);
            app.ShowTab(1);
            var numeric=UnityEngine.Object.FindObjectsOfType<InputField>().First(f=>f.name=="IS numeric");
            numeric.onEndEdit.Invoke("4.25");Check(Mathf.Abs(app.Circuit.IsSetpoint-4.25f)<.001f,"numeric input updates circuit");
            numeric.onEndEdit.Invoke("invalid");Check(Mathf.Abs(app.Circuit.IsSetpoint-4.25f)<.001f,"invalid input preserves current");
            app.ShowTab(3);
            Check(UnityEngine.Object.FindObjectsOfType<Text>().Any(t=>t.text=="+IS / +IM"),"sampling is a separate visible page");
            app.ToggleDataPanel();Check(app.DataPanelVisible,"data navigation opens full workspace");
            app.ShowTab(4);Check(!app.DataPanelVisible && app.ActiveTab==4,"navigation exits data into circuit checks");
            app.ResetControls();
            app.ShowTab(0);
            return "PASS: "+checks+" scene/UI assertions. Socket colliders, two-click cables, graph/visual matching, interlocks, switch poses, moving harness, slider reset.\n";
        }
    }
}
