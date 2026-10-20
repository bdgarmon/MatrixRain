using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using MatrixOffice;
using MatrixOfficeActor;

// Composition fixture executes the production PowerPoint builder and records its
// editable shape commands. It is not a substitute for a Windows Office pilot.
public class Node : DynamicObject {
    public Dictionary<string,object> Values=new Dictionary<string,object>();
    public override bool TryGetMember(GetMemberBinder b,out object result) { if(!Values.TryGetValue(b.Name,out result)) { result=new Node(); Values[b.Name]=result; } return true; }
    public override bool TrySetMember(SetMemberBinder b,object value) { Values[b.Name]=value; return true; }
    public override bool TryGetIndex(GetIndexBinder b,object[] indexes,out object result) { string key="["+String.Join(",",indexes)+"]";if(!Values.TryGetValue(key,out result))Values[key]=result=new Node();return true; }
    public override bool TryInvokeMember(InvokeMemberBinder b,object[] args,out object result) { Values[b.Name]=args; result=new Node(); return true; }
}
public class Shape {
    public string Kind,Image,Name;
    public float X,Y,W,H;
    public dynamic TextFrame=new Node(),Fill=new Node(),Line=new Node();
    public Shapes Owner;
    public void Delete() { Owner.Items.Remove(this); }
}
public class Shapes {
    public List<Shape> Items=new List<Shape>();
    public int Count {get {return Items.Count;}}
    [System.Runtime.CompilerServices.IndexerName("At")]
    public Shape this[int i] {get {return Items[i-1];}}
    public Shape Item(int index) {return Items[index-1];}
    Shape Add(string kind,float x,float y,float w,float h) {var s=new Shape {Kind=kind,X=x,Y=y,W=w,H=h,Owner=this};Items.Add(s);return s;}
    public Shape AddTextbox(int orientation,float x,float y,float w,float h) {return Add("text",x,y,w,h);}
    public Shape AddShape(int type,float x,float y,float w,float h) {return Add("rect",x,y,w,h);}
    public Shape AddPicture(string path,int link,int save,float x,float y,float w,float h) {var s=Add("image",x,y,w,h);s.Image=path;return s;}
}
public class Slide {public Shapes Shapes=new Shapes(); public int FollowMasterBackground;public dynamic Background=new Node();}
public class Slides {
    public List<Slide> Items=new List<Slide>(); public int Count {get{return Items.Count;}}
    public Slide this[int i] {get{return Items[i-1];}}
    public Slide Add(int index,int layout) {var s=new Slide();Items.Insert(index-1,s);return s;}
}
public class Deck {public Slides Slides=new Slides();public dynamic PageSetup=new Node(),Windows=new Node();}
class Check {
    static int count;
    static void Assert(bool value,string label) {if(!value) throw new Exception(label);count++;Console.WriteLine("PASS: "+label);}
    static object Value(Node node,string[] keys) {object current=node;foreach(string key in keys) {var n=current as Node;if(n==null || !n.Values.TryGetValue(key,out current))return null;}return current;}
    static void Main(string[] args) {
        if(WordWindow.Handle(new StrictWordDocument())!=new IntPtr(12345)) throw new Exception("Word must resolve document Window.Hwnd");
        string root=args.Length==0 ? Path.Combine(Path.GetTempPath(),"StudioPreview") : Path.GetFullPath(args[0]);Directory.CreateDirectory(root);
        var options=new JsonSerializerOptions {IncludeFields=true,WriteIndented=true};
        var themes=new HashSet<string>();
        for(int seed=0;seed<100;seed++) {
            var b=StudioBrief.Create(seed);themes.Add(b.Theme);
            Assert(JsonSerializer.Serialize(b,options)==JsonSerializer.Serialize(StudioBrief.Create(seed),options),"Seed "+seed+" reproduces a coherent brief");
            foreach(var p in b.Products) {
                Assert(p.Cost(2)>p.Cost(3) && p.Cost(3)>p.Cost(0),p.Name+" supplier shock and recovery reconcile");
                Assert(p.Cost(2)<p.Price && p.Margin(2)>0 && p.Margin(2)<1,p.Name+" unit economics remain valid");
            }
        }
        Assert(themes.Count==3,"All three visual directions occur");
        CultureInfo previous=CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");
            var svg=XDocument.Parse(StudioScene.Svg(StudioBrief.Create(1),0));
            foreach(var attr in svg.Descendants().Attributes().Where(a=>new[]{"x","y","width","height","rx","ry","cx","cy"}.Contains(a.Name.LocalName))) {
                double parsed; Assert(Double.TryParse(attr.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out parsed),"Artwork coordinates remain culture invariant");
            }
        } finally {CultureInfo.CurrentCulture=previous;}
        foreach(double invalid in new[]{Double.NaN,Double.PositiveInfinity,Double.NegativeInfinity}) {
            bool rejected=false;try {StudioBrief.Stage(invalid,600);}catch(ArgumentException){rejected=true;}Assert(rejected,"Non-finite story clock rejected");
        }
        Assert(StudioBrief.Stage(0,600)==0 && StudioBrief.Stage(150,600)==1 && StudioBrief.Stage(300,600)==2 && StudioBrief.Stage(450,600)==3 && StudioBrief.Stage(800,600)==3,"Narrative stages follow quarter-day boundaries and clamp at launch");
        var plan=OfficePlan.Create(21600,42);
        Assert(plan.Any(p=>p.Action=="create-word") && plan.Any(p=>p.Action=="edit-word"),"Word creation and editing are scheduled");
        var collected=new HashSet<string>();
        for(int seed=0;collected.Count<3 && seed<100;seed++) {
            var brief=StudioBrief.Create(seed);if(!collected.Add(brief.Theme))continue;
            string dir=Path.Combine(root,brief.Theme.Replace(" ","-"));Directory.CreateDirectory(dir);
            var deck=new Deck();
            int deckCursor=StudioContent.Presentation(deck,brief,0,0,dir);
            Assert(deck.Slides.Count==1 && deck.Slides.Items[0].Shapes.Count==4,"Startup creates one partial slide, not a finished deck");
            var firstSlide=deck.Slides.Items[0];
            var firstShape=firstSlide.Shapes.Items[0];
            deckCursor=StudioContent.Presentation(deck,brief,0,deckCursor,dir);
            Assert(deck.Slides.Count==1 && firstSlide.Shapes.Count>4,"The next edit adds content to the current slide");
            Assert(Object.ReferenceEquals(firstShape,firstSlide.Shapes.Items[0]),"Building a slide retains existing shape objects");
            for(int stage=0;stage<4;stage++) {
                int limit=new[]{20,28,32,36}[stage];
                while(deckCursor<limit) deckCursor=StudioContent.Presentation(deck,brief,stage,deckCursor,dir);
                Assert(deck.Slides.Count==new[]{5,7,8,9}[stage],brief.Theme+" stage "+stage+" reveals the correct number of slides");
                Assert(Object.ReferenceEquals(firstSlide,deck.Slides.Items[0]),"Progression preserves previously built slides");
            }
            foreach(var slide in deck.Slides.Items) {
                Assert(slide.Shapes.Items.All(s=>s.X>=0 && s.Y>=0 && s.W>0 && s.H>0 && s.X+s.W<=960 && s.Y+s.H<=540),"Slide commands stay on the 16:9 canvas");
                Assert(slide.Shapes.Items.Count(s=>s.Kind=="text")>=3,"Native editable typography remains on every slide");
            }
            var rendered=deck.Slides.Items.Select(s=>new {Background=brief.Background,Shapes=s.Shapes.Items.Select(v=>new {
                v.Kind,v.X,v.Y,v.W,v.H,v.Image,
                Text=Value((Node)v.TextFrame,new[]{"TextRange","Text"}),Size=Value((Node)v.TextFrame,new[]{"TextRange","Font","Size"}),
                Color=Value((Node)v.TextFrame,new[]{"TextRange","Font","Color","RGB"}),Bold=Value((Node)v.TextFrame,new[]{"TextRange","Font","Bold"}),
                Fill=Value((Node)v.Fill,new[]{"ForeColor","RGB"}) }).ToArray()}).ToArray();
            File.WriteAllText(Path.Combine(dir,"slides.json"),JsonSerializer.Serialize(rendered,options));
            for(int i=0;i<3;i++) {
                var scene=StudioScene.Create(brief,i);Assert(scene.Count>20,"Product art contains a detailed native scene");
                File.WriteAllText(Path.Combine(dir,"product-"+i+".json"),JsonSerializer.Serialize(new {brief.Background,Shapes=scene},options));
                XDocument.Parse(StudioScene.Svg(brief,i));
            }
            int before=deck.Slides.Count;
            StudioContent.Presentation(deck,brief,3,deckCursor,dir);
            Assert(deck.Slides.Count==before,"Ordinary edits preserve the existing slide count");
            var secondDeck=new Deck(); StudioContent.Presentation(secondDeck,brief,3,0,dir);
            Assert(secondDeck.Slides.Count==1 && secondDeck.Slides.Items[0].Shapes.Count==4,"A new file starts at its own first edit even late in the story");
            var book=new Workbook();var excel=new Excel();var word=new WordDocument();
            int bookCursor=StudioContent.Spreadsheet(book,excel,brief,0,0,dir);
            Assert(book.Worksheets.Count==2 && book.Worksheets[1].ChartList.Count==0,"Startup does not create the chart or scenario sheet");
            Assert(book.Worksheets[2].Data.ContainsKey("A7") && !book.Worksheets[2].Data.ContainsKey("A8"),"Initial workbook enters only two component rows");
            Assert(!book.Worksheets[1].Data.ContainsKey("B9"),"Financial summaries wait for a complete product BOM");
            var retainedCell=book.Worksheets[2].Data["A6"];
            int wordCursor=StudioContent.Word(word,brief,0,0,dir);
            Assert(word.Paragraphs.Count==2 && !String.Join(" ",word.Paragraphs.Select(p=>p.Text)).Contains("The product family"),"Word begins with two text additions, not the completed brief");
            var retainedParagraph=word.Paragraphs[0];
            for(int stage=0;stage<4;stage++) {
                int cap=new[]{8,16,22,26}[stage];
                while(bookCursor<cap) bookCursor=StudioContent.Spreadsheet(book,excel,brief,stage,bookCursor,dir);
                Assert(Object.ReferenceEquals(retainedCell,book.Worksheets[2].Data["A6"]),"Spreadsheet edits preserve existing cells");
                Assert(book.Worksheets.Count==(stage<2 ? 2 : 3) && book.Worksheets[1].ChartList.Count==(stage==0 ? 0 : 1),"Chart and scenario sheet arrive only after their prerequisites");
                Assert(book.Worksheets[1].Shapes.Count==1,"Dashboard hero image is embedded once, not duplicated by edits");
                for(int p=0;p<(stage==0 ? 1 : 3);p++) {
                    Assert((int)book.Worksheets[1].Data["B"+(p+9)].Value2==brief.Products[p].Price,"Dashboard price matches the shared launch brief");
                    if(stage>=2) Assert((double)book.Worksheets[3].Data["C"+(p+6)].Value2==brief.Products[p].Cost(2),"Scenario sheet carries the same supplier shock");
                    if(stage==2) Assert(!book.Worksheets[3].Data.ContainsKey("D"+(p+6)),"Second-source results are withheld until Reveal");
                }
                Assert(book.Worksheets[1].Data["C9"].Formula.Contains("SUMIF('Bill of materials'!"),"Dashboard cost uses native Excel BOM formulas");
                Assert(book.Worksheets[1].Data["D9"].Formula=="=(B9-C9)/B9","Gross margin is recalculated by Excel");
                for(int edit=0;edit<40;edit++) {int next=StudioContent.Word(word,brief,stage,wordCursor,dir);if(next==wordCursor)break;wordCursor=next;}
                Assert(Object.ReferenceEquals(retainedParagraph,word.Paragraphs[0]),"Word preserves earlier paragraphs while appending sections and revisions");
                string prose=String.Join("\n",word.Paragraphs.Select(p=>p.Text));
                foreach(var p in brief.Products) Assert(prose.Contains(p.Name) && prose.Contains(p.Cost(stage).ToString("F2",CultureInfo.InvariantCulture)),"Word product economics match workbook and slides");
                Assert(prose.Contains(StudioBrief.Stages[stage]) && (stage<1 || prose.Contains(brief.Decision(stage))),"Word reflects the current design decision and revision");
                Assert(brief.EmailBody(stage).Contains(brief.Decision(stage)),"Outlook draft carries the same design decision");
            }
        }
        Console.WriteLine("TOTAL: "+count+" studio model/composition checks passed.");
    }
}
public sealed class StrictWordDocument {
    public StrictWordWindow ActiveWindow { get { return new StrictWordWindow(); } }
}
public sealed class StrictWordWindow {
    public int Hwnd { get { return 12345; } }
}
