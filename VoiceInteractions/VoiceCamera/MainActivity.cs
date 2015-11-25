using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Content.PM;
using Android;
using System.Linq;

namespace VoiceCamera
{
    [Activity(Label = "VoiceCamera", MainLauncher = true, Icon = "@drawable/ic_launcher")]
    public class MainActivity : BaseActivity
    {
        readonly string [] Permissions = 
            {
                Manifest.Permission.Camera,
                Manifest.Permission.WriteExternalStorage
            };
        
        protected override int LayoutResource
        {
            get { return Resource.Layout.main; }
        }

        View layout;
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            layout = FindViewById<LinearLayout>(Resource.Id.main);

            var intent = Intent;
            if (NeedPermissions(this))
            {
                RequestPermissions();
            }
            else if (intent != null)
            {
                intent.SetComponent(null);
                intent.SetPackage("com.google.android.GoogleCamera");
                intent.SetFlags(ActivityFlags.NewTask);
                StartActivity(intent);
                Finish();
            }
            else
                Finish();
           

            SupportActionBar.SetDisplayHomeAsUpEnabled(false);
            SupportActionBar.SetHomeButtonEnabled(false);

        }
        const int RequestLocationId = 0;


        public static bool NeedPermissions(Activity activity)
        {
            return activity.CheckSelfPermission(Manifest.Permission.Camera) != Permission.Granted ||
            activity.CheckSelfPermission(Manifest.Permission.WriteExternalStorage) != Permission.Granted;
        }

        void RequestPermissions()
        {
          RequestPermissions(Permissions, RequestLocationId); 
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            switch (requestCode)
            {
                case RequestLocationId:
                    {
                        bool hasAllPermissions = grantResults.Where(r => r == Permission.Denied).Count() == 0;

                        if (!hasAllPermissions)
                            Toast.MakeText(this, "Unable to get all required permissions", ToastLength.Long).Show();

                        Finish();
                          
                    }
                    break;
            }
        }
       
    }
}

