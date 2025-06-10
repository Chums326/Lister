using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ChumsLister.WPF.Views.Wizards
{
    /// <summary>
    /// A single node in the category tree. Can have placeholder children for lazy loading.
    /// </summary>
    public class CategoryNode : INotifyPropertyChanged
    {
        private string _categoryId;
        private string _categoryName;
        private bool _isLeaf;
        private ObservableCollection<CategoryNode> _children;

        public string CategoryId
        {
            get => _categoryId;
            set
            {
                _categoryId = value;
                OnPropertyChanged();
            }
        }

        public string CategoryName
        {
            get => _categoryName;
            set
            {
                _categoryName = value;
                OnPropertyChanged();
            }
        }

        public bool IsLeaf
        {
            get => _isLeaf;
            set
            {
                _isLeaf = value;
                OnPropertyChanged();

                // When setting as leaf, clear any placeholder children
                if (value && _children != null && _children.Count == 1 && _children[0].CategoryName == "Loading...")
                {
                    _children.Clear();
                }
            }
        }

        // We bind TreeViewItem.ItemsSource to this.
        public ObservableCollection<CategoryNode> Children
        {
            get => _children;
            set
            {
                _children = value;
                OnPropertyChanged();
            }
        }

        public CategoryNode()
        {
            Children = new ObservableCollection<CategoryNode>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}