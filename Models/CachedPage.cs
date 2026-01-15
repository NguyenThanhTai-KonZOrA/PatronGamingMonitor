using System;
using System.Collections.Generic;

namespace PatronGamingMonitor.Models
{
    public sealed class CachedPage
    {
        public List<LevyTicket> Items { get; set; } = new List<LevyTicket>();
        public int TotalCount { get; set; } = 0;
        public DateTime LastSync { get; set; } = DateTime.Now;
    }
}