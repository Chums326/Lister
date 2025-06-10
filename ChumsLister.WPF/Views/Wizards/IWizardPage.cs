using ChumsLister.Core.Models;

namespace ChumsLister.WPF.Views.Wizards
{
    public interface IWizardPage
    {
        bool ValidatePage();
        void SaveData(ListingWizardData listingData);
        void LoadData(ListingWizardData listingData);
    }
}