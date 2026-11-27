using System;
using System.Threading;

namespace ZkTimeSync
{
    public class TaskSchedulerService
    {
        private Thread _workerThread;
        private bool _isStopping = false;
        private AppConfig _config;
        private CronExpression _cronExpr;
        private DateTime _lastRunTime = DateTime.MinValue;

        public TaskSchedulerService(AppConfig config)
        {
            _config = config;
            ReloadCron();
        }

        public void ReloadCron()
        {
            try
            {
                if (_config.General.UseCron && !string.IsNullOrWhiteSpace(_config.General.CronExpression))
                {
                    _cronExpr = new CronExpression(_config.General.CronExpression);
                    Logger.Info("加载CRON表达式成功: " + _config.General.CronExpression);
                }
                else
                {
                    _cronExpr = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("解析CRON表达式失败: " + ex.Message);
                _cronExpr = null;
            }
        }

        public void Start()
        {
            _isStopping = false;

            bool startupSyncEnabled = _config.General != null && _config.General.SyncOnStartup;

            // 固定间隔模式下，调度器原本会在启动瞬间立即同步一次；
            // 若已启用"启动自动同步"，则把基准时间设为当前时间，避免同一次启动重复同步两遍。
            if (startupSyncEnabled && !(_config.General.UseCron && _cronExpr != null))
            {
                _lastRunTime = DateTime.Now;
            }

            _workerThread = new Thread(WorkerLoop);
            _workerThread.IsBackground = true;
            _workerThread.Name = "ZkSync_SchedulerThread";
            _workerThread.Start();
            Logger.Info("定时任务调度器已启动。");

            if (startupSyncEnabled)
            {
                var startupThread = new Thread(StartupSyncWorker);
                startupThread.IsBackground = true;
                startupThread.Name = "ZkSync_StartupSyncThread";
                startupThread.Start();
            }
        }

        /// <summary>
        /// 启动自动同步：程序启动/服务器重启后延迟执行一次全员对时，
        /// 用于覆盖"断电恢复后尚未到定时同步时刻"的空档期。
        /// </summary>
        private void StartupSyncWorker()
        {
            int delaySec = _config.General.StartupSyncDelaySeconds;
            if (delaySec < 0) delaySec = 0;

            Logger.Info(string.Format("已启用启动自动同步：{0} 秒后执行首次对时。", delaySec));

            try
            {
                // 分段休眠，便于程序退出时能快速结束线程
                for (int i = 0; i < delaySec && !_isStopping; i++)
                {
                    Thread.Sleep(1000);
                }
            }
            catch { return; }

            if (_isStopping) return;

            // 冷启动判定（断电/服务器重启恢复）：以系统开机时长为准，而非设备上次同步时间。
            // 断电恢复后服务器时钟可能被重置/回退，或与设备时间存在偏差，
            // 若仍用 DateTime.Now - LastSyncTime 判断，会误判为"刚刚同步过"而跳过，
            // 恰恰错过了最需要的一次对时。开机时长由 Windows 维护，重启必然归零，最可靠。
            double bootMinutes = 0;
            bool coldBoot = IsFreshBoot(out bootMinutes);
            if (coldBoot)
            {
                Logger.Info(string.Format("检测到服务器冷启动（开机至今 {0:0.#} 分钟），断电恢复首次对时不受防重复保护限制，强制执行。", bootMinutes));
            }
            else
            {
                // 同一开机周期内程序被反复重启时，才启用防重复保护，避免重复推送通知
                int guardMin = _config.General.StartupSyncGuardMinutes;
                if (guardMin > 0)
                {
                    DateTime? lastSync = GetLatestSyncTimeFromDevices();
                    if (lastSync.HasValue)
                    {
                        double elapsedMin = (DateTime.Now - lastSync.Value).TotalMinutes;
                        if (elapsedMin >= 0 && elapsedMin < guardMin)
                        {
                            Logger.Info(string.Format("启动自动同步已跳过：最近一次同步为 {0:yyyy-MM-dd HH:mm:ss}（{1:0.#} 分钟前），未超过保护阈值 {2} 分钟。",
                                lastSync.Value, elapsedMin, guardMin));
                            return;
                        }
                    }
                }
            }

            Logger.Info("触发程序启动自动同步（防止断电/重启错过定时同步时间）。");
            SyncEngine.ExecuteSync(_config);
        }

        /// <summary>
        /// 判断是否处于服务器冷启动后的窗口期。Environment.TickCount 是系统启动至今的
        /// 毫秒数（49.7 天溢出回绕，用无符号转换规避），重启后必然归零，不受系统时钟
        /// 被重置/回退的影响，是断电恢复场景最可靠的判据。
        /// </summary>
        private bool IsFreshBoot(out double bootMinutes)
        {
            uint tick = unchecked((uint)Environment.TickCount);
            bootMinutes = tick / 60000.0;

            int windowMin = _config.General != null ? _config.General.BootSyncWindowMinutes : 2;
            if (windowMin <= 0) return false; // 0 = 关闭冷启动强制同步
            return bootMinutes <= windowMin;
        }

        /// <summary>
        /// 取所有记忆设备中最新的同步时间（无论成功失败，只要执行过就算），
        /// 用于判断本次启动是否需要补一次同步。
        /// </summary>
        private DateTime? GetLatestSyncTimeFromDevices()
        {
            if (_config.Devices == null || _config.Devices.Count == 0) return null;

            DateTime latest = DateTime.MinValue;
            foreach (var dev in _config.Devices)
            {
                if (dev == null || string.IsNullOrWhiteSpace(dev.LastSyncTime)) continue;

                DateTime t;
                if (DateTime.TryParse(dev.LastSyncTime.Trim(), out t) && t > latest)
                {
                    latest = t;
                }
            }
            return latest == DateTime.MinValue ? (DateTime?)null : latest;
        }

        public void Stop()
        {
            _isStopping = true;
            if (_workerThread != null)
            {
                _workerThread.Join(1500);
            }
            Logger.Info("定时任务调度器已停止。");
        }

        private void WorkerLoop()
        {
            while (!_isStopping)
            {
                try
                {
                    DateTime now = DateTime.Now;

                    if (_config.General.UseCron && _cronExpr != null)
                    {
                        // 保证同一分钟内不重复触发（比较完整时间，避免仅比较分钟数导致跨小时误判）
                        if (now.Second == 0 && (now - _lastRunTime).TotalSeconds >= 60 && _cronExpr.IsTimeToRun(now))
                        {
                            _lastRunTime = now;
                            Logger.Info("触发 CRON 预定时间: " + now.ToString("yyyy-MM-dd HH:mm:ss"));
                            ThreadPool.QueueUserWorkItem(state => SyncEngine.ExecuteSync(_config));
                        }
                    }
                    else
                    {
                        // 固定分钟间隔模式
                        int intervalMin = _config.General.SyncIntervalMinutes > 0 ? _config.General.SyncIntervalMinutes : 60;
                        if (_lastRunTime == DateTime.MinValue || (now - _lastRunTime).TotalMinutes >= intervalMin)
                        {
                            _lastRunTime = now;
                            Logger.Info(string.Format("触发定时同步 (每隔 {0} 分钟)", intervalMin));
                            ThreadPool.QueueUserWorkItem(state => SyncEngine.ExecuteSync(_config));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("定时调度检查异常: " + ex.Message);
                }

                Thread.Sleep(1000); // 每秒检查一次
            }
        }
    }
}
