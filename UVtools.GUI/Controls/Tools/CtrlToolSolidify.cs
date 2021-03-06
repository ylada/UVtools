﻿/*
 *                     GNU AFFERO GENERAL PUBLIC LICENSE
 *                       Version 3, 19 November 2007
 *  Copyright (C) 2007 Free Software Foundation, Inc. <https://fsf.org/>
 *  Everyone is permitted to copy and distribute verbatim copies
 *  of this license document, but changing it is not allowed.
 */
using System.Windows.Forms;
using UVtools.Core.Operations;

namespace UVtools.GUI.Controls.Tools
{
    public partial class CtrlToolSolidify : CtrlToolWindowContent
    {
        private OperationSolidify Operation { get; }
        public CtrlToolSolidify()
        {
            InitializeComponent();
            Operation = new OperationSolidify();
            SetOperation(Operation);
        }
    }
}
