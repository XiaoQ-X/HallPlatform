using UnityEngine;

namespace HallLab
{
    public enum PartKind { Tester, Magnet, Stage, Sample, IsSwitch, VoltageSwitch, ImSwitch, Case }

    public sealed class ReferencePart : MonoBehaviour
    {
        public PartKind kind;
        public string title;
        [TextArea(3, 8)] public string description;
        [TextArea(2, 6)] public string evidence;
    }
}
