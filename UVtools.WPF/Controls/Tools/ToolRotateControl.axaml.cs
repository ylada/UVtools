﻿using Avalonia.Markup.Xaml;
using UVtools.Core.Operations;

namespace UVtools.WPF.Controls.Tools
{
    public class ToolRotateControl : ToolControl
    {
        public OperationRotate Operation { get; }

        public ToolRotateControl()
        {
            InitializeComponent();
            BaseOperation = Operation = new OperationRotate();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
