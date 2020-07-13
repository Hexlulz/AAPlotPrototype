using System;
using System.Collections.Generic;
using System.Text;

namespace AAPlotPrototype.Models.Plots
{
    public class PlotNextEvent
    {
        public uint Id { get; set; }
        public uint EventId { get; set;}
        public int Position { get; set; }
        public uint NextEventId { get; set; }
        public bool PerTarget { get; set; }
        public bool Casting { get; set; }
        public int Delay { get; set; }
        public int Speed { get; set; }
        public bool Channeling { get; set; }
        public bool Fail { get; set; }
    }
}
