using System;
using System.Collections.Generic;
using System.Text;

namespace AAPlotPrototype.Models.Plots
{
    class PlotEffect
    {
        public uint Id { get; set; }
        public uint EventId { get; set; }
        public int Position { get; set; }
        public uint ActualId { get; set; }
        public string ActualType { get; set; }
    }
}
