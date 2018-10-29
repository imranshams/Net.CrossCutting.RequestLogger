using System;
using System.Collections.Generic;
using System.Text;

namespace Net.CrossCutting.RequestLogger.Setting
{
    public interface IRequestLogSetting
    {
        bool Status { get; set; }
        string SqlServerConnectionString { get; set; }
        string SqlServerTableName { get; set; }
        string Provider { get; set; }
        string FilePath { get; set; }
    }
}
