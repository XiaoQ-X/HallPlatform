using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HallLab.Editor
{
    public static class WebBridgeChecks
    {
        private static int count;
        private static void Check(bool value,string message){if(!value)throw new Exception(message);count++;}
        public static void Run()
        {
            foreach (float dt in new[] { .02f, .125f, .25f, 1f / 3f }) {
                var stability = new SamplingStability();
                float field = HallEffectMath.MagneticFieldTarget(.4f);
                for (int i=0;i<3;i++) stability.Update(true,3,.4f,3,.4f,1,1,field,dt);
                Check(!stability.Ready,"Single/few observations must not establish stability");
                for (int i=0;i<20;i++) stability.Update(true,3,.4f,3,.4f,1,1,field,dt);
                Check(stability.Ready,"Stable observations accepted at low frame rates");
                stability.Update(true,3,.4f,3,.4f,1,1,field,2f);
                Check(!stability.Ready,"Long interruption invalidates stability");
                stability.Update(true,3,.4f,3,.4f,1,-1,field,dt);
                Check(!stability.Ready,"Direction change invalidates stability");
            }
            var root=Path.GetFullPath("Logs/WebModelChecks");Directory.CreateDirectory(root);
            var model=new HallExperimentModel{StorageFolder=root,RecoveryFolder=Path.Combine(root,"recovery")};
            var circuit=new THHCircuit();
            Check(!circuit.TurnOn(out _),"Incomplete circuit must not power on");
            circuit.ConnectStandard();circuit.SetCurrent(3,.4f);
            Check(!circuit.TurnOn(out _),"Nonzero startup must be rejected");
            int[] signsIs={1,1,-1,-1},signsIm={1,-1,-1,1};
            for(int i=0;i<4;i++) {
                circuit.TurnOff();circuit.SetSwitch(0,signsIs[i]);circuit.SetSwitch(2,signsIm[i]);
                Check(circuit.TurnOn(out _),"Power on");circuit.SetCurrent(3,.4f);
                model.InvalidateSampling();model.Tick(circuit,.02f);
                Check(!model.RecordCurrentReading(circuit),"Unstable reading rejected");
                for(int n=0;n<1500&&!model.SamplingStable;n++)model.Tick(circuit,.02f);
                Check(model.SamplingStable,"Stable reading reached");
                Check(model.RecordCurrentReading(circuit),"Direction accepted");
                if(i<3)Check(model.MeasurementCount==i+1,"Distinct direction slots");
            }
            Check(model.Rows.Count==1,"One complete group");
            var row=model.Rows.Single();
            Check(Math.Abs(row.vh-(row.v1-row.v2+row.v3-row.v4)/4)<1e-6,"Symmetry formula");
            Check(row.b1>0&&row.b2<0&&row.b3<0&&row.b4>0,"Field signs");
            var eventData=new WebEvent {type="OnExperimentComplete",rows=new[]{row},history=new[]{"{\"type\":\"OnMeasureData\"}"}};
            var roundtrip=JsonUtility.FromJson<WebEvent>(JsonUtility.ToJson(eventData));
            Check(roundtrip.rows[0].id==row.id && roundtrip.history.Length==1,"Protocol serialization");
            var config=JsonUtility.FromJson<WebInit>("{\"schemaVersion\":1,\"caseId\":\"test\",\"material\":\"n-silicon\",\"thickness_mm\":0.5,\"maxIs_mA\":10,\"maxIm_A\":1}");
            Check(config.maxIs_mA==10&&config.thickness_mm==.5f,"Init serialization");
            var missing=new WebParameter();JsonUtility.FromJsonOverwrite("{\"name\":\"IS_mA\"}",missing);
            Check(float.IsNaN(missing.value),"Missing parameter must not silently become zero");
            File.WriteAllText(Path.Combine(root,"result.txt"),"PASS: "+count+" model and serialization assertions. Browser bridge tests are separate.");
            Debug.Log("HALL_WEB_MODEL_CHECK_PASS "+count);
        }
    }
}
