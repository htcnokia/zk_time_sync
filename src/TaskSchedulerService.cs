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
            _workerThread = new Thread(WorkerLoop);
            _workerThread.IsBackground = true;
            _workerThread.Name = "ZkSync_SchedulerThread";
            _workerThread.Start();
            Logger.Info("定时任务调度器已启动。");
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
