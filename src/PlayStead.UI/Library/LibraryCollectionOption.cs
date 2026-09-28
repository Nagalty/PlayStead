using System.ComponentModel;
using System.Runtime.CompilerServices;
using PlayStead.Core.Collections;
using PlayStead.Core.Library;

namespace PlayStead.UI.Library;

public sealed class LibraryCollectionOption : INotifyPropertyChanged
{
    private bool _isFilterSelected;
    private bool _isMember;

    public LibraryCollectionOption(GameCollection collection)
    {
        Collection = collection;
    }

    public GameCollection Collection { get; private set; }
    public Guid Id => Collection.Id;
    public string Name => Collection.Name;
    public bool IsFilterSelected { get => _isFilterSelected; set => Set(ref _isFilterSelected, value); }
    public bool IsMember { get => _isMember; set => Set(ref _isMember, value); }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? FilterChanged;
    public event EventHandler? MembershipChanged;

    public void Update(GameCollection collection, bool filterSelected, bool member)
    {
        Collection = collection;
        OnPropertyChanged(nameof(Collection));
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Name));
        _isFilterSelected = filterSelected;
        _isMember = member;
        OnPropertyChanged(nameof(IsFilterSelected));
        OnPropertyChanged(nameof(IsMember));
    }

    private void Set(ref bool field, bool value, [CallerMemberName] string? name = null)
    {
        if (field == value) return;
        field = value;
        OnPropertyChanged(name);
        if (name == nameof(IsFilterSelected)) FilterChanged?.Invoke(this, EventArgs.Empty);
        if (name == nameof(IsMember)) MembershipChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
