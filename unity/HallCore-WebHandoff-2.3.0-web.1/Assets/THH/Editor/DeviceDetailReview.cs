using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HallLab.Editor
{
    public static partial class THHSceneBuilder
    {
        public static void ReviewDeviceDetails()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/THH/Scenes/THH_ReferenceWorkbench.unity");
            app=UnityEngine.Object.FindObjectOfType<THHWorkbench>();
            app.InitializePreview();app.StandardWiring();
            ValidateDeviceDetails();
            CaptureDeviceDetails();
        }
        [MenuItem("TH-H/导出设备细节近景")]
        public static void CaptureDeviceDetails()
        {
            app=UnityEngine.Object.FindObjectOfType<THHWorkbench>();app.InitializePreview();app.StandardWiring();
            var canvases=UnityEngine.Object.FindObjectsOfType<Canvas>();
            var states=canvases.Select(c=>c.enabled).ToArray();
            var orbit=app.orbit;float shift=orbit.compositionShift;
            foreach(var canvas in canvases)canvas.enabled=false;
            orbit.compositionShift=0;
            try{
                RenderDetail("11-设备全景",new Vector3(.005f,.24f,.025f),18,25,1.66f);
                RenderDetail("12-测试仪五金与接口",new Vector3(-.355f,.112f,-.04f),-20,15,.70f);
                RenderDetail("13-电磁铁与线圈骨架",new Vector3(.39f,.207f,.095f),30,18,.43f);
                RenderDetail("14-二维样品架",new Vector3(.176f,.118f,.04f),-27,32,.29f);
                RenderDetail("15-双刀双掷开关",new Vector3(.141f,.101f,-.13f),18,40,.24f);
                // Look almost square-on through the gap so the specimen and the
                // two pole faces are both readable; the oblique angle hid the
                // 0.5 mm sample behind the near pole in the previous export.
                RenderDetail("16-样品与磁隙近景",new Vector3(.39f,.1648f,.045f),0,2,.105f);
            }finally{
                for(int i=0;i<canvases.Length;i++)canvases[i].enabled=states[i];
                orbit.compositionShift=shift;app.FocusView(0);
            }
        }
        private static void RenderDetail(string name,Vector3 target,float yaw,float pitch,float distance)
        {
            app.orbit.target=target;app.orbit.yaw=yaw;app.orbit.pitch=pitch;app.orbit.distance=distance;app.orbit.Apply();
            var rt=new RenderTexture(1800,1200,24){antiAliasing=4};rt.Create();
            var old=RenderTexture.active;float aspect=app.view.aspect;app.view.aspect=1.5f;app.view.targetTexture=rt;
            var image=new Texture2D(1800,1200,TextureFormat.RGB24,false);
            try{
                app.view.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1800,1200),0,0);image.Apply();
                File.WriteAllBytes("预览/"+name+".png",image.EncodeToPNG());
            }finally{
                RenderTexture.active=old;app.view.targetTexture=null;app.view.aspect=aspect;
                rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static void ValidateDeviceDetails()
        {
            int count=0;
            void Check(bool ok,string message){count++;if(!ok)throw new Exception("Device detail check: "+message);}
            var renderers=app.GetComponentsInChildren<Renderer>();
            foreach(var flange in app.GetComponentsInChildren<Transform>().Where(t=>t.name=="OpenBobbinFlange")){
                // Both visible core ends must have an opening rather than a solid cream slab.
                Check(flange.childCount==4,"bobbin frame has four sides");
                foreach(var r in flange.GetComponentsInChildren<Renderer>())
                    Check(!r.bounds.Contains(flange.position),"bobbin opening unobstructed by flange");
            }
            var mechanics=app.GetComponentInChildren<StageMechanics>();
            var upper=renderers.First(r=>r.name=="PoleFaceUpper").bounds;
            var lower=renderers.First(r=>r.name=="PoleFaceLower").bounds;
            foreach(float x in new[]{-1f,0,1f})foreach(float y in new[]{-1f,0,1f}){
                app.MoveStage(x,y);
                Check(Mathf.Abs(mechanics.xSaddle.localPosition.x-app.sampleCarriage.localPosition.x)<.00001f,"X saddle follows carriage");
                var carrier=renderers.First(r=>r.name=="SampleCarrier_Approximate").bounds;
                var silicon=renderers.First(r=>r.name=="SiliconReference_0p5mm").bounds;
                Check(carrier.min.y>lower.max.y && silicon.max.y<upper.min.y,"sample assembly fits magnetic gap throughout travel");
                foreach(var lead in app.GetComponentsInChildren<LeadPath>()){
                    var mesh=lead.GetComponent<MeshFilter>().sharedMesh;
                    Check(mesh!=null && mesh.vertexCount>30 && mesh.bounds.size.sqrMagnitude>0,"round wire mesh rebuilt with moving anchors");
                    foreach(var v in mesh.vertices)if(!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z))throw new Exception("Non-finite cable vertex");
                    if((int)lead.to>=24 && (int)lead.to<=28){
                        var clamp=renderers.First(r=>r.name=="ProbeHolder").bounds;clamp.Expand(.0016f);
                        var route=lead.GetComponent<LineRenderer>();bool clear=true;
                        for(int n=0;n<route.positionCount;n++)if(clamp.Contains(route.GetPosition(n))){clear=false;break;}
                        Check(clear,"sample harness clears probe clamp at "+x+","+y+" / "+lead.to);
                    }
                }
            }
            app.MoveStage(0,0);
            File.WriteAllText("预览/设备细节验证.txt","PASS: "+count+" device geometry assertions. Open bobbin, nine stage poses, specimen clearance, finite round cable geometry.\nVisual approximations; dimensions and screw pitch are not measured specifications.\n");
            Debug.Log("THH_DEVICE_DETAILS_PASS");
        }
    }
}
