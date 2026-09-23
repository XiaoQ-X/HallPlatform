using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HallLab.Editor
{
    public static partial class THHSceneBuilder
    {
        private const string ScenePath = "Assets/THH/Scenes/THH_ReferenceWorkbench.unity";
        private static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        private static int meshId;
        private static THHWorkbench app;
        private static readonly Dictionary<Pin, Transform> pins = new Dictionary<Pin, Transform>();

        [MenuItem("TH-H/重建结构核对场景")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/THH/Scenes");
            Directory.CreateDirectory("Assets/THH/Generated");
            Directory.CreateDirectory("预览");
            mats.Clear(); meshId = 0; pins.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Material("Shell", new Color(.72f,.76f,.76f), .24f,.42f);
            Material("Face", new Color(.84f,.87f,.84f),.12f,.42f);
            Material("Aluminum", new Color(.62f,.68f,.69f), .8f,.55f);
            Material("CaseBlue", new Color(.13f,.27f,.36f),.25f,.32f);
            Material("Deck", new Color(.67f,.72f,.70f),.35f,.32f);
            Material("Iron",new Color(.18f,.21f,.235f),.38f,.40f);
            Material("Black",new Color(.035f,.043f,.044f),.05f,.25f);
            Material("Brass",new Color(.62f,.47f,.23f),.75f,.45f);
            Material("Bobbin",new Color(.77f,.75f,.51f),.08f,.3f);
            Material("Coil",new Color(.47f,.064f,.08f),.42f,.38f);
            Material("CoilHighlight",new Color(.70f,.17f,.17f),.48f,.45f);
            Material("Red",new Color(.64f,.06f,.055f),.05f,.42f);
            Material("Yellow",new Color(.81f,.64f,.13f),.05f,.38f);
            Material("Orange",new Color(.83f,.30f,.10f),.05f,.34f);
            Material("White",new Color(.85f,.87f,.82f),.02f,.3f);
            Material("Green",new Color(.10f,.23f,.16f),.05f,.3f);
            Material("Sample",new Color(.17f,.44f,.48f),.22f,.55f);
            Material("SampleCarrier",new Color(.78f,.83f,.78f),.18f,.42f);
            Material("Display",new Color(.017f,.048f,.031f),.1f,.68f);
            Material("Bench",new Color(.16f,.195f,.22f),.08f,.30f);
            Material("Floor",new Color(.17f,.22f,.23f),.02f,.25f);
            Material("MachinedSteel",new Color(.39f,.43f,.45f),.72f,.48f);
            Material("Rubber",new Color(.025f,.030f,.031f),.02f,.18f);
            PrepareLabelMaterial();
            var root = new GameObject("TH-H Reference Workbench");
            app=root.AddComponent<THHWorkbench>();
            BuildEnvironment();
            BuildTester(root.transform);
            BuildCase(root.transform);
            BuildExternalLeads(root.transform);
            var camera = new GameObject("Main Camera",typeof(Camera),typeof(InspectionCamera));
            camera.tag="MainCamera"; app.view=camera.GetComponent<Camera>(); app.orbit=camera.GetComponent<InspectionCamera>();
            app.view.fieldOfView=43; app.view.nearClipPlane=.025f; app.view.farClipPlane=20;
            app.view.clearFlags=CameraClearFlags.SolidColor; app.view.backgroundColor=new Color(.11f,.14f,.18f);
            app.view.allowHDR=true; app.view.allowMSAA=true; app.orbit.Preset(0);
            QualitySettings.antiAliasing=4; QualitySettings.shadowDistance=8; QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=ShadowResolution.High; QualitySettings.pixelLightCount=4;
            PlayerSettings.companyName="Hall Effect Lab"; PlayerSettings.productName="霍尔效应教学仿真";
            PlayerSettings.defaultScreenWidth=1440; PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.runInBackground=true;
            TeachingRelease.ApplySceneLabels();
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            Validate();
            Capture();
            VerifyInteractions();
            ValidateDeviceDetails();
            CaptureDeviceDetails();
            Debug.Log("THH_BUILD_SUCCESS " + ScenePath);
        }

        private static Material Material(string name,Color color,float metal,float gloss)
        {
            string path="Assets/THH/Generated/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
            m.color=color; m.SetFloat("_Metallic",metal);m.SetFloat("_Glossiness",gloss);
            mats[name]=m;return m;
        }

        private static Transform Group(string name,Transform parent,Vector3 position)
        { var go=new GameObject(name); go.transform.SetParent(parent,false);go.transform.localPosition=position;return go.transform; }
        private static GameObject Shape(string name,PrimitiveType primitive,Transform parent,Vector3 p,Vector3 size,string material)
        {
            var go=GameObject.CreatePrimitive(primitive);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=p;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=mats[material];return go;
        }
        private static GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,string material)
        {
            var go=Shape(name,PrimitiveType.Cube,parent,p,size,material);
            if(HasMachinedEdges(name)) {
                go.GetComponent<MeshFilter>().sharedMesh=RoundedBoxMesh(name,size);
                go.transform.localScale=Vector3.one;
                go.GetComponent<BoxCollider>().size=size;
            }
            return go;
        }
        private static GameObject Cylinder(string name,Transform parent,Vector3 p,float radius,float height,string material, bool front=false)
        {
            var go=Shape(name,PrimitiveType.Cylinder,parent,p,new Vector3(radius*2,height*.5f,radius*2),material);
            if(front)go.transform.localRotation=Quaternion.Euler(90,0,0);return go;
        }
        private static ReferencePart Part(Transform target,PartKind kind,string title,string description,string evidence)
        {
            var p=target.gameObject.AddComponent<ReferencePart>();p.kind=kind;p.title=title;p.description=description;p.evidence=evidence;return p;
        }
        private static TextMesh Label(string name,Transform parent,string text,Vector3 position,float size,bool top=false,Color? color=null)
        {
            var t=Group(name,parent,position);if(top)t.localRotation=Quaternion.Euler(90,0,0);
            var mesh=t.gameObject.AddComponent<TextMesh>();mesh.text=text;mesh.fontSize=64;mesh.characterSize=size;
            mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.GetComponent<MeshRenderer>().sharedMaterial=mats["WorldLabel"];
            t.gameObject.AddComponent<DepthLabel>();
            mesh.characterSize=size*.27f;
            mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.color=color??new Color(.12f,.17f,.17f);
            return mesh;
        }
        private static void Screw(Transform parent,Vector3 p,bool front=false)
        {
            Cylinder("Fastener",parent,p,.0023f,.0014f,"Aluminum",front);
            var slot=Box("ScrewSlot",parent,p+(front?Vector3.back:Vector3.up)*.0008f,
                front?new Vector3(.0026f,.00045f,.0002f):new Vector3(.0026f,.0002f,.00045f),"Iron");
        }
        private static void BuildEnvironment()
        {
            Box("Worktop",null,new Vector3(0,-.035f,0),new Vector3(1.80f,.06f,1.05f),"Bench");
            Box("WorktopEdge",null,new Vector3(0,-.057f,0),new Vector3(1.81f,.016f,1.06f),"Iron");
            Box("Floor",null,new Vector3(0,-.84f,0),new Vector3(40,.04f,40),"Floor");
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Box("BenchLeg",null,new Vector3(x*.73f,-.45f,z*.38f),new Vector3(.04f,.8f,.04f),"Iron");
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.61f,.68f,.74f);
            RenderSettings.ambientEquatorColor=new Color(.40f,.47f,.52f);
            RenderSettings.ambientGroundColor=new Color(.23f,.27f,.30f);
            var light=new GameObject("Large soft key",typeof(Light));var key=light.GetComponent<Light>();
            key.type=LightType.Directional;key.intensity=.78f;key.color=new Color(1,.98f,.92f);key.shadows=LightShadows.Soft;
            key.shadowBias=.035f;key.shadowNormalBias=.015f;light.transform.rotation=Quaternion.Euler(48,-32,0);
            var fill=new GameObject("Cool fill",typeof(Light));fill.GetComponent<Light>().type=LightType.Directional;
            fill.GetComponent<Light>().intensity=.52f;fill.GetComponent<Light>().color=new Color(.72f,.86f,1);
            fill.transform.rotation=Quaternion.Euler(24,140,0);
            var rim=new GameObject("Rear edge light",typeof(Light));
            rim.GetComponent<Light>().type=LightType.Directional;rim.GetComponent<Light>().intensity=.45f;
            rim.GetComponent<Light>().color=new Color(.76f,.86f,1);rim.transform.rotation=Quaternion.Euler(34,195,0);
        }

        private static void BuildTester(Transform root)
        {
            var unit=Group("THH_TestInstrument",root,new Vector3(-.355f,0,-.035f));
            Part(unit,PartKind.Tester,"TH-H 测试仪","独立机箱容纳工作恒流源、励磁恒流源和毫伏表。\n\n六个端子连接实验箱的三组公共端。电流屏显示软件设定；电压显示 N/A 表示尚未求解。",
                "天煌官网产品全景为外观主依据。\n说明书只核对功能；机箱尺寸、部分面板铭文与背部布局未核实。");
            Box("Enclosure",unit,new Vector3(0,.112f,0),new Vector3(.405f,.174f,.285f),"Shell");
            Box("PanelGasket",unit,new Vector3(0,.112f,-.144f),new Vector3(.405f,.173f,.009f),"Black");
            Box("FrontBezel",unit,new Vector3(0,.112f,-.146f),new Vector3(.401f,.167f,.016f),"Aluminum");
            Box("PrintedFrontPanel",unit,new Vector3(0,.113f,-.155f),new Vector3(.384f,.151f,.005f),"Face");
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                Box("RubberFoot",unit,new Vector3(x*.160f,.014f,z*.103f),new Vector3(.042f,.025f,.042f),"Black");
            Label("Title",unit,"TH-H    HALL EFFECT TESTER",new Vector3(0,.177f,-.159f),.0082f);
            Display(unit,new Vector3(-.065f,.133f,-.163f),out app.voltageDisplay,"VH / mV");
            Display(unit,new Vector3(.068f,.133f,-.163f),out app.currentDisplay,"IS / mA   |   IM / A");
            // Functional approximation from the product photo, not a traced exact faceplate.
            float[] portX={-.170f,-.126f,-.067f,-.025f,.142f,.181f};
            for(int i=0;i<6;i++) {
                Socket(unit,new Vector3(portX[i],.073f,-.166f),true,i%2==0?"Red":"Black",(Pin)i);
                Label("Polarity",unit,i%2==0?"+":"-",new Vector3(portX[i],.091f,-.159f),.006f);
            }
            Label("IsPort",unit,"IS OUT",new Vector3(-.148f,.048f,-.159f),.0057f);
            Label("VoltagePort",unit,"VH / Vsig IN",new Vector3(-.046f,.048f,-.159f),.0057f);
            Label("ImPort",unit,"IM OUT",new Vector3(.162f,.048f,-.159f),.0057f);
            Knob(unit,new Vector3(.035f,.070f,-.177f),"IS");Knob(unit,new Vector3(.091f,.070f,-.177f),"IM");
            Cylinder("ZeroTrim",unit,new Vector3(-.17f,.126f,-.163f),.006f,.005f,"Black",true);
            Label("ZeroLegend",unit,"ZERO",new Vector3(-.17f,.147f,-.16f),.005f);
            Cylinder("CurrentSelector",unit,new Vector3(.164f,.13f,-.165f),.010f,.009f,"Red",true);
            Label("SelectorLegend",unit,"IS / IM",new Vector3(.164f,.15f,-.16f),.0048f);
            Cylinder("RangeSelector",unit,new Vector3(0,.134f,-.163f),.006f,.006f,"Black",true);
            for(int side=-1;side<=1;side+=2)
                for(int i=0;i<11;i++)Box("SideVent_Approximate",unit,new Vector3(side*.203f,.145f,.068f-i*.011f),new Vector3(.001f,.038f,.003f),"Iron");
            foreach(var p in new[]{new Vector3(-.181f,.18f,-.16f),new Vector3(.181f,.18f,-.16f),new Vector3(-.181f,.042f,-.16f),new Vector3(.181f,.042f,-.16f)})Screw(unit,p,true);
            TesterHardware(unit);
        }
        private static void Display(Transform unit,Vector3 center,out TextMesh readout,string caption)
        {
            Box("DisplayBezel",unit,center,new Vector3(.110f,.046f,.009f),"Black");
            Box("DisplayGlass",unit,center+Vector3.back*.005f,new Vector3(.095f,.033f,.001f),"Display");
            readout=Label("Readout",unit,"----",center+Vector3.back*.006f,.025f,false,new Color(.38f,.70f,.39f));
            readout.fontStyle=FontStyle.Bold;
            Label("DisplayCaption",unit,caption,center+new Vector3(0,-.029f,-.005f),.0045f);
        }
        private static void Knob(Transform unit,Vector3 p,string caption)
        {
            Cylinder("KnobCollar",unit,p+Vector3.forward*.009f,.018f,.005f,"Aluminum",true);
            Cylinder("AdjustKnob",unit,p,.014f,.02f,"Shell",true);
            Cylinder("KnobFaceInset",unit,p+Vector3.back*.0104f,.0117f,.0010f,"Aluminum",true);
            for(int rib=0;rib<32;rib++){
                float angle=rib*Mathf.PI/16;
                Cylinder("KnurledGrip",unit,p+new Vector3(Mathf.Cos(angle)*.0136f,Mathf.Sin(angle)*.0136f,0),.00065f,.016f,"Shell",true);
            }
            Box("KnobIndex",unit,p+new Vector3(0,.006f,-.0112f),new Vector3(.0012f,.01f,.0005f),"Black");
            // TeachingInteraction adds bounded setpoint labels above the knobs
            // and range labels below them, also when loading older saved scenes.
            for(int i=0;i<11;i++){
                float a=(i*25-125)*Mathf.Deg2Rad;
                Box("DialTick",unit,p+new Vector3(Mathf.Sin(a)*.022f,Mathf.Cos(a)*.022f,.014f),new Vector3(.001f,.002f,.001f),"Iron");
            }
        }
        private static void Socket(Transform parent,Vector3 p,bool front,string color,Pin pin)
        {
            var socket=Group("Terminal_"+pin,parent,p);
            var insulator=Cylinder("SocketInsulator",socket,Vector3.zero,.009f,.008f,color,front);
            var direction=front?Vector3.back:Vector3.up;
            Cylinder("SocketMountWasher",socket,-direction*.0048f,.0108f,.0014f,"Aluminum",front);
            Cylinder("InsulatorShoulder",socket,direction*.003f,.0065f,.003f,color,front);
            Cylinder("SocketMetalRim",socket,direction*.005f,.0045f,.002f,"Brass",front);
            Cylinder("SocketHole",socket,direction*.0061f,.0027f,.001f,"Black",front);
            var terminal=socket.gameObject.AddComponent<CircuitTerminal>();terminal.pin=pin;terminal.outward=direction;
            terminal.indicator=insulator.GetComponent<Renderer>();
            pins[pin]=socket;
        }

        private static void BuildCase(Transform root)
        {
            var c=Group("THH_ExperimentCase",root,new Vector3(.29f,0,.02f));
            Part(c,PartKind.Case,"箱式实验装置","电磁铁与样品架位于箱体后部，三个开关并排位于前部。\n\n该版优先还原部件关系；独立测试仪放在左侧便于查看与连线。",
                "主依据：天煌产品全景。\n高校实拍仅作结构辅助，型号/批次未明确。箱体与台面尺寸均为近似。");
            Box("CaseBottom",c,new Vector3(0,.032f,0),new Vector3(.50f,.060f,.54f),"CaseBlue");
            Box("MountingDeck",c,new Vector3(0,.066f,0),new Vector3(.475f,.009f,.513f),"Deck");
            for(int side=-1;side<=1;side+=2){
                Box("SideRim",c,new Vector3(side*.246f,.069f,0),new Vector3(.011f,.014f,.54f),"Aluminum");
                Box("CrossRim",c,new Vector3(0,.069f,side*.265f),new Vector3(.49f,.014f,.011f),"Aluminum");
                for(int z=-1;z<=1;z+=2)Box("CornerProtector",c,new Vector3(side*.241f,.035f,z*.257f),new Vector3(.025f,.056f,.026f),"Aluminum");
                Box("FrontLatch",c,new Vector3(side*.151f,.046f,-.276f),new Vector3(.032f,.019f,.008f),"Aluminum");
                Box("LatchCatch",c,new Vector3(side*.151f,.048f,-.281f),new Vector3(.016f,.011f,.005f),"Iron");
                Cylinder("Hinge",c,new Vector3(side*.14f,.073f,.266f),.007f,.06f,"Aluminum").transform.localRotation=Quaternion.Euler(0,0,90);
            }
            Box("CarryHandle",c,new Vector3(0,.032f,-.307f),new Vector3(.12f,.016f,.013f),"Rubber");
            for(int side=-1;side<=1;side+=2)Box("HandleReturn",c,new Vector3(side*.062f,.032f,-.290f),new Vector3(.012f,.016f,.038f),"Black");
            for(int side=-1;side<=1;side+=2)Box("HandleBracket",c,new Vector3(side*.072f,.032f,-.28f),new Vector3(.015f,.025f,.018f),"Aluminum");
            var lid=Group("OpenLid",c,new Vector3(0,.074f,.267f));lid.localRotation=Quaternion.Euler(8,0,0);
            Box("LidShell",lid,new Vector3(0,.256f,.009f),new Vector3(.50f,.515f,.028f),"CaseBlue");
            Box("LidLining",lid,new Vector3(0,.256f,-.008f),new Vector3(.472f,.486f,.007f),"Face");
            foreach(int side in new[]{-1,1}){
                Box("LidSideRail",lid,new Vector3(side*.246f,.256f,-.012f),new Vector3(.010f,.515f,.012f),"Aluminum");
                Box("LidEdgeRail",lid,new Vector3(0,side>0?.507f:.007f,-.012f),new Vector3(.49f,.013f,.012f),"Aluminum");
                Box("LidCatch",lid,new Vector3(side*.15f,.515f,.003f),new Vector3(.031f,.015f,.020f),"Aluminum");
            }
            Box("LidReferencePlate",lid,new Vector3(0,.31f,-.014f),new Vector3(.108f,.145f,.001f),"Black");
            Label("PlateHeading",lid,"TH-H",new Vector3(0,.364f,-.016f),.009f,false,Color.white);
            Label("PlateNote",lid,"IS   VH   IM\nCONNECTION\nREFERENCE",new Vector3(0,.306f,-.016f),.0042f,false,new Color(.75f,.80f,.74f));
            // Plate intentionally does not invent an unreadable manufacturer circuit decal.
            CaseHardware(c,lid);
            BuildMagnet(c);
            BuildStage(c);
            app.switchBlades=new Transform[3];
            for(int i=0;i<3;i++)BuildSwitch(c,i,new Vector3((i-1)*.149f,.073f,-.15f));
            FixedLeads(c);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)Screw(c,new Vector3(x*.226f,.072f,z*.236f));
        }

        private static void BuildMagnet(Transform c)
        {
            var magnet=Group("Electromagnet",c,new Vector3(.10f,.077f,.092f));
            Part(magnet,PartKind.Magnet,"电磁铁与励磁线圈","位于实验箱右后侧，与左侧样品架对接。外观采用深色铁轭、红色线圈及浅色线圈骨架。\n\n样品的观察位置位于磁极间。",
                "颜色和大体位置参考产品照与高校实拍。\n铁轭内部路径、极靴形状和气隙尺寸未获得近照，当前采用显式近似结构。");
            Box("MagnetFoot",magnet,new Vector3(0,.008f,0),new Vector3(.145f,.016f,.19f),"Iron");
            // Approximate C-yoke, single winding: preserve observed silhouette, do not claim a measured magnetic circuit.
            Box("RearYoke_Approximate",magnet,new Vector3(0,.102f,.057f),new Vector3(.08f,.195f,.038f),"Iron");
            Box("LowerReturn_Approximate",magnet,new Vector3(0,.033f,-.009f),new Vector3(.08f,.042f,.13f),"Iron");
            Box("TopCore_Approximate",magnet,new Vector3(0,.189f,0),new Vector3(.075f,.048f,.16f),"Iron");
            // Keep the pole faces broad enough to carry the yoke, but reduce their
            // front-to-back depth so the small specimen remains visible in the air
            // gap instead of disappearing behind a solid black block in the close-up.
            Box("UpperPole_Approximate",magnet,new Vector3(0,.133f,-.067f),new Vector3(.073f,.062f,.028f),"Iron");
            Box("LowerPole_Approximate",magnet,new Vector3(0,.064f,-.067f),new Vector3(.073f,.022f,.028f),"Iron");
            BuildOpenBobbin(magnet);
            var points=new List<Vector3>();
            for(int i=0;i<=2880;i++){float t=i/2880f;float a=t*36*Mathf.PI*2;points.Add(new Vector3(Mathf.Cos(a)*.0545f,.189f+Mathf.Sin(a)*.0545f,Mathf.Lerp(-.046f,.046f,t)));}
            Tube("WindingDetail",magnet,points,.00065f,"CoilHighlight",8);
            Socket(magnet,new Vector3(.049f,.143f,-.048f),false,"Red",Pin.CoilStart);
            Socket(magnet,new Vector3(.049f,.143f,.048f),false,"Black",Pin.CoilEnd);
            Label("CoilLeadMark",magnet,"* START",new Vector3(.054f,.155f,-.050f),.0036f,true);
            for(int side=-1;side<=1;side+=2)Screw(magnet,new Vector3(side*.059f,.017f,-.066f));
            Label("MagnetCaption",c,"ELECTROMAGNET",new Vector3(.1f,.073f,-.033f),.0055f,true);
            MagnetHardware(magnet);
        }

        private static void BuildStage(Transform c)
        {
            var stage=Group("XY_SampleStage",c,new Vector3(-.123f,.076f,.064f));
            Part(stage,PartKind.Stage,"二维样品架","左侧机构承载探杆，将小样品送入右侧磁极间。\n\n使用右侧两个滑条检查移动与装配关系；行程为归一化参数，不代表实机毫米数。",
                "实物照片提供位置与外形线索；说明书确认二维调节功能。\n机构内部构造、精确行程与刻度待核实。");
            Box("StageBase",stage,new Vector3(0,.013f,0),new Vector3(.145f,.026f,.12f),"Aluminum");
            Box("LowerSlide",stage,new Vector3(0,.030f,0),new Vector3(.118f,.01f,.088f),"Iron");
            Box("FixedTerminalBank",stage,new Vector3(-.013f,.065f,.066f),new Vector3(.065f,.009f,.012f),"Black");
            Box("TerminalBankBracket",stage,new Vector3(-.013f,.010f,.064f),new Vector3(.072f,.010f,.035f),"Aluminum");
            foreach(float x in new[]{-.036f,.017f})Box("TerminalBankPost",stage,new Vector3(x,.038f,.066f),new Vector3(.008f,.045f,.008f),"Black");
            for(int side=-1;side<=1;side+=2)Box("SlideRail",stage,new Vector3(0,.035f,side*.034f),new Vector3(.142f,.008f,.006f),"MachinedSteel");
            var carriage=Group("MovingCarriage",stage,new Vector3(0,.046f,0));app.sampleCarriage=carriage;
            Box("CarriagePlate",carriage,Vector3.zero,new Vector3(.09f,.010f,.080f),"Deck");
            Box("ProbeHolder",carriage,new Vector3(-.024f,.033f,-.039f),new Vector3(.022f,.060f,.032f),"Iron");
            Box("ProbeSupportRail",carriage,new Vector3(.089f,.036f,-.039f),new Vector3(.226f,.005f,.012f),"Aluminum");
            Box("ProbePCB",carriage,new Vector3(.182f,.040f,-.039f),new Vector3(.062f,.003f,.016f),"Green");
            // Align the specimen with the centre of the approximate pole footprint at the home pose.
            var sample=Group("HallSample",carriage,new Vector3(.223f,.041f,-.039f));
            Part(sample,PartKind.Sample,"霍尔样品","位于探杆末端的小样品，不是横跨磁极的大型金属片。\n\n物理几何与画面表现分离；近景使用托片帮助辨认位置。",
                "TH-H 说明书给出硅样品及电极示意。\n透明封装和引脚外观缺微距；当前托片为占位，未宣称精确封装。");
            Box("SampleCarrier_Approximate",sample,Vector3.zero,new Vector3(.025f,.003f,.016f),"SampleCarrier");
            Box("SiliconReference_0p5mm",sample,new Vector3(0,.0018f,0),new Vector3(.006f,.0005f,.004f),"Sample");
            // The .006 outer length above is a visual placeholder, NOT the 3 mm voltage-contact spacing.
            // Specimen (a): A/A' on one transverse section, C' 3 mm along the same upper side.
            Vector3[] electrodes={new Vector3(-.003f,.0022f,0),new Vector3(.003f,.0022f,0),
                new Vector3(-.0015f,.0022f,-.002f),new Vector3(-.0015f,.0022f,.002f),new Vector3(.0015f,.0022f,.002f)};
            string[] electrodeNames={"D","E","A","A'","C'"};
            string[] electrodeColors={"Red","Black","Orange","Yellow","White"};
            for(int i=0;i<5;i++) {
                Pin pin=(Pin)(24+i);var electrode=Group("Electrode_"+electrodeNames[i],sample,electrodes[i]);pins[pin]=electrode;
                Box("ElectrodePad",electrode,Vector3.zero,new Vector3(.0006f,.00018f,.0006f),"Brass");
                var contact=electrode.gameObject.AddComponent<CircuitTerminal>();contact.pin=pin;
                Label("ElectrodeName",sample,electrodeNames[i],new Vector3(-.009f+i*.0044f,.0023f,-.0055f),.0018f,true);
                Cylinder("HarnessBank_"+electrodeNames[i],stage,new Vector3(-.036f+i*.011f,.073f,.066f),.0025f,.005f,electrodeColors[i]);
            }
            Box("C_NC_Unwired",sample,new Vector3(.0015f,.0022f,-.002f),new Vector3(.0006f,.00018f,.0006f),"Brass");
            Label("NCLabel",sample,"C:NC",new Vector3(.007f,.0023f,.0045f),.0018f,true);
            StageHardware(stage,carriage);
            Label("StageCaption",c,"X / Y   SAMPLE STAGE",new Vector3(-.131f,.073f,-.030f),.005f,true);
        }

        private static void BuildSwitch(Transform c,int index,Vector3 position)
        {
            var s=Group(new[]{"Switch_IS","Switch_VH_Vsigma","Switch_IM"}[index],c,position);
            Part(s,new[]{PartKind.IsSwitch,PartKind.VoltageSwitch,PartKind.ImSwitch}[index],
                new[]{"工作电流换向开关","电压测量选择开关","励磁电流换向开关"}[index],
                new[]{"双刀双掷连接样品 D/E，改变工作电流方向。\n\n学生导线连接中间公共端；上下两排为厂家固定线。","上接测量 A−A′（VH）；下接测量 A′−C′（Vσ）。\n\n这是测量电极选择，不是电压极性换向。","双刀双掷改变线圈电流方向。\n\n正向仅表示从线圈起点到终点，不等同于已标定的磁场正方向。"}[index],
                "接线依据：TH-H 说明书接线图及样品（a）。\n触点尺寸近似；中位作为软件断开状态，不认定实机有中位定位档。");
            Box("BakeliteBase",s,new Vector3(0,.009f,0),new Vector3(.089f,.018f,.110f),"Black");
            for(int row=-1;row<=1;row++)for(int col=-1;col<=1;col+=2){
                Vector3 p=new Vector3(col*.022f,.024f,row*.039f);
                Cylinder("TerminalStud",s,p,.004f,.018f,"Brass");
                Cylinder("TerminalCap",s,p+Vector3.up*.010f,.0055f,.003f,"Aluminum");
                if(row!=0) {
                    for(int jaw=-1;jaw<=1;jaw+=2)Box("ContactJaw",s,p+new Vector3(jaw*.0038f,.012f,0),new Vector3(.002f,.014f,.011f),"Brass");
                    Pin pin=(Pin)(12+index*4+(row>0?0:2)+(col>0?1:0));
                    var anchor=Group("FixedContact_"+pin,s,p+new Vector3(col*.003f,0,.009f));pins[pin]=anchor;
                    var fixedPin=anchor.gameObject.AddComponent<CircuitTerminal>();fixedPin.pin=pin;
                    var hit=anchor.gameObject.AddComponent<SphereCollider>();hit.radius=.007f;
                } else {
                    Box("CommonBridge",s,new Vector3(col*.031f,.024f,0),new Vector3(.025f,.003f,.008f),"Brass");
                    Socket(s,new Vector3(col*.043f,.028f,0),false,col<0?"Red":"Black",(Pin)(6+index*2+(col>0?1:0)));
                }
            }
            var pivot=Group("BladePivot",s,new Vector3(0,.038f,0)); app.switchBlades[index]=pivot;
            for(int col=-1;col<=1;col+=2)Box("KnifeBlade",pivot,new Vector3(col*.022f,0,.022f),new Vector3(.005f,.0025f,.053f),"Brass");
            Box("InsulatedCrossbar",pivot,new Vector3(0,0,.046f),new Vector3(.070f,.010f,.012f),"Black");
            Cylinder("SwitchHandle",pivot,new Vector3(0,0,.053f),.0075f,.052f,"Black").transform.localRotation=Quaternion.Euler(0,0,90);
            Label("SwitchCaption",c,new[]{"IS INPUT","VH / Vsig OUTPUT","IM INPUT"}[index],position+new Vector3(0,.001f,-.073f),.0051f,true);
            Label("CommonMark",s,"+ COM -",new Vector3(0,.0195f,-.013f),.0043f,true,Color.white);
            Label("UpperMark",s,index==1?"VH":"+",new Vector3(0,.0195f,.030f),.005f,true,Color.white);
            Label("LowerMark",s,index==1?"Vsig":"-",new Vector3(0,.0195f,-.036f),.0046f,true,Color.white);
            for(int col=-1;col<=1;col+=2)Screw(s,new Vector3(col*.036f,.019f,-.045f));
            SwitchHardware(s,pivot);
        }

        private static void FixedLeads(Transform c)
        {
            var fixedRoot=Group("FactoryFixedLeads",c,Vector3.zero);
            string[] colors={"Red","Black","Orange","Yellow","White"};
            foreach(var edge in THHCircuit.FactoryEdges) {
                var path=new List<Transform>{pins[edge.A]};string color="Black";
                if((int)edge.B>=24 && (int)edge.B<=28) {
                    int i=(int)edge.B-24;color=colors[i];
                    // The fixed harness runs around the rear of the XY stage.  The
                    // earlier front loop crossed both handwheels and the probe
                    // holder, making the mechanism hard to inspect.
                    path.Add(Group("HarnessTurn",fixedRoot,new Vector3(pins[edge.A].position.x-c.position.x,.119f,.105f)));
                    path.Add(Group("HarnessLoop",fixedRoot,new Vector3(-.214f+i*.004f,.142f,.115f)));
                    path.Add(Group("HarnessRearTurn",fixedRoot,new Vector3(-.202f+i*.008f,.143f,.130f+i*.008f)));
                    path.Add(Group("FixedBank",fixedRoot,new Vector3(-.159f+i*.011f,.150f,.130f)));
                    path.Add(Group("MovingHarness",app.sampleCarriage,new Vector3(.005f,.048f,.055f+i*.0025f)));
                    path.Add(Group("ClampBypass",app.sampleCarriage,new Vector3(.026f,.044f,-.044f+i*.0025f)));
                    path.Add(Group("ProbeTrace",app.sampleCarriage,new Vector3(.170f,.044f,-.044f+i*.0025f)));
                    // Fan out from the PCB edge; the final lead is a fine bond wire at the pad.
                    Vector3 pad=app.sampleCarriage.InverseTransformPoint(pins[edge.B].position);
                    path.Add(Group("FineLeadFanout",app.sampleCarriage,new Vector3(.209f,pad.y+.0008f,-.044f+i*.0025f)));
                    path.Add(Group("ElectrodeApproach",app.sampleCarriage,pad+new Vector3(-.0005f,.0003f,0)));
                } else if((int)edge.B>=29) {
                    int i=(int)edge.B-29;color=i==0?"Red":"Black";
                    path.Add(Group("CoilTurn",fixedRoot,new Vector3(.207f+i*.011f,.108f,-.069f)));
                    path.Add(Group("CoilRise",fixedRoot,new Vector3(.222f+i*.008f,.190f,.12f+i*.035f)));
                } else {
                    int index=((int)edge.A-12)/4;int side=((int)edge.A%2==0?-1:1);color=side<0?"Black":"Red";
                    if(index==1)color="Yellow";
                    float x=(index-1)*.149f;
                    path.Add(Group("CrossoverSide",fixedRoot,new Vector3(x+side*.054f,.099f,-.205f)));
                    path.Add(Group("CrossoverBack",fixedRoot,new Vector3(x-side*.054f,.092f,-.223f)));
                    path.Add(Group("CrossoverRise",fixedRoot,new Vector3(x-side*.055f,.106f,-.095f)));
                }
                path.Add(pins[edge.B]);
                var wire=Group("Fixed_"+edge.A+"_"+edge.B,fixedRoot,Vector3.zero).gameObject;
                var jacket=LeadPath.Configure(wire,mats[color],.0016f);
                if((int)edge.B>=24 && (int)edge.B<=28){
                    jacket.widthMultiplier=1;
                    jacket.widthCurve=new AnimationCurve(new Keyframe(0,.0016f),new Keyframe(.68f,.0016f),new Keyframe(.88f,.0006f),new Keyframe(.97f,.00016f),new Keyframe(1,.00016f));
                }
                var lead=wire.AddComponent<LeadPath>();lead.from=edge.A;lead.to=edge.B;lead.anchors=path.ToArray();lead.Refresh();
            }
        }
        private static void BuildExternalLeads(Transform root)
        {
            app.externalLeads=Group("StudentExternalLeads",root,Vector3.zero).gameObject;
            app.positiveLead=mats["Red"];app.negativeLead=mats["Black"];
        }
        private static void Curve(string name,Transform parent,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float radius,string material)
        {
            var points=new List<Vector3>();for(int i=0;i<=42;i++){float t=i/42f,u=1-t;points.Add(u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d);}
            Tube(name,parent,points,radius,material,8);
        }
        private static void Tube(string name,Transform parent,IList<Vector3> path,float radius,string material,int sides)
        {
            int n=path.Count;var verts=new Vector3[n*sides];var normals=new Vector3[n*sides];var uv=new Vector2[n*sides];
            var tris=new int[(n-1)*sides*6];
            Vector3 previous=Vector3.up;
            for(int i=0;i<n;i++){
                Vector3 tangent=(path[Mathf.Min(i+1,n-1)]-path[Mathf.Max(i-1,0)]).normalized;
                Vector3 normal=Vector3.ProjectOnPlane(previous,tangent).normalized;
                if(normal.sqrMagnitude<.1f)normal=Vector3.Cross(tangent,Vector3.right).normalized;
                Vector3 binormal=Vector3.Cross(tangent,normal).normalized;previous=normal;
                for(int j=0;j<sides;j++){
                    float angle=2*Mathf.PI*j/sides;Vector3 radial=normal*Mathf.Cos(angle)+binormal*Mathf.Sin(angle);
                    int v=i*sides+j;verts[v]=path[i]+radial*radius;normals[v]=radial;uv[v]=new Vector2(j/(float)sides,i/(float)(n-1));
                    if(i==n-1)continue;int k=(i*sides+j)*6,next=i*sides+(j+1)%sides;
                    tris[k]=v;tris[k+1]=next;tris[k+2]=v+sides;tris[k+3]=next;tris[k+4]=next+sides;tris[k+5]=v+sides;
                }
            }
            var mesh=new Mesh{name=name};mesh.vertices=verts;mesh.normals=normals;mesh.uv=uv;mesh.triangles=tris;mesh.RecalculateBounds();
            string assetPath="Assets/THH/Generated/Tube_"+(meshId++).ToString("D3")+".asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if(existing!=null){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,assetPath);
            var go=Group(name,parent,Vector3.zero).gameObject;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mats[material];
        }

        [MenuItem("TH-H/验证场景结构")]
        public static void Validate()
        {
            Directory.CreateDirectory("预览");
            var workbench=UnityEngine.Object.FindObjectOfType<THHWorkbench>();
            if(workbench==null)throw new Exception("Missing THHWorkbench");
            if(workbench.switchBlades.Length!=3)throw new Exception("Expected exactly three switches");
            var nodes=Array.FindAll(workbench.GetComponentsInChildren<CircuitTerminal>(),t=>!t.transform.IsChildOf(workbench.externalLeads.transform));
            if(nodes.Length!=31)throw new Exception("Expected 31 electrical nodes, excluding detachable plugs");
            if(workbench.GetComponentsInChildren<LeadPath>().Length!=THHCircuit.FactoryEdges.Count)throw new Exception("Missing fixed graph edges");
            if(workbench.sampleCarriage==null||workbench.view==null||workbench.orbit==null)throw new Exception("Missing references");
            foreach(var part in workbench.GetComponentsInChildren<ReferencePart>())
                if(string.IsNullOrWhiteSpace(part.evidence))throw new Exception("Part has no evidence boundary: "+part.name);
            foreach(var filter in UnityEngine.Object.FindObjectsOfType<MeshFilter>())
                if(filter.sharedMesh==null)throw new Exception("Missing saved mesh: "+filter.name);
            File.WriteAllText("预览/场景验证.txt","PASS\nThree switches; 31 electrical nodes, including 12 editable sockets; fixed wire paths correspond to circuit edges; evidence notes and camera/stage references valid.\nThis checks software structure only, not physical fidelity.\n");
            File.WriteAllText("预览/接线逻辑验证.txt",CircuitChecks.Run());
            Debug.Log("THH_VALIDATION_PASS");
        }

        [MenuItem("TH-H/导出参考视角预览")]
        public static void Capture()
        {
            app=UnityEngine.Object.FindObjectOfType<THHWorkbench>();
            app.InitializePreview();
            app.StandardWiring();
            app.view.aspect=1440f/900;
            string[] names={"01-全景","02-测试仪面板","03-实验箱","04-样品与磁隙"};
            for(int i=0;i<4;i++){
                app.FocusView(i);Canvas.ForceUpdateCanvases();
                var rt=new RenderTexture(1440,900,24,RenderTextureFormat.ARGB32){antiAliasing=4};rt.Create();
                app.view.targetTexture=rt;
                Canvas.ForceUpdateCanvases();app.view.Render();
                var old=RenderTexture.active;RenderTexture.active=rt;
                var tex=new Texture2D(1440,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1440,900),0,0);tex.Apply();
                File.WriteAllBytes("预览/"+names[i]+".png",tex.EncodeToPNG());
                RenderTexture.active=old;app.view.targetTexture=null;rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
            }
            app.FocusView(0);Debug.Log("THH_CAPTURE_SUCCESS");
        }

        [MenuItem("TH-H/验证结构交互")]
        public static void VerifyInteractions()
        {
            app=UnityEngine.Object.FindObjectOfType<THHWorkbench>();
            File.WriteAllText("预览/交互验证.txt",WorkbenchChecks.Run(app)+"Editor invocation checks; not a manual Play-mode acceptance test.\n");
            Debug.Log("THH_INTERACTION_PASS");
        }
    }
}
