using UnityEngine;

namespace HallLab
{
    // End transforms belong to real terminals. Moving the sample updates its harness too.
    [RequireComponent(typeof(LineRenderer))]
    public sealed class LeadPath : MonoBehaviour
    {
        public Transform[] anchors;
        public Pin from, to;
        private Vector3[] previousAnchors;
        private void LateUpdate() { Refresh(); }
        public void Refresh()
        {
            if (anchors == null || anchors.Length < 2) return;
            bool changed=previousAnchors==null||previousAnchors.Length!=anchors.Length;
            if(!changed)for(int i=0;i<anchors.Length;i++)if(previousAnchors[i]!=anchors[i].position){changed=true;break;}
            if(!changed)return;
            if(previousAnchors==null||previousAnchors.Length!=anchors.Length)previousAnchors=new Vector3[anchors.Length];
            for(int i=0;i<anchors.Length;i++)previousAnchors[i]=anchors[i].position;
            var line = GetComponent<LineRenderer>();
            const int steps = 24;
            line.positionCount = (anchors.Length - 1) * steps + 1;
            for (int i = 0; i < anchors.Length - 1; i++) {
                Vector3 a = anchors[Mathf.Max(i - 1, 0)].position, b = anchors[i].position;
                Vector3 c = anchors[i + 1].position, d = anchors[Mathf.Min(i + 2, anchors.Length - 1)].position;
                for (int j = 0; j < steps; j++) {
                    float t = j / (float)steps;
                    line.SetPosition(i * steps + j, .5f * ((2*b) + (-a+c)*t + (2*a-5*b+4*c-d)*t*t + (-a+3*b-3*c+d)*t*t*t));
                }
            }
            line.SetPosition(line.positionCount - 1, anchors[anchors.Length - 1].position);
            GetComponent<RoundCable>().Refresh();
        }
        public static LineRenderer Configure(GameObject target, Material material, float width)
        {
            var line = target.AddComponent<LineRenderer>();
            line.sharedMaterial = material; line.useWorldSpace = true;
            line.startWidth = line.endWidth = width; line.numCapVertices = 5; line.numCornerVertices = 4;
            line.generateLightingData = true;
            target.AddComponent<RoundCable>();
            return line;
        }
    }
}
