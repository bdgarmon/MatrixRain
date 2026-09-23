using System;
using System.Drawing;
using System.Windows.Forms;
namespace MatrixRainLab {
    internal sealed class ExitChoice : Form {
        public int Choice { get; private set; }
        public ExitChoice(bool canStop) {
            Text="An experiment may still be running"; StartPosition=FormStartPosition.CenterParent;
            ClientSize=new Size(580,225); FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=MinimizeBox=false;
            var layout=new FlowLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(18),FlowDirection=FlowDirection.TopDown,WrapContents=false };
            Controls.Add(layout);
            layout.Controls.Add(new Label { Text="Closing the display releases its keep-awake request.\r\nThe worker continues through its baseline/workload sequence unless stopped.",AutoSize=false,Width=540,Height=52 });
            string[] labels={"Return to display","Stop experiment and exit (admin)","Leave experiment running and exit"};
            for(int i=0;i<3;i++) { int choice=i; var button=new Button { Text=labels[i],Width=530,Height=35,Enabled=i!=1 || canStop }; button.Click+=delegate { Choice=choice; Close(); }; layout.Controls.Add(button); if(i==0) { AcceptButton=button; CancelButton=button; } }
        }
    }
}
