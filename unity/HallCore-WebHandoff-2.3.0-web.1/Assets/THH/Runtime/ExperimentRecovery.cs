using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HallLab
{
    public static class ExperimentRecovery
    {
        [Serializable]
        public sealed class Snapshot
        {
            public int version = ExperimentLogger.SchemaVersion;
            public string sessionId;
            public List<ExperimentRow> rows = new List<ExperimentRow>();
        }

        public static string DefaultFolder => Path.Combine(ExperimentStorage.Root, "Recovery");

        public static bool Write(string path, string session, IReadOnlyList<ExperimentRow> rows, out string error)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var snapshot = new Snapshot { sessionId = session, rows = rows.ToList() };
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(snapshot));
                using(var stream = new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                    stream.Write(bytes,0,bytes.Length); stream.Flush(true);
                }
                if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path);
                error = ""; return true;
            } catch(Exception ex) when(IsStorageError(ex)) { error = "恢复文件写入失败：" + ex.Message; return false; }
            finally { try { if(File.Exists(temporary))File.Delete(temporary); } catch(Exception ex) when(IsStorageError(ex)) {} }
        }

        public static string[] Find(string folder)
        {
            try { return Directory.Exists(folder) ? Directory.GetFiles(folder,"*.json").OrderByDescending(File.GetLastWriteTimeUtc).ToArray() : Array.Empty<string>(); }
            catch(Exception ex) when(IsStorageError(ex)) { return Array.Empty<string>(); }
        }

        public static bool Read(string path, out Snapshot snapshot, out string error)
        {
            snapshot = null;
            try {
                if(new FileInfo(path).Length > 32*1024*1024) throw new InvalidDataException("恢复文件过大");
                var value = JsonUtility.FromJson<Snapshot>(File.ReadAllText(path,Encoding.UTF8));
                if(value == null || string.IsNullOrWhiteSpace(value.sessionId) || value.version != ExperimentLogger.SchemaVersion || value.rows == null || value.rows.Any(r =>
                    r == null || string.IsNullOrEmpty(r.id) || string.IsNullOrEmpty(r.seriesId) ||
                    (r.label != "VH-IS" && r.label != "VH-IM") || Math.Abs(r.voltageDirection) != 1 ||
                    !new[]{r.primaryCurrent,r.secondaryCurrent,r.v1,r.v2,r.v3,r.v4,r.vh,r.b1,r.b2,r.b3,r.b4,
                        r.is1,r.is2,r.is3,r.is4,r.im1,r.im2,r.im3,r.im4}.All(HallEffectMath.IsFinite)))
                    throw new InvalidDataException("恢复文件格式或数据无效");
                snapshot=value; error="";return true;
            } catch(Exception ex) when(IsStorageError(ex)) { error="无法读取恢复文件："+ex.Message;return false; }
        }

        public static bool Remove(string path)
        { try { if(File.Exists(path)) File.Delete(path);return true; } catch(Exception ex) when(IsStorageError(ex)) {return false;} }
        private static bool IsStorageError(Exception ex) => ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException || ex is System.Security.SecurityException;
    }
}
