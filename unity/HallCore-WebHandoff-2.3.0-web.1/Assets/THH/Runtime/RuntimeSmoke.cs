using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace HallLab
{
    // Only explicit -hall-smoke launches write checks/screenshots and exit automatically.
    public sealed class RuntimeSmoke : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnableIfRequested()
        {
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"-hall-smoke")>=0)
                new GameObject("Runtime smoke runner").AddComponent<RuntimeSmoke>();
        }
        private IEnumerator Start()
        {
            yield return null;yield return null;
            try {
                var app=FindObjectOfType<THHWorkbench>();
                string result=CircuitChecks.Run()+WorkbenchChecks.Run(app);
                var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-hall-output");
                if(index<0 || index+1>=args.Length)throw new Exception("Missing output folder");
                string folder=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(folder);
                result+=ExperimentChecks.Run(app,folder);
                result+=RemediationChecks.Run(app,Path.Combine(folder,"整改验证数据"));
                app.ToggleDataPanel();
                Capture(app.view,Path.Combine(folder,"17-实验数据与VH-IS曲线.png"));
                app.SelectDataSweep(false);
                Capture(app.view,Path.Combine(folder,"18-实验数据与VH-IM曲线.png"));
                app.ToggleDataPanel();
                app.StandardWiring();app.TogglePower();app.SetCurrents(3.25f,.6f);app.FocusView(0);
                Capture(app.view,Path.Combine(folder,"05-独立程序运行.png"));
                app.ShowTab(1);
                Capture(app.view,Path.Combine(folder,"08-控制面板.png"));
                app.ShowTab(0);
                app.TogglePower();app.ChooseTerminal(Pin.IsPlus);app.ChooseTerminal(Pin.IsMinus);app.TogglePower();
                Capture(app.view,Path.Combine(folder,"06-短接拦截.png"));
                app.StandardWiring();app.SetSwitch(1,-1);app.FocusView(2);
                Capture(app.view,Path.Combine(folder,"07-纵向电压接线.png"));
                app.ShowTab(2);app.FocusView(1);
                Capture(app.view,Path.Combine(folder,"09-测试仪细节.png"));
                app.ToggleInspector();app.FocusView(0);
                Capture(app.view,Path.Combine(folder,"10-专注装置.png"));
                app.ToggleInspector();
                app.ResetControls();app.ShowTab(0);app.FocusView(0);
                Capture(app.view,Path.Combine(folder,"UI-01-接线.png"));
                app.StandardWiring();app.ShowTab(4);
                Capture(app.view,Path.Combine(folder,"UI-02-检查.png"));
                app.ShowTab(1);app.TogglePhysicalModel();app.TogglePower();app.SetCurrents(2,.6f);
                for(int frame=0;frame<240;frame++)app.ExperimentModel.Tick(app.Circuit,.05f);
                app.SetCurrents(2,.6f);
                Capture(app.view,Path.Combine(folder,"UI-03-控制.png"));
                app.ShowTab(3);app.RecordCurrentReading();
                Capture(app.view,Path.Combine(folder,"UI-04-采样.png"));
                app.ToggleDataPanel();
                Capture(app.view,Path.Combine(folder,"UI-05-数据.png"));
                app.ShowTab(2);app.FocusView(1);
                Capture(app.view,Path.Combine(folder,"UI-06-器材.png"));
                File.WriteAllText(Path.Combine(folder,"独立程序验证.txt"),result+"Windows player checks passed. Software validation only; not calibrated experimental validation.\n");
                Debug.Log("THH_RUNTIME_SMOKE_PASS");Application.Quit(0);
            } catch(Exception error) { Debug.LogException(error);Application.Quit(1); }
        }
        public static void Capture(Camera camera,string path,int width=1440,int height=900)
        {
            var rt=new RenderTexture(width,height,24){antiAliasing=4};rt.Create();var previous=RenderTexture.active;
            camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=previous;rt.Release();Destroy(rt);Destroy(image);
        }
    }
}
