using UnityEngine;
using UnityEngine.EventSystems;

namespace HallLab
{
    public sealed class InspectionCamera : MonoBehaviour
    {
        public Vector3 target = new Vector3(-0.06f, 0.20f, 0.03f);
        public float yaw = 10f, pitch = 27f, distance = 1.75f;
        public float compositionShift = .13f;
        private Vector3 dragStart;
        private THHWorkbench workbench;

        public void Preset(int index)
        {
            if (index == 1) { target = new Vector3(-0.36f, .13f, -.13f); yaw = 0; pitch = 8; distance = .83f; }
            else if (index == 2) { target = new Vector3(.29f, .285f, .05f); yaw = 4; pitch = 32; distance = 1.30f; }
            else if (index == 3) { target = new Vector3(.39f, .1648f, .045f); yaw = 55; pitch = 1; distance = .27f; }
            else { target = new Vector3(.015f, .23f, .025f); yaw = 14; pitch = 26; distance = 1.72f; }
            Apply();
        }

        public void Apply()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            transform.SetPositionAndRotation(target + rotation * new Vector3(compositionShift*distance, 0, -distance), rotation);
        }

        private void LateUpdate()
        {
            if(workbench==null)workbench=FindObjectOfType<THHWorkbench>();
            if(workbench!=null&&(workbench.HasOpenDialog||workbench.InstrumentPointerCaptured)){dragStart=Input.mousePosition;return;}
            if(EventSystem.current!=null&&EventSystem.current.currentSelectedGameObject!=null&&EventSystem.current.currentSelectedGameObject.GetComponent<UnityEngine.UI.InputField>()!=null)return;
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) dragStart = Input.mousePosition;
            if (!overUi)
            {
                Vector3 delta = Input.mousePosition - dragStart;
                if (Input.GetMouseButton(1)) { yaw += delta.x * .20f; pitch = Mathf.Clamp(pitch - delta.y * .16f, 1f, 78f); }
                if (Input.GetMouseButton(2)) target -= (transform.right * delta.x + transform.up * delta.y) * distance * .00065f;
            distance = Mathf.Clamp(distance * Mathf.Pow(.90f, Input.mouseScrollDelta.y), .18f, 2.8f);
            }
            dragStart = Input.mousePosition;
            if (Input.GetKeyDown(KeyCode.Home)) Preset(0);
            Apply();
        }
    }
}
