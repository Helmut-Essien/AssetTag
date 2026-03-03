namespace MobileApp.Controls;

public partial class SkeletonListView : ContentView
{
    public static readonly BindableProperty CountProperty =
        BindableProperty.Create(nameof(Count), typeof(int), typeof(SkeletonListView), 5, propertyChanged: OnCountChanged);

    public static readonly BindableProperty TypeProperty =
        BindableProperty.Create(nameof(Type), typeof(SkeletonType), typeof(SkeletonListView), SkeletonType.Card, propertyChanged: OnTypeChanged);

    public static readonly BindableProperty ItemSpacingProperty =
        BindableProperty.Create(nameof(ItemSpacing), typeof(double), typeof(SkeletonListView), 0.0);

    public int Count
    {
        get => (int)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public SkeletonType Type
    {
        get => (SkeletonType)GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    public double ItemSpacing
    {
        get => (double)GetValue(ItemSpacingProperty);
        set => SetValue(ItemSpacingProperty, value);
    }

    public SkeletonListView()
    {
        InitializeComponent();
    }

    private static void OnCountChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkeletonListView view)
        {
            view.GenerateSkeletonItems();
        }
    }

    private static void OnTypeChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is SkeletonListView view)
        {
            view.GenerateSkeletonItems();
        }
    }

    private void GenerateSkeletonItems()
    {
        if (Content is not VerticalStackLayout container)
            return;

        container.Children.Clear();

        for (int i = 0; i < Count; i++)
        {
            var skeletonLoader = new SkeletonLoader
            {
                Type = Type
            };
            container.Children.Add(skeletonLoader);
        }
    }
}
