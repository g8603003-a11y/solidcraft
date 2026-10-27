# SolidCraft.Net — C# SDK

透過 SolidCraft 桌面版的**控制通道**（`127.0.0.1:27878`，一行一個 JSON，見 `docs/control-protocol.md`）
操作 SolidCraft：執行建模指令、讀取設計、追蹤變更。核心是 Rust，公司系統整合與自動化放在 C#。

- 不需修改 SolidCraft；桌面版啟動時控制通道預設開啟（系統選項 › 助理 › 允許 AI 代理連線）。
- .NET 8，只用 `System.Text.Json`，沒有其他相依套件。
- 每次啟動的金鑰在 `%LOCALAPPDATA%\SolidCraft\control\<port>.token`，只有同一位使用者讀得到；SDK 會自動尋找。

## 編譯與試跑

先啟動 SolidCraft，再按兩下 `csharp\build-csharp.cmd`：沒有 .NET 8 SDK 會自動安裝，編譯後執行範例
（`SolidCraft.Sample`）：建一個參數化板件、量測、改參數、設定零件號碼、讀 BOM、匯出 STEP、彩現 PNG。
記錄在 `csharp\build.log`。

手動：

```
dotnet build csharp/SolidCraft.Net.sln -c Release
dotnet run --project csharp/SolidCraft.Sample
```

## 用法

```csharp
using SolidCraft;

await using var app = await SolidCraftClient.ConnectAsync();   // 自動找到執行中的 SolidCraft

// 強型別輔助 API
await app.Files.NewAsync("Bracket");
await app.Parameters.AddAsync("L", "80 mm");
await app.Sketch.CreateAsync("XY", name: "Base");
var rect = await app.Sketch.RectangleAsync((0, 0), (40, 25));
await app.Sketch.DimensionAsync(rect.Sides!["bottom"], "L");
await app.Sketch.FinishAsync();
await app.Solid.ExtrudeAsync(6, Operation.New, name: "Plate");
await app.Solid.FilletAsync(new Point3[] { (40, 25, 3) }, radius: 5);

var bodies = await app.Inspect.MeasureAsync();                 // 體積、質量、質心、面數
await app.Parameters.SetAsync("L", "120 mm");                  // 模型重建
await app.Files.ExportStepAsync(@"C:\out\plate.step");

// 任何指令：Commands.* 列出全部 479 個指令，IntelliSense 顯示參數說明
var r = await app.ExecuteAsync(Commands.Solid.Shell, new { faces = new[] { new[] { 0.0, 0.0, 6.0 } }, thickness = 1.5 });

// 多個指令一次送（一個畫面幀內執行，失敗自動回復）
await app.BatchAsync(new[]
{
    new CommandCall(Commands.Sketch.Create, new { plane = "XY" }),
    new CommandCall(Commands.Sketch.CircleCenter, new { center = new[] { 0.0, 0.0 }, radius = 10 }),
    new CommandCall(Commands.Sketch.Finish),
    new CommandCall(Commands.Solid.Extrude, new { distance = 20 }),
});

// 追蹤設計變更（ERP 回寫、即時同步）
app.DesignChanged += (_, e) => Console.WriteLine($"revision {e.Revision.Revision}");
await app.SubscribeAsync();
```

### 類別

| 類別 | 內容 |
|---|---|
| `SolidCraftClient` | 連線、`ExecuteAsync`、`BatchAsync`、`ScriptAsync`、`InspectDocumentAsync`、`GetRevisionAsync`、`SubscribeAsync`、`ListCommandsAsync` |
| `app.Sketch` | 草圖：建立／完成、直線、矩形、圓、弧、槽、多邊形、文字、尺寸、限制條件 |
| `app.Solid` | 伸長、旋轉、方塊、圓柱、圓角、導角、薄殼、孔、螺紋、複製排列、鏡射、組合、時間軸（抑制、編輯、退回）、復原 |
| `app.Parameters` | 新增、設定（單個或多個一次重建）、列出、匯入／匯出 |
| `app.Files` | 新設計、開啟、儲存、匯出 STEP/STL/3MF/OBJ、插入 STEP、平板型式 DXF |
| `app.Inspect` | 量測、零組件物理屬性、BOM、干涉檢查、本體拓樸 |
| `app.Design` | 設計屬性（零件號碼、版次、專案…，可放公司自訂鍵）、單位、材料、零組件 |
| `app.Ui` | 視角、相機、截圖、彩現、開啟對話方塊（`StartAsync`）、按鍵／輸入 |
| `Commands` | 由 `tools/gen_commands.py` 從 Rust 原始碼產生的全部指令常數與參數說明 |

### 長時間指令

App 對一個請求最多等 60 秒（批次 3600 秒），之後回「仍在執行」。SDK 預設（`WaitForLongRunning = true`）
會透過 `control.pending` 繼續等真正的結果，所以 `await` 一定拿到結果或例外；不會重送。

### 錯誤

指令失敗丟 `SolidCraftException`（`Command`、`Error`、`IsBusy`、`IsStopped`）；批次中途失敗丟
`SolidCraftBatchException`（`Result.Failed.Index` 是哪一步、設計是否已回復）；找不到或連不上 App 丟
`SolidCraftConnectException`。

### 同時多個連線

一條連線一次跑一個請求（App 本身也是依序執行）。要平行讀取（例如一邊監看一邊操作），開第二個
`SolidCraftClient`；App 最多服務 16 條連線。

## 更新上游後重新產生指令表

```
python3 csharp/tools/gen_commands.py          # 重寫 SolidCraft.Net/Commands.g.cs
python3 csharp/tools/gen_commands.py --check  # CI：檔案過期時失敗
```

## 檔案

```
csharp/
  SolidCraft.Net/            類別庫
    SolidCraftClient.cs      連線、協定、事件
    ControlDiscovery.cs      尋找連接埠與金鑰檔
    Models.cs                回覆型別、Expr／Point2／Point3
    SketchCommands.cs        草圖
    SolidCommands.cs         特徵、時間軸
    ParameterCommands.cs     參數
    FileCommands.cs          檔案、量測／BOM、設計屬性
    UiCommands.cs            視窗
    Commands.g.cs            （產生）指令常數
  SolidCraft.Sample/         範例主控台程式
  tools/gen_commands.py      指令常數產生器
  build-csharp.cmd           一鍵編譯與試跑
```
