using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HallLab.Editor
{
    public static class TeachingRelease
    {
        public static void ApplySceneLabels()
        {
            foreach(var label in UnityEngine.Object.FindObjectsOfType<TextMesh>(true))
            {
                if(label.text=="TH-H    HALL EFFECT TESTER")label.text="HALL EFFECT  /  SIMULATION";
                if(label.name=="PlateHeading" && label.text=="TH-H")label.text="HALL LAB";
                // No calibration operation exists: do not present the decorative trim as usable ZERO.
                if(label.name=="ZeroLegend")label.text="SIM";
                EditorUtility.SetDirty(label);
            }
            foreach(var part in UnityEngine.Object.FindObjectsOfType<ReferencePart>(true))
            {
                if(part.kind==PartKind.Tester)
                {
                    part.title="教学测试仪（仿真）";
                    part.evidence="外观参考公开产品照片，非原厂或授权软件。\n机箱尺寸、面板位置及背部结构为近似；面板 SIM 饰件不提供实机调零。";
                }
                if(part.kind==PartKind.Sample)
                    part.evidence="电极关系参考公开说明书；未核实具体实物批次。\n托片、引脚和封装为示意；画面外形不代表实测尺寸。";
                EditorUtility.SetDirty(part);
            }
        }

        public static void ValidateDependencies(string scene)
        {
            // Fail closed on new media until its provenance is reviewed; research originals
            // stay outside Assets and must never become scene or Resources dependencies.
            foreach(string file in Directory.GetFiles("Assets","*",SearchOption.AllDirectories))
            {
                string path=file.Replace('\\','/');
                string ext=Path.GetExtension(path).ToLowerInvariant();
                bool media=ext==".png"||ext==".jpg"||ext==".jpeg"||ext==".tga"||ext==".psd"||ext==".pdf"||
                    ext==".fbx"||ext==".obj"||ext==".blend"||ext==".ttf"||ext==".otf"||ext==".mp4";
                bool reviewedFont=path=="Assets/THH/Resources/Fonts/NotoSansSC-Regular.otf" && File.Exists("Assets/ThirdParty/NotoSansSC/LICENSE.txt");
                if(media && !reviewedFont && !(ext==".png" && path.StartsWith("Assets/THH/Resources/Icons/",StringComparison.Ordinal)))
                    throw new InvalidOperationException("Unreviewed release media: "+path);
            }
            foreach(string path in AssetDatabase.GetDependencies(scene,true))
                if(path.Contains("参考资料")||path.Contains("原始资料")||path.Contains("实物照片"))
                    throw new InvalidOperationException("Research-only asset in release: "+path);
            if(Directory.Exists("Assets/StreamingAssets") && Directory.GetFiles("Assets/StreamingAssets","*",SearchOption.AllDirectories).Length>0)
                throw new InvalidOperationException("StreamingAssets requires an explicit provenance review");
            foreach(string component in new[]{"UIExtensions","UiRoundedCorners","Lucide"})
                if(!File.Exists("Assets/ThirdParty/"+component+"/LICENSE.txt"))throw new InvalidOperationException("Missing license: "+component);
            Debug.Log("TEACHING_RELEASE_PROVENANCE_CHECK_PASS");
        }
    }
}
