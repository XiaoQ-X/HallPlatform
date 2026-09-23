using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HallLab
{
    public sealed class ExperimentLogger
    {
        public const int SchemaVersion = 4;
        private const string Header = "类型,IS(mA),IM(A),V1(mV),V2(mV),V3(mV),V4(mV),Vcorr=VH+VE(mV),B1(T),B2(T),B3(T),B4(T),批次,电压方向,Vcorr归一(mV),IS1(mA),IS2(mA),IS3(mA),IS4(mA),IM1(A),IM2(A),IM3(A),IM4(A),记录ID";
        private readonly UTF8Encoding utf8 = new UTF8Encoding(false);
        private readonly object writeLock = new object();
        private readonly List<string> pendingLogLines = new List<string>();
        private string initializationError = "", saveError = "", logError = "";
        public string FolderPath { get; private set; } = "";
        public string LogFilePath { get; private set; } = "";
        public string CsvFilePath { get; private set; } = "";
        public string MetadataFilePath { get; private set; } = "";
        public string LastError => string.Join(" ", new[] { initializationError, saveError, logError }).Trim();
        public bool HasSession => !string.IsNullOrEmpty(CsvFilePath);

        public static string DefaultFolder => Path.Combine(ExperimentStorage.Root, "用户操作记录");

        public bool Initialize(string sessionId, string folder = null)
        {
            lock (writeLock)
            {
                if (pendingLogLines.Count > 0 && !Log("SESSION", "保存待重试操作记录")) return false;
                var created = new List<string>();
                try
                {
                    string destination = Path.GetFullPath(folder ?? DefaultFolder);
                    Directory.CreateDirectory(destination);
                    string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N");
                    string log = Path.Combine(destination, "用户操作记录_" + stamp + ".txt");
                    string csv = Path.Combine(destination, "实验数据_" + stamp + ".csv");
                    string metadata = Path.Combine(destination, "参数来源_" + stamp + ".json");
                    CreateNew(csv, Header + Environment.NewLine, created);
                    CreateNew(log, "SESSION " + sessionId + Environment.NewLine, created);
                    CreateNew(metadata, JsonUtility.ToJson(new SessionMetadata(sessionId), true), created);
                    // Do not replace an existing session until all new files exist.
                    FolderPath = destination; LogFilePath = log; CsvFilePath = csv; MetadataFilePath = metadata;
                    initializationError = saveError = logError = "";
                    return true;
                }
                catch (Exception ex) when (IsFileError(ex))
                {
                    foreach (string file in created) TryDeleteOwnedTemporaryFile(file);
                    initializationError = "创建记录文件失败：" + ex.Message;
                    return false;
                }
            }
        }

        private void CreateNew(string path, string text, List<string> created)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created.Add(path);
                byte[] bytes = utf8.GetBytes(text); stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
        }

        public bool Log(string category, string message)
        {
            if (!HasSession) { logError = "记录器尚未初始化"; return false; }
            lock (writeLock)
            {
                pendingLogLines.Add(string.Format(CultureInfo.InvariantCulture,
                    "[{0:yyyy-MM-dd HH:mm:ss.fff}] [{1}] {2}{3}", DateTime.Now, category, message, Environment.NewLine));
                try
                {
                    File.AppendAllText(LogFilePath, string.Concat(pendingLogLines), utf8);
                    pendingLogLines.Clear();
                    logError = ""; return true;
                }
                catch (Exception ex) when (IsFileError(ex))
                { logError = "操作日志写入失败：" + ex.Message; return false; }
            }
        }

        public bool SaveRows(IReadOnlyList<ExperimentRow> rows)
        {
            if (!HasSession) { saveError = "记录器尚未初始化，未保存文件"; return false; }
            var builder = new StringBuilder(Header).AppendLine();
            foreach (ExperimentRow row in rows)
            {
                if (row == null || Math.Abs(row.voltageDirection) != 1) { saveError = "无效数据行或电压方向"; return false; }
                float[] values = { row.primaryCurrent, row.secondaryCurrent, row.v1, row.v2, row.v3, row.v4, row.vh, row.b1, row.b2, row.b3, row.b4 };
                foreach (float value in values)
                    if (!HallEffectMath.IsFinite(value)) { saveError = "保存被拒绝：存在非有限数值。"; return false; }
                builder.Append(EscapeCsv(row.label));
                for (int i = 0; i < values.Length; i++) builder.Append(',').Append(values[i].ToString(i < 2 ? "F3" : i < 7 ? "F4" : "F6", CultureInfo.InvariantCulture));
                builder.Append(',').Append(EscapeCsv(row.seriesId)).Append(',').Append(row.voltageDirection);
                float[] samples = {row.NormalizedHall,row.is1,row.is2,row.is3,row.is4,row.im1,row.im2,row.im3,row.im4};
                foreach(float sample in samples) {
                    if(!HallEffectMath.IsFinite(sample)) { saveError="采样值无效"; return false; }
                    builder.Append(',').Append(sample.ToString("F6",CultureInfo.InvariantCulture));
                }
                builder.Append(',').Append(EscapeCsv(row.id));
                builder.AppendLine();
            }
            lock (writeLock)
            {
                string temporary = CsvFilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] bytes = utf8.GetBytes(builder.ToString());
                        stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                    }
                    // Same-directory replacement: a failed write never truncates the previous CSV.
                    if (File.Exists(CsvFilePath)) File.Replace(temporary, CsvFilePath, null);
                    else File.Move(temporary, CsvFilePath);
                    saveError = ""; initializationError = ""; return true;
                }
                catch (Exception ex) when (IsFileError(ex))
                { saveError = "实验数据保存失败（数据仍保留在当前表格）：" + ex.Message; return false; }
                finally { TryDeleteOwnedTemporaryFile(temporary); }
            }
        }

        private static string EscapeCsv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        private static bool IsFileError(Exception ex) => ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException || ex is System.Security.SecurityException;
        private static void TryDeleteOwnedTemporaryFile(string path)
        { try { if (File.Exists(path)) File.Delete(path); } catch (Exception ex) when (IsFileError(ex)) { } }

        [Serializable]
        private sealed class SessionMetadata
        {
            public int schemaVersion = SchemaVersion;
            public string sessionId, createdAtUtc;
            public string instrumentReference = "独立霍尔效应教学仿真；功能研究参考天煌 TH-H 公开资料，具体批次未核实；非原厂或授权软件";
            public string sampleReference = HallEffectMath.SampleMaterialName;
            public float thicknessMm = HallEffectMath.SampleThicknessMm, widthMm = HallEffectMath.SampleWidthMm, electrodeSpacingMm = HallEffectMath.SampleLengthMm;
            public string geometrySource = "用户提供文字资料 S2；非实物检验结论";
            public float maxIsMilliamps = HallInstrument.MaxIsMilliamps, maxImAmps = HallInstrument.MaxImAmps, voltmeterRangeMillivolts = HallInstrument.VoltmeterRangeMillivolts;
            public string rangeSource = "用户提供 TH-H 参数摘要 S1";
            public string magneticModel = HallEffectMath.MagneticModelId;
            public float demoDensityPerM3 = HallEffectMath.CarrierVolumeDensityPerCubicMeter, demoMobilityM2PerVs = HallEffectMath.CarrierMobilitySquareMetersPerVoltSecond;
            public float demoCoilTurns = HallEffectMath.CoilTurns, demoMagneticPathM = HallEffectMath.MagneticPathLengthMeters, demoCoupling = HallEffectMath.MagneticCircuitCoupling;
            public float demoMsAperM = HallEffectMath.MagneticSaturationMagnetizationAmpsPerMeter, demoShapeAperM = HallEffectMath.MagneticAnhystereticShapeAmpsPerMeter, demoResponsePerSecond = HallEffectMath.MagneticResponsePerSecond;
            public string pendingSpecifications = "B-IM校准；实物批次与接线对应关系；预热时间；10μA/1mA含义；±0.5%准确度基准；显示分辨率";
            public string limitations = "当前模型的浓度、迁移率、磁路和噪声参数未作实机标定；未模拟真实预热；三位电压小数不代表硬件分辨率；禁止据此生成实机不确定度。";
            public string csvUnits = "IS:mA; IM:A; V1-V4,Vcorr=VH+VE:mV; B1-B4:T; columns 1-12 retained; Vcorr归一 = Vcorr * 电压方向; VE未扣除，需独立标定后得到理想VH; each row includes series and four actual currents";
            public string unityVersion = Application.unityVersion;
            public SessionMetadata(string id) { sessionId = id; createdAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture); }
        }
    }
}

