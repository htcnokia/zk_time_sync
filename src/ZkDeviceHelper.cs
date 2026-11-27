using System;
using System.Reflection;

namespace ZkTimeSync
{
    /// <summary>
    /// 中控 SDK zkemkeeper.dll COM 动态反射包装器
    /// 使用 late-binding (Activator.CreateInstance + Type.GetTypeFromProgID)
    /// 无论开发机是否安装注册了 zkemkeeper，只要运行时系统注册了该 COM 组件即可正常调用。
    /// 编译时无需依赖外部 Interop.zkemkeeper.dll，用 csc.exe 即可直接编译！
    /// </summary>
    public class ZkDeviceHelper : IDisposable
    {
        private object _zkInstance;
        private Type _zkType;
        private bool _connected = false;

        public bool IsConnected
        {
            get { return _connected; }
        }

        public ZkDeviceHelper()
        {
            try
            {
                _zkType = Type.GetTypeFromProgID("zkemkeeper.ZKEM");
                if (_zkType == null)
                {
                    // 尝试通过 CLSID 或者查找注册表
                    _zkType = Type.GetTypeFromCLSID(new Guid("00853A19-BD51-419B-9269-2DABE57EB61F"));
                }

                if (_zkType != null)
                {
                    _zkInstance = Activator.CreateInstance(_zkType);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("初始化 zkemkeeper COM 失败: " + ex.Message + " (请确认是否已以管理员权限运行 regsvr32 zkemkeeper.dll)");
            }
        }

        public bool Connect(string ip, int port, int commKey, int timeoutMs)
        {
            if (_zkInstance == null || _zkType == null)
            {
                Logger.Error("zkemkeeper 组件未注册或初始化失败，无法连接 " + ip);
                return false;
            }

            try
            {
                // 如果设置了通信密码
                if (commKey > 0)
                {
                    _zkType.InvokeMember("SetCommPassword", BindingFlags.InvokeMethod, null, _zkInstance, new object[] { commKey });
                }

                // Connect_Net(string IPAdd, int Port)
                object result = _zkType.InvokeMember("Connect_Net", BindingFlags.InvokeMethod, null, _zkInstance, new object[] { ip, port });
                _connected = (result != null && (bool)result);

                if (!_connected)
                {
                    int errorCode = 0;
                    object[] errArgs = new object[] { errorCode };
                    _zkType.InvokeMember("GetLastError", BindingFlags.InvokeMethod, null, _zkInstance, errArgs);
                    errorCode = (int)errArgs[0];
                    Logger.Warn(string.Format("连接设备 {0}:{1} 失败，错误码: {2}", ip, port, errorCode));
                }
                return _connected;
            }
            catch (Exception ex)
            {
                Logger.Error(string.Format("连接设备 {0}:{1} 异常: {2}", ip, port, ex.Message));
                return false;
            }
        }

        /// <summary>
        /// 将指定时间写入设备。
        /// 优先使用 SetDeviceTime2 显式下发目标时间（支持把设备所在地时区的本地时间写入设备）；
        /// 仅当目标时间等同于服务器本机时间且 allowLocalFallback 为真时，
        /// 才回退到 SetDeviceTime(1)（该接口只能按运行主机的系统时间设置，跨时区时会导致时间错误）。
        /// </summary>
        public bool SyncTime(DateTime targetTime, bool allowLocalFallback, out string errorMsg)
        {
            errorMsg = "";
            if (!_connected || _zkInstance == null)
            {
                errorMsg = "设备未连接";
                return false;
            }

            try
            {
                bool success = false;

                // SetDeviceTime2(int dwMachineNumber, int dwYear, int dwMonth, int dwDay, int dwHour, int dwMinute, int dwSecond)
                try
                {
                    object res = _zkType.InvokeMember("SetDeviceTime2", BindingFlags.InvokeMethod, null, _zkInstance,
                        new object[] { 1, targetTime.Year, targetTime.Month, targetTime.Day, targetTime.Hour, targetTime.Minute, targetTime.Second });
                    success = (res != null && (bool)res);
                }
                catch (Exception ex2)
                {
                    Logger.Warn("调用 SetDeviceTime2 异常: " + ex2.Message);
                }

                // 回退：部分老固件不支持 SetDeviceTime2，只能按主机系统时间设置
                if (!success && allowLocalFallback)
                {
                    object res2 = _zkType.InvokeMember("SetDeviceTime", BindingFlags.InvokeMethod, null, _zkInstance, new object[] { 1 });
                    success = (res2 != null && (bool)res2);
                    if (success)
                    {
                        Logger.Warn("设备不支持 SetDeviceTime2，已回退为按服务器本机系统时间设置。");
                    }
                }

                if (!success)
                {
                    int errorCode = 0;
                    object[] errArgs = new object[] { errorCode };
                    _zkType.InvokeMember("GetLastError", BindingFlags.InvokeMethod, null, _zkInstance, errArgs);
                    errorCode = (int)errArgs[0];
                    errorMsg = "设置设备时间失败，错误代码: " + errorCode
                        + (allowLocalFallback ? "" : "（该设备时区与服务器不同，未使用 SetDeviceTime 回退以避免时间错误）");
                    return false;
                }

                // 刷新设备数据以生效
                try
                {
                    _zkType.InvokeMember("RefreshData", BindingFlags.InvokeMethod, null, _zkInstance, new object[] { 1 });
                }
                catch { }

                return true;
            }
            catch (Exception ex)
            {
                errorMsg = "同步异常: " + ex.Message;
                return false;
            }
        }

        public void Disconnect()
        {
            if (_connected && _zkInstance != null && _zkType != null)
            {
                try
                {
                    _zkType.InvokeMember("Disconnect", BindingFlags.InvokeMethod, null, _zkInstance, null);
                }
                catch { }
                _connected = false;
            }
        }

        public void Dispose()
        {
            Disconnect();
            if (_zkInstance != null)
            {
                try
                {
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(_zkInstance);
                }
                catch { }
                _zkInstance = null;
            }
        }
    }
}
