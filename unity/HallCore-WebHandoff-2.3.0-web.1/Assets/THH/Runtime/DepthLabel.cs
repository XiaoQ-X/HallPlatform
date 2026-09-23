using UnityEngine;

namespace HallLab
{
    // TextMesh's default material draws through solids. Use depth testing while retaining the dynamic font atlas.
    [ExecuteAlways, RequireComponent(typeof(TextMesh))]
    public sealed class DepthLabel : MonoBehaviour
    {
        private TextMesh label;
        private Renderer surface;
        private MaterialPropertyBlock properties;
        private void OnEnable(){label=GetComponent<TextMesh>();surface=GetComponent<Renderer>();}
        private void OnWillRenderObject()
        {
            if(label==null || label.font==null || surface==null)return;
            if(properties==null)properties=new MaterialPropertyBlock();
            properties.SetTexture("_MainTex",label.font.material.mainTexture);
            surface.SetPropertyBlock(properties);
        }
    }
}
