using System;

namespace ClockQuantization.Tests.Assets
{
    class MetronomeOptions
    {
        public static readonly MetronomeOptions Default = new MetronomeOptions
        {
            MaxIntervalTimeSpan = TimeSpan.FromMinutes(1),
            IsManual = false,
            StartSuspended = true,
        };

        public static readonly MetronomeOptions Manual = new MetronomeOptions
        {
            MaxIntervalTimeSpan = TimeSpan.FromMinutes(1),
            IsManual = true,
            StartSuspended = true,
        };

        public static readonly MetronomeOptions Automatic = new MetronomeOptions
        {
            MaxIntervalTimeSpan = TimeSpan.FromMinutes(1),
            IsManual = false,
            StartSuspended = false,
        };

        public TimeSpan MaxIntervalTimeSpan { get; set; }
        public bool IsManual { get; set; }
        public bool StartSuspended { get; set; }
    }
}
