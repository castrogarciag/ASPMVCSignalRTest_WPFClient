﻿using ProductsAPI;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ASPMVCProducts_WPFClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly DependencyProperty APIClientProperty = DependencyProperty.Register("APIClient", typeof(ProductsAPIClient), typeof(MainWindow), new PropertyMetadata(null));

        private ProductsAPIClient APIClient
        {
            get { return (ProductsAPIClient)GetValue(APIClientProperty); }
            set { SetValue(APIClientProperty, value); }
        }

        bool mLoggingOut; //Flag to know if user logout is accidental or voluntary
        Point mProductListMouseDown; //Position to track where mouse down ocurred when clicking delete button of list items
        public MainWindow()
        {
            InitializeComponent();
            this.APIClient = new ProductsAPIClient();
            this.APIClient.PropertyChanged += _OnAPIClientPropertyChanged;
        }
        private async Task _QueryProductLists()
        {
            try
            {
                await this.APIClient.QueryProductLists();
            }
            catch
            {
                MessageBox.Show(this, "Error retrieving product lists from server", "Network error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task _QueryProductEntries()
        {
            if (!(mProductListsItemsControl.SelectedItem is ProductListDTO))
                return;

            var lSelectedList = (ProductListDTO)mProductListsItemsControl.SelectedItem;
            try
            {
                await APIClient.QueryProductEntries(lSelectedList);
            }
            catch
            {
                MessageBox.Show(this, String.Format("Error retrieving from server product entries of list {0}", lSelectedList.Name), "Network error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void mLoginBtn_Click(object sender, RoutedEventArgs e)
        {
            await _Login();
            await _QueryProductLists();
        }

        private async void mRegisterBtn_Click(object sender, RoutedEventArgs e)
        {
            await _Register();
            await _QueryProductLists();
        }

        private async void mLogoutBtn_Click(object sender, RoutedEventArgs e)
        {
            await _Logout();
        }

        private async void mAddProductListBtn_Click(object sender, RoutedEventArgs e)
        {
            await _AddProductList();
        }

        private async void mDeleteProductListBtn_Click(object sender, RoutedEventArgs e)
        {
            var lSender = sender as FrameworkElement;
            if (lSender != null && lSender.DataContext is ProductListDTO)
                await _DeleteProductList((ProductListDTO)lSender.DataContext);
        }

        private async void mAddProductEntryBtn_Click(object sender, RoutedEventArgs e)
        {
            await _AddProductEntry();
        }

        private async void mDeleteProductEntry_Click(object sender, RoutedEventArgs e)
        {
            var lSender = sender as FrameworkElement;
            if (lSender == null || !(lSender.DataContext is ProductEntryDTO))
                return;
            if (!(mProductListsItemsControl.SelectedItem is ProductListDTO))
                return;

            await _DeleteProductEntry((ProductListDTO)mProductListsItemsControl.SelectedItem, (ProductEntryDTO)lSender.DataContext);
        }

        private async void mProductEntryNameTxtBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await _AddProductEntry();
            }
        }

        private async void mProductListNameTxtBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await _AddProductList();

            }
        }

        private void ProductListDelete_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var lSender = sender as IInputElement;
            if (lSender != null)
                mProductListMouseDown = e.GetPosition(lSender);
        }

        private async void ProductListDelete_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var lDownPos = mProductListMouseDown;
            mProductListMouseDown = new Point();
            var lSender = sender as FrameworkElement;
            if (lSender == null || !(lSender.DataContext is ProductListDTO))
                return;

            var lUpPos = e.GetPosition(lSender);
            if (Math.Abs(lUpPos.X - lDownPos.X) < 3 && Math.Abs(lUpPos.Y - lDownPos.Y) < 3)
            {
                var lProductList = (ProductListDTO)lSender.DataContext;
                await _DeleteProductList(lProductList);
            }
        }

        private void ProductEntryDelete_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var lSender = sender as IInputElement;
            if (lSender != null)
                mProductListMouseDown = e.GetPosition(lSender);
        }

        private async void ProductEntryDelete_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var lDownPos = mProductListMouseDown;
            mProductListMouseDown = new Point();
            var lSender = sender as FrameworkElement;
            if (lSender == null || !(lSender.DataContext is ProductEntryDTO))
                return;

            if (!(mProductListsItemsControl.SelectedItem is ProductListDTO))
                return;

            var lUpPos = e.GetPosition(lSender);
            if (Math.Abs(lUpPos.X - lDownPos.X) < 3 && Math.Abs(lUpPos.Y - lDownPos.Y) < 3)
            {
                await _DeleteProductEntry((ProductListDTO)mProductListsItemsControl.SelectedItem, (ProductEntryDTO)lSender.DataContext);
            }
        }

        private async Task _Login()
        {
            try
            {
                if (String.IsNullOrEmpty(mUserNameTxtBox.Text) || string.IsNullOrEmpty(mPwdBox.Password))
                    return;
                await APIClient.Login(new RegisterUserDTO() { UserName = mUserNameTxtBox.Text, Password = mPwdBox.Password });
            }
            catch (Exception ex)
            {
                if (ex is HttpRequestException && ex.Message.Contains(((int)HttpStatusCode.Unauthorized).ToString()))
                    MessageBox.Show(this, "Invalid username and/or password", "Unauthorized", MessageBoxButton.OK, MessageBoxImage.Stop);
                else
                    MessageBox.Show(this, "Error communicating with server", "Network error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task _Logout()
        {
            mLoggingOut = true;
            await APIClient.Logout();
            mLoggingOut = false;
        }

        private async Task _Register()
        {
            if (String.IsNullOrEmpty(mUserNameTxtBox.Text) || string.IsNullOrEmpty(mPwdBox.Password))
                return;
            try
            {
                await APIClient.RegisterUser(new RegisterUserDTO() { UserName = mUserNameTxtBox.Text, Password = mPwdBox.Password });
            }
            catch (Exception ex)
            {
                if (ex is HttpRequestException && ex.Message.Contains(((int)HttpStatusCode.NotModified).ToString()))
                    MessageBox.Show(this, "Invalid username. There's already an user with the given username", "Unauthorized", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                else
                    MessageBox.Show(this, "Error communicating with server", "Network error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task _AddProductList()
        {
            if (String.IsNullOrEmpty(mProductListNameTxtBox.Text))
                return;
            try
            {
                await APIClient.CreateProductList(new ProductListDTO(mProductListNameTxtBox.Text));
            }
            catch (Exception ex)
            {

                if (ex is HttpRequestException && ex.Message.Contains(((int)HttpStatusCode.Conflict).ToString()))
                    MessageBox.Show(this, "Invalid name. There's already a product list with the given name", "Invalid name", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                else
                    MessageBox.Show(this, "Error communicating with server", "Network error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task _AddProductEntry()
        {
            if (!(mProductListsItemsControl.SelectedItem is ProductListDTO))
                return;

            if (String.IsNullOrEmpty(mProductEntryNameTxtBox.Text))
                return;

            var lSelectedList = (ProductListDTO)mProductListsItemsControl.SelectedItem;
            try
            {
                await APIClient.CreateProductEntry(lSelectedList, new ProductEntryDTO() { ProductName = mProductEntryNameTxtBox.Text });
            }
            catch (Exception ex)
            {
                if (ex is HttpRequestException && ex.Message.Contains(((int)HttpStatusCode.Conflict).ToString()))
                    MessageBox.Show(this, "Invalid name. The given product is already in the list", "Duplicated product", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                else
                    MessageBox.Show(this, "Error communicating with server", "Network error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task _DeleteProductList(ProductListDTO aProductList)
        {
            try
            {
                await APIClient.DeleteProductList(aProductList);
            }
            catch (Exception ex)
            {
                //In very high concurrent scenarios race conditions may ocurr, that lead the application to delete an already (just) deleted product list
                if (ex is HttpRequestException && ex.Message.Contains(((int)HttpStatusCode.NotFound).ToString()))
                {
                    //The given list was not found (probably already deleted in a race condition) Nothing is done
                }
                else
                    MessageBox.Show(this, "Error communicating with server", "Network error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task _DeleteProductEntry(ProductListDTO aProductList, ProductEntryDTO aProductEntry)
        {
            try
            {
                await APIClient.DeleteProductEntry(aProductList, aProductEntry);
            }
            catch (Exception ex)
            {
                //In very high concurrent scenarios race conditions may ocurr, that lead the application to delete an already (just) deleted product entry
                if (ex is HttpRequestException && ex.Message.Contains(((int)HttpStatusCode.NotFound).ToString()))
                {
                    //The given product entry was not found (probably already deleted in a race condition) Nothing is done
                }
                else
                    MessageBox.Show(this, "Error communicating with server", "Network error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void _OnAPIClientPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.CheckAccess())
            {
                //Connection is lost
                if (e.PropertyName == "LoggedInUser" && this.APIClient.LoggedInUser == null && !mLoggingOut)
                {
                    MessageBox.Show(this, "Connection with API server was lost. Try reconnecting", "Connection lost", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                this.Dispatcher.BeginInvoke(new Action<object, PropertyChangedEventArgs>(
                    (aSender, aArgs) => { _OnAPIClientPropertyChanged(aSender, aArgs); }), sender, e);
            }
        }


    }
}

