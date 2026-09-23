using UnityEngine;

namespace HallEffectLab
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(50)]
    public sealed class HallEffectOrbitCamera : MonoBehaviour
    {
        [SerializeField] private Vector3 _defaultFocus = Vector3.zero;
        [SerializeField] private float _defaultDistance = 27f;
        [SerializeField] private float _defaultYaw = 0f;
        [SerializeField] private float _defaultPitch = 18f;
        [SerializeField] private float _moveSpeed = 8f;
        [SerializeField] private float _rotateSpeed = 0.22f;
        [SerializeField] private float _panSpeed = 0.0022f;
        [SerializeField] private float _zoomSpeed = 3.2f;
        [SerializeField] private float _minimumDistance = 8f;
        [SerializeField] private float _maximumDistance = 48f;

        private Vector3 _focus;
        private float _distance;
        private float _yaw;
        private float _pitch;
        private Vector3 _lastMousePosition;
        private bool _hasMousePosition;

        [SerializeField] private float _touchZoomSpeed = 0.02f;
        private readonly Vector2[] _lastTouches = new Vector2[2];
        private bool _hasTouches;

        public bool PointerOverGui { get; set; }
        public Vector3 FocusPoint => _focus;
        public float Distance => _distance;

        private void Awake()
        {
            ResetView();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (Input.touchCount > 0)
            {
                HandleTouchInput();
            }
            else
            {
                HandlePointerInput();
            }
            HandleKeyboardInput();
            ApplyNow();
        }

        public void ConfigureView(Vector3 focus, float distance, float yaw, float pitch)
        {
            _focus = focus;
            _distance = Mathf.Clamp(distance, _minimumDistance, _maximumDistance);
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, -80f, 80f);
            ApplyNow();
        }

        public void ResetView()
        {
            ConfigureView(_defaultFocus, _defaultDistance, _defaultYaw, _defaultPitch);
        }

        public void FocusOn(Vector3 focus)
        {
            _focus = focus;
            ApplyNow();
        }

        public void ApplyNow()
        {
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            transform.rotation = rotation;
            transform.position = _focus - rotation * Vector3.forward * _distance;
        }

        private void HandlePointerInput()
        {
            Vector3 mousePosition = Input.mousePosition;
            if (!_hasMousePosition)
            {
                _lastMousePosition = mousePosition;
                _hasMousePosition = true;
                return;
            }

            Vector3 mouseDelta = mousePosition - _lastMousePosition;
            _lastMousePosition = mousePosition;

            if (PointerOverGui)
            {
                return;
            }

            if (Input.GetMouseButton(1))
            {
                _yaw += mouseDelta.x * _rotateSpeed;
                _pitch = Mathf.Clamp(
                    _pitch - mouseDelta.y * _rotateSpeed,
                    -80f,
                    80f);
            }

            if (Input.GetMouseButton(2))
            {
                Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                Vector3 right = rotation * Vector3.right;
                Vector3 up = rotation * Vector3.up;
                float scale = _distance * _panSpeed;
                _focus -= (right * mouseDelta.x + up * mouseDelta.y) * scale;
            }

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _distance = Mathf.Clamp(
                    _distance - scroll * _zoomSpeed,
                    _minimumDistance,
                    _maximumDistance);
            }
        }

        private void HandleTouchInput()
        {
            int touchCount = Input.touchCount;
            if (touchCount == 0)
            {
                _hasTouches = false;
                return;
            }

            if (PointerOverGui)
            {
                if (touchCount >= 1) { _lastTouches[0] = Input.GetTouch(0).position; }
                if (touchCount >= 2) { _lastTouches[1] = Input.GetTouch(1).position; }
                _hasTouches = true;
                return;
            }

            if (touchCount == 1)
            {
                Vector2 current = Input.GetTouch(0).position;
                if (_hasTouches)
                {
                    Vector2 delta = current - _lastTouches[0];
                    _yaw += delta.x * _rotateSpeed;
                    _pitch = Mathf.Clamp(_pitch - delta.y * _rotateSpeed, -80f, 80f);
                }
                _lastTouches[0] = current;
            }
            else
            {
                Vector2 t0 = Input.GetTouch(0).position;
                Vector2 t1 = Input.GetTouch(1).position;
                if (_hasTouches)
                {
                    float previousPinch = Vector2.Distance(_lastTouches[0], _lastTouches[1]);
                    float currentPinch = Vector2.Distance(t0, t1);
                    float pinchDelta = currentPinch - previousPinch;
                    _distance = Mathf.Clamp(
                        _distance - pinchDelta * _touchZoomSpeed,
                        _minimumDistance,
                        _maximumDistance);

                    Vector2 previousMid = (_lastTouches[0] + _lastTouches[1]) * 0.5f;
                    Vector2 currentMid = (t0 + t1) * 0.5f;
                    Vector2 midDelta = currentMid - previousMid;
                    Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
                    Vector3 right = rotation * Vector3.right;
                    Vector3 up = rotation * Vector3.up;
                    float scale = _distance * _panSpeed;
                    _focus -= (right * midDelta.x + up * midDelta.y) * scale;
                }
                _lastTouches[0] = t0;
                _lastTouches[1] = t1;
            }

            _hasTouches = true;
        }

        private void HandleKeyboardInput()
        {
            if (PointerOverGui)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetView();
                return;
            }

            if (Input.GetKeyDown(KeyCode.F))
            {
                FocusOn(_defaultFocus);
            }

            float horizontal = 0f;
            float vertical = 0f;
            float depth = 0f;
            if (Input.GetKey(KeyCode.A))
            {
                horizontal -= 1f;
            }

            if (Input.GetKey(KeyCode.D))
            {
                horizontal += 1f;
            }

            if (Input.GetKey(KeyCode.Q))
            {
                vertical -= 1f;
            }

            if (Input.GetKey(KeyCode.E))
            {
                vertical += 1f;
            }

            if (Input.GetKey(KeyCode.S))
            {
                depth -= 1f;
            }

            if (Input.GetKey(KeyCode.W))
            {
                depth += 1f;
            }

            if (Mathf.Abs(horizontal) + Mathf.Abs(vertical) + Mathf.Abs(depth) < 0.001f)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 movement =
                rotation * Vector3.right * horizontal +
                Vector3.up * vertical +
                rotation * Vector3.forward * depth;
            float speedMultiplier = Input.GetKey(KeyCode.LeftShift) ||
                                    Input.GetKey(KeyCode.RightShift)
                ? 2.5f
                : 1f;
            _focus += movement.normalized * (_moveSpeed * speedMultiplier * Time.unscaledDeltaTime);
        }
    }
}
