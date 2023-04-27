using HanumanInstitute.MvvmDialogs.Avalonia;

namespace CollimationCircles
{
    public class ViewLocator : ViewLocatorBase
    {
        protected override string GetViewName(object viewModel)
        {
            string vm = viewModel.GetType().FullName!.Replace("ViewModel", "View");
            return vm;
        }
    }
}