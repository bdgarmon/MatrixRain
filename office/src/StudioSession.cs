using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using MatrixOffice;

namespace MatrixOfficeActor {
    internal sealed partial class Applications {
        StudioBrief studio;
        int storyStage,renderedStage;
        readonly Dictionary<string,int> studioEdits=new Dictionary<string,int>();
        int StudioCursor(string path) { int value; return studioEdits.TryGetValue(path,out value) ? value : 0; }
        void StudioAdvanced(string path,int cursor) {
            studioEdits[path]=cursor;
            log("STUDIO BUILD / "+Path.GetFileName(path)+" / cursor="+cursor+" / "+StudioBrief.Stages[storyStage]);
        }
        dynamic word;
        bool quitWord;
        HashSet<int> wordProcessesBefore;
        int workbookReview,presentationReview,wordReview;
        readonly Dictionary<string,object> wordDocuments=new Dictionary<string,object>();
        void InitializeStudio(int seed) {
            studio=StudioBrief.Create(seed); renderedStage=0;
            progress.StudioCollection=studio.Collection; progress.StudioTheme=studio.Theme; progress.StudioStage=StudioBrief.Stages[0];
            RecordStudio();
            log("HARDWARE LAUNCH STUDIO / "+studio.Collection+" / "+studio.Theme+" / seed="+seed);
        }
        void RecordStudio() {
            string path=Path.Combine(documents,"Hardware-Launch-Studio.json");
            bool exists=File.Exists(path);
            string content=new JavaScriptSerializer().Serialize(new { Collection=studio.Collection,Theme=studio.Theme,Finish=studio.Finish,
                Stage=StudioBrief.Stages[storyStage],Decision=studio.Decision(storyStage),Disclaimer=StudioBrief.Disclaimer,Products=studio.Products,
                Economics=studio.Products.Select(p=>new { Product=p.Name,Price=p.Price,UnitCost=p.Cost(storyStage),GrossMargin=p.Margin(storyStage) }).ToArray() });
            File.WriteAllText(path,content); File.WriteAllText(Path.Combine(local,"studio-brief.json"),content);
            Saved(path,!exists);
        }
        public void SetStoryStage(int stage) {
            if(stage<0 || stage>3) throw new ArgumentOutOfRangeException("stage");
            storyStage=stage;
            progress.StudioStage=StudioBrief.Stages[stage];
        }
        void PrimeStudio() {
            // Seed working documents, never complete deliverables. Each foreground
            // work interval and planned edit advances that file's own build cursor.
            foreach(string kind in new[]{"xlsx","pptx","docx"}) {
                if(files[kind].Count>0) continue;
                Guard(); string path=NewPath(kind);
                startupStage("Studio working draft / "+Path.GetFileName(path));
                if(kind=="xlsx") Spreadsheet(path,true); else if(kind=="pptx") Presentation(path,true); else WordDocument(path,true);
                Saved(path,true); files[kind].Add(path);
            }
        }
        void ReviewStudioView(IntPtr window,string name) {
            if(name=="Excel" && books.Count>0) {
                string path=books.Keys.Last(); int before=StudioCursor(path); Guard(); Spreadsheet(path,false); Saved(path,false);
                if(StudioCursor(path)>before) return;
                dynamic book=books.Values.Last(); book.Activate();
                int index=new[]{1,1,2,3}[workbookReview++%4]; index=Math.Min(index,(int)book.Worksheets.Count); dynamic sheet=book.Worksheets[index];
                try { sheet.Activate(); } finally { Release((object)sheet); }
                log("STUDIO VIEW / Excel / sheet "+index);
            } else if(name=="PowerPoint") {
                string title=Native.Title(window);
                var match=decks.FirstOrDefault(p=>title.IndexOf(Path.GetFileNameWithoutExtension(p.Key),StringComparison.OrdinalIgnoreCase)>=0);
                if(match.Value!=null) {
                    int before=StudioCursor(match.Key); Guard(); Presentation(match.Key,false); Saved(match.Key,false);
                    if(StudioCursor(match.Key)>before) return;
                    dynamic deck=match.Value,view=deck.Windows[1];
                    try { int slide=presentationReview++%(int)deck.Slides.Count+1; view.View.GotoSlide(slide); log("STUDIO VIEW / PowerPoint / slide "+slide); }
                    finally { Release((object)view); }
                }
            } else if(name=="Word" && wordDocuments.Count>0) {
                string path=wordDocuments.Keys.Last(); int before=StudioCursor(path); Guard(); WordDocument(path,false); Saved(path,false);
                if(StudioCursor(path)>before) return;
                dynamic doc=wordDocuments.Values.Last(); doc.Activate();
                int pages=Math.Max(1,Math.Min(storyStage>=1 ? 4 : 3,1+StudioCursor(path)/12));
                int page=wordReview++%pages+1; dynamic range=doc.GoTo(1,1,page);
                try { doc.ActiveWindow.ScrollIntoView(range,true); log("STUDIO VIEW / Word / page "+page); } finally { Release((object)range); }
            }
        }
        void RefreshStudio() {
            if(renderedStage==storyStage) return;
            log("STUDIO STORY TRANSITION / "+StudioBrief.Stages[storyStage]+" / "+studio.Decision(storyStage));
            RecordStudio();
            // A story transition unlocks later content. It must not fill all three
            // documents in the background; the next focused edit builds that content.
            renderedStage=storyStage;
        }
        void OpenWord() {
            if(word!=null) return;
            wordProcessesBefore=Native.ProcessIds("WINWORD");
            word=StartOffice("Word","Word.Application","WINWORD");
            word.Visible=true; word.WindowState=1; word.DisplayAlerts=0; word.AutomationSecurity=3;
            // Word exposes Hwnd on a document Window, not Application. A new
            // automation instance can have no windows until Documents.Add.
            progress.AppOpens++; log("Opened Word automation; awaiting owned document window.");
        }
        void WordDocument(string path,bool create) {
            OpenWord(); dynamic doc;
            if(!wordDocuments.ContainsKey(path)) { doc=create ? word.Documents.Add() : word.Documents.Open(path,false,false); wordDocuments[path]=doc; }
            else doc=wordDocuments[path];
            IntPtr window=WordWindow.Handle((object)doc);
            int pid=Native.WindowPid(window);
            if(pid>0 && wordProcessesBefore!=null && !wordProcessesBefore.Contains(pid) && !quitWord) { TrackProcess(pid); quitWord=true; }
            Focus(window,"Word");
            doc.Activate();
            int cursor=StudioContent.Word(doc,studio,storyStage,StudioCursor(path),local);
            if(create) doc.SaveAs2(path,16); else doc.Save();
            StudioAdvanced(path,cursor);
            foreach(string key in wordDocuments.Keys.Where(k=>k!=path).Take(Math.Max(0,wordDocuments.Count-2)).ToArray()) {
                dynamic old=wordDocuments[key]; old.Close(0); Release((object)old); wordDocuments.Remove(key);
            }
        }
        void AddWordFocus(List<KeyValuePair<IntPtr,string>> targets) {
            foreach(var doc in wordDocuments.Values) targets.Add(new KeyValuePair<IntPtr,string>(WordWindow.Handle(doc),"Word"));
        }
        void CloseWord(List<Exception> errors) {
            bool clean=true;
            foreach(var entry in wordDocuments.ToArray()) {
                dynamic doc=entry.Value;
                bool saved=CleanupSteps.Try("Save Word document: "+entry.Key,delegate { doc.Save(); Saved(entry.Key,false); },errors);
                bool closed=saved && CleanupSteps.Try("Close Word document: "+entry.Key,delegate { doc.Close(0); },errors);
                clean=clean && saved && closed;
                if(closed) { CleanupSteps.Try("Release Word document: "+entry.Key,delegate { Release((object)doc); },errors); wordDocuments.Remove(entry.Key); }
            }
            if(word!=null && clean) CleanupSteps.Try("Quit/release Word",delegate {
                if(quitWord) { word.Quit(0); progress.AppCloses++; } Release((object)word); word=null; quitWord=false;
            },errors);
        }
    }
}
