using System;
using System.Drawing;
using System.Windows.Forms;
namespace MatrixRainLab {
    internal sealed class WorkloadControls : Form {
        readonly ComboBox mode=new ComboBox();
        readonly NumericUpDown baseline=Number(0,600,60), duration=Number(1,28800,600), intensity=Number(1,95,50), threads=Number(1,32,2), memory=Number(8,4096,512), disk=Number(4,256,64), budget=Number(4,4096,1024), rate=Number(1,256,10);
        readonly TrackBar speed=new TrackBar {Minimum=25,Maximum=600,TickFrequency=100,Dock=DockStyle.Fill};
        readonly TrackBar spacing=new TrackBar {Minimum=75,Maximum=400,TickFrequency=50,Dock=DockStyle.Fill};
        bool applying;
        int previousMode=-1;
        readonly Label description=new Label();
        public WorkloadRequest Value { get; private set; }
        static NumericUpDown Number(int min,int max,int value) { return new NumericUpDown { Minimum=min,Maximum=max,Value=value,Dock=DockStyle.Fill,ThousandsSeparator=true }; }
        public WorkloadControls(WorkloadRequest request,Settings appearance) {
            Text="Test and rain settings"; StartPosition=FormStartPosition.CenterParent; ClientSize=new Size(720,585);
            FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false;
            Font=new Font("Segoe UI",10);
            var layout=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=2 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48)); Controls.Add(layout);
            mode.DropDownStyle=ComboBoxStyle.DropDownList; mode.Dock=DockStyle.Fill;
            mode.Items.AddRange(new object[]{"Baseline (rain off)","Visualization (rain on)","CPU","Memory","Disk I/O","Local science / pi","Microsoft 365 Testing","Windows Workday","Random rotation (until stopped)"});
            Row(layout,"Selected test",mode); Row(layout,"Workload duration (seconds)",duration); Row(layout,"Baseline first (seconds)",baseline); Row(layout,"Intensity (%)",intensity);
            Row(layout,"CPU / science threads",threads); Row(layout,"Memory allocation (MiB)",memory);
            Row(layout,"Disk scratch size (MiB)",disk); Row(layout,"Total write budget (MiB)",budget); Row(layout,"Disk rate ceiling (MiB/s)",rate);
            speed.Value=(int)(appearance.RainSpeed*100); spacing.Value=(int)(appearance.RainSpacing*100);
            Row(layout,"Rain speed (0.25x — 6x)",speed); Row(layout,"Column spacing (dense — wide)",spacing);
            description.Dock=DockStyle.Fill; FullRow(layout,description,68);
            var apply=new Button { Text="Apply",AutoSize=true }; var cancel=new Button { Text="Cancel",DialogResult=DialogResult.Cancel,AutoSize=true };
            var buttons=new FlowLayoutPanel { Dock=DockStyle.Fill }; buttons.Controls.Add(apply); buttons.Controls.Add(cancel); FullRow(layout,buttons,38);
            apply.Click+=delegate { try { Value=Request(); Value.Validate(); appearance.RainSpeed=speed.Value/100.0; appearance.RainSpacing=spacing.Value/100.0; DialogResult=DialogResult.OK; Close(); } catch(Exception e) { MessageBox.Show(this,e.Message,"Test settings"); } };
            CancelButton=cancel; AcceptButton=apply; mode.SelectedIndexChanged+=delegate { ModeChanged(); }; Apply(request);
        }
        static void Row(TableLayoutPanel table,string text,Control control) {
            int row=table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
            table.Controls.Add(new Label { Text=text,AutoSize=true,Anchor=AnchorStyles.Left },0,row); table.Controls.Add(control,1,row);
        }
        static void FullRow(TableLayoutPanel table,Control control,int height) { int row=table.RowCount++; table.RowStyles.Add(new RowStyle(SizeType.Absolute,height)); table.Controls.Add(control,0,row); table.SetColumnSpan(control,2); }
        WorkloadRequest Request() { return new WorkloadRequest { Mode=WorkloadRequest.Modes[mode.SelectedIndex],Seconds=(int)duration.Value,BaselineSeconds=(int)baseline.Value,Intensity=(int)intensity.Value,Threads=(int)threads.Value,MemoryMiB=(int)memory.Value,DiskMiB=(int)disk.Value,WriteLimitMiB=(int)budget.Value,DiskMiBPerSecond=(int)rate.Value }; }
        void Apply(WorkloadRequest r) { applying=true; mode.SelectedIndex=Array.IndexOf(WorkloadRequest.Modes,r.Mode); duration.Value=r.Seconds; baseline.Value=Math.Max(1,r.BaselineSeconds); intensity.Value=r.Intensity; threads.Value=r.Threads; memory.Value=r.MemoryMiB; disk.Value=r.DiskMiB; budget.Value=r.WriteLimitMiB; rate.Value=r.DiskMiBPerSecond; applying=false; }
        void ModeChanged() {
            int i=mode.SelectedIndex;
            if(!applying && i!=previousMode) { if(i==6 || i==7) duration.Value=21600; else if(previousMode==6 || previousMode==7) duration.Value=600; }
            duration.Maximum=i==6 || i==7 ? 28800 : 3600; duration.Minimum=i==6 || i==7 ? 60 : 1;
            previousMode=i;
            baseline.Enabled=i!=0 && i!=8; duration.Enabled=i!=8; threads.Enabled=i==2 || i==5; memory.Enabled=i==3; disk.Enabled=budget.Enabled=rate.Enabled=i==4; intensity.Enabled=i>=2 && i<=5;
            string[] descriptions={"No synthetic pressure. Rain is switched off once at the beginning; the display stays open.","Uses the existing display's rain. No additional synthetic pressure or GPU benchmark.","Intensity is per worker, not whole-machine CPU. Two threads at 50% use roughly one logical CPU.","Touches the allocation repeatedly. Stops if measured free RAM drops below 1 GiB or 20% of total.","Effective write target = rate ceiling x intensity. Stops at duration or write budget, whichever comes first.","Local Monte Carlo calculation of pi. Records the estimate and sample count; no external projects or downloads.","Hardware Launch Studio: illustrated Word briefs, Excel launch models, PowerPoint product reveals and synthetic Outlook drafts. Six hours by default; complete app setup first."};
            description.Text=i==7 ? "Windows built-in Notepad, File Explorer and Edge. Real journal edits and saves; generated Desktop/Documents files remain. Requires no Office apps." : i==8 ? "Continuous randomized 3–12 minute activities with screensaver-only rest after every activity. Microsoft 365 excluded. One admin approval; runs until Stop. Rotation uses its own capped pressure settings." : descriptions[Math.Max(0,i)];
        }
    }
}
