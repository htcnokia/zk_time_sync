# 中控考勤/门禁全自动时间同步系统 (ZkTimeSync)

本项目是专为**中控 (ZKTeco) 考勤机、门禁一体机**开发的高可靠性、全自动化时间同步工具。
完全遵循 **Windows + VS Code + C# 5.0** 开发标准，零第三方厚重外部依赖，直接支持通过系统内置的 `csc.exe` + `.bat` 脚本一键免 VS IDE 编译打包！

---

## 🛠 功能特性全集

1. **环境与原生兼容性**
   - 目标语言：**C# 5.0**，兼容 `.NET Framework 4.0 / 4.5+`。
   - 采用原生 **WinForms + Windows Service** 双模无缝融合设计。
   - 编译方式：提供 `build.bat`，直接调用 `csc.exe` 批量编译，不需要安装体量庞大的 Visual Studio IDE。
   - 架构强锁定为 `x86 (32位)`，彻底解决中控原生 32 位底层 COM `zkemkeeper.dll` 在 64 位系统上的互操作兼容难题。

2. **中控底层 SDK (zkemkeeper.dll) 动态深度适配**
   - 采用 Late-Binding 反射动态加载技术，编译期**零依赖外部 Interop 程序集**。
   - 自动获取本机 `zkemkeeper.ZKEM` COM 实例，调用 `SetCommPassword`、`Connect_Net`、`SetDeviceTime` / `SetDeviceTime2`、`RefreshData` 完成毫秒级高精度对时。

3. **双运行模式：桌面程序 与 Windows 系统服务（Windows Server 推荐）**
   - **交互桌面模式**：Windows 11 Fluent Design 浅色界面，标准窗口边框，支持一键手工执行、实时日志滚屏、扫描进度条、参数即时保存。
   - **Windows 服务运行**：以 `LocalSystem` 账户后台无头运行，**不依赖任何用户登录**，断电重启后自动拉起并对时。
   - **服务加固配置**：安装时自动配置 `start= delayed-auto`（延迟自动启动等网络就绪）、`depend= Tcpip/Dnscache`（网络依赖）、failure 动作（崩溃后自动重启）。
   - **冷启动强制对时**：以系统开机时长判定断电恢复，重启后窗口期内（默认 2 分钟）强制执行一次全员对时，不受防重复保护误伤；该机制以 Windows 开机时长为准，即使服务器时钟被重置也可靠。
   - **STA 线程保证**：同步引擎内部强制在 STA 单元创建中控 COM 组件，规避服务/定时线程默认 MTA 导致的调用失败。
   - **服务管理脚本**：`service_manager.bat`（需管理员运行）提供安装/修复、启动、停止、状态查询、卸载、COM 注册菜单。
   - **桌面登录自启**（可选）：写入当前用户注册表 `Run` 项，仅用于桌面模式，服务模式无需此项。

4. **智能设备网络扫描与自记忆库**
   - **CIDR 高级语法解析**：支持 `192.168.0.0/24, 192.168.2.0/24`、`192.168.0.1-192.168.100.254` 范围混合输入。
   - **MSSQL 数据库自动拉取**：可直连中控考勤软件（如 ZKBioSecurity / 考勤管理系统）后台 SQL Server 数据库，动态查询活跃设备 IP、端口及密码。
   - **一键立即导入**：点击「📥 立即导入」可跳过网络扫描，直接把 SQL 数据库中的设备按 IP 去重合并进记忆设备库。
   - **自动导入**：勾选「自动导入」后，每次自动同步执行前会先同步 SQL 后台设备信息（新增设备、刷新已有设备参数，按 IP 去重）。
   - **多并发探测**：支持多线程并发 Ping 探测 + TCP 4370 握手 + 中控 UDP 4370 特征包 (`0x50 0x50`) 指纹识别。
   - **设备记忆库**：发现或手工添加的设备自动持久化存储在 `config.json` 中，并保存每次同步的状态与时间。

5. **精细设备通讯参数**
   - 支持全局默认通讯端口（默认 4370）、默认 CommKey。
   - 支持单设备自定义覆盖（不同机器可指定各自独立的端口与通信密码）。

6. **定时调度系统与 CRON 表达式**
   - 内置轻量级 CRON 引擎，支持秒、分、时、日、月、周（如 `0 0 2 * * ?` 每天凌晨2点执行）。
   - 图形化界面实时配置与验证，同时也支持简易的“每 N 分钟”执行模式。

7. **企业微信消息推送（自建应用 + 群机器人 Webhook 双模式）**
   - **内容格式**：
     - 首部自定义样式/字符定义
     - 汇总统计（总设备数、成功数、失败数、同步时间）
     - 成功设备详细列表
     - 失败设备详细列表（附带具体错误原因：如超时、密码错误等）
     - 尾部自定义样式/字符（如运维排查指南或提示）
   - **双协议支持**：
     - 群聊机器人：Webhook 模式（输入 Webhook URL 即可直接推送）。
     - 自建应用：支持填入 `CorpId`、`CorpSecret`、`AgentId`、`ToUser`，自动获取与刷新 AccessToken 进行定向安全推送。

---

## 📁 目录文件结构

```text
zk_timesync/
├── build.bat                   # 一键编译脚本 (自动定位 csc.exe 并编译出可执行文件)
├── service_manager.bat         # Windows 服务安装/卸载/启动及 DLL 注册管理脚本
├── config.json                 # 核心持久化配置文件 (设备库、定时参数、企业微信配置)
├── README.md                   # 详细架构与操作使用说明书
└── src/
    ├── Models.cs               # 强类型数据模型定义
    ├── ConfigManager.cs        # JSON配置读写、格式化器与多级别分卷文件日志引擎
    ├── ZkDeviceHelper.cs       # zkemkeeper.dll COM 组件 Late-Binding 反射互操作包装器
    ├── DeviceScanner.cs        # CIDR/网段范围解析器、MSSQL查询器、中控指纹扫描器
    ├── CronExpression.cs       # 兼容标准的 5/6 段 CRON 表达式解析与时间计算器
    ├── SyncEngine.cs           # 核心时间同步引擎 (多设备轮询、错误收集、状态更新、触发推送)
    ├── TaskSchedulerService.cs # 定时工作线程守护服务
    ├── WeComNotifier.cs        # 企业微信群机器人 Webhook 与自建应用 API 推送模块
    ├── MainForm.cs             # Fluent Design 图形化控制台（标准 Windows 窗口）
    ├── UiTheme.cs              # Fluent 设计令牌与自绘控件（按钮/卡片/导航/日志台）
    └── Program.cs              # 程序入口、Windows 服务与加固安装器、命令行参数解析
```

---

## 🚀 编译与使用步骤

### 1. 编译生成 exe
在当前目录下双击运行 `build.bat`，脚本将自动查找系统中的 `C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe` 并编译生成：
`bin\ZkTimeSync.exe`

### 2. 运行环境配置 (中控 SDK)
中控考勤/门禁依赖 32 位的 `zkemkeeper.dll` 以及相关的底层通信库（如 `commpro.dll`、`comms.dll` 等）：
1. 项目已随附完整 SDK 动态库：32 位版位于 `sdk_x86\` 目录，64 位版位于 `sdk_x64\` 目录（本程序为 32 位，使用 `sdk_x86`）。
2. 以管理员权限运行 `service_manager.bat` 并选择选项 `[3]` 自动注册 `sdk_x86\zkemkeeper.dll`，或在命令行手动运行：
   ```cmd
   regsvr32 sdk_x86\zkemkeeper.dll
   ```
   （64 位系统请使用 `C:\Windows\SysWOW64\regsvr32.exe sdk_x86\zkemkeeper.dll` 注册 32 位组件。）

### 3. 作为桌面程序使用
双击 `bin\ZkTimeSync.exe` 即可打开图形界面：
- **记忆设备管理**：查看所有受控考勤机，支持手工增删改。
- **局域网扫描/数据库导入**：输入网段（如 `192.168.1.0/24`）或配置 SQL Server 连接串，一键并发扫描，扫描出的设备勾选后即可导入记忆库；也可点击「📥 立即导入」跳过扫描直接把 SQL 设备按 IP 去重导入记忆库，或勾选「自动导入」让每次自动同步前自动刷新 SQL 设备信息。
- **自动同步与定时任务**：图形化开启 CRON 定时对时或间隔分钟，支持一键勾选开机自启。
- **企业微信通知设置**：配置 Webhook 或自建应用，自定义头部/尾部文案，支持一键发送测试消息。
- **运行日志与实时监控**：可点击“立即执行全员时间同步任务”，实时查看各考勤机对时日志。

### 4. 作为 Windows 服务长期无头运行（Windows Server 推荐）

> 服务以 **LocalSystem** 账户运行，**无需任何用户登录**，非常适合机房服务器断电恢复场景。

**第 1 步（仅首次）：注册中控 COM 组件到机器级**
以管理员身份运行 `service_manager.bat` → 选择 `[6]` 注册中控 SDK COM 组件。
服务运行在 LocalSystem 账户下，必须把 `zkemkeeper.dll` 注册到 HKLM（机器级），否则服务加载 COM 会失败。

**第 2 步：安装服务**
`service_manager.bat` → `[1]` 安装/修复系统服务。该操作会自动完成：
- 幂等安装（已存在则先删除旧服务，避免残留旧路径）
- `start= delayed-auto`：延迟自动启动，等网络/磁盘就绪
- `depend= Tcpip/Dnscache`：显式网络依赖
- `failure ... restart/60000/restart/60000/restart/300000`：服务异常退出后自动重启

等价命令行（管理员 CMD）：
```cmd
bin\ZkTimeSync.exe -install
```

**第 3 步：启动并验证**
`service_manager.bat` → `[2]` 启动服务、`[4]` 查询状态。等价命令：
```cmd
net start ZkTimeSyncService
bin\ZkTimeSync.exe -status
```

**断电恢复自动对时的完整链路**
1. 服务器断电重启 → Windows 拉起 `ZkTimeSync` 服务（LocalSystem，无需登录）。
2. 延迟自动启动 + 网络依赖确保网络栈就绪后才运行。
3. 服务 `OnStart` 启动调度器；若配置开启了"启动自动同步"，延迟 N 秒（默认 30）后执行首次对时。
4. 由于检测到"服务器刚开机"（开机时长在默认 2 分钟窗口内），此次对时**强制跳过防重复保护**，确保断电后的第一件事就是把正确时间写入所有设备。
5. 之后按 CRON / 固定间隔继续定时对时；结果写入 `bin\logs\log_yyyyMMdd.txt`，并按配置推送企业微信。

**验证方法**
- 在图形界面"自动同步"页确认：程序启动后自动执行一次全员对时 = 开启；冷启动判定窗口 = 2 分钟；防重复保护 = 5 分钟。
- 重启服务器（或在 CMD 执行 `shutdown /r /t 0`），**不登录任何用户**，等待约 1 分钟后查看 `bin\logs\` 当日日志，应出现"检测到服务器冷启动 ... 强制执行"及随后的设备同步记录。
- 若服务被系统策略拦截未能启动，检查 Windows 事件查看器 → Windows 日志 → 系统，筛选来源 `Service Control Manager`。

**卸载服务**
`service_manager.bat` → `[5]`，或管理员 CMD 执行 `bin\ZkTimeSync.exe -uninstall`。

**命令行参数一览**
| 参数 | 说明 |
|------|------|
| (无) | 启动图形界面 |
| `-service` | 以 Windows 服务方式运行（供 SCM 调用） |
| `-install` / `-i` | 安装/修复系统服务（需管理员） |
| `-uninstall` / `-u` | 卸载系统服务（需管理员） |
| `-status` / `-st` | 查询服务运行状态 |
| `-sync` | 执行一次静默同步后退出 |
