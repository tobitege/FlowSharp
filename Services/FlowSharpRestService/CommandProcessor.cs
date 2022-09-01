﻿/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using Clifton.Core.Utils;
using Clifton.Core.ExtensionMethods;
using Clifton.Core.Semantics;

using FlowSharpLib;
using FlowSharpServiceInterfaces;
using FlowSharpCodeServiceInterfaces;
using FlowSharpCodeShapeInterfaces;

namespace FlowSharpRestService
{
    public class CommandProcessor : IReceptor
    {
        // Ex: localhost:8001/flowsharp?cmd=CmdUpdateProperty&Name=btnTest&PropertyName=Text&Value=Foobar
        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdUpdateProperty cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            controller.Canvas.FindForm().BeginInvoke(() =>
            {
                var els = controller.Elements.Where(e => e.Name == cmd.Name);
                els.ForEach(el =>
                {
                    var pi = el.GetType().GetProperty(cmd.PropertyName);
                    if (pi == null) return;
                    var cval = Converter.Convert(cmd.Value, pi.PropertyType);

                    el?.Canvas.BeginInvoke(() =>
                    {
                        pi.SetValue(el, cval);
                        controller.Redraw(el);
                    });
                });
            });
        }

        // Ex: localhost:8001:flowsharp?cmd=CmdShowShape&Name=btnTest
        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdShowShape cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            controller.Canvas.FindForm().BeginInvoke(() =>
            {
                var el = controller.Elements.FirstOrDefault(e => e.Name == cmd.Name);
                el?.Canvas.BeginInvoke(() =>
                {
                    controller.FocusOn(el);
                });
            });
        }

        // Ex: localhost:8001/flowsharp?cmd=CmdGetShapeFiles
        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdGetShapeFiles cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;

            // Invoke, because we have to get the filenames before we respond.
            controller.Canvas.FindForm().Invoke(() =>
            {
                var els = controller.Elements.Where(e => e is IFileBox);
                cmd.Filenames.AddRange(els.Cast<IFileBox>().Where(el => !string.IsNullOrEmpty(el.Filename)).Select(el => el.Filename));
            });
        }

        // FlowSharpCodeOutputWindowService required for this behavior.
        // Ex: localhost:8001/flowsharp?cmd=CmdOutputMessage&Text=foobar
        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdOutputMessage cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var w = proc.ServiceManager.Get<IFlowSharpCodeOutputWindowService>();

            controller.Canvas.FindForm().BeginInvoke(() =>
            {
                cmd.Text.Split('\n').Where(s => !string.IsNullOrEmpty(s.Trim())).ForEach(s => w.WriteLine(s.Trim()));
            });
        }

        // Ex: localhost:8001/flowsharp?cmd=CmdDropShape&ShapeName=Box&X=50&Y=100
        // Ex: localhost:8001/flowsharp?cmd=CmdDropShape&ShapeName=Box&X=50&Y=100&Text=Foobar&FillColor=!FF00ff&Width=300
        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdDropShape cmd)
        {
            var shapes = proc.ServiceManager.Get<IFlowSharpToolboxService>().ShapeList;
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var t = shapes.SingleOrDefault(s => s.Name == cmd.ShapeName);

            if (t == null) return;
            controller.Canvas.FindForm().BeginInvoke(() =>
            {
                var el = (GraphicElement)Activator.CreateInstance(t, new object[] { controller.Canvas });
                el.DisplayRectangle = new Rectangle(cmd.X, cmd.Y, cmd.Width ?? el.DefaultRectangle().Width, cmd.Height ?? el.DefaultRectangle().Height);
                el.Name = cmd.Name;
                el.Text = cmd.Text;

                cmd.FillColor.IfNotNull(c => el.FillColor = GetColor(c));
                cmd.BorderColor.IfNotNull(c => el.BorderPenColor = GetColor(c));
                cmd.TextColor.IfNotNull(c => el.TextColor = GetColor(c));

                el.UpdateProperties();
                el.UpdatePath();
                controller.Insert(el);
            });
        }

        // Ex: localhost:8001/flowsharp?cmd=CmdDropConnector&ConnectorName=DiagonalConnector&X1=50&Y1=100&X2=150&Y2=150
        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdDropConnector cmd)
        {
            var shapes = proc.ServiceManager.Get<IFlowSharpToolboxService>().ShapeList;
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            var t = shapes.SingleOrDefault(s => s.Name == cmd.ConnectorName);

            if (t == null) return;
            controller.Canvas.FindForm().BeginInvoke(() =>
            {
                var el = (DynamicConnector)Activator.CreateInstance(t, new object[] { controller.Canvas });
                // el = (DynamicConnector)el.CloneDefault(controller.Canvas, new Point(cmd.X1, cmd.Y1));
                // el = (DynamicConnector)el.CloneDefault(controller.Canvas);

                el.Name = cmd.Name;
                el.StartPoint = new Point(cmd.X1, cmd.Y1);
                el.EndPoint = new Point(cmd.X2, cmd.Y2);
                cmd.BorderColor.IfNotNull(c => el.BorderPenColor = GetColor(c));
                var x1 = cmd.X1.Min(cmd.X2);
                var y1 = cmd.Y1.Min(cmd.Y2);
                var x2 = cmd.X1.Max(cmd.X2);
                var y2 = cmd.Y1.Max(cmd.Y2);
                el.DisplayRectangle = new Rectangle(x1, y1, x2 - x1, y2 - y1);

                el.UpdatePath();
                controller.Insert(el);
            });
        }

        public void Process(ISemanticProcessor proc, IMembrane membrane, CmdClearCanvas cmd)
        {
            var controller = proc.ServiceManager.Get<IFlowSharpCanvasService>().ActiveController;
            controller.Canvas.FindForm().BeginInvoke(() =>
            {
                controller.Clear();
                controller.Canvas.Invalidate();
            });
        }

        protected Color GetColor(string colorString)
        {
            Color color;

            // Get the color from its name or an RGB value as hex codes #RRGGBB
            if (colorString[0] == '!')
            {
                color = ColorTranslator.FromHtml("#" + colorString.Substring(1));
            }
            else
            {
                color = Color.FromName(colorString);
            }

            return color;
        }
    }
}