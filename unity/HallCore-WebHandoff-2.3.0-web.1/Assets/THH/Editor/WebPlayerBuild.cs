using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HallLab.Editor
{
    public static class WebPlayerBuild
    {
        [MenuItem("TH-H/Build WebGL handoff")]
        public static void Build()
        {
            const string scene="Assets/THH/Scenes/THH_ReferenceWorkbench.unity";
            const string destination="Builds/WebGL";
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL,BuildTarget.WebGL))
                throw new InvalidOperationException("Install matching Unity WebGL Build Support first.");
            EditorSceneManager.OpenScene(scene);
            THHPlayerBuild.ConfigureAssets();TeachingRelease.ValidateDependencies(scene);
            WebBridgeChecks.Run();
            PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback=true;
            PlayerSettings.WebGL.template="PROJECT:Hall";
            PlayerSettings.WebGL.initialMemorySize=128;
            PlayerSettings.WebGL.maximumMemorySize=2048;
            PlayerSettings.WebGL.exceptionSupport=WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL,ManagedStrippingLevel.Low);
            Directory.CreateDirectory(destination);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{scene},locationPathName=destination,
                target=BuildTarget.WebGL,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("WebGL build failed: "+report.summary.result);
            File.Copy("Tools/Web/handoff.html",destination+"/handoff.html",true);
            File.Copy("Tools/Web/hall-host.js",destination+"/hall-host.js",true);
            Directory.CreateDirectory(destination+"/Licenses");
            foreach(var file in Directory.GetFiles("Assets/ThirdParty","LICENSE.txt",SearchOption.AllDirectories))
                File.Copy(file,destination+"/Licenses/"+new DirectoryInfo(Path.GetDirectoryName(file)).Name+".txt",true);
            File.Copy("THIRD_PARTY_NOTICES.md",destination+"/THIRD_PARTY_NOTICES.md",true);
            Debug.Log("HALL_WEBGL_BUILD_PASS "+report.summary.totalSize);
        }
    }
}
