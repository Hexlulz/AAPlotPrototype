using AAPlotPrototype.Models.Plots;
using System;
using System.Collections.Generic;

namespace AAPlotPrototype
{
    class Program
    {
        static void Main(string[] args)
        {
            PlotManager manager = new PlotManager();
            manager.LoadDb(@"URI=file:D:\compact.db");
            //manager.LoadDb(@"URI=file:D:\archeagev5.db");
            manager.AnalyzePlot(2169);
        }
    }
}
