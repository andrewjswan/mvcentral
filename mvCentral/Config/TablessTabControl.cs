﻿using System;
using System.Windows.Forms;

namespace mvCentral
{
    public partial class TablessTabControl : TabControl
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x1328 && !DesignMode)
            {
                m.Result = (IntPtr)1;
            }
            else
            {
                base.WndProc(ref m);
            }
        }

        public TablessTabControl()
        {
            InitializeComponent();
        }
    }
}
