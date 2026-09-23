using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HallLab.Editor
{
    public static partial class THHSceneBuilder
    {
        private static readonly HashSet<string> machined = new HashSet<string>{
            "Enclosure","FrontBezel","PrintedFrontPanel","PanelGasket","DisplayBezel",
            "CaseBottom","MountingDeck","SideRim","CrossRim","CornerProtector","FrontLatch","LatchCatch",
            "CarryHandle","HandleBracket","LidShell","LidSideRail","LidEdgeRail",
            "MagnetFoot","RearYoke_Approximate","LowerReturn_Approximate","TopCore_Approximate",
            "UpperPole_Approximate","LowerPole_Approximate","CoilBobbinFlange","StageBase","LowerSlide",
            "CarriagePlate","ProbeHolder","BakeliteBase","InsulatedCrossbar",
            "BobbinSide","BobbinBridge","HandleReturn","SaddleBlock","YSlideGuide","XGuideKeeper","ProbeClampCap"
        };
        private static bool HasMachinedEdges(string name) => machined.Contains(name);
        private static void PrepareLabelMaterial()
        {
            const string path="Assets/THH/Generated/WorldLabel.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find("HallLab/Depth Text");
            if(shader==null)throw new System.Exception("Missing depth-correct label shader");
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
            material.shader=shader;mats["WorldLabel"]=material;
        }
        // The bounding dimensions remain unchanged. Edge radii are visual approximations, not measured specifications.
        private static Mesh RoundedBoxMesh(string name,Vector3 size)
        {
            float radius=Mathf.Min(.003f,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.22f);
            Vector3 half=size*.5f,core=half-Vector3.one*radius;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            Vector3[] axes={Vector3.right,Vector3.up,Vector3.forward};
            float AxisSize(Vector3 a)=>Mathf.Abs(Vector3.Dot(half,a));
            float[] Grid(float h)=>new[]{-h,-h+radius*.2929f,-h+radius,0,h-radius,h-radius*.2929f,h};
            for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2){
                Vector3 n=axes[axis]*sign,u=axes[(axis+1)%3],v=axes[(axis+2)%3]*sign;
                var xs=Grid(AxisSize(u));var ys=Grid(AxisSize(v));int start=vertices.Count;
                for(int y=0;y<7;y++)for(int x=0;x<7;x++){
                    Vector3 p=n*AxisSize(n)+u*xs[x]+v*ys[y];
                    Vector3 q=new Vector3(Mathf.Clamp(p.x,-core.x,core.x),Mathf.Clamp(p.y,-core.y,core.y),Mathf.Clamp(p.z,-core.z,core.z));
                    Vector3 normal=(p-q).normalized;
                    vertices.Add(q+normal*radius);normals.Add(normal);uv.Add(new Vector2(x/6f,y/6f));
                    if(x<6&&y<6){
                        int k=start+y*7+x;
                        triangles.AddRange(new[]{k,k+1,k+7,k+1,k+8,k+7});
                    }
                }
            }
            var mesh=new Mesh{name=name+"_Machined"};mesh.SetVertices(vertices);mesh.SetNormals(normals);
            mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
            string path="Assets/THH/Generated/Detail_"+name+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);return existing;}
            AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
    }
}
