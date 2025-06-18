using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Models
{
    public class HearthBeatReponse
    {

        public string Type { get; set; } = "HEARTBEAT_RESPONSE";
        public string ClientIP { get; set; }
        public DateTime Timestamp { get; set; }
    }

}
