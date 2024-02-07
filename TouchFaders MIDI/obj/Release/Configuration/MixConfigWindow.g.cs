﻿#pragma checksum "..\..\..\Configuration\MixConfigWindow.xaml" "{8829d00f-11b8-4213-878b-770e8597ac16}" "87DB90A09DDE5A6F058FBCACC480F5591BD88964C677AA37C7BE2C5D2003ED5D"
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using TouchFaders.Configuration;


namespace TouchFaders.Configuration {
    
    
    /// <summary>
    /// MixConfigWindow
    /// </summary>
    public partial class MixConfigWindow : System.Windows.Window, System.Windows.Markup.IComponentConnector {
        
        
        #line 1 "..\..\..\Configuration\MixConfigWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal TouchFaders.Configuration.MixConfigWindow mixConfigWindow;
        
        #line default
        #line hidden
        
        
        #line 9 "..\..\..\Configuration\MixConfigWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.Grid mixConfigWindowGrid;
        
        #line default
        #line hidden
        
        
        #line 11 "..\..\..\Configuration\MixConfigWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Media.ScaleTransform ApplicationScaleTransform;
        
        #line default
        #line hidden
        
        
        #line 18 "..\..\..\Configuration\MixConfigWindow.xaml"
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        internal System.Windows.Controls.DataGrid mixDataGrid;
        
        #line default
        #line hidden
        
        private bool _contentLoaded;
        
        /// <summary>
        /// InitializeComponent
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        public void InitializeComponent() {
            if (_contentLoaded) {
                return;
            }
            _contentLoaded = true;
            System.Uri resourceLocater = new System.Uri("/TouchFaders;component/configuration/mixconfigwindow.xaml", System.UriKind.Relative);
            
            #line 1 "..\..\..\Configuration\MixConfigWindow.xaml"
            System.Windows.Application.LoadComponent(this, resourceLocater);
            
            #line default
            #line hidden
        }
        
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Never)]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1800:DoNotCastUnnecessarily")]
        void System.Windows.Markup.IComponentConnector.Connect(int connectionId, object target) {
            switch (connectionId)
            {
            case 1:
            this.mixConfigWindow = ((TouchFaders.Configuration.MixConfigWindow)(target));
            
            #line 8 "..\..\..\Configuration\MixConfigWindow.xaml"
            this.mixConfigWindow.Loaded += new System.Windows.RoutedEventHandler(this.mixConfigWindow_Loaded);
            
            #line default
            #line hidden
            return;
            case 2:
            this.mixConfigWindowGrid = ((System.Windows.Controls.Grid)(target));
            
            #line 9 "..\..\..\Configuration\MixConfigWindow.xaml"
            this.mixConfigWindowGrid.SizeChanged += new System.Windows.SizeChangedEventHandler(this.mixConfigWindowGrid_SizeChanged);
            
            #line default
            #line hidden
            return;
            case 3:
            this.ApplicationScaleTransform = ((System.Windows.Media.ScaleTransform)(target));
            return;
            case 4:
            this.mixDataGrid = ((System.Windows.Controls.DataGrid)(target));
            
            #line 18 "..\..\..\Configuration\MixConfigWindow.xaml"
            this.mixDataGrid.MouseDown += new System.Windows.Input.MouseButtonEventHandler(this.mixDataGrid_MouseDown);
            
            #line default
            #line hidden
            
            #line 18 "..\..\..\Configuration\MixConfigWindow.xaml"
            this.mixDataGrid.LoadingRow += new System.EventHandler<System.Windows.Controls.DataGridRowEventArgs>(this.mixDataGrid_LoadingRow);
            
            #line default
            #line hidden
            return;
            }
            this._contentLoaded = true;
        }
    }
}

