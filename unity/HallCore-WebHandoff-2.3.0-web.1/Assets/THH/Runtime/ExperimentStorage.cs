using System.IO;
using UnityEngine;

namespace HallLab
{
    public static class ExperimentStorage
    {
        // Keep the established Windows data location after the public product rename.
        // This avoids abandoning existing recovery files or splitting user sessions.
        public static string Root
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return Path.Combine(Path.GetDirectoryName(Application.persistentDataPath), "TH-H 霍尔效应实验工作台");
#else
                return Application.persistentDataPath;
#endif
            }
        }
    }
}
