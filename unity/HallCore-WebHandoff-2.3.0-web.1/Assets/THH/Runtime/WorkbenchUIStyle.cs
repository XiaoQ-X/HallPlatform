using System.Collections.Generic;
using Nobi.UiRoundedCorners;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;

namespace HallLab
{
    public sealed class WorkbenchUIRoot : MonoBehaviour { public THHWorkbench owner; }

    public sealed class UIHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public string message;
        private GameObject bubble;
        public void OnPointerEnter(PointerEventData data) { Show(); }
        public void OnSelect(BaseEventData data) { Show(); }
        public void OnPointerExit(PointerEventData data) { Hide(); }
        public void OnDeselect(BaseEventData data) { Hide(); }
        private void OnDisable() { Hide(); }
        private void Hide() { if(bubble!=null)Destroy(bubble); }
        private void Show()
        {
            Hide();var canvas=GetComponentInParent<Canvas>();if(canvas==null)return;
            bubble=new GameObject("Tooltip",typeof(RectTransform),typeof(Image));bubble.transform.SetParent(canvas.transform,false);
            var rect=bubble.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(240,38);
            rect.position=transform.position;
            var point=rect.anchoredPosition;var bounds=((RectTransform)canvas.transform).rect;
            point.x=Mathf.Clamp(point.x,bounds.xMin+126,bounds.xMax-126);
            point.y=Mathf.Clamp(point.y-48,bounds.yMin+24,bounds.yMax-24);rect.anchoredPosition=point;
            bubble.GetComponent<Image>().color=new Color(.12f,.16f,.18f,.97f);bubble.GetComponent<Image>().raycastTarget=false;
            var textObject=new GameObject("Text",typeof(RectTransform),typeof(Text));textObject.transform.SetParent(bubble.transform,false);
            var tr=textObject.GetComponent<RectTransform>();tr.anchorMin=Vector2.zero;tr.anchorMax=Vector2.one;tr.offsetMin=Vector2.zero;tr.offsetMax=Vector2.zero;
            var text=textObject.GetComponent<Text>();text.font=GetComponentInChildren<Text>().font;text.fontSize=13;text.text=message;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;
        }
    }

    public sealed class UIChoiceStyle : MonoBehaviour
    {
        public Color idle, selected, idleText, selectedText;
        public Image icon;
        public GameObject underline;
        public void Apply(Text label,bool on)
        {
            GetComponent<Image>().color=on?selected:idle;label.color=on?selectedText:idleText;
            if(icon!=null)icon.color=label.color;if(underline!=null)underline.SetActive(on);
        }
    }

    public sealed partial class THHWorkbench
    {
        private GameObject uiRoot;
        private readonly Dictionary<string,Sprite> iconSprites=new Dictionary<string,Sprite>();
        private readonly UICircle[] samplingRings=new UICircle[4];
        private Text samplingCount;
        private Image toggleTrack;
        private RectTransform toggleThumb;
        private RectTransform recoveryDialog,quitDialog,folderDialog;
        private InputField folderInput;
        private Text folderError,quitError;
        private string recoveryCandidate;
        private Text recoveryDescription;
        private bool allowQuit;
        public bool HasOpenDialog => (folderDialog!=null&&folderDialog.gameObject.activeSelf)||
            (recoveryDialog!=null&&recoveryDialog.gameObject.activeSelf)||(quitDialog!=null&&quitDialog.gameObject.activeSelf)||
            (resetDialog!=null&&resetDialog.gameObject.activeSelf);
        public void HandleEscape()
        {
            foreach(var dialog in new[]{folderDialog,quitDialog,resetDialog,recoveryDialog})
                if(dialog!=null&&dialog.gameObject.activeSelf){dialog.gameObject.SetActive(false);return;}
            if(SelectedPin.HasValue)CancelConnection();
        }


        private void Round(RectTransform rect,float radius=6)
        {var shape=rect.gameObject.AddComponent<ImageWithRoundedCorners>();shape.radius=radius;shape.Validate();shape.Refresh();}
        private Image Icon(Transform parent,string key,float x,float top,float size,Color color)
        {
            if(!iconSprites.TryGetValue(key,out var sprite)) {
                var texture=Resources.Load<Texture2D>("Icons/"+key);
                if(texture==null)throw new System.InvalidOperationException("Missing UI icon: "+key);
                sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100);iconSprites[key]=sprite;
            }
            var rt=Rect("Icon "+key,parent,new Vector2(0,1),new Vector2(0,1),new Vector2(x,-top-size),new Vector2(x+size,-top));
            var image=rt.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=color;image.raycastTarget=false;return image;
        }
        private UICircle Circle(Transform parent,string name,float x,float top,float size,Color color,bool fill=true,float thickness=2)
        {
            var rt=Rect(name,parent,new Vector2(0,1),new Vector2(0,1),new Vector2(x,-top-size),new Vector2(x+size,-top));
            var circle=rt.gameObject.AddComponent<UICircle>();circle.color=color;circle.Fill=fill;circle.Thickness=thickness;circle.ArcSteps=64;circle.raycastTarget=false;return circle;
        }
        private Text IconButton(Transform parent,string name,string icon,float x,float top,UnityEngine.Events.UnityAction action,bool accent=false,float size=36)
        {
            var text=Button(parent,name,x,top,size,action,accent,size);text.text="";
            Icon(text.transform.parent,icon,(size-18)/2,(size-18)/2,18,accent?Color.white:Muted);
            text.transform.parent.gameObject.AddComponent<UIHint>().message=name;return text;
        }
        private Text TextAction(Transform parent,string title,string icon,float x,float top,float width,UnityEngine.Events.UnityAction action,bool primary=false)
        {
            var text=Button(parent,title,x,top,width,action,primary,40);text.alignment=TextAnchor.MiddleLeft;
            text.rectTransform.offsetMin=new Vector2(40,text.rectTransform.offsetMin.y);
            Icon(text.transform.parent,icon,12,11,18,primary?Color.white:Muted);return text;
        }
        private Text Choice(Transform parent,string title,float x,float top,float width,UnityEngine.Events.UnityAction action,string icon=null,bool underline=false)
        {
            var text=Button(parent,title,x,top,width,action,false,38);
            var style=text.transform.parent.gameObject.AddComponent<UIChoiceStyle>();
            style.idle=Color.clear;style.selected=underline?Color.clear:new Color(.88f,.95f,.93f);style.idleText=Muted;style.selectedText=AccentColor;
            if(icon!=null){style.icon=Icon(text.transform.parent,icon,12,10,18,Muted);text.rectTransform.offsetMin=new Vector2(38,-38);text.alignment=TextAnchor.MiddleLeft;}
            if(underline)style.underline=Panel("Active indicator",text.transform.parent,new Vector2(0,0),new Vector2(1,0),new Vector2(10,0),new Vector2(-10,2),AccentColor).gameObject;
            style.Apply(text,false);return text;
        }
        private RectTransform Modal(Transform parent,string name,string title,string body,float height=260)
        {
            var backdrop=Panel(name,parent,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,new Color(.04f,.08f,.09f,.45f));
            var box=Panel("Dialog",backdrop,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(-290,-height/2),new Vector2(290,height/2),Color.white);Round(box,8);
            Label(box,title,22,Ink,28,24,524,36).fontStyle=FontStyle.Bold;
            Label(box,body,14,Muted,28,74,524,62);backdrop.gameObject.SetActive(false);return backdrop;
        }
        private void BuildStorageDialogs(Transform root)
        {
            folderDialog=Modal(root,"Save as folder","另存实验数据","目标目录",286);
            var box=folderDialog.GetChild(0);
            var fieldBox=Panel("Folder input",box,new Vector2(0,1),new Vector2(0,1),new Vector2(28,-156),new Vector2(552,-112),PanelAlt);Round(fieldBox);
            folderInput=fieldBox.gameObject.AddComponent<InputField>();var label=Label(fieldBox,"",14,Ink,12,4,500,36);folderInput.textComponent=label;folderInput.targetGraphic=fieldBox.GetComponent<Image>();
            folderError=Label(box,"",12,WarningColor,28,162,524,45);
            TextAction(box,"保存到此目录","save",350,220,202,()=>{
                bool saved=ExperimentModel.SaveAs(folderInput.text);
                if(saved){folderDialog.gameObject.SetActive(false);Refresh("已另存实验数据");if(quitDialog.gameObject.activeSelf)CompleteQuit();}
                else folderError.text=ExperimentModel.LastStorageError;
            },true);
            Button(box,"取消",232,220,106,()=>folderDialog.gameObject.SetActive(false),false,40);
            recoveryDialog=Modal(root,"Recovery","发现未导出的实验数据","上次保存未完成。恢复后可继续查看和导出数据。",238);
            box=recoveryDialog.GetChild(0);
            recoveryDescription=box.GetComponentsInChildren<Text>()[1];
            Button(box,"稍后处理",248,164,132,()=>recoveryDialog.gameObject.SetActive(false),false,40);
            TextAction(box,"恢复数据","undo-2",394,164,158,()=>{
                bool restored=ExperimentModel.Restore(recoveryCandidate);
                if(restored)recoveryDialog.gameObject.SetActive(false);else recoveryDescription.text=ExperimentModel.LastStorageError;
                Refresh(restored?"实验数据已恢复，请保存 CSV":ExperimentModel.LastStorageError);if(restored){viewSweepIs=true;if(!dataVisible)ToggleDataPanel();}
            },true);
            quitDialog=Modal(root,"Unsaved exit","实验数据尚未保存","请重试保存或另存到可写目录。",304);
            box=quitDialog.GetChild(0);quitError=Label(box,"",13,WarningColor,28,126,524,54);
            Button(box,"返回实验",28,232,118,()=>quitDialog.gameObject.SetActive(false),false,40);
            Button(box,"保留恢复后退出",154,232,146,()=>{if(ExperimentModel.PersistRecovery())CompleteQuit();else quitError.text=ExperimentModel.LastStorageError;},false,40);
            Button(box,"另存",308,232,90,ShowSaveAs,false,40);
            TextAction(box,"保存并退出","save",406,232,146,()=>{if(ExperimentModel.SaveData())CompleteQuit();else quitError.text=ExperimentModel.LastStorageError;},true);
        }
        public void ShowSaveAs()
        {
            if(RejectAutomaticEdit())return;
            folderInput.SetTextWithoutNotify(ExperimentModel.StorageFolder??ExperimentLogger.DefaultFolder);folderError.text="";
            folderDialog.SetAsLastSibling();folderDialog.gameObject.SetActive(true);folderInput.Select();
        }
        private void InitializeRecovery()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return;
#else
            if(!Application.isPlaying||ValidationLaunch||System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-hall-teaching-check")>=0)return;
            ShowRecoveryBrowser(false);
            Application.wantsToQuit+=CanQuit;
            ExperimentModel.TrackOperation("会话","启动 "+Application.version);
            ExperimentModel.SaveData();
#endif
        }
        public void ShowRecoveryBrowser(){ShowRecoveryBrowser(true);}
        private void ShowRecoveryBrowser(bool explicitRequest)
        {
            if(LessonActive){Refresh("请先返回学生实验再恢复数据");return;}
            var files=ExperimentRecovery.Find(ExperimentModel.RecoveryFolder);
            int invalid=0;recoveryCandidate=null;
            foreach(var file in files) {
                if(file==ExperimentModel.RecoveryPath)continue;
                if(ExperimentRecovery.Read(file,out _,out _)){recoveryCandidate=file;break;}
                invalid++;
            }
            if(recoveryCandidate==null){if(explicitRequest)Refresh(invalid>0?"恢复文件损坏，已保留原文件；没有可恢复的有效数据":"没有其他会话待恢复的数据");return;}
            recoveryDescription.text="恢复文件："+System.IO.Path.GetFileName(recoveryCandidate)+"\n已跳过 "+invalid+" 个损坏文件；保存后可再次打开此入口恢复其他文件。";
            recoveryDialog.SetAsLastSibling();recoveryDialog.gameObject.SetActive(true);
        }
        private bool CanQuit()
        {
            if(LessonActive)EndLesson();
            if(allowQuit)return true;
            if(automaticExperimentRunning)StopAutomaticExperiment("退出前已停止自动实验，数据保留");
            if(!ExperimentModel.HasUnsavedRows && ExperimentModel.MeasurementCount==0)return true;
            quitDialog.gameObject.SetActive(true);quitError.text=ExperimentModel.MeasurementCount>0?"本组四方向读数未完成，退出将清除本组。":"未保存数据仍保留在当前会话。";
            return false;
        }
        public void RequestQuit() {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Leaving the WebGL experiment is a navigation action. It must not be
            // treated as "finish" because students may leave with an incomplete
            // four-direction group; the web host will persist and abandon it.
            WebBridge.Instance?.RequestExit();
#else
            if(CanQuit())CompleteQuit();
#endif
        }
        private void CompleteQuit(){allowQuit=true;Application.Quit();}
    }
}
