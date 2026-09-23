using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HallLab.Editor
{
    public static class RemediationBuild
    {
        public static void Run()
        {
            Directory.CreateDirectory("预览/改版后-20260918");
            THHPlayerBuild.ConfigureAssets();THHSceneBuilder.Build();
            var app=UnityEngine.Object.FindObjectOfType<THHWorkbench>();
            string result=ExperimentChecks.Run(app,"预览/改版后-20260918")+RemediationChecks.Run(app,"预览/改版后-20260918/整改验证数据");
            string temporary="Assets/THH/Scenes/Validation_"+Guid.NewGuid().ToString("N")+".unity";
            try {
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),temporary,true);
                EditorSceneManager.OpenScene(temporary);
                if(UnityEngine.Object.FindObjectsOfType<Canvas>().Length!=0)throw new Exception("Preview UI was serialized into scene");
                app=UnityEngine.Object.FindObjectOfType<THHWorkbench>();app.InitializePreview();app.InitializePreview();
                if(System.Linq.Enumerable.Count(Resources.FindObjectsOfTypeAll<WorkbenchUIRoot>(),x=>x.owner==app)!=1)throw new Exception("Repeated UI initialization");
                result+="PASS: preview-save-reload and repeated initialization.\n";
            }finally {EditorSceneManager.OpenScene("Assets/THH/Scenes/THH_ReferenceWorkbench.unity");AssetDatabase.DeleteAsset(temporary);}
            File.WriteAllText("预览/改版后-20260918/编辑器验证.txt",result);
            THHPlayerBuild.BuildWindows();Debug.Log("THH_REMEDIATION_BUILD_PASS");
        }
    }
}
