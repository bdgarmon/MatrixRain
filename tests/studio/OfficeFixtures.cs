using System;
using System.Collections.Generic;

public class Cell : Node {
    public object Value2;
    public string Formula,NumberFormat;
    public bool WrapText;
    public dynamic Font=new Node(),Interior=new Node();
    public void Merge() { }
}
public class Ranges {
    readonly Dictionary<string,Cell> values;
    public Ranges(Dictionary<string,Cell> cells) {values=cells;}
    public Cell this[string address] { get { Cell c;if(!values.TryGetValue(address,out c))values[address]=c=new Cell();return c; } }
}
public class Cells {
    readonly Dictionary<string,Cell> values;
    public dynamic Font=new Node();
    public Cells(Dictionary<string,Cell> cells) {values=cells;}
    public void Clear() {throw new Exception("Progressive spreadsheet must not clear existing cells");}
}
public class ChartObject {
    public Charts Owner;public dynamic Chart=new Node();
    public void Delete() {Owner.Items.Remove(this);}
}
public class Charts {
    public List<ChartObject> Items=new List<ChartObject>(); public int Count {get{return Items.Count;}}
    public ChartObject Item(int index) {return Items[index-1];}
    public ChartObject Add(int left,int top,int width,int height) {var c=new ChartObject {Owner=this};Items.Add(c);return c;}
}
public class Worksheet {
    public string Name;
    public Dictionary<string,Cell> Data=new Dictionary<string,Cell>();
    public Ranges Range;
    public Cells Cells;
    public dynamic Columns=new Node(),Rows=new Node();
    public Charts ChartList=new Charts();
    public Shapes Shapes=new Shapes();
    public Worksheet() {Range=new Ranges(Data);Cells=new Cells(Data);}
    public Charts ChartObjects() {return ChartList;}
    public void Activate() { }
}
public class Worksheets {
    public List<Worksheet> Items=new List<Worksheet> {new Worksheet()};public int Count {get{return Items.Count;}}
    public Worksheet this[int index] {get{return Items[index-1];}}
    public Worksheet Add(object before,object after) {var s=new Worksheet();Items.Add(s);return s;}
}
public class Workbook {
    public Worksheets Worksheets=new Worksheets();public void Activate() { }
}
public class Excel {
    public dynamic ActiveWindow=new Node();public int Calculations;
    public void Calculate() {Calculations++;}
}
public class WordContent {
    readonly WordDocument doc;
    public WordContent(WordDocument d) {doc=d;}
    public int End {get{return doc.End;}}
    public void Delete() {throw new Exception("Progressive Word must append rather than replace the document");}
}
public class WordRange {
    public WordDocument Owner;
    public string Text;
    public dynamic Font=new Node(),ParagraphFormat=new Node();
    public void InsertAfter(string text) {Text+=text;if(!Owner.Paragraphs.Contains(this))Owner.Paragraphs.Add(this);Owner.End+=text.Length;}
    public void InsertBreak(int kind) {Owner.End++;}
}
public class InlinePictures {
    public dynamic AddPicture(string path,bool link,bool save,object range) {return new Node();}
}
public class WordDocument {
    public int End=1;
    public List<WordRange> Paragraphs=new List<WordRange>();
    public WordContent Content;
    public dynamic PageSetup=new Node(),Sections=new Node(),ActiveWindow=new Node();
    public InlinePictures InlineShapes=new InlinePictures();
    public WordDocument() {Content=new WordContent(this);}
    public WordRange Range(int start,int end) {if(start<0 || end<start || end>=End)throw new Exception("Invalid Word range");return new WordRange {Owner=this};}
    public void Activate() { }
    public object GoTo(int what,int which,int count) {return new Node();}
}
