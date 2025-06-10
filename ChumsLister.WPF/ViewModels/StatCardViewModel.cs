using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ChumsLister.WPF.ViewModels
{
    public class StatCardViewModel : INotifyPropertyChanged
    {
        private string _title;
        private string _value;

        public StatCardViewModel(string title, string value)
        {
            _title = title;
            _value = value;
        }

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged();
            }
        }

        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}