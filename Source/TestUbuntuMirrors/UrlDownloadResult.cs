using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestUbuntuMirrors
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum UrlCheckResult
	{
        [Description("Ok")]
        Ok,
        [Description("NotFound")]
        NotFound,
        [Description("TimeOut")]
        TimeOut,
        [Description("OtherError")]
        OtherError
	}
}
