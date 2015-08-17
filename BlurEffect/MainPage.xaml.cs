using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace BlurEffect
{
    /// <summary>
    /// Please note that I could simplify things by using the TouchArea Rectangle as the container visual to host the effect visual, 
    /// but I wanted to prove a point that I could use expression key frame animation to manipulate multiple elements at the same time!
    /// </summary>
    public sealed partial class MainPage : Page
    {
        Compositor _compositor;
        ContainerVisual _touchAreaVisual;
        EffectVisual _effectVisual;
        Vector3KeyFrameAnimation _animation;
        float _x;

        CanvasBitmap _bitmap;

        public MainPage()
        {
            this.InitializeComponent();
        }

        #region Composition

        async void LayoutRoot_Loaded(object sender, RoutedEventArgs e)
        {
            // get visuals from xaml object
            _touchAreaVisual = GetVisual(this.TouchArea);
            var imagePanelVisual = GetVisual(this.ImagePanel);

            // get compositor
            _compositor = imagePanelVisual.Compositor;

            // load the background image
            var image = _compositor.DefaultGraphicsDevice.CreateImageFromUri(new Uri("ms-appx:///Assets/White.png"));
            await image.CompleteLoadAsync();

            // currently GaussianBlurEffect is not supported in Composition
            var effectDefination = new SaturationEffect // new GaussianBlurEffect
            {
                //BorderMode = EffectBorderMode.Soft,
                //BlurAmount = 5f,
                //Optimization = EffectOptimization.Quality,
                Source = new CompositionEffectSourceParameter("Overlay")
            };

            // create the actual effect
            var effectFactory = _compositor.CreateEffectFactory(effectDefination);
            var effect = effectFactory.CreateEffect();
            effect.SetSourceParameter("Overlay", image);

            // create the effect visual
            _effectVisual = _compositor.CreateEffectVisual();
            _effectVisual.Effect = effect;
            _effectVisual.Opacity = 0.8f;
            _effectVisual.Size = new Vector2((float)this.ImagePanel.ActualWidth, (float)this.ImagePanel.ActualHeight);

            // place the effect visual onto the UI
            imagePanelVisual.Children.InsertAtTop(_effectVisual);
        }

        void TouchArea_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            // reset the animation
            // todo: wonder if there should be a method to remove a certain key frame?
            // so I'd only need to remove the keyframe (_animation.InsertKeyFrame(1.0f, new Vector3());)
            // rather than create a new animation instance
            _x = 0.0f;
            _animation = _compositor.CreateVector3KeyFrameAnimation();
            _animation.InsertExpressionKeyFrame(0.0f, "touch.Offset");
            _animation.SetReferenceParameter("touch", _touchAreaVisual);
        }

        void TouchArea_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            // get the movement on X axis
            _x += (float)e.Delta.Translation.X;

            // keep the pan within the bountry
            if (_x < -this.ImagePanel.ActualWidth / 2 || _x > 0) return;

            // set the pan rectangle's visual's offset
            _touchAreaVisual.Offset = new Vector3(_x, 0.0f, 0.0f);
            // kick off the effect visual's animation so to have both visuals' offset in sync
            _effectVisual.ConnectAnimation("Offset", _animation).Start();
        }

        void TouchArea_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            // once the finger lifts up, add another key frame and
            // kick off the finish animation to roll back the visuals' offset
            _animation.InsertKeyFrame(1.0f, new Vector3());
            _effectVisual.ConnectAnimation("Offset", _animation).Start();
            _touchAreaVisual.ConnectAnimation("Offset", _animation).Start();
        }

        static ContainerVisual GetVisual(FrameworkElement element)
        {
            return (ContainerVisual)ElementCompositionPreview.GetContainerVisual(element);
        }

        #endregion

        #region Win2D

        void Canvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            args.TrackAsyncAction(CreateResourcesAsync(sender).AsAsyncAction());
        }

        async Task CreateResourcesAsync(CanvasControl sender)
        {
            // give it a little bit delay to ensure the image is load, ideally you want to Image.ImageOpened event instead
            await Task.Delay(200);

            using (var stream = new InMemoryRandomAccessStream())
            {
                // get the stream from the background image
                var target = new RenderTargetBitmap();
                await target.RenderAsync(this.Image2);

                var pixelBuffer = await target.GetPixelsAsync();
                var pixels = pixelBuffer.ToArray();

                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, stream);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight, (uint)target.PixelWidth, (uint)target.PixelHeight, 96, 96, pixels);

                await encoder.FlushAsync();
                stream.Seek(0);

                // load the stream into our bitmap
                _bitmap = await CanvasBitmap.LoadAsync(sender, stream);
            }
        }

        void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            using (var session = args.DrawingSession)
            {
                var blur = new GaussianBlurEffect
                {
                    BlurAmount = 50.0f, // increase this to make it more blurry or vise versa.
                    //Optimization = EffectOptimization.Balanced, // default value
                    //BorderMode = EffectBorderMode.Soft // default value
                    Source = _bitmap
                };

                session.DrawImage(blur, new Rect(0, 0, sender.ActualWidth, sender.ActualHeight),
                    new Rect(0, 0, _bitmap.SizeInPixels.Width, _bitmap.SizeInPixels.Height), 0.9f);
            }
        }

        void Overlay_ManipulationStarted(object sender, ManipulationStartedRoutedEventArgs e)
        {
            // reset the inital with of the Rect
            _x = (float)this.ImagePanel2.ActualWidth;
        }

        void Overlay_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            // get the movement on X axis
            _x += (float)e.Delta.Translation.X;

            // keep the pan within the bountry
            if (_x > this.ImagePanel2.ActualWidth || _x < 0) return;

            // we clip the overlay to reveal the actual image underneath
            this.Clip.Rect = new Rect(0, 0, _x, this.ImagePanel2.ActualHeight);
        }

        void Overlay_ManipulationCompleted(object sender, ManipulationCompletedRoutedEventArgs e)
        {
            // reset the clip to show the full overlay
            this.Clip.Rect = new Rect(0, 0, this.ImagePanel2.ActualWidth, this.ImagePanel2.ActualHeight);
        }

        #endregion
    }
}