#if UNITY_EDITOR
using System;
using System.Reflection;
using HallEffectLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HallEffectLabSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/HallEffectMicroscopicLab.unity";

    [MenuItem("Tools/Hall Effect/Create Microscopic Lab Scene", false, 10)]
    public static void CreateMicroscopicLabScene()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Single);

        GameObject root = new GameObject("HallEffectMicroscopicLab");
        root.AddComponent<HallEffectLabController>();

        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogError("HALL_SCENE_BUILD_FAILED: Unity could not save " + ScenePath);
            return;
        }

        AssetDatabase.Refresh();
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true)
        };

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        Debug.Log("HALL_SCENE_BUILD_COMPLETE: " + ScenePath);
    }

    [MenuItem("Tools/Hall Effect/Run Physics Self-Test", false, 20)]
    public static void RunPhysicsSelfTest()
    {
        HallEffectSimulation hall = new HallEffectSimulation(84, 170917)
        {
            HallFieldEnabled = true
        };
        HallEffectSimulation control = new HallEffectSimulation(84, 170917)
        {
            HallFieldEnabled = false
        };

        hall.Reset(1f, 1f);
        control.Reset(1f, 1f);
        const float step = 1f / 180f;
        for (int i = 0; i < 8 * 180; i++)
        {
            hall.Step(step);
            control.Step(step);
        }

        float forceBalance = 0f;
        for (int i = 0; i < HallEffectSimulation.HighlightedParticleCount; i++)
        {
            forceBalance += hall.GetLateralForceBalance(i);
        }

        forceBalance /= HallEffectSimulation.HighlightedParticleCount;
        float hallFieldError = Mathf.Abs(hall.HallElectricField.y + 0.72f);
        float transverseCurrent = Mathf.Abs(hall.TransverseCurrentDensity);
        float controlCurrent = control.TransverseCurrentDensity;
        bool passed =
            hall.ExternalElectricField.x > 0f &&
            hall.DriftVelocity.x < 0f &&
            hall.GetLorentzForce(3).y < 0f &&
            hall.HallElectricField.y < 0f &&
            hall.HallVoltage > 0f &&
            hallFieldError < 0.12f &&
            transverseCurrent < 0.08f &&
            forceBalance > 0.90f &&
            control.HallElectricField == Vector2.zero &&
            controlCurrent > 0.20f &&
            hall.CurrentStage == 3;

        string report =
            "HALL_PHYSICS_SELF_TEST " + (passed ? "PASS" : "FAIL") +
            " | E_x=" + hall.ExternalElectricField.x.ToString("0.000") +
            " | v_dx=" + hall.DriftVelocity.x.ToString("0.000") +
            " | F_B,y=" + hall.GetLorentzForce(3).y.ToString("0.000") +
            " | E_H,y=" + hall.HallElectricField.y.ToString("0.000") +
            " | V_H=" + hall.HallVoltage.ToString("0.000") +
            " | J_y=" + hall.TransverseCurrentDensity.ToString("0.000") +
            " | F_balance=" + forceBalance.ToString("0.000") +
            " | control_J_y=" + controlCurrent.ToString("0.000") +
            " | stage=" + hall.CurrentStage;

        if (passed)
        {
            Debug.Log(report);
        }
        else
        {
            Debug.LogError(report);
        }
    }

    [MenuItem("Tools/Hall Effect/Run 3D Lab Self-Test", false, 30)]
    public static void Run3DLabSelfTest()
    {
        GameObject cameraObject = null;
        GameObject controllerObject = null;
        string failure = string.Empty;

        try
        {
            Type cameraType = FindType("HallEffectLab.HallEffectOrbitCamera");
            if (cameraType == null)
            {
                failure = "missing HallEffectOrbitCamera";
            }
            else
            {
                cameraObject = new GameObject("HallEffect3DSelfTestCamera");
                Component cameraComponent = cameraObject.AddComponent(cameraType);
                MethodInfo configure = cameraType.GetMethod(
                    "ConfigureView",
                    BindingFlags.Instance | BindingFlags.Public);
                MethodInfo apply = cameraType.GetMethod(
                    "ApplyNow",
                    BindingFlags.Instance | BindingFlags.Public);
                MethodInfo reset = cameraType.GetMethod(
                    "ResetView",
                    BindingFlags.Instance | BindingFlags.Public);

                if (configure == null || apply == null || reset == null)
                {
                    failure = "camera interaction API is incomplete";
                }
                else
                {
                    Vector3 focus = new Vector3(1f, 2f, 3f);
                    const float distance = 18f;
                    const float yaw = 25f;
                    const float pitch = 20f;
                    configure.Invoke(cameraComponent, new object[] { focus, distance, yaw, pitch });
                    apply.Invoke(cameraComponent, null);

                    Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
                    Vector3 expected = focus - rotation * Vector3.forward * distance;
                    if (Vector3.Distance(cameraObject.transform.position, expected) > 0.05f)
                    {
                        failure = "camera orbit transform is incorrect";
                    }
                    else
                    {
                        reset.Invoke(cameraComponent, null);
                        apply.Invoke(cameraComponent, null);
                        if (Vector3.Distance(cameraObject.transform.position, expected) < 0.5f)
                        {
                            failure = "camera reset did not change the view";
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(failure))
            {
                Type controllerType = FindType("HallEffectLab.HallEffectLabController");
                PropertyInfo panelVisible = controllerType == null
                    ? null
                    : controllerType.GetProperty(
                        "IsControlPanelVisible",
                        BindingFlags.Instance | BindingFlags.Public);
                MethodInfo setPanelVisible = controllerType == null
                    ? null
                    : controllerType.GetMethod(
                        "SetControlPanelVisible",
                        BindingFlags.Instance | BindingFlags.Public);
                PropertyInfo controlPanelHeight = controllerType == null
                    ? null
                    : controllerType.GetProperty(
                        "ControlPanelHeight",
                        BindingFlags.Instance | BindingFlags.Public);
                PropertyInfo forceDiagramHeight = controllerType == null
                    ? null
                    : controllerType.GetProperty(
                        "ForceDiagramHeight",
                        BindingFlags.Instance | BindingFlags.Public);
                if (controllerType == null ||
                    panelVisible == null ||
                    setPanelVisible == null ||
                    controlPanelHeight == null ||
                    forceDiagramHeight == null)
                {
                    failure = "panel layout or visibility API is incomplete";
                }
                else
                {
                    controllerObject = new GameObject("HallEffect3DSelfTestController");
                    Component controller = controllerObject.AddComponent(controllerType);
                    float measuredPanelHeight = (float)controlPanelHeight.GetValue(controller);
                    float measuredForceDiagramHeight = (float)forceDiagramHeight.GetValue(controller);
                    if (measuredPanelHeight < 240f)
                    {
                        failure = "control panel did not enlarge";
                    }
                    else if (measuredForceDiagramHeight < 80f)
                    {
                        failure = "force analysis diagram is too short";
                    }
                    else if (!(bool)panelVisible.GetValue(controller))
                    {
                        failure = "control panel should start visible";
                    }
                    else
                    {
                        setPanelVisible.Invoke(controller, new object[] { false });
                        if ((bool)panelVisible.GetValue(controller))
                        {
                            failure = "panel visibility did not toggle off";
                        }

                        setPanelVisible.Invoke(controller, new object[] { true });
                        if (!(bool)panelVisible.GetValue(controller))
                        {
                            failure = "panel visibility did not toggle on";
                        }
                    }
                }
            }
        }
        catch (Exception exception)
        {
            failure = exception.GetType().Name + ": " + exception.Message;
        }
        finally
        {
            if (cameraObject != null)
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            if (controllerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(controllerObject);
            }
        }

        if (string.IsNullOrEmpty(failure))
        {
            Debug.Log(
                "HALL_3D_LAB_SELF_TEST PASS | camera_orbit=ok | camera_reset=ok | " +
                "panel_toggle=ok | panel_layout=ok | force_diagram=ok");
        }
        else
        {
            Debug.LogError("HALL_3D_LAB_SELF_TEST FAIL | " + failure);
        }
    }

    private static Type FindType(string fullName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type = assemblies[i].GetType(fullName, false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
#endif
