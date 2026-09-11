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
        /// 同步时间到设备
        /// </summary>
        public bool SyncTime(out string errorMsg)
        {
            errorMsg = "";
            if (!_connected || _zkInstance == null)
            {
                errorMsg = "设备未连接";
                return false;
            }

            try
            {
                // SetDeviceTime(int dwMachineNumber) 默认将设备时间同步为当前电脑系统时间
                object res = _zkType.InvokeMember("SetDeviceTime", BindingFlags.InvokeMethod, null, _zkInstance, new object[] { 1 });
                bool success = (res != null && (bool)res);

                if (!success)
                {
                    // 也可以尝试 SetDeviceTime2(int dwMachineNumber, int dwYear, int dwMonth, int dwDay, int dwHour, int dwMinute, int dwSecond)
                    DateTime now = DateTime.Now;
                    object res2 = _zkType.InvokeMember("SetDeviceTime2", BindingFlags.InvokeMethod, null, _zkInstance,
                        new object[] { 1, now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second });
                    success = (res2 != null && (bool)res2);
                }

                if (!success)
                {
                    int errorCode = 0;
                    object[] errArgs = new object[] { errorCode };
                    _zkType.InvokeMember("GetLastError", BindingFlags.InvokeMethod, null, _zkInstance, errArgs);
                    errorCode = (int)errArgs[0];
                    errorMsg = "SetDeviceTime 失败，错误代码: " + errorCode;
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
