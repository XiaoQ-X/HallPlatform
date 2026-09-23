using UnityEngine;

namespace HallLab
{
    // Preserve the line's route for circuit checks, but render a round insulation jacket.
    [ExecuteAlways, RequireComponent(typeof(LineRenderer), typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RoundCable : MonoBehaviour
    {
        private Mesh mesh;
        private Vector3[] previous;
        private Vector3[] buffer;
        private void OnEnable(){previous=null;Refresh();}
        public void Refresh()
        {
            var line=GetComponent<LineRenderer>();
            int count=line.positionCount;
            if(count<2)return;
            if(buffer==null||buffer.Length!=count)buffer=new Vector3[count];
            var points=buffer;line.GetPositions(points);
            bool changed=previous==null || previous.Length!=count;
            if(!changed)for(int i=0;i<count;i++)if(points[i]!=previous[i]){changed=true;break;}
            if(!changed)return;
            if(previous==null||previous.Length!=count)previous=new Vector3[count];
            System.Array.Copy(points,previous,count);
            const int sides=10;
            var vertices=new Vector3[count*sides+2];var normals=new Vector3[vertices.Length];
            var indices=new int[(count-1)*sides*6+sides*6];
            var lengths=new float[count];
            for(int i=1;i<count;i++)lengths[i]=lengths[i-1]+Vector3.Distance(points[i],points[i-1]);
            Vector3 normal=Vector3.up;int k=0;
            for(int i=0;i<count;i++){
                var tangent=(points[Mathf.Min(i+1,count-1)]-points[Mathf.Max(0,i-1)]).normalized;
                if(tangent.sqrMagnitude<.01f)tangent=Vector3.forward;
                normal=Vector3.ProjectOnPlane(normal,tangent).normalized;
                if(normal.sqrMagnitude<.01f)normal=Vector3.Cross(tangent,Vector3.right).normalized;
                if(normal.sqrMagnitude<.01f)normal=Vector3.Cross(tangent,Vector3.forward).normalized;
                var binormal=Vector3.Cross(tangent,normal).normalized;
                float radius=line.widthCurve.Evaluate(lengths[i]/Mathf.Max(.00001f,lengths[count-1]))*line.widthMultiplier*.5f;
                for(int j=0;j<sides;j++){
                    float angle=j*Mathf.PI*2/sides;int v=i*sides+j;
                    var radial=normal*Mathf.Cos(angle)+binormal*Mathf.Sin(angle);
                    vertices[v]=transform.InverseTransformPoint(points[i]+radial*radius);
                    normals[v]=transform.InverseTransformDirection(radial);
                    if(i<count-1){int next=i*sides+(j+1)%sides;
                        indices[k++]=v;indices[k++]=next;indices[k++]=v+sides;
                        indices[k++]=next;indices[k++]=next+sides;indices[k++]=v+sides;
                    }
                }
            }
            int start=count*sides,end=start+1;
            vertices[start]=transform.InverseTransformPoint(points[0]);vertices[end]=transform.InverseTransformPoint(points[count-1]);
            normals[start]=transform.InverseTransformDirection((points[0]-points[1]).normalized);
            normals[end]=transform.InverseTransformDirection((points[count-1]-points[count-2]).normalized);
            for(int j=0;j<sides;j++){
                indices[k++]=start;indices[k++]=(j+1)%sides;indices[k++]=j;
                indices[k++]=end;indices[k++]=(count-1)*sides+j;indices[k++]=(count-1)*sides+(j+1)%sides;
            }
            if(mesh==null){mesh=new Mesh{name="Round cable jacket",hideFlags=HideFlags.DontSave};GetComponent<MeshFilter>().sharedMesh=mesh;}
            mesh.Clear();mesh.vertices=vertices;mesh.normals=normals;mesh.triangles=indices;mesh.RecalculateBounds();
            GetComponent<MeshRenderer>().sharedMaterial=line.sharedMaterial;line.enabled=false;
        }
        private void OnDestroy(){if(mesh!=null){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}}
    }
}
