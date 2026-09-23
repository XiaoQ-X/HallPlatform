using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HallLab
{
    public sealed partial class THHWorkbench
    {
        private const int RowsPerPage = 10;
        private Text samplingText, sweepButton, dataStatus, pageText, curveAxis, dataTitle;
        private readonly Text[,] tableCells = new Text[RowsPerPage, 8];
        private readonly Text[] xTicks = new Text[6], yTicks = new Text[6];
        private Text isTableButton, imTableButton;
        private bool viewSweepIs=true;
        private string selectedSeries;
        private Dropdown seriesDropdown;
        private Text previousPageButton,nextPageButton,undoDeleteButton,newSeriesButton;
        private readonly List<string> seriesIds=new List<string>();
        private readonly Text[] deleteButtons=new Text[RowsPerPage];
        private List<ExperimentRow> visibleRows=new List<ExperimentRow>();
        private int dataPage, renderedVersion = -1, renderedPage = -1;
        public bool DataPanelVisible => dataVisible;
        public string DisplayedFit => fitText == null ? "" : fitText.text;

        private void BuildDataInterface(Transform parent)
        {
            dataPanel = Panel("Experiment data",parent,Vector2.zero,Vector2.one,new Vector2(0,52),new Vector2(0,-72),Color.white);
            Icon(dataPanel,"chart-no-axes-combined",32,28,25,AccentColor);
            Label(dataPanel,"实验数据",25,Ink,72,19,500,42).fontStyle=FontStyle.Bold;
            Label(dataPanel,"四方向原始读数与归一化修正电压（Vcorr = VH + VE）",13,Muted,32,72,700,28);
            IconButton(dataPanel,"返回装置","x",1372,26,ToggleDataPanel);
            isTableButton=Choice(dataPanel,"VH-IS",32,126,150,()=>BrowseDataSweep(true),null,true);
            imTableButton=Choice(dataPanel,"VH-IM",198,126,150,()=>BrowseDataSweep(false),null,true);
            seriesDropdown=SeriesSelector(dataPanel,386,126,374);
            newSeriesButton=TextAction(dataPanel,"新批次","circle-plus",780,125,142,()=>{
                if(RejectAutomaticEdit())return;
                if(ExperimentModel.SelectSweep(viewSweepIs)&&ExperimentModel.NewSeries()){ShowTab(3);Refresh("新批次已就绪");}
                else Refresh(ExperimentModel.LastRecordMessage);
            });
            undoDeleteButton=IconButton(dataPanel,"撤销删除","undo-2",960,126,()=>{if(RejectAutomaticEdit())return;ExperimentModel.UndoDelete();renderedVersion=-1;RefreshDataPanel();});
            IconButton(dataPanel,"另存目录","folder-open",1012,126,ShowSaveAs);
            IconButton(dataPanel,"打开记录目录","info",1064,126,OpenExperimentFolder);
            TextAction(dataPanel,"保存 CSV","download",1150,125,226,SaveExperimentData,true);
            Rule(dataPanel,188,1376);
            dataTitle=Label(dataPanel,"",15,Ink,32,211,760,30);
            string[] names={"序号","IS / mA","IM / A","V1 / mV","V2 / mV","V3 / mV","V4 / mV","Vcorr / mV"};
            for(int col=0;col<8;col++) {
                float left=col==0?32:88+(col-1)*100, width=col==0?48:100;
                Label(dataPanel,names[col],13,Muted,left,262,width,28).alignment=TextAnchor.MiddleCenter;
                for(int row=0;row<RowsPerPage;row++) {
                    var cell=Label(dataPanel,"",14,Ink,left,308+row*30,width,28);
                    cell.alignment=TextAnchor.MiddleCenter;cell.verticalOverflow=VerticalWrapMode.Truncate;
                    tableCells[row,col]=cell;
                }
            }
            for(int row=0;row<RowsPerPage;row++) {
                int slot=row;deleteButtons[row]=IconButton(dataPanel,"删除此行","x",799,309+row*30,()=>{
                    if(RejectAutomaticEdit())return;
                    int index=dataPage*RowsPerPage+slot;if(index<visibleRows.Count){ExperimentModel.DeleteRow(visibleRows[index].id);renderedVersion=-1;RefreshDataPanel();}
                },false,26);
            }
            for(int row=0;row<=RowsPerPage;row++)Rule(dataPanel,302+row*30,768);
            dataTableText=Label(dataPanel,"",16,Muted,64,386,680,96);
            dataTableText.alignment=TextAnchor.MiddleCenter;
            previousPageButton=IconButton(dataPanel,"上一页","chevron-left",32,624,()=>ChangeDataPage(-1));
            pageText=Label(dataPanel,"",13,Muted,158,628,500,26);pageText.alignment=TextAnchor.MiddleCenter;
            nextPageButton=IconButton(dataPanel,"下一页","chevron-right",752,624,()=>ChangeDataPage(1));
            Label(dataPanel,"线性拟合",18,Ink,860,212,500,30).fontStyle=FontStyle.Bold;
            fitText=Label(dataPanel,"",15,Cyan,860,256,532,64);
            var curveRect=Rect("CurveImage",dataPanel,new Vector2(0,1),new Vector2(0,1),
                new Vector2(914,-560),new Vector2(1394,-338));
            curveImage=curveRect.gameObject.AddComponent<RawImage>();curveImage.raycastTarget=false;
            curveTexture=new Texture2D(620,300,TextureFormat.RGBA32,false){filterMode=FilterMode.Bilinear};
            curveImage.texture=curveTexture;
            for(int i=0;i<6;i++) {
                xTicks[i]=Label(dataPanel,"",12,Muted,887+i*96,564,54,24);xTicks[i].alignment=TextAnchor.MiddleCenter;
                yTicks[i]=Label(dataPanel,"",12,Muted,842,548-i*44.4f,62,24);yTicks[i].alignment=TextAnchor.MiddleRight;
            }
            curveAxis=Label(dataPanel,"",13,Muted,860,608,532,56);
            dataStatus=Label(dataPanel,"",12,Muted,32,688,1376,68);
            dataPanel.gameObject.SetActive(false);
        }

        private Dropdown SeriesSelector(Transform parent,float x,float top,float width)
        {
            var root=Panel("Series selector",parent,new Vector2(0,1),new Vector2(0,1),new Vector2(x,-top-38),new Vector2(x+width,-top),PanelAlt);Round(root);
            var dropdown=root.gameObject.AddComponent<Dropdown>();dropdown.targetGraphic=root.GetComponent<Image>();
            dropdown.captionText=Label(root,"实验批次",13,Ink,12,0,width-46,38);dropdown.captionText.alignment=TextAnchor.MiddleLeft;
            Icon(root,"chevron-right",width-28,10,18,Muted).transform.localRotation=Quaternion.Euler(0,0,-90);
            var template=Panel("Template",root,new Vector2(0,0),new Vector2(1,0),new Vector2(0,-190),new Vector2(0,0),Color.white);
            template.pivot=new Vector2(.5f,1);template.anchoredPosition=Vector2.zero;
            var viewport=Panel("Viewport",template,Vector2.zero,Vector2.one,new Vector2(4,4),new Vector2(-4,-4),Color.white);viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Content",viewport,new Vector2(0,1),Vector2.one,new Vector2(0,-38),Vector2.zero);content.pivot=new Vector2(.5f,1);
            var item=Panel("Item",content,new Vector2(0,1),new Vector2(1,1),new Vector2(0,-38),Vector2.zero,PanelAlt);
            var toggle=item.gameObject.AddComponent<Toggle>();toggle.targetGraphic=item.GetComponent<Image>();
            var mark=Panel("Selected",item,new Vector2(0,0),new Vector2(0,1),Vector2.zero,new Vector2(3,0),AccentColor);toggle.graphic=mark.GetComponent<Image>();
            dropdown.itemText=Label(item,"",13,Ink,12,0,width-38,38);dropdown.itemText.alignment=TextAnchor.MiddleLeft;
            var scroll=template.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
            dropdown.template=template;template.gameObject.SetActive(false);dropdown.onValueChanged.AddListener(index=>{
                if(index<seriesIds.Count){selectedSeries=seriesIds[index];dataPage=0;renderedVersion=-1;RefreshDataPanel();}
            });return dropdown;
        }

        public void ToggleDataPanel()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            WebBridge.Instance?.RequestSnapshot();
#else
            dataVisible=!dataVisible;
            dataPanel.gameObject.SetActive(dataVisible);
            sceneTools.gameObject.SetActive(!dataVisible);
            readingDock.gameObject.SetActive(!dataVisible);
            RefreshNavigation();UpdateLessonLayout();
            if(dataVisible) { viewSweepIs=ExperimentModel.SweepIs;selectedSeries=null;renderedVersion=-1;RefreshDataPanel(); }
#endif
        }
        public void SelectDataSweep(bool sweepIs)
        {
            if(dataVisible){BrowseDataSweep(sweepIs);return;}
            if(RejectAutomaticEdit())return;
            if(!ExperimentModel.SelectSweep(sweepIs)) { Refresh(ExperimentModel.LastRecordMessage);return; }
            viewSweepIs=sweepIs;selectedSeries=null;
            dataPage=0; renderedVersion=-1;
            Refresh(sweepIs ? "记录到 VH-IS 表：固定 IM，逐组改变 IS" : "记录到 VH-IM 表：固定 IS，逐组改变 IM");
        }
        public void BrowseDataSweep(bool sweepIs){viewSweepIs=sweepIs;selectedSeries=null;dataPage=0;renderedVersion=-1;RefreshDataPanel();}
        public void ClearPendingReadings()
        { if(RejectAutomaticEdit())return;ExperimentModel.ClearMeasurementSet();ExperimentModel.TrackOperation("清除本组","完整数据保留");Refresh("当前未完成的四方向读数已清除，已保存表格保留"); }
        public void SaveExperimentData()
        {
            bool saved=ExperimentModel.SaveData();
            Refresh(saved ? "实验表格已保存 CSV" : ExperimentModel.LastStorageError);
        }
        public void OpenExperimentFolder()
        {
            if(string.IsNullOrEmpty(ExperimentModel.CsvFilePath)) SaveExperimentData();
            if(!string.IsNullOrEmpty(ExperimentModel.CsvFilePath))
                try {
                    var folder=Path.GetDirectoryName(ExperimentModel.CsvFilePath);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder){UseShellExecute=true});
                } catch(Exception error) {Refresh("无法打开记录目录："+error.Message);}
        }
        private void ChangeDataPage(int direction)
        { dataPage+=direction;RefreshDataPanel(); }
        private void RefreshSamplingControls()
        {
            if(samplingText==null)return;
            var report=Circuit.Evaluate();
            string message=!PhysicalModelEnabled?"物理模型未开启":!Circuit.Powered?"输出已关闭":!report.Ready?"实验回路无效":!report.HallMode?"当前为 Vσ，请切换到 VH":
                Circuit.IsSetpoint<.15f||Circuit.ImSetpoint<.05f?"电流不足：IS ≥ 0.15 mA，IM ≥ 0.05 A":ExperimentModel.SamplingStable?"读数稳定，可记录":"等待电流 / 磁场稳定";
            for(int i=0;i<4;i++) {
                sampleValues[i].text=ExperimentModel.HasMeasurement(i)?ExperimentModel.MeasurementValue(i).ToString("F4")+" mV":"待记录";
                sampleValues[i].color=ExperimentModel.HasMeasurement(i)?AccentColor:Muted;
                if(samplingRings[i]!=null)samplingRings[i].SetProgress(ExperimentModel.HasMeasurement(i)?1:0);
            }
            samplingCount.text="本组进度    "+ExperimentModel.MeasurementCount+" / 4";
            samplingText.text=message;
            sweepButton.text=ExperimentModel.SweepIs?"固定 IM，逐组调整 IS":"固定 IS，逐组调整 IM";
        }
        private void RefreshDataPanel()
        {
            if(dataPanel==null)return;
            dataStatus.text=ExperimentModel.HasUnsavedRows ? "保存失败，数据仍在当前表格。请点击“保存 / 重试 CSV”。\n"+ExperimentModel.LastStorageError
                : string.IsNullOrEmpty(ExperimentModel.CsvFilePath) ? "尚未生成 CSV；四方向完成一组后自动保存，也可手动保存。"
                : "已保存 CSV："+ExperimentModel.CsvFilePath;
            dataStatus.text+="\n换向前归零断电；四组保持电流幅值一致。当前读数由模型计算。";
            undoDeleteButton.transform.parent.GetComponent<Button>().interactable=ExperimentModel.CanUndoDelete&&!automaticExperimentRunning;
            newSeriesButton.transform.parent.GetComponent<Button>().interactable=!automaticExperimentRunning&&ExperimentModel.MeasurementCount==0;
            if(renderedVersion==ExperimentModel.DataVersion && renderedPage==dataPage)return;
            bool sweepIs=viewSweepIs;
            var all=ExperimentModel.Rows.Where(r=>r.label==(sweepIs?"VH-IS":"VH-IM")).ToList();
            seriesIds.Clear();seriesIds.AddRange(all.Select(r=>r.seriesId).Distinct());
            if(selectedSeries==null||!seriesIds.Contains(selectedSeries))selectedSeries=seriesIds.LastOrDefault();
            seriesDropdown.interactable=seriesIds.Count>0;
            seriesDropdown.ClearOptions();
            seriesDropdown.AddOptions(seriesIds.Count==0?new List<string>{"暂无实验批次"}:seriesIds.Select((id,i)=>{
                var first=all.First(r=>r.seriesId==id);return "批次 "+(i+1)+"   ·   "+(sweepIs?"IM "+F(first.secondaryCurrent,3)+" A":"IS "+F(first.primaryCurrent,2)+" mA");
            }).ToList());
            seriesDropdown.SetValueWithoutNotify(Math.Max(0,seriesIds.IndexOf(selectedSeries)));seriesDropdown.RefreshShownValue();
            var rows=all.Where(r=>r.seriesId==selectedSeries).ToList();visibleRows=rows;
            int pages=Math.Max(1,(rows.Count+RowsPerPage-1)/RowsPerPage);
            dataPage=Mathf.Clamp(dataPage,0,pages-1);renderedPage=dataPage;renderedVersion=ExperimentModel.DataVersion;
            previousPageButton.transform.parent.GetComponent<Button>().interactable=dataPage>0;
            nextPageButton.transform.parent.GetComponent<Button>().interactable=dataPage<pages-1;
            SetSelected(isTableButton,sweepIs);SetSelected(imTableButton,!sweepIs);
            dataTitle.text=(sweepIs?"VH-IS   /   固定 IM，扫描 IS":"VH-IM   /   固定 IS，扫描 IM")+"     ·     Vcorr=VH+VE，已按电压方向归一";
            pageText.text="第 "+(dataPage+1)+" / "+pages+" 页   ·   共 "+rows.Count+" 组";
            dataTableText.text=rows.Count==0?"暂无完整数据\n在采样页记录 V1–V4 后自动加入表格":"";
            for(int row=0;row<RowsPerPage;row++) {
                int index=dataPage*RowsPerPage+row;
                string[] values=Array.Empty<string>();
                if(index<rows.Count) {
                    var r=rows[index];values=new[]{(index+1).ToString(),F(r.primaryCurrent,2),F(r.secondaryCurrent,3),F(r.v1,4),F(r.v2,4),F(r.v3,4),F(r.v4,4),F(r.NormalizedHall,4)};
                }
                deleteButtons[row].transform.parent.gameObject.SetActive(index<rows.Count);
                deleteButtons[row].transform.parent.GetComponent<Button>().interactable=!automaticExperimentRunning;
                for(int col=0;col<8;col++)tableCells[row,col].text=values.Length==0?"":values[col];
            }
            bool fitted=ExperimentFit.TryFit(rows,sweepIs,out float slope,out float intercept,out float rSquared,out string reason);
            fitText.text=fitted?string.Format(CultureInfo.InvariantCulture,"Vcorr = {0:F5} × {1} + {2:F5} mV\nR² = {3:F5}   ·   n = {4}",slope,sweepIs?"IS":"IM",intercept,rSquared,rows.Count):reason;
            DrawCurve(rows,slope,intercept,fitted,sweepIs);
        }
        private static string F(float value,int digits)=>value.ToString("F"+digits,CultureInfo.InvariantCulture);
        private void DrawCurve(IReadOnlyList<ExperimentRow> rows,float slope,float intercept,bool fitted,bool sweepIs)
        {
            int w=curveTexture.width,h=curveTexture.height;
            var pixels=new Color32[w*h];
            for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32(245,248,246,255);
            float maxX=rows.Count==0?1:Mathf.Max(.01f,rows.Max(r=>r.SweepCurrent)*1.1f);
            float limitY=rows.Count==0 ? .1f : Mathf.Max(.01f,rows.Max(r=>Mathf.Abs(r.NormalizedHall))*1.2f);
            if(fitted && rows.Count>0) {
                limitY=Mathf.Max(limitY,Mathf.Abs(slope*rows.Min(r=>r.SweepCurrent)+intercept)*1.1f);
                limitY=Mathf.Max(limitY,Mathf.Abs(slope*rows.Max(r=>r.SweepCurrent)+intercept)*1.1f);
            }
            void Dot(int x,int y,Color32 color){if(x>=0&&x<w&&y>=0&&y<h)pixels[y*w+x]=color;}
            int Y(float v)=>Mathf.RoundToInt((v+limitY)/(2*limitY)*(h-1));
            for(int tick=0;tick<6;tick++) {
                int x=Mathf.RoundToInt(tick*(w-1)/5f),y=Mathf.RoundToInt(tick*(h-1)/5f);
                for(int n=0;n<h;n++)Dot(x,n,new Color32(214,223,218,255));
                for(int n=0;n<w;n++)Dot(n,y,new Color32(214,223,218,255));
                xTicks[tick].text=F(maxX*tick/5,2);yTicks[tick].text=F(-limitY+2*limitY*tick/5,3);
            }
            for(int x=0;x<w;x++)Dot(x,Y(0),new Color32(95,123,137,255));
            if(fitted) {
                float low=rows.Min(r=>r.SweepCurrent),high=rows.Max(r=>r.SweepCurrent);
                for(int x=0;x<w;x++) {
                    float current=x/(float)(w-1)*maxX;if(current<low||current>high)continue;
                    int y=Y(slope*current+intercept);for(int d=-1;d<=1;d++)Dot(x,y+d,new Color32(188,104,35,255));
                }
            }
            foreach(var row in rows) {
                int x=Mathf.RoundToInt(row.SweepCurrent/maxX*(w-1)),y=Y(row.NormalizedHall);
                for(int dx=-4;dx<=4;dx++)for(int dy=-4;dy<=4;dy++)if(dx*dx+dy*dy<=16)Dot(x+dx,y+dy,new Color32(12,104,87,255));
            }
            curveTexture.SetPixels32(pixels);curveTexture.Apply(false);
            curveAxis.text="横轴 "+(sweepIs?"IS / mA":"IM / A")+"   ·   纵轴 Vcorr / mV\n绿色点：四方向修正（VH+VE）   橙色线：线性拟合；VH-IM 仅在未饱和区近似线性";
        }
        private void OnDestroy()
        {
            Application.wantsToQuit-=CanQuit;
            if(ExperimentModel.HasUnsavedRows)ExperimentModel.PersistRecovery();
            if(uiRoot!=null){uiRoot.SetActive(false);if(Application.isPlaying)Destroy(uiRoot);else DestroyImmediate(uiRoot);}
            foreach(var sprite in iconSprites.Values){if(Application.isPlaying)Destroy(sprite);else DestroyImmediate(sprite);}
#if !UNITY_WEBGL || UNITY_EDITOR
            if(font!=null){if(Application.isPlaying)Destroy(font);else DestroyImmediate(font);}
#endif
            if(curveTexture!=null) { if(Application.isPlaying)Destroy(curveTexture);else DestroyImmediate(curveTexture); }
        }
    }
}
