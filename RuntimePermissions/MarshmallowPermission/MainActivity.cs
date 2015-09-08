using Android.App;
using Android.Widget;
using Android.OS;
using Android.Support.V7.App;
using System.Threading.Tasks;
using Android;
using Android.Support.V4.Content;
using AlertDialog = Android.Support.V7.App.AlertDialog;
using Geolocator.Plugin;
using System;
using Android.Content.PM;
using Android.Views;
using Android.Support.Design.Widget;
using Android.Support.V4.App;

namespace MarshmallowPermission
{
	[Activity (Label = "Marshmallow Permission", MainLauncher = true, Icon = "@mipmap/icon")]
	public class MainActivity : AppCompatActivity
	{
        const int RequestLocationId = 0;
      
        readonly string [] PermissionsLocation = 
            {
                Manifest.Permission.AccessCoarseLocation,
                Manifest.Permission.AccessFineLocation
            };

        TextView textLocation;
        Button buttonGetLocation, buttonGetLocationCompat;
        View layout;
		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);

			SetContentView (Resource.Layout.Main);
           
            layout = FindViewById<LinearLayout>(Resource.Id.main_layout);
            textLocation = FindViewById<TextView>(Resource.Id.label);

            buttonGetLocation = FindViewById<Button> (Resource.Id.myButton);
            buttonGetLocationCompat = FindViewById<Button>(Resource.Id.myButtonCompat);

            buttonGetLocation.Click += async (sender, e) => await TryGetLocationAsync();
            buttonGetLocationCompat.Click += async (sender, e) => await GetLocationCompatAsync();
		}

        async Task TryGetLocationAsync()
        {
            if ((int)Build.VERSION.SdkInt < 23)
            {
                await GetLocationAsync();
                return;
            }

            await GetLocationPermissionAsync();
        }

        async Task GetLocationAsync()
        {
  
            textLocation.Text = "Getting Location";
            try
            {
                var locator = CrossGeolocator.Current;
                locator.DesiredAccuracy = 100;
                var position = await locator.GetPositionAsync(20000);

                textLocation.Text = string.Format("Lat: {0}  Long: {1}", position.Latitude, position.Longitude);
            }
            catch (Exception ex)
            {

                textLocation.Text = "Unable to get location: " + ex.ToString();
            }
        }

        async Task GetLocationPermissionAsync()
        {
            const string permission = Manifest.Permission.AccessFineLocation;

            if (CheckSelfPermission(permission) == (int)Permission.Granted)
            {
                await GetLocationAsync();
                return;
            }

            if (ShouldShowRequestPermissionRationale(permission))
            {
                //Explain to the user why we need to read the contacts
                Snackbar.Make(layout, "Location access is required to show coffee shops nearby.",
                    Snackbar.LengthIndefinite)
                    .SetAction("OK", v => RequestPermissions(PermissionsLocation, RequestLocationId))
                    .Show();
               
                return;
            }

            RequestPermissions(PermissionsLocation, RequestLocationId); 

        }

        public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, int[] grantResults)
        {
            switch (requestCode)
            {
                case RequestLocationId:
                    {
                        if (grantResults[0] == (int)Permission.Granted)
                        {
                            //Permission granted
                            var snack = Snackbar.Make(layout, "Location permission is available, getting lat/long.",
                                            Snackbar.LengthShort);
                            snack.Show();
                            
                            await GetLocationAsync();
                        }
                        else
                        {
                            //Permission Denied :(
                            //Disabling location functionality
                            var snack = Snackbar.Make(layout, "Location permission is denied.", Snackbar.LengthShort);
                            snack.Show();
                        }
                    }
                    break;
            }
        }

        async Task GetLocationCompatAsync()
        {
            const string permission = Manifest.Permission.AccessFineLocation;

            if (ContextCompat.CheckSelfPermission(this, permission) == (int)Permission.Granted)
            {
                await GetLocationAsync();
                return;
            }

            if (ActivityCompat.ShouldShowRequestPermissionRationale(this, permission))
            {
                //Explain to the user why we need to read the contacts
                Snackbar.Make(layout, "Location access is required to show coffee shops nearby.",
                    Snackbar.LengthIndefinite)
                    .SetAction("OK", v => RequestPermissions(PermissionsLocation, RequestLocationId))
                    .Show();

                return;
            }

            RequestPermissions(PermissionsLocation, RequestLocationId); 
        }
               
	}
}
