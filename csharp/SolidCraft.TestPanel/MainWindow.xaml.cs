using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SolidCraft.TestPanel;

/// <summary>A row of the parameters grid.</summary>
public sealed class ParamRow
{
    public string Name { get; set; } = "";
    public string Expression { get; set; } = "";
    public string Value { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Source { get; set; } = "";
    public string Error { get; set; } = "";
}

/// <summary>A row of the command list.</summary>
public sealed class CommandItem
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Params { get; init; } = "";
    public string Display => $"{Id}  —  {Label}";
}

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions Pretty = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private SolidCraftClient? _app;
    private IReadOnlyList<CommandItem> _commands = Array.Empty<CommandItem>();
    private bool _applyingParameter;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await ConnectAsync();
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
            _app = null;
        }
    }

    // ---- Connection ------------------------------------------------------------------------

    private async void Connect_Click(object sender, RoutedEventArgs e) => await ConnectAsync();

    private async void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
            _app = null;
        }
        SetConnected(false, "已中斷連線。");
    }

    private async Task ConnectAsync()
    {
        if (_app is not null)
        {
            return;
        }
        try
        {
            ConnectButton.IsEnabled = false;
            StatusText.Text = "連線中…";
            var app = await SolidCraftClient.ConnectAsync();
            app.DesignChanged += (_, ev) => Dispatcher.BeginInvoke(() =>
            {
                Log($"design.changed  {ev.Revision.DocumentId}  修訂 {ev.Revision.Revision}");
                StatusText.Text = $"已連線（連接埠 {app.Port}）  設計 {ev.Revision.DocumentId}  修訂 {ev.Revision.Revision}";
            });
            app.EventReceived += (_, ev) =>
            {
                if (ev.Event != "design.changed")
                {
                    Dispatcher.BeginInvoke(() => Log($"{ev.Event}  {ev.Params}"));
                }
            };
            app.Closed += (_, ex) => Dispatcher.BeginInvoke(() =>
            {
                _app = null;
                SetConnected(false, "連線已關閉" + (ex is null ? "。" : "：" + ex.Message));
            });
            var rev = await app.SubscribeAsync();
            _app = app;
            SetConnected(true, "已連線。");
            StatusText.Text = $"已連線（連接埠 {app.Port}）  設計 {rev.DocumentId}  修訂 {rev.Revision}";
            Log($"連線到 127.0.0.1:{app.Port}，設計 {rev.DocumentId} 修訂 {rev.Revision}");
            await LoadCommandsAsync();
            await LoadMaterialsAsync();
            await RefreshParametersAsync();
        }
        catch (Exception ex)
        {
            SetConnected(false, "連線失敗：" + ex.Message);
            StatusText.Text = "尚未連線。請先啟動 SolidCraft，再按「連線」。";
        }
    }

    private void SetConnected(bool connected, string message)
    {
        Tabs.IsEnabled = connected;
        ConnectButton.IsEnabled = !connected;
        DisconnectButton.IsEnabled = connected;
        if (!connected)
        {
            StatusText.Text = "尚未連線。";
        }
        Message(message);
    }

    private SolidCraftClient Client => _app ?? throw new InvalidOperationException("尚未連線到 SolidCraft");

    /// <summary>Run an action against the app, showing errors in the message bar instead of crashing.</summary>
    private async Task Run(Func<Task> action, string? doing = null)
    {
        try
        {
            if (doing is not null)
            {
                Message(doing + "…");
            }
            await action();
            if (doing is not null)
            {
                Message(doing + "：完成");
            }
        }
        catch (SolidCraftBatchException ex)
        {
            Message($"批次第 {ex.Result.Failed?.Index} 步（{ex.Result.Failed?.Command}）失敗：{ex.Result.Failed?.Error}");
        }
        catch (SolidCraftException ex)
        {
            Message("失敗：" + ex.Message);
        }
        catch (Exception ex)
        {
            Message("錯誤：" + ex.Message);
        }
    }

    private void Message(string text)
    {
        MessageText.Text = text;
    }

    private void Log(string line)
    {
        EventLog.Items.Add($"{DateTime.Now:HH:mm:ss}  {line}");
        if (EventLog.Items.Count > 500)
        {
            EventLog.Items.RemoveAt(0);
        }
        EventLog.ScrollIntoView(EventLog.Items[EventLog.Items.Count - 1]);
    }

    private static double Num(TextBox box)
    {
        if (!double.TryParse(box.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
        {
            throw new FormatException($"「{box.Text}」不是數字");
        }
        return v;
    }

    /// <summary>A number or an expression: text that doesn't parse as a number is sent as an expression.</summary>
    private static Expr ExprOf(TextBox box)
    {
        var t = box.Text.Trim();
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? new Expr(v) : new Expr(t);
    }

    private static double[] Triple(TextBox box)
    {
        var parts = box.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3)
        {
            throw new FormatException("請輸入 x,y,z 三個數字");
        }
        return parts.Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();
    }

    private static double[] Pair(TextBox box)
    {
        var parts = box.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            throw new FormatException("請輸入 x,y 兩個數字");
        }
        return parts.Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();
    }

    private static string ComboText(ComboBox box) =>
        box.SelectedItem is ComboBoxItem item ? item.Content?.ToString() ?? "" : box.Text;

    private static Operation OpOf(ComboBox box) => ComboText(box) switch
    {
        "join" => Operation.Join,
        "cut" => Operation.Cut,
        "intersect" => Operation.Intersect,
        "auto" => Operation.Auto,
        _ => Operation.New,
    };

    // ---- Parameters ------------------------------------------------------------------------

    private async void RefreshParameters_Click(object sender, RoutedEventArgs e) => await Run(RefreshParametersAsync);

    private async Task RefreshParametersAsync()
    {
        var list = await Client.Parameters.ListAsync();
        ParamGrid.ItemsSource = list.Select(p => new ParamRow
        {
            Name = p.Name,
            Expression = p.Expression ?? "",
            Value = p.Value.ValueKind == JsonValueKind.Number ? p.Value.GetDouble().ToString("0.###", CultureInfo.InvariantCulture) : p.Value.ToString(),
            Unit = p.Unit ?? "",
            Source = p.Source ?? "",
            Error = p.Error ?? "",
        }).ToList();
    }

    private async void AddParameter_Click(object sender, RoutedEventArgs e)
    {
        var name = NewParamName.Text.Trim();
        var expr = NewParamExpr.Text.Trim();
        if (name.Length == 0 || expr.Length == 0)
        {
            Message("請輸入參數名稱和運算式。");
            return;
        }
        await Run(async () =>
        {
            await Client.Parameters.AddAsync(name, expr);
            await RefreshParametersAsync();
        }, $"新增參數 {name}");
    }

    private async void DeleteParameter_Click(object sender, RoutedEventArgs e)
    {
        if (ParamGrid.SelectedItem is not ParamRow row)
        {
            Message("請先選取一個參數。");
            return;
        }
        await Run(async () =>
        {
            await Client.Parameters.DeleteAsync(row.Name);
            await RefreshParametersAsync();
        }, $"刪除參數 {row.Name}");
    }

    private void ParamGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (_applyingParameter || e.EditAction != DataGridEditAction.Commit || e.Row.Item is not ParamRow row || e.EditingElement is not TextBox box)
        {
            return;
        }
        var text = box.Text.Trim();
        if (text == row.Expression)
        {
            return;
        }
        _ = Dispatcher.BeginInvoke(async () =>
        {
            _applyingParameter = true;
            try
            {
                await Run(async () =>
                {
                    await Client.Parameters.SetAsync(row.Name, text);
                    await RefreshParametersAsync();
                }, $"設定 {row.Name} = {text}");
            }
            finally
            {
                _applyingParameter = false;
            }
        });
    }

    // ---- Modelling -------------------------------------------------------------------------

    private async void NewDesign_Click(object sender, RoutedEventArgs e) =>
        await Run(async () => { await Client.Files.NewAsync(); await RefreshParametersAsync(); }, "新設計");

    private async void BuildSample_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        await Client.Parameters.AddAsync("L", "80 mm");
        await Client.Parameters.AddAsync("W", "50 mm");
        await Client.Parameters.AddAsync("T", "6 mm");
        await Client.Parameters.AddAsync("inset", "8 mm");
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
            new(Commands.Solid.Extrude, new { distance = "T", name = "Plate", body_name = "Plate" }),
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
        var r = await Client.BatchAsync(build, new BatchOptions { Timings = true });
        await Client.Ui.ViewAsync(StandardView.Iso);
        await Client.Ui.FitAsync();
        await RefreshParametersAsync();
        Message($"範例板件：{r.Completed} 步，{r.TotalMs:F0} ms。到「參數」頁改 L 或 W 看模型重建。");
    });

    private async void Undo_Click(object sender, RoutedEventArgs e) => await Run(async () => { await Client.Solid.UndoAsync(); await RefreshParametersAsync(); }, "復原");
    private async void Redo_Click(object sender, RoutedEventArgs e) => await Run(async () => { await Client.Solid.RedoAsync(); await RefreshParametersAsync(); }, "取消復原");

    private async void Box_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var name = BoxName.Text.Trim();
        await Client.Solid.BoxAsync(ExprOf(BoxL), ExprOf(BoxW), ExprOf(BoxH), operation: OpOf(BoxOp), name: name.Length == 0 ? null : name);
        await Client.Ui.FitAsync();
    }, "建立方塊");

    private async void Cylinder_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var b = Triple(CylBase);
        var name = CylName.Text.Trim();
        await Client.Solid.CylinderAsync(ExprOf(CylR), ExprOf(CylH), new Point3(b[0], b[1], b[2]), operation: OpOf(CylOp), name: name.Length == 0 ? null : name);
    }, "建立圓柱");

    private async void CircleExtrude_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var c = Pair(CircleC);
        var offset = ExprOf(SketchOffset);
        await Client.Sketch.CreateAsync(ComboText(SketchPlane), offset.IsNumber && offset.Number == 0 ? (Expr?)null : offset);
        var circle = await Client.Sketch.CircleDiameterAsync(new Point2(c[0], c[1]), ExprOf(CircleD));
        await Client.Sketch.FinishAsync();
        await Client.Solid.ExtrudeAsync(ExprOf(ExtrudeD), OpOf(ExtrudeOp));
        Message($"畫圓 {circle.Curve} 並伸長：完成");
    }, "畫圓並伸長");

    private async void Fillet_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var p = Triple(FilletPt);
        await Client.Solid.FilletAsync(new[] { new Point3(p[0], p[1], p[2]) }, ExprOf(FilletR));
    }, "圓角");

    private async void ViewIso_Click(object sender, RoutedEventArgs e) => await Run(() => Client.Ui.ViewAsync(StandardView.Iso, true));
    private async void ViewFront_Click(object sender, RoutedEventArgs e) => await Run(() => Client.Ui.ViewAsync(StandardView.Front, true));
    private async void ViewTop_Click(object sender, RoutedEventArgs e) => await Run(() => Client.Ui.ViewAsync(StandardView.Top, true));
    private async void ViewFit_Click(object sender, RoutedEventArgs e) => await Run(() => Client.Ui.FitAsync());
    private async void ViewHome_Click(object sender, RoutedEventArgs e) => await Run(() => Client.Ui.ViewAsync(StandardView.Home, true));

    // ---- Measure / materials ---------------------------------------------------------------

    private async void Measure_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var raw = await Client.Inspect.MeasureRawAsync();
        var bodies = raw.TryGetProperty("bodies", out var list) ? Json.To<List<BodyMeasure>>(list) : new List<BodyMeasure>();
        BodyGrid.ItemsSource = bodies;
        if (raw.TryGetProperty("total", out var total))
        {
            MeasureTotal.Text = $"合計：體積 {Json.Number(total, "volume_mm3"):N1} mm³，質量 {Json.Number(total, "mass_g"):N2} g，{bodies.Count} 個本體";
        }
    }, "量測");

    private async Task LoadMaterialsAsync()
    {
        try
        {
            var r = await Client.Design.MaterialsAsync();
            var names = new List<string>();
            if (r.TryGetProperty("materials", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in list.EnumerateArray())
                {
                    var n = Json.String(m, "name");
                    if (!string.IsNullOrEmpty(n)) names.Add(n);
                }
            }
            MaterialBox.ItemsSource = names;
            if (names.Count > 0) MaterialBox.SelectedIndex = 0;
        }
        catch (SolidCraftException)
        {
            MaterialBox.ItemsSource = new[] { "Steel", "Aluminum", "ABS Plastic" };
            MaterialBox.SelectedIndex = 0;
        }
    }

    private async void AssignMaterial_Click(object sender, RoutedEventArgs e)
    {
        var bodies = BodyGrid.SelectedItems.OfType<BodyMeasure>().Select(b => b.Name).ToList();
        var material = MaterialBox.Text.Trim();
        if (bodies.Count == 0 || material.Length == 0)
        {
            Message("請先量測，選取本體，再選材料。");
            return;
        }
        await Run(async () =>
        {
            await Client.Solid.AssignMaterialAsync(bodies, material);
            Measure_Click(sender, e);
        }, $"指定材料 {material}");
    }

    private async void Interference_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var r = await Client.Inspect.InterferenceAsync();
        var count = Json.Number(r, "count") ?? 0;
        Message(count == 0 ? "干涉檢查：沒有本體重疊。" : $"干涉檢查：{count} 組本體重疊，詳情在「指令」頁的結果。");
        CommandResult.Text = JsonSerializer.Serialize(r, Pretty);
    });

    // ---- Design properties -----------------------------------------------------------------

    private async void LoadProps_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var p = await Client.Design.PropertiesAsync();
        PropPartNumber.Text = p.GetValueOrDefault("part_number") ?? "";
        PropRevision.Text = p.GetValueOrDefault("revision") ?? "";
        PropDescription.Text = p.GetValueOrDefault("description") ?? "";
        PropProject.Text = p.GetValueOrDefault("project") ?? "";
        PropAuthor.Text = p.GetValueOrDefault("author") ?? "";
        PropOrganization.Text = p.GetValueOrDefault("organization") ?? "";
    }, "讀取設計屬性");

    private async void SaveProps_Click(object sender, RoutedEventArgs e) => await Run(() =>
        Client.Design.SetPropertiesAsync(new Dictionary<string, object?>
        {
            ["part_number"] = PropPartNumber.Text.Trim(),
            ["revision"] = PropRevision.Text.Trim(),
            ["description"] = PropDescription.Text.Trim(),
            ["project"] = PropProject.Text.Trim(),
            ["author"] = PropAuthor.Text.Trim(),
            ["organization"] = PropOrganization.Text.Trim(),
        }), "寫入設計屬性");

    private async void Bom_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var rows = await Client.Inspect.BomAsync();
        BomGrid.ItemsSource = rows;
        Message(rows.Count == 0 ? "BOM：沒有零組件（單一零件的設計沒有 BOM 列）。" : $"BOM：{rows.Count} 列。");
    });

    // ---- Files -----------------------------------------------------------------------------

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "SolidCraft 設計與可匯入檔案|*.solidcraft;*.step;*.stp;*.igs;*.iges;*.3mf;*.stl;*.obj;*.glb;*.gltf;*.dxf;*.svg|所有檔案|*.*" };
        if (dlg.ShowDialog(this) == true)
        {
            await Run(async () => { await Client.Files.OpenAsync(dlg.FileName); await RefreshParametersAsync(); }, "開啟 " + System.IO.Path.GetFileName(dlg.FileName));
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        try
        {
            await Client.Files.SaveAsync();
        }
        catch (SolidCraftException ex) when (ex.Message.Contains("path", StringComparison.OrdinalIgnoreCase))
        {
            SaveAs_Click(sender, e);
            return;
        }
    }, "儲存");

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "SolidCraft 設計|*.solidcraft", DefaultExt = ".solidcraft" };
        if (dlg.ShowDialog(this) == true)
        {
            await Run(() => Client.Files.SaveAsAsync(dlg.FileName, overwrite: true), "另存 " + System.IO.Path.GetFileName(dlg.FileName));
        }
    }

    private async void ExportStep_Click(object sender, RoutedEventArgs e) => await ExportAsync("STEP|*.step", ".step", "step");
    private async void ExportStl_Click(object sender, RoutedEventArgs e) => await ExportAsync("STL|*.stl", ".stl", "stl");
    private async void Export3mf_Click(object sender, RoutedEventArgs e) => await ExportAsync("3MF|*.3mf", ".3mf", "3mf");

    private async Task ExportAsync(string filter, string ext, string format)
    {
        var dlg = new SaveFileDialog { Filter = filter, DefaultExt = ext, FileName = "export" + ext };
        if (dlg.ShowDialog(this) != true)
        {
            return;
        }
        await Run(async () =>
        {
            await Client.Files.ExportAsync(dlg.FileName, format);
            FileResult.Text = "已匯出 " + dlg.FileName;
        }, "匯出 " + format.ToUpperInvariant());
    }

    private async void InsertStep_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "STEP / IGES|*.step;*.stp;*.igs;*.iges|所有檔案|*.*" };
        if (dlg.ShowDialog(this) == true)
        {
            await Run(() => Client.Files.InsertStepAsync(dlg.FileName), "插入 " + System.IO.Path.GetFileName(dlg.FileName));
        }
    }

    private async void Render_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "PNG|*.png", DefaultExt = ".png", FileName = "render.png" };
        if (dlg.ShowDialog(this) != true)
        {
            return;
        }
        await Run(async () =>
        {
            await Client.Ui.RenderAsync(dlg.FileName, (int)Num(RenderW), (int)Num(RenderH));
            FileResult.Text = "已彩現 " + dlg.FileName;
        }, "彩現");
    }

    private async void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "PNG|*.png", DefaultExt = ".png", FileName = "screenshot.png" };
        if (dlg.ShowDialog(this) != true)
        {
            return;
        }
        await Run(async () =>
        {
            await Client.Ui.ScreenshotAsync(dlg.FileName);
            FileResult.Text = "已截圖 " + dlg.FileName;
        }, "截圖");
    }

    // ---- Commands --------------------------------------------------------------------------

    private async Task LoadCommandsAsync()
    {
        var infos = await Client.ListCommandsAsync();
        _commands = infos.Select(c => new CommandItem { Id = c.Id, Label = c.Label, Params = c.Params }).OrderBy(c => c.Id).ToList();
        FilterCommands();
    }

    private void CommandFilter_TextChanged(object sender, TextChangedEventArgs e) => FilterCommands();

    private void FilterCommands()
    {
        var f = CommandFilter.Text.Trim();
        CommandList.ItemsSource = f.Length == 0
            ? _commands
            : _commands.Where(c => c.Id.Contains(f, StringComparison.OrdinalIgnoreCase) || c.Label.Contains(f, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void CommandList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CommandList.SelectedItem is CommandItem item)
        {
            CommandId.Text = item.Id;
            CommandDoc.Text = item.Params.Length == 0 ? "（沒有參數說明）" : item.Params;
        }
    }

    private async void Execute_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        var id = CommandId.Text.Trim();
        if (id.Length == 0)
        {
            throw new InvalidOperationException("請選擇或輸入指令 id");
        }
        object? parameters = null;
        var text = CommandParams.Text.Trim();
        if (text.Length > 0)
        {
            using var doc = JsonDocument.Parse(text);
            parameters = doc.RootElement.Clone();
        }
        var isControl = id.StartsWith("ui.", StringComparison.Ordinal) || id is "document.inspect" or "document.revision" or "engine.commands" or "control.running";
        var result = isControl ? await Client.RequestAsync(id, parameters) : await Client.ExecuteAsync(id, parameters);
        CommandResult.Text = JsonSerializer.Serialize(result, Pretty);
    }, "執行 " + CommandId.Text.Trim());

    private async void InspectDoc_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        CommandResult.Text = JsonSerializer.Serialize(await Client.InspectDocumentAsync(), Pretty);
    }, "document.inspect");

    private async void InspectUi_Click(object sender, RoutedEventArgs e) => await Run(async () =>
    {
        CommandResult.Text = JsonSerializer.Serialize(await Client.Ui.InspectAsync(), Pretty);
    }, "ui.inspect");

    private void ClearLog_Click(object sender, RoutedEventArgs e) => EventLog.Items.Clear();
}
