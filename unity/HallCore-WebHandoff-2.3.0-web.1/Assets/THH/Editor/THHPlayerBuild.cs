using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HallLab.Editor
{
    public static class THHPlayerBuild
    {
        public static void RebuildAll()
        {
            ConfigureAssets();
            THHSceneBuilder.Build();
            BuildWindows();
        }

        public static void ConfigureAssets()
        {
            PlayerSettings.bundleVersion=HallRelease.Version;
            PlayerSettings.companyName="Hall Effect Lab";
            PlayerSettings.productName="霍尔效应教学仿真";
            PlayerSettings.resizableWindow=true;
            PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=800;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{UnityEngine.Rendering.GraphicsDeviceType.Direct3D11});
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            foreach(string path in Directory.GetFiles("Assets/THH/Resources/Icons","*.png")) {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.alphaIsTransparency=true;
                importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders=settings.FindProperty("m_AlwaysIncludedShaders");
            var shader=Shader.Find("UI/RoundedCorners/RoundedCorners");
            bool exists=false;for(int i=0;i<shaders.arraySize;i++)if(shaders.GetArrayElementAtIndex(i).objectReferenceValue==shader)exists=true;
            if(!exists){shaders.InsertArrayElementAtIndex(shaders.arraySize);shaders.GetArrayElementAtIndex(shaders.arraySize-1).objectReferenceValue=shader;settings.ApplyModifiedProperties();}
            AssetDatabase.SaveAssets();
        }

        [MenuItem("TH-H/构建 Windows 核对程序")]
        public static void BuildWindows()
        { BuildPlayer("Builds/THH-Reference.exe"); }

        public static void BuildValidation() { BuildPlayer("Builds/Validation/THH-Reference.exe"); }

        public static void BuildTeaching() { BuildPlayer("Builds/Teaching-"+HallRelease.Version+"/Hall-Teaching.exe"); }

        public static void BuildUiReview()
        { BuildWindows(); }

        private static void BuildPlayer(string outputPath)
        {
            const string scene = "Assets/THH/Scenes/THH_ReferenceWorkbench.unity";
            EditorSceneManager.OpenScene(scene);
            TeachingRelease.ApplySceneLabels();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            TeachingRelease.ValidateDependencies(scene);
            THHSceneBuilder.Validate();
            ConfigureAssets();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { scene },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CleanBuildCache
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Windows player build failed: " + report.summary.result);
            Debug.Log("THH_PLAYER_BUILD_PASS " + report.summary.totalSize + " bytes");
            File.Copy("THIRD_PARTY_NOTICES.md",Path.Combine(Path.GetDirectoryName(outputPath),"THIRD_PARTY_NOTICES.md"),true);
            File.Copy("TEACHING_RELEASE.md",Path.Combine(Path.GetDirectoryName(outputPath),"使用与来源说明.md"),true);
            string licenses=Path.Combine(Path.GetDirectoryName(outputPath),"Licenses");Directory.CreateDirectory(licenses);
            foreach(string component in new[]{"UIExtensions","UiRoundedCorners","Lucide"})
                File.Copy("Assets/ThirdParty/"+component+"/LICENSE.txt",Path.Combine(licenses,component+".txt"),true);
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(outputPath),"version.txt"),"霍尔效应教学仿真 "+PlayerSettings.bundleVersion+"\nUnity "+Application.unityVersion+"\n"+DateTime.UtcNow.ToString("O"));
        }
    }
}
