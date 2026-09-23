using System;
using System.Collections.Generic;
using System.Text.Json;
namespace System.Web.Script.Serialization {
    public class JavaScriptSerializer {
        public string Serialize(object value) { return JsonSerializer.Serialize(value, new JsonSerializerOptions { IncludeFields=true }); }
        public T Deserialize<T>(string text) {
            if(typeof(T)==typeof(Dictionary<string,object>)) {
                var parsed=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(text);
                var result=new Dictionary<string,object>();
                foreach(var kv in parsed) result[kv.Key]=kv.Value.ValueKind==JsonValueKind.String ? kv.Value.GetString() : null;
                return (T)(object)result;
            }
            return JsonSerializer.Deserialize<T>(text,new JsonSerializerOptions { IncludeFields=true });
        }
    }
}
