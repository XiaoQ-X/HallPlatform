using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HallEffectLab
{
    /// <summary>
    /// Builds and drives the complete microscopic Hall-effect experiment.
    /// The electromagnetic evolution comes from HallEffectSimulation; this class
    /// only maps those values to a lit, three-dimensional teaching model.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class HallEffectLabController : MonoBehaviour
    {
        private const float MaximumModelTime = 8f;
        private const int ParticleCount = 84;
        private const int TrailPointLimit = 120;
        private const float CrystalHalfWidth = 6.8f;
        private const float CrystalHalfHeight = 2.45f;
        private const float CrystalHalfDepth = 1.2f;
        private static float HeaderHeight => Screen.width < 700 ? 142f : 94f;
        private const float MinimumControlPanelHeight = 430f;
        private const float MaximumControlPanelHeight = 570f;
        private const float ControlPanelHeightFraction = 0.30f;
        private const float ForceDiagramAreaOffset = 214f;

        [Header("Initial normalized experiment values")]
        [SerializeField] private float _externalElectricField = 1f;
        [SerializeField] private float _magneticField = 1f;
        [SerializeField] private float _simulationSpeed = 1f;

        [Header("Visible teaching aids")]
        [SerializeField] private bool _showVectors = true;
        [SerializeField] private bool _showTrajectories = true;
        [SerializeField] private bool _showIonLattice = true;

        private readonly List<Material> _materials = new List<Material>();
        private Camera _camera;
        private HallEffectOrbitCamera _orbitCamera;
        private Mesh _cubeMesh;
        private Mesh _sphereMesh;
        private Mesh _cylinderMesh;
        private Mesh _coneMesh;
        private Texture2D _whiteTexture;
        private Font _font;
        private HallPanel _hallPanel;
        private HallPanel _controlPanel;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _vectorLabelStyle;
        private GUIStyle _sectionTitleStyle;
        private GUIStyle _forceLabelStyle;
        private GUIStyle _forceSymbolStyle;
        private GUIStyle _worldSmallStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _toggleStyle;
        private GUIStyle _legendTitleStyle;
        private GUIStyle _legendItemStyle;
        private GUIStyle _legendSymbolStyle;
        private bool _isRunning = true;
        private bool _isControlPanelVisible = true;
        private float _modelTime;
        private bool _showLegend;
        private int _viewportWidth, _viewportHeight;
        private bool _viewportPanelVisible;

        public float MagneticField => _magneticField;
        public float ExternalElectricField => _externalElectricField;
        public bool IsControlPanelVisible => _isControlPanelVisible;
        public float ControlPanelHeight => GetControlPanelHeight();
        public float ForceDiagramHeight =>
            Mathf.Max(0f, GetControlPanelHeight() - ForceDiagramAreaOffset - 16f);
        public bool HasForceDirectionPlane =>
            _hallPanel != null && _hallPanel.HasForceDirectionPlane;
        private bool IsModelComplete => _modelTime >= MaximumModelTime - 0.0001f;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            CreateCamera();
            CreateSharedResources();
            CreateFont();

            Palette palette = new Palette(this);
            CreateWorldEnvironment(palette);
            BuildExperiment(palette);
            ResetExperiment();
        }

        private void Update()
        {
            if (_orbitCamera != null)
            {
                _orbitCamera.PointerOverGui = IsPointerOverGui();
            }

            if (_hallPanel == null || _controlPanel == null)
            {
                return;
            }

            UpdateViewport();

            float realDeltaTime = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            if (_isRunning)
            {
                float step = realDeltaTime * _simulationSpeed;
                _hallPanel.Step(step);
                _controlPanel.Step(step);
                _modelTime = Mathf.Min(
                    MaximumModelTime,
                    _hallPanel.Simulation.ElapsedModelTime);
                if (_modelTime >= MaximumModelTime)
                {
                    _isRunning = false;
                }
            }

            _hallPanel.UpdateVisuals(realDeltaTime);
            _controlPanel.UpdateVisuals(realDeltaTime);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _materials.Count; i++)
            {
                if (_materials[i] != null)
                {
                    Destroy(_materials[i]);
                }
            }

            DestroyMesh(_cubeMesh);
            DestroyMesh(_sphereMesh);
            DestroyMesh(_cylinderMesh);
            DestroyMesh(_coneMesh);

            if (_whiteTexture != null)
            {
                Destroy(_whiteTexture);
            }

            if (_font != null)
            {
                Destroy(_font);
            }
        }

        public void SetControlPanelVisible(bool visible)
        {
            _isControlPanelVisible = visible;
            if (_orbitCamera != null)
            {
                _orbitCamera.PointerOverGui = IsPointerOverGui();
            }
        }

        private void UpdateViewport()
        {
            if (_viewportWidth == Screen.width && _viewportHeight == Screen.height && _viewportPanelVisible == _isControlPanelVisible) return;
            _viewportWidth = Screen.width;
            _viewportHeight = Screen.height;
            _viewportPanelVisible = _isControlPanelVisible;
            bool portrait = Screen.width < 700;
            float bottom = _isControlPanelVisible ? GetControlPanelHeight() + 14f : 8f;
            float viewHeight = Mathf.Max(100f, Screen.height - bottom - HeaderHeight - 8f);
            _camera.rect = new Rect(0f, bottom / Screen.height, 1f, viewHeight / Screen.height);
            _hallPanel.Root.localPosition = portrait ? new Vector3(0f, 5.6f, 0f) : new Vector3(-7.75f, 0.08f, 0f);
            _controlPanel.Root.localPosition = portrait ? new Vector3(0f, -5.6f, 0f) : new Vector3(7.75f, 0.08f, 0f);
            _hallPanel.Root.localScale = _controlPanel.Root.localScale = Vector3.one * (portrait ? 0.48f : 0.72f);
            float distance = portrait ? 21.5f : Mathf.Clamp(16f * viewHeight / Screen.width * 2.2f, 16f, 38f);
            _orbitCamera.ConfigureView(new Vector3(0f, 0.22f, 0f), distance, -8f, 10f);
            _labelStyle = null;
        }

        private void CreateCamera()
        {
            GameObject cameraObject = new GameObject("HallEffectFreeCamera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.tag = "MainCamera";

            _camera = cameraObject.AddComponent<Camera>();
            _camera.orthographic = false;
            _camera.fieldOfView = 44f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 220f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.035f, 0.042f, 0.048f, 1f);
            _camera.allowHDR = true;
            _camera.allowMSAA = true;

            _orbitCamera = cameraObject.AddComponent<HallEffectOrbitCamera>();
            _orbitCamera.ConfigureView(
                new Vector3(0f, 0.22f, 0f),
                25f,
                -16f,
                22f);
        }

        private void CreateSharedResources()
        {
            _cubeMesh = CreateCubeMesh();
            _sphereMesh = CreateSphereMesh(18, 12);
            _cylinderMesh = CreateCylinderMesh(20);
            _coneMesh = CreateConeMesh(24);
            _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTexture.name = "HallEffectWhiteTexture";
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();
        }

        private void CreateFont()
        {
            string[] candidates =
            {
                "Microsoft YaHei",
                "微软雅黑",
                "SimHei",
                "Noto Sans CJK SC",
                "Arial"
            };
#if UNITY_WEBGL && !UNITY_EDITOR
            Font embeddedFont = Resources.Load<Font>("Fonts/NotoSansSC");
            if (embeddedFont != null)
            {
                _font = embeddedFont;
            }
            else
#endif
            {
                _font = Font.CreateDynamicFontFromOSFont(candidates, 28);
            }
        }

        private void CreateWorldEnvironment(Palette palette)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.22f, 0.25f, 0.29f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.12f, 0.14f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.035f, 0.04f, 0.045f, 1f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = _camera.backgroundColor;
            RenderSettings.fogStartDistance = 36f;
            RenderSettings.fogEndDistance = 80f;

            CreateBox(
                transform,
                "LaboratoryBench",
                new Vector3(50f, 0.28f, 26f),
                new Vector3(0f, -2.58f, 0.8f),
                palette.Floor,
                true);

            GameObject keyLightObject = new GameObject("KeyLight");
            keyLightObject.transform.SetParent(transform, false);
            keyLightObject.transform.rotation = Quaternion.Euler(42f, -34f, 0f);
            Light keyLight = keyLightObject.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.color = new Color(1f, 0.95f, 0.88f, 1f);
            keyLight.intensity = 1.15f;
            keyLight.shadows = LightShadows.Soft;
            keyLight.shadowStrength = 0.62f;

            GameObject fillLightObject = new GameObject("CoolFillLight");
            fillLightObject.transform.SetParent(transform, false);
            fillLightObject.transform.position = new Vector3(-9f, 6f, -10f);
            Light fillLight = fillLightObject.AddComponent<Light>();
            fillLight.type = LightType.Point;
            fillLight.color = new Color(0.62f, 0.78f, 1f, 1f);
            fillLight.intensity = 1.25f;
            fillLight.range = 32f;

            GameObject rimLightObject = new GameObject("WarmRimLight");
            rimLightObject.transform.SetParent(transform, false);
            rimLightObject.transform.position = new Vector3(10f, 4.5f, 9f);
            Light rimLight = rimLightObject.AddComponent<Light>();
            rimLight.type = LightType.Point;
            rimLight.color = new Color(1f, 0.62f, 0.38f, 1f);
            rimLight.intensity = 0.75f;
            rimLight.range = 28f;
        }

        private void BuildExperiment(Palette palette)
        {
            GameObject generatedRoot = new GameObject("GeneratedHallEffect3DModel");
            generatedRoot.transform.SetParent(transform, false);

            _hallPanel = new HallPanel(
                this,
                generatedRoot.transform,
                palette,
                "有霍尔电场：真实模型",
                new Vector3(-7.75f, 0.08f, 0f),
                true,
                170917);

            _controlPanel = new HallPanel(
                this,
                generatedRoot.transform,
                palette,
                "无霍尔电场：对照实验",
                new Vector3(7.75f, 0.08f, 0f),
                false,
                170917);
        }

        private void ResetExperiment()
        {
            _modelTime = 0f;
            _hallPanel.Reset(_externalElectricField, _magneticField);
            _controlPanel.Reset(_externalElectricField, _magneticField);
            ApplyVisibility();
        }

        private void SetModelTime(float targetTime)
        {
            _hallPanel.SetTime(targetTime);
            _controlPanel.SetTime(targetTime);
            _modelTime = Mathf.Clamp(targetTime, 0f, MaximumModelTime);
        }

        private void SetExperimentParameters()
        {
            float preservedTime = _modelTime;
            _hallPanel.Reset(_externalElectricField, _magneticField);
            _controlPanel.Reset(_externalElectricField, _magneticField);
            SetModelTime(preservedTime);
        }
        /// 平台（WebBridge）下发归一化电场 / 磁场后调用。
        public void SetExperimentParameters(float externalElectricField, float magneticField)
        {
            _externalElectricField = Mathf.Clamp(externalElectricField, 0f, 3f);
            _magneticField = Mathf.Clamp(magneticField, 0f, 2f);
            _isRunning = true;
            SetExperimentParameters();
        }

        private void ApplyVisibility()
        {
            _hallPanel.SetVectorVisibility(_showVectors);
            _controlPanel.SetVectorVisibility(_showVectors);
            _hallPanel.SetTrajectoryVisibility(_showTrajectories);
            _controlPanel.SetTrajectoryVisibility(_showTrajectories);
            _hallPanel.SetIonLatticeVisibility(_showIonLattice);
            _controlPanel.SetIonLatticeVisibility(_showIonLattice);
        }

        private void OnGUI()
        {
            EnsureGuiStyles();
            if (_hallPanel == null)
            {
                return;
            }

            if (_orbitCamera != null)
            {
                _orbitCamera.PointerOverGui = IsPointerOverGui();
            }

            DrawWorldLabels();
            DrawHeader();
            DrawControlPanelToggle();
            if (_isControlPanelVisible)
            {
                DrawControlPanel();
            }

            DrawParticleLegend();
        }

        private void DrawHeader()
        {
            float width = Screen.width;
            bool mobile = width < 700;
            DrawSolidRect(
                new Rect(0f, 0f, width, HeaderHeight),
                new Color(0.025f, 0.031f, 0.036f, 0.94f));
            DrawSolidRect(
                new Rect(0f, HeaderHeight - 2f, width, 2f),
                new Color(0.16f, 0.65f, 0.78f, 0.9f));

            GUI.Label(
                new Rect(mobile ? 16f : 24f, 7f, mobile ? width - 32f : width - 450f, 40f),
                "霍尔效应微观物理模型",
                _titleStyle);
            GUI.Label(
                new Rect(mobile ? 16f : 24f, mobile ? 42f : 51f, width - 32f, mobile ? 48f : 36f),
                mobile ? "归一化教学模型 · q=-e\nE外:+X，B:+Z（初始视角入屏）" : "m dv/dt = q(E外 + E霍 + v×B) - m v/τ     q=-e，E外:+X，B:+Z（初始视角入屏）；归一化单位，稳态 |E霍|=|v漂|B",
                _subtitleStyle);
        }

        private void DrawControlPanelToggle()
        {
            Rect buttonRect = GetControlPanelToggleRect();
            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = _isControlPanelVisible
                ? new Color(0.20f, 0.58f, 0.68f, 1f)
                : new Color(0.18f, 0.74f, 0.84f, 1f);
            if (GUI.Button(
                    buttonRect,
                    _isControlPanelVisible ? "隐藏控制面板" : "显示控制面板",
                    _buttonStyle))
            {
                SetControlPanelVisible(!_isControlPanelVisible);
            }

            GUI.backgroundColor = previousBackground;
        }

        private void DrawControlPanel()
        {
            Rect area = GetControlPanelRect();
            DrawSolidRect(area, new Color(0.030f, 0.041f, 0.049f, 0.97f));
            DrawSolidRect(
                new Rect(area.x, area.y, area.width, 2f),
                new Color(0.16f, 0.72f, 0.84f, 0.96f));

            bool narrow = area.width < 1200f;
            float leftWidth = Mathf.Clamp(area.width * 0.20f, 280f, 350f);
            float middleWidth = Mathf.Clamp(area.width * 0.48f, 500f, 1040f);
            float rightX = area.x + leftWidth + middleWidth + 18f;
            float rightWidth = Mathf.Max(290f, area.x + area.width - rightX - 16f);

            float y = area.y + 18f;
            float buttonWidth = 104f;

            if (Screen.width < 700f)
            {
                DrawMobileControlPanel(area);
                return;
            }

            if (narrow)
            {
                float compactButtonWidth = Mathf.Max(120f, (area.width - 48f) * 0.5f);
                Rect runRect = new Rect(area.x + 18f, y, compactButtonWidth, 38f);
                if (IsModelComplete)
                {
                    DrawSolidRect(runRect, new Color(0.08f, 0.22f, 0.24f, 1f));
                    DrawRectOutline(runRect, new Color(0.26f, 0.72f, 0.74f, 1f), 1f);
                    GUI.Label(runRect, "已完成", _buttonStyle);
                }
                else if (GUI.Button(runRect, _isRunning ? "暂停" : "继续", _buttonStyle))
                {
                    _isRunning = !_isRunning;
                }
                if (GUI.Button(new Rect(area.x + 30f + compactButtonWidth, y, compactButtonWidth, 38f), "重置", _buttonStyle))
                {
                    ResetExperiment();
                    _isRunning = true;
                }
                y += 48f;
                GUI.Label(new Rect(area.x + 18f, y, area.width - 36f, 24f), "模拟速度", _smallStyle);
                DrawSpeedButton(area.x + 18f, y + 28f, 74f, "0.5×", 0.5f);
                DrawSpeedButton(area.x + 99f, y + 28f, 74f, "1×", 1f);
                DrawSpeedButton(area.x + 180f, y + 28f, 74f, "2×", 2f);
                y += 70f;
                DrawNarrowSlider(area, ref y, "时间 t", _modelTime, 0f, MaximumModelTime, value => SetModelTime(value), _modelTime.ToString("0.00") + " s");
                DrawNarrowSlider(area, ref y, "外电场 E", _externalElectricField, 0.4f, 1.5f, value => { _externalElectricField = value; SetExperimentParameters(); }, _externalElectricField.ToString("0.00"));
                DrawNarrowSlider(area, ref y, "磁感应强度 B", _magneticField, 0f, 1.4f, value => { _magneticField = value; SetExperimentParameters(); }, _magneticField.ToString("0.00"));
                GUI.Label(new Rect(area.x + 18f, y + 2f, area.width - 36f, 24f), "显示选项", _smallStyle);
                y += 30f;
                float toggleWidth = Mathf.Max(120f, (area.width - 48f) * 0.5f);
                _showVectors = GUI.Toggle(new Rect(area.x + 18f, y, toggleWidth, 30f), _showVectors, "矢量与方向", _toggleStyle);
                _showTrajectories = GUI.Toggle(new Rect(area.x + 30f + toggleWidth, y, toggleWidth, 30f), _showTrajectories, "电子轨迹", _toggleStyle);
                y += 36f;
                _showIonLattice = GUI.Toggle(new Rect(area.x + 18f, y, toggleWidth, 30f), _showIonLattice, "正离子晶格", _toggleStyle);
                GUI.Label(new Rect(area.x + 30f + toggleWidth, y, toggleWidth, 38f), "归一化单位\n右键旋转 / 滚轮缩放", _smallStyle);
                ApplyVisibility();
                return;
            }

            Rect desktopRunRect = new Rect(area.x + 18f, y, buttonWidth, 40f);
            if (IsModelComplete)
            {
                DrawSolidRect(desktopRunRect, new Color(0.08f, 0.22f, 0.24f, 1f));
                DrawRectOutline(desktopRunRect, new Color(0.26f, 0.72f, 0.74f, 1f), 1f);
                GUI.Label(desktopRunRect, "已完成", _buttonStyle);
            }
            else if (GUI.Button(desktopRunRect, _isRunning ? "暂停" : "继续", _buttonStyle))
            {
                _isRunning = !_isRunning;
            }

            if (GUI.Button(
                    new Rect(area.x + 134f, y, buttonWidth, 40f),
                    "重置",
                    _buttonStyle))
            {
                ResetExperiment();
                _isRunning = true;
            }

            GUI.Label(
                new Rect(area.x + 18f, y + 52f, leftWidth - 38f, 26f),
                "模拟速度",
                _smallStyle);
            DrawSpeedButton(area.x + 18f, y + 82f, 74f, "0.5×", 0.5f);
            DrawSpeedButton(area.x + 99f, y + 82f, 74f, "1×", 1f);
            DrawSpeedButton(area.x + 180f, y + 82f, 74f, "2×", 2f);

            float sliderX = area.x + leftWidth + 18f;
            float sliderLabelWidth = 122f;
            float sliderValueWidth = 86f;
            float sliderWidth = Mathf.Max(
                150f,
                middleWidth - sliderLabelWidth - sliderValueWidth - 24f);

            GUI.Label(
                new Rect(sliderX, y, sliderLabelWidth, 26f),
                "时间 t",
                _smallStyle);
            float newTime = GUI.HorizontalSlider(
                new Rect(sliderX + sliderLabelWidth, y + 9f, sliderWidth, 22f),
                _modelTime,
                0f,
                MaximumModelTime);
            GUI.Label(
                new Rect(
                    sliderX + sliderLabelWidth + sliderWidth + 10f,
                    y,
                    sliderValueWidth,
                    26f),
                _modelTime.ToString("0.00") + " s",
                _smallStyle);
            if (Mathf.Abs(newTime - _modelTime) > 0.001f)
            {
                SetModelTime(newTime);
            }

            y += 42f;
            GUI.Label(
                new Rect(sliderX, y, sliderLabelWidth, 26f),
                "外电场 E",
                _smallStyle);
            float newElectricField = GUI.HorizontalSlider(
                new Rect(sliderX + sliderLabelWidth, y + 9f, sliderWidth, 22f),
                _externalElectricField,
                0.4f,
                1.5f);
            GUI.Label(
                new Rect(
                    sliderX + sliderLabelWidth + sliderWidth + 10f,
                    y,
                    sliderValueWidth,
                    26f),
                _externalElectricField.ToString("0.00"),
                _smallStyle);
            if (Mathf.Abs(newElectricField - _externalElectricField) > 0.001f)
            {
                _externalElectricField = newElectricField;
                SetExperimentParameters();
            }

            y += 42f;
            GUI.Label(
                new Rect(sliderX, y, sliderLabelWidth, 26f),
                "磁感应强度 B",
                _smallStyle);
            float newMagneticField = GUI.HorizontalSlider(
                new Rect(sliderX + sliderLabelWidth, y + 9f, sliderWidth, 22f),
                _magneticField,
                0f,
                1.4f);
            GUI.Label(
                new Rect(
                    sliderX + sliderLabelWidth + sliderWidth + 10f,
                    y,
                    sliderValueWidth,
                    26f),
                _magneticField.ToString("0.00"),
                _smallStyle);
            if (Mathf.Abs(newMagneticField - _magneticField) > 0.001f)
            {
                _magneticField = newMagneticField;
                SetExperimentParameters();
            }

            GUI.Label(
                new Rect(rightX, area.y + 18f, rightWidth - 116f, 26f),
                "显示选项",
                _smallStyle);
            if (GUI.Button(
                    new Rect(rightX + rightWidth - 102f, area.y + 16f, 96f, 38f),
                    "关闭",
                    _buttonStyle))
            {
                SetControlPanelVisible(false);
            }

            float firstToggleWidth = Mathf.Floor((rightWidth - 12f) * 0.5f);
            float secondToggleWidth = rightWidth - firstToggleWidth - 12f;
            _showVectors = GUI.Toggle(
                new Rect(rightX, area.y + 60f, firstToggleWidth, 28f),
                _showVectors,
                "矢量与方向",
                _toggleStyle);
            _showTrajectories = GUI.Toggle(
                new Rect(
                    rightX + firstToggleWidth + 12f,
                    area.y + 60f,
                    secondToggleWidth,
                    28f),
                _showTrajectories,
                "电子轨迹",
                _toggleStyle);
            _showIonLattice = GUI.Toggle(
                new Rect(rightX, area.y + 98f, firstToggleWidth, 28f),
                _showIonLattice,
                "正离子晶格",
                _toggleStyle);
            GUI.Label(
                new Rect(rightX, area.y + 130f, rightWidth, 54f),
                "单位：归一化实验单位    右键旋转 / 中键平移 / 滚轮缩放",
                _smallStyle);
            ApplyVisibility();

            float forceY = area.y + ForceDiagramAreaOffset;
            DrawForceAnalysis(
                new Rect(
                    area.x + 16f,
                    forceY,
                    area.width - 32f,
                    Mathf.Max(70f, area.yMax - forceY - 16f)));
        }

        private void DrawMobileControlPanel(Rect area)
        {
            float x = area.x + 18f;
            float width = area.width - 36f;
            float y = area.y + 12f;
            float buttonWidth = Mathf.Max(110f, (width - 12f) * 0.5f);
            Rect runRect = new Rect(x, y, buttonWidth, 34f);
            if (IsModelComplete)
            {
                DrawSolidRect(runRect, new Color(0.08f, 0.22f, 0.24f, 1f));
                DrawRectOutline(runRect, new Color(0.26f, 0.72f, 0.74f, 1f), 1f);
                GUI.Label(runRect, "已完成", _buttonStyle);
            }
            else if (GUI.Button(runRect, _isRunning ? "暂停" : "继续", _buttonStyle))
            {
                _isRunning = !_isRunning;
            }
            if (GUI.Button(new Rect(x + buttonWidth + 12f, y, buttonWidth, 34f), "重置", _buttonStyle))
            {
                ResetExperiment();
                _isRunning = true;
            }

            y += 42f;
            GUI.Label(new Rect(x, y, 90f, 22f), "模拟速度", _smallStyle);
            DrawSpeedButton(x, y + 24f, 72f, "0.5×", 0.5f);
            DrawSpeedButton(x + 80f, y + 24f, 72f, "1×", 1f);
            DrawSpeedButton(x + 160f, y + 24f, 72f, "2×", 2f);

            y += 62f;
            DrawNarrowSlider(area, ref y, "时间 t", _modelTime, 0f, MaximumModelTime, value => SetModelTime(value), _modelTime.ToString("0.00") + " s");
            DrawNarrowSlider(area, ref y, "外电场 E", _externalElectricField, 0.4f, 1.5f, value => { _externalElectricField = value; SetExperimentParameters(); }, _externalElectricField.ToString("0.00"));
            DrawNarrowSlider(area, ref y, "磁感应 B", _magneticField, 0f, 1.4f, value => { _magneticField = value; SetExperimentParameters(); }, _magneticField.ToString("0.00"));

            y += 2f;
            GUI.Label(new Rect(x, y, width, 22f), "显示选项", _smallStyle);
            y += 22f;
            float toggleWidth = (width - 12f) * 0.5f;
            _showVectors = GUI.Toggle(new Rect(x, y, toggleWidth, 26f), _showVectors, "矢量与方向", _toggleStyle);
            _showTrajectories = GUI.Toggle(new Rect(x + toggleWidth + 12f, y, toggleWidth, 26f), _showTrajectories, "电子轨迹", _toggleStyle);
            y += 28f;
            _showIonLattice = GUI.Toggle(new Rect(x, y, toggleWidth, 26f), _showIonLattice, "正离子晶格", _toggleStyle);
            ApplyVisibility();
        }

        private void DrawForceAnalysis(Rect area)
        {
            DrawSolidRect(area, new Color(0.047f, 0.064f, 0.074f, 0.98f));
            DrawSolidRect(
                new Rect(area.x, area.y, area.width, 2f),
                new Color(0.20f, 0.78f, 0.90f, 0.90f));

            float titleWidth = Mathf.Clamp(area.width * 0.38f, 360f, 500f);
            float equationX = area.x + titleWidth + 20f;
            GUI.Label(
                new Rect(area.x + 18f, area.y + 2f, titleWidth, 44f),
                "受力分析（电子 q = -e）",
                _sectionTitleStyle);
            GUI.Label(
                new Rect(
                    equationX,
                    area.y + 8f,
                    Mathf.Max(260f, area.xMax - equationX - 18f),
                    34f),
                "F洛 = q(v×B)    F霍 = -eE霍    稳态：F洛 + F霍 ≈ 0",
                _smallStyle);

            float contentY = area.y + 54f;
            float contentHeight = Mathf.Max(30f, area.height - 64f);
            float fieldWidth = Mathf.Clamp(area.width * 0.48f, 420f, 1700f);
            Rect fieldRect = new Rect(area.x + 12f, contentY, fieldWidth, contentHeight);
            Rect forceRect = new Rect(
                fieldRect.xMax + 18f,
                contentY,
                Mathf.Max(260f, area.xMax - fieldRect.xMax - 30f),
                contentHeight);

            HallEffectSimulation simulation = _hallPanel == null ? null : _hallPanel.Simulation;
            DrawFieldAndMotionDiagram(fieldRect, simulation);
            DrawLorentzForceBalance(forceRect, simulation);
        }

        private void DrawFieldAndMotionDiagram(Rect area, HallEffectSimulation simulation)
        {
            if (area.height < 24f || area.width < 220f)
            {
                return;
            }

            DrawSolidRect(area, new Color(0.022f, 0.031f, 0.036f, 0.72f));
            float firstRowY = area.y + area.height * 0.30f;
            float secondRowY = area.y + area.height * 0.72f;
            Color electric = new Color(1f, 0.60f, 0.12f, 1f);
            Color drift = new Color(0.58f, 0.96f, 0.28f, 1f);
            Color magnetic = new Color(0.30f, 0.70f, 1f, 1f);
            bool noMagneticField = simulation == null || simulation.MagneticFieldMagnitude <= 0.0001f;

            DrawGuiArrow(
                new Vector2(area.x + 34f, firstRowY),
                new Vector2(area.x + area.width * 0.43f, firstRowY),
                7f,
                electric);
            float electricLabelWidth = Mathf.Clamp(area.width * 0.34f, 170f, 280f);
            DrawForceLabel(
                new Rect(
                    Mathf.Min(
                        area.x + area.width * 0.45f,
                        area.xMax - electricLabelWidth - 10f),
                    firstRowY - 20f,
                    electricLabelWidth,
                    38f),
                "E外  +X",
                electric);

            DrawGuiArrow(
                new Vector2(area.x + area.width * 0.42f, secondRowY),
                new Vector2(area.x + 34f, secondRowY),
                7f,
                drift);
            float driftLabelWidth = Mathf.Clamp(area.width * 0.36f, 180f, 300f);
            DrawForceLabel(
                new Rect(
                    Mathf.Min(
                        area.x + area.width * 0.45f,
                        area.xMax - driftLabelWidth - 10f),
                    secondRowY - 20f,
                    driftLabelWidth,
                    38f),
                "v漂  -X",
                drift);

            if (noMagneticField)
            {
                DrawForceLabel(
                    new Rect(area.x + area.width * 0.54f, area.y + area.height * 0.38f, area.width * 0.40f, 62f),
                    "B = 0\n无横向洛伦兹力，E霍 = 0",
                    magnetic);
                return;
            }

            Vector2 magneticCenter = new Vector2(
                area.x + area.width * 0.79f,
                area.y + area.height * 0.45f);
            GUI.Label(
                new Rect(magneticCenter.x - 34f, magneticCenter.y - 34f, 68f, 68f),
                "⊗",
                _forceSymbolStyle);
            float magneticLabelWidth = Mathf.Clamp(
                area.width * 0.46f,
                190f,
                332f);
            DrawForceLabel(
                new Rect(
                    Mathf.Clamp(
                        magneticCenter.x - magneticLabelWidth * 0.5f,
                        area.x + 8f,
                        area.xMax - magneticLabelWidth - 8f),
                    magneticCenter.y - 4f,
                    magneticLabelWidth,
                    38f),
                "B +Z（入屏）",
                magnetic);
        }

        private void DrawLorentzForceBalance(Rect area, HallEffectSimulation simulation)
        {
            if (area.height < 24f || area.width < 180f)
            {
                return;
            }

            DrawSolidRect(area, new Color(0.022f, 0.031f, 0.036f, 0.72f));
            float upperY = area.y + 55f;
            float lowerY = area.yMax - 52f;
            float lorentzX = area.x + area.width * 0.34f;
            float hallX = area.x + area.width * 0.66f;
            Color lorentz = new Color(1f, 0.30f, 0.62f, 1f);
            Color hall = new Color(0.98f, 0.80f, 0.20f, 1f);
            bool noMagneticField = simulation == null || simulation.MagneticFieldMagnitude <= 0.0001f;
            bool hallFieldDisabled = simulation == null || !simulation.HallFieldEnabled || simulation.HallElectricFieldMagnitude <= 0.0001f;

            if (noMagneticField)
            {
                DrawForceLabel(
                    new Rect(area.x + 14f, area.y + 18f, area.width - 28f, 44f),
                    "B = 0：F洛 = 0，E霍 = 0，横向受力为零",
                    new Color(0.68f, 0.88f, 1f, 1f));
                DrawForceLabel(
                    new Rect(area.x + 14f, area.y + 72f, area.width - 28f, 38f),
                    "请增大磁感应强度 B，观察受力与霍尔电场建立",
                    new Color(0.76f, 0.82f, 0.86f, 1f));
                return;
            }

            DrawGuiArrow(
                new Vector2(lorentzX, upperY),
                new Vector2(lorentzX, lowerY),
                8f,
                lorentz);
            DrawGuiArrow(
                new Vector2(hallX, lowerY),
                new Vector2(hallX, upperY),
                8f,
                hall);

            float statusWidth = Mathf.Clamp(area.width * 0.42f, 180f, 270f);
            DrawForceLabel(
                new Rect(
                    area.xMax - statusWidth - 10f,
                    area.y + 8f,
                    statusWidth,
                    34f),
                "稳态合力趋于 0",
                new Color(0.86f, 0.96f, 1f, 1f));

            float labelWidth = Mathf.Max(130f, area.width * 0.5f - 24f);
            float labelY = area.yMax - 42f;
            DrawForceLabel(
                new Rect(area.x + 12f, labelY, labelWidth, 36f),
                "F洛 = q(v×B)  -Y",
                lorentz);
            DrawForceLabel(
                new Rect(area.x + area.width * 0.5f + 12f, labelY, labelWidth, 36f),
                hallFieldDisabled ? "F霍 = 0（对照组）" : "F霍 = -eE霍  +Y",
                hall);
        }

        private void DrawGuiArrow(Vector2 start, Vector2 end, float thickness, Color color)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length < 2f)
            {
                return;
            }

            Vector2 direction = delta / length;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            float headLength = Mathf.Clamp(length * 0.23f, 9f, 18f);
            float headWidth = headLength * 0.62f;
            Vector2 headBase = end - direction * headLength;

            DrawGuiLine(start, end, thickness, color);
            DrawGuiLine(headBase + normal * headWidth, end, thickness, color);
            DrawGuiLine(headBase - normal * headWidth, end, thickness, color);
        }

        private void DrawGuiLine(Vector2 start, Vector2 end, float thickness, Color color)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length < 0.5f)
            {
                return;
            }

            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.color = color;
            GUI.DrawTexture(
                new Rect(start.x, start.y - thickness * 0.5f, length, thickness),
                _whiteTexture);
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private void DrawForceLabel(Rect rect, string text, Color color)
        {
            DrawSolidRect(
                new Rect(rect.x - 4f, rect.y + 3f, rect.width + 8f, rect.height - 6f),
                new Color(0.010f, 0.016f, 0.020f, 0.72f));
            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.Label(rect, text, _forceLabelStyle);
            GUI.color = previousColor;
        }

        private void DrawSpeedButton(float x, float y, float width, string text, float speed)
        {
            bool active = Mathf.Abs(_simulationSpeed - speed) < 0.01f;
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = active
                ? new Color(0.18f, 0.82f, 0.94f, 1f)
                : new Color(0.27f, 0.34f, 0.38f, 1f);
            if (GUI.Button(new Rect(x, y, width, 30f), text, _buttonStyle))
            {
                _simulationSpeed = speed;
            }

            GUI.backgroundColor = previous;
        }

        private void DrawNarrowSlider(
            Rect area,
            ref float y,
            string label,
            float value,
            float min,
            float max,
            Action<float> setter,
            string display)
        {
            float labelWidth = 88f;
            float valueWidth = 58f;
            float sliderWidth = Mathf.Max(110f, area.width - labelWidth - valueWidth - 52f);
            GUI.Label(new Rect(area.x + 18f, y, labelWidth, 24f), label, _smallStyle);
            float next = GUI.HorizontalSlider(new Rect(area.x + 18f + labelWidth, y + 4f, sliderWidth, 22f), value, min, max);
            GUI.Label(new Rect(area.x + 18f + labelWidth + sliderWidth + 8f, y, valueWidth, 24f), display, _smallStyle);
            if (Mathf.Abs(next - value) > 0.001f)
            {
                setter(next);
            }
            y += 38f;
        }

        private void DrawParticleLegend()
        {
            bool mobile = Screen.width < 700;
            Rect toggle = mobile ? new Rect(16f, 98f, 150f, 34f) : new Rect(Screen.width - 430f, 24f, 196f, 46f);
            if (GUI.Button(toggle, _showLegend ? "收起粒子图例" : "粒子颜色说明", _buttonStyle)) _showLegend = !_showLegend;
            if (!_showLegend) return;
            bool compact = Screen.height < 850f || mobile;
            float legendWidth = compact
                ? Mathf.Clamp(Screen.width - 36f, 300f, 620f)
                : Mathf.Clamp(Screen.width * 0.29f, 500f, 620f);
            float rowHeight = compact ? 34f : 48f;
            float legendHeight = compact ? (mobile ? 184f : 116f) : 68f + rowHeight * 6f;
            // Keep the desktop legend away from the crystal's left teaching
            // labels; narrow screens use the full available width.
            float x = compact ? 18f : Mathf.Max(18f, Screen.width - legendWidth - 18f);
            float y = HeaderHeight + 16f;

            DrawSolidRect(
                new Rect(x + 4f, y + 5f, legendWidth, legendHeight),
                new Color(0f, 0f, 0f, 0.45f));
            DrawSolidRect(
                new Rect(x, y, legendWidth, legendHeight),
                new Color(0.025f, 0.035f, 0.042f, 0.94f));
            DrawRectOutline(
                new Rect(x, y, legendWidth, legendHeight),
                new Color(0.24f, 0.70f, 0.82f, 0.92f),
                2f);
            DrawSolidRect(
                new Rect(x, y, legendWidth, 3f),
                new Color(0.18f, 0.78f, 0.90f, 1f));

            GUI.Label(
                new Rect(x + 20f, y + 8f, legendWidth - 40f, 38f),
                "粒子图例：每个小球代表什么",
                _legendTitleStyle);

            if (compact)
            {
                GUI.Label(
                    new Rect(x + 20f, y + 48f, legendWidth - 40f, mobile ? 58f : 28f),
                    "红球 = 正离子晶格　金黄球 = 自由电子　青球 = 跟踪电子",
                    _legendItemStyle);
                GUI.Label(
                    new Rect(x + 20f, y + (mobile ? 110f : 78f), legendWidth - 40f, mobile ? 58f : 28f),
                    "橙球 = +Y 面正离子　蓝球 = -Y 面电子　青线 = 跟踪电子轨迹",
                    _legendItemStyle);
                return;
            }

            float rowY = y + 54f;
            DrawLegendRow(
                new Rect(x + 20f, rowY, legendWidth - 40f, rowHeight),
                new Color(0.92f, 0.25f, 0.14f, 1f),
                "红色球：半导体正离子晶格（固定正电荷）");
            DrawLegendRow(
                new Rect(x + 20f, rowY + rowHeight, legendWidth - 40f, rowHeight),
                new Color(1f, 0.68f, 0.08f, 1f),
                "金黄色球：自由电子（参与导电）");
            DrawLegendRow(
                new Rect(x + 20f, rowY + rowHeight * 2f, legendWidth - 40f, rowHeight),
                new Color(0.16f, 0.94f, 1f, 1f),
                "青色球：被跟踪电子（显示运动轨迹）");
            DrawLegendRow(
                new Rect(x + 20f, rowY + rowHeight * 3f, legendWidth - 40f, rowHeight),
                new Color(1f, 0.48f, 0.20f, 1f),
                "亮橙色球：+Y 面暴露的正离子");
            DrawLegendRow(
                new Rect(x + 20f, rowY + rowHeight * 4f, legendWidth - 40f, rowHeight),
                new Color(0.18f, 0.76f, 1f, 1f),
                "蓝色球：-Y 面积累的电子");

            Rect trailRow = new Rect(
                x + 20f,
                rowY + rowHeight * 5f,
                legendWidth - 40f,
                rowHeight);
            DrawSolidRect(
                new Rect(trailRow.x + 7f, trailRow.y + 18f, 26f, 5f),
                new Color(0.24f, 0.88f, 1f, 1f));
            DrawSolidRect(
                new Rect(trailRow.x + 17f, trailRow.y + 4f, 5f, 32f),
                new Color(0.24f, 0.88f, 1f, 0.28f));
            GUI.Label(
                new Rect(trailRow.x + 42f, trailRow.y, trailRow.width - 42f, trailRow.height),
                "青色轨迹线：被跟踪电子的运动路径",
                _legendItemStyle);
        }

        private void DrawLegendRow(Rect rect, Color symbolColor, string text)
        {
            Color previous = GUI.color;
            GUI.color = symbolColor;
            GUI.Label(
                new Rect(rect.x, rect.y - 3f, 26f, rect.height + 6f),
                "●",
                _legendSymbolStyle);
            GUI.color = previous;
            GUI.Label(
                new Rect(rect.x + 34f, rect.y, rect.width - 34f, rect.height),
                text,
                _legendItemStyle);
        }

        private void DrawWorldLabels()
        {
            _hallPanel.DrawWorldLabels();
            _controlPanel.DrawWorldLabels();
        }

        private void DrawWorldLabel(
            Vector3 worldPosition,
            string text,
            GUIStyle style,
            float width,
            float height)
        {
            Vector3 screenPoint = _camera.WorldToScreenPoint(worldPosition);
            if (screenPoint.z <= 0f)
            {
                return;
            }

            float x = Mathf.Clamp(
                screenPoint.x - width * 0.5f,
                8f,
                Mathf.Max(8f, Screen.width - width - 8f));
            float y = Mathf.Clamp(
                Screen.height - screenPoint.y - height * 0.5f,
                HeaderHeight + 4f,
                Mathf.Max(HeaderHeight + 4f, Screen.height - height - 8f));

            DrawLabelFrame(new Rect(x, y, width, height), text, style);
        }

        private float GetAnnotationBottomLimit()
        {
            if (_isControlPanelVisible)
            {
                return GetControlPanelRect().yMin - 8f;
            }

            return Screen.height - 8f;
        }

        private void DrawLabelFrame(Rect rect, string text, GUIStyle style)
        {
            float x = rect.x;
            float y = rect.y;
            float width = rect.width;
            float height = rect.height;

            DrawSolidRect(
                new Rect(x + 3f, y + 4f, width + 8f, height + 4f),
                new Color(0f, 0f, 0f, 0.48f));
            DrawRectOutline(
                new Rect(x - 7f, y - 5f, width + 14f, height + 10f),
                new Color(0.06f, 0.12f, 0.15f, 0.98f),
                3f);
            DrawSolidRect(
                new Rect(x - 5f, y - 3f, width + 10f, height + 6f),
                new Color(0.012f, 0.019f, 0.024f, 0.96f));
            DrawSolidRect(
                new Rect(x - 5f, y - 3f, 4f, height + 6f),
                new Color(0.20f, 0.76f, 0.90f, 0.92f));

            Color textColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, textColor.a * 0.92f);
            GUI.Label(new Rect(x + 2f, y + 2f, width, height), text, style);
            GUI.color = textColor;
            GUI.Label(new Rect(x, y, width, height), text, style);
        }

        private void DrawColoredWorldLabel(
            Vector3 worldPosition,
            string text,
            GUIStyle style,
            float width,
            float height,
            Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            DrawWorldLabel(worldPosition, text, style, width, height);
            GUI.color = previousColor;
        }

        private void EnsureGuiStyles()
        {
            if (_labelStyle != null)
            {
                return;
            }

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.90f, 0.97f, 1f, 1f) }
            };
            _subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = 17,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.76f, 0.88f, 0.92f, 1f) },
                wordWrap = true
            };
            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = 21,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                wordWrap = true
            };
            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.90f, 0.96f, 0.98f, 1f) },
                wordWrap = true,
                clipping = TextClipping.Overflow
            };
            _vectorLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                wordWrap = true
            };
            _sectionTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = 27,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.91f, 0.97f, 1f, 1f) }
            };
            _forceLabelStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = 23,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white },
                wordWrap = true,
                clipping = TextClipping.Overflow
            };
            _forceSymbolStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = 54,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.32f, 0.78f, 1f, 1f) }
            };
            _worldSmallStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = 17,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.94f, 0.98f, 1f, 1f) },
                wordWrap = true
            };
            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                font = _font,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Clip
            };
            _toggleStyle = new GUIStyle(GUI.skin.toggle)
            {
                font = _font,
                fontSize = 18,
                normal = { textColor = new Color(0.90f, 0.96f, 0.98f, 1f) },
                onNormal = { textColor = Color.white },
                wordWrap = true
            };
            _legendTitleStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = 23,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.92f, 0.98f, 1f, 1f) }
            };
            _legendItemStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.92f, 0.97f, 0.98f, 1f) },
                wordWrap = true,
                clipping = TextClipping.Overflow
            };
            _legendSymbolStyle = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        private Rect GetHeaderRect()
        {
            return new Rect(0f, 0f, Screen.width, HeaderHeight);
        }

        private Rect GetControlPanelToggleRect()
        {
            if (Screen.width < 700) return new Rect(Screen.width - 166f, 98f, 150f, 34f);
            return new Rect(Screen.width - 224f, 24f, 200f, 46f);
        }

        private Rect GetControlPanelRect()
        {
            float panelHeight = GetControlPanelHeight();
            return new Rect(
                18f,
                Screen.height - panelHeight - 14f,
                Screen.width - 36f,
                panelHeight);
        }

        private float GetControlPanelHeight()
        {
            if (Screen.width < 700f) return 318f;
            if (Screen.width < 1236f) return 382f;
            return Mathf.Clamp(
                Screen.height * ControlPanelHeightFraction,
                MinimumControlPanelHeight,
                MaximumControlPanelHeight);
        }

        private bool IsPointerOverGui()
        {
            Vector2 mouse = new Vector2(
                Input.mousePosition.x,
                Screen.height - Input.mousePosition.y);
            if (GetHeaderRect().Contains(mouse) ||
                GetControlPanelToggleRect().Contains(mouse))
            {
                return true;
            }

            return _isControlPanelVisible && GetControlPanelRect().Contains(mouse);
        }

        private void DrawSolidRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _whiteTexture);
            GUI.color = previous;
        }

        private void DrawRectOutline(Rect rect, Color color, float thickness)
        {
            DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawSolidRect(
                new Rect(rect.x, rect.yMax - thickness, rect.width, thickness),
                color);
            DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawSolidRect(
                new Rect(rect.xMax - thickness, rect.y, thickness, rect.height),
                color);
        }

        private Material CreateMaterial(
            string name,
            Color color,
            float metallic,
            float smoothness,
            Color? emission,
            bool transparent,
            bool doubleSided)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            Material material = new Material(shader)
            {
                name = "HallEffect3D_" + name,
                color = color
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (transparent)
            {
                material.SetFloat("_Mode", 3f);
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
            }

            if (doubleSided && material.HasProperty("_Cull"))
            {
                material.SetInt("_Cull", 0);
            }

            _materials.Add(material);
            return material;
        }

        private GameObject CreateBox(
            Transform parent,
            string name,
            Vector3 scale,
            Vector3 localPosition,
            Material material,
            bool receiveShadows)
        {
            return CreatePart(
                parent,
                name,
                _cubeMesh,
                material,
                localPosition,
                Quaternion.identity,
                scale,
                true,
                receiveShadows);
        }

        private GameObject CreateSphere(
            Transform parent,
            string name,
            Vector3 localPosition,
            float diameter,
            Material material)
        {
            return CreatePart(
                parent,
                name,
                _sphereMesh,
                material,
                localPosition,
                Quaternion.identity,
                Vector3.one * diameter,
                true,
                false);
        }

        private GameObject CreatePart(
            Transform parent,
            string name,
            Mesh mesh,
            Material material,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            bool castShadows,
            bool receiveShadows)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = localRotation;
            part.transform.localScale = localScale;

            MeshFilter filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;
            renderer.receiveShadows = receiveShadows;
            return part;
        }

        private static void DestroyMesh(Mesh mesh)
        {
            if (mesh != null)
            {
                UnityEngine.Object.Destroy(mesh);
            }
        }

        private static Mesh CreateCubeMesh()
        {
            Mesh mesh = new Mesh { name = "HallEffect3DCubeMesh" };
            Vector3[] vertices =
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, -0.5f)
            };
            int[] triangles =
            {
                0, 2, 1, 0, 3, 2,
                4, 6, 5, 4, 7, 6,
                0, 1, 5, 0, 5, 4,
                3, 7, 6, 3, 6, 2,
                8, 9, 10, 8, 10, 11,
                12, 15, 14, 12, 14, 13
            };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateSphereMesh(int longitudeSegments, int latitudeSegments)
        {
            Mesh mesh = new Mesh { name = "HallEffect3DSphereMesh" };
            Vector3[] vertices = new Vector3[
                (latitudeSegments + 1) * (longitudeSegments + 1)];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[
                latitudeSegments * longitudeSegments * 6];

            int vertexIndex = 0;
            for (int latitude = 0; latitude <= latitudeSegments; latitude++)
            {
                float v = latitude / (float)latitudeSegments;
                float theta = v * Mathf.PI;
                float y = Mathf.Cos(theta) * 0.5f;
                float radius = Mathf.Sin(theta) * 0.5f;
                for (int longitude = 0; longitude <= longitudeSegments; longitude++)
                {
                    float u = longitude / (float)longitudeSegments;
                    float phi = u * Mathf.PI * 2f;
                    vertices[vertexIndex] = new Vector3(
                        Mathf.Sin(phi) * radius,
                        y,
                        Mathf.Cos(phi) * radius);
                    uvs[vertexIndex] = new Vector2(u, 1f - v);
                    vertexIndex++;
                }
            }

            int triangleIndex = 0;
            for (int latitude = 0; latitude < latitudeSegments; latitude++)
            {
                for (int longitude = 0; longitude < longitudeSegments; longitude++)
                {
                    int row = latitude * (longitudeSegments + 1) + longitude;
                    int nextRow = row + longitudeSegments + 1;
                    triangles[triangleIndex++] = row;
                    triangles[triangleIndex++] = nextRow;
                    triangles[triangleIndex++] = row + 1;
                    triangles[triangleIndex++] = row + 1;
                    triangles[triangleIndex++] = nextRow;
                    triangles[triangleIndex++] = nextRow + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateCylinderMesh(int segments)
        {
            Mesh mesh = new Mesh { name = "HallEffect3DCylinderMesh" };
            Vector3[] vertices = new Vector3[(segments + 1) * 2 + 2];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 12];

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = t * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * 0.5f;
                float y = Mathf.Sin(angle) * 0.5f;
                vertices[i] = new Vector3(x, y, -0.5f);
                vertices[i + segments + 1] = new Vector3(x, y, 0.5f);
                uvs[i] = new Vector2(t, 0f);
                uvs[i + segments + 1] = new Vector2(t, 1f);
            }

            int bottomCenter = vertices.Length - 2;
            int topCenter = vertices.Length - 1;
            vertices[bottomCenter] = new Vector3(0f, 0f, -0.5f);
            vertices[topCenter] = new Vector3(0f, 0f, 0.5f);
            uvs[bottomCenter] = new Vector2(0.5f, 0.5f);
            uvs[topCenter] = new Vector2(0.5f, 0.5f);

            int triangleIndex = 0;
            for (int i = 0; i < segments; i++)
            {
                int next = i + 1;
                int top = i + segments + 1;
                int topNext = next + segments + 1;

                triangles[triangleIndex++] = i;
                triangles[triangleIndex++] = top;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = top;
                triangles[triangleIndex++] = topNext;

                triangles[triangleIndex++] = bottomCenter;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = i;
                triangles[triangleIndex++] = topCenter;
                triangles[triangleIndex++] = top;
                triangles[triangleIndex++] = topNext;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateConeMesh(int segments)
        {
            Mesh mesh = new Mesh { name = "HallEffect3DConeMesh" };
            Vector3[] vertices = new Vector3[segments + 2];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float angle = t * Mathf.PI * 2f;
                vertices[i] = new Vector3(
                    Mathf.Cos(angle) * 0.5f,
                    Mathf.Sin(angle) * 0.5f,
                    -0.5f);
                uvs[i] = new Vector2(t, 0f);
            }

            int apex = vertices.Length - 2;
            int baseCenter = vertices.Length - 1;
            vertices[apex] = new Vector3(0f, 0f, 0.5f);
            vertices[baseCenter] = new Vector3(0f, 0f, -0.5f);
            uvs[apex] = new Vector2(0.5f, 1f);
            uvs[baseCenter] = new Vector2(0.5f, 0.5f);

            int triangleIndex = 0;
            for (int i = 0; i < segments; i++)
            {
                int next = i + 1;
                triangles[triangleIndex++] = i;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = apex;
                triangles[triangleIndex++] = baseCenter;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = i;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private sealed class Palette
        {
            public readonly Material Crystal;
            public readonly Material CrystalEdge;
            public readonly Material Ion;
            public readonly Material Electron;
            public readonly Material Highlight;
            public readonly Material Trail;
            public readonly Material ExternalField;
            public readonly Material Drift;
            public readonly Material MagneticField;
            public readonly Material HallField;
            public readonly Material Lorentz;
            public readonly Material HallForce;
            public readonly Material PositiveCharge;
            public readonly Material NegativeCharge;
            public readonly Material ForcePlane;
            public readonly Material ForcePlaneEdge;
            public readonly Material Electrode;
            public readonly Material Floor;
            public readonly Material Hardware;

            public Palette(HallEffectLabController owner)
            {
                Crystal = owner.CreateMaterial(
                    "Crystal",
                    new Color(0.13f, 0.42f, 0.56f, 0.20f),
                    0.05f,
                    0.92f,
                    null,
                    true,
                    false);
                CrystalEdge = owner.CreateMaterial(
                    "CrystalEdge",
                    new Color(0.25f, 0.66f, 0.78f, 1f),
                    0.45f,
                    0.72f,
                    new Color(0.015f, 0.12f, 0.16f, 1f),
                    false,
                    false);
                Ion = owner.CreateMaterial(
                    "PositiveIon",
                    new Color(0.92f, 0.25f, 0.14f, 1f),
                    0.45f,
                    0.60f,
                    new Color(0.16f, 0.018f, 0.005f, 1f),
                    false,
                    false);
                Electron = owner.CreateMaterial(
                    "Electron",
                    new Color(1f, 0.68f, 0.08f, 1f),
                    0.05f,
                    0.78f,
                    new Color(0.45f, 0.18f, 0.005f, 1f),
                    false,
                    false);
                Highlight = owner.CreateMaterial(
                    "HighlightedElectron",
                    new Color(0.16f, 0.94f, 1f, 1f),
                    0.02f,
                    0.88f,
                    new Color(0.05f, 0.52f, 0.75f, 1f),
                    false,
                    false);
                Trail = owner.CreateMaterial(
                    "ElectronTrail",
                    new Color(0.24f, 0.88f, 1f, 1f),
                    0.02f,
                    0.88f,
                    new Color(0.02f, 0.28f, 0.40f, 1f),
                    false,
                    true);
                ExternalField = owner.CreateMaterial(
                    "ExternalElectricField",
                    new Color(1f, 0.58f, 0.08f, 1f),
                    0.05f,
                    0.80f,
                    new Color(0.48f, 0.16f, 0.005f, 1f),
                    false,
                    true);
                Drift = owner.CreateMaterial(
                    "DriftVelocity",
                    new Color(0.55f, 0.94f, 0.26f, 1f),
                    0.05f,
                    0.80f,
                    new Color(0.12f, 0.38f, 0.015f, 1f),
                    false,
                    true);
                MagneticField = owner.CreateMaterial(
                    "MagneticField",
                    new Color(0.24f, 0.62f, 1f, 1f),
                    0.10f,
                    0.84f,
                    new Color(0.025f, 0.16f, 0.55f, 1f),
                    false,
                    true);
                HallField = owner.CreateMaterial(
                    "HallElectricField",
                    new Color(1f, 0.24f, 0.18f, 1f),
                    0.05f,
                    0.82f,
                    new Color(0.50f, 0.025f, 0.015f, 1f),
                    false,
                    true);
                Lorentz = owner.CreateMaterial(
                    "LorentzForce",
                    new Color(1f, 0.22f, 0.78f, 1f),
                    0.05f,
                    0.84f,
                    new Color(0.52f, 0.015f, 0.24f, 1f),
                    false,
                    true);
                HallForce = owner.CreateMaterial(
                    "HallForce",
                    new Color(0.98f, 0.80f, 0.20f, 1f),
                    0.04f,
                    0.86f,
                    new Color(0.48f, 0.28f, 0.008f, 1f),
                    false,
                    true);
                PositiveCharge = owner.CreateMaterial(
                    "ExposedPositiveCharge",
                    new Color(1f, 0.48f, 0.20f, 1f),
                    0.08f,
                    0.80f,
                    new Color(0.55f, 0.10f, 0.015f, 1f),
                    false,
                    false);
                NegativeCharge = owner.CreateMaterial(
                    "AccumulatedElectronCharge",
                    new Color(0.18f, 0.76f, 1f, 1f),
                    0.04f,
                    0.86f,
                    new Color(0.025f, 0.30f, 0.60f, 1f),
                    false,
                    false);
                ForcePlane = owner.CreateMaterial(
                    "ForceDirectionPlane",
                    new Color(0.035f, 0.11f, 0.145f, 0.34f),
                    0.02f,
                    0.72f,
                    new Color(0.01f, 0.045f, 0.06f, 1f),
                    true,
                    true);
                ForcePlaneEdge = owner.CreateMaterial(
                    "ForceDirectionPlaneEdge",
                    new Color(0.30f, 0.82f, 0.94f, 0.88f),
                    0.05f,
                    0.82f,
                    new Color(0.025f, 0.24f, 0.32f, 1f),
                    true,
                    true);
                Electrode = owner.CreateMaterial(
                    "CopperElectrode",
                    new Color(0.82f, 0.38f, 0.14f, 1f),
                    0.88f,
                    0.66f,
                    null,
                    false,
                    false);
                Floor = owner.CreateMaterial(
                    "LaboratoryBench",
                    new Color(0.085f, 0.095f, 0.10f, 1f),
                    0.72f,
                    0.34f,
                    null,
                    false,
                    false);
                Hardware = owner.CreateMaterial(
                    "DarkHardware",
                    new Color(0.10f, 0.12f, 0.13f, 1f),
                    0.86f,
                    0.48f,
                    null,
                    false,
                    false);
            }
        }

        private sealed class ArrowVisual
        {
            private readonly Transform _root;
            private readonly Transform _shaft;
            private readonly Transform _head;
            private readonly float _radius;

            public ArrowVisual(
                HallEffectLabController owner,
                Transform parent,
                string name,
                Material material,
                float radius)
            {
                _root = new GameObject(name).transform;
                _root.SetParent(parent, false);
                _radius = radius;
                _shaft = owner.CreatePart(
                    _root,
                    "Shaft",
                    owner._cylinderMesh,
                    material,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one,
                    false,
                    false).transform;
                _head = owner.CreatePart(
                    _root,
                    "Head",
                    owner._coneMesh,
                    material,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one,
                    false,
                    false).transform;
            }

            public void Set(Vector3 start, Vector3 end)
            {
                Vector3 delta = end - start;
                float length = delta.magnitude;
                if (length < 0.02f)
                {
                    SetVisible(false);
                    return;
                }

                SetVisible(true);
                Vector3 direction = delta / length;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, direction);
                float headLength = Mathf.Clamp(length * 0.25f, 0.34f, 0.74f);
                float shaftLength = Mathf.Max(0.02f, length - headLength * 0.86f);

                _shaft.localPosition = start + direction * shaftLength * 0.5f;
                _shaft.localRotation = rotation;
                _shaft.localScale = new Vector3(
                    _radius * 2f,
                    _radius * 2f,
                    shaftLength);

                _head.localPosition = end - direction * headLength * 0.5f;
                _head.localRotation = rotation;
                _head.localScale = new Vector3(
                    _radius * 3.4f,
                    _radius * 3.4f,
                    headLength);
            }

            public void SetVisible(bool visible)
            {
                _root.gameObject.SetActive(visible);
            }
        }

        private sealed class HallPanel
        {
            private readonly HallEffectLabController _owner;
            private readonly bool _isReferenceModel;
            private readonly Transform _root;
            private Transform _ionRoot;
            private readonly Transform _vectorRoot;
            private readonly Transform _trajectoryRoot;
            private readonly Transform[] _particleTransforms;
            private readonly float[] _particleDepths;
            private readonly LineRenderer[] _trails;
            private readonly int[] _trailSerials;
            private readonly List<Vector3>[] _trailPoints;
            private readonly Transform[] _positiveCharges;
            private readonly Transform[] _negativeCharges;
            private readonly ArrowVisual _hallArrow;
            private readonly ArrowVisual _lorentzArrow;
            private readonly string _panelTitle;
            private float _trailAccumulator;
            private Vector3 _lorentzLabelPosition;
            private bool _vectorsVisible = true;
            private bool _trajectoriesVisible = true;
            private bool _ionLatticeVisible = true;

            public HallEffectSimulation Simulation { get; }
            public Transform Root => _root;
            public bool HasForceDirectionPlane { get; private set; }

            public HallPanel(
                HallEffectLabController owner,
                Transform parent,
                Palette palette,
                string panelTitle,
                Vector3 localPosition,
                bool hallFieldEnabled,
                int randomSeed)
            {
                _owner = owner;
                _panelTitle = panelTitle;
                _isReferenceModel = hallFieldEnabled;
                Simulation = new HallEffectSimulation(ParticleCount, randomSeed)
                {
                    HallFieldEnabled = hallFieldEnabled
                };

                GameObject panelObject = new GameObject(
                    hallFieldEnabled ? "HallFieldExperiment3D" : "NoHallFieldControl3D");
                _root = panelObject.transform;
                _root.SetParent(parent, false);
                _root.localPosition = localPosition;
                _root.localScale = Vector3.one * 0.72f;

                owner.CreateBox(
                    _root,
                    "SemiconductorCrystal",
                    new Vector3(
                        CrystalHalfWidth * 2f,
                        CrystalHalfHeight * 2f,
                        CrystalHalfDepth * 2f),
                    Vector3.zero,
                    palette.Crystal,
                    false);

                CreateCrystalFrame(palette.CrystalEdge);
                CreateElectrodes(palette.Electrode);
                CreateIonLattice(palette.Ion);

                _trajectoryRoot = new GameObject("ElectronTrajectories").transform;
                _trajectoryRoot.SetParent(_root, false);
                _trails = new LineRenderer[HallEffectSimulation.HighlightedParticleCount];
                _trailSerials = new int[HallEffectSimulation.HighlightedParticleCount];
                _trailPoints =
                    new List<Vector3>[HallEffectSimulation.HighlightedParticleCount];
                for (int i = 0; i < _trails.Length; i++)
                {
                    _trailSerials[i] = -1;
                    _trailPoints[i] = new List<Vector3>(TrailPointLimit);
                    GameObject trailObject = new GameObject("TrackedTrajectory_" + (i + 1));
                    trailObject.transform.SetParent(_trajectoryRoot, false);
                    LineRenderer line = trailObject.AddComponent<LineRenderer>();
                    line.sharedMaterial = palette.Trail;
                    line.useWorldSpace = false;
                    line.alignment = LineAlignment.View;
                    line.textureMode = LineTextureMode.Stretch;
                    line.numCapVertices = 6;
                    line.numCornerVertices = 4;
                    line.widthMultiplier = 0.052f;
                    line.positionCount = 0;
                    line.shadowCastingMode = ShadowCastingMode.Off;
                    line.receiveShadows = false;
                    _trails[i] = line;
                }

                _particleTransforms = new Transform[ParticleCount];
                _particleDepths = new float[ParticleCount];
                for (int i = 0; i < ParticleCount; i++)
                {
                    bool highlighted = i < HallEffectSimulation.HighlightedParticleCount;
                    _particleDepths[i] = highlighted
                        ? Mathf.Lerp(
                            -CrystalHalfDepth * 0.42f,
                            CrystalHalfDepth * 0.42f,
                            i / (float)(HallEffectSimulation.HighlightedParticleCount - 1))
                        : Mathf.Lerp(
                            -CrystalHalfDepth * 0.62f,
                            CrystalHalfDepth * 0.62f,
                            Mathf.Repeat((i + 1) * 0.618034f, 1f));
                    _particleTransforms[i] = owner.CreateSphere(
                        _root,
                        (highlighted ? "TrackedElectron_" : "Electron_") + (i + 1),
                        Vector3.zero,
                        highlighted ? 0.30f : 0.135f,
                        highlighted ? palette.Highlight : palette.Electron).transform;
                }

                const int chargeColumns = 9;
                const int chargeLayers = 3;
                _positiveCharges = new Transform[chargeColumns * chargeLayers];
                _negativeCharges = new Transform[chargeColumns * chargeLayers];
                int chargeIndex = 0;
                for (int layer = 0; layer < chargeLayers; layer++)
                {
                    float z = Mathf.Lerp(
                        -CrystalHalfDepth * 0.68f,
                        CrystalHalfDepth * 0.68f,
                        layer / (float)(chargeLayers - 1));
                    for (int column = 0; column < chargeColumns; column++)
                    {
                        float x = Mathf.Lerp(
                            -5.6f,
                            5.6f,
                            column / (float)(chargeColumns - 1));
                        _positiveCharges[chargeIndex] = owner.CreateSphere(
                            _root,
                            "ExposedPositiveCharge_" + chargeIndex,
                            new Vector3(x, CrystalHalfHeight - 0.10f, z),
                            0.17f,
                            palette.PositiveCharge).transform;
                        _negativeCharges[chargeIndex] = owner.CreateSphere(
                            _root,
                            "AccumulatedElectronCharge_" + chargeIndex,
                            new Vector3(x, -CrystalHalfHeight + 0.10f, z),
                            0.17f,
                            palette.NegativeCharge).transform;
                        chargeIndex++;
                    }
                }

                _vectorRoot = new GameObject("FieldVectors").transform;
                _vectorRoot.SetParent(_root, false);

                // Force analysis is shown once in the control panel.

                float[] externalRows = { -1.28f, 0f, 1.28f };
                for (int i = 0; i < externalRows.Length; i++)
                {
                    ArrowVisual externalArrow = owner.CreateArrow(
                        _vectorRoot,
                        "ExternalElectricField_" + (i + 1),
                        palette.ExternalField,
                        0.084f);
                    externalArrow.Set(
                        new Vector3(-5.55f, externalRows[i], CrystalHalfDepth + 0.46f),
                        new Vector3(5.55f, externalRows[i], CrystalHalfDepth + 0.46f));
                }

                ArrowVisual driftArrow = owner.CreateArrow(
                    _vectorRoot,
                    "ElectronDriftVelocity",
                    palette.Drift,
                    0.082f);
                driftArrow.Set(
                    new Vector3(5.30f, -2.03f, CrystalHalfDepth + 0.59f),
                    new Vector3(0.65f, -2.03f, CrystalHalfDepth + 0.59f));

                float[] magneticColumns = { -4.4f, -2.2f, 0f, 2.2f, 4.4f };
                for (int i = 0; i < magneticColumns.Length; i++)
                {
                    ArrowVisual magneticArrow = owner.CreateArrow(
                        _vectorRoot,
                        "MagneticField_" + (i + 1),
                        palette.MagneticField,
                        0.078f);
                    magneticArrow.Set(
                        new Vector3(magneticColumns[i], 0.22f, -1.45f),
                        new Vector3(magneticColumns[i], 0.22f, 1.62f));
                }

                _hallArrow = owner.CreateArrow(
                    _vectorRoot,
                    "HallElectricField",
                    palette.HallField,
                    0.088f);
                _hallArrow.Set(
                    new Vector3(5.55f, 1.8f, CrystalHalfDepth + 0.52f),
                    new Vector3(5.55f, -1.8f, CrystalHalfDepth + 0.52f));

                _lorentzArrow = owner.CreateArrow(
                    _vectorRoot,
                    "LorentzForce",
                    palette.Lorentz,
                    0.090f);
                _lorentzArrow.Set(Vector3.zero, Vector3.down * 0.6f);
            }

            private void CreateForceDirectionPlane(Palette palette)
            {
                if (!_isReferenceModel)
                {
                    return;
                }

                Transform planeRoot = new GameObject("ForceDirectionPlane").transform;
                planeRoot.SetParent(_vectorRoot, false);
                planeRoot.localPosition = new Vector3(0f, 5.55f, 0.62f);

                _owner.CreateBox(
                    planeRoot,
                    "TranslucentForcePlane",
                    new Vector3(13.6f, 5.0f, 0.055f),
                    Vector3.zero,
                    palette.ForcePlane,
                    false);

                const float halfWidth = 6.8f;
                const float halfHeight = 2.5f;
                const float edge = 0.075f;
                _owner.CreateBox(
                    planeRoot,
                    "ForcePlaneTopEdge",
                    new Vector3(halfWidth * 2f + edge, edge, edge),
                    new Vector3(0f, halfHeight, 0f),
                    palette.ForcePlaneEdge,
                    false);
                _owner.CreateBox(
                    planeRoot,
                    "ForcePlaneBottomEdge",
                    new Vector3(halfWidth * 2f + edge, edge, edge),
                    new Vector3(0f, -halfHeight, 0f),
                    palette.ForcePlaneEdge,
                    false);
                _owner.CreateBox(
                    planeRoot,
                    "ForcePlaneLeftEdge",
                    new Vector3(edge, halfHeight * 2f + edge, edge),
                    new Vector3(-halfWidth, 0f, 0f),
                    palette.ForcePlaneEdge,
                    false);
                _owner.CreateBox(
                    planeRoot,
                    "ForcePlaneRightEdge",
                    new Vector3(edge, halfHeight * 2f + edge, edge),
                    new Vector3(halfWidth, 0f, 0f),
                    palette.ForcePlaneEdge,
                    false);
                _owner.CreateBox(
                    planeRoot,
                    "ForcePlaneHorizontalGuide",
                    new Vector3(halfWidth * 2f - 0.16f, 0.022f, 0.025f),
                    Vector3.zero,
                    palette.ForcePlaneEdge,
                    false);
                _owner.CreateBox(
                    planeRoot,
                    "ForcePlaneVerticalGuide",
                    new Vector3(0.022f, halfHeight * 2f - 0.16f, 0.025f),
                    Vector3.zero,
                    palette.ForcePlaneEdge,
                    false);

                const float arrowZ = 0.10f;
                ArrowVisual external = _owner.CreateArrow(
                    planeRoot,
                    "Plane_ExternalElectricField",
                    palette.ExternalField,
                    0.067f);
                external.Set(
                    new Vector3(-5.85f, 0.72f, arrowZ),
                    new Vector3(-1.25f, 0.72f, arrowZ));

                ArrowVisual drift = _owner.CreateArrow(
                    planeRoot,
                    "Plane_ElectronDriftVelocity",
                    palette.Drift,
                    0.067f);
                drift.Set(
                    new Vector3(5.85f, 0.72f, arrowZ),
                    new Vector3(1.25f, 0.72f, arrowZ));

                ArrowVisual hallField = _owner.CreateArrow(
                    planeRoot,
                    "Plane_HallElectricField",
                    palette.HallField,
                    0.072f);
                hallField.Set(
                    new Vector3(-0.98f, 1.92f, arrowZ),
                    new Vector3(-0.98f, -1.30f, arrowZ));

                ArrowVisual lorentzForce = _owner.CreateArrow(
                    planeRoot,
                    "Plane_LorentzForce",
                    palette.Lorentz,
                    0.078f);
                lorentzForce.Set(
                    new Vector3(0.40f, 1.92f, arrowZ),
                    new Vector3(0.40f, -1.68f, arrowZ));

                ArrowVisual hallForce = _owner.CreateArrow(
                    planeRoot,
                    "Plane_HallForce",
                    palette.HallForce,
                    0.078f);
                hallForce.Set(
                    new Vector3(1.78f, -1.68f, arrowZ),
                    new Vector3(1.78f, 1.92f, arrowZ));

                CreateMagneticIntoPlaneSymbol(
                    planeRoot,
                    new Vector3(-3.85f, -0.92f, arrowZ),
                    palette.MagneticField);

                CreateForcePlaneSampleElectrons(
                    planeRoot,
                    palette,
                    new Vector3(4.75f, -1.15f, arrowZ));

                HasForceDirectionPlane = true;
            }

            private void CreateMagneticIntoPlaneSymbol(
                Transform parent,
                Vector3 localPosition,
                Material material)
            {
                const int segmentCount = 24;
                const float radius = 0.62f;
                const float stroke = 0.078f;
                float segmentLength = Mathf.PI * 2f * radius / segmentCount * 1.08f;

                for (int i = 0; i < segmentCount; i++)
                {
                    float angle = i / (float)segmentCount * Mathf.PI * 2f;
                    Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                    Vector3 tangent = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f);
                    _owner.CreatePart(
                        parent,
                        "MagneticIntoPlaneRing_" + i,
                        _owner._cylinderMesh,
                        material,
                        localPosition + radial * radius,
                        Quaternion.FromToRotation(Vector3.forward, tangent),
                        new Vector3(stroke, stroke, segmentLength),
                        false,
                        false);
                }

                Vector3 crossOffset = Vector3.forward * 0.025f;
                _owner.CreatePart(
                    parent,
                    "MagneticIntoPlaneCrossA",
                    _owner._cylinderMesh,
                    material,
                    localPosition + crossOffset,
                    Quaternion.FromToRotation(
                        Vector3.forward,
                        new Vector3(1f, 1f, 0f).normalized),
                    new Vector3(stroke, stroke, radius * 1.38f),
                    false,
                    false);
                _owner.CreatePart(
                    parent,
                    "MagneticIntoPlaneCrossB",
                    _owner._cylinderMesh,
                    material,
                    localPosition + crossOffset,
                    Quaternion.FromToRotation(
                        Vector3.forward,
                        new Vector3(1f, -1f, 0f).normalized),
                    new Vector3(stroke, stroke, radius * 1.38f),
                    false,
                    false);
            }

            private void CreateForcePlaneSampleElectrons(
                Transform parent,
                Palette palette,
                Vector3 center)
            {
                _owner.CreateSphere(
                    parent,
                    "PlaneLegendFreeElectron",
                    center + new Vector3(-0.42f, 0f, 0.08f),
                    0.28f,
                    palette.Electron);
                _owner.CreateSphere(
                    parent,
                    "PlaneLegendTrackedElectron",
                    center + new Vector3(0.42f, 0f, 0.08f),
                    0.34f,
                    palette.Highlight);
            }

            public void Reset(float externalElectricField, float magneticField)
            {
                Simulation.Reset(externalElectricField, magneticField);
                for (int i = 0; i < _trailPoints.Length; i++)
                {
                    _trailPoints[i].Clear();
                    _trailSerials[i] = -1;
                    _trails[i].positionCount = 0;
                }

                _trailAccumulator = 0f;
                UpdateVisuals(0f);
            }

            public void Step(float deltaTime)
            {
                Simulation.Step(deltaTime);
            }

            public void SetTime(float targetTime)
            {
                targetTime = Mathf.Max(0f, targetTime);
                if (targetTime < Simulation.ElapsedModelTime)
                {
                    Reset(Simulation.ExternalElectricFieldMagnitude, Simulation.MagneticFieldMagnitude);
                }
                Simulation.AdvanceTo(targetTime);
                for (int i = 0; i < _trailPoints.Length; i++)
                {
                    _trailPoints[i].Clear();
                    _trailSerials[i] = -1;
                    _trails[i].positionCount = 0;
                }

                UpdateVisuals(0f);
            }

            public void UpdateVisuals(float deltaTime)
            {
                for (int i = 0; i < _particleTransforms.Length; i++)
                {
                    HallEffectSimulation.ParticleSnapshot particle =
                        Simulation.GetParticle(i);
                    _particleTransforms[i].localPosition = new Vector3(
                        particle.Position.x,
                        particle.Position.y,
                        _particleDepths[i]);
                    _particleTransforms[i].gameObject.SetActive(!particle.IsAbsorbed);
                }

                UpdateTrajectories(deltaTime);
                UpdateChargeFaces();
                UpdateVectors();
            }

            public void SetVectorVisibility(bool visible)
            {
                _vectorsVisible = visible;
                _vectorRoot.gameObject.SetActive(visible);
            }

            public void SetTrajectoryVisibility(bool visible)
            {
                _trajectoriesVisible = visible;
                _trajectoryRoot.gameObject.SetActive(visible);
            }

            public void SetIonLatticeVisibility(bool visible)
            {
                _ionLatticeVisible = visible;
                _ionRoot.gameObject.SetActive(visible);
            }

            public void DrawWorldLabels()
            {
                // 计算晶体在屏幕上的包围盒（GUI 坐标，y 向下）。
                float minX = float.MaxValue;
                float maxX = float.MinValue;
                float minY = float.MaxValue;
                float maxY = float.MinValue;
                bool any = false;
                for (int ix = 0; ix < 2; ix++)
                {
                    for (int iy = 0; iy < 2; iy++)
                    {
                        for (int iz = 0; iz < 2; iz++)
                        {
                            Vector3 local = new Vector3(
                                (ix == 0 ? -1f : 1f) * CrystalHalfWidth,
                                (iy == 0 ? -1f : 1f) * CrystalHalfHeight,
                                (iz == 0 ? -1f : 1f) * CrystalHalfDepth);
                            Vector3 screen = _owner._camera.WorldToScreenPoint(_root.TransformPoint(local));
                            if (screen.z <= 0f)
                            {
                                continue;
                            }

                            float gy = Screen.height - screen.y;
                            minX = Mathf.Min(minX, screen.x);
                            maxX = Mathf.Max(maxX, screen.x);
                            minY = Mathf.Min(minY, gy);
                            maxY = Mathf.Max(maxY, gy);
                            any = true;
                        }
                    }
                }

                if (!any)
                {
                    return;
                }

                float centerX = (minX + maxX) * 0.5f;
                float boxWidth = Mathf.Max(240f, maxX - minX);
                float tagHeight = 36f;
                float gap = 8f;
                Color modelColor = _isReferenceModel
                    ? new Color(0.84f, 0.96f, 1f, 1f)
                    : new Color(1f, 0.84f, 0.60f, 1f);

                // 标题：晶体正上方，贴近晶体。
                float titleWidth = Mathf.Min(boxWidth, 520f);
                float titleY = Mathf.Max(minY - gap - tagHeight, HeaderHeight + 4f);
                DrawScreenTag(
                    new Rect(centerX - titleWidth * 0.5f, titleY, titleWidth, tagHeight),
                    _panelTitle,
                    _owner._labelStyle,
                    modelColor);

                // 阶段说明：晶体正下方，贴近晶体。
                float stageWidth = Mathf.Min(boxWidth, 720f);
                float stageHeight = 46f;
                float stageY = Mathf.Min(maxY + gap, _owner.GetAnnotationBottomLimit() - stageHeight);
                stageY = Mathf.Max(stageY, titleY + tagHeight + 4f);
                DrawScreenTag(
                    new Rect(centerX - stageWidth * 0.5f, stageY, stageWidth, stageHeight),
                    GetStageText(),
                    _owner._worldSmallStyle,
                    modelColor);
            }

            private void DrawForcePlaneLabels()
            {
                const float planeZ = 0.96f;
                if (Simulation.MagneticFieldMagnitude <= 0.0001f)
                {
                    _owner.DrawWorldLabel(
                        _root.TransformPoint(new Vector3(0f, 6.10f, planeZ)),
                        "B=0：F洛=0，E霍=0\n横向受力为零",
                        _owner._vectorLabelStyle,
                        440f,
                        52f);
                    return;
                }
                _owner.DrawWorldLabel(
                    _root.TransformPoint(new Vector3(0f, 9.02f, planeZ)),
                    "受力方向平面  |  电子 q = -e  |  稳态 F洛 + F霍 ≈ 0",
                    _owner._vectorLabelStyle,
                    720f,
                    34f);
                _owner.DrawColoredWorldLabel(
                    _root.TransformPoint(new Vector3(-3.55f, 6.92f, planeZ)),
                    "E外 (+X)",
                    _owner._vectorLabelStyle,
                    175f,
                    32f,
                    new Color(1f, 0.66f, 0.18f, 1f));
                _owner.DrawColoredWorldLabel(
                    _root.TransformPoint(new Vector3(3.55f, 6.92f, planeZ)),
                    "v漂 (-X)",
                    _owner._vectorLabelStyle,
                    175f,
                    32f,
                    new Color(0.62f, 1f, 0.34f, 1f));
                _owner.DrawColoredWorldLabel(
                    _root.TransformPoint(new Vector3(-1.05f, 4.62f, planeZ)),
                    "E霍 (-Y)",
                    _owner._vectorLabelStyle,
                    170f,
                    32f,
                    new Color(1f, 0.38f, 0.30f, 1f));
                _owner.DrawColoredWorldLabel(
                    _root.TransformPoint(new Vector3(-4.55f, 3.72f, planeZ)),
                    "F洛 (-Y)",
                    _owner._vectorLabelStyle,
                    170f,
                    32f,
                    new Color(1f, 0.40f, 0.80f, 1f));
                _owner.DrawColoredWorldLabel(
                    _root.TransformPoint(new Vector3(2.60f, 5.52f, planeZ)),
                    "F霍 (+Y)",
                    _owner._vectorLabelStyle,
                    175f,
                    32f,
                    new Color(1f, 0.84f, 0.24f, 1f));
                _owner.DrawColoredWorldLabel(
                    _root.TransformPoint(new Vector3(-4.35f, 5.52f, planeZ)),
                    "B +Z（入屏）",
                    _owner._vectorLabelStyle,
                    160f,
                    48f,
                    new Color(0.36f, 0.76f, 1f, 1f));
                _owner.DrawWorldLabel(
                    _root.TransformPoint(new Vector3(4.60f, 3.65f, planeZ)),
                    "金色：自由电子\n青色：跟踪电子",
                    _owner._worldSmallStyle,
                    210f,
                    48f);
            }

            private void DrawScreenTag(Rect rect, string text, GUIStyle style, Color color)
            {
                Color previous = GUI.color;
                GUI.color = color;
                _owner.DrawLabelFrame(rect, text, style);
                GUI.color = previous;
            }

        private void CreateCrystalFrame(Material material)
            {
                const float edge = 0.065f;
                for (int ySign = -1; ySign <= 1; ySign += 2)
                {
                    for (int zSign = -1; zSign <= 1; zSign += 2)
                    {
                        _owner.CreateBox(
                            _root,
                            "HorizontalFrame_" + ySign + "_" + zSign,
                            new Vector3(CrystalHalfWidth * 2f + edge, edge, edge),
                            new Vector3(
                                0f,
                                CrystalHalfHeight * ySign,
                                CrystalHalfDepth * zSign),
                            material,
                            false);
                    }
                }

                for (int xSign = -1; xSign <= 1; xSign += 2)
                {
                    for (int zSign = -1; zSign <= 1; zSign += 2)
                    {
                        _owner.CreateBox(
                            _root,
                            "VerticalFrame_" + xSign + "_" + zSign,
                            new Vector3(edge, CrystalHalfHeight * 2f + edge, edge),
                            new Vector3(
                                CrystalHalfWidth * xSign,
                                0f,
                                CrystalHalfDepth * zSign),
                            material,
                            false);
                    }
                }

                for (int xSign = -1; xSign <= 1; xSign += 2)
                {
                    for (int ySign = -1; ySign <= 1; ySign += 2)
                    {
                        _owner.CreateBox(
                            _root,
                            "DepthFrame_" + xSign + "_" + ySign,
                            new Vector3(edge, edge, CrystalHalfDepth * 2f + edge),
                            new Vector3(
                                CrystalHalfWidth * xSign,
                                CrystalHalfHeight * ySign,
                                0f),
                            material,
                            false);
                    }
                }
            }

            private void CreateElectrodes(Material material)
            {
                _owner.CreateBox(
                    _root,
                    "LeftCopperElectrode",
                    new Vector3(1.25f, 4.35f, 2.30f),
                    new Vector3(-7.42f, 0f, 0f),
                    material,
                    true);
                _owner.CreateBox(
                    _root,
                    "RightCopperElectrode",
                    new Vector3(1.25f, 4.35f, 2.30f),
                    new Vector3(7.42f, 0f, 0f),
                    material,
                    true);
            }

            private void CreateIonLattice(Material material)
            {
                _ionRoot = new GameObject("PositiveIonLattice3D").transform;
                _ionRoot.SetParent(_root, false);

                for (int layer = 0; layer < 3; layer++)
                {
                    float z = Mathf.Lerp(
                        -CrystalHalfDepth * 0.62f,
                        CrystalHalfDepth * 0.62f,
                        layer / 2f);
                    for (int row = 0; row < 5; row++)
                    {
                        for (int column = 0; column < 13; column++)
                        {
                            float x = -6f + column;
                            float y = -1.8f + row * 0.9f;
                            _owner.CreateSphere(
                                _ionRoot,
                                "Ion_" + layer + "_" + row + "_" + column,
                                new Vector3(x, y, z),
                                0.20f,
                                material);
                        }
                    }
                }
            }

            private void UpdateTrajectories(float deltaTime)
            {
                _trailAccumulator += deltaTime;
                bool addPoint = _trailAccumulator >= 0.022f;
                if (addPoint)
                {
                    _trailAccumulator = 0f;
                }

                for (int i = 0; i < HallEffectSimulation.HighlightedParticleCount; i++)
                {
                    HallEffectSimulation.ParticleSnapshot particle = Simulation.GetParticle(i);
                    if (_trailSerials[i] != particle.WrapSerial || particle.IsAbsorbed)
                    {
                        _trailPoints[i].Clear();
                        _trailSerials[i] = particle.WrapSerial;
                    }

                    if (addPoint)
                    {
                        Vector3 point = new Vector3(
                            particle.Position.x,
                            particle.Position.y,
                            _particleDepths[i]);
                        if (_trailPoints[i].Count == 0 ||
                            Vector3.Distance(
                                _trailPoints[i][_trailPoints[i].Count - 1],
                                point) > 0.024f)
                        {
                            _trailPoints[i].Add(point);
                        }

                        if (_trailPoints[i].Count > TrailPointLimit)
                        {
                            _trailPoints[i].RemoveAt(0);
                        }
                    }

                    _trails[i].positionCount = _trailPoints[i].Count;
                    for (int pointIndex = 0; pointIndex < _trailPoints[i].Count; pointIndex++)
                    {
                        _trails[i].SetPosition(pointIndex, _trailPoints[i][pointIndex]);
                    }
                }
            }

            private void UpdateChargeFaces()
            {
                float target = Mathf.Max(Simulation.TargetSurfaceChargeDensity, 0.01f);
                float normalizedCharge = _isReferenceModel
                    ? Mathf.Clamp01(Simulation.SurfaceChargeDensity / target)
                    : Mathf.Clamp01(
                        Simulation.SurfaceChargeDensity /
                        (Simulation.TargetSurfaceChargeDensity * 2.5f + 0.01f));
                bool showSurfaceCharge =
                    Simulation.HallFieldEnabled &&
                    Simulation.MagneticFieldMagnitude > 0.0001f;
                float visibleScale = showSurfaceCharge
                    ? Mathf.Lerp(0.48f, 1.55f, normalizedCharge)
                    : 0f;

                for (int i = 0; i < _positiveCharges.Length; i++)
                {
                    _positiveCharges[i].localScale = Vector3.one * (0.17f * visibleScale);
                    _negativeCharges[i].localScale = Vector3.one * (0.17f * visibleScale);
                }
            }

            private void UpdateVectors()
            {
                int trackedIndex = Mathf.Min(
                    3,
                    HallEffectSimulation.HighlightedParticleCount - 1);
                HallEffectSimulation.ParticleSnapshot particle =
                    Simulation.GetParticle(trackedIndex);
                Vector2 force = Simulation.GetLorentzForce(trackedIndex);
                float forceMagnitude = force.magnitude;
                Vector3 arrowStart = new Vector3(
                    particle.Position.x + 0.18f,
                    particle.Position.y - 0.04f,
                    _particleDepths[trackedIndex] + 0.24f);

                if (forceMagnitude > 0.001f && !particle.IsAbsorbed)
                {
                    Vector2 direction2D = force / forceMagnitude;
                    float arrowLength = Mathf.Clamp(
                        0.78f + forceMagnitude * 0.24f,
                        0.82f,
                        1.26f);
                    Vector3 direction = new Vector3(direction2D.x, direction2D.y, 0f);
                    Vector3 arrowEnd = arrowStart + direction * arrowLength;
                    _lorentzArrow.Set(arrowStart, arrowEnd);
                    _lorentzLabelPosition =
                        arrowEnd + direction * 0.10f + Vector3.down * 0.25f;
                    _lorentzArrow.SetVisible(_vectorsVisible);
                }
                else
                {
                    _lorentzArrow.SetVisible(false);
                }

                float hallFieldMagnitude = Simulation.HallElectricFieldMagnitude;
                if (hallFieldMagnitude > 0.01f)
                {
                    float halfLength = Mathf.Lerp(
                        0.74f,
                        2.20f,
                        Mathf.Clamp01(hallFieldMagnitude / 1.2f));
                    _hallArrow.Set(
                        new Vector3(
                            5.55f,
                            halfLength,
                            CrystalHalfDepth + 0.52f),
                        new Vector3(
                            5.55f,
                            -halfLength,
                            CrystalHalfDepth + 0.52f));
                    _hallArrow.SetVisible(_vectorsVisible);
                }
                else
                {
                    _hallArrow.SetVisible(false);
                }
            }

            private string GetStageText()
            {
                if (Simulation.MagneticFieldMagnitude <= 0.0001f)
                {
                    return _isReferenceModel
                        ? "B=0：无横向洛伦兹力，E霍=0，未建立霍尔电压"
                        : "对照组 B=0：无磁场，不产生横向洛伦兹力";
                }

                if (!_isReferenceModel)
                {
                    return "对照组关闭 E霍：电子偏向 -Y，约定电流 J_y 指向 +Y";
                }

                switch (Simulation.CurrentStage)
                {
                    case 0:
                        return "① 外电场使电子沿 -X 漂移，初始 F洛 指向 -Y";
                    case 1:
                        return "② 电子在 -Y 面积累，+Y 面暴露正离子";
                    case 2:
                        return "③ 电荷积累建立 E霍，方向 -Y";
                    default:
                        return "④ 稳态：qE霍 抵消 q(v×B)，J_y→0，霍尔电压建立";
                }
            }
        }

        private ArrowVisual CreateArrow(
            Transform parent,
            string name,
            Material material,
            float radius)
        {
            return new ArrowVisual(this, parent, name, material, radius);
        }
    }
}
