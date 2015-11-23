
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Gms.Actions;
using Android.Support.V7.App;
using Android.Provider;
using Android.Util;

namespace VoiceCamera
{
    [Activity(Label = "Voice Camera", LaunchMode = Android.Content.PM.LaunchMode.SingleTop)]  
    [IntentFilter(new []{MediaStore.IntentActionStillImageCamera}, Categories = new []{Intent.CategoryDefault, Intent.CategoryVoice})]
    [IntentFilter(new []{MediaStore.IntentActionStillImageCameraSecure}, Categories = new []{Intent.CategoryDefault, Intent.CategoryVoice})]
    public class VoiceActivity : BaseActivity
    {

        protected override int LayoutResource
        {
            get
            {
                return Resource.Layout.activity_camera;
            }
        }

        CameraChoiceRequest request; 
        Button buttonRear, buttonFront;
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            var intent = Intent;
            if (intent == null)
            {
                Finish();
                return;
            }
           
            if (MainActivity.NeedPermissions(this))
            {
                StartActivity(new Intent(this, typeof(MainActivity)).SetFlags(ActivityFlags.NewTask));
                Finish();
                return;
            }

            if (!IsVoiceInteraction)
            {
                if (intent != null)
                {
                    intent.SetComponent(null);
                    intent.SetPackage("com.google.android.GoogleCamera");
                    intent.SetFlags(ActivityFlags.NewTask);
                    StartActivity(intent);
                }
                Finish();
                return;
            }

            buttonFront = FindViewById<Button>(Resource.Id.button_front);
            buttonRear = FindViewById<Button>(Resource.Id.button_rear);


           

            buttonFront.Click += (sender, e) => 
                {
                    var fragment = CameraFragment.NewInstance();
                    Intent.PutExtra("android.intent.extra.USE_FRONT_CAMERA", true);
                    fragment.Arguments = Intent.Extras;
                    FragmentManager.BeginTransaction().Replace(Resource.Id.container, fragment).Commit();
                    buttonRear.Visibility = ViewStates.Gone;
                    buttonFront.Visibility = ViewStates.Gone;
                    request.Cancel();
                };

            buttonRear.Click += (sender, e) => 
                {
                    var fragment = CameraFragment.NewInstance();
                    Intent.PutExtra("android.intent.extra.USE_FRONT_CAMERA", false);
                    fragment.Arguments = Intent.Extras;
                    FragmentManager.BeginTransaction().Replace(Resource.Id.container, fragment).Commit();
                    buttonRear.Visibility = ViewStates.Gone;
                    buttonFront.Visibility = ViewStates.Gone;
                    request.Cancel();
                };
        }

protected override void OnResume()
{
    base.OnResume();
    if (!IsVoiceInteraction)
        return;

    //Send our our first request asking for front or rear facing camera to use.
    var front = new VoiceInteractor.PickOptionRequest.Option("Front Camera", 0);
    front.AddSynonym("Front");
    front.AddSynonym("Selfie");
    front.AddSynonym("Forward");

    var rear = new VoiceInteractor.PickOptionRequest.Option("Rear Camera", 1);
    rear.AddSynonym("Rear");
    rear.AddSynonym("Back");
    rear.AddSynonym("Normal");

    var prompt = new VoiceInteractor.Prompt("Which camera would you like to use?");
    request = new CameraChoiceRequest(prompt, new [] { front, rear }, new [] {buttonFront, buttonRear});


    VoiceInteractor.SubmitRequest(request);
        
}

        class ConfirmTaxiRequest : VoiceInteractor.ConfirmationRequest
        {
            public ConfirmTaxiRequest(VoiceInteractor.Prompt prompt) 
                :base(prompt, null)
            {
            }
            public override void OnConfirmationResult(bool confirmed, Bundle result)
            {
                base.OnConfirmationResult(confirmed, result);
                if (confirmed)
                {
                    //Finalize taxi confiramation
                    Toast.MakeText(Activity, "Your taxi has been confirmed.", ToastLength.Long).Show();
                }
                else
                {
                    //Finalize taxi confiramation
                    Toast.MakeText(Activity, "Your taxi has been confirmed.", ToastLength.Long).Show();
                }

                Activity.Finish();
            }

            public override void OnCancel()
            {
                base.OnCancel();
                Activity.Finish();
            }
        }

        protected class CameraChoiceRequest : VoiceInteractor.PickOptionRequest
        {
            View[] views;
            public CameraChoiceRequest(VoiceInteractor.Prompt prompt, Option[] choices, View[] views) 
                : base(prompt, choices, null)
            {
                this.views = views;
            }

            public override void OnPickOptionResult(bool finished, Option[] selections, Bundle result)
            {
                base.OnPickOptionResult(finished, selections, result);

                if (!finished || selections.Length != 1)
                    return;

                Log.Debug("VoiceCamera", "Selected: " + selections[0].Label + " Index: " + selections[0].Index);

                var fragment = CameraFragment.NewInstance();
                Activity.Intent.PutExtra("android.intent.extra.USE_FRONT_CAMERA", selections[0].Index == 0);
                fragment.Arguments = Activity.Intent.Extras;
                Activity.FragmentManager.BeginTransaction().Replace(Resource.Id.container, fragment).Commit();
                foreach (var view in views)
                    view.Visibility = ViewStates.Gone;
            }
        }
    }
}

