using System;
#if !PORTABLE_TEST
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
#endif
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using MatrixOffice;

namespace MatrixOfficeActor {
    // Office is used through late binding so the endpoint needs no interop packages.
    internal static class StudioContent {
        // One actor STA owns these short-lived drawing limits. Each call resets
        // them before drawing; completed slides are left intact on ordinary edits.
        static int shapeIndex,shapeSkip,shapeLimit=Int32.MaxValue;
        static bool DrawNext() { int index=shapeIndex++; return index>=shapeSkip && index<shapeLimit; }
        static void Pace(int milliseconds) {
#if !PORTABLE_TEST
            System.Threading.Thread.Sleep(milliseconds);
#endif
        }
        static int Ole(string hex) { int rgb=Int32.Parse(hex.TrimStart('#'),NumberStyles.HexNumber,CultureInfo.InvariantCulture); return (rgb>>16&255)|(rgb&65280)|((rgb&255)<<16); }
        static void Release(object value) { if(value!=null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
        internal static string Art(string directory,StudioBrief brief,int product) {
#if PORTABLE_TEST
            string preview=Path.Combine(directory,"studio-product-"+product+".svg");
            File.WriteAllText(preview,StudioScene.Svg(brief,product)); return preview;
#else
            string path=Path.Combine(directory,"studio-product-"+product+".png");
            if(File.Exists(path)) return path;
            using(var image=new Bitmap(1600,1000,PixelFormat.Format32bppPArgb)) using(var g=Graphics.FromImage(image)) {
                g.SmoothingMode=SmoothingMode.AntiAlias; g.Clear(ColorTranslator.FromHtml(brief.Background));
                foreach(var shape in StudioScene.Create(brief,product)) {
                    using(var brush=new SolidBrush(Color.FromArgb(shape.Alpha,ColorTranslator.FromHtml(shape.Fill)))) {
                        if(shape.Kind=="ellipse") g.FillEllipse(brush,shape.X,shape.Y,shape.W,shape.H);
                        else if(shape.Kind=="polygon") {
                            var points=new PointF[shape.Points.Length/2]; for(int i=0;i<points.Length;i++) points[i]=new PointF(shape.Points[i*2],shape.Points[i*2+1]);
                            g.FillPolygon(brush,points);
                        } else if(shape.Radius>0) {
                            float d=shape.Radius*2; using(var p=new GraphicsPath()) {
                                p.AddArc(shape.X,shape.Y,d,d,180,90); p.AddArc(shape.X+shape.W-d,shape.Y,d,d,270,90);
                                p.AddArc(shape.X+shape.W-d,shape.Y+shape.H-d,d,d,0,90); p.AddArc(shape.X,shape.Y+shape.H-d,d,d,90,90); p.CloseFigure(); g.FillPath(brush,p);
                            }
                        } else g.FillRectangle(brush,shape.X,shape.Y,shape.W,shape.H);
                    }
                }
                image.Save(path,ImageFormat.Png);
            }
            return path;
#endif
        }
        static void Text(dynamic slide,string text,float x,float y,float w,float h,float size,string color,bool bold=false) {
            if(!DrawNext()) return;
            dynamic shape=slide.Shapes.AddTextbox(1,x,y,w,h);
            try {
                shape.TextFrame.MarginLeft=0; shape.TextFrame.MarginRight=0; shape.TextFrame.MarginTop=0; shape.TextFrame.MarginBottom=0;
                dynamic range=shape.TextFrame.TextRange;
                try { range.Text=text; range.Font.Name="Segoe UI"; range.Font.Size=size; range.Font.Bold=bold ? -1 : 0; range.Font.Color.RGB=Ole(color); }
                finally { Release((object)range); }
            } finally { Release((object)shape); }
            Pace(150);
        }
        static void Rect(dynamic slide,float x,float y,float w,float h,string color) {
            if(!DrawNext()) return;
            dynamic shape=slide.Shapes.AddShape(1,x,y,w,h);
            try { shape.Fill.Solid(); shape.Fill.ForeColor.RGB=Ole(color); shape.Line.Visible=0; }
            finally { Release((object)shape); }
        }
        static void Picture(dynamic slide,string path,float x,float y,float w,float h) {
            if(!DrawNext()) return;
            dynamic picture=slide.Shapes.AddPicture(path,0,-1,x,y,w,h); Release((object)picture); Pace(150);
        }
        internal static int Presentation(dynamic deck,StudioBrief brief,int stage,int turn,string local) {
            deck.PageSetup.SlideWidth=960; deck.PageSetup.SlideHeight=540;
            int cap=stage==0 ? 5 : stage==1 ? 7 : stage==2 ? 8 : 9;
            int current=Math.Min(cap-1,turn/4),desired=current+1;
            bool complete=turn>=cap*4;
            int old=(int)deck.Slides.Count;
            for(int i=old;i<desired;i++) { dynamic added=deck.Slides.Add(i+1,12); Release((object)added); }
            for(int i=0;i<desired;i++) {
                if(complete ? i!=5 && i!=6 : i!=current) continue;
                shapeIndex=0; shapeLimit=complete || turn%4==3 ? Int32.MaxValue : (turn%4+1)*4;
                dynamic slide=deck.Slides[i+1];
                try {
                    dynamic workWindow=deck.Windows[1];
                    try { workWindow.Activate(); workWindow.View.GotoSlide(i+1); } finally { Release((object)workWindow); }
                    if(complete) while(slide.Shapes.Count>0) { dynamic first=slide.Shapes[1]; try { first.Delete(); } finally { Release((object)first); } }
                    shapeSkip=(int)slide.Shapes.Count;
                    slide.FollowMasterBackground=0; slide.Background.Fill.Solid(); slide.Background.Fill.ForeColor.RGB=Ole(brief.Background);
                    Text(slide,StudioBrief.Brand,42,26,650,22,12,brief.Muted,true);
                    Rect(slide,42,66,42,3,brief.Accent);
                    Text(slide,StudioBrief.Disclaimer+"   /   "+StudioBrief.Stages[stage]+"   /   "+(i+1).ToString("D2"),42,510,885,18,8,brief.Muted);
                    if(i==0) {
                        Text(slide,"Designed for\nthe work ahead.",42,110,430,170,48,brief.Ink,true);
                        Text(slide,brief.Collection+"\n"+brief.Theme+"\n\nThree instruments. One design language.",45,320,340,120,18,brief.Muted);
                        Picture(slide,Art(local,brief,0),395,105,550,344);
                    } else if(i==1) {
                        Text(slide,"A quieter kind of bold.",42,100,850,70,42,brief.Ink,true);
                        string[] words={"MATERIAL","PROPORTION","PURPOSE"}; string[] descriptions={brief.Finish,"Thin borders. Soft corners.\nAn honest, visible service path.","Keep the useful.\nRemove the unnecessary."};
                        for(int c=0;c<3;c++) { float x=44+c*303; Rect(slide,x,210,270,5,brief.Accent); Text(slide,words[c],x,238,270,30,13,brief.Accent,true); Text(slide,descriptions[c],x,290,262,145,23,brief.Ink); }
                    } else if(i>=2 && i<=4) {
                        var p=brief.Products[i-2];
                        Text(slide,p.Category,43,103,350,30,13,brief.Accent,true);
                        Text(slide,p.Name,42,145,400,80,52,brief.Ink,true);
                        Text(slide,p.Tagline,44,240,340,76,25,brief.Ink);
                        Text(slide,p.Specification.Replace(" / ","\n"),45,335,345,115,15,brief.Muted);
                        Picture(slide,Art(local,brief,i-2),400,100,545,341);
                        Text(slide,"CONCEPT PRICE  $"+p.Price,490,449,400,30,19,brief.Accent,true);
                    } else if(i==5) {
                        Text(slide,"Designed to add up.",42,98,850,65,42,brief.Ink,true);
                        Text(slide,"Modeled unit economics / USD / excludes freight, tax and development",44,168,850,30,14,brief.Muted);
                        for(int n=0;n<3;n++) { var p=brief.Products[n]; float y=238+n*76;
                            Text(slide,p.Name,45,y,190,30,20,brief.Ink,true);
                            Rect(slide,242,y+2,(float)(p.Cost(stage)/1600*400),22,brief.Accent);
                            Text(slide,"$"+p.Cost(stage).ToString("F2",CultureInfo.InvariantCulture)+" COST   /   "+p.Margin(stage).ToString("P1",CultureInfo.InvariantCulture)+" GROSS MARGIN",655,y,267,52,14,brief.Ink);
                        }
                    } else if(i==6) {
                        Text(slide,stage<2 ? "Make the tradeoffs visible." : stage==2 ? "A constraint becomes a choice." : "Resolve the constraint.\nProtect the idea.",42,100,840,130,40,brief.Ink,true);
                        Text(slide,brief.Decision(stage),46,265,820,105,24,brief.Ink);
                        Rect(slide,45,413,870,2,brief.Accent);
                        Text(slide,"DESIGN INVARIANT   /   QUIET FORM, REPAIRABLE ACCESS, UNCHANGED LAUNCH PRICES",45,437,860,40,12,brief.Muted,true);
                    } else if(i==7) {
                        Text(slide,"A deliberate path to launch.",42,100,850,65,39,brief.Ink,true);
                        string[] names={"DESIGN LOCK","VALIDATION","PILOT BUILD","REVEAL"};
                        Rect(slide,85,290,760,3,brief.Accent);
                        for(int n=0;n<4;n++) { float x=45+n*225; Rect(slide,x+40,282,18,18,brief.Accent); Text(slide,"W"+(n*3+1).ToString("D2"),x,220,210,45,30,brief.Ink,true); Text(slide,names[n],x,329,210,45,13,brief.Accent,true); Text(slide,n==1 ? "Second-source qualification\nand thermal review" : n==2 ? "Serviceability and\nproduction readiness" : n==0 ? "Materials, proportions\nand interface detail" : "One coherent family.\nReady for the work ahead.",x,385,210,70,16,brief.Muted); }
                    } else {
                        Text(slide,"Less noise.\nMore possibility.",42,115,870,180,66,brief.Ink,true);
                        Text(slide,brief.Collection+" / "+brief.Theme,48,365,800,50,24,brief.Accent);
                        Text(slide,"ARC 14     /     CORE STUDIO     /     CANVAS 32",48,444,850,30,16,brief.Muted);
                    }
                } finally { Release((object)slide); }
            }
            dynamic windows=deck.Windows, window=windows[1];
            try { window.Activate(); window.View.GotoSlide(current+1); }
            finally { Release((object)window); Release((object)windows); }
            shapeLimit=Int32.MaxValue; shapeSkip=0;
            return Math.Min(turn+1,cap*4);
        }
        static void Cell(dynamic sheet,string address,object value) {
            dynamic r=sheet.Range[address];
            try {
                if(Convert.ToString((object)r.Value2,CultureInfo.InvariantCulture)!=Convert.ToString(value,CultureInfo.InvariantCulture)) { r.Value2=value; Pace(80); }
            } finally { Release((object)r); }
        }
        static void Banner(dynamic sheet,string address,string text,int size,StudioBrief brief) {
            dynamic r=sheet.Range[address]; try { r.Merge(); r.Value2=text; r.Font.Name="Segoe UI"; r.Font.Size=size; r.Font.Bold=true; r.Font.Color=Ole(brief.Ink); } finally { Release((object)r); }
        }
        static dynamic Sheet(dynamic book,int index,string name) {
            while(book.Worksheets.Count<index) { dynamic added=book.Worksheets.Add(Type.Missing,book.Worksheets[book.Worksheets.Count]); Release((object)added); }
            dynamic sheet=book.Worksheets[index]; sheet.Name=name; sheet.Cells.Font.Name="Segoe UI"; sheet.Cells.Font.Size=11;
            sheet.Columns["A:O"].ColumnWidth=12; sheet.Rows["1:40"].RowHeight=25;
            return sheet;
        }
        internal static int Spreadsheet(dynamic book,dynamic excel,StudioBrief brief,int stage,int turn,string local) {
            int cap=stage==0 ? 8 : stage==1 ? 16 : stage==2 ? 22 : 26;
            int visibleRows=Math.Min(27,(Math.Min(turn,cap-1)+1)*2);
            int completeProducts=visibleRows/9;
            dynamic bom=Sheet(book,2,"Bill of materials");
            try {
                bom.Activate(); excel.ActiveWindow.ScrollRow=Math.Max(1,visibleRows-4);
                Banner(bom,"A1:H2",brief.Collection+" / Component architecture",26,brief);
                Cell(bom,"A4","FICTIONAL BOM / USD / "+StudioBrief.Stages[stage]);
                string[] headers={"Product","Component","Quantity","Base cost","Scenario cost","Extended cost"};
                for(int c=0;c<6;c++) Cell(bom,((char)('A'+c))+"5",headers[c]);
                int row=6;
                foreach(var p in brief.Products) foreach(var part in p.Parts) {
                    if(row-6>=visibleRows) continue;
                    Cell(bom,"A"+row,p.Name); Cell(bom,"B"+row,part.Name); Cell(bom,"C"+row,part.Quantity); Cell(bom,"D"+row,part.Cost);
                    Cell(bom,"E"+row,StudioProduct.UnitCost(part,stage)); bom.Range["F"+row].Formula="=C"+row+"*E"+row; row++;
                }
                bom.Range["A5:F5"].Interior.Color=Ole(brief.Accent); bom.Range["A5:F5"].Font.Color=Ole("#FFFFFF");
                bom.Range["D6:F"+(row-1)].NumberFormat="$#,##0.00"; bom.Columns["A:B"].ColumnWidth=25;
                dynamic dashboard=Sheet(book,1,"Launch dashboard");
                try {
                    dashboard.Range["A1:O35"].Interior.Color=Ole(brief.Background); dashboard.Range["A1:O35"].Font.Color=Ole(brief.Ink);
                    Banner(dashboard,"A1:O2","NORTH / "+brief.Theme,30,brief);
                    Banner(dashboard,"A3:O4","THREE INSTRUMENTS. ONE DESIGN LANGUAGE.",16,brief);
                    Banner(dashboard,"A6:O6",StudioBrief.Stages[stage]+" / Concept economics",14,brief);
                    if(turn>=3) {
                    bool hero=false;
                    dynamic shapes=dashboard.Shapes;
                    try {
                        for(int i=1;i<=shapes.Count;i++) { dynamic shape=shapes.Item(i); try { if((string)shape.Name=="StudioHero") hero=true; } finally { Release((object)shape); } }
                        if(!hero) { dynamic picture=shapes.AddPicture(Art(local,brief,0),0,-1,800,32,300,188); try { picture.Name="StudioHero"; } finally { Release((object)picture); } }
                    } finally { Release((object)shapes); }
                    }
                    string[] columns={"Product","Price","Unit cost","Gross margin","Launch units","Modeled revenue"};
                    for(int c=0;c<6;c++) Cell(dashboard,((char)('A'+c))+"8",columns[c]);
                    for(int i=0;i<completeProducts;i++) { var p=brief.Products[i]; int r=i+9;
                        Cell(dashboard,"A"+r,p.Name); Cell(dashboard,"B"+r,p.Price);
                        dashboard.Range["C"+r].Formula="=SUMIF('Bill of materials'!A6:A"+(row-1)+",A"+r+",'Bill of materials'!F6:F"+(row-1)+")";
                        dashboard.Range["D"+r].Formula="=(B"+r+"-C"+r+")/B"+r;
                        Cell(dashboard,"E"+r,p.Units); dashboard.Range["F"+r].Formula="=B"+r+"*E"+r;
                    }
                    dashboard.Range["A8:F8"].Interior.Color=Ole(brief.Accent); dashboard.Range["A8:F8"].Font.Color=Ole("#FFFFFF");
                    dashboard.Columns["A:A"].ColumnWidth=22; dashboard.Columns["B:F"].ColumnWidth=18;
                    dashboard.Range["B9:C11"].NumberFormat="$#,##0.00"; dashboard.Range["D9:D11"].NumberFormat="0.0%"; dashboard.Range["F9:F11"].NumberFormat="$#,##0";
                    if(completeProducts>=2) {
                    dynamic charts=dashboard.ChartObjects();
                    try {
                        dynamic obj=charts.Count==0 ? charts.Add(18,328,770,285) : charts.Item(1);
                        try { dynamic chart=obj.Chart; try {
                            chart.SetSourceData(dashboard.Range["A8:C"+(8+completeProducts)],2); chart.ChartType=51; chart.HasTitle=true; chart.HasLegend=true;
                            chart.ChartTitle.Text="Concept price and modeled unit cost"; chart.ChartStyle=10;
                            chart.ChartArea.Format.Fill.Solid(); chart.ChartArea.Format.Fill.ForeColor.RGB=Ole(brief.Background);
                            chart.PlotArea.Format.Fill.Solid(); chart.PlotArea.Format.Fill.ForeColor.RGB=Ole(brief.Background);
                            chart.ChartArea.Font.Color=Ole(brief.Ink); chart.ChartTitle.Font.Color=Ole(brief.Ink); chart.Legend.Font.Color=Ole(brief.Ink);
                            chart.Axes(1).TickLabels.Font.Color=Ole(brief.Muted); chart.Axes(2).TickLabels.Font.Color=Ole(brief.Muted);
                            dynamic price=chart.SeriesCollection(1),cost=chart.SeriesCollection(2);
                            try { price.Format.Fill.ForeColor.RGB=Ole(brief.Muted); cost.Format.Fill.ForeColor.RGB=Ole(brief.Accent); }
                            finally { Release((object)price); Release((object)cost); }
                        } finally { Release((object)chart); } }
                        finally { Release((object)obj); }
                    } finally { Release((object)charts); }
                    }
                    Banner(dashboard,"A27:O29",visibleRows<27 ? "WORK IN PROGRESS / "+visibleRows+" of 27 component rows entered. Financial summaries appear after each product BOM is complete." : brief.Decision(stage),12,brief); dashboard.Range["A27:O29"].WrapText=true;
                    Cell(dashboard,"A32",StudioBrief.Disclaimer); Cell(dashboard,"A33","Gross margin excludes logistics, development, channel costs and tax.");
                } finally { Release((object)dashboard); }
            } finally { Release((object)bom); }
            if(stage>=2) {
            dynamic scenario=Sheet(book,3,"Scenario model");
            try {
                Banner(scenario,"A1:H2","The cost of a decision.",28,brief);
                string[] header={"Product","Original cost","Supply shock","Second source","Retained price","Recovered / unit"};
                for(int c=0;c<header.Length;c++) Cell(scenario,((char)('A'+c))+"5",header[c]);
                for(int i=0;i<Math.Min(3,Math.Max(0,turn-15));i++) { var p=brief.Products[i]; int r=i+6; Cell(scenario,"A"+r,p.Name); Cell(scenario,"B"+r,p.Cost(0)); Cell(scenario,"C"+r,p.Cost(2)); if(stage>=3) { Cell(scenario,"D"+r,p.Cost(3)); scenario.Range["F"+r].Formula="=C"+r+"-D"+r; } Cell(scenario,"E"+r,p.Price); }
                scenario.Range["A5:F5"].Interior.Color=Ole(brief.Accent); scenario.Range["A5:F5"].Font.Color=Ole("#FFFFFF");
                scenario.Range["B6:F8"].NumberFormat="$#,##0.00"; scenario.Columns["A:A"].ColumnWidth=24; scenario.Columns["B:F"].ColumnWidth=18;
                Banner(scenario,"A11:F13",stage<3 ? "What-if assumption: memory +18%. Second-source qualification is pending. All fictional." : "What-if assumptions: memory +18% (shock); +6% (qualified second source). All fictional.",12,brief); scenario.Range["A11:F13"].WrapText=true;
            } finally { Release((object)scenario); }
            }
            book.Activate(); excel.Calculate();
            dynamic active=book.Worksheets[visibleRows<27 ? 2 : stage>=2 && turn%2==0 ? 3 : 1]; try { active.Activate(); } finally { Release((object)active); }
            excel.ActiveWindow.DisplayGridlines=false; excel.ActiveWindow.Zoom=90;
            return Math.Min(turn+1,cap);
        }
        sealed class WordWriter {
            readonly int start; int position;
            internal WordWriter(int cursor) { start=cursor; }
            internal bool Take() { int i=position++; return i>=start && i<start+2; }
            internal int Cursor { get { return Math.Min(position,start+2); } }
            internal void Paragraph(dynamic doc,string text,int size,string color,bool bold=false) {
            if(!Take()) return;
            int start=Math.Max(0,(int)doc.Content.End-1); dynamic range=doc.Range(start,start);
            try {
                string content=text+"\r";
                for(int i=0;i<content.Length;i+=8) { range.InsertAfter(content.Substring(i,Math.Min(8,content.Length-i))); doc.ActiveWindow.ScrollIntoView(range,true); Pace(40); }
                range.Font.Name="Segoe UI"; range.Font.Size=size; range.Font.Color=Ole(color); range.Font.Bold=bold ? -1 : 0; range.ParagraphFormat.SpaceAfter=12;
            }
            finally { Release((object)range); }
        }
            internal void Page(dynamic doc) { if(!Take()) return; dynamic end=doc.Range((int)doc.Content.End-1,(int)doc.Content.End-1); try { end.InsertBreak(7); } finally { Release((object)end); } }
        }
        internal static int Word(dynamic doc,StudioBrief brief,int stage,int turn,string local) {
            var writer=new WordWriter(turn);
            doc.PageSetup.TopMargin=48; doc.PageSetup.BottomMargin=45; doc.PageSetup.LeftMargin=48; doc.PageSetup.RightMargin=48;
            writer.Paragraph(doc,StudioBrief.Brand,12,brief.Accent,true);
            writer.Paragraph(doc,"Designed for\rthe work ahead.",46,"#19252B",true);
            writer.Paragraph(doc,brief.Collection+" / "+brief.Theme,19,brief.Accent);
            if(writer.Take()) {
            dynamic at=doc.Range((int)doc.Content.End-1,(int)doc.Content.End-1);
            try { dynamic picture=doc.InlineShapes.AddPicture(Art(local,brief,0),false,true,at); try { picture.Width=480; picture.Height=300; } finally { Release((object)picture); } }
            finally { Release((object)at); }
            }
            writer.Paragraph(doc,"\rDESIGN LANGUAGE + PRODUCT STRATEGY\r"+StudioBrief.Stages[stage],13,brief.Accent,true);
            writer.Page(doc);
            writer.Paragraph(doc,"01 / The design thesis",28,"#19252B",true);
            writer.Paragraph(doc,"Quiet form. Deliberate detail.",20,brief.Accent);
            writer.Paragraph(doc,"A coherent family should feel related before a specification is read. Arc 14, Core Studio and Canvas 32 share softened edges, an uninterrupted primary surface, disciplined port placement and a visible path to service.",12,"#34474E");
            writer.Paragraph(doc,"Material direction",17,brief.Accent,true); writer.Paragraph(doc,brief.Finish+". The finish balances a tactile enclosure with a restrained accent. Material descriptions and sustainability aspirations are concept targets, not verified manufacturing claims.",12,"#34474E");
            writer.Paragraph(doc,"Experience principles",17,brief.Accent,true); writer.Paragraph(doc,"01  Make the primary task obvious.\r02  Design the service path with the product.\r03  Treat acoustics, thermals and accessibility as design inputs.\r04  Keep visual noise low and useful information clear.",12,"#34474E");
            string[] reviews={"Refine the edge radius so the portable and desktop read as one family.","Review port spacing with the service team before the enclosure freeze.","Check contrast, reach and viewing angles with accessibility in mind.","Compare the tactile finish under warm and cool lighting.","Keep acoustic and thermal targets visible in the design review."};
            writer.Paragraph(doc,"Working annotation / review "+(turn+1),13,brief.Accent,true); writer.Paragraph(doc,reviews[turn%reviews.Length],12,"#34474E");
            writer.Page(doc);
            writer.Paragraph(doc,"02 / The product family",28,"#19252B",true);
            foreach(var p in brief.Products) { writer.Paragraph(doc,p.Name+" / "+p.Category,20,brief.Accent,true); writer.Paragraph(doc,p.Tagline+"\r"+p.Specification,12,"#34474E"); writer.Paragraph(doc,"Concept price $"+p.Price+" / modeled unit cost $"+p.Cost(stage).ToString("F2",CultureInfo.InvariantCulture)+" / gross margin "+p.Margin(stage).ToString("P1",CultureInfo.InvariantCulture),11,"#596A71"); }
            if(stage>=1) {
                writer.Page(doc); writer.Paragraph(doc,"03 / Engineering + launch review",28,"#19252B",true);
                writer.Paragraph(doc,brief.Decision(stage),18,brief.Accent,true);
                writer.Paragraph(doc,"Decision record",17,"#19252B",true);
                writer.Paragraph(doc,"Keep launch prices stable. Validate the enclosure, thermal path and component architecture before freezing the bill of materials. Use the workbook for original, supply-shock and second-source cost scenarios; its gross-margin model excludes development, freight, channel costs and tax.",12,"#34474E");
                writer.Paragraph(doc,"Review gates",17,brief.Accent,true);
                writer.Paragraph(doc,"W01 / Design lock — proportions, interfaces and materials.\rW04 / Validation — second-source qualification, thermals and accessibility.\rW07 / Pilot build — serviceability and production readiness.\rW10 / Reveal — one coherent product story.",12,"#34474E");
                writer.Paragraph(doc,"Next review",17,brief.Accent,true); writer.Paragraph(doc,stage<3 ? "Resolve the open assumptions and reconcile all three deliverables before the next gate." : "The revised concept is ready for an internal reveal. Retain this decision record alongside the final financial model and presentation.",12,"#34474E");
            }
            dynamic footer=doc.Sections[1].Footers[1].Range;
            try { footer.Text=StudioBrief.Disclaimer+" / "+StudioBrief.Stages[stage]; footer.Font.Name="Segoe UI"; footer.Font.Size=8; footer.Font.Color=Ole("#596A71"); }
            finally { Release((object)footer); }
            if(writer.Cursor==turn) {
                var note=new WordWriter(0);
                note.Paragraph(doc,"Revision / "+StudioBrief.Stages[stage],13,brief.Accent,true);
                note.Paragraph(doc,brief.Decision(stage),12,"#34474E");
                foreach(var product in brief.Products) {
                    var economics=new WordWriter(0);
                    economics.Paragraph(doc,product.Name+" / updated modeled unit cost $"+product.Cost(stage).ToString("F2",CultureInfo.InvariantCulture)+" / retained concept price $"+product.Price,11,"#596A71");
                }
            }
            doc.Activate();
            // Rotate the visible page as well as the application so the rack shows more than covers.
            dynamic selected=doc.GoTo(1,1,Math.Max(1,Math.Min(stage>=1 ? 4 : 3,1+writer.Cursor/12)));
            try { doc.ActiveWindow.ScrollIntoView(selected,true); } finally { Release((object)selected); }
            return writer.Cursor;
        }
    }
}
