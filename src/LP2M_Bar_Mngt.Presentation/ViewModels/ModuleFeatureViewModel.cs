using System.Windows.Input;

namespace LP2M_Bar_Mngt.Presentation.ViewModels;

public sealed class ModuleFeatureViewModel
{
    public ModuleFeatureViewModel(
        string name,
        string status,
        string details,
        string buttonText,
        ICommand command)
    {
        Name = name;
        Status = status;
        Details = details;
        ButtonText = buttonText;
        Command = command;
    }

    public string Name { get; }

    public string Status { get; }

    public string Details { get; }

    public string ButtonText { get; }

    public ICommand Command { get; }
}
