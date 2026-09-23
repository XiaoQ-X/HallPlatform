using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HallEffectLab.EditorTools
{
    /// 命令行：Unity -batchmode -quit -executeMethod HallEffectLab.EditorTools.HallWebGLBuild.Build
    public static class HallWebGLBuild
    {
        public static void Build()
        {
            string scene = "Assets/Scenes/HallEffectMicroscopicLab.unity";
            if (!File.Exists(scene))
            {
                scene = "Assets/Scenes/SampleScene.unity";
            }
            if (!File.Exists(scene))
            {
                throw new System.Exception("未找到可构建场景");
            }

            // 强制重新导入字体，避免使用 Library 中缓存的旧字形。
            string fontPath = "Assets/Resources/Fonts/NotoSansSC.otf";
            if (File.Exists(fontPath))
            {
                AssetDatabase.ImportAsset(fontPath, ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.Refresh();

            PlayerSettings.SetScriptingBackend(BuildTargetGroup.WebGL, ScriptingImplementation.IL2CPP);
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = false;

            string outPath = "BuildWebGL";
            Directory.CreateDirectory(outPath);

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { scene },
                locationPathName = outPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new System.Exception("WebGL 构建未成功：" + report.summary.result +
                    "，错误数 " + report.summary.totalErrors);
            }

            Debug.Log("WebGL 构建成功 -> " + outPath);
        }
    }
}
