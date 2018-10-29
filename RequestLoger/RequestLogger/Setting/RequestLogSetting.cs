using System;
using System.Collections.Generic;
using System.Text;

namespace Net.CrossCutting.RequestLogger.Setting
{
    public class RequestLogSetting : IRequestLogSetting
    {
        public bool Status { get; set; }
        public string SqlServerConnectionString { get; set; }
        public string SqlServerTableName { get; set; }
        public string Provider { get; set; }
        public string FilePath { get; set; }
    }
}
