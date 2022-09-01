/* The MIT License (MIT)
*
* Copyright (c) 2015 Marc Clifton
*
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
*
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
*
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable CheckNamespace
// ReSharper disable InconsistentNaming

namespace Clifton.Core.Semantics
{
    public class Membrane : IMembrane
    {
        protected Membrane Parent { get; set; }
        protected List<Membrane> ChildMembranes { get; set; }
        protected List<Type> OutbndPermeableTo { get; set; }
        protected List<Type> InbndPermeableTo { get; set; }

        protected Membrane()
        {
            Parent = null;
            ChildMembranes = new List<Membrane>();
            OutbndPermeableTo = new List<Type>();
            InbndPermeableTo = new List<Type>();
        }

        public void AddChild(Membrane child)
        {
            ChildMembranes.Add(child);
            child.Parent = this;
        }

        public void OutboundPermeableTo<T>()
            where T : ISemanticType
        {
            OutbndPermeableTo.Add(typeof(T));
        }

        public void InboundPermeableTo<T>()
            where T : ISemanticType
        {
            InbndPermeableTo.Add(typeof(T));
        }

        /// <summary>
        /// Given this membrane's outbound list, what membranes are inbound permeabe to the ST as well?
        /// </summary>
        public List<IMembrane> PermeateTo(ISemanticType st)
        {
            var ret = new List<IMembrane>();
            var sttype = st.GetType();

            if (!OutbndPermeableTo.Contains(sttype)) return ret;
            // Can we traverse to the parent?
            if ((Parent != null) && (Parent.InbndPermeableTo.Contains(sttype)))
            {
                ret.Add(Parent);
            }

            // Can we traverse to children?
            ret.AddRange(ChildMembranes.Where(child => child.InbndPermeableTo.Contains(sttype)).Cast<IMembrane>());

            return ret;
        }
    }

    /// <summary>
    /// Type for our built-in membrane
    /// </summary>
    public class SurfaceMembrane : Membrane
    {
    }

    /// <summary>
    /// Type for our built-in membrane
    /// </summary>
    public class LoggerMembrane : Membrane
    {
    }
}
