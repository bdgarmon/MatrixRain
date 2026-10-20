using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
[assembly: AssemblyTitle("Matrix Rain")]
[assembly: AssemblyDescription("Local experiment display for the After Six DEX lab")]
[assembly: AssemblyVersion("3.2.0.0")]
[assembly: AssemblyFileVersion("3.2.0.0")]

namespace MatrixRainLab {
    internal sealed class CsvLog {
        readonly string directory;
        string path;
        const string Header = "utc,hostname,node,experiment,workload,state,state_source,experiment_start_utc,cpu_pct,ram_used_gib,ram_total_gib,gpu_busiest_engine_pct,dedicated_gpu_used_gib,shared_gpu_used_gib,display_cpu_pct,display_working_set_mib,uptime_seconds,rain_enabled,frame_cap,warning";
        public CsvLog(string dir) { directory = dir; }
        void Rotate() {
            Directory.CreateDirectory(directory);
            var files = new DirectoryInfo(directory).GetFiles("samples-*.csv").OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
            for (int i = 4; i < files.Length; i++) files[i].Delete();
            path = Path.Combine(directory, "samples-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".csv");
            File.WriteAllText(path, Header + Environment.NewLine);
        }
        public void Write(Sample s, Settings settings) {
            if (path == null || new FileInfo(path).Length >= 10 * 1024 * 1024) Rotate();
            string[] values = { s.Utc.ToString("o"), Environment.MachineName, settings.NodeLabel, settings.ExperimentLabel,
                s.Workload, s.State, s.StateSource, s.ExperimentStart.ToString("o"), Metrics.Number(s.Cpu,"F2"),
                Metrics.Number(s.RamUsed,"F3"), Metrics.Number(s.RamTotal,"F3"), Metrics.Number(s.Gpu,"F2"),
                Metrics.Number(s.Dedicated,"F3"), Metrics.Number(s.Shared,"F3"), Metrics.Number(s.AppCpu,"F2"),
                Metrics.Number(s.AppRam,"F2"), s.UptimeSeconds.ToString("F0",System.Globalization.CultureInfo.InvariantCulture), s.RainEnabled.ToString(), s.FrameCap.ToString(), s.Warning };
            File.AppendAllText(path, String.Join(",", values.Select(Metrics.Csv).ToArray()) + Environment.NewLine);
        }
    }
    internal sealed class LabForm : Form {
        readonly Settings settings;
        readonly Button workloadButton = new Button(), go = new Button(), halt = new Button(), export = new Button(), resultButton = new Button();
        readonly ComboBox choose = new ComboBox();
        readonly System.Windows.Forms.Timer controlTimer = new System.Windows.Forms.Timer();
        readonly WorkloadSession session;
        WorkloadRequest request = new WorkloadRequest();
        string observedPhase;
        bool allowExit, exitAfterStop, exporting;
        string exportMessage;
        readonly DisplayAwake awakeRequest = new DisplayAwake(Native.SetThreadExecutionState);
        bool awake;
        int wakeTicks;
        readonly string configDirectory;
        readonly Random random = new Random();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly ManualResetEvent stop = new ManualResetEvent(false);
        readonly Stopwatch animation = Stopwatch.StartNew();
        readonly Font small = new Font("Consolas", 14, FontStyle.Regular, GraphicsUnit.Pixel);
        readonly Font label = new Font("Consolas", 16, FontStyle.Regular, GraphicsUnit.Pixel);
        readonly Font medium = new Font("Consolas", 26, FontStyle.Bold, GraphicsUnit.Pixel);
        readonly Font big = new Font("Consolas", 42, FontStyle.Bold, GraphicsUnit.Pixel);
        readonly Brush green = new SolidBrush(Color.FromArgb(105, 255, 161));
        readonly Brush white = new SolidBrush(Color.FromArgb(222, 255, 235));
        readonly Brush muted = new SolidBrush(Color.FromArgb(112, 153, 129));
        readonly Brush dark = new SolidBrush(Color.FromArgb(5, 17, 11));
        readonly Pen border = new Pen(Color.FromArgb(35, 84, 52));
        readonly string glyphs = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ<>:+=*/";
        Bitmap atlas;
        float[] heads, speeds;
        int[] lengths, seeds;
        double lastFrame, lastIdleDraw;
        volatile bool rain;
        bool fullscreen;
        Font controlFont;
        Rectangle windowBounds;
        volatile Sample sample;
        const int Cell = 24, Shades = 7;
        public LabForm(Settings value, string directory) {
            settings = value; configDirectory = directory; rain = settings.RainEnabled;
            try {
                string preference=Path.Combine(Program.DataDirectory,"appearance.json");
                if(File.Exists(preference)) {
                    var stored=new JavaScriptSerializer().Deserialize<Settings>(File.ReadAllText(preference));
                    stored.Validate(); settings.RainSpeed=stored.RainSpeed; settings.RainSpacing=stored.RainSpacing;
                }
            } catch(Exception e) { LaunchDiagnostics.Write(Program.DataDirectory,"Appearance preference ignored: "+e.Message); }
            session = new WorkloadSession(directory);
            Text = "Matrix Rain v" + Program.Version; Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); BackColor = Color.Black; KeyPreview = true;
            ClientSize = new Size(1100, 760); MinimumSize = new Size(900, 620);
            StartPosition = FormStartPosition.Manual;
            Screen screen = Screen.AllScreens[Math.Min(settings.Monitor, Screen.AllScreens.Length - 1)];
            Location = new Point(screen.WorkingArea.Left + 30, screen.WorkingArea.Top + 30);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            export.Text="Export logs"; resultButton.Text="Last result";
            export.Click+=delegate { ExportLogs(); }; resultButton.Click+=delegate { ShowResult(); };
            workloadButton.Text = "Settings"; go.Text = "Go (admin)"; halt.Text = "Stop (admin)";
            foreach (var button in new[] { workloadButton, go, halt, export, resultButton }) {
                button.BackColor = Color.FromArgb(10,35,21); button.ForeColor = Color.FromArgb(140,255,180);
                button.FlatStyle = FlatStyle.Flat; button.Font = label; Controls.Add(button);
            }
            choose.DropDownStyle = ComboBoxStyle.DropDownList; choose.Font = label;
            choose.Items.AddRange(new object[] { "Random rotation", "Baseline (rain off)", "Visualization", "CPU", "Memory", "Disk I/O", "Science / pi", "Microsoft 365 Testing", "Windows Workday" });
            choose.SelectedIndex = 0; Controls.Add(choose);
            choose.SelectedIndexChanged += delegate {
                string selected=SelectedMode();
                bool app=selected=="office" || selected=="windows", previousApp=request.Mode=="office" || request.Mode=="windows";
                if(app && !previousApp) request.Seconds=MatrixOffice.OfficePlan.DefaultSeconds;
                else if(!app && previousApp) request.Seconds=600;
                request.Mode=selected; Invalidate();
            };
            workloadButton.Click += delegate { OpenWorkloads(); };
            go.Click += delegate { exportMessage=null; request.Mode = SelectedMode(); session.OwnerWindow=Handle; session.StartRun(request); RefreshControls(); };
            halt.Click += delegate { exportMessage=null; session.OwnerWindow=Handle; session.StopRun(); RefreshControls(); };
            controlTimer.Interval = 1000;
            controlTimer.Tick += delegate { session.Poll(); RefreshControls(); if (exitAfterStop && !session.Busy) { exitAfterStop=false; if(session.StopAcknowledged && session.CanStart && (session.Latest==null || session.Latest.State!="RUNNING")) { allowExit=true; Close(); return; } } if (++wakeTicks >= 15) { RenewAwake(); wakeTicks = 0; } Invalidate(); };
            BuildAtlas();
            Shown += delegate {
                if (settings.Fullscreen) ToggleFullscreen();
                RenewAwake(); controlTimer.Start();
                var thread = new Thread(SampleLoop); thread.IsBackground = true; thread.Name = "Lab metrics"; thread.Start();
                timer.Start();
            };
            timer.Interval = Math.Max(1, 1000 / settings.FramesPerSecond);
            timer.Tick += delegate {
                double now = animation.Elapsed.TotalSeconds, delta = Math.Min(0.15, now - lastFrame); lastFrame = now;
                if (WindowState == FormWindowState.Minimized) return;
                if (rain && heads != null) {
                    for (int i = 0; i < heads.Length; i++) {
                        heads[i] += (float)(delta * speeds[i] * settings.RainSpeed);
                        if (heads[i] - lengths[i] * Cell > ClientSize.Height) { heads[i] = -random.Next(20, 300); seeds[i] = random.Next(100000); }
                    }
                }
                if (rain || now - lastIdleDraw >= 1) { Invalidate(); lastIdleDraw = now; }
            };
            KeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.KeyCode == Keys.W) { OpenWorkloads(); e.Handled = true; }
                if (e.KeyCode == Keys.Escape) Close();
                if (e.KeyCode == Keys.F11) ToggleFullscreen();
                if (e.KeyCode == Keys.Space && !choose.Focused && !go.Focused && !halt.Focused && !workloadButton.Focused) { rain = !rain; Invalidate(); e.Handled = true; }
            };
            Resize += delegate { ResetRain(); PositionControls(); };
            ResetRain(); PositionControls();
        }
        string officeLaunched, officeCompletionShown;
        void EnsureOfficeHelper() {
            var run=session.Latest;
            if(run!=null && officeLaunched!=null && run.State!="RUNNING" && officeCompletionShown!=run.RunId) {
                officeCompletionShown=run.RunId; WindowState=FormWindowState.Normal;
                // A prerequisite warning intentionally leaves Notepad in front. Matrix
                // Rain remains visible behind it with the failure result.
                bool requirementsMissing=run.Office!=null && !String.IsNullOrEmpty(run.Office.Error) && run.Office.Error.IndexOf("required Microsoft 365 desktop applications",StringComparison.OrdinalIgnoreCase)>=0;
                if(!requirementsMissing) Activate();
            }
            if(run==null || (run.ActivePhase!="office" && run.ActivePhase!="windows") || run.State!="RUNNING" || run.HelperId==officeLaunched) {
                if(run!=null && run.State=="RUNNING" && run.ActivePhase!="office" && run.ActivePhase!="windows" && WindowState==FormWindowState.Minimized) { WindowState=FormWindowState.Normal; }
                return;
            }
            using(var identity=System.Security.Principal.WindowsIdentity.GetCurrent()) {
                if(run.ActorSid!=identity.User.Value || run.ActorSession!=Process.GetCurrentProcess().SessionId) return;
            }
            officeLaunched=run.HelperId;
            // Reopening the display observes the already-authorized helper; it must not start another or minimize the controls again.
            if(run.Office!=null && run.Office.Pid>0) return;
            try {
                string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MatrixOfficeActor.exe");
                var start=new ProcessStartInfo(path,"--run-id "+run.HelperId+" --data "+WorkloadRequest.Quote(configDirectory)) {UseShellExecute=false,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory};
                using(var actor=Process.Start(start)) { LaunchDiagnostics.Write(Program.DataDirectory,"Started unelevated Office helper / run "+run.RunId+" / PID "+actor.Id); }
                WindowState=FormWindowState.Minimized;
            } catch(Exception e) { exportMessage="Office helper could not start. Export logs for details."; LaunchDiagnostics.Write(Program.DataDirectory,e.ToString()); }
        }
        string SelectedMode() { return choose.SelectedIndex == 0 ? "rotation" : WorkloadRequest.Modes[choose.SelectedIndex - 1]; }
        void RefreshControls() {
            EnsureOfficeHelper();
            go.Enabled = session.CanStart; halt.Enabled = session.CanStop;
            choose.Enabled = workloadButton.Enabled = !session.Busy;
            resultButton.Enabled=session.LastResult!=null;
            var run = session.Latest;
            if (run!=null && run.RunId+run.ActivePhase!=observedPhase) {
                if(run.State=="RUNNING" && !run.Stale) rain=run.ActivePhase!="baseline";
                observedPhase=run.RunId+run.ActivePhase;
            }
        }
        void RenewAwake() {
            // UI-thread request applies for the entire display lifetime, including baseline/minimized.
            // Legacy KeepAwake=false configs remain parseable but no longer disable this behavior.
            awake = awakeRequest.Renew();
        }
        protected override void WndProc(ref Message m) {
            const int WM_POWERBROADCAST = 0x218;
            if (m.Msg == WM_POWERBROADCAST && (m.WParam.ToInt32() == 7 || m.WParam.ToInt32() == 18)) RenewAwake();
            base.WndProc(ref m);
        }
        void OpenWorkloads() {
            request.Mode = SelectedMode();
            using (var panel = new WorkloadControls(request,settings)) {
                if (panel.ShowDialog(this) == DialogResult.OK) {
                    string previous = request.Mode; request = panel.Value;
                    if (request.Mode != previous) choose.SelectedIndex = request.Mode=="rotation" ? 0 : Array.IndexOf(WorkloadRequest.Modes, request.Mode) + 1;
                    ResetRain();
                    try {
                        Directory.CreateDirectory(Program.DataDirectory);
                        File.WriteAllText(Path.Combine(Program.DataDirectory,"appearance.json"),new JavaScriptSerializer().Serialize(new { settings.RainSpeed,settings.RainSpacing }));
                    } catch(Exception e) { exportMessage="Appearance applied; could not save preference: "+e.Message; }
                }
            }
            Invalidate();
        }
        void Place(Control control, int x, int y, int width, int height) {
            float scale = Math.Min(ClientSize.Width / 1100f, ClientSize.Height / 760f);
            control.SetBounds((int)((ClientSize.Width-1100*scale)/2+x*scale), (int)((ClientSize.Height-760*scale)/2+y*scale), (int)(width*scale), (int)(height*scale));
        }
        void PositionControls() {
            float scale = Math.Min(ClientSize.Width / 1100f, ClientSize.Height / 760f);
            var font = new Font("Consolas", Math.Max(12, 16*scale), FontStyle.Regular, GraphicsUnit.Pixel);
            foreach (var control in new Control[] { choose, go, halt, workloadButton, export, resultButton }) control.Font = font;
            if (controlFont != null) controlFont.Dispose(); controlFont = font;
            Place(choose,36,620,244,34); Place(go,292,618,140,36); Place(halt,444,618,140,36);
            Place(workloadButton,596,618,120,36); Place(resultButton,728,618,144,36); Place(export,884,618,180,36);
        }
        void ToggleFullscreen() {
            if (!fullscreen) {
                windowBounds = Bounds; FormBorderStyle = FormBorderStyle.None;
                Bounds = Screen.FromControl(this).Bounds; fullscreen = true;
            } else { FormBorderStyle = FormBorderStyle.Sizable; Bounds = windowBounds; fullscreen = false; }
        }
        void BuildAtlas() {
            atlas = new Bitmap(glyphs.Length * Cell, Shades * Cell, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(atlas)) using (var font = new Font("Consolas", 18, FontStyle.Bold, GraphicsUnit.Pixel)) {
                g.Clear(Color.Transparent); g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                for (int shade = 0; shade < Shades; shade++) {
                    Color color = shade == 0 ? Color.FromArgb(225, 255, 235) : Color.FromArgb(10, 240 - shade * 31, 52);
                    using (var brush = new SolidBrush(color))
                        for (int i = 0; i < glyphs.Length; i++) g.DrawString(glyphs[i].ToString(), font, brush, i * Cell, shade * Cell);
                }
            }
        }
        void ResetRain() {
            int n = Math.Max(1, (int)(ClientSize.Width / (Cell * settings.RainSpacing)) + 1);
            heads = new float[n]; speeds = new float[n]; lengths = new int[n]; seeds = new int[n];
            for (int i = 0; i < n; i++) {
                heads[i] = random.Next(-500, Math.Max(1, ClientSize.Height)); speeds[i] = random.Next(55, 155);
                lengths[i] = random.Next(12, Math.Max(24,ClientSize.Height/Cell)); seeds[i] = random.Next(100000);
            }
        }
        void SampleLoop() {
            try {
                using (var telemetry = new Telemetry(settings, configDirectory)) {
                    var log = settings.EnableCsv ? new CsvLog(Path.Combine(Program.DataDirectory, "logs")) : null;
                    // Prime interval counters instead of showing an invalid instant CPU rate.
                    if (stop.WaitOne(1000)) return;
                    do {
                        Sample next;
                        try { next = telemetry.Collect(); }
                        catch (Exception e) { next = new Sample { State = "METRICS ERROR", Warning = e.Message, ExperimentStart = DateTime.UtcNow }; }
                        next.RainEnabled = rain; next.FrameCap = settings.FramesPerSecond;
                        if (log != null) { try { log.Write(next, settings); } catch (Exception e) { next.Warning += " CSV: " + e.Message; } }
                        sample = next;
                    } while (!stop.WaitOne(settings.SampleSeconds * 1000));
                }
            } catch (Exception e) { sample = new Sample { State = "METRICS ERROR", Warning = e.Message, ExperimentStart = DateTime.UtcNow }; }
        }
        void TextLine(Graphics g, string text, Font font, Brush brush, float x, float y, float width) {
            using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap })
                g.DrawString(text, font, brush, new RectangleF(x, y, width, font.GetHeight(g) + 8), format);
        }
        void Meter(Graphics g, string title, string value, double? percent, int x, int y) {
            TextLine(g, title, label, muted, x, y, 260);
            TextLine(g, value, medium, white, x, y + 26, 270);
            g.FillRectangle(dark, x, y + 68, 260, 5);
            if (percent.HasValue) g.FillRectangle(green, x, y + 68, (float)(260 * Metrics.Clamp(percent.Value) / 100), 5);
        }
        protected override void OnPaint(PaintEventArgs e) {
            var g = e.Graphics; g.Clear(Color.Black);
            if (rain && heads != null) {
                for (int i = 0; i < heads.Length; i++) for (int j = lengths[i] - 1; j >= 0; j--) {
                    int y = (int)heads[i] - j * Cell;
                    if (y < -Cell || y > ClientSize.Height) continue;
                    int shade = j == 0 ? 0 : Math.Min(Shades - 1, 1 + j * (Shades - 2) / lengths[i]);
                    int symbol = (seeds[i] + j * 17 + (int)(heads[i] / Cell)) % glyphs.Length;
                    if (symbol < 0) symbol += glyphs.Length;
                    int x=(int)(i*Cell*settings.RainSpacing);
                    if(j==0) using(var glow=new SolidBrush(Color.FromArgb(36,55,255,110))) g.FillEllipse(glow,x-3,y-3,Cell+6,Cell+6);
                    g.DrawImage(atlas, new Rectangle(x, y, Cell, Cell), new Rectangle(symbol * Cell, shade * Cell, Cell, Cell), GraphicsUnit.Pixel);
                }
            }
            float scale = Math.Min(ClientSize.Width / 1100f, ClientSize.Height / 760f);
            g.TranslateTransform((ClientSize.Width - 1100 * scale) / 2, (ClientSize.Height - 760 * scale) / 2);
            g.ScaleTransform(scale, scale); g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using (var panel = new SolidBrush(Color.FromArgb(220, 3, 11, 7))) {
                g.FillRectangle(panel, 20, 16, 1060, 116);
                g.FillRectangle(panel, 20, 560, 1060, 180);
            }
            g.FillRectangle(green, 20, 16, 3, 116);
            Sample s = sample;
            double age = s == null ? 0 : Math.Max(0, (DateTime.UtcNow - s.Utc).TotalSeconds);
            TextLine(g, Environment.MachineName, medium, white, 36, 26, 420);
            string headerNote=s!=null && !String.IsNullOrEmpty(s.Warning) ? s.Warning : s!=null && age>settings.SampleSeconds*2+2 ? "METRICS STALE" : settings.LabTitle+" / "+settings.ExperimentLabel;
            TextLine(g,headerNote,small,muted,465,34,460);
            TextLine(g,"v"+Program.Version,small,green,940,34,124);
            string metrics = s == null ? "Sampling CPU / RAM / GPU..." :
                "CPU " + Metrics.Number(s.Cpu,"F1") + "%    RAM " + Metrics.Number(s.RamUsed,"F1") + "/" + Metrics.Number(s.RamTotal,"F1") +
                " GiB    GPU " + Metrics.Number(s.Gpu,"F1") + "%    VRAM " + Metrics.Number(s.Dedicated,"F2") + " GiB";
            TextLine(g, metrics, label, green, 36, 63, 1030);
            TextLine(g, "UP " + (s == null ? "--" : Metrics.Duration(s.UptimeSeconds + age)) +
                "   SHARED GPU " + (s == null ? "--" : Metrics.Number(s.Shared,"F2")) + " GiB   DISPLAY " +
                (s == null ? "--" : Metrics.Number(s.AppCpu,"F1") + "% / " + Metrics.Number(s.AppRam,"F0") + " MiB") +
                "   RAIN " + (rain ? settings.FramesPerSecond + " FPS" : "OFF"), small, muted, 36, 95, 1030);
            var run = session.Latest;
            bool running = run != null && run.State == "RUNNING" && !run.Stale;
            string state = session.Busy ? (session.AwaitingElevation ? "AWAITING ADMIN" : "AWAITING ACK") : run == null ? (session.CanStart ? "READY" : "STATUS UNAVAILABLE") : run.Stale ? "HEARTBEAT STALE" : run.State;
            Brush stateBrush = running || run == null ? green : white;
            if (!running || animation.ElapsedMilliseconds % 1200 < 800) g.FillEllipse(stateBrush, 36, 578, 10, 10);
            double phaseElapsed=run==null ? 0 : run.PhaseElapsed(DateTime.UtcNow);
            string phase=run==null ? "" : WorkloadRequest.Label(run.ActivePhase).ToUpperInvariant()+" "+run.PhaseIndex+(run.Mode=="rotation" ? " / CONTINUOUS" : "/"+run.PhaseCount);
            TextLine(g,state+" / "+phase,label,stateBrush,56,570,560);
            string next=SelectedMode()=="baseline" ? "BASELINE "+request.Seconds+"s" : "BASELINE "+request.BaselineSeconds+"s > "+WorkloadRequest.Label(SelectedMode()).ToUpperInvariant()+" "+request.Seconds+"s";
            TextLine(g,SelectedMode()=="rotation" ? "RANDOM + REST / UNTIL STOPPED" : "NEXT: "+next,small,green,630,573,432);
            g.FillRectangle(dark,36,600,1028,5);
            if(run!=null) g.FillRectangle(green,36,600,(float)(1028*run.Progress(DateTime.UtcNow)),5);
            string progress=run==null ? "Ready for next test" : "PHASE "+Metrics.Duration(phaseElapsed)+" / "+Metrics.Duration(run.PhaseSeconds)+
                "   LEFT "+(run.Stale ? "UNKNOWN" : Metrics.Duration(run.Remaining(DateTime.UtcNow)))+"   TOTAL LEFT "+(run.Mode=="rotation" ? "UNTIL STOPPED" : run.Stale ? "UNKNOWN" : Metrics.Duration(run.TotalRemaining(DateTime.UtcNow)))+(run.ActivePhase=="disk" ? " max" : "");
            TextLine(g,progress,small,green,36,662,760);
            TextLine(g,awake ? "AWAKE REQUEST ACTIVE" : "AWAKE REQUEST FAILED",small,awake ? green : white,800,662,265);
            string note=exportMessage ?? (run!=null && run.Office!=null ?
                (String.IsNullOrEmpty(run.Office.StudioTheme) ? "" : run.Office.StudioTheme+" / "+run.Office.StudioStage+" / ")+run.Office.State+" / "+run.Office.Activity+" / "+run.Office.Created+" files / "+run.Office.Saved+" saves" : session.Message);
            if(run!=null && run.State=="FAILED") note=run.Mode=="office" && run.Office!=null && !String.IsNullOrEmpty(run.Office.Error) ?
                "MICROSOFT 365 TESTING NOT SUCCESSFUL: "+run.Office.Error : "Test failed. Click Last result for the full error; Export logs includes diagnostics.";
            if(run!=null && run.Stale) note="No worker heartbeat for over 20s. Progress frozen; check logs before retrying.";
            TextLine(g,note,small,green,36,687,1030);
            string last=session.LastResult==null ? "No finished test yet" : session.LastResult.State=="FAILED" ? session.LastResult.Mode+" / FAILED — click Last result for details" : session.LastResult.Summary;
            TextLine(g,"LAST: "+last,small,muted,36,714,1030);
        }
        void ShowResult() {
            var last=session.LastResult; if(last==null) return;
            string text=last.Summary+"\r\nRun ID: "+last.RunId+"\r\nStarted UTC: "+last.StartedUtc+"\r\nEnded UTC: "+last.UpdatedUtc+
                "\r\nTotal elapsed: "+Metrics.Duration(last.ElapsedSeconds);
            if(last.Result!=null) text+="\r\nMemory: "+last.Result.AllocatedMiB+" MiB; written: "+last.Result.BytesWritten+" bytes"+(last.ScientificPi.HasValue ? "; Pi: "+last.ScientificPi.Value.ToString("F6") : "");
            if(last.Office!=null) text+="\r\nOffice files created: "+last.Office.Created+" / saves: "+last.Office.Saved+" / actions: "+last.Office.Actions+
                "\r\nPaused desktop seconds: "+last.Office.PausedSeconds+" / skipped scheduled actions: "+last.Office.SkippedActions+
                "\r\nForeground confirmed/attempted/denied: "+last.Office.ForegroundConfirmed+" / "+last.Office.ForegroundAttempts+" / "+last.Office.ForegroundDenied+"\r\nMouse/keyboard pause seconds: "+last.Office.InteractionPausedSeconds+"\r\nOutlook: "+last.Office.OutlookPreparation+" / drafts: "+last.Office.OutlookDrafts+"\r\nBrowsers: "+last.Office.Browsers+" / requested tabs: "+last.Office.BrowserTabsRequested+" / tab switches: "+last.Office.BrowserTabSwitches+"\r\nApplication opens/closes: "+last.Office.AppOpens+" / "+last.Office.AppCloses+"\r\nDesktop: "+last.Office.Desktop+"\r\nDocuments: "+last.Office.Documents+"\r\n"+last.Office.Error;
            if(last.Phases!=null) foreach(var phase in last.Phases) text+="\r\n\r\n"+phase.Phase+": "+phase.StartedUtc+" > "+phase.EndedUtc+
                "\r\n"+(phase.Result==null ? phase.Error : Metrics.Duration(phase.Result.ElapsedSeconds)+" / "+phase.Result.Reason);
            using(var dialog=new Form { Text="Last result — select text to copy", StartPosition=FormStartPosition.CenterParent,
                Size=new Size(850,540), MinimumSize=new Size(500,300), ShowInTaskbar=false }) {
                var details=new TextBox { Multiline=true, ReadOnly=true, WordWrap=true, ScrollBars=ScrollBars.Vertical,
                    Dock=DockStyle.Fill, Font=new Font("Segoe UI",11), Text=text };
                dialog.Controls.Add(details);
                dialog.ShowDialog(this);
            }
        }
        void ExportLogs() {
            if(exporting) return;
            using(var dialog=new SaveFileDialog { Filter="Troubleshooting ZIP (*.zip)|*.zip",FileName="MatrixRain-"+Environment.MachineName+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".zip" }) {
                if(dialog.ShowDialog(this)!=DialogResult.OK) return;
                var powerChoice=MessageBox.Show(this,"Include administrator power diagnostics? Yes requests UAC for a read-only helper; No exports available diagnostics without elevation.","Troubleshooting export",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
                if(powerChoice==DialogResult.Cancel) return;
                bool captureAwake=awake;
                IntPtr owner=Handle;
                string path=dialog.FileName; exporting=true; export.Enabled=false; exportMessage="Exporting troubleshooting bundle...";
                var thread=new Thread(delegate() {
                    string message;
                    try {
                        string powerPath=null, powerNote="Standard-user export selected.";
                        if(powerChoice==DialogResult.Yes) {
                            string id=Guid.NewGuid().ToString("N");
                            try {
                                string worker=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"MatrixLabWorker.exe");
                                string args="--power-report true --data "+WorkloadRequest.Quote(configDirectory)+" --run-id "+id;
                                LaunchDiagnostics.Write(Program.DataDirectory,"Power helper elevation requested / "+id);
                                using(var helper=new ElevationOperation(delegate { return ElevatedProcess.Start(worker,args,owner); })) {
                                    while(!helper.Complete) Thread.Sleep(100);
                                    if(helper.Error!=null) throw helper.Error;
                                    string report=Path.Combine(configDirectory,"workloads","power-"+id+".json");
                                    var wait=Stopwatch.StartNew();
                                    while(!File.Exists(report) && wait.Elapsed.TotalSeconds<25) Thread.Sleep(100);
                                    if(File.Exists(report)) { powerPath=report; powerNote="Elevated read-only helper; no workload started or stopped."; }
                                    else powerNote="Elevated helper produced no report within 25s; standard-user fallback used.";
                                }
                                LaunchDiagnostics.Write(Program.DataDirectory,"Power helper / "+id+" / "+powerNote);
                            } catch(System.ComponentModel.Win32Exception ex) { LaunchDiagnostics.Write(Program.DataDirectory,"Power helper / "+id+" / "+ex); powerNote=ex.NativeErrorCode==1223 ? "UAC cancelled; standard-user fallback used." : "Elevation unavailable: "+ex.Message; }
                        }
                        Troubleshooting.Export(path,configDirectory,Program.DataDirectory,AppDomain.CurrentDomain.BaseDirectory,captureAwake,powerPath,powerNote); message=(powerChoice==DialogResult.Yes && powerPath==null ? "Saved limited diagnostics (admin capture unavailable): " : "Saved troubleshooting ZIP: ")+path; }
                    catch(Exception ex) { message="Export failed: "+ex.Message; }
                    if(!IsDisposed && IsHandleCreated) try { BeginInvoke((Action)delegate { exporting=false; export.Enabled=true; exportMessage=message; Invalidate(); }); } catch(InvalidOperationException) { }
                }); thread.IsBackground=true; thread.Start();
            }
        }
        protected override void OnFormClosing(FormClosingEventArgs e) {
            if(e.CloseReason==CloseReason.UserClosing && !allowExit) {
                if(exporting) { e.Cancel=true; exportMessage="Wait for the troubleshooting export to finish before exiting."; }
                else {
                    session.Poll();
                    if(session.ExitNeedsChoice) {
                        e.Cancel=true;
                        using(var dialog=new ExitChoice(session.CanStop && !session.Busy)) {
                            dialog.ShowDialog(this);
                            if(dialog.Choice==2) { allowExit=true; e.Cancel=false; }
                            else if(dialog.Choice==1) { exportMessage=null; session.OwnerWindow=Handle; session.StopRun(); exitAfterStop=session.Busy; }
                        }
                    }
                }
            }
            base.OnFormClosing(e);
        }
        protected override void OnFormClosed(FormClosedEventArgs e) {
            timer.Stop(); controlTimer.Stop(); stop.Set();
            awakeRequest.Dispose();
            base.OnFormClosed(e);
        }
        protected override void Dispose(bool disposing) {
            if (disposing) {
                timer.Dispose(); controlTimer.Dispose(); session.Dispose(); if (atlas != null) atlas.Dispose();
                if (controlFont != null) controlFont.Dispose(); small.Dispose(); label.Dispose(); medium.Dispose(); big.Dispose();
                green.Dispose(); white.Dispose(); muted.Dispose(); dark.Dispose(); border.Dispose();
                // The worker owns its telemetry and exits after stop is signalled; do not block UI on a driver.
            }
            base.Dispose(disposing);
        }
    }
    internal static class Program {
        internal static string Version { get { var v=typeof(Program).Assembly.GetName().Version; return v.Major+"."+v.Minor+"."+v.Build; } }
        internal static string DataDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AfterSix", "MatrixRainLab"); } }
        [STAThread] static int Main(string[] args) {
            bool diagnostic = Array.IndexOf(args, "--diagnostics") >= 0;
            bool validate = Array.IndexOf(args, "--validate") >= 0;
            try {
                string config = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                for (int i = 0; i < args.Length; i++) {
                    if (args[i] == "--config" && i + 1 < args.Length) config = Path.GetFullPath(args[++i]);
                    else if (args[i] != "--diagnostics" && args[i] != "--validate" && args[i] != "--windowed") throw new ArgumentException("Unknown or incomplete argument: " + args[i]);
                }
                string json = File.ReadAllText(config);
                var serializer = new JavaScriptSerializer();
                var properties = serializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(json);
                if (properties == null) throw new ArgumentException("Config must be a JSON object.");
                Settings.ValidateKeys(properties.Keys);
                var settings = serializer.Deserialize<Settings>(json);
                if (settings == null) throw new ArgumentException("Config must be a JSON object.");
                settings.Validate();
                if (validate) return 0;
                settings.NodeLabel = Environment.MachineName;
                if (diagnostic) {
                    using (var telemetry = new Telemetry(settings, Path.GetDirectoryName(config))) {
                        Thread.Sleep(1500); var s = telemetry.Collect(); Directory.CreateDirectory(DataDirectory);
                        File.WriteAllText(Path.Combine(DataDirectory,"diagnostics.json"), new JavaScriptSerializer().Serialize(s));
                        return s.Cpu.HasValue && s.RamTotal.HasValue && s.RamTotal > 0 ? 0 : 2;
                    }
                }
                if (Array.IndexOf(args, "--windowed") >= 0) settings.Fullscreen = false;
                if (!Environment.UserInteractive || Process.GetCurrentProcess().SessionId == 0) throw new InvalidOperationException("Launch in a signed-in user's desktop, not SYSTEM/session 0.");
                bool created;
                using (var mutex = new Mutex(true, @"Local\AfterSix.MatrixRainLab", out created)) {
                    if (!created) return 0;
                    Native.SetProcessDPIAware(); Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                    using (var form = new LabForm(settings, Path.GetDirectoryName(config))) Application.Run(form);
                }
                return 0;
            } catch (Exception e) {
                try { Directory.CreateDirectory(DataDirectory); File.WriteAllText(Path.Combine(DataDirectory,"last-error.txt"), DateTime.UtcNow.ToString("o") + "\r\n" + e); } catch { }
                if (!diagnostic && !validate && Environment.UserInteractive) MessageBox.Show(e.Message, "Matrix Rain", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
    }
}
