﻿using System;
using ReactiveUI;
using UIKit;
using CodeHub.WebViews;
using CodeHub.Core.ViewModels.App;
using Foundation;
using CodeHub.iOS.Services;
using System.Threading.Tasks;
using System.Linq;
using CodeHub.Core.Services;
using Splat;
using CodeHub.Core.ViewModels;
using CodeHub.iOS.ViewControllers;
using BigTed;
using System.Reactive.Disposables;

namespace CodeHub.iOS.Views.App
{
    public class UpgradeView : BaseViewController
    {
        private readonly IFeaturesService _featuresService;
        private readonly IInAppPurchaseService _inAppPurchaseService;
        private readonly UIWebView _web;
        private readonly UIActivityIndicatorView _activityView;

        public UpgradeView()
        {
            _featuresService = Locator.Current.GetService<IFeaturesService>();
            _inAppPurchaseService = Locator.Current.GetService<IInAppPurchaseService>();

            _web = new UIWebView { ScalesPageToFit = true, AutoresizingMask = UIViewAutoresizing.All };
            _web.LoadFinished += (sender, e) => NetworkActivityService.Instance.PopNetworkActive();
            _web.LoadStarted += (sender, e) => NetworkActivityService.Instance.PushNetworkActive();
            _web.LoadError += (sender, e) => NetworkActivityService.Instance.PopNetworkActive();
            _web.ShouldStartLoad = (w, r, n) => ShouldStartLoad(r, n);

            _activityView = new UIActivityIndicatorView
            {
                Color = Theme.PrimaryNavigationBarColor,
                AutoresizingMask = UIViewAutoresizing.FlexibleWidth,
            };
        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            Title = "Pro Upgrade";

            _activityView.Frame = new CoreGraphics.CGRect(0, 44, View.Frame.Width, 88f);
            _web.Frame = new CoreGraphics.CGRect(0, 0, View.Frame.Width, View.Frame.Height);
            Add(_web);

            Load().ToBackground();
        }

        private async Task Load()
        {
            _web.UserInteractionEnabled = false;
            _web.LoadHtmlString("", NSBundle.MainBundle.BundleUrl);

            _activityView.Alpha = 1;
            _activityView.StartAnimating();
            View.Add(_activityView);

            var productData = (await _inAppPurchaseService.RequestProductData(FeaturesService.ProEdition)).Products.FirstOrDefault();
            var enabled = _featuresService.IsProEnabled;
            var model = new UpgradeDetailsModel(productData != null ? productData.LocalizedPrice() : null, enabled);

            var content = new UpgradeDetailsRazorView { Model = model }.GenerateString();
            _web.LoadHtmlString(content, NSBundle.MainBundle.BundleUrl);
            _web.UserInteractionEnabled = true;

            UIView.Animate(0.2f, 0, UIViewAnimationOptions.BeginFromCurrentState | UIViewAnimationOptions.CurveEaseInOut,
                () => _activityView.Alpha = 0, () =>
                {
                    _activityView.RemoveFromSuperview();
                    _activityView.StopAnimating();
                });
        }

        protected virtual bool ShouldStartLoad (NSUrlRequest request, UIWebViewNavigationType navigationType)
        {

            var url = request.Url;

            if (url.Scheme.Equals("app"))
            {
                var func = url.Host;

                if (string.Equals(func, "buy", StringComparison.OrdinalIgnoreCase))
                {
                    // Purchase
                    Activate(_featuresService.ActivatePro).ToBackground();
                }
                else if (string.Equals(func, "restore", StringComparison.OrdinalIgnoreCase))
                {
                    // Restore
                    Activate(_featuresService.RestorePro).ToBackground();
                }

                return false;
            }

            if (url.Scheme.Equals("file"))
            {
                return true;
            }

            if (url.Scheme.Equals("http") || url.Scheme.Equals("https"))
            {
                var vm = new WebBrowserViewModel().Init(url.AbsoluteString);
                var view = new WebBrowserView(true, true) { ViewModel = vm };
                view.NavigationItem.LeftBarButtonItem = new UIBarButtonItem(Images.Cancel, UIBarButtonItemStyle.Done, 
                    (s, e) => DismissViewController(true, null));
                PresentViewController(new ThemedNavigationController(view), true, null);
                return false;
            }

            return false;
        }

        private async Task Activate(Func<Task> activation)
        {
            BTProgressHUD.ShowContinuousProgress("Activating...", ProgressHUD.MaskType.Gradient);
            using (Disposable.Create(BTProgressHUD.Dismiss))
                await activation();
            
            BTProgressHUD.ShowSuccessWithStatus("Activated!");
            await Load();
        }
    }
}

