using System.Windows;
using System.Windows.Input;
using GarmentERP.Desktop.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GarmentERP.Desktop;

public partial class MainWindow : Window
{
    private readonly PosMainViewModel _viewModel;

    public MainWindow() : this(((App)System.Windows.Application.Current)?.Services?.GetService<PosMainViewModel>()!)
    {
    }

    public MainWindow(PosMainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? ((App)System.Windows.Application.Current)?.Services?.GetRequiredService<PosMainViewModel>()!;
        DataContext = _viewModel;

        Loaded += async (s, e) =>
        {
            await _viewModel.InitializeAsync();
            TxtBarcode.Focus();
        };
    }

    private void TxtBarcode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _viewModel.ScanBarcodeCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void TxtProductSearchInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _viewModel.AddFirstFilteredVariantToCartCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void BtnSearchItem_Click(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            TxtProductSearchInput?.Focus();
            TxtProductSearchInput?.SelectAll();
        });
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F1:
                _viewModel.OpenProductSearchModalCommand.Execute(null);
                if (_viewModel.IsProductSearchModalOpen)
                {
                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
                    {
                        TxtProductSearchInput?.Focus();
                        TxtProductSearchInput?.SelectAll();
                    });
                }
                e.Handled = true;
                break;
            case Key.F2:
                _viewModel.NavigateToCommand.Execute("POS");
                TxtBarcode.Focus();
                e.Handled = true;
                break;
            case Key.F4:
                _viewModel.OpenDiscountModalCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F5:
                _viewModel.OpenCustomerModalCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F6:
                _viewModel.HoldCurrentBillCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F7:
                _viewModel.OpenHeldBillsModalCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F9:
                _viewModel.OpenSplitPaymentModalCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F10:
                _viewModel.QuickCashCheckoutCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F11:
                _viewModel.GenerateShiftZReportCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F12:
                _viewModel.PrintLastReceiptCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Escape:
                if (_viewModel.IsReceiptModalOpen) _viewModel.IsReceiptModalOpen = false;
                else if (_viewModel.IsPaymentModalOpen) _viewModel.IsPaymentModalOpen = false;
                else if (_viewModel.IsZReportModalOpen) _viewModel.IsZReportModalOpen = false;
                else if (_viewModel.IsProductSearchModalOpen) _viewModel.IsProductSearchModalOpen = false;
                else if (_viewModel.IsCustomerModalOpen) _viewModel.IsCustomerModalOpen = false;
                else if (_viewModel.IsDiscountModalOpen) _viewModel.IsDiscountModalOpen = false;
                else if (_viewModel.IsHeldBillsModalOpen) _viewModel.IsHeldBillsModalOpen = false;
                else _viewModel.ClearCartCommand.Execute(null);
                TxtBarcode.Focus();
                e.Handled = true;
                break;
        }
    }
}
