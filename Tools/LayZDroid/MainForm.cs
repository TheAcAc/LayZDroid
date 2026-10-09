using System.Diagnostics;
using System.Text;
using System.Text.Json;
namespace LayZDroid;
public sealed class MainForm:Form
{
    readonly Store store;readonly Runner runner;readonly List<Instance> instances;
    readonly Color background=Color.FromArgb(17,14,28),surface=Color.FromArgb(30,23,45),accent=Color.FromArgb(128,48,164);
    readonly DataGridView grid=new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,RowHeadersVisible=false};
    readonly Label status=new(){Dock=DockStyle.Fill,AutoSize=false,Padding=new Padding(12),Text="Preview: hardware limits are provisional. Set up the runtime to begin."};
    readonly NumericUpDown ram=new(){Minimum=768,Maximum=8192,Increment=256,Value=1024,Width=90};
    readonly NumericUpDown cores=new(){Minimum=1,Maximum=16,Value=1,Width=60};
    readonly TextBox name=new(){Width=150};readonly ComboBox size=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=185};readonly ComboBox gpu=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=100};
    readonly Label host=new(){AutoSize=true};readonly List<Button> buttons=[];readonly Button cancel;
    readonly System.Windows.Forms.Timer timer=new(){Interval=3000};CancellationTokenSource? operation;bool busy;Guid? loaded;
    public MainForm(Store storage)
    {
        store=storage;runner=new(store);instances=store.Load();Text="LayZDroid · 1.0.1";Width=1080;Height=720;MinimumSize=new Size(840,570);BackColor=background;ForeColor=Color.FromArgb(237,225,249);Font=new Font("Segoe UI",10);StartPosition=FormStartPosition.CenterScreen;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=6,ColumnCount=1,Padding=new Padding(18)};
        foreach(int h in new[]{65,90,75})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,85));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,65));Controls.Add(layout);
        var title=new Label{Text="LayZDroid",Font=new Font("Segoe UI Semibold",25),AutoSize=true};var heading=new FlowLayoutPanel{Dock=DockStyle.Fill};heading.Controls.Add(title);heading.Controls.Add(new Label{Text="Built for LayZ. No launcher ads. No bundled app store.\nOne focused alternative to a general-purpose BlueStacks setup.",AutoSize=true,Margin=new Padding(24,10,0,0)});layout.Controls.Add(heading,0,0);
        var tools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};
        tools.Controls.Add(Button("Set up runtime",Setup));tools.Controls.Add(Button("Add instance",Add));tools.Controls.Add(Button("Start selected",()=>_ = Run(async ct=>{await runner.Start(Selected(),Progress(),ct);store.Save(instances);})));tools.Controls.Add(Button("Start all",()=>_ = Run(async ct=>{foreach(var i in instances){ct.ThrowIfCancellationRequested();if(await runner.Owner(i,ct) is null){await runner.Start(i,Progress(),ct);store.Save(instances);}}})));
        tools.Controls.Add(Button("Stop selected",()=>_ = Run(async ct=>{await runner.Stop(Selected(),ct);store.Save(instances);})));tools.Controls.Add(Button("Delete selected",Delete));tools.Controls.Add(Button("Open KaW",()=>_ = Run(async ct=>{var i=Selected();if(await runner.Owner(i,ct) is null)await runner.Start(i,Progress(),ct);await runner.OpenGame(i,ct);})));tools.Controls.Add(Button("Import APK",Import));tools.Controls.Add(Button("Open LayZ",OpenLayZ));
        cancel=new Button{Text="Cancel task",AutoSize=true,Enabled=false,BackColor=surface,ForeColor=ForeColor,FlatStyle=FlatStyle.Flat};cancel.Click+=(_,_)=>operation?.Cancel();tools.Controls.Add(cancel);layout.Controls.Add(tools,0,1);
        size.Items.Add("900 × 1600 · 240 DPI");size.SelectedIndex=0;size.Enabled=false;gpu.Items.AddRange(["Host GPU","Software GPU"]);gpu.SelectedIndex=0;
        var settings=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};void Field(string text,Control control){settings.Controls.Add(new Label{Text=text,AutoSize=true,Margin=new Padding(5,8,5,0)});settings.Controls.Add(control);}
        Field("Name",name);Field("RAM MB",ram);Field("Cores",cores);Field("Display",size);Field("Graphics",gpu);settings.Controls.Add(Button("Save settings",SaveSettings));settings.Controls.Add(new Label{Text="Change settings while the instance is stopped. RAM is the guest allocation. 768/1024 MB are experimental; increase it if KaW struggles.",AutoSize=true,Margin=new Padding(5,8,0,0)});layout.Controls.Add(settings,0,2);
        grid.BackgroundColor=surface;grid.BorderStyle=BorderStyle.None;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle=new DataGridViewCellStyle{BackColor=surface,ForeColor=ForeColor};grid.DefaultCellStyle=new DataGridViewCellStyle{BackColor=background,ForeColor=ForeColor,SelectionBackColor=accent,SelectionForeColor=Color.White};grid.GridColor=surface;
        foreach(string col in new[]{"Instance","State","RAM / cores","Display","VM private MB","ADB serial"})grid.Columns.Add(col,col);grid.SelectionChanged+=(_,_)=>LoadSelection();layout.Controls.Add(grid,0,3);
        var footer=new FlowLayoutPanel{Dock=DockStyle.Fill};footer.Controls.Add(Button("Export test report",Export));footer.Controls.Add(Button("Diagnostics folder",()=>Process.Start(new ProcessStartInfo("explorer.exe",store.Root){UseShellExecute=true})));footer.Controls.Add(Button("Choose data folder",ChooseFolder));footer.Controls.Add(host);layout.Controls.Add(footer,0,4);layout.Controls.Add(status,0,5);
        timer.Tick+=(_,_)=>RefreshRows();Shown+=async(_,_)=>{await Run(async ct=>{foreach(var i in instances){var owner=await runner.Owner(i,ct);if(owner is not null){i.Pid=owner.Value.Pid;i.Started=owner.Value.Start;i.ProcessPath=owner.Value.Path;i.Status="Running · check game";}else{i.Pid=0;i.Status="Stopped";}}store.Save(instances);});timer.Start();};
        FormClosing+=(_,e)=>{if(busy){e.Cancel=true;operation?.Cancel();status.Text="Cancelling the current task. You can close LayZDroid when it finishes.";}};
        RefreshRows();
    }
    Button Button(string text,Action action){var b=new Button{Text=text,AutoSize=true,Height=34,FlatStyle=FlatStyle.Flat,BackColor=accent,ForeColor=Color.White,Margin=new Padding(4)};b.FlatAppearance.BorderSize=0;b.Click+=(_,_)=>{try{action();}catch(Exception ex){status.Text=ex.Message;}};buttons.Add(b);return b;}
    Instance Selected()=>grid.CurrentRow?.Tag as Instance??throw new IOException("Select an instance first.");
    IProgress<string> Progress()=>new Progress<string>(message=>{status.Text=message;RefreshRows();});
    async Task Run(Func<CancellationToken,Task> action)
    {
        if(busy)return;busy=true;operation=new();foreach(var b in buttons)b.Enabled=false;foreach(var control in new Control[]{grid,name,ram,cores,size,gpu})control.Enabled=false;cancel.Enabled=true;
        try{await action(operation.Token);status.Text="Task complete.";}catch(OperationCanceledException){status.Text="Task cancelled or timed out. Check instance status; an already-started emulator may still be open.";}catch(Exception ex){status.Text=ex.Message;}
        finally{try{store.Save(instances);}catch(Exception ex){status.Text="Could not save settings: "+ex.Message;}operation.Dispose();operation=null;busy=false;foreach(var b in buttons)b.Enabled=true;foreach(var control in new Control[]{grid,name,ram,cores,size,gpu})control.Enabled=true;size.Enabled=false;cancel.Enabled=false;RefreshRows();}
    }
    void RefreshRows()
    {
        Guid? selected=grid.CurrentRow?.Tag is Instance old?old.Id:null;
        foreach(var row in grid.Rows.Cast<DataGridViewRow>().Where(r=>r.Tag is Instance missing&&!instances.Contains(missing)).ToArray())grid.Rows.Remove(row);
        foreach(var i in instances)
        {
            string memory="—";
            if(i.Pid>0)try{using var p=Process.GetProcessById(i.Pid);if(p.HasExited||!Policy.Owns(i.Pid,i.Started,i.ProcessPath,p.Id,p.StartTime.ToUniversalTime(),p.MainModule?.FileName??"")){i.Status="Stopped";i.Pid=0;}else memory=(p.PrivateMemorySize64/1048576).ToString();}catch(ArgumentException){i.Status="Stopped";i.Pid=0;}catch{memory="Unavailable";}
            var row=grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r=>r.Tag is Instance found&&found.Id==i.Id);
            if(row is null){int n=grid.Rows.Add();row=grid.Rows[n];row.Tag=i;}row.SetValues(i.Name,i.Status,$"{i.RamMb} MB / {i.Cores}",$"{i.Width} × {i.Height} · 240 DPI",memory,"emulator-"+i.Port);if(i.Id==selected)grid.CurrentCell=row.Cells[0];
        }
        LoadSelection();try{var m=HostMemory.Read();host.Text=$"Free RAM: {m.Available/1048576} MB · commit available: {m.CommitRemaining/1048576} MB";}catch{host.Text="Host memory unavailable";}
    }
    void LoadSelection(){if(grid.CurrentRow?.Tag is not Instance i||loaded==i.Id)return;loaded=i.Id;name.Text=i.Name;ram.Value=i.RamMb;cores.Value=i.Cores;size.SelectedIndex=0;gpu.SelectedIndex=i.Gpu=="host"?0:1;}
    void Add()
    {
        var busyPorts=Runner.BusyPorts();foreach(var i in instances){busyPorts.Add(i.Port);busyPorts.Add(i.Port+1);}var item=new Instance{Name="LayZDroid "+(instances.Count+1),Port=Policy.NextPort(busyPorts)};store.Configure(item);instances.Add(item);store.Save(instances);RefreshRows();grid.CurrentCell=grid.Rows[^1].Cells[0];status.Text="Fresh instance created. Start it, then import your KaW APK.";
    }
    void SaveSettings()=>_ = Run(async ct=>{var i=Selected();if(await runner.Owner(i,ct) is not null)throw new IOException("Stop this instance before changing its settings.");i.Name=string.IsNullOrWhiteSpace(name.Text)?"LayZDroid":name.Text.Trim();i.RamMb=(int)ram.Value;i.Cores=(int)cores.Value;i.Width=900;i.Height=1600;i.Gpu=gpu.SelectedIndex==0?"host":"software";store.Configure(i);store.Save(instances);});
    void Delete()
    {
        var i=Selected();if(MessageBox.Show(this,$"Delete {i.Name}?\n\nThis removes its installed games, sign-ins and saved Android data. It cannot be undone. Other instances are kept.","Delete instance",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
        _=Run(async ct=>{if(await runner.Owner(i,ct) is not null)throw new IOException("Stop this instance, then choose Delete selected again.");i.Pid=0;store.DeleteInstance(instances,i);loaded=null;});
    }
    void Setup()
    {
        if(RuntimeSetup.Ready(store)){status.Text="Runtime is already ready.";return;}
        using var terms=new Form{Text="LayZDroid runtime setup",Width=780,Height=620,StartPosition=FormStartPosition.CenterParent,BackColor=background,ForeColor=ForeColor};
        var text=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=surface,ForeColor=ForeColor,Text=RuntimeTerms.Text};
        var bottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=65};var agree=new CheckBox{Text="I accept the displayed Android SDK terms",AutoSize=true};var install=new Button{Text="Download runtime",Enabled=false,AutoSize=true,BackColor=accent,ForeColor=Color.White,DialogResult=DialogResult.OK};agree.CheckedChanged+=(_,_)=>install.Enabled=agree.Checked;bottom.Controls.Add(agree);bottom.Controls.Add(install);terms.Controls.Add(text);terms.Controls.Add(bottom);
        if(terms.ShowDialog(this)==DialogResult.OK)_=Run(ct=>RuntimeSetup.InstallAsync(store,Progress(),ct));
    }
    void Import()
    {
        var i=Selected();using var picker=new OpenFileDialog{Filter="Android APK files|*.apk",Multiselect=true,Title="Select the base APK and all required split APKs"};if(picker.ShowDialog(this)!=DialogResult.OK)return;
        _=Run(async ct=>{if(await runner.Owner(i,ct) is null)await runner.Start(i,Progress(),ct);await runner.Import(i,picker.FileNames,ct);});
    }
    void OpenLayZ()
    {
        if(!RuntimeSetup.Ready(store))throw new IOException("Set up the runtime first.");using var pick=new OpenFileDialog{Filter="LayZ executable|*.exe",Title="Select the updated LayZ executable"};if(pick.ShowDialog(this)!=DialogResult.OK)return;
        if(!Path.GetFileName(pick.FileName).StartsWith("LayZ by Princess",StringComparison.OrdinalIgnoreCase))throw new IOException("Select the LayZ application executable.");
        _=Run(async ct=>status.Text=await runner.OpenLayZAsync(pick.FileName,ct));
    }
    void Export()
    {
        using var pick=new SaveFileDialog{Filter="JSON report|*.json",FileName="LayZDroid-test-report.json"};if(pick.ShowDialog(this)!=DialogResult.OK)return;
        var m=HostMemory.Read();File.WriteAllText(pick.FileName,JsonSerializer.Serialize(new{Product="LayZDroid",Version="1.0.1",Date=DateTimeOffset.UtcNow,Windows=Environment.OSVersion.VersionString,HostMemoryMb=m.Total/1048576,AvailableMemoryMb=m.Available/1048576,CommitAvailableMb=m.CommitRemaining/1048576,RuntimeReady=RuntimeSetup.Ready(store),Instances=instances.Select(i=>new{i.Name,i.Status,i.RamMb,i.Cores,i.Width,i.Height,i.Gpu}),Qualification="Preview; actual low-spec performance not yet verified"},new JsonSerializerOptions{WriteIndented=true}));status.Text="Test report saved. It contains settings and host resources, without game credentials or account disks.";
    }
    void ChooseFolder()
    {
        using var pick=new FolderBrowserDialog{Description="Choose a drive/folder with at least 6 GB free. A separate LayZDroid data folder will be used.",UseDescriptionForTitle=true};if(pick.ShowDialog(this)!=DialogResult.OK)return;
        var preferenceRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LayZDroid");Directory.CreateDirectory(preferenceRoot);File.WriteAllText(Path.Combine(preferenceRoot,"data-folder.txt"),Path.Combine(pick.SelectedPath,"LayZDroid"));
        MessageBox.Show(this,"Data folder saved. Close and reopen LayZDroid to use it. Existing instances stay in their original folder.","LayZDroid");
    }
}
