namespace MobileApp.Controls;

public partial class SkeletonLoader : ContentView
{
    public static readonly BindableProperty TypeProperty =
        BindableProperty.Create(nameof(Type), typeof(SkeletonType), typeof(SkeletonLoader), SkeletonType.Card);

    public SkeletonType Type
    {
        get => (SkeletonType)GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    public SkeletonLoader()
    {
        InitializeComponent();
        StartShimmerAnimation();
    }

    private void StartShimmerAnimation()
    {
        // Animate the whole loader (single animation per loader) to reduce
        // the number of concurrent animations and main-thread overhead.
        AnimateShimmer(this);
    }

    private void FindSkeletonElements(IView view, List<View> elements)
    {
        // No longer used but kept for compatibility if needed in future.
        if (view is Border border && border.StyleId != "NoShimmer")
        {
            elements.Add(border);
        }
        else if (view is BoxView boxView && boxView.StyleId != "NoShimmer")
        {
            elements.Add(boxView);
        }

        if (view is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                FindSkeletonElements(child, elements);
            }
        }
        else if (view is ContentView contentView && contentView.Content != null)
        {
            FindSkeletonElements(contentView.Content, elements);
        }
        else if (view is ScrollView scrollView && scrollView.Content != null)
        {
            FindSkeletonElements(scrollView.Content, elements);
        }
    }

    private async void AnimateShimmer(View element)
    {
        // Create a pulsing opacity animation for shimmer effect on the whole
        // loader to minimize concurrent animations.
        try
        {
            while (true)
            {
                await element.FadeTo(0.7, 700, Easing.SinInOut);
                await element.FadeTo(1.0, 700, Easing.SinInOut);
            }
        }
        catch (Exception)
        {
            // Animation may be cancelled when the view is unloaded; swallow.
        }
    }
}

public enum SkeletonType
{
    Card,
    AssetItem,
    StatCard
}
