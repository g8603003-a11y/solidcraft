// SolidCraft.Net 範例：連線到執行中的 SolidCraft，建立一個參數化的板件，量測、改參數、
// 設定零件號碼、讀 BOM、匯出 STEP、彩現 PNG。
//
// 先啟動 SolidCraft（控制通道預設開啟），再執行：dotnet run --project csharp/SolidCraft.Sample

using System.Globalization;
using System.Text.Json;
using SolidCraft;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

await using var app = await SolidCraftClient.ConnectAsync();
Console.WriteLine($"已連線到 SolidCraft（連接埠 {app.Port}）");

// 訂閱設計變更事件：每次模型改變，App 會主動送一行 design.changed。
app.DesignChanged += (_, e) => Console.WriteLine($"  [事件] 設計 {e.Revision.DocumentId} 變更，修訂 {e.Revision.Revision}");
var start = await app.SubscribeAsync();
Console.WriteLine($"目前設計 {start.DocumentId}，修訂 {start.Revision}");

// 1. 新設計與參數
await app.Files.NewAsync("SDK Sample");
await app.Parameters.AddAsync("L", "80 mm");
await app.Parameters.AddAsync("W", "50 mm");
await app.Parameters.AddAsync("T", "6 mm");
await app.Parameters.AddAsync("inset", "8 mm");

// 2. 一個 batch 建出板件：草圖 → 伸長 → 圓角 → 孔位草圖 → 柱孔 → 直線複製排列
var build = new List<CommandCall>
{
    new(Commands.Sketch.Create, new { plane = "XY", name = "PlateSketch" }),
    new(Commands.Sketch.DrawDimensioned, new
    {
        command = Commands.Sketch.RectangleCenter,
        @params = new { center = new[] { 0.0, 0.0 }, corner = new[] { 40.0, 25.0 } },
        dimensions = new { width = "L", height = "W" },
    }),
    new(Commands.Sketch.Finish),
    new(Commands.Solid.Extrude, new { distance = "T", name = "Plate" }),
    new(Commands.Solid.Fillet, new
    {
        edges = new[] { new[] { 40.0, 25.0, 3.0 }, new[] { -40.0, 25.0, 3.0 }, new[] { 40.0, -25.0, 3.0 }, new[] { -40.0, -25.0, 3.0 } },
        radius = 5,
        name = "Corners",
    }),
    new(Commands.Sketch.Create, new { plane = "XY", name = "HolePts", offset = "T" }),
    new(Commands.Sketch.Point, new { point = new[] { 32.0, 17.0 } }),
    new(Commands.Sketch.Dimension, new { entities = new[] { "origin", "p1" }, type = "horizontal", value = "L/2 - inset" }),
    new(Commands.Sketch.Dimension, new { entities = new[] { "origin", "p1" }, type = "vertical", value = "W/2 - inset" }),
    new(Commands.Sketch.Finish),
    new(Commands.Solid.Hole, new { sketch = "HolePts", diameter = 3.4, type = "counterbore", cb_diameter = 6.5, cb_depth = 3.3, name = "Hole" }),
    new(Commands.Solid.PatternRectangular, new
    {
        features = new[] { "Hole" },
        dir1 = new[] { -1.0, 0.0, 0.0 }, count1 = 2, spacing1 = "L - 2*inset",
        dir2 = new[] { 0.0, -1.0, 0.0 }, count2 = 2, spacing2 = "W - 2*inset",
        name = "Holes",
    }),
};
var result = await app.BatchAsync(build, new BatchOptions { Timings = true });
Console.WriteLine($"批次完成：{result.Completed} 步，{result.TotalMs:F0} ms");

// 3. 量測
foreach (var b in await app.Inspect.MeasureAsync())
{
    Console.WriteLine($"本體 {b.Name}：體積 {b.VolumeMm3:F1} mm³，{b.Faces} 個面，質量 {b.MassG:F2} g（{b.Material}）");
}

// 4. 改參數，模型重建，再量一次
await app.Parameters.SetAsync("L", "120 mm");
var after = await app.Inspect.MeasureAsync();
Console.WriteLine($"L = 120 之後：體積 {after[0].VolumeMm3:F1} mm³（預期 35335.2）");

// 5. 設計屬性（零件號碼、版次），以及材料與 BOM
await app.Design.SetPropertiesAsync(new Dictionary<string, object?>
{
    ["part_number"] = "ACME-1042",
    ["revision"] = "A",
    ["description"] = "SDK sample plate",
    ["com.acme.erp"] = new { item_id = 88123, material_code = "AL6061" },
});
await app.Solid.AssignMaterialAsync(new[] { "Plate" }, "Aluminum");
var props = await app.Design.PropertiesAsync();
Console.WriteLine($"零件號碼 {props.GetValueOrDefault("part_number")}，版次 {props.GetValueOrDefault("revision")}");
foreach (var row in await app.Inspect.BomAsync())
{
    Console.WriteLine($"BOM {row.ItemText}: {row.PartNumber ?? "(無零件號碼)"} ×{row.Count} {row.Description}");
}

// 6. 匯出與彩現到暫存資料夾
var outDir = Path.Combine(Path.GetTempPath(), "solidcraft-sdk-sample");
Directory.CreateDirectory(outDir);
var step = Path.Combine(outDir, "plate.step");
await app.Files.ExportStepAsync(step);
Console.WriteLine($"已匯出 {step}");

await app.Ui.ViewAsync(StandardView.Iso);
await app.Ui.FitAsync();
var png = Path.Combine(outDir, "plate.png");
await app.Ui.RenderAsync(png, 960, 600);
Console.WriteLine($"已彩現 {png}");

// 7. 任何指令都能用 ExecuteAsync 直接呼叫；Commands.* 有全部 479 個指令與參數說明。
var doc = await app.InspectDocumentAsync();
Console.WriteLine($"時間軸特徵數：{doc.GetProperty("timeline").GetArrayLength()}");

Console.WriteLine("完成。");
