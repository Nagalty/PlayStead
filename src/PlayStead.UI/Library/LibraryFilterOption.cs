using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PlayStead.UI.Library;

public sealed class LibraryFilterOption : INotifyPropertyChanged
{
    private bool _isSelected;

    public LibraryFilterOption(string key, string label)
    {
        Key = key;
        Label = label;
    }

    public string Key { get; }
    public string Label { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Changed;
}
