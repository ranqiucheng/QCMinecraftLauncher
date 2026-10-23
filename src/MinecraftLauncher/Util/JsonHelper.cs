using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace MinecraftLauncher.Util
{
    /// <summary>轻量 JSON 封装，基于 .NET Framework 内置的 JavaScriptSerializer。</summary>
    public static class Json
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue,
            RecursionLimit = 256
        };

        public static T Deserialize<T>(string json)
        {
            return Serializer.Deserialize<T>(json);
        }

        public static Dictionary<string, object> DeserializeObject(string json)
        {
            return Serializer.Deserialize<Dictionary<string, object>>(json);
        }

        public static object DeserializeAny(string json)
        {
            return Serializer.Deserialize<object>(json);
        }

        public static string Serialize(object obj)
        {
            return Serializer.Serialize(obj);
        }
    }
}