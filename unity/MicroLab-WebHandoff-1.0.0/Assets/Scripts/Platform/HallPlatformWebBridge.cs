using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HallEffectLab
{
    /// <summary>
    /// 教学平台通信桥。GameObject 名称必须为 “WebBridge”，平台经
    /// UnityLoader 的 SendMessage('WebBridge', method, json) 下发命令；
    /// 本脚本通过 jslib(HallPlatformEmit) 把事件以 hall-event 回传平台。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HallPlatformWebBridge : MonoBehaviour
    {
        [Serializable] private sealed class OutEvent
        {
            public int schemaVersion = 1;
            public int step = 1;
            public string type;
            public string eventId;
            public string sessionId;
            public string caseId;
            public string timestamp;
            public long elapsedMs;
            public string code = "";
            public string detail = "";
            public bool success;
            public bool isDemo;
            public EventState state;
        }

        [Serializable] private sealed class EventState
        {
            public bool powered = true;
            public bool stable = true;
            public bool finished;
            public double magneticField;
            public double externalElectricField;
            public int commandCount;
            public Report report;
        }

        [Serializable] private sealed class Report
        {
            public string module;
            public string completedAt;
            public int commandCount;
        }

        [Serializable] private sealed class InitCmd
        {
            public int schemaVersion;
            public string caseId;
            public string material;
            public double thickness_mm;
            public double maxIs_mA;
            public double maxIm_A;
        }

        [Serializable] private sealed class ParamCmd
        {
            public string name;
            public double value;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void HallPlatformEmit(string json);
#else
        private static void HallPlatformEmit(string json) { Debug.Log("[HallPlatform] " + json); }
#endif

        private string _sessionId;
        private string _caseId = "case-microscopic";
        private int _seq;
        private int _commandCount;
        private DateTime _startUtc;
        private bool _finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<HallPlatformWebBridge>() != null)
            {
                return;
            }

            GameObject go = new GameObject("WebBridge");
            go.AddComponent<HallPlatformWebBridge>();
            DontDestroyOnLoad(go);
        }

        private void Start()
        {
            _sessionId = Guid.NewGuid().ToString("N");
            _startUtc = DateTime.UtcNow;
            Emit("OnReady", "ready", "", true);
        }

        // 平台 → Unity：所有方法使用 string 参数以匹配 SendMessage。
        public void InitExperiment(string json)
        {
            InitCmd cmd = SafeJson<InitCmd>(json);
            if (cmd != null && !string.IsNullOrEmpty(cmd.caseId))
            {
                _caseId = cmd.caseId;
            }
            _commandCount++;
            Emit("OnCommandResult", "InitExperiment", "", true);
        }

        public void SetParameter(string json)
        {
            ParamCmd cmd = SafeJson<ParamCmd>(json);
            bool ok = true;
            if (cmd != null && !string.IsNullOrEmpty(cmd.name))
            {
                HallEffectLabController controller = FindObjectOfType<HallEffectLabController>();
                if (controller != null)
                {
                    float e = controller.ExternalElectricField;
                    float b = controller.MagneticField;
                    string n = cmd.name;
                    if (n == "externalField" || n == "externalElectricField")
                    {
                        e = (float)cmd.value;
                    }
                    else if (n == "magneticField" || n == "B")
                    {
                        if (cmd.value < 0d || cmd.value > 2d)
                        {
                            ok = false;
                        }
                        else
                        {
                            b = (float)cmd.value;
                        }
                    }
                    else
                    {
                        ok = false;
                    }

                    if (ok)
                    {
                        controller.SetExperimentParameters(e, b);
                    }
                }
                else
                {
                    ok = false;
                }
                _commandCount++;
            }
            else
            {
                ok = false;
            }

            Emit("OnCommandResult", "SetParameter", json, ok);
        }

        public void RequestSnapshot(string json)
        {
            Emit("OnSnapshot", "snapshot", "", true);
        }

        public void ResetExperiment(string json)
        {
            HallEffectLabController controller = FindObjectOfType<HallEffectLabController>();
            if (controller != null)
            {
                controller.SetExperimentParameters(1f, 1f);
            }
            _finished = false;
            Emit("OnStepState", "reset", "", true);
        }

        public void FinishFromWeb(string json)
        {
            if (_finished)
            {
                return;
            }
            _finished = true;
            Report report = new Report
            {
                module = "microscopic",
                completedAt = DateTime.UtcNow.ToString("o"),
                commandCount = _commandCount
            };
            Emit("OnExperimentComplete", "finish", "", true, report);
        }

        private static T SafeJson<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }
            try { return JsonUtility.FromJson<T>(json); }
            catch (Exception) { return null; }
        }

        private EventState BuildState(Report report)
        {
            HallEffectLabController controller = FindObjectOfType<HallEffectLabController>();
            EventState state = new EventState
            {
                finished = _finished,
                commandCount = _commandCount,
                report = report
            };
            if (controller != null)
            {
                state.magneticField = controller.MagneticField;
                state.externalElectricField = controller.ExternalElectricField;
            }
            return state;
        }

        private void Emit(string type, string code, string detail, bool success, Report report = null)
        {
            OutEvent e = new OutEvent
            {
                type = type,
                eventId = _sessionId + ":" + (++_seq),
                sessionId = _sessionId,
                caseId = _caseId,
                timestamp = DateTime.UtcNow.ToString("o"),
                elapsedMs = (long)(DateTime.UtcNow - _startUtc).TotalMilliseconds,
                code = code ?? "",
                detail = detail ?? "",
                success = success,
                isDemo = false,
                state = BuildState(report)
            };
            HallPlatformEmit(JsonUtility.ToJson(e));
        }
    }
}
