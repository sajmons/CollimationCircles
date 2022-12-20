using Avalonia.Controls.Templates;
using HanumanInstitute.MvvmDialogs.Avalonia;
using System;

namespace CollimationCircles
{
    public class ViewLocator : ViewLocatorBase, IDataTemplate
    {
        protected override string GetViewName(object viewModel)
        {
            string vm = viewModel.GetType().FullName!.Replace("ViewModel", "View");
            return vm;
        }
    }
}