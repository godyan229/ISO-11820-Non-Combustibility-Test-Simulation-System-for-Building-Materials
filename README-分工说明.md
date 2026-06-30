# ISO 11820 不燃性试验仿真系统 — 四人分工

## 分工总览

| 成员 | 负责层 | 文件数 | 核心职责 |
|:--:|------|:--:|------|
| **张宏生** | 数据与模型层 | 11 | 7个实体类 + SQLite 6表CRUD + 全局单例 + 配置文件 |
| **卢昕泽** | 业务逻辑层 | 4 | 5状态机 + 5通道4阶段温度仿真 + 数据采集定时器 |
| **晏玉宁** | 用户界面层 | 5 | 登录/主界面/新建试验/现象记录/详情 5个Form |
| **周鹏** | 输出与系统层 | 5 | CSV/Excel/PDF 导出 + 程序入口 + 自动化测试 + 项目文件 |

## 各成员详细说明

### 张宏生 — 数据与模型层（Models + Data + Global）
**文件**：`Models/*.cs`（7个）、`Data/DbHelper.cs`、`Global/AppContext.cs`、`appsettings.json`

| 文件 | 说明 |
|------|------|
| `Operator.cs` | 操作员实体（用户名/密码/角色） |
| `ProductMaster.cs` | 样品主数据（编号/名称/规格/尺寸） |
| `Apparatus.cs` | 设备信息（编号/名称/检定日期/恒功率） |
| `SensorConfig.cs` | 传感器配置（通道ID/量程） |
| `TestMaster.cs` | **试验记录核心实体**（30字段：温升/失重率/火焰/判定...） |
| `CalibrationRecord.cs` | 校准记录 |
| `MasterMessage.cs` | 系统消息 |
| `DbHelper.cs` | **SQLite 数据库操作**：6表自动建表、种子数据（默认用户/设备/传感器）、参数化SQL CRUD、多条件查询 |
| `AppContext.cs` | **全局单例**：持有 Configuration + DbHelper + CurrentUser |
| `appsettings.json` | 配置文件：数据库路径/仿真参数/文件存储路径 |

**答辩要点**：说明6张表的设计思路（为什么分这些表）、参数化SQL防注入、单例模式的作用

---

### 卢昕泽 — 业务逻辑层（Core + SensorSimulator + DaqWorker）
**文件**：`Core/TestController.cs`、`Core/DataBroadcastEventArgs.cs`、`Services/SensorSimulator.cs`、`Services/DaqWorker.cs`

| 文件 | 说明 |
|------|------|
| `TestController.cs` | **5状态机**（Idle→Preparing→Ready→Recording→Complete）+ 终止条件检查 + 温漂计算 |
| `DataBroadcastEventArgs.cs` | 事件数据载体（温度/状态/曲线/消息） |
| `SensorSimulator.cs` | **五通道四阶段温度仿真引擎**：Heating(线性+噪声)→Stable(750°C钳位)→Recording(指数趋近)→Cooling |
| `DaqWorker.cs` | 800ms定时器驱动仿真循环（支持仿真/硬件双模切换） |

**答辩要点**：四阶段算法的物理依据、状态机自动转换条件、噪声模型的设计

---

### 晏玉宁— 用户界面层（Forms）
**文件**：`Forms/LoginForm.cs`、`Forms/MainForm.cs`、`Forms/NewTestForm.cs`、`Forms/ObservationForm.cs`、`Forms/TestDetailForm.cs`

| 文件 | 说明 |
|------|------|
| `LoginForm.cs` | 角色选择（单选按钮）+ 密码验证 → 跳转主界面 |
| `MainForm.cs` | **主窗体**：3Tab页（实时监控/记录查询/设备校准）+ OxyPlot实时曲线 + LED温度面板 + 系统消息 + **后台线程仿真循环 + UI更新** |
| `NewTestForm.cs` | 新建试验：样品信息填写 + 设备信息自动带入 + 时长模式选择 |
| `ObservationForm.cs` | 现象记录：火焰复选框 + 试验后质量 + 自动计算失重率/温升/判定 + 触发三格式导出 |
| `TestDetailForm.cs` | 试验详情弹窗：完整试验信息展示 |

**答辩要点**：AutoScroll处理、按钮状态矩阵（8按钮×5状态互锁）、BeginInvoke跨线程更新

---

### 周鹏— 输出与系统层（Export + Program + Test + Project）
**文件**：`Services/ExportService.cs`、`Program.cs`、`DemoTest.cs`、`ISO11820.csproj`、`appsettings.json`

| 文件 | 说明 |
|------|------|
| `ExportService.cs` | **三格式导出**：CSV(UTF-8 BOM)、Excel(EPPlus三Sheet+嵌入式折线图)、PDF(自研WindowsFontResolver中文字体) |
| `Program.cs` | 程序入口：Serilog日志配置 + AppGlobal初始化 + Application.Run |
| `DemoTest.cs` | **自动化测试框架**：8组42项断言，可脱离GUI一键运行 |
| `ISO11820.csproj` | 项目文件：.NET 8 WinForms + 9个NuGet包 |
| `appsettings.json` | 全局配置 |

**答辩要点**：CSV为什么加BOM、PDF中文字体解决方案（IFontResolver）、自动化测试的设计思路

---

## 依赖关系

```
周鹏（项目入口）
  → 晏玉宁（UI 展示）
    → 卢昕泽（业务逻辑）
      → 张宏生（数据模型）

各层通过接口/事件解耦，可独立讲解和演示。
