using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Gw2Giveaway
{
    public class SlotViewModel : INotifyPropertyChanged
    {
        public PrizeSlot Slot { get; }
        public ICommand EditCommand { get; }          // Select GW2 item
        public ICommand CustomPrizeCommand { get; }   // Set custom prize
        public ICommand ClearCommand { get; }
        public ICommand SetDisplayStackCommand { get; }
        public ICommand SetGiveawayAmountCommand { get; }
        public string Rarity => Slot.Item?.Rarity ?? "";
        public string Description => Slot.Item?.Description ?? "";
        public bool IsGw2Item => Slot.Item != null;
        public ICommand SetPrizeRarityCommand { get; }
       // public bool ShowPrizeRarityBadges { get; set; } = true;
        // In constructor

        private readonly Action _save;
        private bool _isHighlighted;
        public bool IsHighlighted
        {
            get => _isHighlighted;
            set
            {
                if (_isHighlighted != value)
                {
                    _isHighlighted = value;
                    OnPropertyChanged();
                }
            }
        }
        public SlotViewModel(PrizeSlot slot, Action save)
        {
            Slot = slot;
            _save = save;

            EditCommand = new RelayCommand(EditGw2Item);
            CustomPrizeCommand = new RelayCommand(SetCustomPrize);
            ClearCommand = new RelayCommand(Clear);
            SetDisplayStackCommand = new RelayCommand(SetDisplayStack);
            SetGiveawayAmountCommand = new RelayCommand(SetGiveawayAmount);
            SetPrizeRarityCommand = new RelayCommand<PrizeRarity>(r => PrizeRarity = r);
        }

        private void EditGw2Item()
        {
            var dialog = new ItemSelectorDialog();
            if (dialog.ShowDialog() != true || dialog.SelectedItem == null) return;

            // Override any custom prize
            Slot.CustomName = null;
            Slot.CustomIconUrl = null;

            Slot.Item = dialog.SelectedItem;
            Slot.ItemId = dialog.SelectedId;

            if (Slot.DisplayStack == 0) Slot.DisplayStack = 1;
            if (Slot.GiveawayAmount <= 0) Slot.GiveawayAmount = 1; // reasonable default

            RaisePropertyChanges();
            _save();
        }

        private void SetCustomPrize()
        {
            var nameDlg = new InputDialog("Custom prize name (e.g. '500 Gems' or 'Steam DLC Key'):", Slot.CustomName ?? "");
            if (nameDlg.ShowDialog() != true || string.IsNullOrWhiteSpace(nameDlg.Result)) return;

            string name = nameDlg.Result.Trim();

            var urlDlg = new InputDialog("Custom icon URL (optional – leave blank for no icon):", Slot.CustomIconUrl ?? "");
            if (urlDlg.ShowDialog() != true) return;

            string url = string.IsNullOrWhiteSpace(urlDlg.Result) ? null : urlDlg.Result.Trim();

            // Override any GW2 item
            Slot.Item = null;
            Slot.ItemId = 0;

            Slot.CustomName = name;
            Slot.CustomIconUrl = url;

            if (Slot.DisplayStack == 0) Slot.DisplayStack = 1;
            if (Slot.GiveawayAmount <= 0) Slot.GiveawayAmount = 1;

            RaisePropertyChanges();
            _save();
        }
        public PrizeRarity PrizeRarity
        {
            get => Slot.PrizeRarity;
            set
            {
                Slot.PrizeRarity = value;
                OnPropertyChanged();
                _save();
            }
        }
        private void SetDisplayStack()
        {
            var dlg = new InputDialog("Display stack size (1-250):", Slot.DisplayStack.ToString());
            if (dlg.ShowDialog() == true && int.TryParse(dlg.Result, out int d) && d >= 1 && d <= 250)
            {
                Slot.DisplayStack = d;
                OnPropertyChanged(nameof(StackText));
                _save();
            }
        }

        private void SetGiveawayAmount()
        {
            var dlg = new InputDialog("Giveaway amount per win (0-250, 0 = disable):", Slot.GiveawayAmount.ToString());
            if (dlg.ShowDialog() == true && int.TryParse(dlg.Result, out int g) && g >= 0 && g <= 250)
            {
                Slot.GiveawayAmount = g;
                _save();
            }
        }

        private void Clear()
        {
            Slot.Item = null;
            Slot.ItemId = 0;
            Slot.CustomName = null;
            Slot.CustomIconUrl = null;
            Slot.DisplayStack = 0;
            Slot.GiveawayAmount = 1;

            RaisePropertyChanges();
            _save();
        }

        private void RaisePropertyChanges()
        {
            OnPropertyChanged(nameof(ImageSource));
            OnPropertyChanged(nameof(StackText));
            OnPropertyChanged(nameof(ItemName));
            OnPropertyChanged(nameof(HasPrize));
        }

        public string ImageSource => Slot.CustomIconUrl ?? Slot.Item?.Icon ?? "";
        public string StackText => Slot.DisplayStack > 1 ? Slot.DisplayStack.ToString() : "";
        public string ItemName => Slot.CustomName ?? Slot.Item?.Name ?? "Empty Slot";

        public bool HasPrize => Slot.Item != null || !string.IsNullOrEmpty(Slot.CustomName);

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class BankViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<SlotViewModel> Slots { get; } = new();
        public bool ShowPrizeRarityBadges { get; set; } = true;
        private long _goldAmount;
        public long GoldAmount
        {
            get => _goldAmount;
            set
            {
                if (_goldAmount != value)
                {
                    _goldAmount = value;
                    _bank.GoldAmount = value;  // ← Critical fix: sync back to the model
                    OnPropertyChanged();
                    _save();
                }
            }
        }

        private readonly PrizeBank _bank;
        private readonly Action _save;

        public BankViewModel(PrizeBank bank, Action save)
        {
            _bank = bank;
            _save = save;
            _goldAmount = bank.GoldAmount;

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 10; c++)
                {
                    Slots.Add(new SlotViewModel(bank.Slots[r, c], _save));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        public RelayCommand(Action<T?> execute) => _execute = execute;

        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute((T?)parameter);
    }

    
}