using System;
using System.Threading;
using System.Windows.Automation;
using MatrixOffice;
namespace MatrixOfficeActor {
    // New Outlook has no supported desktop COM API. This helper only writes after it
    // finds the newly-created compose controls through Windows UI Automation.
    internal static class OutlookDraft {
        static AutomationElement First(AutomationElement root,Condition condition) {
            return root==null ? null : root.FindFirst(TreeScope.Descendants,condition);
        }
        static bool Set(AutomationElement element,string value) {
            if(element==null) return false;
            object pattern;
            if(!element.TryGetCurrentPattern(ValuePattern.Pattern,out pattern)) return false;
            ((ValuePattern)pattern).SetValue(value); return true;
        }
        static AutomationElement Subject(AutomationElement root) {
            var names=new[]{"Subject","Add a subject","Subject line"};
            foreach(var name in names) {
                var found=First(root,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit),new PropertyCondition(AutomationElement.NameProperty,name)));
                if(found!=null) return found;
            }
            return null;
        }
        static AutomationElement Body(AutomationElement root) {
            var names=new[]{"Message body","Message","Body"};
            foreach(var name in names) {
                var found=First(root,new PropertyCondition(AutomationElement.NameProperty,name));
                object pattern;
                if(found!=null && found.TryGetCurrentPattern(ValuePattern.Pattern,out pattern)) return found;
            }
            return null;
        }
        internal static bool TryCreateAndClose(IntPtr window,int seed,Action<string> log) {
            return TryCreateAndClose(window,OutlookDraftPlan.Subject(seed),OutlookDraftPlan.Body(seed),log);
        }
        internal static bool TryCreateAndClose(IntPtr window,string subjectText,string bodyText,Action<string> log) {
            if(!Native.NewOutlookMessage(window)) { log("OUTLOOK DRAFT skipped: compose keyboard shortcut was not safe to send."); return false; }
            AutomationElement root=null,subject=null,body=null;
            for(int i=0;i<30 && (subject==null || body==null);i++) {
                try { root=AutomationElement.FromHandle(window); subject=Subject(root); body=Body(root); }
                catch(Exception) { }
                if(subject==null || body==null) Thread.Sleep(250);
            }
            if(subject==null || body==null) { log("OUTLOOK DRAFT skipped: New Outlook compose fields were not exposed by UI Automation."); return false; }
            try {
                if(!Set(subject,subjectText) || !Set(body,bodyText)) { log("OUTLOOK DRAFT skipped: compose fields do not permit automation values."); return false; }
                // Apply a small formatting change only inside the draft body. We avoid
                // Ctrl+Enter and never set a recipient, so this helper cannot send mail.
                body.SetFocus(); Thread.Sleep(100);
                bool formatted=Native.BoldComposeHeader();
                log("OUTLOOK DRAFT composed with empty recipient field; header bold="+formatted+"; closing without send.");
                Native.CloseComposeWithEscape(); return true;
            } catch(Exception e) { log("OUTLOOK DRAFT skipped: "+e.GetType().Name+": "+e.Message); return false; }
        }
    }
}
