/*
* Copyright (c) Marc Clifton
* The Code Project Open License (CPOL) 1.02
* http://www.codeproject.com/info/cpol10.aspx
*/

using System.Collections.Generic;
using System.Windows.Forms;

using Clifton.Core.ServiceManagement;

using FlowSharpLib;
using FlowSharpServiceInterfaces;

namespace FlowSharpMenuService
{
    public partial class NavigateDlg : Form
    {
        protected IServiceManager serviceManager;
        public NavigateDlg(IServiceManager serviceManager, List<NavigateToShape> navNames)
        {
            this.serviceManager = serviceManager;
            InitializeComponent();
            lbShapes.Items.AddRange(navNames.ToArray());
        }

        private void LbShapes_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                case (char)Keys.Escape:
                    e.Handled = true;
                    Close();
                    break;

                case (char)Keys.Enter:
                    e.Handled = true;
                    FocusSelectedShape();
                    break;
            }
        }

        private void LbShapes_MouseClick(object sender, MouseEventArgs e)
        {
            FocusSelectedShape();
        }

        private void FocusSelectedShape()
        {
            if (!(lbShapes.SelectedItem is NavigateToShape selectedShape))
            {
                return;
            }

            Close();
            serviceManager.Get<IFlowSharpEditService>().FocusOnShape(selectedShape.Shape);
        }
    }
}
