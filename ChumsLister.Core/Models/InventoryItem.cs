using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ChumsLister.Core.Models
{
    public class InventoryItem : INotifyPropertyChanged
    {

        private string _sku;
        private string _transid;
        private string _modelHdSku;
        private string _description;
        private int _qty;
        private decimal _retailPrice;
        private decimal _costItem;
        private decimal _totalCostItem;
        private int _qtySold;
        private decimal _soldPrice;
        private string _status;
        private string _repo;
        private string _location;
        private string _dateSold;





        public string SKU
        {
            get => _sku;
            set
            {
                _sku = value;
                OnPropertyChanged();
            }
        }

        public string TRANS_ID
        {
            get => _transid;
            set
            {
                _transid = value;
                OnPropertyChanged();
            }
        }

        public string MODEL_HD_SKU
        {
            get => _modelHdSku;
            set
            {
                _modelHdSku = value;
                OnPropertyChanged();
            }
        }

        public string DESCRIPTION
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public int QTY
        {
            get => _qty;
            set
            {
                _qty = value;
                OnPropertyChanged();
            }
        }


        public decimal RETAIL_PRICE
        {
            get => _retailPrice;
            set { _retailPrice = value; OnPropertyChanged(); }
        }


        public decimal COST_ITEM
        {
            get => _costItem;
            set
            {
                _costItem = value;
                OnPropertyChanged();
            }
        }

        public decimal TOTAL_COST_ITEM
        {
            get => _totalCostItem;
            set
            {
                _totalCostItem = value;
                OnPropertyChanged();
            }
        }

        public int QTY_SOLD
        {
            get => _qtySold;
            set
            {
                _qtySold = value;
                OnPropertyChanged();
            }
        }

        public decimal SOLD_PRICE
        {
            get => _soldPrice;
            set
            {
                _soldPrice = value;
                OnPropertyChanged();
            }
        }

        public string STATUS
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public string REPO
        {
            get => _repo;
            set
            {
                _repo = value;
                OnPropertyChanged();
            }
        }

        public string LOCATION
        {
            get => _location;
            set
            {
                _location = value;
                OnPropertyChanged();
            }
        }

        public string DATE_SOLD
        {
            get => _dateSold;
            set
            {
                _dateSold = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void AddSoldPrice(decimal price)
        {
            SOLD_PRICE = price;
                
        }

        public void AddSoldDate(DateTime date)
        {
            DATE_SOLD = string.IsNullOrWhiteSpace(DATE_SOLD)
                ? date.ToString("MM/dd/yyyy")
                : $"{DATE_SOLD},{date:MM/dd/yyyy}";
        }

        

        public List<DateTime> GetSoldDates()
        {
            if (string.IsNullOrWhiteSpace(DATE_SOLD)) return new List<DateTime>();
            return DATE_SOLD.Split(',')
                .Select(d => DateTime.TryParse(d, out var date) ? date : DateTime.MinValue)
                .Where(d => d != DateTime.MinValue)
                .ToList();
        }


    }
}