using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HallLab.Editor
{
    public static partial class THHSceneBuilder
    {
        // Visible construction details. Dimensions remain explicitly approximate.
        private static void TesterHardware(Transform unit)
        {
            for(int side=-1;side<=1;side+=2){
                Box("EnclosureJoint",unit,new Vector3(side*.2026f,.073f,0),new Vector3(.0005f,.0012f,.269f),"Iron");
                for(int z=-1;z<=1;z+=2){
                    var head=Cylinder("SideCoverScrew",unit,new Vector3(side*.2033f,.086f,z*.116f),.0023f,.0012f,"MachinedSteel");
                    head.transform.localRotation=Quaternion.Euler(0,0,90);
                }
            }
            // Fine border and functional group dividers follow the existing face layout.
            for(int side=-1;side<=1;side+=2)
                Box("FaceplateRule",unit,new Vector3(0,side>0?.165f:.038f,-.1581f),new Vector3(.365f,.0005f,.0002f),"CaseBlue");
            foreach(float x in new[]{-.099f,.006f,.121f})
                Box("OutputGroupRule",unit,new Vector3(x,.067f,-.1581f),new Vector3(.0004f,.039f,.0002f),"CaseBlue");
            foreach(float x in new[]{-.065f,.068f}){
                for(int side=-1;side<=1;side+=2)
                    Box("DisplayInnerLip",unit,new Vector3(x,.133f+side*.017f,-.1687f),new Vector3(.097f,.0008f,.0005f),"MachinedSteel");
            }
            Box("TrimScrewSlot",unit,new Vector3(-.17f,.126f,-.166f),new Vector3(.007f,.001f,.0004f),"Aluminum");
            Box("SelectorIndex",unit,new Vector3(.164f,.135f,-.170f),new Vector3(.0012f,.005f,.0004f),"White");
        }

        private static void CaseHardware(Transform c,Transform lid)
        {
            for(int side=-1;side<=1;side+=2){
                Box("SideExtrusionGroove",c,new Vector3(side*.252f,.063f,0),new Vector3(.0008f,.002f,.513f),"Iron");
                Box("FrontExtrusionGroove",c,new Vector3(0,.063f,side*.271f),new Vector3(.466f,.002f,.0008f),"Iron");
                for(int n=0;n<5;n++){
                    Screw(c,new Vector3(side*.245f,.0765f,-.228f+n*.114f));
                    Screw(lid,new Vector3(side*.246f,.034f+n*.108f,-.019f),true);
                }
                Box("HingeLeafBase",c,new Vector3(side*.14f,.072f,.248f),new Vector3(.06f,.002f,.024f),"Aluminum");
                Box("HingeLeafLid",lid,new Vector3(side*.14f,.019f,-.014f),new Vector3(.06f,.034f,.002f),"Aluminum");
                for(int i=-1;i<=1;i+=2){
                    Screw(c,new Vector3(side*.14f+i*.020f,.074f,.245f));
                    Screw(lid,new Vector3(side*.14f+i*.020f,.025f,-.016f),true);
                }
                // Folded corner reinforcements and latch hinge pin.
                for(int z=-1;z<=1;z+=2){
                    Box("DeckCornerGusset",c,new Vector3(side*.225f,.073f,z*.248f),new Vector3(.030f,.002f,.016f),"Aluminum");
                    Screw(c,new Vector3(side*.228f,.075f,z*.248f));
                }
                var hinge=Cylinder("LatchPivot",c,new Vector3(side*.151f,.054f,-.282f),.002f,.029f,"MachinedSteel");
                hinge.transform.localRotation=Quaternion.Euler(0,0,90);
                Box("LatchBail",c,new Vector3(side*.151f,.038f,-.282f),new Vector3(.026f,.004f,.003f),"MachinedSteel");
                for(int end=-1;end<=1;end+=2)
                    Box("LatchBailSide",c,new Vector3(side*.151f+end*.012f,.044f,-.282f),new Vector3(.002f,.014f,.003f),"MachinedSteel");
            }
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Box("CaseRubberPad",c,new Vector3(x*.203f,.002f,z*.218f),new Vector3(.035f,.004f,.035f),"Rubber");
        }

        private static void BuildOpenBobbin(Transform magnet)
        {
            for(int side=-1;side<=1;side+=2){
                var flange=Group("OpenBobbinFlange",magnet,new Vector3(0,.189f,side*.052f));
                foreach(int edge in new[]{-1,1}){
                    Box("BobbinSide",flange,new Vector3(edge*.052f,0,0),new Vector3(.019f,.114f,.011f),"Bobbin");
                    Box("BobbinBridge",flange,new Vector3(0,edge*.042f,0),new Vector3(.085f,.030f,.011f),"Bobbin");
                }
            }
            // An open winding volume surrounds the rectangular iron core; no solid end caps cover it.
            var vertices=new List<Vector3>();var indices=new List<int>();
            void Face(Vector3 a,Vector3 b,Vector3 d,Vector3 e){int k=vertices.Count;vertices.AddRange(new[]{a,b,d,e});indices.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});}
            Vector3 Point(int n,bool inner,float z){
                float a=n*Mathf.PI*2/80;float x=Mathf.Cos(a),y=Mathf.Sin(a);
                float r=inner?Mathf.Min(.039f/Mathf.Max(.00001f,Mathf.Abs(x)),.025f/Mathf.Max(.00001f,Mathf.Abs(y))):.054f;
                return new Vector3(x*r,.189f+y*r,z);
            }
            for(int i=0;i<80;i++){
                var a=Point(i,false,-.0465f);var b=Point(i+1,false,-.0465f);
                var c=Point(i,true,-.0465f);var d=Point(i+1,true,-.0465f);var depth=Vector3.forward*.093f;
                Face(a,b,b+depth,a+depth);Face(c,c+depth,d+depth,d);
                Face(a,c,d,b);Face(a+depth,b+depth,d+depth,c+depth);
            }
            var mesh=new Mesh{name="Open winding around core"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            const string path="Assets/THH/Generated/OpenWinding.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved==null){AssetDatabase.CreateAsset(mesh,path);saved=mesh;}else{EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);}
            var winding=Group("RedWindingBody",magnet,Vector3.zero).gameObject;
            winding.AddComponent<MeshFilter>().sharedMesh=saved;winding.AddComponent<MeshRenderer>().sharedMaterial=mats["Coil"];
        }

        private static void MagnetHardware(Transform magnet)
        {
            foreach(int side in new[]{-1,1}){
                Box("MagnetMountEar",magnet,new Vector3(side*.057f,.023f,.050f),new Vector3(.025f,.012f,.035f),"Iron");
                Bolt(magnet,new Vector3(side*.057f,.030f,.050f));
                var terminal=Group("CoilTerminalSupport",magnet,new Vector3(.049f,.118f,side*.048f));
                Box("TerminalBracket",terminal,Vector3.zero,new Vector3(.023f,.050f,.022f),"Bobbin");
            }
            // Fine face finish and the visible assembly seam, without inventing internal laminations.
            Box("PoleFaceLower",magnet,new Vector3(0,.064f+.022f/2+.00015f,-.067f),new Vector3(.065f,.0003f,.028f),"MachinedSteel");
            Box("PoleFaceUpper",magnet,new Vector3(0,.133f-.062f/2-.00015f,-.067f),new Vector3(.065f,.0003f,.028f),"MachinedSteel");
            Box("YokeAssemblySeam",magnet,new Vector3(0,.2132f,.057f),new Vector3(.071f,.0004f,.001f),"Black");
        }

        private static void Bolt(Transform parent,Vector3 p)
        {
            Cylinder("BoltWasher",parent,p,.0045f,.0008f,"Aluminum");
            Cylinder("BoltHead",parent,p+Vector3.up*.0015f,.0032f,.0025f,"MachinedSteel");
            Cylinder("HexRecess",parent,p+Vector3.up*.0028f,.0014f,.0002f,"Black");
        }

        private static Transform Thimble(Transform parent,string name,Vector3 position,bool alongX)
        {
            var root=Group(name,parent,position);
            var axis=Group("ThimbleAxis",root,Vector3.zero);axis.localRotation=alongX?Quaternion.Euler(0,0,90):Quaternion.Euler(90,0,0);
            Cylinder("ThimbleGrip",axis,Vector3.zero,.010f,.021f,"Rubber");
            for(int i=0;i<28;i++){
                float a=i*Mathf.PI*2/28;
                Cylinder("ThimbleFlute",axis,new Vector3(Mathf.Cos(a)*.0097f,0,Mathf.Sin(a)*.0097f),.00055f,.018f,"Black");
            }
            Cylinder("ThimbleEndCap",axis,new Vector3(0,.0107f,0),.008f,.001f,"MachinedSteel");
            return root;
        }

        private static void StageHardware(Transform stage,Transform carriage)
        {
            var mechanics=stage.gameObject.AddComponent<StageMechanics>();
            var saddle=Group("XSlideSaddle",stage,new Vector3(0,.036f,0));mechanics.xSaddle=saddle;
            Box("SaddleBlock",saddle,Vector3.zero,new Vector3(.097f,.007f,.086f),"MachinedSteel");
            for(int side=-1;side<=1;side+=2){
                Box("YSlideGuide",saddle,new Vector3(side*.034f,.005f,0),new Vector3(.006f,.007f,.095f),"Aluminum");
                Box("XGuideKeeper",stage,new Vector3(0,.035f,side*.045f),new Vector3(.14f,.014f,.007f),"Aluminum");
                Bolt(stage,new Vector3(side*.060f,.027f,.046f));
                Bolt(stage,new Vector3(side*.060f,.027f,-.046f));
                Screw(carriage,new Vector3(side*.034f,.0058f,-.029f));
                Screw(carriage,new Vector3(side*.034f,.0058f,.029f));
            }
            Box("XSpindleBearing",stage,new Vector3(-.069f,.034f,0),new Vector3(.011f,.022f,.025f),"Iron");
            Cylinder("XLeadScrew",stage,new Vector3(-.074f,.037f,0),.0026f,.090f,"MachinedSteel").transform.localRotation=Quaternion.Euler(0,0,90);
            Cylinder("XMicrometerSleeve",stage,new Vector3(-.090f,.037f,0),.0057f,.026f,"Aluminum").transform.localRotation=Quaternion.Euler(0,0,90);
            mechanics.xThimble=Thimble(stage,"XAdjustKnob",new Vector3(-.118f,.037f,0),true);
            Box("YSpindleBearing",saddle,new Vector3(0,.007f,-.047f),new Vector3(.025f,.017f,.009f),"Iron");
            Cylinder("YLeadScrew",saddle,new Vector3(0,.007f,-.052f),.0024f,.046f,"MachinedSteel",true);
            mechanics.yThimble=Thimble(saddle,"YAdjustKnob",new Vector3(0,.007f,-.083f),false);
            Cylinder("YSleeve",saddle,new Vector3(0,.007f,-.063f),.005f,.024f,"Aluminum",true);
            // Unnumbered ticks deliberately do not imply a calibrated mm travel.
            for(int i=0;i<9;i++)Box("UncalibratedSleeveTick",stage,new Vector3(-.101f+i*.0025f,.0428f,0),new Vector3(.0003f,.0002f,i%4==0?.006f:.0035f),"Iron");
            Box("ProbeClampCap",carriage,new Vector3(-.024f,.065f,-.039f),new Vector3(.027f,.004f,.036f),"Aluminum");
            Bolt(carriage,new Vector3(-.024f,.068f,-.039f));
            Box("ProbeHarnessClip",carriage,new Vector3(.084f,.044f,-.039f),new Vector3(.008f,.002f,.016f),"Black");
        }

        private static void SwitchHardware(Transform s,Transform pivot)
        {
            foreach(int col in new[]{-1,1}){
                for(int row=-1;row<=1;row++){
                    Vector3 p=new Vector3(col*.022f,.033f,row*.039f);
                    Cylinder("ContactWasher",s,p,.006f,.0007f,"Brass");
                    Box("ContactScrewSlot",s,p+Vector3.up*.0028f,new Vector3(.006f,.0003f,.001f),"Iron");
                    if(row!=0){
                        var lug=Cylinder("WireLugCollar",s,p+new Vector3(col*.003f,-.010f,.003f),.0038f,.002f,"Brass");
                        Box("CrimpBarrel",s,p+new Vector3(col*.003f,-.010f,.009f),new Vector3(.004f,.003f,.009f),"Brass");
                    }
                }
                Cylinder("KnifePivotAxle",pivot,new Vector3(col*.022f,0,0),.0023f,.010f,"Aluminum").transform.localRotation=Quaternion.Euler(0,0,90);
                Bolt(pivot,new Vector3(col*.022f,.0015f,.046f));
            }
        }
    }
}
