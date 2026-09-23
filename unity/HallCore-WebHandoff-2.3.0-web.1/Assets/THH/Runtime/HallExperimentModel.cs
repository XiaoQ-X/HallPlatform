using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HallLab
{
    // Adapter around the reusable huoer calculation module.  THHCircuit remains
    // the source of truth for wiring and interlocks; this class only turns a
    // valid circuit state into a demonstrative reading.
    public sealed class HallExperimentModel
    {
        public float ActualIsMilliamps { get; private set; }
        public float ActualImAmps { get; private set; }
        public float MagneticFieldTesla { get; private set; }
        public float CoilVoltageVolts { get; private set; }
        public ElectricalReading LiveReading { get; private set; }
        public bool Ready { get; private set; }
        public IReadOnlyList<ExperimentRow> Rows => rows.AsReadOnly();
        public string LastStorageError => string.Join(" ",new[]{storageError,recoveryError,logger.LastError}).Trim();
        public string CsvFilePath => logger.CsvFilePath;
        public string StorageFolder { get; set; }
        private string recoveryFolder;
        public string RecoveryFolder { get => recoveryFolder??ExperimentRecovery.DefaultFolder; set => recoveryFolder=value; }
        public string RecoveryPath => Path.Combine(RecoveryFolder,sessionId+".json");
        public bool RecoverySaved { get; private set; }
        public bool CanUndoDelete => deletedRow != null;
        public string CurrentSeries { get; private set; }
        public string OperationLogPath => logger.LogFilePath;
        public string LastRecordMessage { get; private set; } = "";
        public bool SweepIs { get; private set; } = true;
        public int DataVersion { get; private set; }
        public bool HasUnsavedRows { get; private set; }
        public bool SamplingStable => stability.Ready;
        public int MeasurementCount => measurementValid.Count(v => v);
        public bool FourDirectionReady => MeasurementCount == 4;
        public string MeasurementStatus => "四方向读数 " + MeasurementCount + "/4";

        private readonly List<ExperimentRow> rows = new List<ExperimentRow>();
        private readonly float[] measurementValues = new float[4];
        private readonly float[] measurementFields = new float[4];
        private readonly float[] measuredIs = new float[4], measuredIm = new float[4];
        private readonly bool[] measurementValid = new bool[4];
        private float pendingIs = -1f, pendingIm = -1f;
        private float pendingField;
        private string webGroupId;
        private WebMeasurement webMeasurement;
        private int pendingVoltageDirection;
        private readonly SamplingStability stability = new SamplingStability();
        private ExperimentLogger logger = new ExperimentLogger();
        private bool loggerInitialized;
        private readonly string sessionId = Guid.NewGuid().ToString("N");
        private readonly List<string> operations = new List<string>();
        private int loggedOperations, seriesNumber, deletedIndex;
        private ExperimentRow deletedRow;
        private string storageError = "", recoveryError = "", recoveredFrom;
        private float phase;
        private float noiseSeed = 37.17f;
        private float previousSignedIm;
        private MagneticFieldState magneticState;

        public void Reset()
        {
            ActualIsMilliamps = 0f;
            ActualImAmps = 0f;
            MagneticFieldTesla = 0f;
            CoilVoltageVolts = 0f;
            LiveReading = default;
            Ready = false;
            phase = 0f;
            previousSignedIm = 0f;
            magneticState = default;
            stability.Reset();
            ClearMeasurementSet();
        }

        public void InvalidateSampling() { stability.Reset(); Ready = false; }
        public bool HasMeasurement(int slot) => measurementValid[slot];
        public float MeasurementValue(int slot) => measurementValues[slot];
        public bool SelectSweep(bool sweepIs)
        {
            if (SweepIs == sweepIs) return true;
            if (MeasurementCount > 0) { LastRecordMessage = "请先完成或清除当前四方向读数，再切换表格类型"; return false; }
            SweepIs = sweepIs; CurrentSeries=null; DataVersion++; TrackOperation("采样类型",sweepIs?"VH-IS":"VH-IM"); return true;
        }

        public bool NewSeries()
        {
            if(MeasurementCount>0) {LastRecordMessage="请先完成或清除本组读数";return false;}
            CurrentSeries=null; DataVersion++;TrackOperation("新建批次","下一组数据将进入新批次");return true;
        }

        public void TrackOperation(string category,string message,bool success=true)
        {
            WebBridge.Instance?.Operation(this,category,message,success);
            operations.Add(DateTime.UtcNow.ToString("O")+" ["+category+"] "+message);
            if(loggerInitialized) FlushOperations();
        }
        private void FlushOperations()
        {
            if(loggedOperations>=operations.Count)return;
            // The logger owns retrying queued lines after a write failure.
            logger.Log("操作",string.Join(Environment.NewLine,operations.Skip(loggedOperations)));
            loggedOperations=operations.Count;
        }
        public bool PersistRecovery()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            RecoverySaved=ExperimentRecovery.Write(RecoveryPath,sessionId,rows,out recoveryError);
            return RecoverySaved;
#endif
        }
        public bool Restore(string path)
        {
            if(!ExperimentRecovery.Read(path,out var snapshot,out storageError))return false;
            var known=new HashSet<string>(rows.Select(r=>r.id));
            rows.AddRange(snapshot.rows.Where(r=>known.Add(r.id)));
            recoveredFrom=path;CurrentSeries=null;HasUnsavedRows=true;DataVersion++;
            TrackOperation("恢复",path); PersistRecovery();return true;
        }
        public bool DeleteRow(string id)
        {
            int index=rows.FindIndex(r=>r.id==id);if(index<0)return false;
            deletedIndex=index;deletedRow=rows[index];rows.RemoveAt(index);DataVersion++;
            TrackOperation("删除数据",id);SaveData();return true;
        }
        public bool UndoDelete()
        {
            if(deletedRow==null)return false;
            rows.Insert(Math.Min(deletedIndex,rows.Count),deletedRow);TrackOperation("撤销删除",deletedRow.id);
            deletedRow=null;DataVersion++;SaveData();return true;
        }

        public bool SaveData()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            HasUnsavedRows=false;
            return true;
#else
            HasUnsavedRows=true;
            PersistRecovery();
            bool saved=false;
            try {
                string folder=Path.GetFullPath(StorageFolder??ExperimentLogger.DefaultFolder);
                bool migrating=loggerInitialized&&!string.Equals(folder,logger.FolderPath,StringComparison.OrdinalIgnoreCase);
                var destination=migrating?new ExperimentLogger():logger;
                bool ready=(!migrating&&loggerInitialized)||destination.Initialize("THH-"+sessionId,folder);
                saved=ready&&destination.SaveRows(rows);
                storageError=saved?"":destination.LastError;
                if(saved) {
                    if(migrating){logger=destination;loggedOperations=0;}
                    loggerInitialized=true;FlushOperations();
                } else if(!migrating) loggerInitialized=ready;
            } catch(Exception ex) when(ex is ArgumentException || ex is NotSupportedException || ex is IOException || ex is UnauthorizedAccessException) {
                storageError=ex.Message;
            }
            HasUnsavedRows = !saved;
            if(saved) {
                ExperimentRecovery.Remove(RecoveryPath);
                if(recoveredFrom!=null) {ExperimentRecovery.Remove(recoveredFrom);recoveredFrom=null;}
                RecoverySaved=false;recoveryError="";
            }
            return saved;
#endif
        }

        public bool SaveAs(string folder) {StorageFolder=folder;return SaveData();}

        public bool RecordCurrentReading(THHCircuit circuit)
        {
            LastRecordMessage = "请先接通有效的 VH 回路";
            if (circuit == null || !circuit.Powered || !Ready || !circuit.Evaluate().Ready) {WebBridge.Instance?.Abnormal("CIRCUIT_NOT_READY",LastRecordMessage);return false;}
            var report = circuit.Evaluate();
            if (!report.HallMode) {WebBridge.Instance?.Abnormal("HALL_MODE_REQUIRED",LastRecordMessage);return false;}
            if (!stability.Ready || !stability.Matches(circuit.IsSetpoint, circuit.ImSetpoint, report.IsDirection, report.ImDirection))
            { LastRecordMessage = "电流或磁场尚未稳定，请等待后再记录"; WebBridge.Instance?.Abnormal("READING_UNSTABLE",LastRecordMessage);return false; }
            if (!HallInstrument.IsReadable(true, true, LiveReading.totalMillivolts) || ActualIsMilliamps < .15f || ActualImAmps < .05f)
            { LastRecordMessage = "读数无效、超量程或电流过小，未记录"; WebBridge.Instance?.Abnormal(!HallInstrument.IsReadable(true,true,LiveReading.totalMillivolts)?"VOLTAGE_OVER_RANGE":"CURRENT_TOO_LOW",LastRecordMessage);return false; }
            if (!RecordSymmetricSlot(circuit)) return false;
            if (!FourDirectionReady) { LastRecordMessage = "当前方向已记录，" + MeasurementStatus; WebBridge.Instance?.Measure(this,webMeasurement);return true; }
            var last=rows.LastOrDefault(r=>r.seriesId==CurrentSeries);
            float held=SweepIs?pendingIm:pendingIs;
            if(last==null || last.label!=(SweepIs?"VH-IS":"VH-IM") ||
                Mathf.Abs((SweepIs?last.secondaryCurrent:last.primaryCurrent)-held)>0.000001f)
                CurrentSeries=sessionId.Substring(0,8)+"-"+(++seriesNumber).ToString("D2");
            rows.Add(new ExperimentRow {
                label = SweepIs ? "VH-IS" : "VH-IM",
                seriesId=CurrentSeries,voltageDirection=pendingVoltageDirection,
                primaryCurrent = pendingIs,
                secondaryCurrent = pendingIm,
                v1 = measurementValues[0], v2 = measurementValues[1],
                v3 = measurementValues[2], v4 = measurementValues[3],
                vh = SymmetricHallMillivolts,
                b1 = measurementFields[0], b2 = measurementFields[1],
                b3 = measurementFields[2], b4 = measurementFields[3],
                is1=measuredIs[0],is2=measuredIs[1],is3=measuredIs[2],is4=measuredIs[3],
                im1=measuredIm[0],im2=measuredIm[1],im3=measuredIm[2],im4=measuredIm[3]
            });
            DataVersion++;
            TrackOperation("合成",CurrentSeries+" / "+rows.Last().id);
            webMeasurement.groupComplete=true;webMeasurement.row=rows.Last();
            webMeasurement.VH_mV=rows.Last().vh;webMeasurement.normalizedVH_mV=rows.Last().NormalizedHall;
            bool saved = SaveData();
            ClearMeasurementSet();
            WebBridge.Instance?.Measure(this,webMeasurement);
            LastRecordMessage = saved ? "四方向已合成为一行并保存 CSV（共 " + rows.Count + " 行）"
                : "已合成为一行，磁盘保存失败；数据保留在表格，请重试保存。" + LastStorageError;
#if UNITY_WEBGL && !UNITY_EDITOR
            LastRecordMessage="四方向已合成并发送网页（共 "+rows.Count+" 组）";
#endif
            return true;
        }

        private bool RecordSymmetricSlot(THHCircuit circuit)
        {
            CircuitReport report = circuit.Evaluate();
            if (!report.HallMode || report.IsDirection == 0 || report.ImDirection == 0) return false;
            if (pendingIs < 0f) {
                webGroupId=Guid.NewGuid().ToString("N");
                pendingIs = circuit.IsSetpoint; pendingIm = circuit.ImSetpoint;
                pendingField = Mathf.Abs(MagneticFieldTesla); pendingVoltageDirection = report.VoltageDirection;
            }
            if (Mathf.Abs(circuit.IsSetpoint - pendingIs) > .000001f || Mathf.Abs(circuit.ImSetpoint - pendingIm) > .000001f ||
                Mathf.Abs(ActualIsMilliamps-pendingIs)>SamplingStability.IsToleranceMilliamps ||
                Mathf.Abs(ActualImAmps-pendingIm)>SamplingStability.ImToleranceAmps ||
                Mathf.Abs(Mathf.Abs(MagneticFieldTesla) - pendingField) > 2f * SamplingStability.FieldToleranceTesla ||
                pendingVoltageDirection != report.VoltageDirection)
            { LastRecordMessage = "四方向须保持电流、磁场幅值和电压接线方向一致；可清除本组后重新采样"; WebBridge.Instance?.Abnormal("GROUP_CONDITION_CHANGED",LastRecordMessage);return false; }
            int slot = report.IsDirection > 0 ? (report.ImDirection > 0 ? 0 : 1) : (report.ImDirection < 0 ? 2 : 3);
            measurementValues[slot] = LiveReading.totalMillivolts;
            measurementFields[slot] = MagneticFieldTesla;
            measuredIs[slot]=ActualIsMilliamps; measuredIm[slot]=ActualImAmps;
            measurementValid[slot] = true;
            webMeasurement=new WebMeasurement {groupId=webGroupId,slot=slot+1,timestamp=DateTime.UtcNow.ToString("O"),
                isDirection=report.IsDirection,imDirection=report.ImDirection,voltageDirection=report.VoltageDirection,
                IS_mA=ActualIsMilliamps,IM_A=ActualImAmps,isSetpoint_mA=pendingIs,imSetpoint_A=pendingIm,
                B_T=MagneticFieldTesla,rawVoltage_mV=LiveReading.totalMillivolts,
                VH_mV=LiveReading.hallMillivolts*report.VoltageDirection,
                V0_mV=LiveReading.misalignmentMillivolts*report.VoltageDirection,
                VE_mV=LiveReading.ettinghausenMillivolts*report.VoltageDirection,
                VN_mV=LiveReading.nernstMillivolts*report.VoltageDirection,
                VRL_mV=LiveReading.righiLeducMillivolts*report.VoltageDirection,
                noise_mV=LiveReading.noiseMillivolts*report.VoltageDirection};
            TrackOperation("方向采样","V"+(slot+1)+" IS="+pendingIs+" IM="+pendingIm+" V="+measurementValues[slot]+" voltageDirection="+pendingVoltageDirection);
            return true;
        }

        public float SymmetricHallMillivolts => FourDirectionReady
            ? (measurementValues[0] - measurementValues[1] + measurementValues[2] - measurementValues[3]) / 4f
            : float.NaN;

        public void ClearMeasurementSet()
        {
            for (int i = 0; i < measurementValid.Length; i++) { measurementValid[i] = false; measurementValues[i] = 0f; measurementFields[i] = 0f; }
            pendingIs = pendingIm = -1f;
        }

        public void Tick(THHCircuit circuit, float deltaTime)
        {
            if (circuit == null || !HallEffectMath.IsFinite(deltaTime) || deltaTime <= 0f) return;
            CircuitReport report = circuit.Evaluate();
            Ready = circuit.Powered && report.Ready;
            float targetIs = Ready ? circuit.IsSetpoint : 0f;
            float targetIm = Ready ? circuit.ImSetpoint : 0f;
            ActualIsMilliamps = Mathf.MoveTowards(ActualIsMilliamps, targetIs, deltaTime * 1.6f);
            ActualImAmps = Mathf.MoveTowards(ActualImAmps, targetIm, deltaTime * .25f);
            int isPolarity = report.IsDirection == 0 ? 1 : report.IsDirection;
            int imPolarity = report.ImDirection == 0 ? 1 : report.ImDirection;
            float signedIm = ActualImAmps * imPolarity;
            float signedDiDt = (signedIm - previousSignedIm) / deltaTime;
            previousSignedIm = signedIm;
            CoilVoltageVolts = HallEffectMath.CoilTerminalVoltageVolts(signedIm, signedDiDt);
            HallEffectMath.UpdateMagneticField(signedIm, ref magneticState, deltaTime);
            MagneticFieldTesla = magneticState.fluxDensityTesla;
            phase += deltaTime;
            MeasurementMode mode = report.HallMode ? MeasurementMode.HallVoltage : MeasurementMode.ConductivityVoltage;
            var reading = HallEffectMath.Evaluate(
                ActualIsMilliamps,
                isPolarity,
                MagneticFieldTesla,
                mode,
                phase,
                noiseSeed,
                HallEffectMath.SampleThicknessMm);
            if (report.VoltageDirection != 0)
            {
                reading.totalMillivolts *= report.VoltageDirection;
            }
            LiveReading = reading;
            stability.Update(Ready && report.HallMode, ActualIsMilliamps, ActualImAmps,
                circuit.IsSetpoint, circuit.ImSetpoint, report.IsDirection, report.ImDirection, MagneticFieldTesla, deltaTime);
        }
    }
}
