﻿using Microsoft.AspNet.SignalR.Client;
using Microsoft.AspNet.SignalR.Client.Hubs;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Security;

namespace ProductsAPI
{
    public class UserDTO
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class LoginRegisterDTO
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    public class ProductListDTO : INotifyPropertyChanged
    {
        int mId;
        public int Id
        {
            get { return mId; }
            set
            {
                if (mId != value)
                {
                    mId = value;
                    _NotifyPropertyChanged("Id");
                }
            }
        }
        public string Name { get; set; }
        internal List<ProductEntryDTO> mProductEntries;
        public event PropertyChangedEventHandler PropertyChanged;
        public ProductListDTO()
        {
            mProductEntries = new List<ProductEntryDTO>();
        }
        public IEnumerable<ProductEntryDTO> ProductEntries
        {
            get { lock (mProductEntries) { return mProductEntries.ToArray(); } }
        }

        internal void _NotifyPropertyChanged(string aPropertyName)
        {
            var lHandler = PropertyChanged;
            if (lHandler != null)
            {
                lHandler(this, new PropertyChangedEventArgs(aPropertyName));
            }
        }
    }

    public class ProductEntryDTO : INotifyPropertyChanged
    {
        int mId;
        public int Id
        {
            get { return mId; }
            set
            {
                if (mId != value)
                {
                    mId = value;
                    _NotifyPropertyChanged("Id");
                }
            }
        }

        string mProductName;
        public string ProductName
        {
            get { return mProductName; }
            set
            {
                if (mProductName != value)
                {
                    mProductName = value;
                    _NotifyPropertyChanged("ProductName");
                }
            }
        }
        int mAmount;
        public int Amount
        {
            get { return mAmount; }
            set
            {
                if (mAmount != value)
                {
                    mAmount = value;
                    _NotifyPropertyChanged("Amount");
                }
            }
        }

        string mComments;
        public string Comments
        {
            get { return mComments; }
            set
            {
                if (mComments != value)
                {
                    mComments = value;
                    _NotifyPropertyChanged("Comments");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void _NotifyPropertyChanged(string aPropertyName)
        {
            var lHandler = PropertyChanged;
            if (lHandler != null)
            {
                lHandler(this, new PropertyChangedEventArgs(aPropertyName));
            }
        }

        internal ProductListDTO OwnerList
        {
            get;
            set;
        }
    }

    public partial class ProductsAPIClient : INotifyPropertyChanged
    {
        private IHubProxy mHubProxy;
        private HubConnection mHubConnection;

        HttpJSONRequester mJSONRequester;
        private Dictionary<string, string> mRequestHeaders = new Dictionary<string, string>();

        public ProductsAPIClient()
        {
            mJSONRequester = new HttpJSONRequester();
            mProductLists = new List<ProductListDTO>();
            mRequestHeaders.Add(APIConstants.API_VERSION_HEADER, APIConstants.API_VERSION_VALUE);
        }

        string mServerURL = APIConstants.DEFAULT_URL_SERVER;
        public string ServerURL
        {
            get { return mServerURL; }
            set
            {
                if (value != mServerURL)
                {
                    mServerURL = value;
                    _NotifyPropertyChanged("ServerURL");
                }
            }
        }

        public UserDTO LoggedInUser
        {
            get;
            private set;
        }

        List<ProductListDTO> mProductLists;
        public IEnumerable<ProductListDTO> ProductLists
        {
            get { lock (mProductLists) { return mProductLists.ToArray(); } }
        }

        int mIsBusy;
        public bool IsBusy
        {
            get { return mIsBusy > 0; }
            set
            {
                var lBefore = this.IsBusy;
                if (value)
                    mIsBusy++;
                else
                    mIsBusy = Math.Max(mIsBusy - 1, 0);
                
                if (this.IsBusy != lBefore)
                    _NotifyPropertyChanged("IsBusy");
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;

        public async Task RegisterUser(LoginRegisterDTO aUser)
        {
            await _LoginOrRegister(aUser, APIConstants.URL_REGISTER_USER);
        }

        public async Task Login(LoginRegisterDTO aUser)
        {
            await _LoginOrRegister(aUser, APIConstants.URL_LOGIN_USER);
        }

        public async Task Logout()
        {
            if (this.LoggedInUser != null)
            {
                using (this.SetBusy())
                {
                    LoggedInUser = null;
                    if (mHubConnection != null)
                    {
                        mHubConnection.StateChanged -= _OnSignalRConnectionStateChanged;
                        mHubConnection.Stop();
                    }
                    try
                    {
                        var lResponse = await mJSONRequester.Post(this.ServerURL, APIConstants.URL_LOGOUT_USER, mRequestHeaders);
                        mRequestHeaders.Remove(FormsAuthentication.FormsCookieName);
                    }
                    catch
                    {
                        //We're disconnecting, so we can swallow exceptions that might come
                    }
                    lock(mProductLists)
                        mProductLists.Clear();
                    _NotifyPropertyChanged("LoggedInUser");
                    _NotifyPropertyChanged("ProductLists");
                }
            }
        }

        public async Task QueryProductLists()
        {
            using (this.SetBusy())
            {
                var lResponse = await mJSONRequester.Get(this.ServerURL, APIConstants.URL_PRODUCT_LISTS, mRequestHeaders);
                lResponse.EnsureSuccessStatusCode();
                List<ProductListDTO> lProductLists = await lResponse.Content.ReadAsAsync<List<ProductListDTO>>();
                lock (mProductLists)
                {
                    mProductLists.Clear();
                    foreach (var lProductList in lProductLists)
                        mProductLists.Add(lProductList);
                }
                foreach (var lList in mProductLists)
                {
                    await QueryProductEntries(lList);
                }
                _NotifyPropertyChanged("ProductLists");
            }
        }

        public async Task CreateProductList(ProductListDTO aProductList)
        {
            using (this.SetBusy())
            {
                var lResponse = await mJSONRequester.Post<ProductListDTO>(this.ServerURL, APIConstants.URL_CREATE_PRODUCT_LIST, aProductList, mRequestHeaders);
                lResponse.EnsureSuccessStatusCode();
            }
        }

        public async Task DeleteProductList(ProductListDTO aProductList)
        {
            using (this.SetBusy())
            {
                var lResponse = await mJSONRequester.Delete(this.ServerURL, string.Format(APIConstants.URL_DELETE_PRODUCT_LIST, aProductList.Id), mRequestHeaders);
                lResponse.EnsureSuccessStatusCode();
            }
        }

        public async Task QueryProductEntries(ProductListDTO aList)
        {
            using (this.SetBusy())
            {
                var lResponse = await mJSONRequester.Get(this.ServerURL, string.Format(APIConstants.URL_PRODUCT_ENTRIES, aList.Id), mRequestHeaders);
                lResponse.EnsureSuccessStatusCode();
                List<ProductEntryDTO> lProductEntries = await lResponse.Content.ReadAsAsync<List<ProductEntryDTO>>();
                //Unsuscribe from previous event listeners
                foreach (var lProductEntry in aList.ProductEntries)
                {
                    lProductEntry.PropertyChanged -= _OnProductEntryPropertyChanged;
                    lProductEntry.OwnerList = null;
                }
                //Suscribe to new product entries
                foreach (var lProductEntry in lProductEntries)
                {
                    lProductEntry.PropertyChanged += _OnProductEntryPropertyChanged;
                    lProductEntry.OwnerList = aList;
                }

                lock (aList.mProductEntries)
                {
                    aList.mProductEntries.Clear();
                    foreach (var lProductEntry in lProductEntries)
                        aList.mProductEntries.Add(lProductEntry);
                }
                aList._NotifyPropertyChanged("ProductEntries");
            }
        }

        public async Task CreateProductEntry(ProductListDTO aList, ProductEntryDTO aProductEntry)
        {
            using (this.SetBusy())
            {
                var lResponse = await mJSONRequester.Post<ProductEntryDTO>(this.ServerURL, string.Format(APIConstants.URL_CREATE_PRODUCT_ENTRY, aList.Id), aProductEntry, mRequestHeaders);
                lResponse.EnsureSuccessStatusCode();
            }
        }

        public async Task DeleteProductEntry(ProductListDTO aList, ProductEntryDTO aProductEntry)
        {
            using (this.SetBusy())
            {
                var lResponse = await mJSONRequester.Delete(this.ServerURL, string.Format(APIConstants.URL_DELETE_PRODUCT_ENTRY, aList.Id, aProductEntry.Id), mRequestHeaders);
                lResponse.EnsureSuccessStatusCode();
            }
        }

        private async void _OnProductEntryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var lProductEntry = (ProductEntryDTO)sender;
            switch (e.PropertyName)
            {
            case "Amount":
            case "Comments":
                await mJSONRequester.Put<ProductEntryDTO, ProductEntryDTO>(this.ServerURL, string.Format(APIConstants.URL_EDIT_PRODUCT_ENTRY, lProductEntry.OwnerList.Id, lProductEntry.Id), lProductEntry, mRequestHeaders);
                break;
            }
        }

        private void _NotifyPropertyChanged(string aPropertyName)
        {
            var lHandler = PropertyChanged;
            if (lHandler != null)
            {
                lHandler(this, new PropertyChangedEventArgs(aPropertyName));
            }
        }

        private async Task _LoginOrRegister(LoginRegisterDTO aUser, string aURL)
        {
            using (this.SetBusy())
            {
                var lResponse = await mJSONRequester.Post<LoginRegisterDTO>(this.ServerURL, aURL, aUser, mRequestHeaders);
                lResponse.EnsureSuccessStatusCode();

                var lUser = await lResponse.Content.ReadAsAsync<UserDTO>();
                var lCookies = mJSONRequester.Cookies.GetCookies(new Uri(this.ServerURL));
                var lAuthCookie = lCookies.Cast<Cookie>().FirstOrDefault(aCookie => aCookie != null && aCookie.Name == FormsAuthentication.FormsCookieName);
                if (lAuthCookie != null)
                {
                    this.mHubConnection = new HubConnection(this.ServerURL + APIConstants.URL_SIGNALR_HUB);
                    this.mHubConnection.CookieContainer = new CookieContainer();
                    this.mHubConnection.CookieContainer.Add(lResponse.RequestMessage.RequestUri, lAuthCookie);
                    
                    mHubProxy = mHubConnection.CreateHubProxy("ProductsHub");
                    mHubProxy.On<string, object>("OnServerEvent", _OnSignalREvent);
                    await mHubConnection.Start();
                    mRequestHeaders[lAuthCookie.Name] = lAuthCookie.Value;
                    LoggedInUser = lUser;
                    mHubConnection.StateChanged += _OnSignalRConnectionStateChanged;
                    _NotifyPropertyChanged("LoggedInUser");
                }
            }
        }

        private void _OnSignalREvent(string aEventName, object aEventData)
        {
            switch (aEventName)
            {
            case "ProductListCreated":
                {
                    var lEventData = (JObject)aEventData;
                    var lList = new ProductListDTO()
                    {
                        Id = (int)lEventData["Id"],
                        Name = (string)lEventData["Name"]
                    };
                    lock (this.ProductLists)
                        mProductLists.Add(lList);

                    _NotifyPropertyChanged("ProductLists");
                }
                break;
            case "ProductListDeleted":
                {
                    var lEventData = (JObject)aEventData;
                    var lList = mProductLists.FirstOrDefault(aList => aList.Id == (int)lEventData["Id"]);
                    if (lList != null)
                    {
                        lock (this.ProductLists)
                            mProductLists.Remove(lList);

                        _NotifyPropertyChanged("ProductLists");
                    }
                }
                break;
            case "ProductEntryCreated":
                {
                    var lEventData = (JObject)aEventData;
                    var lList = mProductLists.FirstOrDefault(aList => aList.Id == (int)lEventData["ListId"]);
                    if (lList != null)
                    {
                        var lEntry = new ProductEntryDTO()
                        {
                            Id = (int)lEventData["Id"],
                            ProductName = (string)lEventData["Name"],
                            Amount = (int)lEventData["Amount"],
                            Comments = (string)lEventData["Comments"],
                            OwnerList = lList
                        };
                        lEntry.PropertyChanged += _OnProductEntryPropertyChanged;
                        lock (lList.mProductEntries)
                            lList.mProductEntries.Add(lEntry);

                        lList._NotifyPropertyChanged("ProductEntries");
                    }
                }
                break;
            case "ProductEntryEdited":
                {
                    var lEventData = (JObject)aEventData;
                    var lList = mProductLists.FirstOrDefault(aList => aList.Id == (int)lEventData["ListId"]);
                    if (lList != null)
                    {
                        var lEntry = lList.ProductEntries.FirstOrDefault(aEntry => aEntry.Id == (int)lEventData["Id"]);
                        if (lEntry != null)
                        {
                            lEntry.Amount = (int)lEventData["Amount"];
                            lEntry.Comments = (string)lEventData["Comments"];
                        }
                    }
                }
                break;
            case "ProductEntryDeleted":
                {
                    var lEventData = (JObject)aEventData;
                    var lList = mProductLists.FirstOrDefault(aList => aList.Id == (int)lEventData["ListId"]);
                    if (lList != null)
                    {
                        var lEntry = lList.ProductEntries.FirstOrDefault(aEntry => aEntry.Id == (int)lEventData["Id"]);
                        if (lEntry != null)
                        {
                            lock (lList.mProductEntries)
                                lList.mProductEntries.Remove(lEntry);

                            lList._NotifyPropertyChanged("ProductEntries");
                        }
                    }
                }
                break;
            }
        }

        private async void _OnSignalRConnectionStateChanged(StateChange aStateChange)
        {
            if (aStateChange.NewState != ConnectionState.Connected)
            {
                await this.Logout();
            }
        }
        private struct TIsBusyToken : IDisposable
        {
            ProductsAPIClient mOwner;
            public TIsBusyToken(ProductsAPIClient aOwner)
            {
                mOwner = aOwner;
                aOwner.IsBusy = true;
            }
            public void Dispose()
            {
                mOwner.IsBusy = false;
            }
        }
        private TIsBusyToken SetBusy()
        {
            return new TIsBusyToken(this);
        }
    }
}
