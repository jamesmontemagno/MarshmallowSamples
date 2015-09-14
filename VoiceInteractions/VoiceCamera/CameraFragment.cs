using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Hardware;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Graphics;
using Android.Content.Res;
using Android.Media;
using Java.IO;
using Java.Nio;
using Java.Lang;

using CameraError = Android.Hardware.Camera2.CameraError;
using Java.Util;

namespace VoiceCamera
{
    public class CameraFragment : Fragment, View.IOnClickListener
    {
        private static string EXTRA_USE_FRONT_FACING_CAMERA = "android.intent.extra.USE_FRONT_CAMERA";

        private static string EXTRA_TIMER_DURATION_SECONDS = "android.intent.extra.TIMER_DURATION_SECONDS";

        private static readonly SparseIntArray ORIENTATIONS = new SparseIntArray();
        // An AutoFitTextureView for camera preview
        private AutoFitTextureView mTextureView;

        // A CameraRequest.Builder for camera preview
        private CaptureRequest.Builder mPreviewBuilder;

        // A CameraCaptureSession for camera preview
        private CameraCaptureSession mPreviewSession;

        // A reference to the opened CameraDevice
        private CameraDevice mCameraDevice;

        // TextureView.ISurfaceTextureListener handles several lifecycle events on a TextureView
        private Camera2BasicSurfaceTextureListener mSurfaceTextureListener;
        private class Camera2BasicSurfaceTextureListener : Java.Lang.Object, TextureView.ISurfaceTextureListener
        {
            private CameraFragment Fragment;
            public Camera2BasicSurfaceTextureListener(CameraFragment fragment)
            {
                Fragment = fragment;
            }
            public void OnSurfaceTextureAvailable (Android.Graphics.SurfaceTexture surface, int width, int height)
            {
                Fragment.ConfigureTransform(width, height);
                Fragment.StartPreview();
            }

            public bool OnSurfaceTextureDestroyed (Android.Graphics.SurfaceTexture surface)
            {
                return true;
            }

            public void OnSurfaceTextureSizeChanged (Android.Graphics.SurfaceTexture surface, int width, int height)
            {
                Fragment.ConfigureTransform(width, height);
                Fragment.StartPreview();
            }

            public void OnSurfaceTextureUpdated (Android.Graphics.SurfaceTexture surface)
            {

            }
        }

        // The size of the camera preview
        private Size mPreviewSize;

        // True if the app is currently trying to open the camera
        private bool mOpeningCamera;

        // CameraDevice.StateListener is called when a CameraDevice changes its state
        private CameraStateListener mStateListener;
        private class CameraStateListener : CameraDevice.StateCallback
        {
            public CameraFragment Fragment;
            public override void OnOpened (CameraDevice camera)
            {

                if (Fragment != null) {
                    Fragment.mCameraDevice = camera;
                    Fragment.StartPreview ();
                    Fragment.mOpeningCamera = false;
                }
            }

            public override void OnDisconnected (CameraDevice camera)
            {
                if (Fragment != null) {
                    camera.Close ();
                    Fragment.mCameraDevice = null;
                    Fragment.mOpeningCamera = false;
                }
            }

            public override void OnError (CameraDevice camera, CameraError error)
            {
                camera.Close();
                if (Fragment != null) {
                    Fragment.mCameraDevice = null;
                    Activity activity = Fragment.Activity;
                    Fragment.mOpeningCamera = false;
                    if (activity != null) {
                        activity.Finish ();
                    }
                }

            }
        }

        private class ImageAvailableListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
        {
            public File File;
            public void OnImageAvailable (ImageReader reader)
            {
                Image image = null;
                try 
                {
                    image = reader.AcquireLatestImage();
                    ByteBuffer buffer = image.GetPlanes()[0].Buffer;
                    byte[] bytes = new byte[buffer.Capacity()];
                    buffer.Get(bytes);
                    Save(bytes);
                }
                catch (FileNotFoundException ex) {
                    Log.WriteLine (LogPriority.Info, "Camera capture session", ex.StackTrace);
                }
                catch (IOException ex) {
                    Log.WriteLine (LogPriority.Info, "Camera capture session", ex.StackTrace);
                }
                finally {
                    if (image != null)
                        image.Close ();
                }
            }

            private void Save(byte[] bytes)
            {
                OutputStream output = null;
                try 
                {
                    if (File != null)
                    {
                        output = new FileOutputStream(File);
                        output.Write(bytes);
                    }
                }
                finally {
                    if (output != null)
                        output.Close ();
                }
            }
        }

        private class CameraCaptureListener : CameraCaptureSession.CaptureCallback
        {
            public CameraFragment Fragment;
            public File File;
            public override void OnCaptureCompleted (CameraCaptureSession session, CaptureRequest request, TotalCaptureResult result)
            {
                if (Fragment != null && File != null)
                {
                    Activity activity = Fragment.Activity;
                    if (activity != null)
                    {
                        Toast.MakeText(activity, "Saved: " + File.ToString(), ToastLength.Short).Show();
                        Fragment.StartPreview ();
                    }
                }
            }
        }

        // This CameraCaptureSession.StateListener uses Action delegates to allow the methods to be defined inline, as they are defined more than once
        private class CameraCaptureStateListener : CameraCaptureSession.StateCallback
        {
            public Action<CameraCaptureSession> OnConfigureFailedAction;
            public override void OnConfigureFailed (CameraCaptureSession session)
            {
                if (OnConfigureFailedAction != null) {
                    OnConfigureFailedAction (session);
                }
            }

            public Action<CameraCaptureSession> OnConfiguredAction;
            public override void OnConfigured (CameraCaptureSession session)
            {
                if (OnConfiguredAction != null) {
                    OnConfiguredAction (session);
                }
            }

        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            mStateListener = new CameraStateListener () { Fragment = this };
            mSurfaceTextureListener = new Camera2BasicSurfaceTextureListener (this);
            ORIENTATIONS.Append ((int)SurfaceOrientation.Rotation0, 90);
            ORIENTATIONS.Append ((int)SurfaceOrientation.Rotation90, 0);
            ORIENTATIONS.Append ((int)SurfaceOrientation.Rotation180, 270);
            ORIENTATIONS.Append ((int)SurfaceOrientation.Rotation270, 180);
        }

        public static CameraFragment NewInstance()
        {
            CameraFragment fragment = new CameraFragment ();
            fragment.RetainInstance = true;
            return fragment;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            return inflater.Inflate (Resource.Layout.fragment_camera2_basic, container, false);
        }

        public override void OnViewCreated (View view, Bundle savedInstanceState)
        {
            mTextureView = (AutoFitTextureView)view.FindViewById (Resource.Id.texture);
            mTextureView.SurfaceTextureListener = mSurfaceTextureListener;
            view.FindViewById (Resource.Id.picture).SetOnClickListener (this);
            view.FindViewById (Resource.Id.info).SetOnClickListener (this);
        }

        public override void OnResume ()
        {
            base.OnResume ();
            OpenCamera ();

            if (Activity.IsVoiceInteraction)
            {
                if (IsTimerSpecified)
                {
                    StartVoiceTimer();
                }
                else
                {
                    StartVoiceTrigger();
                }
            }

        }

        void StartVoiceTrigger()
        {
            var option = new VoiceInteractor.PickOptionRequest.Option("Cheese", 1);
            option.AddSynonym("ready");
            option.AddSynonym("go");
            option.AddSynonym("take it");
            option.AddSynonym("ok");

            var prompt = new VoiceInteractor.Prompt("Say Cheese");
            Activity.VoiceInteractor.SubmitRequest(new ChoiceRequest(this, prompt, new []{ option }));
        }

        class ChoiceRequest : VoiceInteractor.PickOptionRequest
        {
            CameraFragment frag;
            public ChoiceRequest(CameraFragment frag, VoiceInteractor.Prompt prompt, Option[] choices) 
                : base(prompt, choices, null)
            {
                this.frag = frag;
            }

            public override void OnPickOptionResult(bool finished, Option[] selections, Bundle result)
            {
                if (finished && selections.Length == 1)
                {
                    frag.TakePicture();
                }
                else
                {
                    Activity.Finish();
                }

            }
            public override void OnCancel()
            {
                base.OnCancel();
                Activity.Finish();
            }
        }

        void StartVoiceTimer()
        {
            var countdown = Arguments.GetInt(EXTRA_TIMER_DURATION_SECONDS);
            timerCountdownToast = new Toast(Activity.ApplicationContext);
            timerCountdownToast.SetGravity(GravityFlags.Center, 0, 0);
            timerCountdownToast.Duration = ToastLength.Short;

            var inflater = Activity.LayoutInflater;
            var view = inflater.Inflate(Resource.Layout.toast_timer, Activity.FindViewById<ViewGroup>(Resource.Id.toast_layout_root));
            timerCountdownToast.View = view;
            var label = view.FindViewById<TextView>(Resource.Id.countdown_text);

            var timer = new Timer("camera_timer");
            timer.ScheduleAtFixedRate(new CountdownTimer(this, label, countdown, timerCountdownToast), 1000, 1000);
        }

        class CountdownTimer : TimerTask
        {
            int countdown;
            readonly CameraFragment frag;
            readonly Toast toast;
            readonly TextView label;
            public CountdownTimer(CameraFragment frag, TextView label, int countdown, Toast toast)
            {
                this.frag = frag;
                this.countdown = countdown;
                this.toast = toast;
                this.label = label;
            }
            #region implemented abstract members of TimerTask
            public override void Run()
            {
                frag.Activity.RunOnUiThread(() =>
                    {
                        if (countdown < 0)
                        {
                            toast.Cancel();
                            frag.TakePicture();
                        }
                        else
                        {
                            label.Text = "Photo in " + countdown.ToString("D");
                            toast.Show();
                        }
                    });

                countdown--;
                if (countdown < 0)
                    Cancel();
                
            }
            #endregion
        }

        Toast timerCountdownToast;

        private bool IsCameraSpecified {
            get{
                
                return Arguments != null && Arguments.ContainsKey(EXTRA_USE_FRONT_FACING_CAMERA);
            }
        }

        private bool IsTimerSpecified {
            get{
                return Arguments != null && Arguments.ContainsKey(EXTRA_TIMER_DURATION_SECONDS);
            }
        }



        private bool ShouldUseCamera(int lensFacing)
        {
            if (Arguments.GetBoolean(EXTRA_USE_FRONT_FACING_CAMERA))
            {
                return lensFacing == (int)LensFacing.Front;
            }
            return lensFacing == (int)LensFacing.Back;
        }


        public override void OnPause ()
        {
            base.OnPause ();
            if (mCameraDevice != null) {
                mCameraDevice.Close ();
                mCameraDevice = null;
            }
        }

        // Opens a CameraDevice. The result is listened to by 'mStateListener'.
        private void OpenCamera()
        {
            Activity activity = Activity;
            if (activity == null || activity.IsFinishing || mOpeningCamera) {
                return;
            }
            mOpeningCamera = true;
            CameraManager manager = (CameraManager)activity.GetSystemService (Context.CameraService);
            try 
            {

                bool front = Arguments.GetBoolean(EXTRA_USE_FRONT_FACING_CAMERA);
                if(front)
                    Log.Debug("VoiceCamera", "Front specified");
                else
                    Log.Debug("VoiceCamera", "Back Specified");

                Log.Debug("VoiceCamera", "Front ID: " + (int)LensFacing.Front + " Back ID: " + (int)LensFacing.Back);
                
                foreach(var cameraId in manager.GetCameraIdList())
                {
                   

                    // To get a list of available sizes of camera preview, we retrieve an instance of
                    // StreamConfigurationMap from CameraCharacteristics
                    var characteristics = manager.GetCameraCharacteristics(cameraId);
                    var lens = characteristics.Get(CameraCharacteristics.LensFacing);
                    Log.Debug("VoiceCamera", "LensFacing: " + lens.ToString());
                    if(!IsCameraSpecified)
                    {
                        Log.Debug("VoiceCamera", "camera not specified");
                        if((int)lens == (int)LensFacing.Front)
                        {
                            Log.Debug("VoiceCamera", "Skip front facing camera");
                            //we don't use a front facing camera in this sample.
                            continue;
                        }
                    }
                    else if(!ShouldUseCamera((int)lens))
                    {
                        Log.Debug("VoiceCamera", "Skipping over camera, should not use"); 
                        //TODO: Handle case where there is no camera match
                        continue;
                    }

                    Log.Debug("VoiceCamera", "Using this camera"); 


                    StreamConfigurationMap map = (StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap);
                    mPreviewSize = map.GetOutputSizes(Java.Lang.Class.FromType(typeof(SurfaceTexture)))[0];
                    Android.Content.Res.Orientation orientation = Resources.Configuration.Orientation;
                    if (orientation == Android.Content.Res.Orientation.Landscape)
                    {
                        mTextureView.SetAspectRatio(mPreviewSize.Width, mPreviewSize.Height);
                    }
                    else 
                    {
                        mTextureView.SetAspectRatio(mPreviewSize.Height, mPreviewSize.Width);
                    }

                    // We are opening the camera with a listener. When it is ready, OnOpened of mStateListener is called.
                    manager.OpenCamera(cameraId, mStateListener, null);
                    return;
                }
            }
            catch (CameraAccessException ex) {
                Toast.MakeText (activity, "Cannot access the camera.", ToastLength.Short).Show ();
                Activity.Finish ();
            } catch (NullPointerException) {
                var dialog = new ErrorDialog ();
                dialog.Show (FragmentManager, "dialog");
            }
        }

        /// <summary>
        /// Starts the camera previe
        /// </summary>
        private void StartPreview()
        {
            if (mCameraDevice == null || !mTextureView.IsAvailable || mPreviewSize == null) {
                return;
            }
            try 
            {
                SurfaceTexture texture = mTextureView.SurfaceTexture;
                System.Diagnostics.Debug.Assert( texture != null );

                // We configure the size of the default buffer to be the size of the camera preview we want
                texture.SetDefaultBufferSize(mPreviewSize.Width, mPreviewSize.Height);

                // This is the output Surface we need to start the preview
                Surface surface = new Surface(texture);

                // We set up a CaptureRequest.Builder with the output Surface
                mPreviewBuilder = mCameraDevice.CreateCaptureRequest(CameraTemplate.Preview);
                mPreviewBuilder.AddTarget(surface);

                // Here, we create a CameraCaptureSession for camera preview.
                mCameraDevice.CreateCaptureSession(new List<Surface>() { surface }, 
                    new CameraCaptureStateListener() 
                    { 
                        OnConfigureFailedAction = (CameraCaptureSession session) => 
                            {
                                Activity activity = Activity;
                                if (activity != null)
                                {
                                    Toast.MakeText(activity, "Failed", ToastLength.Short).Show();
                                }
                            },
                        OnConfiguredAction = (CameraCaptureSession session) =>
                            {
                                mPreviewSession = session;
                                UpdatePreview ();
                            }
                    },
                    null);


            }
            catch (CameraAccessException ex) {
                Log.WriteLine (LogPriority.Info, "Camera2BasicFragment", ex.StackTrace);
            }
        }

        /// <summary>
        /// Updates the camera preview, StartPreview() needs to be called in advance
        /// </summary>
        private void UpdatePreview()
        {
            if (mCameraDevice == null) {
                return;
            }

            try 
            {
                // The camera preview can be run in a background thread. This is a Handler for the camere preview
                SetUpCaptureRequestBuilder(mPreviewBuilder);
                HandlerThread thread = new HandlerThread("CameraPreview");
                thread.Start();
                Handler backgroundHandler = new Handler(thread.Looper);

                // Finally, we start displaying the camera preview
                mPreviewSession.SetRepeatingRequest(mPreviewBuilder.Build(), null, backgroundHandler);
            }
            catch (CameraAccessException ex) {
                Log.WriteLine (LogPriority.Info, "Camera2BasicFragment", ex.StackTrace);
            }
        }

        /// <summary>
        /// Sets up capture request builder.
        /// </summary>
        /// <param name="builder">Builder.</param>
        private void SetUpCaptureRequestBuilder(CaptureRequest.Builder builder)
        {
            // In this sample, w just let the camera device pick the automatic settings
            builder.Set (CaptureRequest.ControlMode, new Java.Lang.Integer((int)ControlMode.Auto));
        }

        /// <summary>
        /// Configures the necessary transformation to mTextureView.
        /// This method should be called after the camera preciew size is determined in openCamera, and also the size of mTextureView is fixed
        /// </summary>
        /// <param name="viewWidth">The width of mTextureView</param>
        /// <param name="viewHeight">VThe height of mTextureView</param>
        private void ConfigureTransform(int viewWidth, int viewHeight)
        {
            Activity activity = Activity;
            if (mTextureView == null || mPreviewSize == null || activity == null) {
                return;
            }

            SurfaceOrientation rotation = activity.WindowManager.DefaultDisplay.Rotation;
            Matrix matrix = new Matrix ();
            RectF viewRect = new RectF (0, 0, viewWidth, viewHeight);
            RectF bufferRect = new RectF (0, 0, mPreviewSize.Width, mPreviewSize.Height);
            float centerX = viewRect.CenterX();
            float centerY = viewRect.CenterY();
            if (rotation == SurfaceOrientation.Rotation90 || rotation == SurfaceOrientation.Rotation270) {
                bufferRect.Offset (centerX - bufferRect.CenterX(), centerY - bufferRect.CenterY());
                matrix.SetRectToRect (viewRect, bufferRect, Matrix.ScaleToFit.Fill);
                float scale = System.Math.Max ((float)viewHeight / mPreviewSize.Height, (float)viewWidth / mPreviewSize.Width);
                matrix.PostScale (scale, scale, centerX, centerY);
                matrix.PostRotate (90 * ((int)rotation - 2), centerX, centerY);
            }
            mTextureView.SetTransform (matrix);
        }

        /// <summary>
        /// Takes a picture.
        /// </summary>
        private void TakePicture()
        {
            try 
            {
                Activity activity = Activity;
                if (activity == null || mCameraDevice == null) {
                    return;
                }
                CameraManager manager = (CameraManager) activity.GetSystemService(Context.CameraService);

                // Pick the best JPEG size that can be captures with this CameraDevice
                CameraCharacteristics characteristics = manager.GetCameraCharacteristics(mCameraDevice.Id);
                Size[] jpegSizes = null;
                if (characteristics != null)
                {
                    jpegSizes = ((StreamConfigurationMap)characteristics.Get(CameraCharacteristics.ScalerStreamConfigurationMap)).GetOutputSizes((int)ImageFormatType.Jpeg);
                }
                int width = 640;
                int height = 480;
                if (jpegSizes != null && jpegSizes.Length > 0)
                {
                    width = jpegSizes[0].Width;
                    height = jpegSizes[0].Height;
                }

                // We use an ImageReader to get a JPEG from CameraDevice
                // Here, we create a new ImageReader and prepare its Surface as an output from the camera
                ImageReader reader = ImageReader.NewInstance(width, height, ImageFormatType.Jpeg, 1);
                List<Surface> outputSurfaces = new List<Surface>(2);
                outputSurfaces.Add(reader.Surface);
                outputSurfaces.Add(new Surface(mTextureView.SurfaceTexture));

                CaptureRequest.Builder captureBuilder = mCameraDevice.CreateCaptureRequest(CameraTemplate.StillCapture);
                captureBuilder.AddTarget(reader.Surface);
                SetUpCaptureRequestBuilder(captureBuilder);
                // Orientation
                SurfaceOrientation rotation = activity.WindowManager.DefaultDisplay.Rotation;
                captureBuilder.Set(CaptureRequest.JpegOrientation, new Java.Lang.Integer(ORIENTATIONS.Get((int)rotation)));

                // Output file
                File file = new File(activity.GetExternalFilesDir(null), "pic.jpg");

                // This listener is called when an image is ready in ImageReader 
                // Right click on ImageAvailableListener in your IDE and go to its definition
                ImageAvailableListener readerListener = new ImageAvailableListener() { File = file };

                // We create a Handler since we want to handle the resulting JPEG in a background thread
                HandlerThread thread = new HandlerThread("CameraPicture");
                thread.Start();
                Handler backgroundHandler = new Handler(thread.Looper);
                reader.SetOnImageAvailableListener(readerListener, backgroundHandler);

                //This listener is called when the capture is completed
                // Note that the JPEG data is not available in this listener, but in the ImageAvailableListener we created above
                // Right click on CameraCaptureListener in your IDE and go to its definition
                CameraCaptureListener captureListener = new CameraCaptureListener() { Fragment = this, File = file };

                mCameraDevice.CreateCaptureSession(outputSurfaces, new CameraCaptureStateListener()
                    {
                        OnConfiguredAction = (CameraCaptureSession session) => {
                            try 
                            {
                                session.Capture(captureBuilder.Build(), captureListener, backgroundHandler);
                            }
                            catch (CameraAccessException ex)
                            {
                                Log.WriteLine(LogPriority.Info, "Capture Session error: ", ex.ToString());
                            }
                        }
                    }, backgroundHandler );
            }
            catch (CameraAccessException ex) {
                Log.WriteLine(LogPriority.Info, "Taking picture error: ", ex.StackTrace);
            }
        }

        public void OnClick (View v)
        {
            switch (v.Id) {
                case Resource.Id.picture:
                    TakePicture ();
                    break;
                case Resource.Id.info:
                    EventHandler<DialogClickEventArgs> nullHandler = null;
                    Activity activity = Activity;
                    if (activity != null) {
                        new AlertDialog.Builder (activity)
                            .SetMessage ("This sample demonstrates the basic use of the Camera2 API. ...")
                            .SetPositiveButton (Android.Resource.String.Ok, nullHandler)
                            .Show ();
                    }
                    break;
            }
        }

        public class ErrorDialog : DialogFragment {
            public override Dialog OnCreateDialog (Bundle savedInstanceState)
            {
                var alert = new AlertDialog.Builder (Activity);
                alert.SetMessage ("This device doesn't support Camera2 API.");
                alert.SetPositiveButton (Android.Resource.String.Ok, new MyDialogOnClickListener (this));
                return alert.Show();

            }
        }

        private class MyDialogOnClickListener : Java.Lang.Object,IDialogInterfaceOnClickListener
        {
            ErrorDialog er;
            public MyDialogOnClickListener(ErrorDialog e)
            {
                er = e;
            }
            public void OnClick(IDialogInterface dialogInterface, int i)
            {
                er.Activity.Finish ();
            }
        }
    }
}

