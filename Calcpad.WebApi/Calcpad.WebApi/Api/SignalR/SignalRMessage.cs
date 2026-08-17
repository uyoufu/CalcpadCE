using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calcpad.WebApi.Api.SignalR
{
    public class SignalRMessage
    {
        [JsonProperty("fromUserId")]
        public string FromUserId { get; set; } = string.Empty;

        [JsonProperty("toRoomIds")]
        public List<string> ToRoomIdList { get; set; } = [];

        [JsonProperty("toUserIds")]
        public List<string> ToUserIdList { get; set; } = [];

        /// <summary>
        /// Method 就是前端的 eventName
        /// </summary>
        [JsonProperty("method")]
        public string Method { get; set; } = "default";

        [JsonProperty("payloads")]
        public SignalRPyloads Payloads { get; set; } = new();

        public SignalRMessage FromUser(string fromUserId)
        {
            FromUserId = fromUserId;
            return this;
        }

        public SignalRMessage ToRooms(params string[] roomIds)
        {
            ToRoomIdList = CleanIds(roomIds);
            return this;
        }

        public SignalRMessage ToUsers(params string[] userIds)
        {
            ToUserIdList = CleanIds(userIds);
            return this;
        }

        public SignalRMessage WithMethod(string methodName)
        {
            Method = string.IsNullOrWhiteSpace(methodName) ? "default" : methodName;
            return this;
        }

        public SignalRMessage Status(int status, string statusText = "")
        {
            Payloads.Status = status;
            if (!string.IsNullOrWhiteSpace(statusText))
                Payloads.StatusText = statusText;
            return this;
        }

        public SignalRMessage Type(string type, string typeData = "")
        {
            Payloads.Type = string.IsNullOrWhiteSpace(type) ? "default" : type;
            Payloads.TypeData = typeData;
            return this;
        }

        public SignalRMessage Command(string command)
        {
            Payloads.Command = string.IsNullOrWhiteSpace(command) ? "default" : command;
            return this;
        }

        public SignalRMessage Message(string message)
        {
            Payloads.Message = message;
            return this;
        }

        public SignalRMessage Background(bool isBackground = true)
        {
            Payloads.IsBackground = isBackground;
            return this;
        }

        public SignalRMessage PayloadsData(JObject? data)
        {
            Payloads.Data = data ?? new JObject();
            return this;
        }

        private static List<string> CleanIds(IEnumerable<string> ids)
        {
            return ids.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct()
                .ToList();
        }
    }

    public class SignalRPyloads
    {
        public int Status { get; set; } = 200;

        public string StatusText { get; set; } = "success";

        public string Type { get; set; } = "default";

        public string TypeData { get; set; } = string.Empty;

        public bool IsBackground { get; set; } = false;

        public string Message { get; set; } = "success";

        public string Command { get; set; } = "default";

        public JObject Data { get; set; } = new();
    }
}
