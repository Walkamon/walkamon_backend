using System;
using System.Collections.Generic;

namespace DAL.Models;

public partial class SystemSetting
{
    public string SettingKey { get; set; } = null!;

    public string SettingValue { get; set; } = null!;

    public DateTime UpdatedAt { get; set; }
}
