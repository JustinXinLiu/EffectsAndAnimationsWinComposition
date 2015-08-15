using Microsoft.Graphics.Canvas.Effects;
using System;
using System.Numerics;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace BlurEffect
{
    public sealed partial class MainPage : Page
    {
        Compositor _compositor;
        Visual _touchAreaVisual;
        EffectVisual _effectVisual;
        Vector3KeyFrameAnimation _animation;
        float _x;

        public MainPage()
        {
            this.InitializeComponent();
        }

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

            // todo: not sure why GaussianBlurEffect doesn't work??
            // Got a feeling it might have something to do with the Source setting, 
            // maybe it's just not supported yet?
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

        private static ContainerVisual GetVisual(FrameworkElement element)
        {
            return (ContainerVisual)ElementCompositionPreview.GetContainerVisual(element);
        }
    }
}