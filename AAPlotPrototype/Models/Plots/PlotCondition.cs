using AAPlotPrototype.Models.Plots.Static;
using System;
using System.Collections.Generic;
using System.Text;

namespace AAPlotPrototype.Models.Plots
{
    public class PlotCondition
    {
        public uint Id { get; set; }
        public uint EventId { get; set; }
        public int Position { get; set; }
        public bool Not_condition { get; set; }
        public PlotConditionType Kind_id { get; set; }
        public int Param1 { get; set; }
        public int Param2 { get; set; }
        public int Param3 { get; set; }
    }
}
