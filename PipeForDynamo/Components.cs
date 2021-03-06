﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.DesignScript.Geometry;
using PipeDataModel.DataTree;
using PipeDataModel.Types;
using PipeDataModel.Pipe;
using PipeForDynamo.Converters;

namespace PipeForDynamo
{
    public class DynamoPipeComponents
    {
        internal static DynamoPipeConverter _converter = new DynamoPipeConverter();

        public static object PullFromPipe(string pipeIdentifier)
        {
            DynamoPipeReceiver receiver = DynamoPipeReceiver.GetReceiver(pipeIdentifier, _converter);
            if (receiver.Update()){ return receiver.Data; }
            return receiver.Message;
        }

        public static object PushToPipe(string pipeIdentifier, object data)
        {
            DynamoPipeSender sender = DynamoPipeSender.GetSender(pipeIdentifier, _converter);
            sender.Data = data;
            bool success = sender.Update();
            if (success) { return true; }
            else { return sender.Message; }
        }
    }
}
