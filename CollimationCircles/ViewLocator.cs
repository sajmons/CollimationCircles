using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CollimationCircles.ViewModels;
using HanumanInstitute.MvvmDialogs.Avalonia;
using System;

namespace CollimationCircles
{
    public class ViewLocator : ViewLocatorBase, IDataTemplate
    {        
        protected override string GetViewName(object viewModel) => viewModel.GetType().FullName!.Replace("ViewModel", "View");

        //public IControl Build(object data)
        //{
        //    var name = data.GetType().FullName!.Replace("ViewModel", "View");
        //    var type = Type.GetType(name);

        //    if (type != null)
        //    {
        //        return (Control)Activator.CreateInstance(type)!;
        //    }

        //    return new TextBlock { Text = "Not Found: " + name };
        //}

        //public bool Match(object data)
        //{

        //    return data is BaseViewModel;
        //}
    }
}