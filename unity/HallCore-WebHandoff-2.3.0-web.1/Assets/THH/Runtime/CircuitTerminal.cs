using UnityEngine;

namespace HallLab
{
    public sealed class CircuitTerminal : MonoBehaviour
    {
        public Pin pin;
        public Vector3 outward = Vector3.up;
        public Renderer indicator;
        public Vector3 Exit => transform.position + transform.TransformDirection(outward) * .027f;
        public void Highlight(bool selected)
        {
            if (indicator == null) return;
            if (!selected) { indicator.SetPropertyBlock(null); return; }
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", new Color(.15f, .95f, .82f));
            indicator.SetPropertyBlock(properties);
        }
    }
}
