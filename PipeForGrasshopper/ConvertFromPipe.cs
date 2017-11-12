﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PipeDataModel.Types.Geometry;
using PipeDataModel.Types;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace PipeForGrasshopper
{
    public class ConvertFromPipe
    {
        public static IGH_Goo ConvertObject(IPipeData data)
        {
            if(data.Value == null) { return null; }

            if (typeof(double).IsAssignableFrom(data.Value.GetType()))
            {
                return new GH_Number((double)data.Value);
            }
            if (typeof(string).IsAssignableFrom(data.Value.GetType()))
            {
                return new GH_String((string)data.Value);
            }
            else if (typeof(Vec).IsAssignableFrom(data.Value.GetType()))
            {
                return ConvertPoint((Vec)data.Value);
            }
            else if (typeof(PipeDataModel.Types.Geometry.Line).IsAssignableFrom(data.Value.GetType()))
            {
                return ConvertLine((PipeDataModel.Types.Geometry.Line)data.Value);
            }
            else { return null; }
        }
        public static GH_Point ConvertPoint(Vec v)
        {
            if(v.Dimensions != 3) { return null; }
            return new GH_Point(new Point3d(v.Coordinates[0], v.Coordinates[1], v.Coordinates[2]));
        }

        public static GH_Line ConvertLine(PipeDataModel.Types.Geometry.Line line)
        {
            return new GH_Line(new Rhino.Geometry.Line(ConvertPoint(line.StartPoint).Value, ConvertPoint(line.EndPoint).Value));
        }
    }
}