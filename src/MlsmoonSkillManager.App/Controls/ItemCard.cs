using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MlsmoonSkillManager.App.Controls;

public class ItemCard : HeaderedContentControl
{
    static ItemCard() => ThemeChrome.Default<ItemCard>();

    public ItemCard() => ThemeChrome.UseAppStyle(this);

    public static readonly DependencyProperty BadgeProperty = DependencyProperty.Register(
        nameof(Badge),
        typeof(object),
        typeof(ItemCard));

    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status),
        typeof(object),
        typeof(ItemCard));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(ItemCard),
        new PropertyMetadata(""));

    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions),
        typeof(object),
        typeof(ItemCard));

    public static readonly DependencyProperty IsNestedProperty = DependencyProperty.Register(
        nameof(IsNested),
        typeof(bool),
        typeof(ItemCard),
        new PropertyMetadata(false));

    public static readonly DependencyProperty ShowDescriptionProperty = DependencyProperty.Register(
        nameof(ShowDescription),
        typeof(bool),
        typeof(ItemCard),
        new PropertyMetadata(true));

    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
        nameof(IsLoading),
        typeof(bool),
        typeof(ItemCard),
        new PropertyMetadata(false, OnIsLoadingChanged));

    public static readonly DependencyProperty LoadTextProperty = DependencyProperty.Register(
        nameof(LoadText),
        typeof(string),
        typeof(ItemCard),
        new PropertyMetadata(""));

    public static readonly DependencyProperty LoadProgressProperty = DependencyProperty.Register(
        nameof(LoadProgress),
        typeof(double),
        typeof(ItemCard),
        new PropertyMetadata(0d));

    public object? Badge
    {
        get => GetValue(BadgeProperty);
        set => SetValue(BadgeProperty, value);
    }

    public object? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public bool IsNested
    {
        get => (bool)GetValue(IsNestedProperty);
        set => SetValue(IsNestedProperty, value);
    }

    public bool ShowDescription
    {
        get => (bool)GetValue(ShowDescriptionProperty);
        set => SetValue(ShowDescriptionProperty, value);
    }

    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    public string LoadText
    {
        get => (string)GetValue(LoadTextProperty);
        set => SetValue(LoadTextProperty, value);
    }

    public double LoadProgress
    {
        get => (double)GetValue(LoadProgressProperty);
        set => SetValue(LoadProgressProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        ApplyLoadingState(false);
        VisualStateManager.GoToState(this, IsMouseOver && !IsLoading ? "MouseOver" : "Normal", false);
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        if (!IsLoading)
        {
            VisualStateManager.GoToState(this, "MouseOver", true);
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        VisualStateManager.GoToState(this, "Normal", true);
    }

    private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ItemCard)d).ApplyLoadingState(true);
    }

    private void ApplyLoadingState(bool animate)
    {
        VisualStateManager.GoToState(this, IsLoading ? "Loading" : "Ready", animate);
        if (IsLoading)
        {
            VisualStateManager.GoToState(this, "Normal", false);
        }
    }
}
