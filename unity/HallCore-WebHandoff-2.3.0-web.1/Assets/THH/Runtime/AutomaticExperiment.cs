using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HallLab
{
    public sealed partial class THHWorkbench
    {
        private Text automaticExperimentButton, automaticProgressText;
        private Coroutine automaticExperimentRoutine;
        private bool automaticExperimentRunning;
        public bool AutomaticExperimentRunning => automaticExperimentRunning;
        public bool AutomaticExperimentSucceeded { get; private set; }
        public string AutomaticExperimentStatus { get; private set; } = "一键接线 → 自动调电流 → 四方向采样\nVH-IS / VH-IM 各 6 组，可随时停止";
        public int AutomaticCompletedGroups { get; private set; }

        private bool RejectAutomaticEdit()
        {
            if(WebBridge.Instance!=null && WebBridge.Instance.Finished){WebBridge.Instance.Abnormal("SESSION_FINISHED","ResetExperiment is required.");return true;}
            if (!automaticExperimentRunning && !LessonActive) return false;
            Refresh(LessonActive?"当前为演示；点击下一步，或返回学生实验后亲手操作":"自动实验正在运行；请先停止再手动操作");
            return true;
        }

        public void ToggleAutomaticExperiment()
        {
            if (automaticExperimentRunning)
            {
                StopAutomaticExperiment("自动实验已停止；完整数据和未完成组均保留");
                return;
            }
            if (HasOpenDialog) return;
            if (ExperimentModel.MeasurementCount > 0)
            {
                Refresh("请先完成或清除当前四方向读数，再启动自动实验");
                ShowTab(3);
                return;
            }
            if (!ExperimentModel.SaveData())
            {
                Refresh("保存失败，自动实验未启动：" + ExperimentModel.LastStorageError);
                return;
            }
            automaticExperimentRunning = true;
            AutomaticExperimentSucceeded = false;
            AutomaticCompletedGroups = 0;
            automaticExperimentRoutine = StartCoroutine(GuardAutomaticExperiment());
        }

        // Flatten nested iterators so every exception shuts down output and releases controls.
        private IEnumerator GuardAutomaticExperiment()
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(RunAutomaticExperiment());
            while (automaticExperimentRunning && stack.Count > 0)
            {
                object next = null;
                Exception failure = null;
                try
                {
                    var current = stack.Peek();
                    if (!current.MoveNext()) stack.Pop();
                    else next = current.Current;
                }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    if(failure is InvalidOperationException)Debug.LogWarning(failure.Message);else Debug.LogException(failure);
                    FinishAutomaticExperiment("自动实验中止：" + failure.Message, false);
                    yield break;
                }
                if (next is IEnumerator nested) stack.Push(nested);
                else if (stack.Count > 0 && automaticExperimentRunning) yield return next;
            }
        }

        private void StopAutomaticExperiment(string message)
        {
            if (automaticExperimentRoutine != null) StopCoroutine(automaticExperimentRoutine);
            FinishAutomaticExperiment(message, false);
        }

        private void SetAutomaticStatus(string message)
        {
            AutomaticExperimentStatus = message;
            if (automaticProgressText != null) automaticProgressText.text = message;
            if (automaticExperimentButton != null)
                automaticExperimentButton.text = LessonActive?"返回学生实验":automaticExperimentRunning ? "停止自动实验" : ValidationLaunch?"自动实验":"观看演示";
            Refresh(message);
        }

        private IEnumerator WaitAutomaticZero()
        {
            Circuit.TurnOff();
            ExperimentModel.InvalidateSampling();
            Refresh();
            float deadline = Time.realtimeSinceStartup + 12f;
            while (ExperimentModel.ActualIsMilliamps > 0f || ExperimentModel.ActualImAmps > 0f)
            {
                if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("电流归零超时");
                yield return null;
            }
        }

        private IEnumerator RunAutomaticExperiment()
        {
            ExperimentModel.TrackOperation("自动实验", "开始；保留已有数据并创建独立批次");
            PhysicalModelEnabled = true;
            yield return ExplainStep("先观察装置：左侧测试仪，右侧实验箱。每步点击下一步，也可连续播放。",0);
            SetAutomaticStatus("自动实验：归零断电，准备标准接线");
            yield return WaitAutomaticZero();
            SelectedPin = null;
            Circuit.Clear();
            for (int i = 0; i < 3; i++) Circuit.SetSwitch(i, 1);
            ShowTab(0);
            for (int pin = 0; pin < 6; pin++)
            {
                yield return ExplainStep("连接第 "+(pin+1)+"/6 根："+THHCircuit.Name((Pin)pin)+" → "+THHCircuit.Name((Pin)(pin+6))+"。同名同极相接；IS供样品、V测电压、IM供线圈。",0,(Pin)pin,(Pin)(pin+6));
                if (!Circuit.Add((Pin)pin, (Pin)(pin + 6), out string reason))
                    throw new InvalidOperationException(reason);
                SetAutomaticStatus("自动接线 " + (pin + 1) + "/6 · 厂家内部线保持连接");
                Refresh(null, true);
                yield return new WaitForSecondsRealtime(.15f);
            }
            if (!Circuit.Evaluate().Ready) throw new InvalidOperationException("标准回路检查未通过");
            yield return ExplainStep("六根线已接好。检查三路隔离、刀闸方向和两路电流归零，再接通输出。",2);
            ShowTab(3);
            float[][] points = { new[] { .6f, 1.2f, 1.8f, 2.4f, 3f, 3.6f }, new[] { .1f, .2f, .3f, .4f, .5f, .6f } };
            for (int sweep = 0; sweep < 2; sweep++)
            {
                bool sweepIs = sweep == 0;
                if (!ExperimentModel.SelectSweep(sweepIs) || !ExperimentModel.NewSeries())
                    throw new InvalidOperationException(ExperimentModel.LastRecordMessage);
                for (int point = 0; point < 6; point++)
                    yield return RunAutomaticPoint(sweepIs, point,
                        sweepIs ? points[sweep][point] : 3f, sweepIs ? .6f : points[sweep][point]);
            }
            if (!ExperimentModel.SaveData()) throw new InvalidOperationException(ExperimentModel.LastStorageError);
            yield return ExplainStep("两个扫描共12组完成。每组四方向消除部分附加电压，分别按批次拟合。返回学生实验后请亲手完成。",0);
            FinishAutomaticExperiment("自动实验完成 · 12/12 组已保存\nVH-IS 与 VH-IM 各 6 组，可在数据页切换查看", true);
            if (!dataVisible) ToggleDataPanel();
            BrowseDataSweep(true);
        }

        private IEnumerator RunAutomaticPoint(bool sweepIs, int index, float isValue, float imValue)
        {
            string table = sweepIs ? "VH-IS" : "VH-IM";
            int before = ExperimentModel.Rows.Count;
            for (int slot = 0; slot < 4; slot++)
            {
                SetAutomaticStatus(table + " 第 " + (index + 1) + "/6 点 · 方向 " + (slot + 1) +
                    "/4 · 总进度 " + AutomaticCompletedGroups + "/12\n归零断电后换向");
                yield return ExplainStep("换向前先将两路电流归零并关闭输出，等待实际电流衰减到零。",1);
                yield return WaitAutomaticZero();
                yield return ExplainStep("操作实验箱刀闸：IS "+(slot<2?"正向":"反向")+"，IM "+(slot==0||slot==3?"正向":"反向")+"，测量开关置 VH。",2);
                Circuit.SetSwitch(0, slot < 2 ? 1 : -1);
                Circuit.SetSwitch(2, slot == 0 || slot == 3 ? 1 : -1);
                Circuit.SetSwitch(1, 1);
                Refresh();
                yield return ExplainStep("点击测试仪 POWER 接通输出，此时 IS、IM 设定均为零。",1);
                if (!Circuit.TurnOn(out string reason)) throw new InvalidOperationException(reason);
                Refresh();
                yield return ExplainStep("调节测试仪旋钮至 IS="+isValue.ToString("0.00")+" mA，IM="+imValue.ToString("0.000")+" A，观察显示逐渐稳定。",1);
                if(LessonActive)yield return DemonstrateCurrentAdjustment(isValue,imValue);
                else Circuit.SetCurrent(isValue, imValue);
                ExperimentModel.InvalidateSampling();
                SetAutomaticStatus(table + " 第 " + (index + 1) + "/6 点 · 方向 " + (slot + 1) +
                    "/4 · 总进度 " + AutomaticCompletedGroups + "/12\nIS " + isValue.ToString("0.00") +
                    " mA / IM " + imValue.ToString("0.000") + " A · 等待稳定");
                float deadline = Time.realtimeSinceStartup + 15f;
                while (!ExperimentModel.SamplingStable)
                {
                    if (Time.realtimeSinceStartup > deadline) throw new InvalidOperationException("读数稳定超时");
                    if (!Circuit.Powered || !PhysicalModelEnabled) throw new InvalidOperationException("实验条件已改变");
                    yield return null;
                }
                yield return ExplainStep("读数稳定，记录 V"+(slot+1)+"。四次读数必须保持相同电流幅值。",1);
                if (!ExperimentModel.RecordCurrentReading(Circuit))
                    throw new InvalidOperationException(ExperimentModel.LastRecordMessage);
                if (ExperimentModel.HasUnsavedRows) throw new InvalidOperationException("保存失败：" + ExperimentModel.LastStorageError);
                Refresh();
                yield return null;
            }
            if (ExperimentModel.Rows.Count != before + 1) throw new InvalidOperationException("四方向未生成完整数据");
            AutomaticCompletedGroups++;
            yield return WaitAutomaticZero();
        }

        private void FinishAutomaticExperiment(string message, bool success)
        {
            automaticExperimentRunning = false;
            automaticExperimentRoutine = null;
            AutomaticExperimentSucceeded = success;
            Circuit.TurnOff();
            ExperimentModel.InvalidateSampling();
            renderedVersion = -1;
            ExperimentModel.TrackOperation("自动实验", message);
            SetAutomaticStatus(message);
        }
    }
}
