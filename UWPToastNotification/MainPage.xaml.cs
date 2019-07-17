
using Microsoft.Identity.Client;
using Microsoft.Toolkit.Uwp.Notifications;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UWPToastNotification
{

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    /// 

    public sealed partial class MainPage : Page
    {

        #region Properties

        DateTime receivedDateTime;
        string subject = string.Empty;
        string id = string.Empty;
        string sendermailid = string.Empty;

        bool check = false;

        public string email_id = string.Empty;

        public static string notificationinput = string.Empty;

        DispatcherTimer timer = new DispatcherTimer();
        TimeSpan span = new TimeSpan(0, 0, 10);

        #endregion


        #region Azure Ad auth and Graph API

        //Set the API Endpoint to Graph 'me' endpoint
        string _graphAPIEndpoint = "https://graph.microsoft.com/v1.0/me";//Set the scope for API call to user.read
        string[] _scopes = new string[] { "user.read", "mail.read" };

        string authtoken;

        public MainPage()
        {
            InitializeComponent();
        }

        /// &lt;summary&gt;
        /// Call AcquireTokenAsync - to acquire a token requiring user to sign-in
        /// &lt;/summary&gt;
        private async void CallGraphButton_Click(object sender, RoutedEventArgs e)
        {
            AuthenticationResult authResult = null;

            var app = App.PublicClientApp;
            ResultText.Text = string.Empty;
            TokenInfoText.Text = string.Empty;

            var accounts = await app.GetAccountsAsync();

            try
            {
                authResult = await app.AcquireTokenSilentAsync(_scopes, accounts.FirstOrDefault());
            }
            catch (MsalUiRequiredException ex)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilentAsync. This indicates you need to call AcquireTokenAsync to acquire a token
                System.Diagnostics.Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

                try
                {
                    authResult = await App.PublicClientApp.AcquireTokenAsync(_scopes);
                }
                catch (MsalException msalex)
                {
                    ResultText.Text = $"Error Acquiring Token:{System.Environment.NewLine}{msalex}";
                }
            }
            catch (Exception ex)
            {
                ResultText.Text = $"Error Acquiring Token Silently:{System.Environment.NewLine}{ex}";
                return;
            }

            if (authResult != null)
            {
                authtoken = authResult.AccessToken;
                ResultText.Text = await GetHttpContentWithToken(_graphAPIEndpoint, authResult.AccessToken);
                DisplayBasicTokenInfo(authResult);
                this.SignOutButton.Visibility = Visibility.Visible;
            }

            GetMails.IsEnabled = true;

        }


        /// <summary>
        /// Perform an HTTP GET request to a URL using an HTTP Authorization header
        /// </summary>
        /// <param name="url">The URL</param>
        /// <param name="token">The token</param>
        /// <returns>String containing the results of the GET operation</returns>
        public async Task<string> GetHttpContentWithToken(string url, string token)
        {
            var httpClient = new System.Net.Http.HttpClient();
            System.Net.Http.HttpResponseMessage response;
            try
            {
                var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                //Add the token in Authorization header
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                response = await httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            catch (Exception ex)
            {
                return ex.ToString();
            }
        }


        /// <summary>
        /// Sign out the current user
        /// </summary>
        private async void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            var accounts = await App.PublicClientApp.GetAccountsAsync();

            if (accounts.Any())
            {
                try
                {
                    await App.PublicClientApp.RemoveAsync(accounts.FirstOrDefault());
                    this.ResultText.Text = "User has signed-out";
                    this.CallGraphButton.Visibility = Visibility.Visible;
                    this.SignOutButton.Visibility = Visibility.Collapsed;
                }
                catch (MsalException ex)
                {
                    ResultText.Text = $"Error signing-out user: {ex.Message}";
                }
            }
        }


        /// <summary>
        /// Display basic information contained in the token
        /// </summary>
        private void DisplayBasicTokenInfo(AuthenticationResult authResult)
        {
            TokenInfoText.Text = "";
            if (authResult != null)
            {
                TokenInfoText.Text += $"Username: {authResult.Account.Username}" + Environment.NewLine;
                TokenInfoText.Text += $"Token Expires: {authResult.ExpiresOn.ToLocalTime()}" + Environment.NewLine;
                TokenInfoText.Text += $"Access Token: {authResult.AccessToken}" + Environment.NewLine;
            }
        }



        #endregion

        private void GetMails_Click(object sender, RoutedEventArgs e)
        {
            email_id = Email_Textbox.Text;
            timer.Interval = span;
            timer.Tick += timertick;
            timer.Start();
        }
        
        public async Task<string> getnewmail()
        {
            var httpClient = new System.Net.Http.HttpClient();
            System.Net.Http.HttpResponseMessage response;

            var url = "https://graph.microsoft.com/v1.0/me/messages?$filter=from/emailAddress/address+eq+'"+email_id+"'+and+isRead+eq+false";
            var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authtoken);
            response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            return content;
            //Mailtextblock.Text = content;
        }

        public async void timertick(object sender, object e)
        {
            timer.Stop();

            string response = await getnewmail();
            dynamic rss = JObject.Parse(response);

            JArray abc = rss.value;

            if (abc.Count <= 0)
            {
                timer.Start();
                return;
            }

            var a = abc[abc.Count - 1];
            

            foreach (dynamic item in a)
            {
                if (item.Name == "receivedDateTime")
                {
                    receivedDateTime = item.Value;
                }

                if (item.Name == "subject")
                {
                    subject = item.Value;
                }

                if (item.Name == "id")
                {
                    if (!(id == (string)item.Value))
                    {
                        id = item.Value;
                    }
                    else
                    {
                        timer.Start();
                        return;
                    }
                }

                if (item.Name == "sender")
                {
                    JObject senderinfo = item.Value;
                    
                    foreach (dynamic i in senderinfo)
                    {
                        JObject aaa = i.Value;

                         foreach (dynamic items in aaa)
                        {
                            if (items.Key == "address")
                            {
                                sendermailid = items.Value;
                            }
                        }

                    }
                }
            }

            // get mails to check for any new emails
            if (string.IsNullOrEmpty(receivedDateTime.ToString()))
            {
                timer.Start();
                return;
            }
            
            // Data for toast notification
            string title = "You've got a new Mail.";
            string mailfrom = "From : " + sendermailid;
            string mailsubject = "Subject : " + subject;

            // Toast content
            var toastContent = new ToastContent()
            {
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = title
                            },
                            new AdaptiveText()
                            {
                                Text = mailfrom
                            },
                            new AdaptiveText()
                            {
                                Text = mailsubject
                            }
                        }
                    }
                },
                Actions = new ToastActionsCustom()
                    {
                        Buttons =
                        {
                            new ToastButtonDismiss()
                        }
                },
                    Launch = "action=viewEvent&eventId=1983",
                    Scenario = ToastScenario.Reminder
            };

            // And create the toast notification
            var toast = new ToastNotification(toastContent.GetXml());

            // Set expiration time
            toast.ExpirationTime = DateTime.Now.AddMinutes(1);

            // pri
            toast.Tag = "18365";
            toast.Group = "wallPosts";

            ToastNotificationManager.CreateToastNotifier().Show(toast);

            timer.Start();
        }
    }
}


